using System.Collections.ObjectModel;
using FevCore.Domain.Comun;

namespace FevCore.Domain.Documentos;

/// <summary>
/// La entidad central y raiz de su agregado.
///
/// Todo lo que cuelga de un documento — sus lineas, sus impuestos, sus
/// totales — se carga junto, se guarda junto y queda consistente junto.
/// Nada de afuera modifica una linea sin pasar por aqui (seccion 5 del
/// modelo de dominio).
///
/// Alcance de H1: solo facturas, sin catalogos ni rangos de numeracion.
/// Las notas llegan en H4, cuando exista el documento referenciado.
/// </summary>
public sealed class Documento
{
    private readonly ReadOnlyCollection<Linea> _lineas;

    public Guid Id { get; }
    public TipoDocumento Tipo { get; }

    /// <summary>
    /// Solo el propio documento cambia su estado. En H1 nace y se queda
    /// en Recibido; las transiciones llegan en H4 (INV-DOC-08, RN-11).
    /// </summary>
    public EstadoDocumento Estado { get; private set; }

    /// <summary>
    /// Identificador que el integrador asigna a la operacion. Es lo que
    /// permite reintentar sin duplicar (RF-15, INV-DOC-06).
    /// </summary>
    public string ReferenciaExterna { get; }

    public Guid IntegradorId { get; }

    public string Prefijo { get; }
    public long Consecutivo { get; }

    /// <summary>Prefijo y consecutivo juntos, como aparece en el documento.</summary>
    public string NumeroCompleto => $"{Prefijo}{Consecutivo}";

    public DateTimeOffset FechaEmision { get; }
    public string Moneda { get; }

    public IReadOnlyList<Linea> Lineas => _lineas;
    public Totales Totales { get; }

    private Documento(
        Guid id,
        TipoDocumento tipo,
        EstadoDocumento estado,
        string referenciaExterna,
        Guid integradorId,
        string prefijo,
        long consecutivo,
        DateTimeOffset fechaEmision,
        string moneda,
        ReadOnlyCollection<Linea> lineas,
        Totales totales)
    {
        Id = id;
        Tipo = tipo;
        Estado = estado;
        ReferenciaExterna = referenciaExterna;
        IntegradorId = integradorId;
        Prefijo = prefijo;
        Consecutivo = consecutivo;
        FechaEmision = fechaEmision;
        Moneda = moneda;
        _lineas = lineas;
        Totales = totales;
    }

    /// <summary>
    /// Emite una factura electronica de venta.
    ///
    /// Verifica todas las invariantes antes de devolver: una factura que
    /// existe es una factura valida.
    /// </summary>
    public static Documento EmitirFactura(
        Guid integradorId,
        string referenciaExterna,
        string prefijo,
        long consecutivo,
        DateTimeOffset fechaEmision,
        IEnumerable<Linea> lineas,
        string moneda = "COP")
    {
        if (integradorId == Guid.Empty)
        {
            throw new ExcepcionDominio(
                "INTEGRADOR_REQUERIDO",
                "Todo documento debe registrar que integrador lo emitio.");
        }

        if (string.IsNullOrWhiteSpace(referenciaExterna))
        {
            throw new ExcepcionDominio(
                "REFERENCIA_EXTERNA_REQUERIDA",
                "La referencia externa es obligatoria: es lo que permite " +
                "reintentar una emision sin duplicarla.");
        }

        if (string.IsNullOrWhiteSpace(prefijo))
        {
            throw new ExcepcionDominio(
                "PREFIJO_REQUERIDO",
                "El documento debe tener prefijo de numeracion.");
        }

        if (consecutivo < 1)
        {
            throw new ExcepcionDominio(
                "CONSECUTIVO_INVALIDO",
                $"El consecutivo debe ser mayor que cero. Recibido: {consecutivo}.");
        }

        if (string.IsNullOrWhiteSpace(moneda))
        {
            throw new ExcepcionDominio(
                "MONEDA_REQUERIDA",
                "El documento debe declarar su moneda.");
        }

        var listaLineas = lineas?.ToList() ?? [];

        // INV-DOC-01 (RN-08)
        if (listaLineas.Count == 0)
        {
            throw new ExcepcionDominio(
                "DOCUMENTO_SIN_LINEAS",
                "Un documento debe tener al menos una linea de detalle.");
        }

        var numerosRepetidos = listaLineas
            .GroupBy(l => l.Numero)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (numerosRepetidos.Count > 0)
        {
            throw new ExcepcionDominio(
                "LINEAS_CON_NUMERO_REPETIDO",
                $"Hay mas de una linea con el numero {string.Join(", ", numerosRepetidos)}.");
        }

        var lineasOrdenadas = listaLineas
            .OrderBy(l => l.Numero)
            .ToList()
            .AsReadOnly();

        // INV-DOC-02, RN-09: los totales se derivan de las lineas, siempre.
        var totales = Totales.Calcular(lineasOrdenadas);

        return new Documento(
            // Version 7: incorpora la marca de tiempo, asi que los
            // identificadores quedan ordenados por creacion. Eso hace que
            // los indices de la base de datos no se fragmenten, cosa que si
            // pasa con los identificadores aleatorios de la version 4.
            id: Guid.CreateVersion7(),
            tipo: TipoDocumento.Factura,
            estado: EstadoDocumento.Recibido,
            referenciaExterna: referenciaExterna.Trim(),
            integradorId: integradorId,
            prefijo: prefijo.Trim(),
            consecutivo: consecutivo,
            fechaEmision: fechaEmision,
            moneda: moneda.Trim().ToUpperInvariant(),
            lineas: lineasOrdenadas,
            totales: totales);
    }
}
