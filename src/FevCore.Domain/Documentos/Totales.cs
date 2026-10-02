using FevCore.Domain.Comun;

namespace FevCore.Domain.Documentos;

/// <summary>
/// Los valores declarados del documento.
///
/// Se calculan siempre a partir de las lineas. Nunca se reciben desde
/// afuera ni se editan (INV-TOT-01).
///
/// El redondeo nunca es linea por linea (RN-06, INV-TOT-02). Los importes
/// se redondean una vez sobre el documento; los impuestos, una vez por
/// grupo de tipo y tarifa, y el total es la suma de los grupos (ADR-0016).
/// Asi el impuesto total es exactamente la suma de los subtotales que el
/// documento declara.
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

        foreach (var linea in lineas)
        {
            brutoExacto += linea.PrecioUnitario * linea.Cantidad;
            descuentosExacto += linea.Descuento;
        }

        // 2. Redondear una sola vez, aqui (RN-06).
        var totalBruto = brutoExacto.Redondear();
        var totalDescuentos = descuentosExacto.Redondear();

        // El impuesto total es la suma de los grupos, cada uno ya redondeado
        // una vez. Redondear la suma exacta de todo daria otro numero cuando
        // dos grupos caen en medio centavo, y el documento declararia un
        // total que no es la suma de sus subtotales (ADR-0016).
        var totalImpuestos = SubtotalImpuesto.Agrupar(lineas)
            .Aggregate(Dinero.Cero, (suma, grupo) => suma + grupo.Valor);

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
