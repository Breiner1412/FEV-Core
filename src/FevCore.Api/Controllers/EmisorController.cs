using FevCore.Api.Contratos;
using FevCore.Application.Catalogos;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Controllers;

[ApiController]
[Route("api/v1/emisor")]
public sealed class EmisorController(GestionEmisor gestion) : ControllerBase
{
    /// <summary>
    /// Consulta la configuracion del emisor (RF-03).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<RespuestaEmisor>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Consultar(CancellationToken cancelacion)
    {
        var emisor = await gestion.ObtenerAsync(cancelacion);

        return emisor is null
            ? NoEncontrado("El emisor no ha sido configurado.")
            : Ok(RespuestaEmisor.Desde(emisor));
    }

    /// <summary>
    /// Configura el emisor, o reemplaza su configuracion si ya existe.
    ///
    /// Es PUT y no POST porque el emisor es uno solo: la operacion deja el
    /// recurso en un estado conocido, sin importar cuantas veces se repita.
    /// </summary>
    [HttpPut]
    [ProducesResponseType<RespuestaEmisor>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Configurar(
        [FromBody] ConfigurarEmisorSolicitud solicitud,
        CancellationToken cancelacion)
    {
        var emisor = await gestion.ConfigurarAsync(
            solicitud.Datos.ADominio(),
            solicitud.NombreComercial,
            cancelacion);

        return Ok(RespuestaEmisor.Desde(emisor));
    }

    private IActionResult NoEncontrado(string detalle) =>
        NotFound(new ProblemDetails
        {
            Type = "https://github.com/Breiner1412/FEV-Core/errors/no-encontrado",
            Title = "Emisor no configurado",
            Status = StatusCodes.Status404NotFound,
            Detail = detalle,
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["codigo"] = "EMISOR_NO_CONFIGURADO",
                ["traceId"] = HttpContext.TraceIdentifier
            }
        });
}
