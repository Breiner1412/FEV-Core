using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace FevCore.Integration.Tests;

/// <summary>
/// La llave publicada de desarrollo existe en Development (RNF-01).
///
/// Es la contraparte de
/// EndpointsSoloDesarrolloTests.En_produccion_la_llave_publicada_de_desarrollo_no_autentica:
/// sin ella, aquella pasaria tambien si la llave no se sembrara en ningun
/// entorno.
///
/// En su propia clase, con su propia base: arrancar en Development siembra la
/// llave, y en una base compartida la prueba de Production dependeria del
/// orden de ejecucion.
/// </summary>
public sealed class SemillaDesarrolloTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    public const string LlaveDeDesarrollo = "fev_desarrollo_no_usar_en_produccion";

    /// <summary>Arranca la API en ese entorno y hace una consulta con la llave publicada.</summary>
    public static async Task<HttpStatusCode> ConsultarConLaLlaveDeDesarrolloEn(
        FabricaApiConBaseDeDatos fabrica, string entorno)
    {
        using var enEntorno = fabrica.WithWebHostBuilder(constructor =>
            constructor.UseEnvironment(entorno));

        var cliente = enEntorno.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", LlaveDeDesarrollo);

        return (await cliente.GetAsync(AyudantesPruebas.RutaRangos)).StatusCode;
    }

    /// <summary>RNF-01.</summary>
    [Fact]
    public async Task En_development_la_llave_publicada_de_desarrollo_autentica()
    {
        Assert.Equal(
            HttpStatusCode.OK,
            await ConsultarConLaLlaveDeDesarrolloEn(fabrica, "Development"));
    }
}
