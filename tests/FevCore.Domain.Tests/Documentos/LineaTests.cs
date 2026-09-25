using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Domain.Tests.Documentos;

public sealed class LineaTests
{
    // ── Ayudante: una linea valida, para no repetir lo mismo en cada prueba ──

    private static Linea CrearLineaValida(
        decimal cantidad = 2m,
        decimal precioUnitario = 150_000m,
        decimal? descuento = null,
        IEnumerable<EspecificacionImpuesto>? impuestos = null) =>
        Linea.Crear(
            numero: 1,
            codigo: "PROD-001",
            descripcion: "Teclado mecanico",
            unidadMedida: "94",
            cantidad: cantidad,
            precioUnitario: Dinero.Desde(precioUnitario),
            impuestos: impuestos ?? [new EspecificacionImpuesto(TipoImpuesto.Iva, 19m)],
            descuento: descuento is null ? null : Dinero.Desde(descuento.Value));

    // ── Calculo ──

    [Fact]
    public void La_base_gravable_es_cantidad_por_precio()
    {
        var linea = CrearLineaValida(cantidad: 2m, precioUnitario: 150_000m);

        Assert.Equal(300_000m, linea.BaseGravable.Valor);
    }

    [Fact]
    public void El_descuento_se_resta_de_la_base_gravable()
    {
        var linea = CrearLineaValida(
            cantidad: 1m,
            precioUnitario: 89_900m,
            descuento: 9_900m);

        Assert.Equal(80_000m, linea.BaseGravable.Valor);
    }

    [Fact]
    public void El_impuesto_se_calcula_sobre_la_base_gravable_no_sobre_el_bruto()
    {
        var linea = CrearLineaValida(
            cantidad: 1m,
            precioUnitario: 100_000m,
            descuento: 20_000m);

        // Base: 100.000 - 20.000 = 80.000. IVA 19% de 80.000 = 15.200.
        Assert.Equal(80_000m, linea.BaseGravable.Valor);
        Assert.Equal(15_200m, linea.Impuestos.Single().Valor.Valor);
    }

    [Fact]
    public void El_total_es_la_base_mas_los_impuestos()
    {
        var linea = CrearLineaValida(cantidad: 2m, precioUnitario: 150_000m);

        // Base 300.000 + IVA 57.000 = 357.000
        Assert.Equal(357_000m, linea.Total.Valor);
    }

    [Fact]
    public void Una_linea_puede_llevar_varios_impuestos()
    {
        var linea = CrearLineaValida(
            cantidad: 1m,
            precioUnitario: 100_000m,
            impuestos:
            [
                new EspecificacionImpuesto(TipoImpuesto.Iva, 19m),
                new EspecificacionImpuesto(TipoImpuesto.Inc, 8m)
            ]);

        Assert.Equal(2, linea.Impuestos.Count);
        Assert.Equal(127_000m, linea.Total.Valor);
    }

    [Fact]
    public void Una_linea_puede_no_llevar_impuestos()
    {
        var linea = CrearLineaValida(
            cantidad: 1m,
            precioUnitario: 100_000m,
            impuestos: []);

        Assert.Empty(linea.Impuestos);
        Assert.Equal(100_000m, linea.Total.Valor);
    }

    [Fact]
    public void El_impuesto_no_se_redondea_en_la_linea()
    {
        // 33.333 x 19% = 6333.27 exacto. Si la linea redondeara,
        // perderiamos los decimales que el total necesita para cuadrar.
        var linea = CrearLineaValida(
            cantidad: 1m,
            precioUnitario: 33_333m,
            impuestos: [new EspecificacionImpuesto(TipoImpuesto.Iva, 19m)]);

        Assert.Equal(6_333.27m, linea.Impuestos.Single().Valor.Valor);
    }

    // ── INV-LIN-01 (RN-08): cantidad y precio mayores que cero ──

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rechaza_cantidades_que_no_sean_positivas(decimal cantidad)
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => CrearLineaValida(cantidad: cantidad));

        Assert.Equal("LINEA_CANTIDAD_INVALIDA", error.Codigo);
    }

    [Fact]
    public void Rechaza_precio_unitario_en_cero()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => CrearLineaValida(precioUnitario: 0m));

        Assert.Equal("LINEA_PRECIO_INVALIDO", error.Codigo);
    }

    // ── INV-LIN-02: el descuento no supera el valor de la linea ──

    [Fact]
    public void Rechaza_un_descuento_mayor_que_el_valor_bruto()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => CrearLineaValida(
                cantidad: 1m,
                precioUnitario: 100_000m,
                descuento: 150_000m));

        Assert.Equal("LINEA_DESCUENTO_EXCESIVO", error.Codigo);
    }

    [Fact]
    public void Acepta_un_descuento_igual_al_valor_bruto()
    {
        // El limite es inclusivo: regalar el producto es valido.
        var linea = CrearLineaValida(
            cantidad: 1m,
            precioUnitario: 100_000m,
            descuento: 100_000m);

        Assert.Equal(0m, linea.BaseGravable.Valor);
        Assert.Equal(0m, linea.Total.Valor);
    }

    // ── INV-IMP-03: no hay dos impuestos del mismo tipo ──

    [Fact]
    public void Rechaza_dos_impuestos_del_mismo_tipo()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => CrearLineaValida(impuestos:
            [
                new EspecificacionImpuesto(TipoImpuesto.Iva, 19m),
                new EspecificacionImpuesto(TipoImpuesto.Iva, 5m)
            ]));

        Assert.Equal("LINEA_IMPUESTO_DUPLICADO", error.Codigo);
    }

    [Fact]
    public void Rechaza_una_tarifa_negativa()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => CrearLineaValida(impuestos:
            [
                new EspecificacionImpuesto(TipoImpuesto.Iva, -19m)
            ]));

        Assert.Equal("IMPUESTO_TARIFA_INVALIDA", error.Codigo);
    }

    // ── Datos obligatorios ──

    [Fact]
    public void Rechaza_una_descripcion_vacia()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => Linea.Crear(
            numero: 1,
            codigo: "PROD-001",
            descripcion: "   ",
            unidadMedida: "94",
            cantidad: 1m,
            precioUnitario: Dinero.Desde(1_000m),
            impuestos: []));

        Assert.Equal("LINEA_DESCRIPCION_REQUERIDA", error.Codigo);
    }

    [Fact]
    public void Rechaza_un_numero_de_linea_menor_que_uno()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => Linea.Crear(
            numero: 0,
            codigo: "PROD-001",
            descripcion: "Teclado mecanico",
            unidadMedida: "94",
            cantidad: 1m,
            precioUnitario: Dinero.Desde(1_000m),
            impuestos: []));

        Assert.Equal("LINEA_NUMERO_INVALIDO", error.Codigo);
    }

    // ── INV-LIN-04: la referencia al producto es informativa ──

    [Fact]
    public void Una_linea_es_valida_sin_referencia_a_producto()
    {
        var linea = CrearLineaValida();

        Assert.Null(linea.ProductoId);
        Assert.Equal("PROD-001", linea.Codigo);
    }
}
