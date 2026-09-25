using System.Globalization;
using FevCore.Domain.Comun;

namespace FevCore.Domain.Tests.Comun;

public sealed class DineroTests
{
    // ── Construccion ──

    [Fact]
    public void Cero_vale_cero()
    {
        Assert.Equal(0m, Dinero.Cero.Valor);
    }

    [Fact]
    public void Se_construye_desde_un_decimal()
    {
        Assert.Equal(150_000m, Dinero.Desde(150_000m).Valor);
    }

    [Fact]
    public void No_acepta_valores_negativos()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Dinero.Desde(-1m));
    }

    // ── Operaciones ──

    [Fact]
    public void Suma_dos_valores()
    {
        var resultado = Dinero.Desde(150_000m) + Dinero.Desde(89_900m);

        Assert.Equal(239_900m, resultado.Valor);
    }

    [Fact]
    public void Resta_dos_valores()
    {
        var resultado = Dinero.Desde(89_900m) - Dinero.Desde(9_900m);

        Assert.Equal(80_000m, resultado.Valor);
    }

    [Fact]
    public void Resta_que_daria_negativo_es_rechazada()
    {
        var valorLinea = Dinero.Desde(100_000m);
        var descuentoExcesivo = Dinero.Desde(150_000m);

        Assert.Throws<InvalidOperationException>(
            () => valorLinea - descuentoExcesivo);
    }

    [Fact]
    public void Multiplica_por_una_cantidad()
    {
        var total = Dinero.Desde(150_000m) * 3m;

        Assert.Equal(450_000m, total.Valor);
    }

    [Fact]
    public void Compara_valores()
    {
        Assert.True(Dinero.Desde(100m) > Dinero.Desde(50m));
        Assert.True(Dinero.Desde(50m) < Dinero.Desde(100m));
        Assert.True(Dinero.Desde(100m) >= Dinero.Desde(100m));
    }

    [Fact]
    public void Dos_valores_iguales_son_iguales()
    {
        Assert.Equal(Dinero.Desde(100m), Dinero.Desde(100m));
    }

    // ── Redondeo ──

    [Theory]
    [InlineData("0.004", "0.00")]
    [InlineData("0.005", "0.01")]
    [InlineData("0.015", "0.02")]
    [InlineData("2.344", "2.34")]
    [InlineData("2.345", "2.35")]
    [InlineData("357000.00", "357000.00")]
    public void Redondea_el_punto_medio_hacia_arriba(string entrada, string esperado)
    {
        var valor = decimal.Parse(entrada, CultureInfo.InvariantCulture);
        var resultadoEsperado = decimal.Parse(esperado, CultureInfo.InvariantCulture);

        Assert.Equal(resultadoEsperado, Dinero.Desde(valor).Redondear().Valor);
    }

    [Fact]
    public void No_usa_el_redondeo_bancario_que_dotnet_trae_por_defecto()
    {
        // Math.Round redondea al par mas cercano si no se le indica otra cosa.
        // Para 0.005 eso da 0.00, porque 0 es par.
        Assert.Equal(0.00m, Math.Round(0.005m, 2));

        // Dinero redondea hacia arriba, que es lo que espera el calculo tributario.
        Assert.Equal(0.01m, Dinero.Desde(0.005m).Redondear().Valor);
    }

    // ── Precision: la razon de existir de este tipo ──

    [Fact]
    public void No_sufre_el_error_del_punto_flotante()
    {
        // Asi se comporta el punto flotante: 0.1 + 0.2 no da exactamente 0.3.
        double conPuntoFlotante = 0.1 + 0.2;
        Assert.NotEqual(0.3, conPuntoFlotante);

        // Dinero si da el valor exacto.
        var conDinero = Dinero.Desde(0.1m) + Dinero.Desde(0.2m);
        Assert.Equal(0.3m, conDinero.Valor);
    }

    [Fact]
    public void Mil_sumas_pequenas_no_desvian_el_total()
    {
        double acumuladoPuntoFlotante = 0;
        var acumuladoDinero = Dinero.Cero;

        for (var i = 0; i < 1000; i++)
        {
            acumuladoPuntoFlotante += 0.01;
            acumuladoDinero += Dinero.Desde(0.01m);
        }

        // El punto flotante se desvia: no llega exactamente a 10.
        Assert.NotEqual(10.0, acumuladoPuntoFlotante);

        // Dinero llega exacto. Esta es la diferencia que hace que la DIAN
        // no rechace el documento por inconsistencia de centavos.
        Assert.Equal(10.00m, acumuladoDinero.Valor);
    }
}
