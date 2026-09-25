using FevCore.Domain.Integradores;
using FevCore.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FevCore.Integration.Tests;

/// <summary>
/// Levanta la API contra un PostgreSQL real, arrancado en un contenedor
/// desechable para estas pruebas.
///
/// Se usa el mismo motor y la misma version que en produccion (ADR-0003).
/// Una base en memoria seria mas rapida, pero no reproduce el comportamiento
/// real: las pruebas pasarian sin probar lo que importa.
///
/// El contenedor toma un puerto libre al azar, asi que no choca con el
/// PostgreSQL de docker-compose si esta corriendo.
/// </summary>
public sealed class FabricaApiConBaseDeDatos : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Llave de API con la que se autentican las pruebas.</summary>
    public const string LlaveDePrueba = "fev_llave_para_pruebas_de_integracion";

    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("fevcore_pruebas")
        .WithUsername("pruebas")
        .WithPassword("pruebas")
        .Build();

    public Guid IntegradorId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        // El entorno Testing evita que la aplicacion migre al arrancar:
        // de eso se encarga esta fabrica, cuando el contenedor ya esta listo.
        constructor.UseEnvironment("Testing");

        constructor.UseSetting(
            "ConnectionStrings:Principal",
            _contenedor.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _contenedor.StartAsync();

        // Acceder a Services construye la aplicacion, y por eso el contenedor
        // debe estar arriba antes: ConfigureWebHost le pide su cadena de conexion.
        using var alcance = Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<FevCoreDbContext>();

        await contexto.Database.MigrateAsync();

        var integrador = Integrador.CrearConLlave(
            "Integrador de pruebas",
            LlaveDePrueba,
            DateTimeOffset.UtcNow);

        IntegradorId = integrador.Id;

        contexto.Integradores.Add(integrador);
        await contexto.SaveChangesAsync();
    }

    // Implementacion explicita: WebApplicationFactory ya tiene un DisposeAsync
    // que devuelve ValueTask, y el de xUnit devuelve Task. Dos metodos con el
    // mismo nombre y distinto tipo de retorno no pueden convivir de otra forma.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _contenedor.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Cliente HTTP con la llave de API ya puesta.</summary>
    public HttpClient CrearClienteAutenticado()
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", LlaveDePrueba);
        return cliente;
    }
}
