using System.Xml.Linq;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Infrastructure.Xml;

namespace FevCore.Integration.Tests;

/// <summary>
/// CE-05: el XML generado valida contra el esquema oficial de UBL 2.1.
///
/// No necesita base de datos. Es una prueba de integracion porque se ejecuta
/// contra un artefacto externo real —los XSD publicados por OASIS— y no
/// contra una imitacion.
///
/// Recordatorio de lo que esto NO prueba: validar contra el esquema
/// significa que la estructura es correcta, no que la DIAN vaya a aceptar el
/// documento. El anexo tecnico anade centenares de validaciones de negocio
/// que ningun XSD expresa.
/// </summary>
public sealed class GeneracionXmlTests
{
    private const string ClaveTecnica = "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c";

    // ── Validacion ──
    //
    // La logica vive en ValidadorEsquemas para que tambien la usen las
    // pruebas de punta a punta, y para que la explicacion de por que hay que
    // cargar el esquema de firma a mano este en un solo sitio.

    private static void AssertValida(string xml, string documentoPrincipal) =>
        ValidadorEsquemas.AssertValida(xml, documentoPrincipal);

    // ── Documentos de ejemplo ──

    private static DatosTributarios DatosEmisor() => DatosTributarios.Crear(
        tipoIdentificacion: DatosTributarios.TipoNit,
        identificacion: "800197268",
        razonSocial: "Comercializadora del Eje SAS",
        direccion: "Calle 20 # 8-45",
        municipioCodigo: "66001",
        regimen: "48",
        digitoVerificacion: "4",
        responsabilidades: ["O-13"]);

    private static DatosTributarios DatosAdquirente() => DatosTributarios.Crear(
        tipoIdentificacion: "13",
        identificacion: "1088123456",
        razonSocial: "Juan Perez",
        direccion: "Carrera 10 # 5-20",
        municipioCodigo: "66001",
        regimen: "49");

    private static Linea CrearLinea(int numero = 1, decimal cantidad = 2m) =>
        Linea.Crear(
            numero: numero,
            codigo: $"PROD-{numero:000}",
            descripcion: "Teclado mecanico",
            unidadMedida: "94",
            cantidad: cantidad,
            precioUnitario: Dinero.Desde(150_000m),
            impuestos: [new EspecificacionImpuesto(TipoImpuesto.Iva, 19m)]);

    private static Documento Factura(params Linea[] lineas) =>
        Documento.EmitirFactura(
            integradorId: Guid.CreateVersion7(),
            referenciaExterna: "VTA-001",
            prefijo: "SETP",
            consecutivo: 990_000_123,
            fechaEmision: new DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.Zero),
            adquirenteId: Guid.CreateVersion7(),
            emisorSnapshot: DatosEmisor(),
            adquirenteSnapshot: DatosAdquirente(),
            lineas: lineas.Length == 0 ? [CrearLinea()] : lineas);

    private static GeneradorXmlUbl Generador() => new(AmbienteDian.Pruebas);

    // ── Las pruebas ──

    [Fact]
    public void Una_factura_valida_contra_el_esquema_oficial()
    {
        var resultado = Generador().Generar(Factura(), ClaveTecnica);

        AssertValida(resultado.Xml, "UBL-Invoice-2.1.xsd");
    }

    [Fact]
    public void Una_factura_de_varias_lineas_valida_contra_el_esquema()
    {
        var resultado = Generador().Generar(
            Factura(CrearLinea(1), CrearLinea(2, 5m), CrearLinea(3, 1m)),
            ClaveTecnica);

        AssertValida(resultado.Xml, "UBL-Invoice-2.1.xsd");
    }

    [Fact]
    public void El_codigo_unico_devuelto_es_el_que_quedo_en_el_xml()
    {
        var resultado = Generador().Generar(Factura(), ClaveTecnica);

        var documento = XDocument.Parse(resultado.Xml);
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var enXml = documento.Root!.Element(cbc + "UUID")!;

        Assert.Equal(resultado.CodigoUnico, enXml.Value);
        Assert.Equal("CUFE-SHA384", enXml.Attribute("schemeName")!.Value);
    }

    /// <summary>
    /// La hora del XML y la del codigo unico son la misma cadena. Si
    /// divergieran, la DIAN recalcularia el codigo desde el XML y obtendria
    /// otro: rechazo sin ninguna pista de por que.
    /// </summary>
    [Fact]
    public void La_fecha_y_la_hora_del_xml_son_las_de_Colombia()
    {
        var resultado = Generador().Generar(Factura(), ClaveTecnica);

        var documento = XDocument.Parse(resultado.Xml);
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        // Emitida a las 15:30 UTC, o sea las 10:30 en Colombia.
        Assert.Equal("2026-09-28", documento.Root!.Element(cbc + "IssueDate")!.Value);
        Assert.Equal("10:30:00-05:00", documento.Root!.Element(cbc + "IssueTime")!.Value);
    }

    [Fact]
    public void Los_importes_llevan_la_moneda_declarada()
    {
        var resultado = Generador().Generar(Factura(), ClaveTecnica);

        var documento = XDocument.Parse(resultado.Xml);
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var pagar = documento.Root!
            .Element(cac + "LegalMonetaryTotal")!
            .Element(cbc + "PayableAmount")!;

        Assert.Equal("357000.00", pagar.Value);
        Assert.Equal("COP", pagar.Attribute("currencyID")!.Value);
    }

    /// <summary>
    /// RN-06, RN-09 y ADR-0016: el XML y el codigo unico cuadran, cada uno
    /// consigo mismo y entre ellos.
    ///
    /// Son dos documentos distintos calculados del mismo dominio, y el
    /// problema original era justamente que no cuadraban. El total se
    /// redondeaba una vez sobre el documento; cada subtotal, y cada valor
    /// del CUFE, por su cuenta.
    ///
    /// Tres grupos que caen en medio centavo: IVA 19% sobre 10.50 (1.995),
    /// IVA 5% sobre 0.10 (0.005) e INC 8% sobre 0.0625 (0.005). Hacen falta
    /// DOS grupos de IVA: con uno solo por codigo, redondear por codigo o por
    /// grupo da lo mismo, y la comparacion con el CUFE no podria fallar. La
    /// primera version de esta prueba tenia ese hueco y lo destapo una
    /// mutacion.
    /// </summary>
    [Fact]
    public void El_xml_y_el_codigo_unico_cuadran_consigo_mismos_y_entre_ellos()
    {
        var factura = Factura(
            Linea.Crear(
                numero: 1, codigo: "PROD-001", descripcion: "Con IVA", unidadMedida: "94",
                cantidad: 1m, precioUnitario: Dinero.Desde(10.50m),
                impuestos: [new EspecificacionImpuesto(TipoImpuesto.Iva, 19m)]),
            Linea.Crear(
                numero: 2, codigo: "PROD-002", descripcion: "Con INC", unidadMedida: "94",
                cantidad: 1m, precioUnitario: Dinero.Desde(0.0625m),
                impuestos: [new EspecificacionImpuesto(TipoImpuesto.Inc, 8m)]),
            Linea.Crear(
                numero: 3, codigo: "PROD-003", descripcion: "Con IVA reducido", unidadMedida: "94",
                cantidad: 1m, precioUnitario: Dinero.Desde(0.10m),
                impuestos: [new EspecificacionImpuesto(TipoImpuesto.Iva, 5m)]));

        static decimal Numero(string texto) =>
            decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);

        // ── 1. El XML: TaxAmount ES la suma de sus TaxSubtotal ──
        var documento = XDocument.Parse(Generador().Generar(factura, ClaveTecnica).Xml);
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var impuestos = documento.Root!.Element(cac + "TaxTotal")!;
        var impuestoXml = Numero(impuestos.Element(cbc + "TaxAmount")!.Value);

        var sumaSubtotales = impuestos.Elements(cac + "TaxSubtotal")
            .Sum(s => Numero(s.Element(cbc + "TaxAmount")!.Value));

        Assert.Equal(impuestoXml, sumaSubtotales);

        // ── 2. El CUFE: ValTot es ValFac mas sus impuestos ──
        var cufe = ValoresCufe.Para(factura, ClaveTecnica, AmbienteDian.Pruebas);

        Assert.Equal(
            Numero(cufe.ValorTotal),
            Numero(cufe.ValorBruto) + Numero(cufe.ValorIva)
                + Numero(cufe.ValorInc) + Numero(cufe.ValorIca));

        // ── 3. Y entre los dos: el impuesto que firma el CUFE es el del XML ──
        Assert.Equal(impuestoXml, Numero(cufe.ValorIva) + Numero(cufe.ValorInc));
    }

    [Fact]
    public void El_xml_declara_la_codificacion_utf8()
    {
        var resultado = Generador().Generar(Factura(), ClaveTecnica);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"", resultado.Xml);
    }

    /// <summary>
    /// Los acentos y las eñes sobreviven. Parece obvio y es el error clasico
    /// de codificacion: una razon social con ñ que llega como interrogantes.
    /// </summary>
    [Fact]
    public void Los_caracteres_del_espanol_se_conservan()
    {
        var datos = DatosTributarios.Crear(
            tipoIdentificacion: "13",
            identificacion: "1088123457",
            razonSocial: "Muñoz e Hijos Compañía Ltda",
            direccion: "Avenida 30 de Agosto # 12-34",
            municipioCodigo: "66001",
            regimen: "49");

        var factura = Documento.EmitirFactura(
            integradorId: Guid.CreateVersion7(),
            referenciaExterna: "VTA-002",
            prefijo: "SETP",
            consecutivo: 2,
            fechaEmision: DateTimeOffset.UtcNow,
            adquirenteId: Guid.CreateVersion7(),
            emisorSnapshot: DatosEmisor(),
            adquirenteSnapshot: datos,
            lineas: [CrearLinea()]);

        var resultado = Generador().Generar(factura, ClaveTecnica);

        Assert.Contains("Muñoz e Hijos Compañía Ltda", resultado.Xml);
        AssertValida(resultado.Xml, "UBL-Invoice-2.1.xsd");

        // Y el hash se calcula sobre bytes UTF-8, no sobre la cadena.
        Assert.Equal(96, resultado.CodigoUnico.Length);
    }
}
