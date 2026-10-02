using System.Net.Http.Json;
using System.Text.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// De donde salen las partes de una nota (ADR-0017).
///
/// El adquirente es parte de la operacion que se corrige: identidad y datos
/// salen de la factura. Antes la identidad salia de la factura y los datos
/// del catalogo actual, una mezcla incoherente de las dos formas.
///
/// El emisor es quien expide el documento nuevo, hoy: sus datos son los
/// actuales.
///
/// En su propia clase porque cambia el emisor, que es uno solo por base.
/// </summary>
public sealed class PartesDeLaNotaTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private static object DatosAdquirente(string direccion) => new
    {
        datos = new
        {
            tipoIdentificacion = "13",
            identificacion = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            razonSocial = "Juan Perez",
            direccion,
            municipioCodigo = "66001",
            regimen = "49"
        }
    };

    private async Task<(HttpClient Cliente, Guid Adquirente, Guid Producto, Guid Factura)> FacturaAprobadaDe()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarTodosLosRangos(cliente);

        var creado = await cliente.PostAsJsonAsync(
            "/api/v1/adquirentes", DatosAdquirente("Carrera 10 # 5-20"));
        creado.EnsureSuccessStatusCode();

        var adquirente = (await LeerJson(creado)).GetProperty("id").GetGuid();
        var producto = await CrearProducto(cliente);
        var (factura, _) = await FacturaAprobada(cliente, adquirente, producto);

        return (cliente, adquirente, producto, factura);
    }

    private static async Task<JsonElement> EmitirNota(HttpClient cliente, Guid factura, Guid producto)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            RutaNotasCredito, SolicitudNota(factura, producto, cantidad: 1m, Referencia()));

        respuesta.EnsureSuccessStatusCode();

        return await LeerJson(respuesta);
    }

    /// <summary>ADR-0017, RN-10.</summary>
    [Fact]
    public async Task El_adquirente_de_la_nota_es_el_de_la_factura_aunque_el_catalogo_haya_cambiado()
    {
        var (cliente, adquirente, producto, factura) = await FacturaAprobadaDe();

        // Despues de facturar, el adquirente se muda y cambia de identificacion.
        (await cliente.PutAsJsonAsync(
            $"/api/v1/adquirentes/{adquirente}", DatosAdquirente("Avenida Nueva # 1-1")))
            .EnsureSuccessStatusCode();

        var nota = await EmitirNota(cliente, factura, producto);
        var original = await LeerJson(await cliente.GetAsync($"{RutaDocumentos}/{factura}"));

        Assert.Equal(
            original.GetProperty("adquirenteId").GetGuid(),
            nota.GetProperty("adquirenteId").GetGuid());

        // Los datos, todos, son los que declaro la factura.
        Assert.Equal(
            original.GetProperty("adquirente").GetRawText(),
            nota.GetProperty("adquirente").GetRawText());
    }

    /// <summary>ADR-0017. El emisor expide la nota hoy, con sus datos de hoy.</summary>
    [Fact]
    public async Task El_emisor_de_la_nota_es_el_actual_aunque_haya_cambiado_desde_la_factura()
    {
        var (cliente, _, producto, factura) = await FacturaAprobadaDe();

        (await cliente.PutAsJsonAsync("/api/v1/emisor", new
        {
            datos = new
            {
                tipoIdentificacion = "31",
                identificacion = "800197268",
                digitoVerificacion = "4",
                razonSocial = "Comercializadora del Eje SAS",
                direccion = "Nueva sede, Calle 50 # 10-10",
                municipioCodigo = "66001",
                regimen = "48",
                responsabilidades = new[] { "O-13" }
            }
        })).EnsureSuccessStatusCode();

        var nota = await EmitirNota(cliente, factura, producto);

        Assert.Equal(
            "Nueva sede, Calle 50 # 10-10",
            nota.GetProperty("emisor").GetProperty("direccion").GetString());
    }
}
