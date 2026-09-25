using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FevCore.Infrastructure.Persistencia;

/// <summary>
/// Permite que las herramientas de linea de comandos creen migraciones sin
/// arrancar la aplicacion.
///
/// La cadena de conexion de aqui SOLO se usa para generar migraciones; la
/// aplicacion en ejecucion toma la suya de la configuracion. Aun asi se lee
/// de una variable de entorno cuando existe, para no fijar credenciales en
/// el codigo (RNF-01).
/// </summary>
public sealed class FabricaDbContextDisenio : IDesignTimeDbContextFactory<FevCoreDbContext>
{
    public FevCoreDbContext CreateDbContext(string[] args)
    {
        var cadena =
            Environment.GetEnvironmentVariable("ConnectionStrings__Principal")
            ?? "Host=localhost;Port=5432;Database=fevcore;Username=fevcore;Password=fevcore";

        var opciones = new DbContextOptionsBuilder<FevCoreDbContext>()
            .UseNpgsql(cadena)
            .Options;

        return new FevCoreDbContext(opciones);
    }
}
