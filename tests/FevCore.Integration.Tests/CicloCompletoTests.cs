using System.Net.Http.Json;
using System.Text.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// El ciclo completo de H7: un documento emitido llega solo a su estado
/// final, sin que nadie lo empuje (RF-18 a RF-21, RNF-04, RNF-05, RN-13).
///
/// Cubre el criterio CE-04: ningun documento queda en estado indeterminado.
///
/// El trabajador en segundo plano esta apagado y las pruebas llaman al
/// procesador a mano. No es una simplificacion: es la unica forma de que la
/// prueba sepa CUANDO se proceso cada paso y pueda afirmar algo sobre el
/// estado intermedio.
/// </summary>
public sealed class CicloCompletoTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private async Task<(HttpClient Cliente, Guid Id)> Emitir()
    {
        fabrica.Validacion.Reiniciar();

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

    private static async Task<JsonElement> Consultar(HttpClient cliente, Guid id) =>
        await LeerJson(await cliente.GetAsync($"{RutaDocumentos}/{id}"));

    private static async Task<string?> Estado(HttpClient cliente, Guid id) =>
        (await Consultar(cliente, id)).GetProperty("estado").GetString();

    // ── El camino feliz ──

    /// <summary>
    /// La demostracion principal de H7: emitir y no tocar nada mas.
    ///
    /// La API respondio 202 sin haber contactado a la autoridad (ADR-0005).
    /// Todo lo demas —generar el XML, firmarlo, transmitirlo y preguntar por
    /// el veredicto— ocurre despues, solo.
    /// </summary>
    [Fact]
    public async Task Una_factura_emitida_llega_sola_a_aprobado()
    {
        var (cliente, id) = await Emitir();

        Assert.Equal("RECIBIDO", await Estado(cliente, id));

        await fabrica.ProcesarTodoAsync();

        var documento = await Consultar(cliente, id);

        Assert.Equal("APROBADO", documento.GetProperty("estado").GetString());

        // Y por el camino quedo firmado y con su codigo unico.
        Assert.False(string.IsNullOrWhiteSpace(
            documento.GetProperty("codigoUnico").GetString()));
    }

    [Fact]
    public async Task El_historial_registra_todo_el_recorrido()
    {
        var (cliente, id) = await Emitir();

        await fabrica.ProcesarTodoAsync();

        var historial = await LeerJson(
            await cliente.GetAsync($"{RutaDocumentos}/{id}/historial"));

        var estados = historial.EnumerateArray()
            .Select(e => e.GetProperty("estadoNuevo").GetString())
            .ToList();

        Assert.Equal(
            ["RECIBIDO", "EN_PROCESO", "TRANSMITIDO", "APROBADO"],
            estados);
    }

    // ── Rechazo ──

    [Fact]
    public async Task Un_documento_rechazado_conserva_los_errores_de_la_autoridad()
    {
        var (cliente, id) = await Emitir();

        fabrica.Validacion.Modo = ModoValidacion.Rechaza;

        await fabrica.ProcesarTodoAsync();

        var documento = await Consultar(cliente, id);

        Assert.Equal("RECHAZADO", documento.GetProperty("estado").GetString());

        var errores = documento.GetProperty("erroresValidacion")
            .EnumerateArray().Select(e => e.GetString()!).ToList();

        Assert.Equal(2, errores.Count);
        Assert.Contains(errores, e => e.Contains("FAU14"));
    }

    // ── RNF-04: el servicio caido no rompe nada ──

    /// <summary>
    /// La emision se acepta aunque la autoridad no este. El documento queda
    /// EN ESPERA, no en error: eso es exactamente lo que pide RNF-04.
    /// </summary>
    [Fact]
    public async Task Con_el_servicio_caido_la_emision_se_acepta_y_el_documento_espera()
    {
        fabrica.Validacion.Reiniciar();
        fabrica.Validacion.Modo = ModoValidacion.Caido;

        var (cliente, id) = await EmitirConServicioCaido();

        await fabrica.ProcesarUnaTareaAsync();

        // No llego a transmitirse, pero tampoco fracaso: sigue en marcha.
        var estado = await Estado(cliente, id);

        Assert.Equal("EN_PROCESO", estado);
        Assert.True(fabrica.Validacion.TransmisionesIntentadas > 0);
    }

    private async Task<(HttpClient Cliente, Guid Id)> EmitirConServicioCaido()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangoFacturas(cliente);

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        respuesta.EnsureSuccessStatusCode();

        return (cliente, (await LeerJson(respuesta)).GetProperty("id").GetGuid());
    }

    /// <summary>
    /// Y cuando la autoridad vuelve, el documento avanza solo. Nadie tuvo
    /// que reencolar nada a mano.
    /// </summary>
    [Fact]
    public async Task Cuando_el_servicio_vuelve_el_documento_avanza_solo()
    {
        fabrica.Validacion.Reiniciar();
        fabrica.Validacion.Modo = ModoValidacion.Caido;

        var (cliente, id) = await EmitirConServicioCaido();

        await fabrica.ProcesarUnaTareaAsync();
        Assert.NotEqual("APROBADO", await Estado(cliente, id));

        fabrica.Validacion.Modo = ModoValidacion.Aprueba;

        await fabrica.ProcesarTodoAsync();

        Assert.Equal("APROBADO", await Estado(cliente, id));
    }

    // ── RNF-05 y RN-13: agotar intentos ──

    /// <summary>
    /// La prueba mas importante del hito.
    ///
    /// La autoridad recibe el documento y no contesta. El sistema reintenta,
    /// agota los intentos y marca el documento FALLIDO. Pero FALLIDO no
    /// significa "no llego": significa "no se sabe". El historial tiene que
    /// decirlo, porque de ahi depende que alguien emita o no un reemplazo
    /// que podria duplicar una factura ya presentada (RN-13).
    /// </summary>
    [Fact]
    public async Task Sin_respuesta_hasta_agotar_intentos_el_documento_queda_fallido()
    {
        fabrica.Validacion.Reiniciar();
        fabrica.Validacion.Modo = ModoValidacion.SinRespuesta;

        var (cliente, id) = await EmitirConServicioCaido();

        await fabrica.ProcesarTodoAsync();

        var documento = await Consultar(cliente, id);

        Assert.Equal("FALLIDO", documento.GetProperty("estado").GetString());

        // La autoridad SI recibio el documento, aunque el emisor no lo sepa.
        Assert.NotEmpty(fabrica.Validacion.Recibidos);

        // Y el historial deja constancia de esa incertidumbre.
        var historial = await LeerJson(
            await cliente.GetAsync($"{RutaDocumentos}/{id}/historial"));

        var ultima = historial.EnumerateArray().Last();

        Assert.Equal("FALLIDO", ultima.GetProperty("estadoNuevo").GetString());
        Assert.Contains(
            "RESULTADO DESCONOCIDO",
            ultima.GetProperty("detalle").GetString()!);
    }

    /// <summary>
    /// Un error definitivo no se reintenta. El servicio entendio y dijo que
    /// no: insistir solo gastaria intentos.
    /// </summary>
    [Fact]
    public async Task Un_error_definitivo_no_se_reintenta()
    {
        fabrica.Validacion.Reiniciar();
        fabrica.Validacion.Modo = ModoValidacion.ErrorDefinitivo;

        var (cliente, id) = await EmitirConServicioCaido();

        await fabrica.ProcesarTodoAsync();

        Assert.Equal("FALLIDO", await Estado(cliente, id));

        // Un solo intento, no tres.
        Assert.Equal(1, fabrica.Validacion.TransmisionesIntentadas);
    }

    // ── RNF-05: consultar hasta tener veredicto ──

    [Fact]
    public async Task Se_consulta_repetidamente_hasta_que_hay_veredicto()
    {
        fabrica.Validacion.Reiniciar();
        fabrica.Validacion.ConsultasAntesDelVeredicto = 2;

        var (cliente, id) = await Emitir();

        fabrica.Validacion.ConsultasAntesDelVeredicto = 2;

        await fabrica.ProcesarTodoAsync();

        Assert.Equal("APROBADO", await Estado(cliente, id));

        // Dos consultas sin veredicto mas la que lo trajo.
        Assert.True(fabrica.Validacion.ConsultasRealizadas >= 3);
    }
}
