using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FevCore.Infrastructure.Persistencia;

/// <summary>
/// Permite que las herramientas de linea de comandos creen y eliminen
/// migraciones sin arrancar la aplicacion.
///
/// La cadena de conexion se busca en dos lugares, en este orden:
///
///   1. La variable de entorno ConnectionStrings__Principal.
///   2. El archivo .env de la raiz del repositorio.
///
/// El paso 2 existe porque las herramientas de EF necesitan conectarse a la
/// base — por ejemplo para saber que migraciones ya se aplicaron — y sin el
/// habria que exportar la contrasena a mano antes de cada comando.
///
/// Si no esta en ninguno, falla diciendo que falta y como ponerlo. Hasta la
/// auditoria final habia un tercer paso, un valor de ultimo recurso con la
/// contrasena escrita aqui. Que fuera de desarrollo no cambiaba que hubiera
/// una contrasena en el repositorio, y RNF-01 no tiene matices. Fallar al
/// generar una migracion por falta de configuracion es correcto; llevar la
/// contrasena dentro no lo es.
/// </summary>
public sealed class FabricaDbContextDisenio : IDesignTimeDbContextFactory<FevCoreDbContext>
{
    private const string Variable = "ConnectionStrings__Principal";

    private static readonly string[] ClavesEnv = ["POSTGRES_DB", "POSTGRES_USER", "POSTGRES_PASSWORD"];

    public FevCoreDbContext CreateDbContext(string[] args)
    {
        var cadena = ResolverCadena(
            Environment.GetEnvironmentVariable(Variable),
            new DirectoryInfo(Directory.GetCurrentDirectory()));

        var opciones = new DbContextOptionsBuilder<FevCoreDbContext>()
            .UseNpgsql(cadena)
            .Options;

        return new FevCoreDbContext(opciones);
    }

    /// <summary>
    /// La cadena de la variable de entorno, o la que se arma con el .env que
    /// se encuentre subiendo desde <paramref name="desde"/>. Publica para
    /// poder probarla sin depender del entorno de quien corre las pruebas.
    /// </summary>
    public static string ResolverCadena(string? deEntorno, DirectoryInfo desde)
    {
        if (!string.IsNullOrWhiteSpace(deEntorno))
        {
            return deEntorno;
        }

        var archivo = BuscarEnv(desde)
            ?? throw new InvalidOperationException(
                "Las herramientas de EF no encuentran la cadena de conexion. Definala " +
                "de una de estas dos formas:" + Environment.NewLine +
                $"  1. La variable de entorno {Variable}. En bash: " +
                $"export {Variable}=\"Host=localhost;Port=5432;Database=...;Username=...;Password=...\". " +
                $"En PowerShell: $env:{Variable} = \"Host=localhost;Port=5432;Database=...;Username=...;Password=...\"." +
                Environment.NewLine +
                "  2. Un archivo .env en la raiz del repositorio con POSTGRES_DB, " +
                "POSTGRES_USER y POSTGRES_PASSWORD. Copie .env.example y cambie los valores." +
                Environment.NewLine +
                "La contrasena nunca se escribe en el codigo (RNF-01).");

        var valores = LeerValores(archivo);
        var faltan = ClavesEnv.Where(clave => !valores.ContainsKey(clave)).ToList();

        if (faltan.Count > 0)
        {
            throw new InvalidOperationException(
                $"El archivo {archivo.FullName} no define {string.Join(", ", faltan)}. " +
                "Agreguelos (ver .env.example), o defina la variable de entorno " +
                $"{Variable} con la cadena completa.");
        }

        return $"Host=localhost;Port=5432;Database={valores["POSTGRES_DB"]};" +
               $"Username={valores["POSTGRES_USER"]};Password={valores["POSTGRES_PASSWORD"]}";
    }

    /// <summary>
    /// Busca un archivo .env subiendo por los directorios padre, porque los
    /// comandos de EF se pueden ejecutar desde la raiz del repositorio o desde
    /// dentro de un proyecto.
    /// </summary>
    private static FileInfo? BuscarEnv(DirectoryInfo desde)
    {
        for (var directorio = desde; directorio is not null; directorio = directorio.Parent)
        {
            var archivo = new FileInfo(Path.Combine(directorio.FullName, ".env"));

            if (archivo.Exists)
            {
                return archivo;
            }
        }

        return null;
    }

    private static Dictionary<string, string> LeerValores(FileInfo archivo) =>
        File.ReadAllLines(archivo.FullName)
            .Select(linea => linea.Trim())
            .Where(linea => linea.Length > 0 && !linea.StartsWith('#') && linea.Contains('='))
            .Select(linea => linea.Split('=', 2))
            .Where(partes => partes[1].Trim().Length > 0)
            .ToDictionary(partes => partes[0].Trim(), partes => partes[1].Trim());
}
