using System.Net.Http.Json;
using FevCore.Application.Abstracciones;
using FevCore.Domain.Salida;
using Microsoft.Extensions.DependencyInjection;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// La toma de tareas contra PostgreSQL real (RNF-04, ADR-0013).
///
/// Estas reglas no se pueden probar en el dominio, porque no viven en el
/// dominio: viven en una consulta SQL con FOR UPDATE SKIP LOCKED. Una prueba
/// con una base en memoria pasaria sin ejecutar nunca la clausula que
/// importa.
///
/// Dos de las demostraciones de H7 son estas: que un proceso muerto a mitad
/// no deja trabajo atascado, y que dos trabajadores no se llevan la misma
/// tarea. El plan de entregas las señala como las mas valiosas del hito, y
/// tiene razon: son las que sostienen CE-04.
/// </summary>
public sealed class BandejaDeSalidaTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private static readonly TimeSpan Generoso = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Emite una factura, que deja una tarea pendiente en la misma
    /// transaccion (ADR-0006), y devuelve el id del documento.
    /// </summary>
    private async Task<Guid> EmitirFactura()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 2m, null, null)]));

        respuesta.EnsureSuccessStatusCode();

        return (await LeerJson(respuesta)).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Cada prueba de esta clase comparte base de datos con las demas, asi
    /// que se empieza dejando la bandeja vacia. Sin esto, una prueba podria
    /// llevarse la tarea que dejo la anterior y afirmar sobre el documento
    /// equivocado.
    /// </summary>
    private async Task VaciarBandeja()
    {
        while (true)
        {
            using var alcance = fabrica.Services.CreateScope();
            var tareas = alcance.ServiceProvider.GetRequiredService<IRepositorioTareas>();

            var tarea = await tareas.TomarSiguienteAsync(
                DateTimeOffset.UtcNow.AddYears(1), TimeSpan.Zero);

            if (tarea is null)
            {
                return;
            }

            tarea.Completar(DateTimeOffset.UtcNow);
            await tareas.GuardarCambiosAsync();
        }
    }

    private static async Task<TareaSalida?> Tomar(
        IServiceScope alcance, DateTimeOffset momento, TimeSpan abandono) =>
        await alcance.ServiceProvider
            .GetRequiredService<IRepositorioTareas>()
            .TomarSiguienteAsync(momento, abandono);

    [Fact]
    public async Task Emitir_deja_una_tarea_lista_para_tomar()
    {
        await VaciarBandeja();
        var documentoId = await EmitirFactura();

        using var alcance = fabrica.Services.CreateScope();
        var tarea = await Tomar(alcance, DateTimeOffset.UtcNow.AddMinutes(1), Generoso);

        Assert.NotNull(tarea);
        Assert.Equal(documentoId, tarea.DocumentoId);
        Assert.Equal(TipoTarea.Emitir, tarea.Tipo);

        // El intento se cuenta al tomarla, no al terminarla.
        Assert.Equal(1, tarea.Intentos);
    }

    [Fact]
    public async Task Una_tarea_tomada_no_se_la_lleva_otro()
    {
        await VaciarBandeja();
        await EmitirFactura();

        var momento = DateTimeOffset.UtcNow.AddMinutes(1);

        using (var primero = fabrica.Services.CreateScope())
        {
            Assert.NotNull(await Tomar(primero, momento, Generoso));
        }

        using var segundo = fabrica.Services.CreateScope();

        // El primero sigue trabajandola. La marca TomadaEn es lo unico que
        // la protege, porque la transaccion que la tomo ya se cerro.
        Assert.Null(await Tomar(segundo, momento, Generoso));
    }

    [Fact]
    public async Task Una_tarea_abandonada_vuelve_sola_a_la_bandeja()
    {
        await VaciarBandeja();
        var documentoId = await EmitirFactura();

        var momento = DateTimeOffset.UtcNow.AddMinutes(1);

        using (var muerto = fabrica.Services.CreateScope())
        {
            Assert.NotNull(await Tomar(muerto, momento, Generoso));
        }

        // Aqui es donde el proceso muere: la tarea queda tomada y nadie va a
        // soltarla nunca. Nada la limpia; lo que pasa es que su marca
        // envejece. Avanzar el reloj media hora es matar el proceso.
        using var relevo = fabrica.Services.CreateScope();

        var recuperada = await Tomar(
            relevo, momento.AddMinutes(30), TimeSpan.FromMinutes(2));

        Assert.NotNull(recuperada);
        Assert.Equal(documentoId, recuperada.DocumentoId);

        // Segundo intento sobre la MISMA tarea, no una nueva. El contador es
        // lo que impide que una tarea que siempre muere se reintente para
        // siempre.
        Assert.Equal(2, recuperada.Intentos);
    }

    [Fact]
    public async Task Una_tarea_reprogramada_espera_su_turno()
    {
        await VaciarBandeja();
        await EmitirFactura();

        var momento = DateTimeOffset.UtcNow.AddMinutes(1);

        using (var alcance = fabrica.Services.CreateScope())
        {
            var tareas = alcance.ServiceProvider.GetRequiredService<IRepositorioTareas>();
            var tarea = await tareas.TomarSiguienteAsync(momento, Generoso);

            tarea!.Reprogramar("el servicio no responde", momento, TimeSpan.FromHours(1));
            await tareas.GuardarCambiosAsync();
        }

        using (var temprano = fabrica.Services.CreateScope())
        {
            // Esta libre, pero todavia no toca. Ignorar la espera creciente
            // convertiria el reintento en un martilleo (RNF-05).
            Assert.Null(await Tomar(temprano, momento.AddSeconds(1), Generoso));
        }

        using var tarde = fabrica.Services.CreateScope();
        Assert.NotNull(await Tomar(tarde, momento.AddMinutes(5), Generoso));
    }

    [Fact]
    public async Task Una_tarea_completada_no_vuelve_a_salir()
    {
        await VaciarBandeja();
        await EmitirFactura();

        var momento = DateTimeOffset.UtcNow.AddMinutes(1);

        using (var alcance = fabrica.Services.CreateScope())
        {
            var tareas = alcance.ServiceProvider.GetRequiredService<IRepositorioTareas>();
            var tarea = await tareas.TomarSiguienteAsync(momento, Generoso);

            tarea!.Completar(momento);
            await tareas.GuardarCambiosAsync();
        }

        using var despues = fabrica.Services.CreateScope();

        // Ni siquiera pasado el tiempo de abandono: completada es completada.
        Assert.Null(await Tomar(despues, momento.AddDays(1), TimeSpan.Zero));
    }

    [Fact]
    public async Task Cuatro_trabajadores_simultaneos_no_se_llevan_la_misma_tarea()
    {
        await VaciarBandeja();
        await EmitirFactura();

        var momento = DateTimeOffset.UtcNow.AddMinutes(1);
        var alcances = Enumerable.Range(0, 4)
            .Select(_ => fabrica.Services.CreateScope())
            .ToList();

        try
        {
            var tomadas = await Task.WhenAll(
                alcances.Select(a => Tomar(a, momento, Generoso)));

            // Exactamente una. Si SKIP LOCKED fallara hacia el lado
            // peligroso, dos trabajadores transmitirian el mismo documento a
            // la DIAN: no dos borradores, dos facturas con el mismo
            // consecutivo.
            Assert.Single(tomadas, t => t is not null);
        }
        finally
        {
            foreach (var alcance in alcances)
            {
                alcance.Dispose();
            }
        }
    }

    [Fact]
    public async Task Tres_trabajadores_simultaneos_se_reparten_tres_tareas()
    {
        await VaciarBandeja();

        for (var i = 0; i < 3; i++)
        {
            await EmitirFactura();
        }

        var momento = DateTimeOffset.UtcNow.AddMinutes(1);
        var alcances = Enumerable.Range(0, 3)
            .Select(_ => fabrica.Services.CreateScope())
            .ToList();

        try
        {
            var tomadas = await Task.WhenAll(
                alcances.Select(a => Tomar(a, momento, Generoso)));

            var conseguidas = tomadas.Where(t => t is not null).ToList();

            // La otra cara de SKIP LOCKED, y la razon de usarlo en lugar de
            // FOR UPDATE a secas: los trabajadores no hacen cola detras del
            // mismo candado, se reparten el trabajo. Sin SKIP LOCKED esto
            // seguiria dando tres, pero en serie; con la exclusion rota
            // daria tres tareas repetidas.
            Assert.Equal(3, conseguidas.Count);
            Assert.Equal(3, conseguidas.Select(t => t!.Id).Distinct().Count());
        }
        finally
        {
            foreach (var alcance in alcances)
            {
                alcance.Dispose();
            }
        }
    }
}
