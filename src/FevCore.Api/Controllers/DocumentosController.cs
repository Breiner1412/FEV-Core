using FevCore.Api.Contratos;
using FevCore.Application.Documentos;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Controllers;

[ApiController]
[Route("api/v1/documentos")]
public sealed class DocumentosController(ConsultarDocumentoHandler manejador) : ControllerBase
{
    /// <summary>
    /// Consulta un documento por su identificador (RF-22).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelacion)
    {
        var documento = await manejador.EjecutarAsync(id, cancelacion);

        if (documento is null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://github.com/Breiner1412/FEV-Core/errors/no-encontrado",
                Title = "Documento no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No existe un documento con el identificador {id}.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["codigo"] = "DOCUMENTO_NO_ENCONTRADO",
                    ["traceId"] = HttpContext.TraceIdentifier
                }
            });
        }

        return Ok(RespuestaDocumento.Desde(documento));
    }

    /// <summary>
    /// Historial completo de estados, del mas antiguo al mas reciente (RF-23).
    ///
    /// Se ordena por secuencia y no por marca de tiempo: dos transiciones
    /// pueden caer en el mismo instante, y entonces ordenar por tiempo
    /// devolveria el historial en un orden arbitrario.
    /// </summary>
    [HttpGet("{id:guid}/historial")]
    [ProducesResponseType<IReadOnlyList<RespuestaTransicion>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Historial(Guid id, CancellationToken cancelacion)
    {
        var documento = await manejador.EjecutarAsync(id, cancelacion);

        return documento is null
            ? NoEncontrado(id)
            : Ok(documento.Transiciones
                .OrderBy(t => t.Secuencia)
                .Select(RespuestaTransicion.Desde)
                .ToList());
    }

    private IActionResult NoEncontrado(Guid id) =>
        NotFound(new ProblemDetails
        {
            Type = "https://github.com/Breiner1412/FEV-Core/errors/no-encontrado",
            Title = "Documento no encontrado",
            Status = StatusCodes.Status404NotFound,
            Detail = $"No existe un documento con el identificador {id}.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["codigo"] = "DOCUMENTO_NO_ENCONTRADO",
                ["traceId"] = HttpContext.TraceIdentifier
            }
        });
}
