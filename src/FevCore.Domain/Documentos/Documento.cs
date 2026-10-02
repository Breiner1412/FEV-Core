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
    private readonly List<Transmision> _transmisiones;
    private readonly List<string> _erroresValidacion;

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

    // ── Resultado de la generacion del XML (H5) ──

    /// <summary>
    /// CUFE: el codigo unico del documento. Nulo hasta que se genera el XML,
    /// porque se calcula sobre los valores tal como quedan escritos en el.
    /// </summary>
    public string? CodigoUnico { get; private set; }

    /// <summary>
    /// El XML generado, guardado tal cual (RF-16, RF-25).
    ///
    /// Se conserva y no se vuelve a generar bajo demanda porque en H6 se
    /// firmara, y una firma vale para unos bytes concretos. Regenerar el XML
    /// mas tarde podria producir algo equivalente pero distinto byte a byte,
    /// y eso invalidaria la firma.
    /// </summary>
    public string? Xml { get; private set; }

    /// <summary>
    /// El XML con la firma digital incrustada (RF-17, RF-25).
    ///
    /// Se guarda aparte del generado y no lo reemplaza. Depurar un problema
    /// de firma casi siempre exige comparar lo que se firmo con lo que
    /// quedo firmado; con una sola columna, el original se pierde y con el
    /// la posibilidad de reproducir el calculo.
    /// </summary>
    public string? XmlFirmado { get; private set; }

    public IReadOnlyList<Linea> Lineas => _lineas;
    public Totales Totales { get; }

    /// <summary>
    /// Historial completo de cambios de estado, del mas antiguo al mas
    /// reciente (RF-23).
    /// </summary>
    public IReadOnlyList<TransicionEstado> Transiciones => _transiciones;

    /// <summary>
    /// Los impuestos del documento por grupo de tipo y tarifa, cada uno
    /// redondeado una vez (RN-06, ADR-0016). Metodo y no propiedad: se
    /// calcula de las lineas, no se guarda.
    /// </summary>
    public IReadOnlyList<SubtotalImpuesto> ImpuestosPorGrupo() =>
        SubtotalImpuesto.Agrupar(_lineas);

    /// <summary>Cada intento de entrega al servicio de validacion (RF-18).</summary>
    public IReadOnlyList<Transmision> Transmisiones => _transmisiones;

    /// <summary>Errores devueltos por la autoridad al rechazar (RF-21).</summary>
    public IReadOnlyList<string> ErroresValidacion => _erroresValidacion;

    /// <summary>
    /// El identificador con el que la autoridad conoce este documento.
    ///
    /// Se deduce de las transmisiones en vez de guardarse aparte: si fuera
    /// un campo propio, podria quedar desalineado con la transmision que
    /// realmente lo produjo.
    /// </summary>
    public string? IdentificadorSeguimiento => _transmisiones
        .Where(t => t.Resultado == ResultadoTransmision.Aceptada)
        .Select(t => t.IdentificadorSeguimiento)
        .LastOrDefault();

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
        _transmisiones = [];
        _erroresValidacion = [];
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
        _transmisiones = [];
        _erroresValidacion = [];

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
    /// Empieza el procesamiento: el documento pasa a EnProceso ANTES de
    /// generar su XML.
    ///
    /// Es lo que la seccion 6.1 dice que significa EN_PROCESO: "se esta
    /// generando o firmando el XML". Y es lo unico que da salida a un fallo
    /// al generar, porque la seccion 6.2 solo lleva a FALLIDO desde
    /// EN_PROCESO. Ver la nota de RegistrarXmlGenerado.
    /// </summary>
    public void IniciarProceso(DateTimeOffset momento) =>
        Transicionar(EstadoDocumento.EnProceso, "Inicia la generacion del XML.", momento);

    /// <summary>
    /// Guarda el XML generado y su codigo unico.
    ///
    /// Hasta la auditoria final este metodo tambien avanzaba el documento a
    /// EnProceso, los tres efectos juntos. La razon era evitar un documento
    /// en EnProceso sin XML, que parecia un estado sin significado. El
    /// razonamiento estaba equivocado por dos lados: la seccion 6.1 define
    /// EN_PROCESO precisamente como "se esta generando", o sea, todavia sin
    /// XML; y al transicionar solo despues de generar con exito, un fallo al
    /// generar dejaba el documento en RECIBIDO, desde donde la maquina de
    /// estados no permite FALLIDO. Evitaba un estado que si significa algo a
    /// cambio de dejar documentos sin salida.
    ///
    /// Ahora la transicion la hace IniciarProceso, antes de generar, y aqui
    /// solo se exige que ya haya ocurrido.
    /// </summary>
    public void RegistrarXmlGenerado(string xml, string codigoUnico)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ExcepcionDominio(
                "XML_VACIO",
                "No se puede registrar un XML vacio.");
        }

        if (string.IsNullOrWhiteSpace(codigoUnico))
        {
            throw new ExcepcionDominio(
                "CODIGO_UNICO_VACIO",
                "El XML debe venir acompanado de su codigo unico.");
        }

        // El XML se genera UNA vez. Volver a generarlo despues de firmado
        // invalidaria la firma, y despues de transmitido dejaria al sistema
        // guardando algo distinto de lo que la autoridad recibio.
        if (Xml is not null)
        {
            throw new ExcepcionDominio(
                "XML_YA_GENERADO",
                $"El documento {NumeroCompleto} ya tiene XML generado. " +
                "Un documento se representa de una sola forma.");
        }

        if (Estado != EstadoDocumento.EnProceso)
        {
            throw new ExcepcionDominio(
                "ESTADO_NO_PERMITE_GENERAR",
                $"El documento {NumeroCompleto} esta en {Estado}. El XML se " +
                "registra con el documento en EnProceso: primero IniciarProceso.");
        }

        Xml = xml;
        CodigoUnico = codigoUnico;
    }

    /// <summary>
    /// Guarda el XML ya firmado (RF-17).
    ///
    /// NO cambia el estado. Firmar no es una transicion de la maquina de
    /// estados: el documento sigue EN_PROCESO, que es precisamente el estado
    /// que la seccion 6.1 define como "se esta generando o firmando el XML".
    /// La marca de tiempo de la firma vive dentro del propio XAdES.
    /// </summary>
    public void RegistrarFirma(string xmlFirmado)
    {
        if (string.IsNullOrWhiteSpace(xmlFirmado))
        {
            throw new ExcepcionDominio(
                "XML_VACIO",
                "No se puede registrar una firma sobre un XML vacio.");
        }

        if (Xml is null)
        {
            throw new ExcepcionDominio(
                "XML_NO_DISPONIBLE",
                $"El documento {NumeroCompleto} no tiene XML generado. " +
                "No hay nada que firmar.");
        }

        // Una firma vale para unos bytes concretos. Volver a firmar produciria
        // otra firma sobre el mismo documento, y en H7 habria dos candidatos
        // a "lo que se transmitio".
        if (XmlFirmado is not null)
        {
            throw new ExcepcionDominio(
                "XML_YA_FIRMADO",
                $"El documento {NumeroCompleto} ya esta firmado.");
        }

        if (Estado != EstadoDocumento.EnProceso)
        {
            throw new ExcepcionDominio(
                "ESTADO_NO_PERMITE_FIRMAR",
                $"El documento {NumeroCompleto} esta en {Estado}. Solo se firma " +
                "un documento en EnProceso.");
        }

        XmlFirmado = xmlFirmado;
    }

    /// <summary>
    /// Deja constancia de un intento de entrega (RF-18, RNF-05).
    ///
    /// Si el servicio la acepto, el documento avanza a Transmitido. En
    /// cualquier otro caso el intento queda registrado y el documento no se
    /// mueve: sera la bandeja de salida quien decida si reintentar o darlo
    /// por fallido.
    /// </summary>
    public Transmision RegistrarTransmision(
        ResultadoTransmision resultado,
        DateTimeOffset momento,
        string? identificadorSeguimiento = null,
        string? respuestaCruda = null)
    {
        var transmision = Transmision.Registrar(
            numeroIntento: _transmisiones.Count + 1,
            enviadaEn: momento,
            resultado: resultado,
            identificadorSeguimiento: identificadorSeguimiento,
            respuestaCruda: respuestaCruda);

        _transmisiones.Add(transmision);

        if (resultado == ResultadoTransmision.Aceptada)
        {
            Transicionar(
                EstadoDocumento.Transmitido,
                "Entregado al servicio de validacion.",
                momento,
                detalle: identificadorSeguimiento);
        }

        return transmision;
    }

    /// <summary>La autoridad valido el documento (RF-19).</summary>
    public void RegistrarAprobacion(DateTimeOffset momento) =>
        Transicionar(
            EstadoDocumento.Aprobado,
            "Validado por la autoridad.",
            momento,
            detalle: IdentificadorSeguimiento);

    /// <summary>
    /// La autoridad rechazo el documento (RF-19, RF-21).
    ///
    /// Los errores se conservan: son lo que el integrador necesita para
    /// corregir y emitir uno nuevo, porque un rechazado no se retransmite
    /// (RN-07).
    /// </summary>
    public void RegistrarRechazo(IEnumerable<string> errores, DateTimeOffset momento)
    {
        var lista = errores?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? [];

        if (lista.Count == 0)
        {
            throw new ExcepcionDominio(
                "RECHAZO_SIN_ERRORES",
                "Un rechazo sin errores no le dice nada al integrador. La " +
                "autoridad siempre indica por que rechaza.");
        }

        _erroresValidacion.Clear();
        _erroresValidacion.AddRange(lista);

        Transicionar(
            EstadoDocumento.Rechazado,
            "Rechazado por la autoridad.",
            momento,
            detalle: $"{lista.Count} error(es) de validacion.");
    }

    /// <summary>
    /// El sistema no logro completar el proceso (RN-13).
    ///
    /// FALLIDO no significa que el documento no llegara: significa que NO SE
    /// SABE. Si hubo una transmision sin respuesta, la autoridad pudo
    /// haberlo recibido. Por eso exige revision manual antes de emitir un
    /// reemplazo, y por eso las transmisiones se conservan todas.
    /// </summary>
    public void RegistrarFallo(string motivo, DateTimeOffset momento) =>
        Transicionar(
            EstadoDocumento.Fallido,
            motivo,
            momento,
            detalle: HuboEnvioSinRespuesta
                ? "RESULTADO DESCONOCIDO: hubo al menos un envio sin respuesta. " +
                  "El documento pudo haber llegado a la autoridad."
                : "No se completo el proceso. No consta que el documento llegara.");

    /// <summary>
    /// Si algun intento salio sin que volviera respuesta. Es lo que separa
    /// "no llego" de "no se sabe" (RN-13, INV-TRM-02).
    /// </summary>
    public bool HuboEnvioSinRespuesta =>
        _transmisiones.Any(t => t.Resultado == ResultadoTransmision.SinRespuesta);

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
