using System.ComponentModel.DataAnnotations;
using FevCore.Domain.Documentos;

namespace FevCore.Api.Contratos;

/// <summary>
/// Cuerpo de POST /api/v1/facturas.
///
/// ALCANCE H1: las lineas traen sus datos en crudo, sin catalogo de
/// productos. El contrato de la etapa 5 describe la version final, donde
/// la linea referencia un producto registrado; eso llega en H2.
/// </summary>
public sealed record EmitirFacturaSolicitud
{
    /// <summary>
    /// Identificador de la operacion en el sistema del integrador.
    /// Reenviar con la misma referencia devuelve el documento ya creado
    /// en vez de crear otro (RF-15).
    /// </summary>
    [Required(ErrorMessage = "La referencia externa es obligatoria.")]
    [StringLength(64, MinimumLength = 1)]
    public required string ReferenciaExterna { get; init; }

    /// <summary>Si se omite, se usa la fecha y hora del servidor.</summary>
    public DateTimeOffset? FechaEmision { get; init; }

    [Required(ErrorMessage = "El documento debe tener al menos una linea.")]
    [MinLength(1, ErrorMessage = "El documento debe tener al menos una linea.")]
    public required IReadOnlyList<LineaSolicitud> Lineas { get; init; }
}

public sealed record LineaSolicitud
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string Codigo { get; init; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string Descripcion { get; init; }

    [Required]
    [StringLength(10, MinimumLength = 1)]
    public required string UnidadMedida { get; init; }

    // ParseLimitsInInvariantCulture es obligatorio: sin el, los limites de
    // texto se convierten con la cultura del sistema, y en una maquina con
    // coma decimal "0.000001" no se puede interpretar. El codigo funcionaria
    // en el contenedor (cultura invariante) y fallaria en el equipo local.
    [Range(typeof(decimal), "0.000001", "999999999",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public required decimal Cantidad { get; init; }

    [Range(typeof(decimal), "0.000001", "999999999999",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "El precio unitario debe ser mayor que cero.")]
    public required decimal PrecioUnitario { get; init; }

    public decimal? Descuento { get; init; }

    public IReadOnlyList<ImpuestoSolicitud>? Impuestos { get; init; }

    /// <summary>Referencia informativa al producto de origen. Opcional en H1.</summary>
    public Guid? ProductoId { get; init; }
}

public sealed record ImpuestoSolicitud
{
    public required TipoImpuesto Tipo { get; init; }

    /// <summary>Porcentaje. Para el 19% de IVA, 19.</summary>
    [Range(typeof(decimal), "0", "100",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "La tarifa debe estar entre 0 y 100.")]
    public required decimal Tarifa { get; init; }
}
