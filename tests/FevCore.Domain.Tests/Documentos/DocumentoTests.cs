using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Domain.Tests.Documentos;

public sealed class DocumentoTests
{
    private static readonly Guid Integrador = Guid.CreateVersion7();

    private static Linea CrearLinea(
        int numero = 1,
        decimal cantidad = 2m,
        decimal precioUnitario = 150_000m,
        decimal? descuento = null,
        decimal tarifaIva = 19m) =>
        Linea.Crear(
            numero: numero,
            codigo: $"PROD-{numero:000}",
            descripcion: "Teclado mecanico",
            unidadMedida: "94",
            cantidad: cantidad,
            precioUnitario: Dinero.Desde(precioUnitario),
            impuestos: [new EspecificacionImpuesto(TipoImpuesto.Iva, tarifaIva)],
            descuento: descuento is null ? null : Dinero.Desde(descuento.Value));

    private static Documento EmitirCon(params Linea[] lineas) =>
        Documento.EmitirFactura(
            integradorId: Integrador,
            referenciaExterna: "VTA-2026-000145",
            prefijo: "SETP",
            consecutivo: 990_000_123,
            fechaEmision: new DateTimeOffset(2026, 9, 25, 14, 30, 0, TimeSpan.FromHours(-5)),
            lineas: lineas);

    // ── Emision basica ──

    [Fact]
    public void Una_factura_nace_en_estado_recibido()
    {
        var factura = EmitirCon(CrearLinea());

        Assert.Equal(EstadoDocumento.Recibido, factura.Estado);
        Assert.Equal(TipoDocumento.Factura, factura.Tipo);
    }

    [Fact]
    public void Una_factura_nace_con_identificador_propio()
    {
        var primera = EmitirCon(CrearLinea());
        var segunda = EmitirCon(CrearLinea());

        Assert.NotEqual(Guid.Empty, primera.Id);
        Assert.NotEqual(primera.Id, segunda.Id);
    }

    [Fact]
    public void El_numero_completo_junta_prefijo_y_consecutivo()
    {
        var factura = EmitirCon(CrearLinea());

        Assert.Equal("SETP990000123", factura.NumeroCompleto);
    }

    [Fact]
    public void Las_lineas_quedan_ordenadas_por_numero()
    {
        var factura = EmitirCon(
            CrearLinea(numero: 3),
            CrearLinea(numero: 1),
            CrearLinea(numero: 2));

        Assert.Equal(new[] { 1, 2, 3 }, factura.Lineas.Select(l => l.Numero).ToArray());
    }

    // ── INV-DOC-01 (RN-08) ──

    [Fact]
    public void Rechaza_una_factura_sin_lineas()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => EmitirCon());

        Assert.Equal("DOCUMENTO_SIN_LINEAS", error.Codigo);
    }

    [Fact]
    public void Rechaza_dos_lineas_con_el_mismo_numero()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => EmitirCon(CrearLinea(numero: 1), CrearLinea(numero: 1)));

        Assert.Equal("LINEAS_CON_NUMERO_REPETIDO", error.Codigo);
    }

    // ── RF-15: la referencia externa es obligatoria ──

    [Fact]
    public void Rechaza_una_factura_sin_referencia_externa()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => Documento.EmitirFactura(
            integradorId: Integrador,
            referenciaExterna: "   ",
            prefijo: "SETP",
            consecutivo: 1,
            fechaEmision: DateTimeOffset.UtcNow,
            lineas: [CrearLinea()]));

        Assert.Equal("REFERENCIA_EXTERNA_REQUERIDA", error.Codigo);
    }

    // ── RF-02: se registra quien emitio ──

    [Fact]
    public void Rechaza_una_factura_sin_integrador()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => Documento.EmitirFactura(
            integradorId: Guid.Empty,
            referenciaExterna: "VTA-001",
            prefijo: "SETP",
            consecutivo: 1,
            fechaEmision: DateTimeOffset.UtcNow,
            lineas: [CrearLinea()]));

        Assert.Equal("INTEGRADOR_REQUERIDO", error.Codigo);
    }

    // ── RN-09 e INV-DOC-02: los totales cuadran ──

    [Fact]
    public void Los_totales_se_calculan_desde_las_lineas()
    {
        var factura = EmitirCon(
            CrearLinea(numero: 1, cantidad: 2m, precioUnitario: 150_000m),
            CrearLinea(numero: 2, cantidad: 1m, precioUnitario: 89_900m, descuento: 9_900m));

        // Bruto:      300.000 + 89.900 = 389.900
        // Descuentos:                      9.900
        // Base:       300.000 + 80.000 = 380.000
        // IVA 19%:     57.000 + 15.200 =  72.200
        // Total:                          452.200
        Assert.Equal(389_900m, factura.Totales.TotalBruto.Valor);
        Assert.Equal(9_900m, factura.Totales.TotalDescuentos.Valor);
        Assert.Equal(380_000m, factura.Totales.TotalBaseImponible.Valor);
        Assert.Equal(72_200m, factura.Totales.TotalImpuestos.Valor);
        Assert.Equal(452_200m, factura.Totales.TotalAPagar.Valor);
    }

    [Fact]
    public void El_total_siempre_es_la_base_mas_los_impuestos()
    {
        var factura = EmitirCon(
            CrearLinea(numero: 1, cantidad: 3m, precioUnitario: 12_345.67m),
            CrearLinea(numero: 2, cantidad: 7m, precioUnitario: 987.65m, descuento: 100.11m),
            CrearLinea(numero: 3, cantidad: 1m, precioUnitario: 55_555.55m, tarifaIva: 5m));

        var t = factura.Totales;

        Assert.Equal(
            t.TotalBaseImponible.Valor + t.TotalImpuestos.Valor,
            t.TotalAPagar.Valor);

        Assert.Equal(
            t.TotalBruto.Valor - t.TotalDescuentos.Valor,
            t.TotalBaseImponible.Valor);
    }

    // ── RN-06: el redondeo va sobre el total, no linea por linea ──

    [Fact]
    public void El_redondeo_se_aplica_sobre_el_total_y_no_linea_por_linea()
    {
        // Tres lineas identicas de 1.000,01 con IVA del 19%.
        // IVA exacto por linea: 190,0019
        //
        // Redondeando por linea:  190,00 x 3 = 570,00
        // Redondeando el total:   570,0057    =  570,01
        //
        // Un centavo de diferencia. La autoridad compara el total declarado
        // contra la sumatoria, y esa diferencia es motivo de rechazo.
        var factura = EmitirCon(
            CrearLinea(numero: 1, cantidad: 1m, precioUnitario: 1_000.01m),
            CrearLinea(numero: 2, cantidad: 1m, precioUnitario: 1_000.01m),
            CrearLinea(numero: 3, cantidad: 1m, precioUnitario: 1_000.01m));

        Assert.Equal(570.01m, factura.Totales.TotalImpuestos.Valor);
        Assert.NotEqual(570.00m, factura.Totales.TotalImpuestos.Valor);

        Assert.Equal(3_000.03m, factura.Totales.TotalBaseImponible.Valor);
        Assert.Equal(3_570.04m, factura.Totales.TotalAPagar.Valor);
    }

    [Fact]
    public void Las_lineas_conservan_sus_decimales_exactos()
    {
        // El redondeo del total no altera lo que cada linea guarda.
        var factura = EmitirCon(
            CrearLinea(numero: 1, cantidad: 1m, precioUnitario: 1_000.01m));

        Assert.Equal(190.0019m, factura.Lineas.Single().Impuestos.Single().Valor.Valor);
    }

    // ── Datos del documento ──

    [Fact]
    public void La_moneda_se_normaliza_a_mayusculas()
    {
        var factura = Documento.EmitirFactura(
            integradorId: Integrador,
            referenciaExterna: "VTA-001",
            prefijo: "SETP",
            consecutivo: 1,
            fechaEmision: DateTimeOffset.UtcNow,
            lineas: [CrearLinea()],
            moneda: "cop");

        Assert.Equal("COP", factura.Moneda);
    }

    [Fact]
    public void La_lista_de_lineas_es_de_solo_lectura()
    {
        var factura = EmitirCon(CrearLinea());

        Assert.False(factura.Lineas is List<Linea>);
    }
}
