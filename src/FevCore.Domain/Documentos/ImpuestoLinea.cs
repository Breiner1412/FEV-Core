using FevCore.Domain.Comun;

namespace FevCore.Domain.Documentos;

/// <summary>
/// Un impuesto ya calculado sobre una linea.
///
/// La tarifa se copia al momento de emitir. Si el producto cambia de
/// tarifa despues, este valor no se mueve (INV-LIN-03).
/// </summary>
public sealed record ImpuestoLinea
{
    public TipoImpuesto Tipo { get; }
    public decimal Tarifa { get; }
    public Dinero BaseGravable { get; }
    public Dinero Valor { get; }

    private ImpuestoLinea(
        TipoImpuesto tipo,
        decimal tarifa,
        Dinero baseGravable,
        Dinero valor)
    {
        Tipo = tipo;
        Tarifa = tarifa;
        BaseGravable = baseGravable;
        Valor = valor;
    }

    /// <summary>
    /// Calcula el impuesto sobre una base.
    ///
    /// NO redondea. El redondeo se aplica una sola vez, sobre el total del
    /// documento (RN-06, INV-IMP-02). Redondear aqui produciria diferencias
    /// de centavos entre la suma de las lineas y el total declarado, que es
    /// una causa documentada de rechazo.
    /// </summary>
    public static ImpuestoLinea Calcular(
        TipoImpuesto tipo,
        decimal tarifa,
        Dinero baseGravable)
    {
        if (tarifa < 0m)
        {
            throw new ExcepcionDominio(
                "IMPUESTO_TARIFA_INVALIDA",
                $"La tarifa de {tipo} no puede ser negativa. Recibida: {tarifa}.");
        }

        var valor = baseGravable * (tarifa / 100m);

        return new ImpuestoLinea(tipo, tarifa, baseGravable, valor);
    }
}
