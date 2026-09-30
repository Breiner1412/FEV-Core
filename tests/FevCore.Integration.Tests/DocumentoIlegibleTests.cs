using System.Net.Http.Json;
using FevCore.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// Una tarea cuyo documento no se puede cargar termina, en vez de
/// reintentarse para siempre (RNF-05).
///
/// El fallo permanente no es inventado: ConvertidorDinero rechaza al leer
/// un importe negativo, asi que una fila con un total negativo no se puede
/// materializar jamas.
///
/// En su propia clase, con su propia base: si se rompe, su tarea queda
/// abierta y se colaria en el bucle de cualquier otra prueba de tareas, que
/// fallaria por una razon que no es la suya. Ver tambien FalloAlRendirseTests.
/// </summary>
public sealed class DocumentoIlegibleTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    /// <summary>RNF-05, CE-04.</summary>
    [Fact]
    public async Task Una_tarea_cuyo_documento_no_se_puede_cargar_agota_sus_intentos_y_se_cierra()
    {
        fabrica.Validacion.Reiniciar();

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

        using (var alcance = fabrica.Services.CreateScope())
        {
            await alcance.ServiceProvider
                .GetRequiredService<FevCoreDbContext>()
                .Database.ExecuteSqlAsync(
                    $"""UPDATE documentos SET "TotalAPagar" = -1 WHERE "Id" = {id}""");
        }

        await fabrica.ProcesarComoElTrabajadorAsync(vueltas: 10);

        var tarea = await fabrica.TareaDeEmisionAsync(id);

        Assert.True(tarea.Completada, $"La tarea sigue abierta tras {tarea.Intentos} intentos.");
        Assert.Equal(3, tarea.Intentos);
        Assert.False(string.IsNullOrWhiteSpace(tarea.UltimoError));
    }
}
