using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Errores;

/// <summary>
/// Ultima red: cualquier excepcion que nadie mas manejo.
///
/// Registra el detalle completo del lado del servidor y le devuelve al
/// integrador un mensaje generico con el identificador de correlacion.
/// Nunca expone rutas, consultas ni trazas de pila (RNF-11).
/// </summary>
public sealed class ManejadorExcepcionNoPrevista(
    ILogger<ManejadorExcepcionNoPrevista> registrador) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excepcion,
        CancellationToken cancelacion)
    {
        registrador.LogError(
            excepcion,
            "Error no previsto procesando {Metodo} {Ruta}. TraceId: {TraceId}.",
            contexto.Request.Method,
            contexto.Request.Path,
            contexto.TraceIdentifier);

        var problema = new ProblemDetails
        {
            Type = "https://github.com/Breiner1412/FEV-Core/errors/interno",
            Title = "Error interno del servidor",
            Status = StatusCodes.Status500InternalServerError,
            // Deliberadamente generico. El detalle real quedo en los registros
            // del servidor, y el traceId permite encontrarlo.
            Detail = "Ocurrio un error inesperado. Si el problema persiste, " +
                     "reporte el identificador de correlacion.",
            Instance = contexto.Request.Path,
            Extensions =
            {
                ["codigo"] = "ERROR_INTERNO",
                ["traceId"] = contexto.TraceIdentifier
            }
        };

        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await contexto.Response.WriteAsJsonAsync(
            problema,
            options: (JsonSerializerOptions?)null,
            contentType: "application/problem+json",
            cancellationToken: cancelacion);

        return true;
    }
}
