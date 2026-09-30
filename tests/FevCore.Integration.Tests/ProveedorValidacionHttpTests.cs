using System.Net;
using System.Text;
using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;
using FevCore.Infrastructure.Validacion;
using Microsoft.Extensions.Logging.Abstractions;

namespace FevCore.Integration.Tests;

/// <summary>
/// Como clasifica los fallos el proveedor HTTP (RF-18, RF-19, RN-13).
///
/// Es la pieza de H7 que mas merece pruebas propias y la mas dificil de
/// probar contra un servicio de verdad: haria falta un servidor que se
/// caiga a voluntad, que devuelva un 429 cuando se le pide, y que reciba
/// una peticion y no conteste nunca. Aqui ese servidor es un manejador
/// falso de dos lineas.
///
/// Lo que se prueba no es que sepa hablar HTTP, sino que sepa TRADUCIR:
/// cada forma de fallar tiene una consecuencia distinta aguas abajo —
/// reintentar, rendirse, o marcar el documento como resultado desconocido—
/// y confundir dos de ellas es el error caro.
///
/// No hereda la fabrica con base de datos: no la necesita, y arrastrar un
/// contenedor de PostgreSQL para probar un switch seria pagar veinte
/// segundos por nada.
/// </summary>
public sealed class ProveedorValidacionHttpTests
{
    /// <summary>
    /// Contesta lo que se le diga, sin red de por medio.
    /// </summary>
    private sealed class ManejadorFalso(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud, CancellationToken cancelacion) =>
            Task.FromResult(responder(solicitud));
    }

    /// <summary>Falla antes de contestar, con la excepcion que se le pida.</summary>
    private sealed class ManejadorQueFalla(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud, CancellationToken cancelacion) =>
            Task.FromException<HttpResponseMessage>(error);
    }

    private static ProveedorValidacionHttp Crear(HttpMessageHandler manejador) =>
        new(new HttpClient(manejador) { BaseAddress = new Uri("http://validacion.pruebas") },
            NullLogger<ProveedorValidacionHttp>.Instance);

    private static ProveedorValidacionHttp ConRespuesta(HttpStatusCode codigo, string cuerpo = "{}") =>
        Crear(new ManejadorFalso(_ => new HttpResponseMessage(codigo)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        }));

    // ── Transmision ──

    [Fact]
    public async Task Una_respuesta_aceptada_con_seguimiento_es_aceptada()
    {
        var proveedor = ConRespuesta(
            HttpStatusCode.Accepted,
            """{"identificadorSeguimiento":"SEG-001"}""");

        var resultado = await proveedor.TransmitirAsync("SETP990000001", "<xml/>");

        Assert.Equal(ResultadoTransmision.Aceptada, resultado.Resultado);
        Assert.Equal("SEG-001", resultado.IdentificadorSeguimiento);
    }

    [Fact]
    public async Task Una_respuesta_aceptada_sin_seguimiento_se_trata_como_transitoria()
    {
        var proveedor = ConRespuesta(HttpStatusCode.Accepted, "{}");

        var resultado = await proveedor.TransmitirAsync("SETP990000001", "<xml/>");

        // Aceptar sin devolver con que consultar despues deja al documento
        // sin forma de averiguar su veredicto. Darlo por bueno seria
        // perderlo: mejor reintentar.
        Assert.Equal(ResultadoTransmision.ErrorTransitorio, resultado.Resultado);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Los_fallos_del_servicio_son_transitorios(HttpStatusCode codigo)
    {
        var proveedor = ConRespuesta(codigo);

        var resultado = await proveedor.TransmitirAsync("SETP990000001", "<xml/>");

        // El problema es del servicio o del momento, no del documento.
        // Reintentar tiene sentido.
        Assert.Equal(ResultadoTransmision.ErrorTransitorio, resultado.Resultado);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task Los_rechazos_del_servicio_son_definitivos(HttpStatusCode codigo)
    {
        var proveedor = ConRespuesta(codigo);

        var resultado = await proveedor.TransmitirAsync("SETP990000001", "<xml/>");

        // El servicio entendio la peticion y dijo que no. Insistir solo
        // gastaria intentos: el documento no va a gustarle mas por repetirlo.
        Assert.Equal(ResultadoTransmision.ErrorDefinitivo, resultado.Resultado);
    }

    [Fact]
    public async Task Agotar_el_tiempo_de_espera_deja_el_resultado_desconocido()
    {
        var proveedor = Crear(new ManejadorQueFalla(new TaskCanceledException()));

        var resultado = await proveedor.TransmitirAsync("SETP990000001", "<xml/>");

        // Este es el caso de RN-13 y la razon de que SinRespuesta exista
        // como resultado aparte. La peticion salio: puede que el servicio la
        // recibiera, la procesara, y solo se perdiera la respuesta.
        // Reintentar a ciegas arriesga un duplicado.
        Assert.Equal(ResultadoTransmision.SinRespuesta, resultado.Resultado);
    }

    [Fact]
    public async Task No_poder_conectar_es_transitorio()
    {
        var proveedor = Crear(new ManejadorQueFalla(
            new HttpRequestException("No such host is known.")));

        var resultado = await proveedor.TransmitirAsync("SETP990000001", "<xml/>");

        // Contraste deliberado con la prueba anterior: ambas "fallan", pero
        // aqui SI se sabe que no llego. El estado del documento es conocido,
        // asi que reintentar es seguro.
        Assert.Equal(ResultadoTransmision.ErrorTransitorio, resultado.Resultado);
    }

    // ── Consulta ──

    [Fact]
    public async Task Un_veredicto_aprobado_se_lee_como_aprobado()
    {
        var proveedor = ConRespuesta(HttpStatusCode.OK, """{"veredicto":"APROBADO"}""");

        var resultado = await proveedor.ConsultarAsync("SEG-001");

        Assert.Equal(VeredictoAutoridad.Aprobado, resultado.Veredicto);
        Assert.Empty(resultado.Errores);
    }

    [Fact]
    public async Task Un_veredicto_rechazado_conserva_los_errores()
    {
        var proveedor = ConRespuesta(
            HttpStatusCode.OK,
            """{"veredicto":"RECHAZADO","errores":["FAD06: NIT invalido","FAJ22: CUFE no coincide"]}""");

        var resultado = await proveedor.ConsultarAsync("SEG-001");

        Assert.Equal(VeredictoAutoridad.Rechazado, resultado.Veredicto);

        // Los errores son lo unico que el emisor puede usar para corregir.
        // Un rechazo sin motivo deja al usuario sin nada que hacer.
        Assert.Equal(2, resultado.Errores.Count);
        Assert.Contains("FAD06: NIT invalido", resultado.Errores);
    }

    /// <summary>
    /// RF-21: el dominio no acepta un rechazo sin errores, asi que el
    /// adaptador nunca debe entregarle uno. Da igual que la autoridad omita
    /// el campo, lo mande vacio o lo mande con textos en blanco.
    /// </summary>
    [Theory]
    [InlineData("""{"veredicto":"RECHAZADO"}""")]
    [InlineData("""{"veredicto":"RECHAZADO","errores":[]}""")]
    [InlineData("""{"veredicto":"RECHAZADO","errores":["  "]}""")]
    public async Task Un_rechazo_sin_detalle_no_deja_la_lista_vacia(string cuerpo)
    {
        var proveedor = ConRespuesta(HttpStatusCode.OK, cuerpo);

        var resultado = await proveedor.ConsultarAsync("SEG-001");

        Assert.Equal(VeredictoAutoridad.Rechazado, resultado.Veredicto);
        Assert.Contains(resultado.Errores, e => !string.IsNullOrWhiteSpace(e));
    }

    [Fact]
    public async Task Un_documento_en_proceso_no_es_ni_aprobado_ni_rechazado()
    {
        var proveedor = ConRespuesta(HttpStatusCode.OK, """{"veredicto":"EN_PROCESO"}""");

        var resultado = await proveedor.ConsultarAsync("SEG-001");

        Assert.Equal(VeredictoAutoridad.EnProceso, resultado.Veredicto);
    }

    [Fact]
    public async Task Un_veredicto_desconocido_no_se_interpreta()
    {
        var proveedor = ConRespuesta(HttpStatusCode.OK, """{"veredicto":"VAYA_USTED_A_SABER"}""");

        var resultado = await proveedor.ConsultarAsync("SEG-001");

        // Ante algo que no se entiende, no disponible. Adivinar seria
        // decidir el destino de un documento con informacion inventada.
        Assert.Equal(VeredictoAutoridad.NoDisponible, resultado.Veredicto);
    }

    [Fact]
    public async Task No_poder_consultar_no_dice_nada_del_documento()
    {
        var proveedor = Crear(new ManejadorQueFalla(new HttpRequestException("caido")));

        var resultado = await proveedor.ConsultarAsync("SEG-001");

        // Consultar es una lectura: que falle no cambia el estado del
        // documento, solo retrasa saberlo.
        Assert.Equal(VeredictoAutoridad.NoDisponible, resultado.Veredicto);
    }
}
