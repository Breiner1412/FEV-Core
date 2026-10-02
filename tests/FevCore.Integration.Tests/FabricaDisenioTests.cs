using FevCore.Infrastructure.Persistencia;

namespace FevCore.Integration.Tests;

/// <summary>
/// La cadena de conexion de las herramientas de EF sale de la configuracion,
/// nunca del codigo (RNF-01).
///
/// La fabrica de tiempo de diseno tenia un valor de ultimo recurso con la
/// contrasena escrita. Que fuera de desarrollo no cambiaba que hubiera una
/// contrasena en el repositorio de un proyecto que dice que no las tiene.
/// Ahora, sin configuracion, falla diciendo que falta y como ponerlo.
///
/// Cada prueba trabaja en una carpeta temporal propia: la busqueda del .env
/// sube por los directorios padre, y desde el repositorio encontraria el
/// .env local de quien corra las pruebas.
/// </summary>
public sealed class FabricaDisenioTests : IDisposable
{
    private readonly DirectoryInfo _carpeta = Directory.CreateTempSubdirectory("fevcore-disenio-");

    public void Dispose() => _carpeta.Delete(recursive: true);

    private void EscribirEnv(params string[] lineas) =>
        File.WriteAllLines(Path.Combine(_carpeta.FullName, ".env"), lineas);

    /// <summary>RNF-01.</summary>
    [Fact]
    public void Sin_variable_ni_env_falla_diciendo_que_falta_y_como_ponerlo()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => FabricaDbContextDisenio.ResolverCadena(null, _carpeta));

        Assert.Contains("ConnectionStrings__Principal", error.Message);
        Assert.Contains(".env", error.Message);
    }

    /// <summary>RNF-01. Un .env a medias no se completa con nada inventado.</summary>
    [Fact]
    public void Un_env_incompleto_falla_nombrando_lo_que_le_falta()
    {
        EscribirEnv("POSTGRES_DB=fevcore", "POSTGRES_USER=fevcore");

        var error = Assert.Throws<InvalidOperationException>(
            () => FabricaDbContextDisenio.ResolverCadena(null, _carpeta));

        Assert.Contains("POSTGRES_PASSWORD", error.Message);
    }

    [Fact]
    public void Un_env_completo_arma_la_cadena()
    {
        EscribirEnv("# comentario", "POSTGRES_DB=base", "POSTGRES_USER=usuario", "POSTGRES_PASSWORD=clave");

        var cadena = FabricaDbContextDisenio.ResolverCadena(null, _carpeta);

        Assert.Contains("Database=base", cadena);
        Assert.Contains("Username=usuario", cadena);
        Assert.Contains("Password=clave", cadena);
    }

    [Fact]
    public void La_variable_de_entorno_gana_sobre_el_env()
    {
        EscribirEnv("POSTGRES_DB=base", "POSTGRES_USER=usuario", "POSTGRES_PASSWORD=clave");

        var cadena = FabricaDbContextDisenio.ResolverCadena("Host=otro;Database=x", _carpeta);

        Assert.Equal("Host=otro;Database=x", cadena);
    }
}
