using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FevCore.Integration.Tests;

/// <summary>
/// El camino completo de H2: configurar el emisor, registrar adquirente y
/// productos, emitir contra el catalogo y comprobar que el documento guarda
/// copias y no referencias.
/// </summary>
public sealed class FacturasEndpointTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private const string RutaFacturas = "/api/v1/facturas";

    // ── Ayudantes ──
    //
    // Delegan en AyudantesPruebas para no repetir el montaje que tambien
    // necesitan las pruebas de numeracion. Los nombres se conservan tal cual
    // para que ninguna de las pruebas de abajo tenga que cambiar.

    private static string Referencia() => AyudantesPruebas.Referencia();

    private static Task<JsonElement> LeerJson(HttpResponseMessage respuesta) =>
        AyudantesPruebas.LeerJson(respuesta);

    /// <summary>
    /// Montaje minimo para poder emitir: emisor configurado y un rango de
    /// numeracion vigente.
    ///
    /// El rango entra aqui en H3. Antes, con el consecutivo provisional,
    /// bastaba con el emisor; ahora RN-02 exige autorizacion de numeracion.
    /// </summary>
    private static async Task ConfigurarEmisor(HttpClient cliente)
    {
        await AyudantesPruebas.ConfigurarEmisor(cliente);
        await AyudantesPruebas.AsegurarRangoFacturas(cliente);
    }

    private static Task<Guid> CrearAdquirente(HttpClient cliente) =>
        AyudantesPruebas.CrearAdquirente(cliente);

    private static Task<Guid> CrearProducto(
        HttpClient cliente,
        decimal precio = 150_000m,
        decimal tarifaIva = 19m) =>
        AyudantesPruebas.CrearProducto(cliente, precio, tarifaIva);

    private static object SolicitudFactura(
        string referencia,
        Guid adquirenteId,
        IEnumerable<(Guid ProductoId, decimal Cantidad, decimal? Precio, decimal? Descuento)> lineas) =>
        AyudantesPruebas.SolicitudFactura(referencia, adquirenteId, lineas);

    // ── Emision contra el catalogo ──

    [Fact]
    public async Task Emitir_una_factura_responde_202_con_el_documento()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 2m, null, null)]));

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var documento = await LeerJson(respuesta);

        Assert.Equal("FACTURA", documento.GetProperty("tipo").GetString());
        Assert.Equal("RECIBIDO", documento.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task La_linea_copia_los_datos_del_producto()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente, precio: 150_000m);

        var documento = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 2m, null, null)])));

        var linea = documento.GetProperty("lineas")[0];

        // La solicitud solo envio el identificador del producto y la cantidad.
        // Todo lo demas lo copio el sistema del catalogo.
        Assert.Equal("Teclado mecanico", linea.GetProperty("descripcion").GetString());
        Assert.Equal("94", linea.GetProperty("unidadMedida").GetString());
        Assert.Equal(150_000m, linea.GetProperty("precioUnitario").GetDecimal());
        Assert.Equal(producto, linea.GetProperty("productoId").GetGuid());
        Assert.Equal("IVA", linea.GetProperty("impuestos")[0].GetProperty("tipo").GetString());
    }

    [Fact]
    public async Task El_documento_guarda_copia_de_los_datos_de_ambas_partes()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var documento = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)])));

        Assert.Equal(
            "Comercializadora del Eje SAS",
            documento.GetProperty("emisor").GetProperty("razonSocial").GetString());

        Assert.Equal(
            "Juan Perez",
            documento.GetProperty("adquirente").GetProperty("razonSocial").GetString());
    }

    // ── RN-10: LA DEMOSTRACION DEL HITO ──

    [Fact]
    public async Task Cambiar_el_precio_de_un_producto_no_altera_facturas_ya_emitidas()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente, precio: 150_000m);

        // 1. Se emite con el precio de hoy.
        var emitida = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 2m, null, null)])));

        var id = emitida.GetProperty("id").GetGuid();

        Assert.Equal(357_000m,
            emitida.GetProperty("totales").GetProperty("totalAPagar").GetDecimal());

        // 2. Sube el precio del producto en el catalogo.
        var actualizacion = await cliente.PutAsJsonAsync($"/api/v1/productos/{producto}", new
        {
            codigo = $"PROD-{Guid.NewGuid():N}"[..12],
            descripcion = "Teclado mecanico",
            unidadMedida = "94",
            precioUnitario = 180_000m,
            impuestos = new[] { new { tipo = "IVA", tarifa = 19m } }
        });

        actualizacion.EnsureSuccessStatusCode();
        Assert.Equal(180_000m,
            (await LeerJson(actualizacion)).GetProperty("precioUnitario").GetDecimal());

        // 3. La factura de ayer sigue diciendo lo mismo que decia.
        var consultada = await LeerJson(
            await cliente.GetAsync($"/api/v1/documentos/{id}"));

        Assert.Equal(150_000m,
            consultada.GetProperty("lineas")[0].GetProperty("precioUnitario").GetDecimal());

        Assert.Equal(357_000m,
            consultada.GetProperty("totales").GetProperty("totalAPagar").GetDecimal());
    }

    [Fact]
    public async Task Cambiar_los_datos_del_adquirente_no_altera_facturas_ya_emitidas()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var emitida = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)])));

        var id = emitida.GetProperty("id").GetGuid();

        // El adquirente se muda.
        var actualizacion = await cliente.PutAsJsonAsync($"/api/v1/adquirentes/{adquirente}", new
        {
            datos = new
            {
                tipoIdentificacion = "13",
                identificacion = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
                razonSocial = "Juan Perez Gomez",
                direccion = "Avenida 30 de Agosto # 40-20",
                municipioCodigo = "66001",
                regimen = "49"
            }
        });

        actualizacion.EnsureSuccessStatusCode();

        // La factura conserva la direccion que se declaro.
        var consultada = await LeerJson(
            await cliente.GetAsync($"/api/v1/documentos/{id}"));

        var adquirenteEnFactura = consultada.GetProperty("adquirente");

        Assert.Equal("Juan Perez", adquirenteEnFactura.GetProperty("razonSocial").GetString());
        Assert.Equal("Carrera 10 # 5-20", adquirenteEnFactura.GetProperty("direccion").GetString());
    }

    // ── El precio se puede negociar ──

    [Fact]
    public async Task Se_puede_sobrescribir_el_precio_del_catalogo()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente, precio: 150_000m);

        var documento = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, 120_000m, null)])));

        Assert.Equal(120_000m,
            documento.GetProperty("lineas")[0].GetProperty("precioUnitario").GetDecimal());
    }

    // ── RN-06: el redondeo sigue yendo sobre el total ──

    [Fact]
    public async Task El_redondeo_del_total_llega_correcto_hasta_la_respuesta()
    {
        // Tres lineas de 1.000,01 con IVA del 19%.
        // IVA exacto por linea: 190,0019
        //   redondeando por linea: 190,00 x 3 = 570,00
        //   redondeando el total:  570,0057    = 570,01
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);

        var uno = await CrearProducto(cliente, precio: 1_000.01m);
        var dos = await CrearProducto(cliente, precio: 1_000.01m);
        var tres = await CrearProducto(cliente, precio: 1_000.01m);

        var documento = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente,
            [
                (uno, 1m, null, null),
                (dos, 1m, null, null),
                (tres, 1m, null, null)
            ])));

        var totales = documento.GetProperty("totales");

        Assert.Equal(570.01m, totales.GetProperty("totalImpuestos").GetDecimal());
        Assert.Equal(3_570.04m, totales.GetProperty("totalAPagar").GetDecimal());
    }

    // ── RF-15: reintentar no duplica ──

    [Fact]
    public async Task Reenviar_la_misma_referencia_devuelve_el_documento_existente()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var solicitud = SolicitudFactura(Referencia(), adquirente, [(producto, 2m, null, null)]);

        var primera = await cliente.PostAsJsonAsync(RutaFacturas, solicitud);
        var segunda = await cliente.PostAsJsonAsync(RutaFacturas, solicitud);

        Assert.Equal(HttpStatusCode.Accepted, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);

        var unaJson = await LeerJson(primera);
        var otraJson = await LeerJson(segunda);

        Assert.Equal(
            unaJson.GetProperty("id").GetGuid(),
            otraJson.GetProperty("id").GetGuid());

        // Y sobre todo: no consumio un consecutivo nuevo.
        Assert.Equal(
            unaJson.GetProperty("consecutivo").GetInt64(),
            otraJson.GetProperty("consecutivo").GetInt64());
    }

    // ── RF-22: consulta ──

    [Fact]
    public async Task Un_documento_emitido_se_puede_consultar_despues()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var emitido = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)])));

        var id = emitido.GetProperty("id").GetGuid();

        var respuesta = await cliente.GetAsync($"/api/v1/documentos/{id}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var consultado = await LeerJson(respuesta);

        Assert.Equal(id, consultado.GetProperty("id").GetGuid());
        Assert.Equal(
            emitido.GetProperty("numeroCompleto").GetString(),
            consultado.GetProperty("numeroCompleto").GetString());
    }

    [Fact]
    public async Task Un_documento_inexistente_responde_404()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.GetAsync($"/api/v1/documentos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        var problema = await LeerJson(respuesta);
        Assert.Equal("DOCUMENTO_NO_ENCONTRADO", problema.GetProperty("codigo").GetString());
    }

    // ── Errores ──

    [Fact]
    public async Task Sin_llave_de_api_responde_401()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(RutaFacturas, new
        {
            referenciaExterna = Referencia(),
            adquirenteId = Guid.NewGuid(),
            lineas = new[] { new { productoId = Guid.NewGuid(), cantidad = 1m } }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_adquirente_inexistente_responde_409()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), Guid.NewGuid(), [(producto, 1m, null, null)]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var problema = await LeerJson(respuesta);
        Assert.Equal("ADQUIRENTE_NO_ENCONTRADO", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Un_producto_inexistente_responde_409()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(Guid.NewGuid(), 1m, null, null)]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var problema = await LeerJson(respuesta);
        Assert.Equal("PRODUCTO_NO_ENCONTRADO", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Un_producto_desactivado_no_se_puede_facturar()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var baja = await cliente.DeleteAsync($"/api/v1/productos/{producto}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var problema = await LeerJson(respuesta);
        Assert.Equal("PRODUCTO_INACTIVO", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Una_cantidad_en_cero_responde_400()
    {
        // 400, no 409: la solicitud esta mal escrita. Se detecta antes de
        // tocar el dominio.
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 0m, null, null)]));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_descuento_mayor_que_la_linea_responde_409()
    {
        // 409, no 400: la solicitud esta bien formada. Lo que falla es una
        // regla del negocio (INV-LIN-02), y solo el dominio puede saberlo.
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente, precio: 100_000m);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, 150_000m)]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var problema = await LeerJson(respuesta);
        Assert.Equal("LINEA_DESCUENTO_EXCESIVO", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Los_errores_no_exponen_detalles_internos()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente, precio: 100m);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, 500m)]));

        // Se afirma primero el codigo esperado: sin esto, la prueba pasaria
        // igual ante un 500, que tampoco contiene esas palabras.
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.DoesNotContain("StackTrace", cuerpo);
        Assert.DoesNotContain("Npgsql", cuerpo);
        Assert.DoesNotContain("C:\\", cuerpo);
        Assert.DoesNotContain("/src/", cuerpo);
    }

    [Fact]
    public async Task Toda_respuesta_trae_su_identificador_de_correlacion()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        Assert.True(respuesta.Headers.Contains("X-Trace-Id"));
    }

    // ── La fecha de emision tal como la manda un integrador colombiano ──

    /// <summary>
    /// El ejemplo del propio contrato manda la fecha con desfase -05:00, y
    /// respondia 500: PostgreSQL solo acepta instantes con desfase cero en
    /// una columna timestamptz. Ninguna prueba enviaba fechaEmision, asi que
    /// el ejemplo documentado nunca se habia ejecutado (RF-11, RNF-11).
    /// </summary>
    [Fact]
    public async Task Una_fecha_de_emision_con_desfase_colombiano_se_acepta_y_conserva_el_instante()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var enColombia = new DateTimeOffset(
            DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-5).Date.AddHours(10), DateTimeKind.Unspecified),
            TimeSpan.FromHours(-5));

        var respuesta = await cliente.PostAsJsonAsync(RutaFacturas, new
        {
            referenciaExterna = Referencia(),
            adquirenteId = adquirente,
            fechaEmision = enColombia,
            lineas = new[] { new { productoId = producto, cantidad = 1m } }
        });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var fecha = (await LeerJson(respuesta)).GetProperty("fechaEmision").GetDateTimeOffset();

        Assert.Equal(enColombia.UtcDateTime, fecha.UtcDateTime);
    }
}
