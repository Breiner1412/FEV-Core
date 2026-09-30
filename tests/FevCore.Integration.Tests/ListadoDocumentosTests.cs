using System.Net.Http.Json;
using System.Text.Json;
using FevCore.Domain.Integradores;
using FevCore.Infrastructure.Persistencia;
using Microsoft.Extensions.DependencyInjection;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// El listado de documentos (RF-24).
///
/// Dos cosas se prueban aqui que no se ven en el codigo: que el total
/// acompana al filtro y no a la tabla, y que el listado devuelve por defecto
/// los documentos del integrador que pregunta.
///
/// Esa segunda regla no es un permiso. La version 1 tiene una sola empresa
/// emisora y los integradores son sus propios sistemas, asi que consultar un
/// documento por identificador esta abierto a proposito. El listado se
/// estrecha por comodidad —el punto de venta rara vez quiere paginar entre
/// las facturas del ERP— y se puede ensanchar con todos=true.
/// </summary>
public sealed class ListadoDocumentosTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private const string LlaveDelOtro = "fev_llave_de_otro_integrador_para_listado";

    private async Task<HttpClient> ClienteDeOtroIntegrador()
    {
        using var alcance = fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<FevCoreDbContext>();

        if (!contexto.Integradores.Any(i => i.Nombre == "Punto de venta"))
        {
            contexto.Integradores.Add(Integrador.CrearConLlave(
                "Punto de venta", LlaveDelOtro, DateTimeOffset.UtcNow));

            await contexto.SaveChangesAsync();
        }

        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", LlaveDelOtro);
        return cliente;
    }

    private static async Task<Guid> EmitirFactura(HttpClient cliente, Guid adquirente, Guid producto)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        respuesta.EnsureSuccessStatusCode();

        return (await LeerJson(respuesta)).GetProperty("id").GetGuid();
    }

    private static JsonElement[] Elementos(JsonElement pagina) =>
        [.. pagina.GetProperty("elementos").EnumerateArray()];

    [Fact]
    public async Task El_listado_devuelve_los_documentos_con_su_total()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);
        var id = await EmitirFactura(cliente, adquirente, producto);

        var pagina = await LeerJson(await cliente.GetAsync($"{RutaDocumentos}?tamanoPagina=100"));

        var documento = Elementos(pagina).Single(e => e.GetProperty("id").GetGuid() == id);

        Assert.Equal("FACTURA", documento.GetProperty("tipo").GetString());
        Assert.False(string.IsNullOrWhiteSpace(documento.GetProperty("numeroCompleto").GetString()));

        // El resumen trae la razon social del adquirente ya resuelta: un
        // listado que obligara a consultar cada documento para pintar una
        // columna no serviria de nada.
        Assert.False(string.IsNullOrWhiteSpace(
            documento.GetProperty("adquirenteRazonSocial").GetString()));

        Assert.True(documento.GetProperty("totalAPagar").GetDecimal() > 0);
    }

    [Fact]
    public async Task El_total_corresponde_al_filtro_y_no_a_la_tabla()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);
        await EmitirFactura(cliente, adquirente, producto);

        var todas = await LeerJson(await cliente.GetAsync($"{RutaDocumentos}?tamanoPagina=100"));

        // No hay notas de credito emitidas por este integrador en esta clase,
        // asi que filtrar por ellas tiene que dar cero y no el total de la
        // tabla. Contar sobre la consulta sin filtrar es el error clasico de
        // la paginacion, y no se nota hasta que alguien pagina.
        var soloNotas = await LeerJson(
            await cliente.GetAsync($"{RutaDocumentos}?tipo=NOTA_CREDITO&tamanoPagina=100"));

        Assert.True(todas.GetProperty("totalElementos").GetInt64() > 0);
        Assert.Equal(0, soloNotas.GetProperty("totalElementos").GetInt64());
        Assert.Empty(Elementos(soloNotas));
    }

    [Fact]
    public async Task Filtrar_por_estado_deja_fuera_los_demas()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);
        await EmitirFactura(cliente, adquirente, producto);

        var pagina = await LeerJson(
            await cliente.GetAsync($"{RutaDocumentos}?estado=RECIBIDO&tamanoPagina=100"));

        Assert.NotEmpty(Elementos(pagina));
        Assert.All(Elementos(pagina), e =>
            Assert.Equal("RECIBIDO", e.GetProperty("estado").GetString()));
    }

    [Fact]
    public async Task Un_rango_de_fechas_que_no_incluye_hoy_no_devuelve_nada()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);
        await EmitirFactura(cliente, adquirente, producto);

        var ayer = Hoy().AddDays(-1);

        var pasado = await LeerJson(await cliente.GetAsync(
            $"{RutaDocumentos}?desde={ayer.AddDays(-30):yyyy-MM-dd}&hasta={ayer:yyyy-MM-dd}"));

        Assert.Equal(0, pasado.GetProperty("totalElementos").GetInt64());
    }

    [Fact]
    public async Task Hasta_incluye_el_dia_completo()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);
        await EmitirFactura(cliente, adquirente, producto);

        // El contrato dice que hasta es inclusive. Si se comparara contra el
        // principio del dia en vez de contra el principio del siguiente, un
        // documento emitido esta tarde se quedaria fuera de una busqueda que
        // pide expresamente el dia de hoy.
        var hoy = await LeerJson(await cliente.GetAsync(
            $"{RutaDocumentos}?desde={Hoy():yyyy-MM-dd}&hasta={Hoy():yyyy-MM-dd}&tamanoPagina=100"));

        Assert.True(hoy.GetProperty("totalElementos").GetInt64() > 0);
    }

    [Fact]
    public async Task El_tamano_de_pagina_tiene_techo()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var pagina = await LeerJson(
            await cliente.GetAsync($"{RutaDocumentos}?tamanoPagina=5000"));

        // Recortado, no rechazado. Pedir demasiado no es un error del
        // integrador, pero traer cien mil documentos de una vez si seria un
        // problema del servidor.
        Assert.Equal(100, pagina.GetProperty("tamanoPagina").GetInt32());
    }

    [Fact]
    public async Task Una_pagina_menor_que_uno_se_trata_como_la_primera()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var pagina = await LeerJson(await cliente.GetAsync($"{RutaDocumentos}?pagina=0"));

        // Sin esto, el desplazamiento seria negativo y PostgreSQL rechazaria
        // la consulta con un 500 por un parametro que el usuario escribio mal.
        Assert.Equal(1, pagina.GetProperty("pagina").GetInt32());
    }

    [Fact]
    public async Task Por_defecto_cada_integrador_ve_lo_suyo()
    {
        var mio = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(mio);
        await AsegurarRangoFacturas(mio);

        var adquirente = await CrearAdquirente(mio);
        var producto = await CrearProducto(mio);
        var idMio = await EmitirFactura(mio, adquirente, producto);

        var otro = await ClienteDeOtroIntegrador();

        var suyos = await LeerJson(
            await otro.GetAsync($"{RutaDocumentos}?tamanoPagina=100"));

        Assert.DoesNotContain(
            Elementos(suyos), e => e.GetProperty("id").GetGuid() == idMio);
    }

    [Fact]
    public async Task Con_todos_se_ven_los_de_la_empresa_entera()
    {
        var mio = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(mio);
        await AsegurarRangoFacturas(mio);

        var adquirente = await CrearAdquirente(mio);
        var producto = await CrearProducto(mio);
        var idMio = await EmitirFactura(mio, adquirente, producto);

        var otro = await ClienteDeOtroIntegrador();

        var completo = await LeerJson(
            await otro.GetAsync($"{RutaDocumentos}?todos=true&tamanoPagina=100"));

        // Hay una sola empresa emisora: los documentos son de la empresa, no
        // del sistema que los origino. Impedir esto seria fingir un
        // aislamiento que el alcance de la version 1 no pide.
        Assert.Contains(
            Elementos(completo), e => e.GetProperty("id").GetGuid() == idMio);
    }

    [Fact]
    public async Task Sin_llave_no_se_lista()
    {
        var anonimo = fabrica.CreateClient();

        var respuesta = await anonimo.GetAsync(RutaDocumentos);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_tipo_que_no_existe_se_rechaza_diciendo_cuales_valen()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.GetAsync($"{RutaDocumentos}?tipo=FACTURITA");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, respuesta.StatusCode);

        var problema = await LeerJson(respuesta);

        Assert.Equal("PARAMETRO_INVALIDO", problema.GetProperty("codigo").GetString());
        Assert.Equal("tipo", problema.GetProperty("parametro").GetString());

        // El mensaje tiene que nombrar los valores como los escribe el
        // contrato. Decir NotaCredito cuando el contrato dice NOTA_CREDITO
        // seria peor que no decir nada.
        Assert.Contains("NOTA_CREDITO", problema.GetProperty("detail").GetString());
    }

}
