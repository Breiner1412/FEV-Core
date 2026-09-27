using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FevCore.Infrastructure.Persistencia;

/// <summary>
/// Permite que las herramientas de linea de comandos creen y eliminen
/// migraciones sin arrancar la aplicacion.
///
/// La cadena de conexion se busca en tres lugares, en este orden:
///
///   1. La variable de entorno ConnectionStrings__Principal.
///   2. El archivo .env de la raiz del repositorio.
///   3. Un valor de ultimo recurso para desarrollo.
///
/// El paso 2 existe porque las herramientas de EF necesitan conectarse a la
/// base — por ejemplo para saber que migraciones ya se aplicaron — y sin el
/// habria que exportar la contrasena a mano antes de cada comando.
///
/// Nunca se fijan credenciales en el codigo (RNF-01): el valor de ultimo
/// recurso solo sirve si alguien esta corriendo con la configuracion de
/// ejemplo sin haberla cambiado.
/// </summary>
public sealed class FabricaDbContextDisenio : IDesignTimeDbContextFactory<FevCoreDbContext>
{
    public FevCoreDbContext CreateDbContext(string[] args)
    {
        var cadena =
            Environment.GetEnvironmentVariable("ConnectionStrings__Principal")
            ?? LeerDelArchivoEnv()
            ?? "Host=localhost;Port=5432;Database=fevcore;Username=fevcore;Password=fevcore";

        var opciones = new DbContextOptionsBuilder<FevCoreDbContext>()
            .UseNpgsql(cadena)
            .Options;

        return new FevCoreDbContext(opciones);
    }

    /// <summary>
    /// Busca un archivo .env subiendo desde el directorio actual, y arma la
    /// cadena de conexion con sus valores.
    ///
    /// Sube por los directorios padre porque los comandos de EF se pueden
    /// ejecutar desde la raiz del repositorio o desde dentro de un proyecto.
    /// </summary>
    private static string? LeerDelArchivoEnv()
    {
        var directorio = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directorio is not null)
        {
            var ruta = Path.Combine(directorio.FullName, ".env");

            if (File.Exists(ruta))
            {
                var valores = File.ReadAllLines(ruta)
                    .Select(linea => linea.Trim())
                    .Where(linea =>
                        linea.Length > 0 &&
                        !linea.StartsWith('#') &&
                        linea.Contains('='))
                    .Select(linea => linea.Split('=', 2))
                    .ToDictionary(
                        partes => partes[0].Trim(),
                        partes => partes[1].Trim());

                if (valores.TryGetValue("POSTGRES_DB", out var baseDatos) &&
                    valores.TryGetValue("POSTGRES_USER", out var usuario) &&
                    valores.TryGetValue("POSTGRES_PASSWORD", out var clave))
                {
                    return $"Host=localhost;Port=5432;Database={baseDatos};" +
                           $"Username={usuario};Password={clave}";
                }
            }

            directorio = directorio.Parent;
        }

        return null;
    }
}
