namespace FevCore.Domain.Documentos;

/// <summary>
/// Tipos de impuesto que puede llevar una linea.
///
/// Es un conjunto cerrado y conocido, no un catalogo administrable:
/// agregar un tipo es una decision de desarrollo, no de configuracion
/// (seccion 7 del modelo de dominio).
/// </summary>
public enum TipoImpuesto
{
    Iva,
    Inc,
    IvaExento,
    IvaExcluido
}
