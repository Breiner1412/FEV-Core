using FevCore.Api.Autenticacion;
using FevCore.Api.Configuracion;
using FevCore.Application.Abstracciones;
using FevCore.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ──

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Abstraccion del reloj, para que las pruebas puedan fijar la hora en vez de
// depender de la del sistema. Es la version que ya trae el framework de lo
// que el documento de arquitectura llamaba IRelojSistema.
builder.Services.AddSingleton(TimeProvider.System);

// La cadena de conexion viene de la configuracion, nunca del codigo (RNF-01).
// Si falta, la aplicacion falla al arrancar con un mensaje claro en vez de
// arrastrar el problema hasta la primera peticion.
builder.Services.AddDbContext<FevCoreDbContext>(opciones =>
{
    var cadena = builder.Configuration.GetConnectionString("Principal")
        ?? throw new InvalidOperationException(
            "Falta la cadena de conexion. Definala en la variable de entorno " +
            "ConnectionStrings__Principal. Ver .env.example.");

    opciones.UseNpgsql(cadena);
});

builder.Services.AddScoped<IRepositorioIntegradores, RepositorioIntegradores>();

// ── Autenticacion y autorizacion ──

builder.Services
    .AddAuthentication(ManejadorAutenticacionLlaveApi.Esquema)
    .AddScheme<AuthenticationSchemeOptions, ManejadorAutenticacionLlaveApi>(
        ManejadorAutenticacionLlaveApi.Esquema,
        _ => { });

builder.Services.AddAuthorization(opciones =>
{
    // Todo endpoint exige autenticacion salvo que diga explicitamente lo
    // contrario con [AllowAnonymous]. Al reves — abrir todo y proteger lo
    // sensible — un endpoint nuevo nace desprotegido y nadie lo nota.
    opciones.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// ── Cadena de procesamiento ──
// El orden importa: autenticar antes de autorizar, y ambos antes de que la
// peticion llegue a un controlador.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();

// Aplica las migraciones pendientes al arrancar, para que "docker compose up"
// baste en una maquina limpia (RNF-08).
//
// En un despliegue con varias instancias esto se sacaria a un paso previo,
// para que dos procesos no intenten migrar a la vez. Con una sola instancia
// es lo mas simple que funciona.
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var alcance = app.Services.CreateScope())
    {
        var contexto = alcance.ServiceProvider.GetRequiredService<FevCoreDbContext>();
        await contexto.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        await SemillaDesarrollo.SembrarIntegradorAsync(app);
    }
}

app.MapControllers();

await app.RunAsync();

// Hace visible la clase Program para los proyectos de prueba.
// Con instrucciones de nivel superior el compilador genera una clase Program
// interna, que WebApplicationFactory no alcanza a ver desde otro proyecto.
public partial class Program;
