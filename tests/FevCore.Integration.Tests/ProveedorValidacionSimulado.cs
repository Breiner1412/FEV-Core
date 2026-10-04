using System.Collections.Concurrent;
using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;

namespace FevCore.Integration.Tests;

/// <summary>
/// Como se comporta la autoridad en una prueba.
/// </summary>
public enum ModoValidacion
{
    Aprueba,
    Rechaza,

    /// <summary>Servicio caido: error transitorio. Escenario de RNF-04.</summary>
    Caido,

    /// <summary>Recibe y no contesta. Escenario de RN-13.</summary>
    SinRespuesta,

    /// <summary>Rechaza la entrega misma. Reintentar no serviria.</summary>
    ErrorDefinitivo,

    /// <summary>
    /// Lanza una excepcion que nadie previo al transmitir. Simula un error
    /// de programacion o de un adaptador, no una respuesta de la autoridad.
    /// </summary>
    FallaAlTransmitir,

    /// <summary>Acepta la entrega y lanza una excepcion no prevista al consultar.</summary>
    FallaAlConsultar,

    /// <summary>
    /// Acepta con un identificador de seguimiento mas largo que su columna.
    /// Guardar la transmision falla siempre, en cada intento.
    /// </summary>
    SeguimientoDemasiadoLargo
}

/// <summary>
/// Doble del servicio de validacion, gobernable desde la prueba.
///
/// Reproduce los mismos modos que FevCore.DianSimulator, pero sin red de por
/// medio: la prueba decide exactamente que devuelve y cuando cambia de
/// opinion. El simulador de verdad sigue existiendo y se ejercita en el
/// recorrido del README y en docker-compose; lo que se prueba aqui es la
/// ORQUESTACION, no el protocolo.
///
/// La traduccion de protocolo —que es lo unico que aporta
/// ProveedorValidacionHttp— tiene sus propias pruebas.
/// </summary>
public sealed class ProveedorValidacionSimulado : IProveedorValidacion
{
    public ModoValidacion Modo { get; set; } = ModoValidacion.Aprueba;

    /// <summary>
    /// Cuantas consultas responden "en proceso" antes del veredicto. Sirve
    /// para ejercitar la consulta repetida con espera creciente (RNF-05).
    /// </summary>
    public int ConsultasAntesDelVeredicto { get; set; }

    /// <summary>Los numeros de documento que llegaron, en orden.</summary>
    public ConcurrentQueue<string> Recibidos { get; } = new();

    public int TransmisionesIntentadas { get; private set; }
    public int ConsultasRealizadas { get; private set; }

    private readonly ConcurrentDictionary<string, int> _consultasPorSeguimiento = new();

    public void Reiniciar()
    {
        Modo = ModoValidacion.Aprueba;
        ConsultasAntesDelVeredicto = 0;
        TransmisionesIntentadas = 0;
        ConsultasRealizadas = 0;
        Recibidos.Clear();
        _consultasPorSeguimiento.Clear();
    }

    public Task<ResultadoEnvio> TransmitirAsync(
        string numeroDocumento,
        string xmlFirmado,
        CancellationToken cancelacion = default)
    {
        TransmisionesIntentadas++;

        if (Modo == ModoValidacion.FallaAlTransmitir)
        {
            throw new InvalidOperationException("Fallo no previsto al transmitir.");
        }

        return Task.FromResult(Modo switch
        {
            ModoValidacion.Caido => new ResultadoEnvio(
                ResultadoTransmision.ErrorTransitorio,
                RespuestaCruda: "El servicio no responde (503)."),

            ModoValidacion.SinRespuesta => Registrar(numeroDocumento, new ResultadoEnvio(
                ResultadoTransmision.SinRespuesta,
                RespuestaCruda: "Tiempo de espera agotado sin respuesta.")),

            ModoValidacion.SeguimientoDemasiadoLargo => Registrar(numeroDocumento, new ResultadoEnvio(
                ResultadoTransmision.Aceptada,
                new string('X', 150),
                "Aceptado.")),

            ModoValidacion.ErrorDefinitivo => new ResultadoEnvio(
                ResultadoTransmision.ErrorDefinitivo,
                RespuestaCruda: "El documento no cumple el formato esperado (400)."),

            _ => Registrar(numeroDocumento, new ResultadoEnvio(
                ResultadoTransmision.Aceptada,
                $"TRK-{numeroDocumento}-{TransmisionesIntentadas}",
                "Aceptado."))
        });
    }

    /// <summary>
    /// Deja constancia incluso cuando no se contesta.
    ///
    /// Es la parte que hace util este modo: la prueba puede comprobar que el
    /// documento SI llego aunque el emisor nunca se haya enterado. Esa
    /// situacion es exactamente lo que RN-13 modela.
    /// </summary>
    private ResultadoEnvio Registrar(string numero, ResultadoEnvio resultado)
    {
        Recibidos.Enqueue(numero);
        return resultado;
    }

    public Task<ResultadoConsulta> ConsultarAsync(
        string identificadorSeguimiento,
        CancellationToken cancelacion = default)
    {
        ConsultasRealizadas++;

        if (Modo == ModoValidacion.FallaAlConsultar)
        {
            throw new InvalidOperationException("Fallo no previsto al consultar.");
        }

        if (Modo == ModoValidacion.Caido || Modo == ModoValidacion.SinRespuesta)
        {
            return Task.FromResult(ResultadoConsulta.NoDisponible("El servicio no responde."));
        }

        var previas = _consultasPorSeguimiento.AddOrUpdate(
            identificadorSeguimiento, 1, (_, cuantas) => cuantas + 1);

        if (previas <= ConsultasAntesDelVeredicto)
        {
            return Task.FromResult(
                new ResultadoConsulta(VeredictoAutoridad.EnProceso, []));
        }

        return Task.FromResult(Modo == ModoValidacion.Rechaza
            ? new ResultadoConsulta(
                VeredictoAutoridad.Rechazado,
                [
                    "DIAN-REGLA-FAU14: El valor total no corresponde con la sumatoria.",
                    "DIAN-REGLA-DAJ01: El adquiriente no se encuentra registrado."
                ])
            : new ResultadoConsulta(VeredictoAutoridad.Aprobado, []));
    }
}
