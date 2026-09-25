using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FevCore.Api.Autenticacion;

namespace FevCore.Integration.Tests;

/// <summary>
/// Verifica RF-01: toda peticion sin credencial valida es rechazada.
///
/// Estas pruebas no necesitan base de datos: una peticion sin cabecera se
/// rechaza antes de consultar nada.
/// </summary>
public sealed class AutenticacionTests(FabricaApiPruebas fabrica)
    : IClassFixture<FabricaApiPruebas>
{
    [Fact]
    public async Task Una_peticion_sin_llave_es_rechazada()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/v1/documentos/" + Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_rechazo_usa_el_formato_de_error_del_contrato()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/v1/documentos/" + Guid.NewGuid());

        Assert.Equal(
            "application/problem+json",
            respuesta.Content.Headers.ContentType?.MediaType);

        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("LLAVE_INVALIDA", problema.GetProperty("codigo").GetString());
        Assert.Equal(401, problema.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task El_error_no_expone_detalles_internos()
    {
        // RNF-11: ningun mensaje de error revela rutas, consultas ni trazas.
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/v1/documentos/" + Guid.NewGuid());
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Exception", cuerpo);
        Assert.DoesNotContain("FevCore.Infrastructure", cuerpo);
        Assert.DoesNotContain("Npgsql", cuerpo);
    }

    [Fact]
    public async Task El_chequeo_de_salud_sigue_siendo_publico()
    {
        // Con la politica de rechazo por defecto, /health debe seguir abierto:
        // quien lo consulta es un orquestador, no un integrador con llave.
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public void La_cabecera_es_la_que_dice_el_contrato()
    {
        Assert.Equal("X-Api-Key", ManejadorAutenticacionLlaveApi.Cabecera);
    }
}
