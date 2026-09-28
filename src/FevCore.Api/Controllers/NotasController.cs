using FevCore.Api.Autenticacion;
using FevCore.Api.Contratos;
using FevCore.Application.Documentos;
using FevCore.Domain.Documentos;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Controllers;

/// <summary>
/// Notas credito y debito.
///
/// Las dos rutas estan en el mismo controlador porque comparten todo salvo
/// el tipo. El contrato las separa —el integrador dice explicitamente cual
/// emite, en vez de mandar un campo "tipo"— porque equivocarse de tipo en un
/// campo es mas facil que equivocarse de direccion (seccion 2.1 del contrato).
/// </summary>
[ApiController]
public sealed class NotasController(EmitirNotaHandler manejador) : ControllerBase
{
    /// <summary>Emite una nota credito contra una factura aprobada (RF-12).</summary>
    [HttpPost("api/v1/notas-credito")]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> EmitirCredito(
        [FromBody] EmitirNotaSolicitud solicitud,
        CancellationToken cancelacion) =>
        Emitir(TipoDocumento.NotaCredito, solicitud, cancelacion);

    /// <summary>Emite una nota debito contra una factura aprobada (RF-13).</summary>
    [HttpPost("api/v1/notas-debito")]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> EmitirDebito(
        [FromBody] EmitirNotaSolicitud solicitud,
        CancellationToken cancelacion) =>
        Emitir(TipoDocumento.NotaDebito, solicitud, cancelacion);

    private async Task<IActionResult> Emitir(
        TipoDocumento tipo,
        EmitirNotaSolicitud solicitud,
        CancellationToken cancelacion)
    {
        var comando = new ComandoEmitirNota(
            Tipo: tipo,
            IntegradorId: User.ObtenerIntegradorId(),
            ReferenciaExterna: solicitud.ReferenciaExterna,
            DocumentoReferenciadoId: solicitud.DocumentoReferenciadoId,
            Motivo: solicitud.Motivo,
            Observaciones: solicitud.Observaciones,
            FechaEmision: solicitud.FechaEmision,
            Lineas: [.. solicitud.Lineas.Select(linea => new LineaComando(
                ProductoId: linea.ProductoId,
                Cantidad: linea.Cantidad,
                PrecioUnitario: linea.PrecioUnitario,
                Descuento: linea.Descuento))]);

        var resultado = await manejador.EjecutarAsync(comando, cancelacion);
        var respuesta = RespuestaDocumento.Desde(resultado.Documento);

        return resultado.YaExistia
            ? Ok(respuesta)
            : Accepted($"/api/v1/documentos/{respuesta.Id}", respuesta);
    }
}
