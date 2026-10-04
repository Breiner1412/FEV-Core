using FevCore.Domain.Comun;

namespace FevCore.Domain.Documentos;

/// <summary>
/// Un grupo de impuesto del documento: un tipo con una tarifa, sumado sobre
/// todas las lineas y redondeado UNA vez (RN-06, ADR-0016).
///
/// Es la fuente unica de los impuestos que declara un documento. De aqui
/// salen el impuesto total de Totales, los TaxSubtotal del XML y los
/// valores de IVA e INC del codigo unico. Antes cada uno redondeaba por su
/// cuenta y, con dos grupos que caian en medio centavo, el XML declaraba un
/// total que no era la suma de sus subtotales, y el CUFE uno que no era la
/// suma de sus partes.
/// </summary>
public sealed record SubtotalImpuesto(
    TipoImpuesto Tipo,
    decimal Tarifa,
    Dinero BaseGravable,
    Dinero Valor)
{
    /// <summary>
    /// Agrupa por tipo y tarifa, acumula con precision completa y redondea
    /// cada grupo una sola vez. Nunca linea por linea, que es lo que RN-06
    /// prohibe: es el redondeo que acumula error.
    /// </summary>
    public static IReadOnlyList<SubtotalImpuesto> Agrupar(IEnumerable<Linea> lineas) =>
        [.. lineas
            .SelectMany(l => l.Impuestos)
            .GroupBy(i => (i.Tipo, i.Tarifa))
            .OrderBy(g => g.Key.Tipo)
            .ThenBy(g => g.Key.Tarifa)
            .Select(g => new SubtotalImpuesto(
                g.Key.Tipo,
                g.Key.Tarifa,
                g.Aggregate(Dinero.Cero, (suma, i) => suma + i.BaseGravable).Redondear(),
                g.Aggregate(Dinero.Cero, (suma, i) => suma + i.Valor).Redondear()))];
}
