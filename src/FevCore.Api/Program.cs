var builder = WebApplication.CreateBuilder(args);

// Servicios que la aplicacion va a necesitar.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Cadena de procesamiento de cada peticion HTTP.
// El orden importa: cada pieza se ejecuta en la secuencia en que se declara.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
