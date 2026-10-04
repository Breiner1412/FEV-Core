using System.Net.Http.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// Una tarea que falla incluso al rendirse termina, y su documento llega a
/// un estado final (RNF-05, CE-04).
///
/// Si la autoridad devuelve un identificador de seguimiento mas largo que
/// su columna, guardar la transmision falla. El catch intenta guardar la
/// reprogramacion con ese mismo cambio pendiente en el contexto, falla otra
/// vez y la excepcion escapa sin haber pasado por la comprobacion del
/// maximo. En la vuelta siguiente se repite todo. Sin un tope que no dependa
/// de llegar a rendirse, eso no termina nunca.
///
/// En su propia clase, con su propia base: ver DocumentoIlegibleTests.
/// </summary>
public sealed class FalloAlRendirseTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    /// <summary>RNF-05, CE-04.</summary>
    [Fact]
    public async Task Una_tarea_que_falla_incluso_al_rendirse_se_cierra_y_el_documento_queda_fallido()
    {
        fabrica.Validacion.Reiniciar();
        fabrica.Validacion.Modo = ModoValidacion.SeguimientoDemasiadoLargo;

        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        respuesta.EnsureSuccessStatusCode();

        var id = (await LeerJson(respuesta)).GetProperty("id").GetGuid();

        await fabrica.ProcesarComoElTrabajadorAsync(vueltas: 10);

        var tarea = await fabrica.TareaDeEmisionAsync(id);

        Assert.True(tarea.Completada, $"La tarea sigue abierta tras {tarea.Intentos} intentos.");

        var documento = await LeerJson(await cliente.GetAsync($"{RutaDocumentos}/{id}"));

        Assert.Equal("FALLIDO", documento.GetProperty("estado").GetString());
    }
}
