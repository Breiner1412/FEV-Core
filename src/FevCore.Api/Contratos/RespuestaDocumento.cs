using FevCore.Application.Abstracciones;
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

    /// <summary>Identificador del adquirente en el catalogo.</summary>
    public required Guid AdquirenteId { get; init; }

    /// <summary>Datos del emisor tal como estaban al emitir (RN-10).</summary>
    public required RespuestaDatosTributarios Emisor { get; init; }

    /// <summary>Datos del adquirente tal como estaban al emitir (RN-10).</summary>
    public required RespuestaDatosTributarios Adquirente { get; init; }

    public required IReadOnlyList<RespuestaLinea> Lineas { get; init; }
    public required RespuestaTotales Totales { get; init; }

    /// <summary>
    /// Nulo hasta que se genera el XML. Lo calcula el emisor al generarlo y
    /// viaja dentro de el desde el primer envio (RF-20); no depende de que la
    /// autoridad apruebe.
    /// </summary>
    public string? CodigoUnico { get; init; }

    // ── Solo en notas ──

    /// <summary>Factura que esta nota corrige. Nulo en facturas.</summary>
    public Guid? DocumentoReferenciadoId { get; init; }

    public MotivoNota? Motivo { get; init; }
    public string? Observaciones { get; init; }

    // ── Resultado ante la autoridad (RF-18, RF-21) ──

    /// <summary>
    /// El identificador con el que la autoridad conoce este documento. Nulo
    /// hasta que se transmite.
    /// </summary>
    public string? IdentificadorSeguimiento { get; init; }

    /// <summary>
    /// Por que la autoridad rechazo el documento. Vacio si no fue rechazado.
    ///
    /// Son lo que el integrador necesita para corregir: un documento
    /// rechazado no se retransmite, se reemplaza por uno nuevo (RN-07).
    /// </summary>
    public required IReadOnlyList<string> ErroresValidacion { get; init; }

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
        AdquirenteId = documento.AdquirenteId,
        Emisor = RespuestaDatosTributarios.Desde(documento.EmisorSnapshot),
        Adquirente = RespuestaDatosTributarios.Desde(documento.AdquirenteSnapshot),
        Lineas = [.. documento.Lineas.Select(RespuestaLinea.Desde)],
        Totales = RespuestaTotales.Desde(documento.Totales),
        CodigoUnico = documento.CodigoUnico,
        DocumentoReferenciadoId = documento.DocumentoReferenciadoId,
        Motivo = documento.Motivo,
        Observaciones = documento.Observaciones,
        IdentificadorSeguimiento = documento.IdentificadorSeguimiento,
        ErroresValidacion = [.. documento.ErroresValidacion]
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

/// <summary>
/// Un documento en un listado (RF-24).
///
/// Lleva menos campos que RespuestaDocumento, y eso es una promesa del
/// contrato, no una casualidad de la implementacion: quien liste y luego
/// necesite el detalle tiene que consultar el documento.
/// </summary>
public sealed record RespuestaDocumentoResumen
{
    public required Guid Id { get; init; }
    public required TipoDocumento Tipo { get; init; }
    public required EstadoDocumento Estado { get; init; }
    public required string NumeroCompleto { get; init; }
    public required DateTimeOffset FechaEmision { get; init; }
    public required string AdquirenteRazonSocial { get; init; }
    public required decimal TotalAPagar { get; init; }
    public string? CodigoUnico { get; init; }

    public static RespuestaDocumentoResumen Desde(ResumenDocumento resumen) => new()
    {
        Id = resumen.Id,
        Tipo = resumen.Tipo,
        Estado = resumen.Estado,
        NumeroCompleto = resumen.NumeroCompleto,
        FechaEmision = resumen.FechaEmision,
        AdquirenteRazonSocial = resumen.AdquirenteRazonSocial,
        TotalAPagar = resumen.TotalAPagar.Valor,
        CodigoUnico = resumen.CodigoUnico
    };
}
