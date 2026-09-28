using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// El camino de H5 por HTTP: emitir, generar el XML, descargarlo y
/// comprobar que valida contra el esquema oficial (RF-16, RF-25, CE-05).
/// </summary>
public sealed class XmlEndpointTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private async Task<(HttpClient Cliente, Guid FacturaId)> EmitirFactura()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 2m, null, null)]));

        respuesta.EnsureSuccessStatusCode();

        return (cliente, (await LeerJson(respuesta)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Generar_el_xml_asigna_codigo_unico_y_avanza_el_estado()
    {
        var (cliente, facturaId) = await EmitirFactura();

        var respuesta = await cliente.PostAsync($"{RutaDocumentos}/{facturaId}/xml", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var documento = await LeerJson(respuesta);

        Assert.Equal("EN_PROCESO", documento.GetProperty("estado").GetString());

        var codigo = documento.GetProperty("codigoUnico").GetString();

        Assert.NotNull(codigo);
        Assert.Matches("^[0-9a-f]{96}$", codigo);
    }

    /// <summary>
    /// La prueba que cierra CE-05: el XML que sale por la API, no el que
    /// produce el generador en memoria, valida contra el esquema oficial.
    /// </summary>
    [Fact]
    public async Task El_xml_descargado_valida_contra_el_esquema_oficial()
    {
        var (cliente, facturaId) = await EmitirFactura();

        (await cliente.PostAsync($"{RutaDocumentos}/{facturaId}/xml", null))
            .EnsureSuccessStatusCode();

        var respuesta = await cliente.GetAsync($"{RutaDocumentos}/{facturaId}/xml");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/xml", respuesta.Content.Headers.ContentType?.MediaType);

        ValidadorEsquemas.AssertValida(
            await respuesta.Content.ReadAsStringAsync(),
            ValidadorEsquemas.Factura);
    }

    /// <summary>
    /// Un documento se representa de una sola forma. Regenerar el XML tras
    /// firmarlo invalidaria la firma, asi que ni siquiera se permite antes.
    /// </summary>
    [Fact]
    public async Task El_xml_no_se_puede_generar_dos_veces()
    {
        var (cliente, facturaId) = await EmitirFactura();

        (await cliente.PostAsync($"{RutaDocumentos}/{facturaId}/xml", null))
            .EnsureSuccessStatusCode();

        var segunda = await cliente.PostAsync($"{RutaDocumentos}/{facturaId}/xml", null);

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal(
            "XML_YA_GENERADO",
            (await LeerJson(segunda)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Descargar_el_xml_antes_de_generarlo_responde_409()
    {
        var (cliente, facturaId) = await EmitirFactura();

        var respuesta = await cliente.GetAsync($"{RutaDocumentos}/{facturaId}/xml");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "XML_NO_DISPONIBLE",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task La_generacion_queda_registrada_en_el_historial()
    {
        var (cliente, facturaId) = await EmitirFactura();

        (await cliente.PostAsync($"{RutaDocumentos}/{facturaId}/xml", null))
            .EnsureSuccessStatusCode();

        var historial = await LeerJson(
            await cliente.GetAsync($"{RutaDocumentos}/{facturaId}/historial"));

        var entradas = historial.EnumerateArray().ToList();

        Assert.Equal(2, entradas.Count);
        Assert.Equal("RECIBIDO", entradas[0].GetProperty("estadoNuevo").GetString());
        Assert.Equal("EN_PROCESO", entradas[1].GetProperty("estadoNuevo").GetString());
        Assert.Contains("XML", entradas[1].GetProperty("motivo").GetString()!);
    }

    /// <summary>
    /// El codigo unico sobrevive al viaje por la base de datos. Parece obvio
    /// y no lo es: el hash se calculo en memoria y aqui se vuelve a leer de
    /// PostgreSQL, en otra peticion.
    /// </summary>
    [Fact]
    public async Task El_codigo_unico_del_xml_es_el_que_queda_guardado()
    {
        var (cliente, facturaId) = await EmitirFactura();

        var generacion = await LeerJson(
            await cliente.PostAsync($"{RutaDocumentos}/{facturaId}/xml", null));

        var codigoGuardado = generacion.GetProperty("codigoUnico").GetString();

        var consulta = await LeerJson(
            await cliente.GetAsync($"{RutaDocumentos}/{facturaId}"));

        Assert.Equal(codigoGuardado, consulta.GetProperty("codigoUnico").GetString());

        var xml = await (await cliente.GetAsync($"{RutaDocumentos}/{facturaId}/xml"))
            .Content.ReadAsStringAsync();

        Assert.Contains(codigoGuardado!, xml);
    }
}
