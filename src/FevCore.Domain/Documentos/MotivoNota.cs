namespace FevCore.Domain.Documentos;

/// <summary>
/// Por que se emite una nota credito o debito.
///
/// La DIAN exige declarar el motivo en el XML. Los valores siguen la lista
/// del anexo tecnico; "Otros" existe porque la lista oficial tambien lo
/// contempla, no como salida facil.
/// </summary>
public enum MotivoNota
{
    DevolucionParcial,
    Anulacion,
    Rebaja,
    Descuento,
    AjustePrecio,
    Otros
}
