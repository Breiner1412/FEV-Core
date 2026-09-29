using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Infrastructure.Xml;
using Microsoft.Extensions.Configuration;

namespace FevCore.Integration.Tests;

/// <summary>
/// RF-17: la firma es verificable con la clave publica del certificado, y
/// cualquier alteracion posterior del XML la invalida.
///
/// Esta clase hace ademas el papel de la prueba de concepto que el plan
/// situaba en H0 para retirar el riesgo R-05. Aquella era desechable; esta
/// se queda, que es mejor: el riesgo no se retira una vez, se mantiene
/// retirado.
/// </summary>
public sealed class FirmaXadesTests
{
    // ── Certificados de prueba ──

    /// <summary>
    /// Genera un certificado autofirmado en memoria.
    ///
    /// No se guarda ninguno en el repositorio: un certificado con clave
    /// privada es un secreto, y RNF-01 dice que no puede haber secretos en
    /// el codigo fuente. Aunque este sea de juguete, aceptar la costumbre de
    /// versionarlos es como se filtran los de verdad.
    /// </summary>
    private static X509Certificate2 CertificadoDePrueba(
        DateTimeOffset desde,
        DateTimeOffset hasta)
    {
        using var rsa = RSA.Create(2048);

        var solicitud = new CertificateRequest(
            "CN=Comercializadora del Eje SAS, O=FEV-Core, C=CO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var generado = solicitud.CreateSelfSigned(desde, hasta);

        // Se exporta y se vuelve a cargar para que la clave privada quede
        // asociada de una forma que SignedXml pueda usar.
        return X509CertificateLoader.LoadPkcs12(
            generado.Export(X509ContentType.Pfx, "prueba"),
            "prueba",
            X509KeyStorageFlags.Exportable);
    }

    private sealed class ProveedorFijo(X509Certificate2 certificado) : IProveedorCertificado
    {
        public X509Certificate2 Obtener(DateTimeOffset momento) => certificado;
    }

    private static string XmlDeEjemplo()
    {
        var factura = Documento.EmitirFactura(
            integradorId: Guid.CreateVersion7(),
            referenciaExterna: "VTA-FIRMA",
            prefijo: "SETP",
            consecutivo: 1,
            fechaEmision: new DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.Zero),
            adquirenteId: Guid.CreateVersion7(),
            emisorSnapshot: DatosTributarios.Crear(
                tipoIdentificacion: DatosTributarios.TipoNit,
                identificacion: "800197268",
                razonSocial: "Comercializadora del Eje SAS",
                direccion: "Calle 20 # 8-45",
                municipioCodigo: "66001",
                regimen: "48",
                digitoVerificacion: "4",
                responsabilidades: ["O-13"]),
            adquirenteSnapshot: DatosTributarios.Crear(
                tipoIdentificacion: "13",
                identificacion: "1088123456",
                razonSocial: "Juan Perez",
                direccion: "Carrera 10 # 5-20",
                municipioCodigo: "66001",
                regimen: "49"),
            lineas:
            [
                Linea.Crear(
                    numero: 1,
                    codigo: "PROD-001",
                    descripcion: "Teclado mecanico",
                    unidadMedida: "94",
                    cantidad: 2m,
                    precioUnitario: Dinero.Desde(150_000m),
                    impuestos: [new EspecificacionImpuesto(TipoImpuesto.Iva, 19m)])
            ]);

        return new GeneradorXmlUbl(AmbienteDian.Pruebas)
            .Generar(factura, "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c")
            .Xml;
    }

    // ── Verificacion ──

    private static XmlElement NodoFirma(string xmlFirmado)
    {
        var documento = new XmlDocument { PreserveWhitespace = true };
        documento.LoadXml(xmlFirmado);

        return (XmlElement)documento.SelectSingleNode(
            "//*[local-name()='Signature' and namespace-uri()='http://www.w3.org/2000/09/xmldsig#']")!;
    }

    private static bool FirmaValida(string xmlFirmado, X509Certificate2 certificado)
    {
        var documento = new XmlDocument { PreserveWhitespace = true };
        documento.LoadXml(xmlFirmado);

        var nodo = (XmlElement)documento.SelectSingleNode(
            "//*[local-name()='Signature' and namespace-uri()='http://www.w3.org/2000/09/xmldsig#']")!;

        var verificador = new SignedXml(documento);
        verificador.LoadXml(nodo);

        return verificador.CheckSignature(certificado.GetRSAPublicKey()!);
    }

    private static string Firmar(X509Certificate2 certificado) =>
        new FirmadorXadesEpes(new ProveedorFijo(certificado))
            .Firmar(XmlDeEjemplo(), DateTimeOffset.UtcNow);

    // ── Las pruebas ──

    [Fact]
    public void La_firma_se_verifica_con_la_clave_publica_del_certificado()
    {
        using var certificado = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        Assert.True(FirmaValida(Firmar(certificado), certificado));
    }

    /// <summary>
    /// La prueba que da sentido a todo lo demas: si alterar el documento no
    /// invalidara la firma, la firma no estaria protegiendo nada.
    /// </summary>
    [Fact]
    public void Alterar_un_solo_valor_del_documento_invalida_la_firma()
    {
        using var certificado = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var firmado = Firmar(certificado);

        Assert.True(FirmaValida(firmado, certificado));

        // Un solo digito del total a pagar.
        var alterado = firmado.Replace("357000.00", "357000.01");

        Assert.NotEqual(firmado, alterado);
        Assert.False(FirmaValida(alterado, certificado));
    }

    [Fact]
    public void La_firma_de_otro_certificado_no_verifica()
    {
        using var propio = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        using var ajeno = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        Assert.False(FirmaValida(Firmar(propio), ajeno));
    }

    // ── Estructura XAdES-EPES ──

    [Fact]
    public void La_firma_queda_dentro_del_bloque_de_extension_de_UBL()
    {
        using var certificado = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var padre = NodoFirma(Firmar(certificado)).ParentNode!;

        Assert.Equal("ExtensionContent", padre.LocalName);
        Assert.Equal("UBLExtension", padre.ParentNode!.LocalName);
    }

    [Fact]
    public void Declara_la_politica_de_firma_que_exige_la_DIAN()
    {
        using var certificado = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var firmado = Firmar(certificado);

        Assert.Contains(
            "https://facturaelectronica.dian.gov.co/politicadefirma/v2/politicadefirmav2.pdf",
            firmado);

        // La huella del documento de politica. Es un valor fijo de la norma:
        // si cambiara, todas las firmas emitidas dejarian de ser conformes.
        Assert.Contains("dMoMvtcG5aIzgYo0tIsSQeVJBDnUnfSOfBpxXrmor0Y=", firmado);
    }

    [Fact]
    public void Declara_el_rol_del_firmante_y_las_propiedades_XAdES()
    {
        using var certificado = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var firmado = Firmar(certificado);

        Assert.Contains("SignerRole", firmado);
        Assert.Contains("supplier", firmado);
        Assert.Contains("SigningTime", firmado);
        Assert.Contains("SigningCertificate", firmado);
        Assert.Contains("SignedProperties", firmado);
    }

    /// <summary>
    /// Las tres referencias: el documento, el bloque de clave publica y las
    /// propiedades XAdES. Sin la tercera esto seria XML-DSig corriente, no
    /// XAdES, y la DIAN lo rechazaria.
    /// </summary>
    [Fact]
    public void La_firma_lleva_las_tres_referencias()
    {
        using var certificado = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var firma = NodoFirma(Firmar(certificado));

        var referencias = firma.SelectNodes(
            ".//*[local-name()='Reference' and namespace-uri()='http://www.w3.org/2000/09/xmldsig#']")!;

        Assert.Equal(3, referencias.Count);

        Assert.Contains(
            referencias.Cast<XmlElement>(),
            r => r.GetAttribute("Type") == "http://uri.etsi.org/01903#SignedProperties");
    }

    [Fact]
    public void El_documento_firmado_sigue_validando_contra_el_esquema()
    {
        using var certificado = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        ValidadorEsquemas.AssertValida(Firmar(certificado), ValidadorEsquemas.Factura);
    }

    // ── Vigencia del certificado (INV-CER-01) ──

    [Fact]
    public void Un_certificado_vencido_no_firma()
    {
        using var vencido = CertificadoDePrueba(
            DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddDays(-1));

        var proveedor = new ProveedorCertificadoConfiguracion(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Firma:CertificadoBase64"] =
                        Convert.ToBase64String(vencido.Export(X509ContentType.Pfx, "x")),
                    ["Firma:Clave"] = "x"
                })
                .Build());

        var error = Assert.Throws<ExcepcionDominio>(
            () => proveedor.Obtener(DateTimeOffset.UtcNow));

        Assert.Equal("CERTIFICADO_VENCIDO", error.Codigo);
    }

    [Fact]
    public void Sin_certificado_configurado_no_se_firma()
    {
        var proveedor = new ProveedorCertificadoConfiguracion(
            new ConfigurationBuilder().Build());

        var error = Assert.Throws<ExcepcionDominio>(
            () => proveedor.Obtener(DateTimeOffset.UtcNow));

        Assert.Equal("CERTIFICADO_NO_CONFIGURADO", error.Codigo);
    }
}
