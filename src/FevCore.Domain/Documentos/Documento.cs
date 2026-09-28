using FevCore.Domain.Comun;

namespace FevCore.Domain.Documentos;

/// <summary>
/// La entidad central y raiz de su agregado.
///
/// Todo lo que cuelga de un documento — sus lineas, sus impuestos, sus
/// totales, su historial de estados — se carga junto, se guarda junto y
/// queda consistente junto. Nada de afuera modifica una linea ni un estado
/// sin pasar por aqui (seccion 5 del modelo de dominio).
/// </summary>
public sealed class Documento
{
    private readonly List<Linea> _lineas;
    private readonly List<TransicionEstado> _transiciones;

    public Guid Id { get; }
    public TipoDocumento Tipo { get; }

    /// <summary>
    /// Solo el propio documento cambia su estado, y solo por Transicionar
    /// (INV-DOC-08, RN-11).
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

    /// <summary>
    /// Referencia al adquirente del catalogo. Informativa: el documento no
    /// depende de que siga existiendo ni de que sus datos no cambien.
    /// </summary>
    public Guid AdquirenteId { get; }

    /// <summary>
    /// COPIA de los datos del emisor al momento de emitir.
    ///
    /// Si el emisor cambia de direccion manana, este documento sigue
    /// declarando la de hoy, que es la que se firmo (RN-10).
    /// </summary>
    public DatosTributarios EmisorSnapshot { get; }

    /// <summary>
    /// COPIA de los datos del adquirente al momento de emitir.
    /// Ver la nota de EmisorSnapshot.
    /// </summary>
    public DatosTributarios AdquirenteSnapshot { get; }

    // ── Solo en notas ──

    /// <summary>
    /// Factura que esta nota corrige. Obligatorio en notas, vacio en
    /// facturas (INV-DOC-03, INV-DOC-04).
    /// </summary>
    public Guid? DocumentoReferenciadoId { get; }

    /// <summary>Por que se emitio la nota. Obligatorio en notas.</summary>
    public MotivoNota? Motivo { get; }

    public string? Observaciones { get; }

    public bool EsNota => Tipo is TipoDocumento.NotaCredito or TipoDocumento.NotaDebito;

    public IReadOnlyList<Linea> Lineas => _lineas;
    public Totales Totales { get; }

    /// <summary>
    /// Historial completo de cambios de estado, del mas antiguo al mas
    /// reciente (RF-23).
    /// </summary>
    public IReadOnlyList<TransicionEstado> Transiciones => _transiciones;

    /// <summary>
    /// Requerido por Entity Framework para reconstruir el documento desde la
    /// base de datos. EF asigna las propiedades por reflexion despues de
    /// llamarlo, asi que los valores de aqui son solo para satisfacer al
    /// compilador. El dominio NUNCA usa este constructor: las unicas
    /// entradas son EmitirFactura y EmitirNota, que verifican todas las
    /// invariantes.
    /// </summary>
    private Documento()
    {
        _lineas = [];
        _transiciones = [];
        ReferenciaExterna = null!;
        Prefijo = null!;
        Moneda = null!;
        Totales = null!;
        EmisorSnapshot = null!;
        AdquirenteSnapshot = null!;
    }

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
        Guid adquirenteId,
        DatosTributarios emisorSnapshot,
        DatosTributarios adquirenteSnapshot,
        Guid? documentoReferenciadoId,
        MotivoNota? motivo,
        string? observaciones,
        List<Linea> lineas,
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
        AdquirenteId = adquirenteId;
        EmisorSnapshot = emisorSnapshot;
        AdquirenteSnapshot = adquirenteSnapshot;
        DocumentoReferenciadoId = documentoReferenciadoId;
        Motivo = motivo;
        Observaciones = observaciones;
        _lineas = lineas;
        Totales = totales;

        // El nacimiento tambien queda registrado, con EstadoAnterior nulo.
        // Asi el historial de RF-23 esta completo desde la primera consulta
        // y se cumple INV-TRA-02 desde el principio: cada transicion empalma
        // con la anterior sin que falte el eslabon inicial.
        _transiciones =
        [
            TransicionEstado.Registrar(
                secuencia: 1,
                estadoAnterior: null,
                estadoNuevo: estado,
                ocurridaEn: fechaEmision,
                motivo: "Documento recibido y numerado.")
        ];
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
        Guid adquirenteId,
        DatosTributarios emisorSnapshot,
        DatosTributarios adquirenteSnapshot,
        IEnumerable<Linea> lineas,
        string moneda = "COP")
    {
        var (lineasOrdenadas, totales) = ValidarBase(
            integradorId, referenciaExterna, prefijo, consecutivo,
            adquirenteId, moneda, lineas);

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
            adquirenteId: adquirenteId,
            emisorSnapshot: emisorSnapshot,
            adquirenteSnapshot: adquirenteSnapshot,

            // INV-DOC-04 (RN-05): una factura no referencia nada.
            documentoReferenciadoId: null,
            motivo: null,
            observaciones: null,

            lineas: lineasOrdenadas,
            totales: totales);
    }

    /// <summary>
    /// Emite una nota credito o debito contra una factura existente.
    ///
    /// Recibe la factura ENTERA, no solo su identificador. Es deliberado:
    /// con el identificador solo, comprobar que sea una factura aprobada
    /// tendria que hacerse fuera del dominio, y la invariante dejaria de
    /// estar donde puede defenderse sola (INV-DOC-03, RN-03, RN-05).
    ///
    /// notasCreditoPrevias es la unica cifra que el documento no puede
    /// averiguar por si mismo: cuanto suman las otras notas credito de esa
    /// factura. La aplicacion la calcula y la entrega; la regla de que no se
    /// exceda el total sigue viviendo aqui (RN-04).
    /// </summary>
    public static Documento EmitirNota(
        TipoDocumento tipo,
        Guid integradorId,
        string referenciaExterna,
        string prefijo,
        long consecutivo,
        DateTimeOffset fechaEmision,
        Documento facturaReferenciada,
        MotivoNota motivo,
        string? observaciones,
        DatosTributarios emisorSnapshot,
        DatosTributarios adquirenteSnapshot,
        IEnumerable<Linea> lineas,
        Dinero notasCreditoPrevias)
    {
        if (tipo is not (TipoDocumento.NotaCredito or TipoDocumento.NotaDebito))
        {
            throw new ExcepcionDominio(
                "TIPO_NOTA_INVALIDO",
                $"EmitirNota solo produce notas credito o debito. Recibido: {tipo}.");
        }

        ArgumentNullException.ThrowIfNull(facturaReferenciada);

        // INV-DOC-03 y RN-05: solo se referencia una factura, nunca otra nota.
        if (facturaReferenciada.Tipo != TipoDocumento.Factura)
        {
            throw new ExcepcionDominio(
                "DOCUMENTO_REFERENCIADO_INVALIDO",
                $"Una nota solo puede referenciar una factura. El documento " +
                $"{facturaReferenciada.NumeroCompleto} es de tipo {facturaReferenciada.Tipo}.");
        }

        // INV-DOC-03 y RN-03: la factura debe estar aprobada por la autoridad.
        if (facturaReferenciada.Estado != EstadoDocumento.Aprobado)
        {
            throw new ExcepcionDominio(
                "DOCUMENTO_REFERENCIADO_NO_APROBADO",
                $"La factura {facturaReferenciada.NumeroCompleto} esta en estado " +
                $"{facturaReferenciada.Estado}. Solo se puede corregir una factura " +
                "aprobada por la autoridad.");
        }

        var (lineasOrdenadas, totales) = ValidarBase(
            integradorId, referenciaExterna, prefijo, consecutivo,
            facturaReferenciada.AdquirenteId, facturaReferenciada.Moneda, lineas);

        // RN-04: el acumulado de notas credito no puede superar la factura.
        // No aplica a las notas debito, que aumentan el valor.
        if (tipo == TipoDocumento.NotaCredito)
        {
            var acumulado = notasCreditoPrevias + totales.TotalAPagar;

            if (acumulado > facturaReferenciada.Totales.TotalAPagar)
            {
                throw new ExcepcionDominio(
                    "NOTA_EXCEDE_VALOR_FACTURA",
                    $"Esta nota llevaria el acumulado de notas credito a {acumulado}, " +
                    $"y la factura {facturaReferenciada.NumeroCompleto} vale " +
                    $"{facturaReferenciada.Totales.TotalAPagar}. Ya hay " +
                    $"{notasCreditoPrevias} en notas anteriores.");
            }
        }

        return new Documento(
            id: Guid.CreateVersion7(),
            tipo: tipo,
            estado: EstadoDocumento.Recibido,
            referenciaExterna: referenciaExterna.Trim(),
            integradorId: integradorId,
            prefijo: prefijo.Trim(),
            consecutivo: consecutivo,
            fechaEmision: fechaEmision,

            // La nota hereda la moneda y el adquirente de la factura: no se
            // corrige una factura en otra moneda ni a nombre de otro.
            moneda: facturaReferenciada.Moneda,
            adquirenteId: facturaReferenciada.AdquirenteId,

            emisorSnapshot: emisorSnapshot,
            adquirenteSnapshot: adquirenteSnapshot,
            documentoReferenciadoId: facturaReferenciada.Id,
            motivo: motivo,
            observaciones: string.IsNullOrWhiteSpace(observaciones)
                ? null
                : observaciones.Trim(),
            lineas: lineasOrdenadas,
            totales: totales);
    }

    /// <summary>
    /// Lleva el documento a un estado nuevo y lo deja registrado.
    ///
    /// Es la UNICA forma de cambiar el estado. Todo lo que exige RN-11 y
    /// RN-12 esta aqui dentro, asi que ningun camino puede saltarselo.
    /// </summary>
    public void Transicionar(
        EstadoDocumento nuevoEstado,
        string motivo,
        DateTimeOffset momento,
        string? detalle = null)
    {
        // RN-11: de un estado terminal no se sale nunca mas.
        if (MaquinaEstados.EsTerminal(Estado))
        {
            throw new ExcepcionDominio(
                "ESTADO_TERMINAL",
                $"El documento {NumeroCompleto} esta en {Estado}, que es un estado " +
                "final. Un documento terminado no vuelve a cambiar de estado.");
        }

        // INV-DOC-08: solo las transiciones de la seccion 6.2.
        if (!MaquinaEstados.Permite(Estado, nuevoEstado))
        {
            var posibles = string.Join(", ", MaquinaEstados.DestinosDesde(Estado));

            throw new ExcepcionDominio(
                "TRANSICION_INVALIDA",
                $"No se puede pasar de {Estado} a {nuevoEstado}. " +
                $"Desde {Estado} solo se puede ir a: {posibles}.");
        }

        // RN-12: el registro se crea ANTES de mover el estado, para que
        // capture de donde venia.
        _transiciones.Add(TransicionEstado.Registrar(
            secuencia: _transiciones.Count + 1,
            estadoAnterior: Estado,
            estadoNuevo: nuevoEstado,
            ocurridaEn: momento,
            motivo: motivo,
            detalle: detalle));

        Estado = nuevoEstado;
    }

    /// <summary>
    /// Validaciones y calculos que comparten factura y nota.
    ///
    /// Vive aparte para que las dos entradas no puedan divergir: si manana
    /// se agrega una invariante, la heredan ambas sin que nadie tenga que
    /// acordarse de copiarla.
    /// </summary>
    private static (List<Linea> Lineas, Totales Totales) ValidarBase(
        Guid integradorId,
        string referenciaExterna,
        string prefijo,
        long consecutivo,
        Guid adquirenteId,
        string moneda,
        IEnumerable<Linea> lineas)
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

        if (adquirenteId == Guid.Empty)
        {
            throw new ExcepcionDominio(
                "ADQUIRENTE_REQUERIDO",
                "Todo documento debe identificar a su adquirente.");
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
            .ToList();

        // INV-DOC-02, RN-09: los totales se derivan de las lineas, siempre.
        return (lineasOrdenadas, Totales.Calcular(lineasOrdenadas));
    }
}
