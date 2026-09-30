using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Application.Tests.Documentos;

/// <summary>
/// El resumen que devuelve el listado (RF-24).
///
/// ResumenDocumento existe para no traer documentos enteros en un listado, y
/// ese ahorro tiene un precio: repite la forma del numero completo que ya
/// define Documento. Una duplicacion sin prueba es una divergencia esperando
/// a que alguien cambie uno de los dos lados.
/// </summary>
public sealed class ResumenDocumentoTests
{
    private static ResumenDocumento Crear(string prefijo, long consecutivo) =>
        new(
            Id: Guid.CreateVersion7(),
            Tipo: TipoDocumento.Factura,
            Estado: EstadoDocumento.Recibido,
            Prefijo: prefijo,
            Consecutivo: consecutivo,
            FechaEmision: DateTimeOffset.UtcNow,
            AdquirenteRazonSocial: "Juan Perez",
            TotalAPagar: Dinero.Desde(357000m),
            CodigoUnico: null);

    [Theory]
    [InlineData("SETP", 1L, "SETP1")]
    [InlineData("SETP", 990000123L, "SETP990000123")]
    [InlineData("NCA", 47L, "NCA47")]
    public void El_numero_completo_es_el_prefijo_pegado_al_consecutivo(
        string prefijo, long consecutivo, string esperado)
    {
        Assert.Equal(esperado, Crear(prefijo, consecutivo).NumeroCompleto);
    }

    [Fact]
    public void El_numero_del_resumen_coincide_con_el_del_documento()
    {
        // Es LA prueba de esta clase. Si alguien cambiara el formato en
        // Documento —un guion entre prefijo y consecutivo, un relleno de
        // ceros— el listado seguiria mostrando el formato viejo y nadie se
        // enteraria: los dos lados compilan igual de bien por separado.
        var documento = Documento.EmitirFactura(
            integradorId: Guid.CreateVersion7(),
            referenciaExterna: "VTA-001",
            prefijo: "SETP",
            consecutivo: 990000123,
            fechaEmision: DateTimeOffset.UtcNow,
            adquirenteId: Guid.CreateVersion7(),
            emisorSnapshot: Datos("800197268", "4", "Comercializadora del Eje SAS", "48"),
            adquirenteSnapshot: Datos("1088123456", null, "Juan Perez", "49", tipo: "13"),
            lineas: [Linea.Crear(
                numero: 1,
                codigo: "PROD-001",
                descripcion: "Teclado mecanico",
                unidadMedida: "94",
                cantidad: 1m,
                precioUnitario: Dinero.Desde(150_000m),
                impuestos: [])]);

        var resumen = Crear(documento.Prefijo, documento.Consecutivo);

        Assert.Equal(documento.NumeroCompleto, resumen.NumeroCompleto);
    }

    private static DatosTributarios Datos(
        string identificacion,
        string? dv,
        string razonSocial,
        string regimen,
        string tipo = DatosTributarios.TipoNit) =>
        DatosTributarios.Crear(
            tipoIdentificacion: tipo,
            identificacion: identificacion,
            razonSocial: razonSocial,
            direccion: "Calle 20 # 8-45",
            municipioCodigo: "66001",
            regimen: regimen,
            digitoVerificacion: dv);
}
