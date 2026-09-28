using System.Net;
using System.Net.Http.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// RN-04 bajo concurrencia.
///
/// Sin bloqueo sobre la factura, dos notas credito simultaneas leen ambas el
/// mismo acumulado —cero—, ambas concluyen que caben, y ambas se guardan: la
/// factura queda acreditada por el doble de su valor. Ninguna restriccion de
/// la base lo impide, porque es una regla aritmetica, no de unicidad. Aqui
/// el bloqueo no es una mejora de disponibilidad como en H3: es lo unico que
/// separa el sistema de un dato contablemente falso.
///
/// Para comprobar que esta prueba sirve, quitar el FOR UPDATE de
/// RepositorioDocumentos.TomarParaActualizarAsync y volver a correrla: las
/// dos notas pasan y la prueba se pone roja.
/// </summary>
public sealed class NotasConcurrentesTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    [Fact]
    public async Task Dos_notas_simultaneas_no_pueden_acreditar_mas_que_la_factura()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarTodosLosRangos(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        // Factura de 2 unidades; cada nota pide las 2. Solo cabe una.
        var (facturaId, total) = await FacturaAprobada(cliente, adquirente, producto);

        var peticiones = new[]
        {
            SolicitudNota(facturaId, producto, cantidad: 2m, Referencia()),
            SolicitudNota(facturaId, producto, cantidad: 2m, Referencia())
        };

        var respuestas = await Task.WhenAll(
            peticiones.Select(p => cliente.PostAsJsonAsync(RutaNotasCredito, p)));

        var aceptadas = respuestas.Count(r => r.StatusCode == HttpStatusCode.Accepted);
        var rechazadas = respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, aceptadas);
        Assert.Equal(1, rechazadas);

        var conflicto = respuestas.First(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(
            "NOTA_EXCEDE_VALOR_FACTURA",
            (await LeerJson(conflicto)).GetProperty("codigo").GetString());
    }

    /// <summary>
    /// Cuatro notas por un cuarto cada una: las cuatro caben justo. Comprueba
    /// que el bloqueo no rechaza de mas, que seria el error opuesto y igual
    /// de grave para el integrador.
    /// </summary>
    [Fact]
    public async Task Cuatro_notas_simultaneas_que_caben_justo_pasan_todas()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarTodosLosRangos(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var (facturaId, _) = await FacturaAprobada(
            cliente, adquirente, producto, cantidad: 4m);

        var peticiones = Enumerable.Range(0, 4)
            .Select(_ => SolicitudNota(facturaId, producto, cantidad: 1m, Referencia()))
            .ToArray();

        var respuestas = await Task.WhenAll(
            peticiones.Select(p => cliente.PostAsJsonAsync(RutaNotasCredito, p)));

        Assert.All(respuestas, r =>
            Assert.Equal(HttpStatusCode.Accepted, r.StatusCode));
    }
}
