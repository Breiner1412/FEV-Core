using System.ComponentModel.DataAnnotations;
using FevCore.Domain.Documentos;

namespace FevCore.Api.Contratos;

/// <summary>
/// Cuerpo de POST /api/v1/facturas.
///
/// Desde H2 la linea referencia un producto del catalogo, como describe el
/// contrato de la etapa 5. Los datos del producto se copian al emitir.
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

    /// <summary>Debe corresponder a un adquirente registrado y activo.</summary>
    [Required(ErrorMessage = "El adquirente es obligatorio.")]
    public required Guid AdquirenteId { get; init; }

    /// <summary>Si se omite, se usa la fecha y hora del servidor.</summary>
    public DateTimeOffset? FechaEmision { get; init; }

    [Required(ErrorMessage = "El documento debe tener al menos una linea.")]
    [MinLength(1, ErrorMessage = "El documento debe tener al menos una linea.")]
    public required IReadOnlyList<LineaSolicitud> Lineas { get; init; }
}

public sealed record LineaSolicitud
{
    /// <summary>Debe corresponder a un producto registrado y activo.</summary>
    [Required(ErrorMessage = "Cada linea debe referenciar un producto.")]
    public required Guid ProductoId { get; init; }

    // ParseLimitsInInvariantCulture es obligatorio: sin el, los limites de
    // texto se convierten con la cultura del sistema, y en una maquina con
    // coma decimal "0.000001" no se puede interpretar. El codigo funcionaria
    // en el contenedor (cultura invariante) y fallaria en el equipo local.
    [Range(typeof(decimal), "0.000001", "999999999",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public required decimal Cantidad { get; init; }

    /// <summary>
    /// Si se omite, se usa el precio vigente del producto. En cualquier
    /// caso queda copiado en la linea y no cambia despues (RN-10).
    /// </summary>
    [Range(typeof(decimal), "0.000001", "999999999999",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "El precio unitario debe ser mayor que cero.")]
    public decimal? PrecioUnitario { get; init; }

    public decimal? Descuento { get; init; }
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
