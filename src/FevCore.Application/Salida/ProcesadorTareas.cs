using FevCore.Application.Abstracciones;
using FevCore.Application.Documentos;
using FevCore.Domain.Documentos;
using FevCore.Domain.Salida;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FevCore.Application.Salida;

/// <summary>
/// Procesa una tarea de la bandeja de salida (ADR-0006).
///
/// Vive aparte del trabajador que lo invoca para poder llamarlo a mano: las
/// pruebas que verifican el ciclo completo necesitan decidir CUANDO se
/// procesa, y un bucle en segundo plano no se deja gobernar.
/// </summary>
public sealed class ProcesadorTareas(
    IRepositorioTareas tareas,
    IRepositorioDocumentos documentos,
    GenerarXmlHandler generador,
    FirmarDocumentoHandler firmador,
    IProveedorValidacion validacion,
    IOptions<OpcionesSalida> opciones,
    TimeProvider reloj,
    ILogger<ProcesadorTareas> registrador)
{
    private readonly OpcionesSalida _opciones = opciones.Value;

    /// <summary>
    /// Toma una tarea y la procesa. Devuelve falso si no habia ninguna.
    /// </summary>
    public async Task<bool> ProcesarUnaAsync(CancellationToken cancelacion = default)
    {
        var momento = reloj.GetUtcNow();

        var tarea = await tareas.TomarSiguienteAsync(
            momento, _opciones.TiempoDeAbandono, cancelacion);

        if (tarea is null)
        {
            return false;
        }

        // Fuera del try a proposito: el catch necesita el documento.
        var documento = await documentos.ObtenerPorIdAsync(tarea.DocumentoId, cancelacion);

        try
        {
            await ProcesarAsync(tarea, documento, cancelacion);
        }
        catch (Exception error)
        {
            // Un fallo inesperado NO puede dejar la tarea tomada para
            // siempre: se devuelve a la bandeja o se agota, segun queden
            // intentos.
            //
            // Y al agotarse tiene que llevarse el documento a FALLIDO. Sin el
            // documento, la tarea se cerraba y el documento se quedaba en
            // EN_PROCESO o TRANSMITIDO, sin trabajo pendiente y sin estado
            // final: exactamente el estado indeterminado que CE-04 prohibe.
            registrador.LogError(
                error, "Fallo inesperado procesando la tarea {Tarea}.", tarea.Id);

            await RendirseOReprogramar(tarea, error.Message, cancelacion, documento);
        }

        return true;
    }

    private async Task ProcesarAsync(
        TareaSalida tarea,
        Documento? documento,
        CancellationToken cancelacion)
    {
        if (documento is null)
        {
            tarea.Agotar("El documento ya no existe.", reloj.GetUtcNow());
            await tareas.GuardarCambiosAsync(cancelacion);
            return;
        }

        // Un documento que ya llego a un estado final no tiene mas trabajo
        // pendiente. Puede pasar si se proceso dos veces por una
        // recuperacion: la tarea simplemente se cierra (RN-11).
        if (MaquinaEstados.EsTerminal(documento.Estado))
        {
            tarea.Completar(reloj.GetUtcNow());
            await tareas.GuardarCambiosAsync(cancelacion);
            return;
        }

        if (tarea.Tipo == TipoTarea.Emitir)
        {
            await EmitirAsync(tarea, documento, cancelacion);
        }
        else
        {
            await ConsultarAsync(tarea, documento, cancelacion);
        }
    }

    // ── Generar, firmar y transmitir ──

    private async Task EmitirAsync(
        TareaSalida tarea,
        Documento documento,
        CancellationToken cancelacion)
    {
        // Cada paso comprueba si ya esta hecho. Un reintento entra por el
        // principio y solo ejecuta lo que falta: generar y firmar son
        // operaciones de una sola vez, y sus metodos de dominio lo imponen.
        if (documento.Xml is null)
        {
            await generador.EjecutarAsync(documento.Id, cancelacion);
        }

        if (documento.XmlFirmado is null)
        {
            await firmador.EjecutarAsync(documento.Id, cancelacion);
        }

        var envio = await validacion.TransmitirAsync(
            documento.NumeroCompleto, documento.XmlFirmado!, cancelacion);

        var momento = reloj.GetUtcNow();

        documento.RegistrarTransmision(
            envio.Resultado,
            momento,
            envio.IdentificadorSeguimiento,
            envio.RespuestaCruda);

        switch (envio.Resultado)
        {
            case ResultadoTransmision.Aceptada:
                // La entrega termino. Consultar el veredicto es otro trabajo,
                // y va en su propia tarea: asi el trabajador no se queda
                // esperando a que la autoridad se decida.
                tarea.Completar(momento);

                await tareas.AgregarAsync(
                    TareaSalida.Crear(documento.Id, TipoTarea.Consultar, momento),
                    cancelacion);

                registrador.LogInformation(
                    "Documento {Numero} transmitido. Seguimiento {Seguimiento}.",
                    documento.NumeroCompleto, envio.IdentificadorSeguimiento);
                break;

            case ResultadoTransmision.ErrorDefinitivo:
                // El servicio entendio y dijo que no. Reintentar es perder el
                // tiempo: el problema esta en el documento.
                documento.RegistrarFallo(
                    "El servicio de validacion rechazo la entrega de forma definitiva.",
                    momento);

                tarea.Agotar(envio.RespuestaCruda ?? "Error definitivo.", momento);
                break;

            default:
                await RendirseOReprogramar(
                    tarea,
                    envio.RespuestaCruda ?? envio.Resultado.ToString(),
                    cancelacion,
                    documento);
                break;
        }

        await tareas.GuardarCambiosAsync(cancelacion);
    }

    // ── Preguntar por el veredicto ──

    private async Task ConsultarAsync(
        TareaSalida tarea,
        Documento documento,
        CancellationToken cancelacion)
    {
        var seguimiento = documento.IdentificadorSeguimiento;

        if (string.IsNullOrWhiteSpace(seguimiento))
        {
            // No deberia ocurrir: la tarea de consulta solo se crea tras una
            // transmision aceptada, y una aceptada siempre trae seguimiento.
            tarea.Agotar("No hay identificador de seguimiento que consultar.", reloj.GetUtcNow());
            await tareas.GuardarCambiosAsync(cancelacion);
            return;
        }

        var consulta = await validacion.ConsultarAsync(seguimiento, cancelacion);
        var momento = reloj.GetUtcNow();

        switch (consulta.Veredicto)
        {
            case VeredictoAutoridad.Aprobado:
                documento.RegistrarAprobacion(momento);
                tarea.Completar(momento);

                registrador.LogInformation(
                    "Documento {Numero} aprobado.", documento.NumeroCompleto);
                break;

            case VeredictoAutoridad.Rechazado:
                documento.RegistrarRechazo(consulta.Errores, momento);
                tarea.Completar(momento);

                registrador.LogInformation(
                    "Documento {Numero} rechazado con {Cuantos} error(es).",
                    documento.NumeroCompleto, consulta.Errores.Count);
                break;

            default:
                // EnProceso o NoDisponible: todavia no hay respuesta. Se
                // vuelve a preguntar mas tarde, cada vez con mas espera.
                await RendirseOReprogramar(
                    tarea,
                    $"Sin veredicto todavia ({consulta.Veredicto}).",
                    cancelacion,
                    documento);
                break;
        }

        await tareas.GuardarCambiosAsync(cancelacion);
    }

    /// <summary>
    /// Devuelve la tarea a la bandeja, o se rinde si ya no quedan intentos.
    ///
    /// Rendirse significa marcar el documento como FALLIDO, que NO quiere
    /// decir "no llego" sino "no se sabe" (RN-13). Por eso el documento
    /// conserva todas sus transmisiones: son la unica forma de distinguir un
    /// servicio que nunca contesto de uno que recibio el documento y se
    /// quedo callado.
    /// </summary>
    private async Task RendirseOReprogramar(
        TareaSalida tarea,
        string error,
        CancellationToken cancelacion,
        Documento? documento = null)
    {
        var momento = reloj.GetUtcNow();

        if (tarea.AgotoIntentos(_opciones.MaximoIntentos))
        {
            tarea.Agotar(error, momento);

            if (documento is not null &&
                MaquinaEstados.Permite(documento.Estado, EstadoDocumento.Fallido))
            {
                documento.RegistrarFallo(
                    $"Se agotaron los {_opciones.MaximoIntentos} intentos.", momento);
            }
            else if (documento is not null && !MaquinaEstados.EsTerminal(documento.Estado))
            {
                // La seccion 6.2 no tiene transicion de RECIBIDO a FALLIDO, asi
                // que un documento que fallo antes de generar su XML se queda
                // donde esta. No se oculta: queda escrito para quien revise.
                registrador.LogError(
                    "Documento {Documento} sin estado final: se agotaron los intentos " +
                    "en {Estado}, desde donde la maquina de estados no permite FALLIDO.",
                    documento.Id, documento.Estado);
            }

            registrador.LogWarning(
                "Tarea {Tarea} agotada tras {Intentos} intentos: {Error}",
                tarea.Id, tarea.Intentos, error);
        }
        else
        {
            tarea.Reprogramar(error, momento, _opciones.EsperaMaxima);

            registrador.LogInformation(
                "Tarea {Tarea} reprogramada para {Momento} (intento {Intento}).",
                tarea.Id, tarea.ProximoIntentoEn, tarea.Intentos);
        }

        await tareas.GuardarCambiosAsync(cancelacion);
    }
}
