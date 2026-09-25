namespace FevCore.Domain.Documentos;

/// <summary>
/// Factura, nota credito y nota debito NO son tres entidades distintas.
/// Son un Documento con distinto tipo y distintas invariantes activas
/// (seccion 7 del modelo de dominio).
/// </summary>
public enum TipoDocumento
{
    Factura,
    NotaCredito,
    NotaDebito
}
