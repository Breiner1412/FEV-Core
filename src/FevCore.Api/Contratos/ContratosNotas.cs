using System.ComponentModel.DataAnnotations;
using FevCore.Domain.Documentos;

namespace FevCore.Api.Contratos;

/// <summary>
/// Cuerpo de POST /api/v1/notas-credito y POST /api/v1/notas-debito.
///
/// No lleva adquirente: la nota se emite al mismo comprador de la factura
/// que corrige, y pedirlo abriria la puerta a que no coincidieran.
/// </summary>
public sealed record EmitirNotaSolicitud
{
    /// <summary>
    /// Identificador de la operacion en el sistema del integrador.
    /// Reenviar con la misma referencia devuelve el documento ya creado
    /// en vez de crear otro (RF-15).
    /// </summary>
    [Required(ErrorMessage = "La referencia externa es obligatoria.")]
    [StringLength(64, MinimumLength = 1)]
    public required string ReferenciaExterna { get; init; }

    /// <summary>
    /// Factura que se corrige. Debe existir, ser de tipo factura y estar
    /// aprobada (RN-03, RN-05).
    /// </summary>
    [Required(ErrorMessage = "La nota debe referenciar una factura.")]
    public required Guid DocumentoReferenciadoId { get; init; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    public required MotivoNota Motivo { get; init; }

    [StringLength(500)]
    public string? Observaciones { get; init; }

    /// <summary>Si se omite, se usa la fecha y hora del servidor.</summary>
    public DateTimeOffset? FechaEmision { get; init; }

    [Required(ErrorMessage = "La nota debe tener al menos una linea.")]
    [MinLength(1, ErrorMessage = "La nota debe tener al menos una linea.")]
    public required IReadOnlyList<LineaSolicitud> Lineas { get; init; }
}

/// <summary>Una transicion del historial (RF-23).</summary>
public sealed record RespuestaTransicion
{
    public required int Secuencia { get; init; }
    public EstadoDocumento? EstadoAnterior { get; init; }
    public required EstadoDocumento EstadoNuevo { get; init; }
    public required DateTimeOffset OcurridaEn { get; init; }
    public required string Motivo { get; init; }
    public string? Detalle { get; init; }

    public static RespuestaTransicion Desde(TransicionEstado transicion) => new()
    {
        Secuencia = transicion.Secuencia,
        EstadoAnterior = transicion.EstadoAnterior,
        EstadoNuevo = transicion.EstadoNuevo,
        OcurridaEn = transicion.OcurridaEn,
        Motivo = transicion.Motivo,
        Detalle = transicion.Detalle
    };
}

/// <summary>
/// Cuerpo del endpoint de desarrollo que fuerza un estado.
/// </summary>
public sealed record TransicionSolicitud
{
    [Required]
    public required EstadoDocumento Estado { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Motivo { get; init; }

    [StringLength(500)]
    public string? Detalle { get; init; }
}
