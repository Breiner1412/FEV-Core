using FevCore.Api.Salida;
using FevCore.Application.Salida;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FevCore.Integration.Tests;

/// <summary>
/// Vigila que la configuracion de las pruebas realmente llegue a la
/// aplicacion.
///
/// Existe por un fallo real: la fabrica apagaba el trabajador de fondo con
/// UseSetting, el trabajador consultaba la opcion correctamente, y aun asi
/// arranco. Nadie se entero porque una configuracion que no aplica no
/// levanta ningun error: solo deja al sistema comportandose distinto de lo
/// que dice el codigo.
///
/// Esa clase de fallo es peor que una excepcion. Con el trabajador
/// encendido, las demas pruebas de H7 dejan de ser deterministas: el bucle
/// procesa tareas por su cuenta mientras la prueba cree tener el control, y
/// el resultado depende de quien llegue primero. Habrian pasado o fallado
/// segun el humor de la maquina.
///
/// De ahi que estas afirmaciones sean sobre el arranque y no sobre una
/// respuesta HTTP: convierten un fallo silencioso en una prueba roja.
/// </summary>
public sealed class ConfiguracionDePruebasTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    [Fact]
    public void El_trabajador_de_fondo_no_se_registra_durante_las_pruebas()
    {
        var alojados = fabrica.Services.GetServices<IHostedService>();

        Assert.DoesNotContain(alojados, servicio => servicio is TrabajadorSalida);
    }

    [Fact]
    public void Las_opciones_de_salida_toman_los_valores_de_prueba()
    {
        var opciones = fabrica.Services
            .GetRequiredService<IOptions<OpcionesSalida>>()
            .Value;

        // Cada uno vale por una fuente distinta de no determinismo: un
        // trabajador suelto, una espera real entre reintentos, y una tarea
        // que tardaria dos minutos en darse por abandonada.
        Assert.False(opciones.Habilitado);
        Assert.Equal(0, opciones.EsperaMaximaSegundos);
        Assert.Equal(3, opciones.MaximoIntentos);
        Assert.Equal(0, opciones.TiempoDeAbandonoSegundos);
    }
}
