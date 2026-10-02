namespace FevCore.Domain.Documentos;

/// <summary>
/// Estados por los que pasa un documento. Ver la maquina de estados en la
/// seccion 6 del documento de requerimientos y su traduccion a codigo en
/// MaquinaEstados.
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
    /// No se llego a un desenlace y hace falta una persona. No implica que la
    /// autoridad no lo haya recibido: el detalle de la transicion dice que
    /// consta, y si hay que verificar ante la DIAN (RN-13, ADR-0015).
    /// Estado terminal.
    /// </summary>
    Fallido
}
