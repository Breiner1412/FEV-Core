using FevCore.Api.Contratos;
using FevCore.Application.Catalogos;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Controllers;

[ApiController]
[Route("api/v1/adquirentes")]
public sealed class AdquirentesController(GestionAdquirentes gestion) : ControllerBase
{
    /// <summary>Lista adquirentes con paginacion (RF-06).</summary>
    [HttpGet]
    [ProducesResponseType<RespuestaPagina<RespuestaAdquirente>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] bool? activo,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20,
        CancellationToken cancelacion = default)
    {
        var resultado = await gestion.ListarAsync(
            activo,
            Math.Max(1, pagina),
            Math.Clamp(tamanoPagina, 1, 100),
            cancelacion);

        return Ok(new RespuestaPagina<RespuestaAdquirente>
        {
            Pagina = resultado.Pagina,
            TamanoPagina = resultado.TamanoPagina,
            TotalElementos = resultado.TotalElementos,
            TotalPaginas = resultado.TotalPaginas,
            Elementos = [.. resultado.Elementos.Select(RespuestaAdquirente.Desde)]
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RespuestaAdquirente>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelacion)
    {
        var adquirente = await gestion.ObtenerAsync(id, cancelacion);

        return adquirente is null
            ? NoEncontrado(id)
            : Ok(RespuestaAdquirente.Desde(adquirente));
    }

    [HttpPost]
    [ProducesResponseType<RespuestaAdquirente>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] AdquirenteSolicitud solicitud,
        CancellationToken cancelacion)
    {
        var adquirente = await gestion.CrearAsync(solicitud.Datos.ADominio(), cancelacion);
        var respuesta = RespuestaAdquirente.Desde(adquirente);

        return Created($"/api/v1/adquirentes/{respuesta.Id}", respuesta);
    }

    /// <summary>
    /// Modifica un adquirente.
    ///
    /// No afecta documentos ya emitidos, que conservan una copia de los
    /// datos vigentes al momento de la emision (RN-10).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RespuestaAdquirente>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] AdquirenteSolicitud solicitud,
        CancellationToken cancelacion)
    {
        var adquirente = await gestion.ActualizarAsync(
            id, solicitud.Datos.ADominio(), cancelacion);

        return adquirente is null
            ? NoEncontrado(id)
            : Ok(RespuestaAdquirente.Desde(adquirente));
    }

    /// <summary>
    /// Desactiva un adquirente. NO lo elimina (INV-ADQ-02).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(Guid id, CancellationToken cancelacion) =>
        await gestion.DesactivarAsync(id, cancelacion)
            ? NoContent()
            : NoEncontrado(id);

    private IActionResult NoEncontrado(Guid id) =>
        NotFound(new ProblemDetails
        {
            Type = "https://github.com/Breiner1412/FEV-Core/errors/no-encontrado",
            Title = "Adquirente no encontrado",
            Status = StatusCodes.Status404NotFound,
            Detail = $"No existe un adquirente con el identificador {id}.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["codigo"] = "ADQUIRENTE_NO_ENCONTRADO",
                ["traceId"] = HttpContext.TraceIdentifier
            }
        });
}
