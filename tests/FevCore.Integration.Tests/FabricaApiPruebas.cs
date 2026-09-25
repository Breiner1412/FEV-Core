using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FevCore.Integration.Tests;

/// <summary>
/// Levanta la API para las pruebas.
///
/// Usa el entorno "Testing", que evita que la aplicacion aplique migraciones
/// al arrancar: las pruebas que necesitan base de datos la preparan por su
/// cuenta, y las que no, no deben depender de que exista una.
/// </summary>
public class FabricaApiPruebas : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        constructor.UseEnvironment("Testing");

        // Cadena de conexion de relleno: satisface la validacion de arranque
        // sin que nadie se conecte. Las pruebas que si usan base de datos
        // la reemplazan.
        constructor.UseSetting(
            "ConnectionStrings:Principal",
            "Host=localhost;Port=5432;Database=fevcore_pruebas;Username=pruebas;Password=pruebas");
    }
}
