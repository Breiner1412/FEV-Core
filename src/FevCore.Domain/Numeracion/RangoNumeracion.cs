using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Domain.Numeracion;

/// <summary>
/// Un conjunto de numeros consecutivos autorizados para emitir documentos.
///
/// Es la UNICA entidad que entrega consecutivos. Nadie mas incrementa el
/// contador, y esa concentracion es lo que permite garantizar RN-01 bajo
/// concurrencia: hay un solo lugar que bloquear (ADR-0009).
///
/// La vigencia se modela con fechas, no con instantes: un rango vale "hasta
/// el 31 de diciembre", no "hasta las 00:00:00 del 31 de diciembre".
/// </summary>
public sealed class RangoNumeracion
{
    public Guid Id { get; }
    public string Prefijo { get; }
    public TipoDocumento TipoDocumento { get; }
    public long NumeroInicial { get; }
    public long NumeroFinal { get; }
    public DateOnly VigenteDesde { get; }
    public DateOnly VigenteHasta { get; }

    /// <summary>
    /// Ultimo numero entregado. Nulo mientras no se haya entregado ninguno.
    /// </summary>
    public long? UltimoAsignado { get; private set; }

    public string NumeroAutorizacion { get; }

    /// <summary>
    /// Clave asociada a la autorizacion. Es necesaria para calcular el
    /// codigo unico del documento, y NO se devuelve en las consultas.
    /// </summary>
    public string ClaveTecnica { get; }

    public DateTimeOffset CreadoEn { get; }

    // ── Valores derivados ──

    /// <summary>Cuantos numeros quedan sin entregar.</summary>
    public long NumerosDisponibles =>
        NumeroFinal - (UltimoAsignado ?? NumeroInicial - 1);

    public bool Agotado => NumerosDisponibles <= 0;

    public bool EstaVigenteEn(DateOnly fecha) =>
        fecha >= VigenteDesde && fecha <= VigenteHasta;

    /// <summary>Dias que faltan para vencer. Negativo si ya vencio.</summary>
    public int DiasParaVencimiento(DateOnly hoy) =>
        VigenteHasta.DayNumber - hoy.DayNumber;

    /// <summary>Requerido por Entity Framework.</summary>
    private RangoNumeracion()
    {
        Prefijo = null!;
        NumeroAutorizacion = null!;
        ClaveTecnica = null!;
    }

    private RangoNumeracion(
        Guid id,
        string prefijo,
        TipoDocumento tipoDocumento,
        long numeroInicial,
        long numeroFinal,
        DateOnly vigenteDesde,
        DateOnly vigenteHasta,
        string numeroAutorizacion,
        string claveTecnica,
        DateTimeOffset creadoEn)
    {
        Id = id;
        Prefijo = prefijo;
        TipoDocumento = tipoDocumento;
        NumeroInicial = numeroInicial;
        NumeroFinal = numeroFinal;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
        NumeroAutorizacion = numeroAutorizacion;
        ClaveTecnica = claveTecnica;
        CreadoEn = creadoEn;
        UltimoAsignado = null;
    }

    public static RangoNumeracion Crear(
        string prefijo,
        TipoDocumento tipoDocumento,
        long numeroInicial,
        long numeroFinal,
        DateOnly vigenteDesde,
        DateOnly vigenteHasta,
        string numeroAutorizacion,
        string claveTecnica,
        DateTimeOffset momento)
    {
        if (string.IsNullOrWhiteSpace(prefijo))
        {
            throw new ExcepcionDominio(
                "RANGO_PREFIJO_REQUERIDO",
                "El rango debe tener prefijo.");
        }

        if (string.IsNullOrWhiteSpace(numeroAutorizacion))
        {
            throw new ExcepcionDominio(
                "RANGO_AUTORIZACION_REQUERIDA",
                "El rango debe indicar su numero de autorizacion.");
        }

        if (string.IsNullOrWhiteSpace(claveTecnica))
        {
            throw new ExcepcionDominio(
                "RANGO_CLAVE_TECNICA_REQUERIDA",
                "El rango debe indicar su clave tecnica.");
        }

        if (numeroInicial < 1)
        {
            throw new ExcepcionDominio(
                "RANGO_LIMITES_INVALIDOS",
                $"El numero inicial debe ser mayor que cero. Recibido: {numeroInicial}.");
        }

        // ── INV-RAN-01 ──
        if (numeroFinal <= numeroInicial)
        {
            throw new ExcepcionDominio(
                "RANGO_LIMITES_INVALIDOS",
                $"El numero final ({numeroFinal}) debe ser mayor que el " +
                $"inicial ({numeroInicial}).");
        }

        if (vigenteHasta < vigenteDesde)
        {
            throw new ExcepcionDominio(
                "RANGO_VIGENCIA_INVALIDA",
                $"La fecha de vencimiento ({vigenteHasta:yyyy-MM-dd}) no puede " +
                $"ser anterior a la de inicio ({vigenteDesde:yyyy-MM-dd}).");
        }

        return new RangoNumeracion(
            id: Guid.CreateVersion7(),
            prefijo: prefijo.Trim().ToUpperInvariant(),
            tipoDocumento: tipoDocumento,
            numeroInicial: numeroInicial,
            numeroFinal: numeroFinal,
            vigenteDesde: vigenteDesde,
            vigenteHasta: vigenteHasta,
            numeroAutorizacion: numeroAutorizacion.Trim(),
            claveTecnica: claveTecnica.Trim(),
            creadoEn: momento);
    }

    /// <summary>
    /// Entrega el siguiente consecutivo y lo marca como usado.
    ///
    /// Es el UNICO lugar donde el contador avanza. Quien llame a este metodo
    /// debe tener la fila bloqueada: el dominio garantiza que el numero es
    /// correcto, pero no puede impedir que dos procesos lean el mismo estado
    /// al mismo tiempo. Eso lo garantiza la transaccion (ADR-0009).
    /// </summary>
    public long TomarSiguienteConsecutivo(DateOnly fecha)
    {
        // ── INV-RAN-04 ──
        if (!EstaVigenteEn(fecha))
        {
            throw new ExcepcionDominio(
                "RANGO_VENCIDO",
                $"El rango {Prefijo} rige del {VigenteDesde:yyyy-MM-dd} al " +
                $"{VigenteHasta:yyyy-MM-dd}, y la fecha de emision es " +
                $"{fecha:yyyy-MM-dd}.");
        }

        if (Agotado)
        {
            throw new ExcepcionDominio(
                "RANGO_AGOTADO",
                $"El rango {Prefijo} no tiene numeros disponibles. " +
                $"Ultimo asignado: {UltimoAsignado}. Final autorizado: {NumeroFinal}.");
        }

        var siguiente = (UltimoAsignado ?? NumeroInicial - 1) + 1;

        UltimoAsignado = siguiente;

        return siguiente;
    }

    /// <summary>
    /// INV-RAN-03: dos rangos del mismo tipo y prefijo no pueden solaparse,
    /// ni en numeros ni en fechas.
    ///
    /// Se comprueba al registrar uno nuevo, contra los que ya existen.
    /// </summary>
    public bool SeSolapaCon(RangoNumeracion otro)
    {
        if (otro.Id == Id)
        {
            return false;
        }

        if (otro.TipoDocumento != TipoDocumento)
        {
            return false;
        }

        var mismosNumeros =
            otro.Prefijo == Prefijo &&
            NumeroInicial <= otro.NumeroFinal &&
            otro.NumeroInicial <= NumeroFinal;

        var mismasFechas =
            VigenteDesde <= otro.VigenteHasta &&
            otro.VigenteDesde <= VigenteHasta;

        // Comparten numeros, o bien rigen al mismo tiempo para el mismo tipo.
        // Lo segundo importa porque la emision no debe tener que elegir entre
        // dos rangos vigentes.
        return mismosNumeros || mismasFechas;
    }
}
