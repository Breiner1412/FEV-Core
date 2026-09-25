using FevCore.Domain.Comun;

namespace FevCore.Domain.Documentos;

/// <summary>
/// Los valores declarados del documento.
///
/// Se calculan siempre a partir de las lineas. Nunca se reciben desde
/// afuera ni se editan (INV-TOT-01).
///
/// El redondeo se aplica AQUI, una sola vez, sobre los totales del
/// documento — no linea por linea (RN-06, INV-TOT-02).
/// </summary>
public sealed record Totales
{
    /// <summary>Suma de cantidad x precio unitario, antes de descuentos.</summary>
    public Dinero TotalBruto { get; }

    /// <summary>Suma de los descuentos de todas las lineas.</summary>
    public Dinero TotalDescuentos { get; }

    /// <summary>Base sobre la que se liquidan los impuestos.</summary>
    public Dinero TotalBaseImponible { get; }

    /// <summary>Suma de todos los impuestos de todas las lineas.</summary>
    public Dinero TotalImpuestos { get; }

    /// <summary>Valor final del documento.</summary>
    public Dinero TotalAPagar { get; }

    /// <summary>Requerido por Entity Framework.</summary>
    private Totales() { }

    private Totales(
        Dinero totalBruto,
        Dinero totalDescuentos,
        Dinero totalBaseImponible,
        Dinero totalImpuestos,
        Dinero totalAPagar)
    {
        TotalBruto = totalBruto;
        TotalDescuentos = totalDescuentos;
        TotalBaseImponible = totalBaseImponible;
        TotalImpuestos = totalImpuestos;
        TotalAPagar = totalAPagar;
    }

    public static Totales Calcular(IReadOnlyList<Linea> lineas)
    {
        // 1. Acumular con precision completa, sin redondear nada.
        var brutoExacto = Dinero.Cero;
        var descuentosExacto = Dinero.Cero;
        var impuestosExacto = Dinero.Cero;

        foreach (var linea in lineas)
        {
            brutoExacto += linea.PrecioUnitario * linea.Cantidad;
            descuentosExacto += linea.Descuento;

            foreach (var impuesto in linea.Impuestos)
            {
                impuestosExacto += impuesto.Valor;
            }
        }

        // 2. Redondear una sola vez, aqui (RN-06).
        var totalBruto = brutoExacto.Redondear();
        var totalDescuentos = descuentosExacto.Redondear();
        var totalImpuestos = impuestosExacto.Redondear();

        // 3. Derivar el resto de los valores YA redondeados, para que los
        //    numeros que el documento declara cuadren entre si. Si cada uno
        //    se redondeara por separado desde su valor exacto, la resta y la
        //    suma podrian diferir en un centavo del valor declarado, y esa
        //    inconsistencia es motivo de rechazo.
        var totalBaseImponible = totalBruto - totalDescuentos;
        var totalAPagar = totalBaseImponible + totalImpuestos;

        return new Totales(
            totalBruto,
            totalDescuentos,
            totalBaseImponible,
            totalImpuestos,
            totalAPagar);
    }
}
