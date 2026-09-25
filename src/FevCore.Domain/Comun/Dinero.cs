using System.Globalization;

namespace FevCore.Domain.Comun;

/// <summary>
/// Un valor monetario. Existe para que sea imposible calcular dinero
/// con punto flotante por descuido (RNF-12).
///
/// No lleva moneda: la version 1 opera solo en pesos colombianos, y todos
/// los valores de un documento comparten la moneda del documento. Agregar
/// moneda aqui seria resolver un problema que todavia no existe.
/// </summary>
public readonly record struct Dinero : IComparable<Dinero>
{
    public static readonly Dinero Cero = new(0m);

    public decimal Valor { get; }

    private Dinero(decimal valor) => Valor = valor;

    /// <summary>
    /// Unica forma de construir un valor monetario.
    /// Rechaza negativos: en este dominio no existen.
    /// </summary>
    public static Dinero Desde(decimal valor)
    {
        if (valor < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(valor),
                valor,
                "Un valor monetario no puede ser negativo.");
        }

        return new Dinero(valor);
    }

    /// <summary>
    /// Redondea a la cantidad de decimales indicada.
    ///
    /// Usa redondeo hacia arriba en el punto medio, NO el redondeo bancario
    /// que .NET aplica por defecto. Math.Round(0.005m, 2) devuelve 0.00
    /// porque redondea al par mas cercano; el calculo tributario espera 0.01.
    ///
    /// Este metodo se aplica sobre el total del documento, no linea por
    /// linea (RN-06, INV-TOT-02).
    /// </summary>
    public Dinero Redondear(int decimales = 2) =>
        new(Math.Round(Valor, decimales, MidpointRounding.AwayFromZero));

    public static Dinero operator +(Dinero a, Dinero b) =>
        new(a.Valor + b.Valor);

    /// <summary>
    /// Resta. Falla si el resultado seria negativo: eso siempre indica
    /// un error de calculo, como un descuento mayor que el valor de la
    /// linea (INV-LIN-02).
    /// </summary>
    public static Dinero operator -(Dinero a, Dinero b)
    {
        var resultado = a.Valor - b.Valor;

        if (resultado < 0m)
        {
            throw new InvalidOperationException(
                $"La resta produce un valor negativo: {a.Valor} - {b.Valor}.");
        }

        return new Dinero(resultado);
    }

    public static Dinero operator *(Dinero dinero, decimal factor)
    {
        if (factor < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(factor),
                factor,
                "El factor no puede ser negativo.");
        }

        return new Dinero(dinero.Valor * factor);
    }

    public static bool operator <(Dinero a, Dinero b) => a.Valor < b.Valor;
    public static bool operator >(Dinero a, Dinero b) => a.Valor > b.Valor;
    public static bool operator <=(Dinero a, Dinero b) => a.Valor <= b.Valor;
    public static bool operator >=(Dinero a, Dinero b) => a.Valor >= b.Valor;

    public int CompareTo(Dinero otro) => Valor.CompareTo(otro.Valor);

    public override string ToString() =>
        Valor.ToString("0.00", CultureInfo.InvariantCulture);
}
