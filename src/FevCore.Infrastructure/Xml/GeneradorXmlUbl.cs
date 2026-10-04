using System.Globalization;
using System.Xml.Linq;
using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Infrastructure.Xml;

/// <summary>
/// Genera el XML en formato UBL 2.1 (RF-16).
///
/// Se construye a mano con XDocument y no con clases generadas desde el
/// esquema. UBL 2.1 define mas de sesenta documentos y miles de elementos;
/// generar clases para usar el uno por ciento habria metido decenas de miles
/// de lineas al repositorio para ocultar justo lo que hay que entender.
///
/// El orden de los elementos NO es libre. El esquema los declara como
/// secuencia, asi que colocarlos en otro orden produce un XML que valida mal
/// aunque tenga todos los datos. Por eso cada bloque de este archivo sigue
/// el orden del XSD, y por eso existe la prueba que valida contra el esquema
/// oficial: es lo unico que garantiza que no se coló ninguno fuera de sitio.
/// </summary>
public sealed class GeneradorXmlUbl(AmbienteDian ambiente) : IGeneradorXml
{
    // ── Espacios de nombres ──
    // Cada prefijo apunta a un espacio distinto. Equivocarse aqui produce un
    // documento que parece correcto y que ningun validador acepta.
    private static readonly XNamespace Fac =
        "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace NotaCredito =
        "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    private static readonly XNamespace NotaDebito =
        "urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2";
    private static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Ext =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    public ResultadoXml Generar(Documento documento, string claveTecnica)
    {
        ArgumentNullException.ThrowIfNull(documento);

        // Los valores se formatean UNA vez y alimentan tanto el codigo unico
        // como el XML. Ver ValoresCufe para por que eso importa tanto.
        var valores = ValoresCufe.Para(documento, claveTecnica, ambiente);
        var codigo = CodigoUnico.CalcularCufe(valores);

        var (raiz, tipoElemento) = documento.Tipo switch
        {
            TipoDocumento.Factura => (Fac, "Invoice"),
            TipoDocumento.NotaCredito => (NotaCredito, "CreditNote"),
            TipoDocumento.NotaDebito => (NotaDebito, "DebitNote"),
            _ => throw new ExcepcionDominio(
                "TIPO_DOCUMENTO_NO_SOPORTADO",
                $"No hay plantilla XML para el tipo {documento.Tipo}.")
        };

        var raizElemento = new XElement(raiz + tipoElemento,
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
            new XAttribute(XNamespace.Xmlns + "ext", Ext),

            // NO se emite ext:UBLExtensions.
            //
            // Ahi es donde en H6 entrara la firma digital, y la primera idea
            // fue dejarlo creado y vacio para no mover la estructura despues.
            // El esquema no lo permite: ExtensionContent esta declarado como
            // "cualquier elemento de otro espacio de nombres" con contenido
            // obligatorio, asi que un contenedor vacio es invalido. Todo el
            // bloque es opcional, de modo que hasta que haya una firma que
            // meter, no se escribe.
            //
            // El prefijo ext si queda declarado arriba: H6 lo necesita y
            // declararlo no cambia la validacion.

            new XElement(Cbc + "UBLVersionID", "UBL 2.1"),
            new XElement(Cbc + "CustomizationID", CodigoOperacion(documento)),
            new XElement(Cbc + "ProfileID", "DIAN 2.1"),
            new XElement(Cbc + "ProfileExecutionID", ((int)ambiente).ToString(CultureInfo.InvariantCulture)),
            new XElement(Cbc + "ID", valores.NumeroFactura),

            new XElement(Cbc + "UUID", codigo,
                new XAttribute("schemeID", ((int)ambiente).ToString(CultureInfo.InvariantCulture)),
                new XAttribute("schemeName", CodigoUnico.EsquemaPara(documento.Tipo))),

            new XElement(Cbc + "IssueDate", valores.Fecha),
            new XElement(Cbc + "IssueTime", valores.Hora));

        // El codigo de tipo cambia de nombre segun el documento, y en las
        // notas no existe: el propio elemento raiz ya dice cual es.
        if (documento.Tipo == TipoDocumento.Factura)
        {
            raizElemento.Add(new XElement(Cbc + "InvoiceTypeCode", "01"));
        }

        raizElemento.Add(
            new XElement(Cbc + "DocumentCurrencyCode", documento.Moneda),
            new XElement(Cbc + "LineCountNumeric",
                documento.Lineas.Count.ToString(CultureInfo.InvariantCulture)));

        // Las notas declaran a que factura corrigen.
        if (documento.EsNota && documento.DocumentoReferenciadoId is not null)
        {
            raizElemento.Add(new XElement(Cac + "BillingReference",
                new XElement(Cac + "InvoiceDocumentReference",
                    new XElement(Cbc + "ID", documento.DocumentoReferenciadoId.Value))));
        }

        raizElemento.Add(
            Emisor(documento),
            Adquirente(documento),
            ImpuestosTotales(documento),
            TotalesMonetarios(documento, tipoElemento));

        foreach (var linea in documento.Lineas)
        {
            raizElemento.Add(LineaDetalle(linea, documento.Moneda, tipoElemento));
        }

        var xml = new XDeclaration("1.0", "UTF-8", null) + Environment.NewLine
            + raizElemento.ToString(SaveOptions.None);

        return new ResultadoXml(xml, codigo);
    }

    /// <summary>
    /// Codigo de operacion. Para factura de venta nacional con validacion
    /// previa es "10"; las notas usan "20" y "30".
    /// </summary>
    private static string CodigoOperacion(Documento documento) => documento.Tipo switch
    {
        TipoDocumento.Factura => "10",
        TipoDocumento.NotaCredito => "20",
        TipoDocumento.NotaDebito => "30",
        _ => "10"
    };

    // ── Partes ──

    private static XElement Emisor(Documento documento) =>
        new(Cac + "AccountingSupplierParty",
            new XElement(Cbc + "AdditionalAccountID", TipoPersona(documento.EmisorSnapshot)),
            Parte(documento.EmisorSnapshot));

    private static XElement Adquirente(Documento documento) =>
        new(Cac + "AccountingCustomerParty",
            new XElement(Cbc + "AdditionalAccountID", TipoPersona(documento.AdquirenteSnapshot)),
            Parte(documento.AdquirenteSnapshot));

    /// <summary>1 para persona juridica, 2 para natural. El NIT es 31.</summary>
    private static string TipoPersona(DatosTributarios datos) =>
        datos.TipoIdentificacion == DatosTributarios.TipoNit ? "1" : "2";

    /// <summary>
    /// El orden de cac:Party lo fija el esquema: nombre, ubicacion, datos
    /// tributarios y por ultimo entidad legal.
    /// </summary>
    private static XElement Parte(DatosTributarios datos) =>
        new(Cac + "Party",
            new XElement(Cac + "PartyName",
                new XElement(Cbc + "Name", datos.RazonSocial)),

            new XElement(Cac + "PhysicalLocation",
                new XElement(Cac + "Address", ContenidoDireccion(datos))),

            new XElement(Cac + "PartyTaxScheme",
                new XElement(Cbc + "RegistrationName", datos.RazonSocial),
                Identificacion(datos),
                new XElement(Cbc + "TaxLevelCode",
                    new XAttribute("listName", datos.Regimen),
                    Responsabilidades(datos)),
                new XElement(Cac + "RegistrationAddress", ContenidoDireccion(datos)),
                new XElement(Cac + "TaxScheme",
                    new XElement(Cbc + "ID", "01"),
                    new XElement(Cbc + "Name", "IVA"))),

            new XElement(Cac + "PartyLegalEntity",
                new XElement(Cbc + "RegistrationName", datos.RazonSocial),
                Identificacion(datos)));

    private static XElement Identificacion(DatosTributarios datos)
    {
        var elemento = new XElement(Cbc + "CompanyID", datos.Identificacion,
            new XAttribute("schemeName", datos.TipoIdentificacion),
            new XAttribute("schemeAgencyID", "195"),
            new XAttribute("schemeAgencyName", "CO, DIAN (Direccion de Impuestos y Aduanas Nacionales)"));

        // El digito de verificacion solo existe para el NIT.
        if (!string.IsNullOrWhiteSpace(datos.DigitoVerificacion))
        {
            elemento.Add(new XAttribute("schemeID", datos.DigitoVerificacion));
        }

        return elemento;
    }

    private static string Responsabilidades(DatosTributarios datos) =>
        datos.Responsabilidades.Count == 0
            ? "R-99-PN"
            : string.Join(";", datos.Responsabilidades);

    /// <summary>
    /// El CONTENIDO de una direccion, sin el elemento que lo envuelve.
    ///
    /// Va asi porque UBL envuelve la direccion de dos maneras distintas y no
    /// intercambiables. cac:PhysicalLocation es una ubicacion y lleva dentro
    /// un cac:Address; cac:RegistrationAddress ES una direccion, y sus hijos
    /// son los campos directamente. Meter un cac:Address dentro de
    /// RegistrationAddress produce un documento invalido.
    ///
    /// El proyecto guarda el codigo de municipio pero no su nombre ni el del
    /// departamento, asi que CityName y CountrySubentity se omiten. Queda
    /// documentado en docs/07-cobertura-ubl.md.
    /// </summary>
    private static object[] ContenidoDireccion(DatosTributarios datos) =>
        [
            new XElement(Cbc + "ID", datos.MunicipioCodigo),
            new XElement(Cac + "AddressLine",
                new XElement(Cbc + "Line", datos.Direccion)),
            new XElement(Cac + "Country",
                new XElement(Cbc + "IdentificationCode", "CO"),
                new XElement(Cbc + "Name", "Colombia"))
        ];

    // ── Impuestos y totales ──

    /// <summary>
    /// En UBL el TaxAmount de un TaxTotal ES la suma de sus TaxSubtotal: no
    /// es una politica de redondeo, es lo que significa el elemento. Los
    /// grupos salen del dominio ya redondeados, y el total de Totales es su
    /// suma (ADR-0016).
    /// </summary>
    private static XElement ImpuestosTotales(Documento documento)
    {
        var moneda = documento.Moneda;

        var total = new XElement(Cac + "TaxTotal",
            Importe(Cbc + "TaxAmount", documento.Totales.TotalImpuestos, moneda));

        foreach (var grupo in documento.ImpuestosPorGrupo())
        {
            total.Add(Subtotal(
                grupo.Tipo, grupo.Tarifa, grupo.BaseGravable, grupo.Valor, moneda));
        }

        return total;
    }

    private static XElement Subtotal(
        TipoImpuesto tipo,
        decimal tarifa,
        Dinero baseGravable,
        Dinero valor,
        string moneda) =>
        new(Cac + "TaxSubtotal",
            Importe(Cbc + "TaxableAmount", baseGravable, moneda),
            Importe(Cbc + "TaxAmount", valor, moneda),
            new XElement(Cbc + "Percent", Decimal(tarifa)),
            new XElement(Cac + "TaxCategory",
                new XElement(Cbc + "Percent", Decimal(tarifa)),
                new XElement(Cac + "TaxScheme",
                    new XElement(Cbc + "ID", tipo.CodigoDian()),
                    new XElement(Cbc + "Name", tipo.NombreDian()))));

    /// <summary>
    /// El nombre del elemento de totales cambia con el documento: en una
    /// factura es LegalMonetaryTotal, en una nota es RequestedMonetaryTotal.
    /// </summary>
    private static XElement TotalesMonetarios(Documento documento, string tipoElemento)
    {
        var nombre = tipoElemento == "Invoice"
            ? "LegalMonetaryTotal"
            : "RequestedMonetaryTotal";

        var moneda = documento.Moneda;
        var totales = documento.Totales;

        return new XElement(Cac + nombre,
            Importe(Cbc + "LineExtensionAmount", totales.TotalBaseImponible, moneda),
            Importe(Cbc + "TaxExclusiveAmount", totales.TotalBaseImponible, moneda),
            Importe(Cbc + "TaxInclusiveAmount", totales.TotalAPagar, moneda),
            Importe(Cbc + "AllowanceTotalAmount", totales.TotalDescuentos, moneda),
            Importe(Cbc + "PayableAmount", totales.TotalAPagar, moneda));
    }

    // ── Lineas ──

    private static XElement LineaDetalle(Linea linea, string moneda, string tipoElemento)
    {
        var (nombre, cantidad) = tipoElemento switch
        {
            "Invoice" => ("InvoiceLine", "InvoicedQuantity"),
            "CreditNote" => ("CreditNoteLine", "CreditedQuantity"),
            _ => ("DebitNoteLine", "DebitedQuantity")
        };

        var elemento = new XElement(Cac + nombre,
            new XElement(Cbc + "ID", linea.Numero.ToString(CultureInfo.InvariantCulture)),
            new XElement(Cbc + cantidad, Decimal(linea.Cantidad),
                new XAttribute("unitCode", linea.UnidadMedida)),
            Importe(Cbc + "LineExtensionAmount", linea.BaseGravable, moneda));

        if (linea.Impuestos.Count > 0)
        {
            // La misma regla que en el documento: el TaxAmount de la linea es
            // la suma de sus subtotales tal como se escriben, no el redondeo
            // de su suma exacta, que podria diferir en un centavo.
            var subtotales = linea.Impuestos
                .Select(i => (Impuesto: i, Valor: i.Valor.Redondear()))
                .ToList();

            var impuestos = new XElement(Cac + "TaxTotal",
                Importe(
                    Cbc + "TaxAmount",
                    subtotales.Aggregate(Dinero.Cero, (suma, s) => suma + s.Valor),
                    moneda));

            foreach (var (impuesto, valor) in subtotales)
            {
                impuestos.Add(Subtotal(
                    impuesto.Tipo,
                    impuesto.Tarifa,
                    impuesto.BaseGravable.Redondear(),
                    valor,
                    moneda));
            }

            elemento.Add(impuestos);
        }

        elemento.Add(
            new XElement(Cac + "Item",
                new XElement(Cbc + "Description", linea.Descripcion),
                new XElement(Cac + "SellersItemIdentification",
                    new XElement(Cbc + "ID", linea.Codigo))),

            new XElement(Cac + "Price",
                Importe(Cbc + "PriceAmount", linea.PrecioUnitario.Redondear(), moneda),
                new XElement(Cbc + "BaseQuantity", Decimal(linea.Cantidad),
                    new XAttribute("unitCode", linea.UnidadMedida))));

        return elemento;
    }

    // ── Formato ──

    private static XElement Importe(XName nombre, Dinero valor, string moneda) =>
        new(nombre,
            new XAttribute("currencyID", moneda),
            valor.ParaDocumento());

    /// <summary>
    /// Cultura invariante siempre. Con configuracion colombiana, 19 saldria
    /// como "19,00" y ningun validador lo aceptaria como numero.
    /// </summary>
    private static string Decimal(decimal valor) =>
        valor.ToString("0.00", CultureInfo.InvariantCulture);
}
