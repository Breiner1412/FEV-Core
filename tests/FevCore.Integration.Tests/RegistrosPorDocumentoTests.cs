using System.Net.Http.Json;
using FevCore.Application.Salida;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// RNF-10: dado un identificador de documento, se recuperan todos sus
/// registros, de la peticion que lo creo al ultimo paso del trabajador.
///
/// La revision de trazabilidad de H8 lo daba por verificado con una prueba
/// que solo miraba el traceId de las respuestas de error. Ningun registro
/// llevaba el documento, y la consola ni siquiera escribia los scopes: el
/// traceId que anadia MiddlewareCorrelacion no aparecia en ninguna parte.
///
/// En su propia clase: necesita capturar los registros de su propia API.
/// </summary>
public sealed class RegistrosPorDocumentoTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private static bool EsDelDocumento(RegistroCapturado registro, Guid id) =>
        registro.Campos.TryGetValue("documentoId", out var valor)
        && valor?.ToString() == id.ToString();

    /// <summary>RNF-10.</summary>
    [Fact]
    public async Task Todos_los_registros_de_un_documento_se_recuperan_por_su_identificador()
    {
        fabrica.Validacion.Reiniciar();

        var captura = new RegistrosCapturados();

        using var conCaptura = fabrica.WithWebHostBuilder(constructor =>
            constructor.ConfigureTestServices(servicios =>
                servicios.AddSingleton<ILoggerProvider>(captura)));

        var cliente = conCaptura.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", FabricaApiConBaseDeDatos.LlaveDePrueba);

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(
                Referencia(),
                await CrearAdquirente(cliente),
                [(await CrearProducto(cliente), 1m, null, null)]));

        respuesta.EnsureSuccessStatusCode();

        var id = (await LeerJson(respuesta)).GetProperty("id").GetGuid();
        var traceId = respuesta.Headers.GetValues("X-Trace-Id").Single();

        while (true)
        {
            using var alcance = conCaptura.Services.CreateScope();

            if (!await alcance.ServiceProvider.GetRequiredService<ProcesadorTareas>().ProcesarUnaAsync())
            {
                break;
            }
        }

        var delDocumento = captura.Registros.Where(r => EsDelDocumento(r, id)).ToList();

        // La emision: el registro que une el documento con la peticion que
        // lo creo. Desde ahi, el traceId lleva al resto de esa peticion.
        Assert.Contains(delDocumento, r =>
            r.Campos.TryGetValue("traceId", out var traza) && traza?.ToString() == traceId);

        // El trabajador: generar, firmar, transmitir, aprobar.
        Assert.Contains(delDocumento, r => r.Categoria == typeof(ProcesadorTareas).FullName);

        // Y TODOS los registros del trabajador llevan el documento, no solo
        // los que alguien se acordo de escribir con el.
        var delTrabajador = captura.Registros
            .Where(r => r.Categoria == typeof(ProcesadorTareas).FullName)
            .ToList();

        Assert.NotEmpty(delTrabajador);
        Assert.All(delTrabajador, r => Assert.True(
            EsDelDocumento(r, id), $"Registro del trabajador sin documento: {r.Mensaje}"));
    }

    /// <summary>
    /// RNF-10, la otra mitad: lo capturado arriba tiene que llegar a la
    /// salida real. La consola por defecto escribe texto y descarta los
    /// scopes, y por los scopes es por donde viajan el traceId y el
    /// documento.
    /// </summary>
    [Fact]
    public void La_consola_escribe_registros_estructurados_con_sus_scopes()
    {
        var consola = fabrica.Services
            .GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;

        var formato = fabrica.Services
            .GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>().CurrentValue;

        Assert.Equal(ConsoleFormatterNames.Json, consola.FormatterName);
        Assert.True(formato.IncludeScopes);
    }
}
