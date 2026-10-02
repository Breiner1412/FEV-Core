using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Domain.Tests.Documentos;

/// <summary>
/// Documentos de ejemplo para las pruebas de estados y notas.
///
/// Una factura de referencia vale 357.000: dos unidades a 150.000 con IVA
/// del 19 por ciento. Ese numero aparece en varias pruebas de RN-04, asi que
/// conviene tenerlo en un solo sitio.
/// </summary>
internal static class FabricaDocumentos
{
    public static readonly Guid Integrador = Guid.CreateVersion7();
    public static readonly Guid AdquirenteId = Guid.CreateVersion7();

    public static readonly DateTimeOffset Momento =
        new(2026, 9, 28, 10, 0, 0, TimeSpan.FromHours(-5));

    public static DatosTributarios DatosEmisor() => DatosTributarios.Crear(
        tipoIdentificacion: DatosTributarios.TipoNit,
        identificacion: "800197268",
        razonSocial: "Comercializadora del Eje SAS",
        direccion: "Calle 20 # 8-45",
        municipioCodigo: "66001",
        regimen: "48",
        digitoVerificacion: "4",
        responsabilidades: ["O-13"]);

    public static DatosTributarios DatosAdquirente() => DatosTributarios.Crear(
        tipoIdentificacion: "13",
        identificacion: "1088123456",
        razonSocial: "Juan Perez",
        direccion: "Carrera 10 # 5-20",
        municipioCodigo: "66001",
        regimen: "49");

    public static Linea Linea1(
        decimal cantidad = 2m,
        decimal precioUnitario = 150_000m) =>
        Linea.Crear(
            numero: 1,
            codigo: "PROD-001",
            descripcion: "Teclado mecanico",
            unidadMedida: "94",
            cantidad: cantidad,
            precioUnitario: Dinero.Desde(precioUnitario),
            impuestos: [new EspecificacionImpuesto(TipoImpuesto.Iva, 19m)]);

    /// <summary>Factura en estado Recibido por 357.000.</summary>
    public static Documento Factura(
        string referencia = "VTA-001",
        long consecutivo = 1,
        decimal cantidad = 2m,
        decimal precioUnitario = 150_000m) =>
        Documento.EmitirFactura(
            integradorId: Integrador,
            referenciaExterna: referencia,
            prefijo: "SETP",
            consecutivo: consecutivo,
            fechaEmision: Momento,
            adquirenteId: AdquirenteId,
            emisorSnapshot: DatosEmisor(),
            adquirenteSnapshot: DatosAdquirente(),
            lineas: [Linea1(cantidad, precioUnitario)]);

    /// <summary>
    /// Factura llevada hasta Aprobado recorriendo la maquina de estados.
    ///
    /// No se "pone" en Aprobado: se camina Recibido, EnProceso, Transmitido,
    /// Aprobado. No hay otra forma, y eso es justamente lo que la maquina de
    /// estados garantiza.
    /// </summary>
    public static Documento FacturaAprobada(
        string referencia = "VTA-001",
        long consecutivo = 1,
        decimal cantidad = 2m,
        decimal precioUnitario = 150_000m)
    {
        var factura = Factura(referencia, consecutivo, cantidad, precioUnitario);

        factura.Transicionar(EstadoDocumento.EnProceso, "Generando XML.", Momento);
        factura.Transicionar(EstadoDocumento.Transmitido, "Enviado al validador.", Momento);
        factura.Transicionar(EstadoDocumento.Aprobado, "Validado por la autoridad.", Momento);

        return factura;
    }

    public static Documento NotaCredito(
        Documento factura,
        decimal cantidad = 2m,
        decimal precioUnitario = 150_000m,
        decimal notasPrevias = 0m,
        string referencia = "NC-001") =>
        Documento.EmitirNota(
            tipo: TipoDocumento.NotaCredito,
            integradorId: Integrador,
            referenciaExterna: referencia,
            prefijo: "NCA",
            consecutivo: 1,
            fechaEmision: Momento,
            facturaReferenciada: factura,
            motivo: MotivoNota.DevolucionParcial,
            observaciones: null,
            emisorSnapshot: DatosEmisor(),
            lineas: [Linea1(cantidad, precioUnitario)],
            notasCreditoPrevias: Dinero.Desde(notasPrevias));
}
