using System.Text.Json;
using System.Text.Json.Serialization;
using FevCore.Api.Autenticacion;
using FevCore.Api.Configuracion;
using FevCore.Api.Errores;
using FevCore.Api.Registros;
using FevCore.Application.Abstracciones;
using FevCore.Application.Documentos;
using FevCore.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ──

builder.Services
    .AddControllers()
    .AddJsonOptions(opciones =>
    {
        // Las enumeraciones viajan como texto, no como numeros: "APROBADO"
        // se entiende solo, y un 3 obliga a consultar una tabla de codigos.
        // La politica de mayusculas con guion bajo produce exactamente los
        // valores del contrato: Iva -> "IVA", NotaCredito -> "NOTA_CREDITO".
        opciones.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    });

builder.Services.AddOpenApi();

// Formato estandar de error para las respuestas que genera el framework,
// como la validacion del modelo de entrada (RNF-11).
builder.Services.AddProblemDetails();

// El orden de registro importa: se prueba uno por uno hasta que alguno
// maneje la excepcion. El generico va de ultimo.
builder.Services.AddExceptionHandler<ManejadorExcepcionDominio>();
builder.Services.AddExceptionHandler<ManejadorExcepcionNoPrevista>();

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
builder.Services.AddScoped<IRepositorioDocumentos, RepositorioDocumentos>();

builder.Services.AddScoped<EmitirFacturaHandler>();
builder.Services.AddScoped<ConsultarDocumentoHandler>();

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
// El orden importa: cada pieza se ejecuta en la secuencia en que se declara.

// Primero, para que cualquier error posterior salga en formato Problem Details.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();

// Despues de autenticar, para que el registro pueda incluir el integrador.
app.UseMiddleware<MiddlewareCorrelacion>();

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
