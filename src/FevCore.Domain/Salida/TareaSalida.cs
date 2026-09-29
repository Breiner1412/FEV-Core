using FevCore.Domain.Comun;

namespace FevCore.Domain.Salida;

public enum TipoTarea
{
    /// <summary>Generar el XML, firmarlo y transmitirlo.</summary>
    Emitir,

    /// <summary>Consultar el veredicto de un documento ya transmitido.</summary>
    Consultar
}

/// <summary>
/// Trabajo pendiente sobre un documento, guardado en la misma base y en la
/// misma transaccion que el documento (ADR-0006).
///
/// Esa simultaneidad es todo el punto de la bandeja de salida. Si el
/// documento se guardara y el trabajo se encolara aparte, existiria un
/// instante en que uno de los dos podria perderse: documento sin procesar,
/// o trabajo sobre un documento que no llego a existir. Con los dos en la
/// misma transaccion, o entran ambos o no entra ninguno.
/// </summary>
public sealed class TareaSalida
{
    public Guid Id { get; }
    public Guid DocumentoId { get; }
    public TipoTarea Tipo { get; }

    public DateTimeOffset CreadaEn { get; }

    /// <summary>Cuantas veces se ha intentado.</summary>
    public int Intentos { get; private set; }

    /// <summary>
    /// No se toca antes de este momento. Es donde vive la espera creciente
    /// de RNF-05: cada fallo empuja esta fecha mas lejos.
    /// </summary>
    public DateTimeOffset ProximoIntentoEn { get; private set; }

    /// <summary>
    /// Cuando un trabajador la tomo. Nulo si esta libre.
    ///
    /// Hace falta porque el trabajo ocurre FUERA de la transaccion que toma
    /// la tarea: mantener una transaccion abierta mientras se espera a un
    /// servicio externo bloquearia una conexion de base de datos durante
    /// segundos. Al soltarla, el bloqueo de fila desaparece, y esta marca es
    /// lo que impide que otro trabajador la agarre mientras tanto.
    ///
    /// Tambien es lo que permite recuperar tareas de procesos que murieron:
    /// una marca vieja significa que nadie la esta trabajando ya (RNF-04).
    /// </summary>
    public DateTimeOffset? TomadaEn { get; private set; }

    public DateTimeOffset? CompletadaEn { get; private set; }

    /// <summary>Por que fallo el ultimo intento. Para diagnostico.</summary>
    public string? UltimoError { get; private set; }

    public bool Completada => CompletadaEn is not null;

    /// <summary>Requerido por Entity Framework.</summary>
    private TareaSalida() { }

    private TareaSalida(Guid id, Guid documentoId, TipoTarea tipo, DateTimeOffset momento)
    {
        Id = id;
        DocumentoId = documentoId;
        Tipo = tipo;
        CreadaEn = momento;
        ProximoIntentoEn = momento;
    }

    public static TareaSalida Crear(Guid documentoId, TipoTarea tipo, DateTimeOffset momento)
    {
        if (documentoId == Guid.Empty)
        {
            throw new ExcepcionDominio(
                "TAREA_SIN_DOCUMENTO",
                "Una tarea de la bandeja de salida debe referirse a un documento.");
        }

        return new TareaSalida(Guid.CreateVersion7(), documentoId, tipo, momento);
    }

    /// <summary>Marca la tarea como tomada por un trabajador.</summary>
    public void Tomar(DateTimeOffset momento)
    {
        if (Completada)
        {
            throw new ExcepcionDominio(
                "TAREA_COMPLETADA",
                "Una tarea completada no se vuelve a tomar.");
        }

        TomadaEn = momento;
        Intentos++;
    }

    public void Completar(DateTimeOffset momento)
    {
        CompletadaEn = momento;
        TomadaEn = null;
        UltimoError = null;
    }

    /// <summary>
    /// Devuelve la tarea a la bandeja para intentarlo mas tarde.
    ///
    /// La espera crece exponencialmente con los intentos y se recorta a un
    /// techo. Sin techo, el intento quince esperaria nueve horas; sin
    /// crecimiento, un servicio caido recibiria una peticion por segundo de
    /// cada documento pendiente, que es como se tumba un servicio que ya
    /// estaba teniendo un mal dia (RNF-05).
    /// </summary>
    public void Reprogramar(string error, DateTimeOffset momento, TimeSpan esperaMaxima)
    {
        var segundos = Math.Pow(2, Math.Min(Intentos, 16));
        var espera = TimeSpan.FromSeconds(segundos);

        if (espera > esperaMaxima)
        {
            espera = esperaMaxima;
        }

        ProximoIntentoEn = momento + espera;
        TomadaEn = null;
        UltimoError = Recortar(error);
    }

    /// <summary>
    /// Se agotaron los intentos. La tarea deja de reintentarse, y el
    /// documento pasa a FALLIDO, que significa resultado desconocido y
    /// exige revision manual (RN-13).
    /// </summary>
    public void Agotar(string error, DateTimeOffset momento)
    {
        CompletadaEn = momento;
        TomadaEn = null;
        UltimoError = Recortar(error);
    }

    public bool AgotoIntentos(int maximo) => Intentos >= maximo;

    /// <summary>
    /// Una tarea tomada hace mas tiempo del permitido se considera
    /// abandonada: el proceso que la tenia murio sin soltarla.
    /// </summary>
    public bool EstaAbandonada(DateTimeOffset momento, TimeSpan tiempoMaximo) =>
        TomadaEn is not null && momento - TomadaEn.Value > tiempoMaximo;

    private static string Recortar(string texto) =>
        texto.Length > 1000 ? texto[..1000] : texto;
}
