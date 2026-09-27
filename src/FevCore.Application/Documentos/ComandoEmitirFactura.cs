namespace FevCore.Application.Documentos;

/// <summary>
/// Lo que se necesita para emitir una factura.
///
/// Desde H2 las lineas ya no traen sus datos en crudo: traen el producto
/// del catalogo. El resto se copia de alli al emitir (RN-10).
/// </summary>
public sealed record ComandoEmitirFactura(
    Guid IntegradorId,
    string ReferenciaExterna,
    Guid AdquirenteId,
    DateTimeOffset? FechaEmision,
    IReadOnlyList<LineaComando> Lineas);

/// <summary>
/// Una linea solicitada.
/// </summary>
/// <param name="ProductoId">Producto del catalogo. Obligatorio.</param>
/// <param name="Cantidad">Cuanto se vende.</param>
/// <param name="PrecioUnitario">
/// Si se omite, se usa el precio vigente del producto. Se permite
/// sobrescribirlo porque un precio se negocia; lo que no se permite es
/// inventar un producto que no existe.
/// </param>
/// <param name="Descuento">Descuento sobre el valor bruto de la linea.</param>
public sealed record LineaComando(
    Guid ProductoId,
    decimal Cantidad,
    decimal? PrecioUnitario = null,
    decimal? Descuento = null);

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
