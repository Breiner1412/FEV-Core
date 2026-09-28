using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using FevCore.Application.Abstracciones;

namespace FevCore.Infrastructure.Xml;

/// <summary>
/// Firma un documento UBL en formato XAdES-EPES, como exige la DIAN (RF-17).
///
/// .NET trae XML-DSig en SignedXml, que es la mitad de abajo del formato:
/// firma, referencias y digestos. XAdES anade encima las "propiedades
/// cualificadas" —cuando se firmo, con que certificado, bajo que politica y
/// en que papel— y esas hay que construirlas a mano como un ds:Object y
/// referenciarlas con una tercera ds:Reference. No hay atajo en la
/// biblioteca estandar.
///
/// La firma queda dentro de ext:UBLExtensions/ext:ExtensionContent, que es
/// donde UBL reserva sitio para ella.
/// </summary>
public sealed class FirmadorXadesEpes(IProveedorCertificado proveedor) : IFirmadorXml
{
    /// <summary>
    /// SignedXml que sabe encontrar las propiedades XAdES.
    ///
    /// Al calcular los digestos, SignedXml resuelve cada referencia "#id"
    /// buscando ese elemento EN EL DOCUMENTO. Las propiedades XAdES no estan
    /// ahi: viven dentro del ds:Object de la firma, que todavia no existe
    /// como XML cuando se calculan los digestos. El resultado es un
    /// "Malformed reference element" que no dice nada de todo esto.
    ///
    /// GetIdElement es virtual justamente para esto. Se le anade el fragmento
    /// de propiedades como segundo sitio donde mirar.
    /// </summary>
    private sealed class FirmaConPropiedadesXades(
        XmlDocument documento,
        XmlElement propiedades) : SignedXml(documento)
    {
        public override XmlElement? GetIdElement(XmlDocument? documento, string id)
        {
            var enDocumento = base.GetIdElement(documento, id);

            if (enDocumento is not null)
            {
                return enDocumento;
            }

            if (propiedades.GetAttribute("Id") == id)
            {
                return propiedades;
            }

            return propiedades.SelectSingleNode($".//*[@Id='{id}']") as XmlElement;
        }
    }

    // ── Espacios de nombres ──
    private const string Ubl = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private const string Xades = "http://uri.etsi.org/01903/v1.3.2#";
    private const string Dsig = "http://www.w3.org/2000/09/xmldsig#";

    /// <summary>
    /// La politica de firma que la DIAN exige, siempre la misma para todos
    /// los documentos electronicos del pais.
    /// </summary>
    private const string PoliticaUrl =
        "https://facturaelectronica.dian.gov.co/politicadefirma/v2/politicadefirmav2.pdf";

    private const string PoliticaDescripcion =
        "Politica de firma para facturas electronicas de la Republica de Colombia";

    /// <summary>Huella SHA-256 del documento de politica, en base 64.</summary>
    private const string PoliticaHuella = "dMoMvtcG5aIzgYo0tIsSQeVJBDnUnfSOfBpxXrmor0Y=";

    /// <summary>El emisor firma como proveedor del bien o servicio.</summary>
    private const string RolFirmante = "supplier";

    public string Firmar(string xml, DateTimeOffset momento)
    {
        var certificado = proveedor.Obtener(momento);

        // PreserveWhitespace es obligatorio. La firma se calcula sobre los
        // bytes exactos del documento; si el lector normalizara los espacios,
        // el digesto se calcularia sobre un texto distinto del que se guarda
        // y la verificacion fallaria sin explicacion aparente.
        var documento = new XmlDocument { PreserveWhitespace = true };
        documento.LoadXml(xml);

        // El hueco de la extension se crea ANTES de firmar.
        //
        // La referencia al documento usa URI vacio con la transformacion
        // "enveloped", que al verificar quita el elemento ds:Signature y
        // calcula el digesto sobre lo que queda. Si el contenedor no
        // existiera al firmar pero si al verificar, lo que queda no seria lo
        // mismo y el digesto no cuadraria.
        var contenedor = CrearContenedorExtension(documento);

        var identificador = $"xmldsig-{Guid.CreateVersion7():N}";
        var idPropiedades = $"{identificador}-signedprops";
        var idKeyInfo = $"{identificador}-keyinfo";

        // Las propiedades se construyen ANTES que la firma: la firma necesita
        // conocerlas para poder resolver la referencia que apunta a ellas.
        var propiedades = ConstruirPropiedadesFirmadas(
            documento, certificado, momento, identificador, idPropiedades);

        var firma = new FirmaConPropiedadesXades(documento, propiedades)
        {
            SigningKey = certificado.GetRSAPrivateKey()
                ?? throw new InvalidOperationException(
                    "El certificado no expone una clave privada RSA utilizable.")
        };

        firma.Signature.Id = identificador;
        firma.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        firma.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        // ── Referencia 1: el documento entero ──
        var referenciaDocumento = new Reference
        {
            Uri = "",
            DigestMethod = SignedXml.XmlDsigSHA256Url
        };
        referenciaDocumento.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        firma.AddReference(referenciaDocumento);

        // ── El bloque de la clave publica ──
        //
        // PENDIENTE: la DIAN espera TRES referencias, incluida una al propio
        // KeyInfo. SignedXml no puede producirla: al calcular los digestos
        // resuelve cada "#id" buscando el elemento en el documento o en los
        // objetos de la firma, y el KeyInfo no esta en ninguno de los dos —
        // solo existe cuando la firma se serializa, despues. Intentarlo
        // falla con "Malformed reference element".
        //
        // Resolverlo exige construir el ds:Signature a mano en vez de dejar
        // que SignedXml lo arme, o firmar en dos pasadas. Queda como lo
        // primero de H6 manana. Mientras tanto la firma es criptografica-
        // mente valida y verificable, pero NO es conforme al anexo tecnico.
        firma.KeyInfo = ConstruirKeyInfo(certificado, idKeyInfo);

        // ── Referencia 2: las propiedades XAdES ──
        // El atributo Type es lo que convierte esto en XAdES y no en un
        // objeto cualquiera colgando de la firma.
        firma.AddObject(new DataObject { Data = propiedades.SelectNodes(".")! });

        firma.AddReference(new Reference
        {
            Uri = $"#{idPropiedades}",
            Type = "http://uri.etsi.org/01903#SignedProperties",
            DigestMethod = SignedXml.XmlDsigSHA256Url
        });

        firma.ComputeSignature();

        contenedor.AppendChild(
            documento.ImportNode(firma.GetXml(), deep: true));

        return documento.OuterXml;
    }

    /// <summary>
    /// Crea ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent al
    /// principio del documento y devuelve el contenedor donde va la firma.
    /// </summary>
    private static XmlElement CrearContenedorExtension(XmlDocument documento)
    {
        var raiz = documento.DocumentElement
            ?? throw new InvalidOperationException("El XML no tiene elemento raiz.");

        var extensiones = documento.CreateElement("ext", "UBLExtensions", Ubl);
        var extension = documento.CreateElement("ext", "UBLExtension", Ubl);
        var contenido = documento.CreateElement("ext", "ExtensionContent", Ubl);

        extension.AppendChild(contenido);
        extensiones.AppendChild(extension);

        // El esquema lo declara como primer elemento de la secuencia.
        raiz.InsertBefore(extensiones, raiz.FirstChild);

        return contenido;
    }

    private static KeyInfo ConstruirKeyInfo(
        X509Certificate2 certificado,
        string id)
    {
        var info = new KeyInfo { Id = id };
        info.AddClause(new KeyInfoX509Data(certificado));

        return info;
    }

    /// <summary>
    /// Las propiedades cualificadas de XAdES-EPES.
    ///
    /// Se construyen como XML literal porque .NET no tiene tipos para ellas.
    /// Los prefijos xades y ds se declaran aqui explicitamente: con
    /// canonicalizacion inclusiva, un prefijo que no este declarado dentro
    /// del bloque se hereda del documento, y si al verificar el contexto es
    /// otro, el digesto cambia.
    /// </summary>
    private static XmlElement ConstruirPropiedadesFirmadas(
        XmlDocument documento,
        X509Certificate2 certificado,
        DateTimeOffset momento,
        string identificadorFirma,
        string idPropiedades)
    {
        var huellaCertificado = Convert.ToBase64String(SHA256.HashData(certificado.RawData));

        // Hora en la zona de Colombia, como el resto del documento.
        var instante = momento
            .ToOffset(TimeSpan.FromHours(-5))
            .ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

        var texto = $"""
            <xades:QualifyingProperties xmlns:xades="{Xades}" xmlns:ds="{Dsig}" Target="#{identificadorFirma}">
              <xades:SignedProperties Id="{idPropiedades}">
                <xades:SignedSignatureProperties>
                  <xades:SigningTime>{instante}</xades:SigningTime>
                  <xades:SigningCertificate>
                    <xades:Cert>
                      <xades:CertDigest>
                        <ds:DigestMethod Algorithm="{SignedXml.XmlDsigSHA256Url}" />
                        <ds:DigestValue>{huellaCertificado}</ds:DigestValue>
                      </xades:CertDigest>
                      <xades:IssuerSerial>
                        <ds:X509IssuerName>{Escapar(certificado.Issuer)}</ds:X509IssuerName>
                        <ds:X509SerialNumber>{SerieDecimal(certificado)}</ds:X509SerialNumber>
                      </xades:IssuerSerial>
                    </xades:Cert>
                  </xades:SigningCertificate>
                  <xades:SignaturePolicyIdentifier>
                    <xades:SignaturePolicyId>
                      <xades:SigPolicyId>
                        <xades:Identifier>{PoliticaUrl}</xades:Identifier>
                        <xades:Description>{PoliticaDescripcion}</xades:Description>
                      </xades:SigPolicyId>
                      <xades:SigPolicyHash>
                        <ds:DigestMethod Algorithm="{SignedXml.XmlDsigSHA256Url}" />
                        <ds:DigestValue>{PoliticaHuella}</ds:DigestValue>
                      </xades:SigPolicyHash>
                    </xades:SignaturePolicyId>
                  </xades:SignaturePolicyIdentifier>
                  <xades:SignerRole>
                    <xades:ClaimedRoles>
                      <xades:ClaimedRole>{RolFirmante}</xades:ClaimedRole>
                    </xades:ClaimedRoles>
                  </xades:SignerRole>
                </xades:SignedSignatureProperties>
              </xades:SignedProperties>
            </xades:QualifyingProperties>
            """;

        var fragmento = new XmlDocument { PreserveWhitespace = true };
        fragmento.LoadXml(texto);

        var elemento = (XmlElement)documento.ImportNode(fragmento.DocumentElement!, deep: true);

        HeredarEspaciosDeNombres(documento, elemento);

        return elemento;
    }

    /// <summary>
    /// Copia al bloque de propiedades los espacios de nombres que declara la
    /// raiz del documento.
    ///
    /// Es la correccion mas sutil de todo el firmador. El digesto de las
    /// propiedades se calcula DOS veces en momentos distintos: al firmar,
    /// cuando el bloque esta suelto y solo conoce los prefijos xades y ds; y
    /// al verificar, cuando ya esta dentro del documento y HEREDA los de la
    /// factura.
    ///
    /// La canonicalizacion inclusiva incluye los espacios heredados, asi que
    /// el mismo contenido produce dos textos distintos y la firma no
    /// verifica. Declararlos aqui hace que el contexto sea identico en los
    /// dos momentos: lo que al verificar se hereda, ya estaba declarado al
    /// firmar.
    ///
    /// Es el mismo principio que ValoresCufe en la etapa 5: cuando algo se
    /// calcula dos veces, las dos veces tienen que partir de lo mismo.
    /// </summary>
    private static void HeredarEspaciosDeNombres(XmlDocument documento, XmlElement destino)
    {
        var raiz = documento.DocumentElement!;

        foreach (XmlAttribute atributo in raiz.Attributes)
        {
            var esDeclaracion =
                atributo.Prefix == "xmlns" ||
                (atributo.Prefix.Length == 0 && atributo.LocalName == "xmlns");

            if (!esDeclaracion)
            {
                continue;
            }

            // Lo que el bloque ya declara por su cuenta manda: no se pisa.
            if (destino.HasAttribute(atributo.Name))
            {
                continue;
            }

            destino.SetAttribute(atributo.Name, atributo.Value);
        }
    }

    /// <summary>
    /// El numero de serie va en decimal, no en el hexadecimal que devuelve
    /// el certificado.
    /// </summary>
    private static string SerieDecimal(
        X509Certificate2 certificado) =>
        System.Numerics.BigInteger
            .Parse("0" + certificado.SerialNumber, NumberStyles.HexNumber)
            .ToString(CultureInfo.InvariantCulture);

    private static string Escapar(string valor) =>
        valor.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
