using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// El camino de H6 por HTTP: emitir, generar el XML, firmarlo y descargar
/// el documento firmado (RF-17, RF-25).
/// </summary>
public sealed class FirmaEndpointTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private async Task<(HttpClient Cliente, Guid Id)> FacturaConXml()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var emision = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 2m, null, null)]));

        emision.EnsureSuccessStatusCode();

        var id = (await LeerJson(emision)).GetProperty("id").GetGuid();

        (await cliente.PostAsync($"{RutaDocumentos}/{id}/xml", null))
            .EnsureSuccessStatusCode();

        return (cliente, id);
    }

    [Fact]
    public async Task Firmar_responde_200_y_no_cambia_el_estado()
    {
        var (cliente, id) = await FacturaConXml();

        var respuesta = await cliente.PostAsync($"{RutaDocumentos}/{id}/firma", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        // Firmar no es una transicion: el documento sigue donde estaba.
        Assert.Equal(
            "EN_PROCESO",
            (await LeerJson(respuesta)).GetProperty("estado").GetString());
    }

    /// <summary>
    /// La prueba que cierra el hito: la firma que sale por la API se
    /// verifica con la clave publica del certificado configurado.
    /// </summary>
    [Fact]
    public async Task El_xml_descargado_lleva_una_firma_verificable()
    {
        var (cliente, id) = await FacturaConXml();

        (await cliente.PostAsync($"{RutaDocumentos}/{id}/firma", null))
            .EnsureSuccessStatusCode();

        var xml = await (await cliente.GetAsync($"{RutaDocumentos}/{id}/xml"))
            .Content.ReadAsStringAsync();

        var documento = new XmlDocument { PreserveWhitespace = true };
        documento.LoadXml(xml);

        var nodo = (XmlElement)documento.SelectSingleNode(
            "//*[local-name()='Signature' and namespace-uri()='http://www.w3.org/2000/09/xmldsig#']")!;

        var verificador = new SignedXml(documento);
        verificador.LoadXml(nodo);

        Assert.True(verificador.CheckSignature(fabrica.Certificado.GetRSAPublicKey()!));
    }

    [Fact]
    public async Task El_xml_firmado_sigue_validando_contra_el_esquema()
    {
        var (cliente, id) = await FacturaConXml();

        (await cliente.PostAsync($"{RutaDocumentos}/{id}/firma", null))
            .EnsureSuccessStatusCode();

        var xml = await (await cliente.GetAsync($"{RutaDocumentos}/{id}/xml"))
            .Content.ReadAsStringAsync();

        ValidadorEsquemas.AssertValida(xml, ValidadorEsquemas.Factura);
    }

    [Fact]
    public async Task Firmar_dos_veces_se_rechaza()
    {
        var (cliente, id) = await FacturaConXml();

        (await cliente.PostAsync($"{RutaDocumentos}/{id}/firma", null))
            .EnsureSuccessStatusCode();

        var segunda = await cliente.PostAsync($"{RutaDocumentos}/{id}/firma", null);

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal(
            "XML_YA_FIRMADO",
            (await LeerJson(segunda)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task No_se_puede_firmar_sin_haber_generado_el_xml()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var emision = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        var id = (await LeerJson(emision)).GetProperty("id").GetGuid();

        var respuesta = await cliente.PostAsync($"{RutaDocumentos}/{id}/firma", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "XML_NO_DISPONIBLE",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    /// <summary>
    /// Antes de firmar, la descarga entrega el XML sin firmar; despues,
    /// el firmado. El sin firmar se conserva para poder reproducir el
    /// calculo, no para entregarlo.
    /// </summary>
    [Fact]
    public async Task La_descarga_entrega_el_firmado_en_cuanto_existe()
    {
        var (cliente, id) = await FacturaConXml();

        var antes = await (await cliente.GetAsync($"{RutaDocumentos}/{id}/xml"))
            .Content.ReadAsStringAsync();

        Assert.DoesNotContain("Signature", antes);

        (await cliente.PostAsync($"{RutaDocumentos}/{id}/firma", null))
            .EnsureSuccessStatusCode();

        var despues = await (await cliente.GetAsync($"{RutaDocumentos}/{id}/xml"))
            .Content.ReadAsStringAsync();

        Assert.Contains("Signature", despues);
        Assert.Contains("dMoMvtcG5aIzgYo0tIsSQeVJBDnUnfSOfBpxXrmor0Y=", despues);
    }

    [Fact]
    public async Task Firmar_un_documento_inexistente_responde_404()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsync(
            $"{RutaDocumentos}/{Guid.NewGuid()}/firma", null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
