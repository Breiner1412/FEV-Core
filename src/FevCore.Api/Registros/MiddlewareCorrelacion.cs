using FevCore.Api.Autenticacion;

namespace FevCore.Api.Registros;

/// <summary>
/// Agrega a cada registro de la peticion su identificador de correlacion y,
/// si esta autenticada, el integrador que la hizo (RNF-10, RF-02).
///
/// Asi, dado un traceId, se pueden recuperar todos los registros de esa
/// peticion sin tener que repetir el dato en cada llamada a LogInformation.
/// </summary>
public sealed class MiddlewareCorrelacion(
    RequestDelegate siguiente,
    ILogger<MiddlewareCorrelacion> registrador)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        var datos = new Dictionary<string, object>
        {
            ["traceId"] = contexto.TraceIdentifier
        };

        if (contexto.User.Identity?.IsAuthenticated == true)
        {
            datos["integradorId"] = contexto.User.ObtenerIntegradorId();
        }

        using (registrador.BeginScope(datos))
        {
            // El traceId tambien viaja de vuelta, para que el integrador
            // pueda citarlo al reportar un problema.
            contexto.Response.Headers["X-Trace-Id"] = contexto.TraceIdentifier;

            await siguiente(contexto);
        }
    }
}
