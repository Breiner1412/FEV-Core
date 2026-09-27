using FevCore.Api.Contratos;
using FevCore.Application.Catalogos;
using FevCore.Domain.Comun;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Controllers;

[ApiController]
[Route("api/v1/productos")]
public sealed class ProductosController(GestionProductos gestion) : ControllerBase
{
    /// <summary>Lista productos con paginacion (RF-07).</summary>
    [HttpGet]
    [ProducesResponseType<RespuestaPagina<RespuestaProducto>>(StatusCodes.Status200OK)]
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

        return Ok(new RespuestaPagina<RespuestaProducto>
        {
            Pagina = resultado.Pagina,
            TamanoPagina = resultado.TamanoPagina,
            TotalElementos = resultado.TotalElementos,
            TotalPaginas = resultado.TotalPaginas,
            Elementos = [.. resultado.Elementos.Select(RespuestaProducto.Desde)]
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RespuestaProducto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelacion)
    {
        var producto = await gestion.ObtenerAsync(id, cancelacion);

        return producto is null
            ? NoEncontrado(id)
            : Ok(RespuestaProducto.Desde(producto));
    }

    [HttpPost]
    [ProducesResponseType<RespuestaProducto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] ProductoSolicitud solicitud,
        CancellationToken cancelacion)
    {
        var producto = await gestion.CrearAsync(
            solicitud.Codigo,
            solicitud.Descripcion,
            solicitud.UnidadMedida,
            Dinero.Desde(solicitud.PrecioUnitario),
            solicitud.ImpuestosADominio(),
            cancelacion);

        var respuesta = RespuestaProducto.Desde(producto);

        return Created($"/api/v1/productos/{respuesta.Id}", respuesta);
    }

    /// <summary>
    /// Modifica un producto, incluido su precio.
    ///
    /// Las facturas ya emitidas conservan el precio que se cobro: cada linea
    /// guarda una copia, no una referencia (RN-10).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RespuestaProducto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] ProductoSolicitud solicitud,
        CancellationToken cancelacion)
    {
        var producto = await gestion.ActualizarAsync(
            id,
            solicitud.Codigo,
            solicitud.Descripcion,
            solicitud.UnidadMedida,
            Dinero.Desde(solicitud.PrecioUnitario),
            solicitud.ImpuestosADominio(),
            cancelacion);

        return producto is null
            ? NoEncontrado(id)
            : Ok(RespuestaProducto.Desde(producto));
    }

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
            Title = "Producto no encontrado",
            Status = StatusCodes.Status404NotFound,
            Detail = $"No existe un producto con el identificador {id}.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["codigo"] = "PRODUCTO_NO_ENCONTRADO",
                ["traceId"] = HttpContext.TraceIdentifier
            }
        });
}
