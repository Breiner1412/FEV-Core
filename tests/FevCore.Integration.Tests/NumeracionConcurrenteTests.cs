using System.Net.Http.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// RNF-06 y CE-03: la asignacion de numeros es correcta bajo concurrencia.
///
/// Esta es la prueba que justifica todo el trabajo de bloqueo de H3. El plan
/// de entregas advierte de la trampa: una prueba de concurrencia que pasa
/// igual con y sin el bloqueo no esta probando nada. Para comprobar que si
/// prueba algo, quitar el FOR UPDATE de RepositorioRangos y volver a
/// correrla: debe ponerse roja.
///
/// Tiene su propia base de datos (su propio IClassFixture) para que ninguna
/// otra clase le consuma numeros del rango mientras corre.
/// </summary>
public sealed class NumeracionConcurrenteTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private const int EmisionesSimultaneas = 25;

    [Fact]
    public async Task Emitir_en_paralelo_no_repite_ni_salta_consecutivos()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        // Todas las peticiones se preparan primero y se lanzan juntas: si se
        // crearan dentro del bucle de envio, el tiempo de armar cada cuerpo
        // las separaria lo suficiente para que no llegaran a competir.
        var peticiones = Enumerable.Range(0, EmisionesSimultaneas)
            .Select(_ => SolicitudFactura(
                Referencia(), adquirente, [(producto, 1m, null, null)]))
            .ToArray();

        var tareas = peticiones.Select(async peticion =>
        {
            var respuesta = await cliente.PostAsJsonAsync(RutaFacturas, peticion);
            respuesta.EnsureSuccessStatusCode();

            return (await LeerJson(respuesta)).GetProperty("consecutivo").GetInt64();
        });

        var consecutivos = await Task.WhenAll(tareas);

        // Sin repetidos: es RN-01, y es lo que el bloqueo impide.
        Assert.Equal(EmisionesSimultaneas, consecutivos.Distinct().Count());

        // Sin saltos: el tramo entregado mide exactamente N numeros. Junto con
        // la comprobacion anterior eso significa que son N consecutivos
        // seguidos. Un salto seria un numero consumido que no llego a ningun
        // documento, y ante la DIAN eso hay que justificar igual que un
        // repetido.
        //
        // No se comprueba que empiecen en 1: las pruebas de esta clase
        // comparten la base de datos y xUnit no garantiza en que orden corren.
        Assert.Equal(
            EmisionesSimultaneas,
            consecutivos.Max() - consecutivos.Min() + 1);
    }

    /// <summary>
    /// El contador del rango queda coherente con lo emitido: RF-10 no puede
    /// informar disponibilidad falsa despues de una rafaga.
    /// </summary>
    [Fact]
    public async Task El_contador_del_rango_refleja_lo_emitido()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var antes = await LeerRangoDeFacturas(cliente);
        var disponiblesAntes = antes.GetProperty("numerosDisponibles").GetInt64();

        const int cuantas = 5;

        for (var i = 0; i < cuantas; i++)
        {
            var respuesta = await cliente.PostAsJsonAsync(
                RutaFacturas,
                SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

            respuesta.EnsureSuccessStatusCode();
        }

        var despues = await LeerRangoDeFacturas(cliente);

        Assert.Equal(
            disponiblesAntes - cuantas,
            despues.GetProperty("numerosDisponibles").GetInt64());
    }

    private static async Task<System.Text.Json.JsonElement> LeerRangoDeFacturas(
        HttpClient cliente)
    {
        var lista = await LeerJson(await cliente.GetAsync(RutaRangos));

        return lista.EnumerateArray()
            .First(r => r.GetProperty("tipoDocumento").GetString() == "FACTURA");
    }
}
