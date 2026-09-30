using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// Consultar, modificar y desactivar emisor, adquirentes y productos
/// (RF-03, RF-06, RF-07).
///
/// Existe por lo que destapo la revision de trazabilidad de H8. Los tres
/// catalogos se usaban en casi todas las pruebas, pero siempre de la misma
/// forma: crear uno para poder emitir. Crear quedaba ejercitado de rebote;
/// consultar, modificar y desactivar no se probaban en ningun sitio.
///
/// Es un punto ciego facil de tener y dificil de ver: la suite estaba verde
/// y el recuento de pruebas subia hito a hito, mientras tres verbos de tres
/// requerimientos "Debe" no los tocaba nadie. Un requerimiento cubierto de
/// rebote no esta cubierto: esta sin probar y con suerte.
/// </summary>
public sealed class CatalogosEndpointTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private const string RutaAdquirentes = "/api/v1/adquirentes";
    private const string RutaProductos = "/api/v1/productos";
    private const string RutaEmisor = "/api/v1/emisor";

    // ── Emisor (RF-03) ──

    [Fact]
    public async Task El_emisor_configurado_se_puede_consultar()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);

        var emisor = await LeerJson(await cliente.GetAsync(RutaEmisor));

        Assert.Equal(
            "Comercializadora del Eje SAS",
            emisor.GetProperty("datos").GetProperty("razonSocial").GetString());

        // El digito de verificacion viaja: sin el, el integrador no puede
        // reproducir el NIT completo tal como aparece en el documento.
        Assert.Equal(
            "4",
            emisor.GetProperty("datos").GetProperty("digitoVerificacion").GetString());
    }

    [Fact]
    public async Task Reconfigurar_el_emisor_reemplaza_sus_datos()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);

        var respuesta = await cliente.PutAsJsonAsync(RutaEmisor, new
        {
            datos = new
            {
                tipoIdentificacion = "31",
                identificacion = "800197268",
                digitoVerificacion = "4",
                razonSocial = "Comercializadora del Eje SAS",
                direccion = "Avenida 30 de Agosto # 40-50",
                municipioCodigo = "66001",
                regimen = "48",
                responsabilidades = new[] { "O-13", "O-15" }
            },
            nombreComercial = "Comercializadora del Eje"
        });

        respuesta.EnsureSuccessStatusCode();

        var emisor = await LeerJson(await cliente.GetAsync(RutaEmisor));

        // Hay un solo emisor: configurarlo de nuevo lo reemplaza, no crea
        // otro. Los documentos ya emitidos conservan su copia (RN-10).
        Assert.Equal(
            "Avenida 30 de Agosto # 40-50",
            emisor.GetProperty("datos").GetProperty("direccion").GetString());
    }

    [Fact]
    public async Task Un_emisor_con_nit_mal_verificado_se_rechaza()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PutAsJsonAsync(RutaEmisor, new
        {
            datos = new
            {
                tipoIdentificacion = "31",
                identificacion = "800197268",
                digitoVerificacion = "9",
                razonSocial = "Comercializadora del Eje SAS",
                direccion = "Calle 20 # 8-45",
                municipioCodigo = "66001",
                regimen = "48",
                responsabilidades = new[] { "O-13" }
            },
            nombreComercial = "Comercializadora del Eje"
        });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var problema = await LeerJson(respuesta);

        Assert.Equal(
            "DIGITO_VERIFICACION_INCORRECTO",
            problema.GetProperty("codigo").GetString());

        // El mensaje dice cual deberia ser. Un error de validacion que no
        // dice como corregirlo obliga a buscar el algoritmo por fuera.
        Assert.Contains("4", problema.GetProperty("detail").GetString()!);
    }

    // ── Adquirentes (RF-06) ──

    [Fact]
    public async Task Un_adquirente_se_consulta_por_su_identificador()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var id = await CrearAdquirente(cliente);

        var adquirente = await LeerJson(
            await cliente.GetAsync($"{RutaAdquirentes}/{id}"));

        Assert.Equal(id, adquirente.GetProperty("id").GetGuid());
        Assert.True(adquirente.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Consultar_un_adquirente_que_no_existe_da_404()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.GetAsync(
            $"{RutaAdquirentes}/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Modificar_un_adquirente_cambia_sus_datos()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var id = await CrearAdquirente(cliente);

        var actual = await LeerJson(await cliente.GetAsync($"{RutaAdquirentes}/{id}"));
        var identificacion = actual.GetProperty("datos")
            .GetProperty("identificacion").GetString();

        var respuesta = await cliente.PutAsJsonAsync($"{RutaAdquirentes}/{id}", new
        {
            datos = new
            {
                tipoIdentificacion = "13",
                identificacion,
                razonSocial = "Juan Perez Gomez",
                direccion = "Carrera 10 # 5-20",
                municipioCodigo = "66001",
                regimen = "49",
                correo = "juan@ejemplo.com"
            }
        });

        respuesta.EnsureSuccessStatusCode();

        var despues = await LeerJson(await cliente.GetAsync($"{RutaAdquirentes}/{id}"));

        Assert.Equal(
            "Juan Perez Gomez",
            despues.GetProperty("datos").GetProperty("razonSocial").GetString());
    }

    [Fact]
    public async Task Un_adquirente_desactivado_no_sirve_para_emitir()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        (await cliente.DeleteAsync($"{RutaAdquirentes}/{adquirente}"))
            .EnsureSuccessStatusCode();

        var consultado = await LeerJson(
            await cliente.GetAsync($"{RutaAdquirentes}/{adquirente}"));

        // Se desactiva, no se borra: los documentos ya emitidos lo
        // referencian, y perder esa referencia romperia la trazabilidad.
        Assert.False(consultado.GetProperty("activo").GetBoolean());

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "ADQUIRENTE_INACTIVO",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Dos_adquirentes_activos_no_comparten_identificacion()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var id = await CrearAdquirente(cliente);

        var existente = await LeerJson(await cliente.GetAsync($"{RutaAdquirentes}/{id}"));

        var respuesta = await cliente.PostAsJsonAsync(RutaAdquirentes, new
        {
            datos = new
            {
                tipoIdentificacion = "13",
                identificacion = existente.GetProperty("datos")
                    .GetProperty("identificacion").GetString(),
                razonSocial = "Otro Juan",
                direccion = "Calle 1",
                municipioCodigo = "66001",
                regimen = "49"
            }
        });

        // INV-ADQ-01. Sin esto, dos fichas del mismo comprador se
        // desincronizan y las facturas salen con datos distintos segun cual
        // se eligiera.
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "IDENTIFICACION_DUPLICADA",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    // ── Productos (RF-07) ──

    [Fact]
    public async Task Un_producto_se_consulta_con_sus_impuestos()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var id = await CrearProducto(cliente);

        var producto = await LeerJson(await cliente.GetAsync($"{RutaProductos}/{id}"));

        Assert.Equal(150_000m, producto.GetProperty("precioUnitario").GetDecimal());

        var impuesto = producto.GetProperty("impuestos").EnumerateArray().Single();

        Assert.Equal("IVA", impuesto.GetProperty("tipo").GetString());
        Assert.Equal(19m, impuesto.GetProperty("tarifa").GetDecimal());
    }

    [Fact]
    public async Task Cambiar_el_precio_no_toca_las_facturas_ya_emitidas()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var emitida = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        var documentoId = (await LeerJson(emitida)).GetProperty("id").GetGuid();

        var actual = await LeerJson(await cliente.GetAsync($"{RutaProductos}/{producto}"));

        (await cliente.PutAsJsonAsync($"{RutaProductos}/{producto}", new
        {
            codigo = actual.GetProperty("codigo").GetString(),
            descripcion = "Teclado mecanico retroiluminado",
            unidadMedida = "94",
            precioUnitario = 180_000m,
            impuestos = new[] { new { tipo = "IVA", tarifa = 19m } }
        })).EnsureSuccessStatusCode();

        var documento = await LeerJson(
            await cliente.GetAsync($"{RutaDocumentos}/{documentoId}"));

        var linea = documento.GetProperty("lineas").EnumerateArray().Single();

        // Una factura emitida es una fotografia de un acuerdo, no una
        // consulta viva contra el catalogo. Es la misma regla que RN-10
        // aplica a los datos de las partes.
        Assert.Equal(150_000m, linea.GetProperty("precioUnitario").GetDecimal());
        Assert.Equal("Teclado mecanico", linea.GetProperty("descripcion").GetString());
    }

    [Fact]
    public async Task Un_producto_desactivado_no_sirve_para_emitir()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        (await cliente.DeleteAsync($"{RutaProductos}/{producto}"))
            .EnsureSuccessStatusCode();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "PRODUCTO_INACTIVO",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    // ── Registros correlacionados (RNF-10) ──

    [Fact]
    public async Task Toda_respuesta_de_error_trae_un_identificador_de_correlacion()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.GetAsync(
            $"{RutaAdquirentes}/{Guid.CreateVersion7()}");

        var problema = await LeerJson(respuesta);

        // Es lo unico que une una respuesta con sus lineas de registro. Sin
        // el, diagnosticar un fallo en produccion empieza por adivinar cual
        // de las peticiones de ese minuto era la del cliente que reclama.
        Assert.False(string.IsNullOrWhiteSpace(
            problema.GetProperty("traceId").GetString()));
    }
}
