using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FevCore.Api.Salida;
using FevCore.Application.Abstracciones;
using FevCore.Application.Salida;
using FevCore.Domain.Integradores;
using FevCore.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

    // La imagen va en el constructor: el constructor sin parametros quedo
    // obsoleto en Testcontainers.
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("fevcore_pruebas")
        .WithUsername("pruebas")
        .WithPassword("pruebas")
        .Build();

    public Guid IntegradorId { get; private set; }

    /// <summary>
    /// La autoridad, gobernable desde la prueba. Sustituye al proveedor HTTP
    /// real, que tiene sus propias pruebas de traduccion de protocolo.
    /// </summary>
    public ProveedorValidacionSimulado Validacion { get; } = new();

    /// <summary>
    /// Certificado autofirmado, generado al vuelo para cada ejecucion.
    ///
    /// No se guarda ninguno en el repositorio. Un certificado con clave
    /// privada es un secreto, y RNF-01 dice que no puede haber secretos en
    /// el codigo fuente. Aunque este sea de juguete, versionarlo seria
    /// aceptar la costumbre por la que acaban filtrandose los de verdad.
    /// </summary>
    public X509Certificate2 Certificado { get; } = CrearCertificado();

    private static X509Certificate2 CrearCertificado()
    {
        using var rsa = RSA.Create(2048);

        var solicitud = new CertificateRequest(
            "CN=Comercializadora del Eje SAS, O=FEV-Core, C=CO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var generado = solicitud.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        return X509CertificateLoader.LoadPkcs12(
            generado.Export(X509ContentType.Pfx, "pruebas"),
            "pruebas",
            X509KeyStorageFlags.Exportable);
    }

    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        // El entorno Testing evita que la aplicacion migre al arrancar:
        // de eso se encarga esta fabrica, cuando el contenedor ya esta listo.
        constructor.UseEnvironment("Testing");

        // AddInMemoryCollection y no UseSetting: los valores de UseSetting
        // entran en la configuracion del anfitrion, antes de que la
        // aplicacion agregue las suyas, y cualquier fuente posterior puede
        // pisarlos sin avisar. Lo que se agrega aqui se anade al final de
        // la cadena, asi que gana siempre. La diferencia se vio en una
        // corrida real: el trabajador de fondo arranco pese a estar
        // apagado por UseSetting.
        constructor.ConfigureAppConfiguration(configuracion =>
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Principal"] = _contenedor.GetConnectionString(),

                // El certificado entra por configuracion, igual que en
                // produccion: en base 64, nunca como archivo (RNF-01).
                ["Firma:CertificadoBase64"] = Convert.ToBase64String(
                    Certificado.Export(X509ContentType.Pfx, "pruebas")),
                ["Firma:Clave"] = "pruebas",

                // El trabajador en segundo plano se apaga: las pruebas
                // deciden cuando se procesa una tarea. Un bucle que corre
                // solo no se deja gobernar, y lo que no se gobierna no se
                // puede probar.
                ["Salida:Habilitado"] = "false",

                // Sin espera entre reintentos: la espera creciente se prueba
                // aparte, en el dominio. Aqui solo estorbaria.
                ["Salida:EsperaMaximaSegundos"] = "0",
                ["Salida:MaximoIntentos"] = "3",
                ["Salida:TiempoDeAbandonoSegundos"] = "0",
            }));

        constructor.ConfigureTestServices(servicios =>
        {
            // RemoveAll y no Replace: AddHttpClient registra varias cosas
            // alrededor del tipo, y dejar alguna suelta haria que se
            // resolviera el proveedor real.
            servicios.RemoveAll<IProveedorValidacion>();
            servicios.AddSingleton<IProveedorValidacion>(Validacion);

            // Y aparte de apagarlo por configuracion, se retira el
            // registro. Apagar depende de que la opcion llegue; retirar,
            // no. Es el mismo cinturon con tirantes que se pone en
            // cualquier mecanismo que, al fallar, falla en silencio.
            //
            // Se quita ESTE descriptor y no todos los IHostedService: el
            // servidor web tambien es uno, y quitarlos todos dejaria la
            // aplicacion sin atender peticiones.
            var registro = servicios.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(TrabajadorSalida));

            if (registro is not null)
            {
                servicios.Remove(registro);
            }
        });
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

    /// <summary>
    /// Procesa una tarea de la bandeja, como haria el trabajador.
    ///
    /// Devuelve falso si no habia ninguna pendiente.
    /// </summary>
    public async Task<bool> ProcesarUnaTareaAsync()
    {
        using var alcance = Services.CreateScope();

        return await alcance.ServiceProvider
            .GetRequiredService<ProcesadorTareas>()
            .ProcesarUnaAsync();
    }

    /// <summary>Vacia la bandeja, con un tope por si algo no avanzara.</summary>
    public async Task<int> ProcesarTodoAsync(int tope = 20)
    {
        var procesadas = 0;

        while (procesadas < tope && await ProcesarUnaTareaAsync())
        {
            procesadas++;
        }

        return procesadas;
    }

    /// <summary>Cliente HTTP con la llave de API ya puesta.</summary>
    public HttpClient CrearClienteAutenticado()
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", LlaveDePrueba);
        return cliente;
    }
}
