using System.Xml;
using System.Xml.Schema;

namespace FevCore.Integration.Tests;

/// <summary>
/// Valida un XML contra los esquemas oficiales de UBL 2.1 versionados en el
/// repositorio (CE-05).
///
/// Recordatorio: validar contra el esquema significa que la ESTRUCTURA es
/// correcta. No significa que la DIAN acepte el documento; el anexo tecnico
/// anade centenares de validaciones de negocio que ningun XSD expresa.
/// </summary>
internal static class ValidadorEsquemas
{
    public const string Factura = "UBL-Invoice-2.1.xsd";
    public const string NotaCredito = "UBL-CreditNote-2.1.xsd";
    public const string NotaDebito = "UBL-DebitNote-2.1.xsd";

    /// <summary>
    /// Sube por el arbol de directorios hasta encontrar la carpeta de
    /// esquemas. Las pruebas corren desde bin/Debug/net10.0, y calcular
    /// cuantos niveles hay que subir a mano se rompe en cuanto cambia el
    /// destino de compilacion.
    /// </summary>
    private static string CarpetaEsquemas()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            var candidata = Path.Combine(directorio.FullName, "schemas", "ubl-2.1");

            if (Directory.Exists(candidata))
            {
                return candidata;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException(
            "No se encontro schemas/ubl-2.1. Ejecute scripts/descargar-esquemas-ubl.ps1.");
    }

    /// <summary>
    /// Lee un esquema que contiene una declaracion DTD.
    ///
    /// El esquema de firma XML del W3C lleva un DOCTYPE con entidades dentro,
    /// y .NET prohibe procesar DTD por defecto. La prohibicion no es
    /// caprichosa: un DTD puede declarar entidades que leen archivos del
    /// disco o que se expanden hasta agotar la memoria, y ese es el vector de
    /// dos ataques clasicos contra procesadores de XML.
    ///
    /// Se habilita SOLO para leer este archivo, que viene versionado en el
    /// repositorio y es de confianza. El XML que se valida despues se lee con
    /// la prohibicion intacta.
    /// </summary>
    private static XmlSchema LeerEsquemaConDtd(string ruta)
    {
        var opciones = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new XmlUrlResolver()
        };

        using var lector = XmlReader.Create(ruta, opciones);

        return XmlSchema.Read(lector, null)
            ?? throw new InvalidOperationException($"No se pudo leer el esquema {ruta}.");
    }

    private static XmlSchemaSet Esquemas(string documentoPrincipal)
    {
        var carpeta = CarpetaEsquemas();
        var conjunto = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };

        // El esquema de firma hay que cargarlo a mano.
        //
        // UBL-SignatureAggregateComponents-2.1.xsd declara el prefijo ds y
        // escribe <xsd:element ref="ds:Signature"/>, pero NO importa el
        // esquema donde ese elemento esta definido: da por hecho que quien lo
        // use ya lo habra cargado. Y la cadena llega hasta la factura. Sin
        // esta linea, el conjunto ni siquiera compila.
        conjunto.Add(LeerEsquemaConDtd(
            Path.Combine(carpeta, "common", "UBL-xmldsig-core-schema-2.1.xsd")));

        conjunto.Add(null, Path.Combine(carpeta, "maindoc", documentoPrincipal));
        conjunto.Compile();

        return conjunto;
    }

    /// <summary>
    /// Valida y devuelve TODOS los problemas, no solo el primero.
    ///
    /// Es la diferencia entre corregir el XML en una vuelta o en quince: el
    /// validador se detendria en el primer elemento fuera de sitio, y cada
    /// ejecucion revelaria un solo problema.
    /// </summary>
    public static IReadOnlyList<string> Problemas(string xml, string documentoPrincipal)
    {
        var problemas = new List<string>();

        var opciones = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = Esquemas(documentoPrincipal)
        };

        opciones.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        opciones.ValidationEventHandler += (_, argumentos) =>
            problemas.Add($"[{argumentos.Severity}] linea {argumentos.Exception?.LineNumber}: {argumentos.Message}");

        using var lector = XmlReader.Create(new StringReader(xml), opciones);

        try
        {
            while (lector.Read()) { }
        }
        catch (XmlException error)
        {
            problemas.Add($"[XML mal formado] {error.Message}");
        }

        return problemas;
    }

    public static void AssertValida(string xml, string documentoPrincipal)
    {
        var problemas = Problemas(xml, documentoPrincipal);

        Assert.True(
            problemas.Count == 0,
            $"El XML no valida contra {documentoPrincipal}:{Environment.NewLine}" +
            string.Join(Environment.NewLine, problemas.Select(p => "  - " + p)));
    }
}
