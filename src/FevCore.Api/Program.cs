using FevCore.Api.Autenticacion;
using FevCore.Api.Configuracion;
using FevCore.Api.Errores;
using FevCore.Api.Registros;
using FevCore.Application.Abstracciones;
using FevCore.Application.Catalogos;
using FevCore.Application.Documentos;
using FevCore.Application.Numeracion;
using FevCore.Application.Salida;
using FevCore.Api.Salida;
using FevCore.Infrastructure.Validacion;
using FevCore.Domain.Documentos;
using FevCore.Infrastructure.Persistencia;
using FevCore.Infrastructure.Xml;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ──

// La MISMA configuracion de JSON para los dos mundos de ASP.NET Core: la de
// los controladores y la de los endpoints de API minima. Ver
// ConfiguracionJson para por que son dos y que pasa si solo se ajusta una.
builder.Services
    .AddControllers()
    .AddJsonOptions(opciones =>
        ConfiguracionJson.Aplicar(opciones.JsonSerializerOptions));

builder.Services.ConfigureHttpJsonOptions(opciones =>
    ConfiguracionJson.Aplicar(opciones.SerializerOptions));

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

    // SplitQuery en lugar de una sola consulta con JOIN.
    //
    // El agregado Documento tiene tres colecciones anidadas: lineas, los
    // impuestos de cada linea y el historial de estados. Con un solo JOIN,
    // la base devuelve el producto de las tres: una factura de 10 lineas con
    // 2 impuestos cada una y 4 transiciones produce 80 filas para traer 16
    // registros, y los datos del documento se repiten en las 80.
    //
    // Con SplitQuery EF hace una consulta por coleccion. Son mas viajes a la
    // base, pero sin multiplicacion. Entity Framework venia avisando de esto
    // desde H1 (warning 20504); con la tercera coleccion deja de ser teorico.
    opciones.UseNpgsql(
        cadena,
        npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
});

builder.Services.AddScoped<IRepositorioIntegradores, RepositorioIntegradores>();
builder.Services.AddScoped<IRepositorioDocumentos, RepositorioDocumentos>();
builder.Services.AddScoped<IRepositorioEmisor, RepositorioEmisor>();
builder.Services.AddScoped<IRepositorioAdquirentes, RepositorioAdquirentes>();
builder.Services.AddScoped<IRepositorioProductos, RepositorioProductos>();
builder.Services.AddScoped<IRepositorioRangos, RepositorioRangos>();
builder.Services.AddScoped<IRepositorioTareas, RepositorioTareas>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();

builder.Services.AddScoped<EmitirFacturaHandler>();
builder.Services.AddScoped<ConsultarDocumentoHandler>();
builder.Services.AddScoped<ListarDocumentosHandler>();
builder.Services.AddScoped<EmitirNotaHandler>();
builder.Services.AddScoped<TransicionarDocumentoHandler>();
builder.Services.AddScoped<GenerarXmlHandler>();
builder.Services.AddScoped<FirmarDocumentoHandler>();

// El certificado se carga una vez: leerlo y descifrarlo en cada peticion
// seria trabajo repetido sobre un dato que no cambia mientras el proceso
// viva. La vigencia SI se comprueba en cada firma (INV-CER-01).
builder.Services.AddSingleton<IProveedorCertificado, ProveedorCertificadoConfiguracion>();
builder.Services.AddSingleton<IFirmadorXml, FirmadorXadesEpes>();

// ── Bandeja de salida y transmision (ADR-0006, ADR-0007) ──

builder.Services
    .AddOptions<OpcionesSalida>()
    .Bind(builder.Configuration.GetSection(OpcionesSalida.Seccion));

builder.Services.AddScoped<ProcesadorTareas>();

// HttpClient con nombre y tiempo de espera acotado.
//
// El tiempo de espera importa mas de lo que parece: es lo que convierte un
// servicio que no contesta en un resultado SIN_RESPUESTA en vez de una
// espera indefinida que bloquearia al trabajador para siempre.
builder.Services
    .AddHttpClient<IProveedorValidacion, ProveedorValidacionHttp>(cliente =>
    {
        cliente.BaseAddress = new Uri(
            builder.Configuration["Validacion:UrlBase"] ?? "http://localhost:5108");

        cliente.Timeout = TimeSpan.FromSeconds(
            builder.Configuration.GetValue("Validacion:TiempoEsperaSegundos", 30));
    });

builder.Services.AddHostedService<TrabajadorSalida>();

// El ambiente entra en el codigo unico, asi que un documento de pruebas y
// uno de produccion con los mismos datos producen codigos distintos. Por
// defecto Pruebas: equivocarse hacia produccion es el error caro.
builder.Services.AddSingleton<IGeneradorXml>(_ => new GeneradorXmlUbl(
    Enum.TryParse<AmbienteDian>(builder.Configuration["Dian:Ambiente"], out var ambiente)
        ? ambiente
        : AmbienteDian.Pruebas));
builder.Services.AddScoped<GestionEmisor>();
builder.Services.AddScoped<GestionAdquirentes>();
builder.Services.AddScoped<GestionProductos>();
builder.Services.AddScoped<GestionRangos>();

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

// Misma lista blanca de entornos que el resto de lo que no existe en
// produccion, y no IsDevelopment: asi el documento generado tambien se sirve
// bajo Testing, que es lo que permite contrastarlo contra el escrito a mano
// dentro de una prueba en vez de a ojo (RNF-07).
if (EndpointsDesarrollo.EstaPermitidoEn(app.Environment))
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

// Endpoints que no existen en produccion. La clase decide sola si se
// registra, segun el entorno.
EndpointsDesarrollo.Mapear(app);

await app.RunAsync();

// Hace visible la clase Program para los proyectos de prueba.
// Con instrucciones de nivel superior el compilador genera una clase Program
// interna, que WebApplicationFactory no alcanza a ver desde otro proyecto.
public partial class Program;
