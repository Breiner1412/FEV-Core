using System.Net;
using System.Net.Http.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// Emitir sin emisor configurado (RF-05).
///
/// Vive en su propia clase porque necesita una base de datos SIN emisor, y
/// casi todas las demas pruebas empiezan configurandolo. Con la fabrica
/// compartida bastaria que otra prueba corriera antes para que esta dejara
/// de probar lo que dice probar.
///
/// El hueco lo destapo la revision de trazabilidad de H8: RF-05 es un
/// requerimiento "Debe" y no lo verificaba nadie. Una guarda sin prueba se
/// pudre en silencio, porque nada avisa cuando deja de guardar.
/// </summary>
public sealed class EmisionSinEmisorTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    [Fact]
    public async Task No_se_emite_una_factura_sin_emisor_configurado()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        Assert.Equal(
            "EMISOR_INCOMPLETO",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Rechazar_por_falta_de_emisor_no_consume_consecutivo()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var antes = await NumerosDisponibles(cliente);

        await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        var despues = await NumerosDisponibles(cliente);

        // Un consecutivo gastado no vuelve. Si el emisor se comprobara
        // DESPUES de tomar el numero, cada intento fallido abriria un hueco
        // en la numeracion, y los huecos hay que justificarlos ante la DIAN.
        Assert.Equal(antes, despues);
    }

    /// <summary>Cuantos numeros quedan en el primer rango (RF-10).</summary>
    private static async Task<long> NumerosDisponibles(HttpClient cliente)
    {
        var rangos = await LeerJson(await cliente.GetAsync(RutaRangos));

        return rangos.EnumerateArray()
            .First()
            .GetProperty("numerosDisponibles")
            .GetInt64();
    }
}
