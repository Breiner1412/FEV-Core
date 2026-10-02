using System.Net;
using System.Net.Http.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// RF-15 bajo concurrencia: varias solicitudes con la misma referencia
/// externa, a la vez, producen un solo documento y todas reciben ese
/// documento.
///
/// Es el escenario para el que existe RF-15: el integrador agota su tiempo
/// de espera y reintenta mientras la primera solicitud sigue en curso. Las
/// dos pasan la comprobacion de "ya existe" antes de que ninguna guarde; la
/// segunda espera el bloqueo del rango y despues choca con el indice unico.
/// El indice evitaba el duplicado, pero la segunda respondia 500.
///
/// Tiene su propia base de datos, como toda prueba de concurrencia.
/// </summary>
public sealed class ReintentosSimultaneosTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private const int ReintentosSimultaneos = 10;

    /// <summary>RF-15, INV-DOC-06.</summary>
    [Fact]
    public async Task Reintentos_simultaneos_con_la_misma_referencia_devuelven_el_mismo_documento()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var solicitud = SolicitudFactura(
            Referencia(),
            await CrearAdquirente(cliente),
            [(await CrearProducto(cliente), 1m, null, null)]);

        var disponiblesAntes = await NumerosDisponiblesDeFacturas(cliente);

        var respuestas = await Task.WhenAll(
            Enumerable.Range(0, ReintentosSimultaneos)
                .Select(_ => cliente.PostAsJsonAsync(RutaFacturas, solicitud)));

        Assert.All(respuestas, r => Assert.True(
            r.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK,
            $"Una solicitud respondio {(int)r.StatusCode}."));

        // Una sola creo el documento; las demas lo encontraron hecho.
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Accepted);

        var ids = new HashSet<Guid>();

        foreach (var respuesta in respuestas)
        {
            ids.Add((await LeerJson(respuesta)).GetProperty("id").GetGuid());
        }

        Assert.Single(ids);

        // Y un solo numero consumido: los perdedores revirtieron el suyo.
        Assert.Equal(disponiblesAntes - 1, await NumerosDisponiblesDeFacturas(cliente));
    }

    private static async Task<long> NumerosDisponiblesDeFacturas(HttpClient cliente) =>
        (await LeerJson(await cliente.GetAsync(RutaRangos)))
            .EnumerateArray()
            .Single(r => r.GetProperty("tipoDocumento").GetString() == "FACTURA")
            .GetProperty("numerosDisponibles")
            .GetInt64();
}
