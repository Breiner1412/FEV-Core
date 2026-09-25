using System.Net;
using System.Net.Http.Json;
using FevCore.Api.Contratos;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FevCore.Integration.Tests;

/// <summary>
/// Levanta la API completa en memoria y le hace peticiones reales.
/// No usa red ni puertos: el cliente habla directo con la aplicacion.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _fabrica;

    public HealthEndpointTests(WebApplicationFactory<Program> fabrica)
    {
        _fabrica = fabrica;
    }

    [Fact]
    public async Task Health_responde_200()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Health_reporta_estado_ok()
    {
        var cliente = _fabrica.CreateClient();

        var cuerpo = await cliente.GetFromJsonAsync<RespuestaSalud>("/health");

        Assert.NotNull(cuerpo);
        Assert.Equal("ok", cuerpo.Estado);
    }

    [Fact]
    public async Task Health_no_expone_rutas_desconocidas()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/health/detalle");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
