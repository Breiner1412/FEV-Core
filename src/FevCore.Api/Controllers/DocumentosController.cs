using FevCore.Api.Contratos;
using FevCore.Application.Documentos;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Controllers;

[ApiController]
[Route("api/v1/documentos")]
public sealed class DocumentosController(
    ConsultarDocumentoHandler manejador,
    GenerarXmlHandler generador) : ControllerBase
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

    /// <summary>
    /// Genera el XML del documento y lo deja guardado (RF-16).
    ///
    /// Es POST y no GET porque tiene efectos: guarda el XML, fija el codigo
    /// unico y mueve el documento a EN_PROCESO. En H7 esto lo disparara el
    /// proceso en segundo plano y esta ruta dejara de hacer falta.
    /// </summary>
    [HttpPost("{id:guid}/xml")]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerarXml(Guid id, CancellationToken cancelacion)
    {
        var documento = await generador.EjecutarAsync(id, cancelacion);

        return documento is null
            ? NoEncontrado(id)
            : Ok(RespuestaDocumento.Desde(documento));
    }

    /// <summary>
    /// Descarga el XML ya generado (RF-25).
    ///
    /// Devuelve application/xml y no JSON: es un archivo, y quien lo pide lo
    /// quiere para guardarlo o enviarlo, no para leerlo dentro de un campo.
    /// </summary>
    [HttpGet("{id:guid}/xml")]
    [Produces("application/xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DescargarXml(Guid id, CancellationToken cancelacion)
    {
        var documento = await manejador.EjecutarAsync(id, cancelacion);

        if (documento is null)
        {
            return NoEncontrado(id);
        }

        if (documento.Xml is null)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://github.com/Breiner1412/FEV-Core/errors/xml-no-disponible",
                Title = "La operacion no procede",
                Status = StatusCodes.Status409Conflict,
                Detail = $"El documento {documento.NumeroCompleto} aun no tiene XML generado.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["codigo"] = "XML_NO_DISPONIBLE",
                    ["traceId"] = HttpContext.TraceIdentifier
                }
            });
        }

        return File(
            System.Text.Encoding.UTF8.GetBytes(documento.Xml),
            "application/xml",
            $"{documento.NumeroCompleto}.xml");
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
