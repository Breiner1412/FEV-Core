namespace FevCore.Domain.Documentos;

/// <summary>
/// Estados por los que pasa un documento. Ver la maquina de estados en la
/// seccion 6 del documento de requerimientos.
///
/// En H1 todo documento nace y se queda en Recibido: el procesamiento en
/// segundo plano llega en H7.
/// </summary>
public enum EstadoDocumento
{
    /// <summary>Aceptado y con consecutivo asignado. Pendiente de procesar.</summary>
    Recibido,

    /// <summary>Generando o firmando el XML.</summary>
    EnProceso,

    /// <summary>Entregado al servicio de validacion. Esperando veredicto.</summary>
    Transmitido,

    /// <summary>Validado por la autoridad. Estado terminal.</summary>
    Aprobado,

    /// <summary>Rechazado por la autoridad. Estado terminal.</summary>
    Rechazado,

    /// <summary>
    /// No se pudo completar el proceso. RESULTADO DESCONOCIDO: no implica
    /// que la autoridad no lo haya recibido (RN-13). Estado terminal.
    /// </summary>
    Fallido
}
