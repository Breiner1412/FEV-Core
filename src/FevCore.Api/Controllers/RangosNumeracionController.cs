using FevCore.Api.Contratos;
using FevCore.Application.Numeracion;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Controllers;

[ApiController]
[Route("api/v1/rangos-numeracion")]
public sealed class RangosNumeracionController(GestionRangos gestion) : ControllerBase
{
    /// <summary>Registra un rango autorizado (RF-08).</summary>
    [HttpPost]
    [ProducesResponseType<RespuestaRango>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(
        [FromBody] RangoSolicitud solicitud,
        CancellationToken cancelacion)
    {
        var rango = await gestion.RegistrarAsync(
            solicitud.Prefijo,
            solicitud.TipoDocumento,
            solicitud.NumeroInicial,
            solicitud.NumeroFinal,
            solicitud.VigenteDesde,
            solicitud.VigenteHasta,
            solicitud.NumeroAutorizacion,
            solicitud.ClaveTecnica,
            cancelacion);

        var respuesta = RespuestaRango.Desde(rango, gestion.Hoy());

        return Created($"/api/v1/rangos-numeracion/{respuesta.Id}", respuesta);
    }

    /// <summary>
    /// Estado de todos los rangos: cuantos numeros quedan y cuantos dias
    /// faltan para el vencimiento (RF-10).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RespuestaRango>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancelacion)
    {
        var rangos = await gestion.ListarAsync(cancelacion);
        var hoy = gestion.Hoy();

        return Ok(rangos.Select(r => RespuestaRango.Desde(r, hoy)).ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RespuestaRango>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelacion)
    {
        var rango = await gestion.ObtenerAsync(id, cancelacion);

        return rango is null
            ? NotFound(new ProblemDetails
            {
                Type = "https://github.com/Breiner1412/FEV-Core/errors/no-encontrado",
                Title = "Rango no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No existe un rango de numeracion con el identificador {id}.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["codigo"] = "RANGO_NO_ENCONTRADO",
                    ["traceId"] = HttpContext.TraceIdentifier
                }
            })
            : Ok(RespuestaRango.Desde(rango, gestion.Hoy()));
    }
}
