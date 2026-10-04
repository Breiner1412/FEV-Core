using FevCore.Application.Abstracciones;
using FevCore.Domain.Integradores;

namespace FevCore.Api.Configuracion;

/// <summary>
/// Crea un integrador con una llave conocida la primera vez que se levanta
/// el sistema en desarrollo.
///
/// SOLO en desarrollo. En un despliegue real las llaves se aprovisionan con
/// un procedimiento administrativo, nunca solas ni escritas en los registros.
///
/// La llave por defecto esta escrita aqui y publicada en el README a
/// proposito: es una credencial de desarrollo, para que cualquiera pueda
/// seguir el recorrido, no un secreto (RNF-01). Por eso mismo solo puede
/// existir donde la lista blanca lo permite.
///
/// Lista blanca, como EndpointsDesarrollo, y no la misma: aqui solo entra
/// Development. Testing siembra su propia llave, y la publicada no tiene por
/// que existir en las pruebas.
/// </summary>
public static class SemillaDesarrollo
{
    private static readonly string[] EntornosPermitidos = ["Development"];

    public static bool EstaPermitidaEn(IWebHostEnvironment entorno) =>
        EntornosPermitidos.Contains(entorno.EnvironmentName);

    /// <summary>
    /// Llave fija de desarrollo. Al no cambiar entre arranques se puede
    /// dejar guardada en una coleccion de peticiones.
    /// Se puede sobrescribir con la variable LLAVE_DESARROLLO.
    /// </summary>
    private const string LlavePorDefecto = "fev_desarrollo_no_usar_en_produccion";

    public static async Task SembrarIntegradorAsync(WebApplication app)
    {
        using var alcance = app.Services.CreateScope();
        var servicios = alcance.ServiceProvider;

        var repositorio = servicios.GetRequiredService<IRepositorioIntegradores>();
        var reloj = servicios.GetRequiredService<TimeProvider>();

        var registrador = servicios
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(SemillaDesarrollo));

        var llave = app.Configuration["LLAVE_DESARROLLO"] ?? LlavePorDefecto;
        var huella = Integrador.CalcularHash(llave);

        if (await repositorio.BuscarPorHashLlaveAsync(huella) is null)
        {
            var integrador = Integrador.CrearConLlave(
                "Integrador de desarrollo",
                llave,
                reloj.GetUtcNow());

            await repositorio.AgregarAsync(integrador);
            await repositorio.GuardarCambiosAsync();
        }

        registrador.LogWarning(
            "ENTORNO DE DESARROLLO. Llave de API disponible: {Llave}", llave);
    }
}
