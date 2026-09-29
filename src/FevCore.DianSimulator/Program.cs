using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

// ─────────────────────────────────────────────────────────────────────────
//  Simulador del servicio de validacion de la DIAN
//
//  Existe para poder probar el ciclo completo sin depender de un servicio
//  externo (ADR-0007). No pretende imitar el protocolo real —el verdadero
//  es SOAP— sino su COMPORTAMIENTO: acepta un documento, devuelve un
//  identificador de seguimiento, y el veredicto llega despues.
//
//  Esa forma es la que obliga al sistema a ser asincrono, que es lo que se
//  quiere ejercitar. Cambiar a la DIAN real significa escribir otra
//  implementacion de IProveedorValidacion, no tocar el resto.
//
//  Lo interesante del simulador no es que funcione: es que pueda FALLAR de
//  las maneras en que falla un servicio real.
// ─────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(opciones =>
{
    opciones.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
});

var app = builder.Build();

var estado = new EstadoSimulador();

// ── Configuracion del comportamiento ──

app.MapGet("/simulador/configuracion", () => estado.Configuracion)
    .WithSummary("Como se esta comportando el simulador ahora mismo.");

app.MapPut("/simulador/configuracion", (ConfiguracionSimulador nueva) =>
{
    estado.Configuracion = nueva;
    app.Logger.LogWarning("Simulador configurado en modo {Modo}.", nueva.Modo);
    return Results.Ok(nueva);
})
.WithSummary("Cambia el comportamiento: aprobar, rechazar, tardar o no responder.");

app.MapPost("/simulador/reiniciar", () =>
{
    estado.Reiniciar();
    return Results.NoContent();
})
.WithSummary("Olvida todo lo recibido y vuelve al comportamiento por defecto.");

// ── El servicio que imita a la DIAN ──

app.MapPost("/validacion/documentos", async (SolicitudValidacion solicitud) =>
{
    var configuracion = estado.Configuracion;

    await Task.Delay(configuracion.DemoraMilisegundos);

    // Modo CAIDO: el servicio no esta. Es lo que RNF-04 exige soportar.
    if (configuracion.Modo == ModoSimulador.Caido)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    // Modo SIN_RESPUESTA: recibe el documento y NO contesta. El cliente no
    // puede saber si llego. Es el escenario de RN-13, y la unica forma de
    // provocarlo a voluntad.
    if (configuracion.Modo == ModoSimulador.SinRespuesta)
    {
        estado.Registrar(solicitud.NumeroDocumento, ModoSimulador.SinRespuesta);
        await Task.Delay(TimeSpan.FromMinutes(5));
        return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
    }

    if (string.IsNullOrWhiteSpace(solicitud.Xml))
    {
        return Results.BadRequest(new { error = "El documento viene vacio." });
    }

    var seguimiento = $"TRK-{Guid.CreateVersion7():N}"[..20];

    estado.Recibidos[seguimiento] = new DocumentoRecibido(
        solicitud.NumeroDocumento,
        configuracion.Modo,
        DateTimeOffset.UtcNow,
        configuracion.SegundosHastaVeredicto);

    app.Logger.LogInformation(
        "Recibido {Numero}. Seguimiento {Seguimiento}. Veredicto en {Segundos}s.",
        solicitud.NumeroDocumento, seguimiento, configuracion.SegundosHastaVeredicto);

    return Results.Accepted(
        $"/validacion/documentos/{seguimiento}",
        new RespuestaTransmision(seguimiento));
})
.WithSummary("Recibe un documento y devuelve un identificador de seguimiento.");

app.MapGet("/validacion/documentos/{seguimiento}", async (string seguimiento) =>
{
    var configuracion = estado.Configuracion;

    await Task.Delay(configuracion.DemoraMilisegundos);

    if (configuracion.Modo == ModoSimulador.Caido)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    if (!estado.Recibidos.TryGetValue(seguimiento, out var recibido))
    {
        return Results.NotFound(new { error = "Identificador de seguimiento desconocido." });
    }

    // El veredicto no esta listo de inmediato: asi se ejercita la consulta
    // repetida con espera creciente (RNF-05).
    var transcurrido = DateTimeOffset.UtcNow - recibido.RecibidoEn;

    if (transcurrido.TotalSeconds < recibido.SegundosHastaVeredicto)
    {
        return Results.Ok(new RespuestaVeredicto(VeredictoSimulado.EnProceso, null));
    }

    return recibido.Modo == ModoSimulador.Rechaza
        ? Results.Ok(new RespuestaVeredicto(
            VeredictoSimulado.Rechazado,
            [
                "DIAN-REGLA-FAU14: El valor total del documento no corresponde " +
                "con la sumatoria de las lineas.",
                "DIAN-REGLA-DAJ01: El adquiriente no se encuentra registrado."
            ]))
        : Results.Ok(new RespuestaVeredicto(VeredictoSimulado.Aprobado, null));
})
.WithSummary("Consulta el veredicto de un documento transmitido.");

app.MapGet("/salud", () => Results.Ok(new { estado = "vivo" }));

app.Run();

// ─────────────────────────────── Tipos ───────────────────────────────

/// <summary>Como se comporta el simulador.</summary>
public enum ModoSimulador
{
    /// <summary>Todo lo que llega termina aprobado.</summary>
    Aprueba,

    /// <summary>Todo lo que llega termina rechazado, con errores.</summary>
    Rechaza,

    /// <summary>El servicio no responde: 503. Escenario de RNF-04.</summary>
    Caido,

    /// <summary>Recibe y no contesta. Escenario de RN-13.</summary>
    SinRespuesta
}

public enum VeredictoSimulado
{
    EnProceso,
    Aprobado,
    Rechazado
}

public sealed record ConfiguracionSimulador
{
    public ModoSimulador Modo { get; init; } = ModoSimulador.Aprueba;

    /// <summary>Cuanto tarda cada respuesta. Para simular lentitud.</summary>
    public int DemoraMilisegundos { get; init; }

    /// <summary>
    /// Cuanto tarda el veredicto en estar listo. Cero significa inmediato,
    /// que es lo que quieren las pruebas automatizadas.
    /// </summary>
    public int SegundosHastaVeredicto { get; init; }
}

public sealed record SolicitudValidacion(string NumeroDocumento, string Xml);
public sealed record RespuestaTransmision(string IdentificadorSeguimiento);
public sealed record RespuestaVeredicto(VeredictoSimulado Veredicto, string[]? Errores);

internal sealed record DocumentoRecibido(
    string NumeroDocumento,
    ModoSimulador Modo,
    DateTimeOffset RecibidoEn,
    int SegundosHastaVeredicto);

/// <summary>
/// Estado en memoria. Un simulador no necesita persistencia: si se
/// reinicia, las pruebas lo reconfiguran.
/// </summary>
internal sealed class EstadoSimulador
{
    public ConfiguracionSimulador Configuracion { get; set; } = new();

    public ConcurrentDictionary<string, DocumentoRecibido> Recibidos { get; } = new();

    /// <summary>
    /// Deja constancia de lo recibido aunque no se conteste. Sirve para que
    /// una prueba pueda comprobar que un documento SI llego, aunque el
    /// emisor nunca se haya enterado.
    /// </summary>
    public void Registrar(string numero, ModoSimulador modo) =>
        Recibidos[$"SIN-RESPUESTA-{numero}"] =
            new DocumentoRecibido(numero, modo, DateTimeOffset.UtcNow, 0);

    public void Reiniciar()
    {
        Configuracion = new ConfiguracionSimulador();
        Recibidos.Clear();
    }
}
