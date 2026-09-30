using System.Net.Http.Json;
using FevCore.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// Un documento que no logra generar su XML llega a un estado final
/// (CE-04, seccion 6.2 de los requerimientos).
///
/// La seccion 6.2 lo preve: EN_PROCESO -> FALLIDO "no se pudo generar o
/// firmar". Pero el documento solo pasaba a EN_PROCESO DESPUES de generar el
/// XML, asi que un fallo al generar lo dejaba en RECIBIDO, y desde RECIBIDO
/// la maquina de estados no permite FALLIDO. Sin salida: la tarea se
/// agotaba y el documento se quedaba "recibido" para siempre.
///
/// El fallo que se usa es real: sin el rango que lo numero, no hay clave
/// tecnica con que calcular el codigo unico.
///
/// En su propia clase porque borra los rangos de su base.
/// </summary>
public sealed class FalloAlGenerarTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    /// <summary>CE-04, RN-11.</summary>
    [Fact]
    public async Task Un_documento_que_no_logra_generar_su_xml_queda_fallido()
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
                .Database.ExecuteSqlAsync($"DELETE FROM rangos_numeracion");
        }

        await fabrica.ProcesarComoElTrabajadorAsync(vueltas: 10);

        var documento = await LeerJson(await cliente.GetAsync($"{RutaDocumentos}/{id}"));

        Assert.Equal("FALLIDO", documento.GetProperty("estado").GetString());

        // Nunca se intento entregar: no llego a haber XML que mandar.
        Assert.Equal(0, fabrica.Validacion.TransmisionesIntentadas);
    }
}
