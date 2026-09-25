// SPIKE: prueba de concepto de firma digital de XML.
//
// Responde una sola pregunta: se puede firmar y verificar XML en .NET
// sin dependencias de terceros?
//
// Esto NO es parte de la solucion. Se borra cuando responda la pregunta.
// Ver el issue "[H0] Spike: prueba de concepto de firma digital (R-05)".

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

Console.WriteLine("=== Spike de firma digital ===\n");

// ── 1. Crear un certificado autofirmado, solo en memoria ──
// En produccion el certificado lo emite una entidad autorizada.
// Para el spike basta con uno inventado: la mecanica de firma es la misma.

using var rsa = RSA.Create(2048);

var solicitud = new CertificateRequest(
    "CN=FEV-Core Spike",
    rsa,
    HashAlgorithmName.SHA256,
    RSASignaturePadding.Pkcs1);

using var certificado = solicitud.CreateSelfSigned(
    DateTimeOffset.UtcNow.AddDays(-1),
    DateTimeOffset.UtcNow.AddYears(1));

Console.WriteLine($"1. Certificado creado: {certificado.Subject}");
Console.WriteLine($"   Vigente hasta:      {certificado.NotAfter:yyyy-MM-dd}\n");

// ── 2. Un XML de juguete ──

var documento = new XmlDocument { PreserveWhitespace = true };
documento.LoadXml("""
    <Factura>
      <Numero>SETP990000123</Numero>
      <Total>357000.00</Total>
    </Factura>
    """);

Console.WriteLine("2. XML sin firmar:");
Console.WriteLine($"   Total declarado: {LeerTotal(documento)}\n");

// ── 3. Firmar ──

var firmador = new SignedXml(documento)
{
    SigningKey = rsa
};

// Referencia vacia significa "firma todo el documento".
var referencia = new Reference(uri: "");

// Sin esta transformacion, el calculo incluiria la firma misma,
// lo cual es imposible: la firma aun no existe cuando se calcula.
referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
firmador.AddReference(referencia);

// Incrusta el certificado para que quien reciba el documento
// pueda verificar sin tenerlo por otro lado.
var datosClave = new KeyInfo();
datosClave.AddClause(new KeyInfoX509Data(certificado));
firmador.KeyInfo = datosClave;

firmador.ComputeSignature();

documento.DocumentElement!.AppendChild(
    documento.ImportNode(firmador.GetXml(), deep: true));

Console.WriteLine("3. XML firmado. La firma quedo dentro del documento.\n");

// ── 4. Verificar ──

Console.WriteLine($"4. Firma valida: {Verificar(documento)}\n");

// ── 5. Alterar un valor y verificar de nuevo ──
// Esta es la prueba que de verdad importa: la firma debe romperse.

documento.GetElementsByTagName("Total")[0]!.InnerText = "1.00";

Console.WriteLine("5. Se altero el total a 1.00 despues de firmar.");
Console.WriteLine($"   Firma valida: {Verificar(documento)}\n");

Console.WriteLine("=== Fin del spike ===");
Console.WriteLine("Si el paso 4 dice True y el paso 5 dice False, la firma funciona.");

// ── Auxiliares ──

static string LeerTotal(XmlDocument doc) =>
    doc.GetElementsByTagName("Total")[0]!.InnerText;

static bool Verificar(XmlDocument doc)
{
    var verificador = new SignedXml(doc);

    var nodoFirma = doc.GetElementsByTagName(
        "Signature",
        SignedXml.XmlDsigNamespaceUrl)[0];

    verificador.LoadXml((XmlElement)nodoFirma!);

    // Sin argumentos, usa la clave publica del certificado incrustado.
    return verificador.CheckSignature();
}
