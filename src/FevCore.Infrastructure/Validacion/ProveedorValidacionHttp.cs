using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;
using Microsoft.Extensions.Logging;

namespace FevCore.Infrastructure.Validacion;

/// <summary>
/// Habla con el servicio de validacion por HTTP (RF-18, RF-19).
///
/// Todo el valor de esta clase esta en como CLASIFICA los fallos. Un fallo
/// no es solo un fallo: la diferencia entre "no llego", "llego y lo
/// rechazaron" y "no se sabe si llego" decide si el sistema reintenta, se
/// rinde, o marca el documento para revision manual (RN-13).
/// </summary>
public sealed class ProveedorValidacionHttp(
    HttpClient cliente,
    ILogger<ProveedorValidacionHttp> registrador) : IProveedorValidacion
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    public async Task<ResultadoEnvio> TransmitirAsync(
        string numeroDocumento,
        string xmlFirmado,
        CancellationToken cancelacion = default)
    {
        try
        {
            var respuesta = await cliente.PostAsJsonAsync(
                "/validacion/documentos",
                new { numeroDocumento, xml = xmlFirmado },
                Json,
                cancelacion);

            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);

            if (respuesta.StatusCode == HttpStatusCode.Accepted ||
                respuesta.StatusCode == HttpStatusCode.OK)
            {
                var aceptada = JsonSerializer.Deserialize<CuerpoTransmision>(cuerpo, Json);

                return string.IsNullOrWhiteSpace(aceptada?.IdentificadorSeguimiento)
                    ? new ResultadoEnvio(
                        ResultadoTransmision.ErrorTransitorio,
                        RespuestaCruda: "Aceptada sin identificador de seguimiento.")
                    : new ResultadoEnvio(
                        ResultadoTransmision.Aceptada,
                        aceptada.IdentificadorSeguimiento,
                        cuerpo);
            }

            return new ResultadoEnvio(Clasificar(respuesta.StatusCode), RespuestaCruda: cuerpo);
        }
        catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            // Se agoto el tiempo de espera DESPUES de enviar la peticion.
            //
            // Este es el caso que RN-13 modela. La peticion salio; puede que
            // el servicio la recibiera, la procesara y solo se perdiera la
            // respuesta. No es un error: es no saber.
            registrador.LogWarning(
                "Sin respuesta al transmitir {Numero}. Resultado desconocido.",
                numeroDocumento);

            return new ResultadoEnvio(
                ResultadoTransmision.SinRespuesta,
                RespuestaCruda: "Tiempo de espera agotado sin respuesta.");
        }
        catch (HttpRequestException error)
        {
            // No se pudo ni conectar. Aqui SI se sabe: no llego.
            //
            // La distincion con el caso de arriba es todo el punto. Ambos
            // "fallan", pero uno deja al documento en un estado conocido y el
            // otro no.
            registrador.LogWarning(
                error, "No se pudo contactar al servicio al transmitir {Numero}.",
                numeroDocumento);

            return new ResultadoEnvio(
                ResultadoTransmision.ErrorTransitorio,
                RespuestaCruda: error.Message);
        }
    }

    public async Task<ResultadoConsulta> ConsultarAsync(
        string identificadorSeguimiento,
        CancellationToken cancelacion = default)
    {
        try
        {
            var respuesta = await cliente.GetAsync(
                $"/validacion/documentos/{Uri.EscapeDataString(identificadorSeguimiento)}",
                cancelacion);

            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);

            if (!respuesta.IsSuccessStatusCode)
            {
                return ResultadoConsulta.NoDisponible(cuerpo);
            }

            var veredicto = JsonSerializer.Deserialize<CuerpoVeredicto>(cuerpo, Json);

            return veredicto?.Veredicto switch
            {
                "APROBADO" => new ResultadoConsulta(VeredictoAutoridad.Aprobado, [], cuerpo),
                "RECHAZADO" => new ResultadoConsulta(
                    VeredictoAutoridad.Rechazado,
                    veredicto.Errores ?? ["La autoridad rechazo el documento sin detallar."],
                    cuerpo),
                "EN_PROCESO" => new ResultadoConsulta(VeredictoAutoridad.EnProceso, [], cuerpo),
                _ => ResultadoConsulta.NoDisponible(cuerpo)
            };
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            // Consultar es una lectura: si falla, no cambia nada del estado
            // del documento. Se vuelve a preguntar mas tarde.
            registrador.LogWarning(
                error, "No se pudo consultar el veredicto de {Seguimiento}.",
                identificadorSeguimiento);

            return ResultadoConsulta.NoDisponible(error.Message);
        }
    }

    /// <summary>
    /// Que significa cada codigo de estado.
    ///
    /// Transitorio quiere decir "vuelve a intentarlo y puede funcionar".
    /// Definitivo quiere decir "reintentar es perder el tiempo": el problema
    /// esta en el documento, no en el momento.
    /// </summary>
    private static ResultadoTransmision Clasificar(HttpStatusCode codigo) => codigo switch
    {
        HttpStatusCode.RequestTimeout => ResultadoTransmision.ErrorTransitorio,
        HttpStatusCode.TooManyRequests => ResultadoTransmision.ErrorTransitorio,

        // Toda la familia 5xx: el problema es del servicio, no del documento.
        >= HttpStatusCode.InternalServerError => ResultadoTransmision.ErrorTransitorio,

        // 4xx: el servicio entendio y dijo que no. El documento no va a
        // gustarle mas por insistir.
        >= HttpStatusCode.BadRequest => ResultadoTransmision.ErrorDefinitivo,

        _ => ResultadoTransmision.ErrorTransitorio
    };

    private sealed record CuerpoTransmision(string? IdentificadorSeguimiento);
    private sealed record CuerpoVeredicto(string? Veredicto, string[]? Errores);
}
