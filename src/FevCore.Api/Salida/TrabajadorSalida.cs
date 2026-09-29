using FevCore.Application.Salida;
using Microsoft.Extensions.Options;

namespace FevCore.Api.Salida;

/// <summary>
/// El bucle que mira la bandeja de salida (ADR-0006, RNF-04).
///
/// Vive en el mismo proceso que la API. No es lo que se haria a escala
/// —ahi seria un servicio aparte— pero para este proyecto anade un
/// despliegue mas sin resolver ningun problema que exista todavia.
///
/// Es deliberadamente tonto: toma una tarea, la procesa, repite. Toda la
/// logica esta en ProcesadorTareas, que se puede llamar a mano desde una
/// prueba. Un bucle en segundo plano no se deja gobernar, y lo que no se
/// puede gobernar no se puede probar.
/// </summary>
public sealed class TrabajadorSalida(
    IServiceScopeFactory fabricaAlcances,
    IOptions<OpcionesSalida> opciones,
    ILogger<TrabajadorSalida> registrador) : BackgroundService
{
    private readonly OpcionesSalida _opciones = opciones.Value;

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        if (!_opciones.Habilitado)
        {
            registrador.LogWarning(
                "El trabajador de la bandeja de salida esta apagado por configuracion.");
            return;
        }

        registrador.LogInformation(
            "Trabajador de la bandeja de salida en marcha. Sondeo cada {Segundos}s.",
            _opciones.IntervaloSondeoSegundos);

        while (!cancelacion.IsCancellationRequested)
        {
            try
            {
                // Un alcance nuevo por vuelta: el contexto de base de datos y
                // los repositorios son de alcance, y reutilizar el mismo
                // durante toda la vida del proceso acumularia en memoria cada
                // entidad que hubiera pasado por el.
                using var alcance = fabricaAlcances.CreateScope();

                var procesador = alcance.ServiceProvider
                    .GetRequiredService<ProcesadorTareas>();

                var huboTrabajo = await procesador.ProcesarUnaAsync(cancelacion);

                // Solo se duerme si no habia nada. Con trabajo acumulado, se
                // vacia la bandeja sin pausas.
                if (!huboTrabajo)
                {
                    await Task.Delay(_opciones.IntervaloSondeo, cancelacion);
                }
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                // Apagado normal del proceso.
                break;
            }
            catch (Exception error)
            {
                // El bucle NUNCA muere.
                //
                // Si una excepcion inesperada lo terminara, el sistema
                // seguiria aceptando documentos y ninguno volveria a
                // procesarse, sin que nada lo indicara. Un trabajador que se
                // muere en silencio es peor que uno que no existe.
                registrador.LogError(
                    error, "Error no controlado en el trabajador. Se continua.");

                await Task.Delay(_opciones.IntervaloSondeo, cancelacion);
            }
        }

        registrador.LogInformation("Trabajador de la bandeja de salida detenido.");
    }
}
