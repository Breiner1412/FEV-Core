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

/// <summary>
/// Como identifica la DIAN cada tipo de impuesto.
///
/// IVA, IVA exento e IVA excluido son el mismo impuesto con distinta
/// tarifa: los tres se declaran con el codigo 01. Vive en el dominio, y no
/// en el generador de XML, porque el codigo unico tambien lo necesita: el
/// valor de IVA que entra en el CUFE es todo lo que el XML declara con 01.
/// </summary>
public static class CodigoDianImpuesto
{
    public const string Iva = "01";
    public const string Inc = "04";

    public static string CodigoDian(this TipoImpuesto tipo) => tipo switch
    {
        TipoImpuesto.Inc => Inc,
        _ => Iva
    };

    public static string NombreDian(this TipoImpuesto tipo) => tipo switch
    {
        TipoImpuesto.Inc => "INC",
        _ => "IVA"
    };
}
