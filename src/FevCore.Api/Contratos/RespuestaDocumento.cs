using FevCore.Domain.Documentos;

namespace FevCore.Api.Contratos;

/// <summary>
/// Representacion de un documento tal como la ve el integrador.
///
/// Es un tipo aparte de la entidad de dominio a proposito: lo que la API
/// promete es un contrato estable, y el dominio debe poder cambiar por
/// dentro sin romperlo.
/// </summary>
public sealed record RespuestaDocumento
{
    public required Guid Id { get; init; }
    public required TipoDocumento Tipo { get; init; }
    public required EstadoDocumento Estado { get; init; }
    public required string ReferenciaExterna { get; init; }
    public required string Prefijo { get; init; }
    public required long Consecutivo { get; init; }
    public required string NumeroCompleto { get; init; }
    public required DateTimeOffset FechaEmision { get; init; }
    public required string Moneda { get; init; }
    public required IReadOnlyList<RespuestaLinea> Lineas { get; init; }
    public required RespuestaTotales Totales { get; init; }

    /// <summary>Nulo mientras el documento no este aprobado (RF-20).</summary>
    public string? CodigoUnico { get; init; }

    public static RespuestaDocumento Desde(Documento documento) => new()
    {
        Id = documento.Id,
        Tipo = documento.Tipo,
        Estado = documento.Estado,
        ReferenciaExterna = documento.ReferenciaExterna,
        Prefijo = documento.Prefijo,
        Consecutivo = documento.Consecutivo,
        NumeroCompleto = documento.NumeroCompleto,
        FechaEmision = documento.FechaEmision,
        Moneda = documento.Moneda,
        Lineas = [.. documento.Lineas.Select(RespuestaLinea.Desde)],
        Totales = RespuestaTotales.Desde(documento.Totales),
        CodigoUnico = null
    };
}

public sealed record RespuestaLinea
{
    public required int Numero { get; init; }
    public required string Codigo { get; init; }
    public required string Descripcion { get; init; }
    public required string UnidadMedida { get; init; }
    public required decimal Cantidad { get; init; }
    public required decimal PrecioUnitario { get; init; }
    public required decimal Descuento { get; init; }
    public required decimal BaseGravable { get; init; }
    public required decimal Total { get; init; }
    public required IReadOnlyList<RespuestaImpuesto> Impuestos { get; init; }
    public Guid? ProductoId { get; init; }

    public static RespuestaLinea Desde(Linea linea) => new()
    {
        Numero = linea.Numero,
        Codigo = linea.Codigo,
        Descripcion = linea.Descripcion,
        UnidadMedida = linea.UnidadMedida,
        Cantidad = linea.Cantidad,
        PrecioUnitario = linea.PrecioUnitario.Valor,
        Descuento = linea.Descuento.Valor,
        BaseGravable = linea.BaseGravable.Valor,
        Total = linea.Total.Valor,
        Impuestos = [.. linea.Impuestos.Select(RespuestaImpuesto.Desde)],
        ProductoId = linea.ProductoId
    };
}

public sealed record RespuestaImpuesto
{
    public required TipoImpuesto Tipo { get; init; }
    public required decimal Tarifa { get; init; }
    public required decimal BaseGravable { get; init; }
    public required decimal Valor { get; init; }

    public static RespuestaImpuesto Desde(ImpuestoLinea impuesto) => new()
    {
        Tipo = impuesto.Tipo,
        Tarifa = impuesto.Tarifa,
        BaseGravable = impuesto.BaseGravable.Valor,
        Valor = impuesto.Valor.Valor
    };
}

public sealed record RespuestaTotales
{
    public required decimal TotalBruto { get; init; }
    public required decimal TotalDescuentos { get; init; }
    public required decimal TotalBaseImponible { get; init; }
    public required decimal TotalImpuestos { get; init; }
    public required decimal TotalAPagar { get; init; }

    public static RespuestaTotales Desde(Totales totales) => new()
    {
        TotalBruto = totales.TotalBruto.Valor,
        TotalDescuentos = totales.TotalDescuentos.Valor,
        TotalBaseImponible = totales.TotalBaseImponible.Valor,
        TotalImpuestos = totales.TotalImpuestos.Valor,
        TotalAPagar = totales.TotalAPagar.Valor
    };
}
