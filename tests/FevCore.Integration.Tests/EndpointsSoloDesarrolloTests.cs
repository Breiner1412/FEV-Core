using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// Lo que solo existe para desarrollo no existe en produccion.
///
/// La lista blanca de entornos de EndpointsDesarrollo solo se habia probado
/// por un lado: que los endpoints estan bajo Testing, porque las pruebas los
/// usan. Que NO esten fuera de ella no lo comprobaba nadie, y es la mitad que
/// importa.
///
/// Generar y firmar por HTTP entran en la lista desde la auditoria final.
/// En H5 y H6 eran la unica forma de disparar esos pasos; desde H7 los hace
/// el trabajador en segundo plano (ADR-0005), y dejarlos abiertos permitia a
/// un integrador competir con el por el mismo documento.
///
/// En su propia clase porque arranca la API con el entorno Production.
/// </summary>
public sealed class EndpointsSoloDesarrolloTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private HttpClient ClienteEnProduccion()
    {
        var enProduccion = fabrica.WithWebHostBuilder(constructor =>
            constructor.UseEnvironment("Production"));

        var cliente = enProduccion.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", FabricaApiConBaseDeDatos.LlaveDePrueba);

        return cliente;
    }

    private static async Task<Guid> EmitirFactura(HttpClient cliente)
    {
        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(
                Referencia(),
                await CrearAdquirente(cliente),
                [(await CrearProducto(cliente), 1m, null, null)]));

        respuesta.EnsureSuccessStatusCode();

        return (await LeerJson(respuesta)).GetProperty("id").GetGuid();
    }

    /// <summary>ADR-0005. Generar y firmar los hace el trabajador, no el integrador.</summary>
    [Theory]
    [InlineData("xml")]
    [InlineData("firma")]
    public async Task En_produccion_no_se_puede_generar_ni_firmar_por_http(string paso)
    {
        var cliente = ClienteEnProduccion();
        var id = await EmitirFactura(cliente);

        var respuesta = await cliente.PostAsync($"{RutaDocumentos}/{id}/{paso}", null);

        // 404 o 405, segun la ruta tenga otro metodo (GET /xml si existe).
        // Lo que no puede ser es que la operacion se ejecute.
        Assert.True(
            respuesta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"POST /{paso} respondio {(int)respuesta.StatusCode} en produccion.");

        var documento = await LeerJson(await cliente.GetAsync($"{RutaDocumentos}/{id}"));

        Assert.Equal("RECIBIDO", documento.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task En_produccion_no_se_puede_forzar_un_estado()
    {
        var cliente = ClienteEnProduccion();
        var id = await EmitirFactura(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/desarrollo/documentos/{id}/estado",
            new { estado = "EN_PROCESO", motivo = "Intento desde produccion." });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// Y bajo Testing si estan: sin esto, las pruebas de arriba pasarian
    /// tambien si los endpoints no existieran en ningun entorno.
    /// </summary>
    [Fact]
    public async Task Fuera_de_produccion_si_se_puede_generar_por_http()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var id = await EmitirFactura(cliente);

        var respuesta = await cliente.PostAsync($"{RutaDocumentos}/{id}/xml", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // ── La llave publicada de desarrollo (RNF-01) ──

    /// <summary>
    /// RNF-01. La llave de desarrollo esta publicada en el README a
    /// proposito, y por eso solo puede existir en Development. Su
    /// contraparte, que en Development si autentica, vive en
    /// SemillaDesarrolloTests: con la misma base, el orden de ejecucion
    /// decidiria si la llave ya estaba sembrada.
    /// </summary>
    [Fact]
    public async Task En_produccion_la_llave_publicada_de_desarrollo_no_autentica()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            await SemillaDesarrolloTests.ConsultarConLaLlaveDeDesarrolloEn(fabrica, "Production"));
    }
}
