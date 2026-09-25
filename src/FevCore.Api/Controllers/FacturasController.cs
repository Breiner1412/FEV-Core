using FevCore.Api.Autenticacion;
using FevCore.Api.Contratos;
using FevCore.Application.Documentos;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Controllers;

[ApiController]
[Route("api/v1/facturas")]
public sealed class FacturasController(EmitirFacturaHandler manejador) : ControllerBase
{
    /// <summary>
    /// Emite una factura electronica de venta (RF-11, RF-14, RF-15).
    ///
    /// Responde de inmediato, sin contactar al servicio de validacion: eso
    /// ocurre despues, en segundo plano (ADR-0005). Un 202 significa
    /// "recibido y encolado", NO "aprobado por la autoridad".
    /// </summary>
    [HttpPost]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Emitir(
        [FromBody] EmitirFacturaSolicitud solicitud,
        CancellationToken cancelacion)
    {
        var comando = new ComandoEmitirFactura(
            IntegradorId: User.ObtenerIntegradorId(),
            ReferenciaExterna: solicitud.ReferenciaExterna,
            FechaEmision: solicitud.FechaEmision,
            Lineas: [.. solicitud.Lineas.Select(linea => new LineaComando(
                Codigo: linea.Codigo,
                Descripcion: linea.Descripcion,
                UnidadMedida: linea.UnidadMedida,
                Cantidad: linea.Cantidad,
                PrecioUnitario: linea.PrecioUnitario,
                Descuento: linea.Descuento,
                Impuestos: [.. (linea.Impuestos ?? []).Select(
                    i => new ImpuestoComando(i.Tipo, i.Tarifa))],
                ProductoId: linea.ProductoId))]);

        var resultado = await manejador.EjecutarAsync(comando, cancelacion);
        var respuesta = RespuestaDocumento.Desde(resultado.Documento);

        // 200 si ya existia, 202 si se acaba de crear.
        //
        // La diferencia importa: 202 promete que hay algo pendiente de
        // procesar. Si el documento ya estuviera aprobado, decir 202 seria
        // afirmar un trabajo pendiente que no existe (RF-15).
        return resultado.YaExistia
            ? Ok(respuesta)
            : Accepted($"/api/v1/documentos/{respuesta.Id}", respuesta);
    }
}
