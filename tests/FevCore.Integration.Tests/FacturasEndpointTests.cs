using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FevCore.Integration.Tests;

/// <summary>
/// El camino delgado de H1, de punta a punta: emitir una factura por HTTP,
/// guardarla en PostgreSQL y volver a consultarla.
/// </summary>
public sealed class FacturasEndpointTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private const string RutaFacturas = "/api/v1/facturas";

    // ── Ayudantes ──

    private static object SolicitudFactura(
        string referencia,
        decimal cantidad = 2m,
        decimal precioUnitario = 150_000m,
        decimal? descuento = null,
        int lineas = 1) => new
        {
            referenciaExterna = referencia,
            lineas = Enumerable.Range(1, lineas).Select(i => new
            {
                codigo = $"PROD-{i:000}",
                descripcion = "Teclado mecanico",
                unidadMedida = "94",
                cantidad,
                precioUnitario,
                descuento,
                impuestos = new[] { new { tipo = "IVA", tarifa = 19m } }
            }).ToArray()
        };

    private static string Referencia() => $"VTA-{Guid.NewGuid():N}"[..20];

    private static async Task<JsonElement> LeerJson(HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    // ── Emision ──

    [Fact]
    public async Task Emitir_una_factura_responde_202_con_el_documento()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia()));

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var documento = await LeerJson(respuesta);

        Assert.Equal("FACTURA", documento.GetProperty("tipo").GetString());
        Assert.Equal("RECIBIDO", documento.GetProperty("estado").GetString());
        Assert.NotEqual(Guid.Empty, documento.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task El_202_incluye_la_direccion_para_consultar_el_documento()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia()));

        var documento = await LeerJson(respuesta);
        var id = documento.GetProperty("id").GetGuid();

        Assert.Equal(
            $"/api/v1/documentos/{id}",
            respuesta.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Los_totales_se_calculan_al_emitir()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), cantidad: 2m, precioUnitario: 150_000m));

        var totales = (await LeerJson(respuesta)).GetProperty("totales");

        Assert.Equal(300_000m, totales.GetProperty("totalBaseImponible").GetDecimal());
        Assert.Equal(57_000m, totales.GetProperty("totalImpuestos").GetDecimal());
        Assert.Equal(357_000m, totales.GetProperty("totalAPagar").GetDecimal());
    }

    // ── RN-06: el redondeo va sobre el total, verificado por HTTP ──

    [Fact]
    public async Task El_redondeo_del_total_llega_correcto_hasta_la_respuesta()
    {
        // Tres lineas de 1.000,01 con IVA del 19%.
        // IVA exacto por linea: 190,0019
        //   redondeando por linea: 190,00 x 3 = 570,00
        //   redondeando el total:  570,0057    = 570,01
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), cantidad: 1m, precioUnitario: 1_000.01m, lineas: 3));

        var totales = (await LeerJson(respuesta)).GetProperty("totales");

        Assert.Equal(570.01m, totales.GetProperty("totalImpuestos").GetDecimal());
        Assert.Equal(3_570.04m, totales.GetProperty("totalAPagar").GetDecimal());
    }

    // ── RF-15: reintentar no duplica ──

    [Fact]
    public async Task Reenviar_la_misma_referencia_devuelve_el_documento_existente()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var referencia = Referencia();
        var solicitud = SolicitudFactura(referencia);

        var primera = await cliente.PostAsJsonAsync(RutaFacturas, solicitud);
        var segunda = await cliente.PostAsJsonAsync(RutaFacturas, solicitud);

        // La primera crea; la segunda encuentra.
        Assert.Equal(HttpStatusCode.Accepted, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);

        var documentoUno = await LeerJson(primera);
        var documentoDos = await LeerJson(segunda);

        Assert.Equal(
            documentoUno.GetProperty("id").GetGuid(),
            documentoDos.GetProperty("id").GetGuid());

        // Y sobre todo: no consumio un consecutivo nuevo.
        Assert.Equal(
            documentoUno.GetProperty("consecutivo").GetInt64(),
            documentoDos.GetProperty("consecutivo").GetInt64());
    }

    [Fact]
    public async Task Dos_referencias_distintas_producen_documentos_distintos()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var una = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia())));

        var otra = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia())));

        Assert.NotEqual(
            una.GetProperty("id").GetGuid(),
            otra.GetProperty("id").GetGuid());

        Assert.NotEqual(
            una.GetProperty("consecutivo").GetInt64(),
            otra.GetProperty("consecutivo").GetInt64());
    }

    // ── RF-22: consulta ──

    [Fact]
    public async Task Un_documento_emitido_se_puede_consultar_despues()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var emitido = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia())));

        var id = emitido.GetProperty("id").GetGuid();

        var respuesta = await cliente.GetAsync($"/api/v1/documentos/{id}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var consultado = await LeerJson(respuesta);

        Assert.Equal(id, consultado.GetProperty("id").GetGuid());
        Assert.Equal(
            emitido.GetProperty("numeroCompleto").GetString(),
            consultado.GetProperty("numeroCompleto").GetString());
        Assert.Equal(
            emitido.GetProperty("totales").GetProperty("totalAPagar").GetDecimal(),
            consultado.GetProperty("totales").GetProperty("totalAPagar").GetDecimal());
    }

    [Fact]
    public async Task Las_lineas_sobreviven_el_viaje_a_la_base_de_datos()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var emitido = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia(), lineas: 3)));

        var id = emitido.GetProperty("id").GetGuid();

        var consultado = await LeerJson(await cliente.GetAsync($"/api/v1/documentos/{id}"));
        var lineas = consultado.GetProperty("lineas");

        Assert.Equal(3, lineas.GetArrayLength());

        var primera = lineas[0];
        Assert.Equal(1, primera.GetProperty("numero").GetInt32());
        Assert.Single(primera.GetProperty("impuestos").EnumerateArray());
        Assert.Equal("IVA", primera.GetProperty("impuestos")[0].GetProperty("tipo").GetString());
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

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia()));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_una_llave_desconocida_responde_401()
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", "fev_esta_llave_no_existe_en_ningun_lado");

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia()));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        var problema = await LeerJson(respuesta);
        Assert.Equal("LLAVE_INVALIDA", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Una_cantidad_en_cero_responde_400()
    {
        // 400, no 409: la solicitud esta mal escrita, no es una regla de
        // negocio que no procede. Se detecta antes de tocar el dominio.
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia(), cantidad: 0m));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_documento_sin_lineas_responde_400()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(RutaFacturas, new
        {
            referenciaExterna = Referencia(),
            lineas = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_descuento_mayor_que_la_linea_responde_409()
    {
        // 409, no 400: la solicitud esta bien formada. Lo que falla es una
        // regla del negocio (INV-LIN-02), y solo el dominio puede saberlo.
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(
                Referencia(),
                cantidad: 1m,
                precioUnitario: 100_000m,
                descuento: 150_000m));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var problema = await LeerJson(respuesta);
        Assert.Equal("LINEA_DESCUENTO_EXCESIVO", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Los_errores_no_exponen_detalles_internos()
    {
        // RNF-11: ni rutas de archivos, ni nombres de tablas, ni trazas.
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), cantidad: 1m, precioUnitario: 100m, descuento: 500m));

        // Se afirma primero el codigo esperado: sin esto, la prueba pasaria
        // igual ante un 500, que tampoco contiene esas palabras. Una prueba
        // que pasa por la razon equivocada es peor que no tenerla.
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
        // RNF-10: dado ese identificador se pueden recuperar los registros
        // de esa peticion en el servidor.
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas, SolicitudFactura(Referencia()));

        Assert.True(respuesta.Headers.Contains("X-Trace-Id"));
    }
}
