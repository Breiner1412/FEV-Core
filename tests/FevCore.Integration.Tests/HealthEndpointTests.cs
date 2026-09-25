using System.Net;
using System.Net.Http.Json;
using FevCore.Api.Contratos;

namespace FevCore.Integration.Tests;

/// <summary>
/// Levanta la API completa en memoria y le hace peticiones reales.
/// No usa red ni puertos: el cliente habla directo con la aplicacion.
/// </summary>
public sealed class HealthEndpointTests(FabricaApiPruebas fabrica)
    : IClassFixture<FabricaApiPruebas>
{
    [Fact]
    public async Task Health_responde_200()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Health_reporta_estado_ok()
    {
        var cliente = fabrica.CreateClient();

        var cuerpo = await cliente.GetFromJsonAsync<RespuestaSalud>("/health");

        Assert.NotNull(cuerpo);
        Assert.Equal("ok", cuerpo.Estado);
    }

    [Fact]
    public async Task Health_responde_sin_base_de_datos()
    {
        // El chequeo de salud no debe consultar la base de datos: si lo
        // hiciera, una caida de la base haria ver el servicio como muerto
        // cuando en realidad esta en pie y puede seguir recibiendo peticiones.
        //
        // Esta prueba corre sin base de datos disponible. Que pase es la
        // demostracion.
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_ruta_desconocida_no_revela_si_existe()
    {
        // Responde 401, no 404.
        //
        // Distinguir "no existe" de "no autorizado" le permitiria a un
        // desconocido mapear la API entera probando direcciones y leyendo
        // cual devuelve cada cosa. Con la politica de rechazo por defecto,
        // todo lo que no sea explicitamente publico responde igual.
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/ruta/que/no/existe");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
