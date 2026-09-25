using FevCore.Domain.Documentos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Lo que se necesita para emitir una factura.
///
/// Es un tipo de la capa de aplicacion, no de la API: describe la intencion
/// en terminos del negocio, sin saber que llego por HTTP ni en que formato.
/// </summary>
public sealed record ComandoEmitirFactura(
    Guid IntegradorId,
    string ReferenciaExterna,
    DateTimeOffset? FechaEmision,
    IReadOnlyList<LineaComando> Lineas);

public sealed record LineaComando(
    string Codigo,
    string Descripcion,
    string UnidadMedida,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal? Descuento = null,
    IReadOnlyList<ImpuestoComando>? Impuestos = null,
    Guid? ProductoId = null);

public sealed record ImpuestoComando(TipoImpuesto Tipo, decimal Tarifa);

/// <summary>
/// Resultado de una emision.
/// </summary>
/// <param name="Documento">El documento, nuevo o preexistente.</param>
/// <param name="YaExistia">
/// Cierto si la referencia externa ya se habia usado y se devolvio el
/// documento anterior en lugar de crear uno nuevo. La API lo traduce a
/// 200 en vez de 202 (RF-15).
/// </param>
public sealed record ResultadoEmision(Domain.Documentos.Documento Documento, bool YaExistia);
