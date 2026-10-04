using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using static FevCore.Domain.Tests.Documentos.FabricaDocumentos;

namespace FevCore.Domain.Tests.Documentos;

/// <summary>RN-03, RN-04, RN-05, INV-DOC-03, INV-DOC-04.</summary>
public sealed class NotasTests
{
    // ── Lo que debe funcionar ──

    [Fact]
    public void Una_nota_credito_contra_una_factura_aprobada_se_emite()
    {
        var factura = FacturaAprobada();

        var nota = NotaCredito(factura);

        Assert.Equal(TipoDocumento.NotaCredito, nota.Tipo);
        Assert.Equal(EstadoDocumento.Recibido, nota.Estado);
        Assert.Equal(factura.Id, nota.DocumentoReferenciadoId);
        Assert.Equal(MotivoNota.DevolucionParcial, nota.Motivo);
        Assert.True(nota.EsNota);
    }

    /// <summary>
    /// La nota no pregunta a quien se le emite: lo hereda. Corregir una
    /// factura a nombre de otro adquirente no tiene sentido contable.
    /// </summary>
    [Fact]
    public void La_nota_hereda_adquirente_y_moneda_de_la_factura()
    {
        var factura = FacturaAprobada();

        var nota = NotaCredito(factura);

        Assert.Equal(factura.AdquirenteId, nota.AdquirenteId);
        Assert.Equal(factura.Moneda, nota.Moneda);
    }

    [Fact]
    public void Una_factura_no_referencia_nada()
    {
        var factura = Factura();

        Assert.Null(factura.DocumentoReferenciadoId);
        Assert.Null(factura.Motivo);
        Assert.False(factura.EsNota);
    }

    // ── RN-03: la factura debe estar aprobada ──

    [Theory]
    [InlineData(EstadoDocumento.Recibido)]
    [InlineData(EstadoDocumento.EnProceso)]
    [InlineData(EstadoDocumento.Transmitido)]
    public void No_se_corrige_una_factura_que_aun_no_fue_aprobada(EstadoDocumento estado)
    {
        var factura = Factura();

        while (factura.Estado != estado)
        {
            factura.Transicionar(
                MaquinaEstados.DestinosDesde(factura.Estado)[0], "Avanzando.", Momento);
        }

        var error = Assert.Throws<ExcepcionDominio>(() => NotaCredito(factura));

        Assert.Equal("DOCUMENTO_REFERENCIADO_NO_APROBADO", error.Codigo);
    }

    [Fact]
    public void No_se_corrige_una_factura_rechazada()
    {
        var factura = Factura();
        factura.Transicionar(EstadoDocumento.EnProceso, "Generando.", Momento);
        factura.Transicionar(EstadoDocumento.Transmitido, "Enviado.", Momento);
        factura.Transicionar(EstadoDocumento.Rechazado, "Rechazada.", Momento);

        var error = Assert.Throws<ExcepcionDominio>(() => NotaCredito(factura));

        Assert.Equal("DOCUMENTO_REFERENCIADO_NO_APROBADO", error.Codigo);
    }

    // ── RN-05: una nota no referencia otra nota ──

    [Fact]
    public void Una_nota_no_puede_referenciar_otra_nota()
    {
        var factura = FacturaAprobada();
        var primeraNota = NotaCredito(factura, cantidad: 1m);

        // Se lleva la nota hasta Aprobado para que el unico motivo posible
        // de rechazo sea su tipo, no su estado.
        primeraNota.Transicionar(EstadoDocumento.EnProceso, "Generando.", Momento);
        primeraNota.Transicionar(EstadoDocumento.Transmitido, "Enviada.", Momento);
        primeraNota.Transicionar(EstadoDocumento.Aprobado, "Aprobada.", Momento);

        var error = Assert.Throws<ExcepcionDominio>(() =>
            NotaCredito(primeraNota, referencia: "NC-002"));

        Assert.Equal("DOCUMENTO_REFERENCIADO_INVALIDO", error.Codigo);
    }

    [Fact]
    public void EmitirNota_rechaza_que_le_pidan_una_factura()
    {
        var factura = FacturaAprobada();

        var error = Assert.Throws<ExcepcionDominio>(() => Documento.EmitirNota(
            tipo: TipoDocumento.Factura,
            integradorId: Integrador,
            referenciaExterna: "X-001",
            prefijo: "SETP",
            consecutivo: 2,
            fechaEmision: Momento,
            facturaReferenciada: factura,
            motivo: MotivoNota.Otros,
            observaciones: null,
            emisorSnapshot: DatosEmisor(),
            lineas: [Linea1()],
            notasCreditoPrevias: Dinero.Cero));

        Assert.Equal("TIPO_NOTA_INVALIDO", error.Codigo);
    }

    // ── RN-04: el acumulado no supera la factura ──

    /// <summary>
    /// La factura vale 357.000. Una nota por exactamente ese valor es una
    /// anulacion total, y es legitima: el limite es "no superar", no
    /// "quedarse por debajo".
    /// </summary>
    [Fact]
    public void Una_nota_por_el_valor_exacto_de_la_factura_se_permite()
    {
        var factura = FacturaAprobada();

        var nota = NotaCredito(factura, notasPrevias: 0m);

        Assert.Equal(factura.Totales.TotalAPagar, nota.Totales.TotalAPagar);
    }

    [Fact]
    public void Una_nota_que_supera_el_acumulado_se_rechaza()
    {
        var factura = FacturaAprobada();

        // Ya se acredito la mitad; esta nota pide la factura entera.
        var error = Assert.Throws<ExcepcionDominio>(() =>
            NotaCredito(factura, notasPrevias: 178_500m));

        Assert.Equal("NOTA_EXCEDE_VALOR_FACTURA", error.Codigo);

        // El mensaje dice cuanto habia antes: sin ese dato el integrador no
        // sabe por cuanto si puede emitirla.
        Assert.Contains("178", error.Message);
    }

    [Fact]
    public void Dos_notas_que_suman_exactamente_la_factura_se_permiten()
    {
        var factura = FacturaAprobada();

        // 178.500 + 178.500 = 357.000, justo el total.
        var primera = NotaCredito(factura, cantidad: 1m, notasPrevias: 0m);
        var segunda = NotaCredito(
            factura,
            cantidad: 1m,
            notasPrevias: primera.Totales.TotalAPagar.Valor,
            referencia: "NC-002");

        Assert.Equal(
            factura.Totales.TotalAPagar,
            primera.Totales.TotalAPagar + segunda.Totales.TotalAPagar);
    }

    /// <summary>
    /// RN-04 habla solo de notas credito. Una nota debito aumenta el valor
    /// de la operacion, asi que no tiene por que caber dentro de la factura.
    /// </summary>
    [Fact]
    public void Una_nota_debito_puede_superar_el_valor_de_la_factura()
    {
        var factura = FacturaAprobada();

        var nota = Documento.EmitirNota(
            tipo: TipoDocumento.NotaDebito,
            integradorId: Integrador,
            referenciaExterna: "ND-001",
            prefijo: "NDA",
            consecutivo: 1,
            fechaEmision: Momento,
            facturaReferenciada: factura,
            motivo: MotivoNota.AjustePrecio,
            observaciones: "Intereses de mora",
            emisorSnapshot: DatosEmisor(),
            lineas: [Linea1(cantidad: 10m)],
            notasCreditoPrevias: Dinero.Cero);

        Assert.True(nota.Totales.TotalAPagar > factura.Totales.TotalAPagar);
        Assert.Equal(TipoDocumento.NotaDebito, nota.Tipo);
    }

    // ── ADR-0017: el adquirente de la nota es el de la factura ──

    /// <summary>
    /// ADR-0017, RN-10. Identidad y datos del adquirente salen del mismo
    /// sitio, la factura, y como copia: si fuera la misma instancia, la
    /// nota y la factura compartirian un objeto, que es el error que la
    /// retrospectiva 2.4 cuenta de DatosTributarios.
    /// </summary>
    [Fact]
    public void La_nota_hereda_identidad_y_datos_del_adquirente_de_la_factura_como_copia()
    {
        var factura = FacturaAprobada();

        var nota = NotaCredito(factura);

        Assert.Equal(factura.AdquirenteId, nota.AdquirenteId);
        Assert.Equal(factura.AdquirenteSnapshot, nota.AdquirenteSnapshot);
        Assert.NotSame(factura.AdquirenteSnapshot, nota.AdquirenteSnapshot);
    }
}
