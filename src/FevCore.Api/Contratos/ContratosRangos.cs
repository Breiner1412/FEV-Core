using System.ComponentModel.DataAnnotations;
using FevCore.Domain.Documentos;
using FevCore.Domain.Numeracion;

namespace FevCore.Api.Contratos;

public sealed class RangoSolicitud
{
    [Required]
    [StringLength(10, MinimumLength = 1)]
    public string Prefijo { get; init; } = string.Empty;

    [Required]
    public TipoDocumento TipoDocumento { get; init; }

    [Range(1, long.MaxValue)]
    public long NumeroInicial { get; init; }

    [Range(1, long.MaxValue)]
    public long NumeroFinal { get; init; }

    [Required]
    public DateOnly VigenteDesde { get; init; }

    [Required]
    public DateOnly VigenteHasta { get; init; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string NumeroAutorizacion { get; init; } = string.Empty;

    /// <summary>
    /// Clave tecnica entregada por la DIAN. Entra pero no vuelve a salir:
    /// ninguna respuesta la incluye.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string ClaveTecnica { get; init; } = string.Empty;
}

public sealed class RespuestaRango
{
    public required Guid Id { get; init; }
    public required string Prefijo { get; init; }
    public required TipoDocumento TipoDocumento { get; init; }
    public required long NumeroInicial { get; init; }
    public required long NumeroFinal { get; init; }
    public required DateOnly VigenteDesde { get; init; }
    public required DateOnly VigenteHasta { get; init; }
    public required long? UltimoAsignado { get; init; }
    public required string NumeroAutorizacion { get; init; }

    /// <summary>RF-10.</summary>
    public required long NumerosDisponibles { get; init; }

    /// <summary>RF-10. Negativo si el rango ya vencio.</summary>
    public required int DiasParaVencimiento { get; init; }

    public required bool Agotado { get; init; }
    public required bool Vigente { get; init; }

    // ClaveTecnica no aparece aqui a proposito.

    public static RespuestaRango Desde(RangoNumeracion rango, DateOnly hoy) => new()
    {
        Id = rango.Id,
        Prefijo = rango.Prefijo,
        TipoDocumento = rango.TipoDocumento,
        NumeroInicial = rango.NumeroInicial,
        NumeroFinal = rango.NumeroFinal,
        VigenteDesde = rango.VigenteDesde,
        VigenteHasta = rango.VigenteHasta,
        UltimoAsignado = rango.UltimoAsignado,
        NumeroAutorizacion = rango.NumeroAutorizacion,
        NumerosDisponibles = rango.NumerosDisponibles,
        DiasParaVencimiento = rango.DiasParaVencimiento(hoy),
        Agotado = rango.Agotado,
        Vigente = rango.EstaVigenteEn(hoy)
    };
}
