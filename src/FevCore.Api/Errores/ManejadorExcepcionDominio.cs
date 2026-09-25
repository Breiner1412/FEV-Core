using System.Text.Json;
using FevCore.Domain.Comun;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Errores;

/// <summary>
/// Traduce una violacion de regla de negocio a una respuesta HTTP.
///
/// 409 y no 400: la solicitud esta bien escrita, pero el negocio no permite
/// lo que pide. Esa distincion le dice al integrador si debe corregir su
/// codigo o resolver una situacion (seccion 3.2 del contrato de la API).
/// </summary>
public sealed class ManejadorExcepcionDominio(
    ILogger<ManejadorExcepcionDominio> registrador) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excepcion,
        CancellationToken cancelacion)
    {
        if (excepcion is not ExcepcionDominio dominio)
        {
            return false;
        }

        registrador.LogInformation(
            "Regla de negocio rechazo la peticion. Codigo: {Codigo}. Ruta: {Ruta}.",
            dominio.Codigo,
            contexto.Request.Path);

        var problema = new ProblemDetails
        {
            Type = $"https://github.com/Breiner1412/FEV-Core/errors/{dominio.Codigo.ToLowerInvariant()}",
            Title = "La operacion no procede",
            Status = StatusCodes.Status409Conflict,
            Detail = dominio.Message,
            Instance = contexto.Request.Path,
            Extensions =
            {
                ["codigo"] = dominio.Codigo,
                ["traceId"] = contexto.TraceIdentifier
            }
        };

        contexto.Response.StatusCode = StatusCodes.Status409Conflict;

        await contexto.Response.WriteAsJsonAsync(
            problema,
            options: (JsonSerializerOptions?)null,
            contentType: "application/problem+json",
            cancellationToken: cancelacion);

        return true;
    }
}
