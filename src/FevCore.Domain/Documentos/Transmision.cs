namespace FevCore.Domain.Documentos;

/// <summary>
/// Que paso en un intento de entregar el documento.
/// </summary>
public enum ResultadoTransmision
{
    /// <summary>El servicio la recibio y devolvio identificador de seguimiento.</summary>
    Aceptada,

    /// <summary>Fallo que puede desaparecer solo: servicio caido, red, tiempo agotado.</summary>
    ErrorTransitorio,

    /// <summary>El servicio la rechazo por su contenido. Reintentar no cambiaria nada.</summary>
    ErrorDefinitivo,

    /// <summary>
    /// NO SE SABE si llego. La peticion salio y no volvio respuesta.
    ///
    /// Es distinto de un error: un error significa que no llego. Esto
    /// significa que puede haber llegado (RN-13, INV-TRM-02).
    /// </summary>
    SinRespuesta
}

/// <summary>
/// Un intento de entregar el documento al servicio de validacion
/// (RF-18, RNF-05).
///
/// Cada intento es una fila propia y no un campo del documento. Si solo se
/// guardara "ultimo resultado", un reintento borraria la evidencia del
/// anterior, y con ella la informacion de que hubo un envio cuyo destino se
/// desconoce. Esa evidencia es justamente lo que RN-13 necesita conservar.
///
/// Es inmutable (INV-TRM-01): un intento es un hecho, no un estado.
/// </summary>
public sealed class Transmision
{
    /// <summary>
    /// El identificador de seguimiento mas largo que se puede guardar. Uno
    /// mas largo no se puede conservar, y sin conservarlo no hay forma de
    /// consultar el veredicto: quien lo recibe lo trata como si no hubiera
    /// venido (ADR-0015).
    /// </summary>
    public const int LongitudMaximaSeguimiento = 100;

    /// <summary>
    /// Posicion en la serie de intentos, empezando en 1. Identifica la
    /// transmision junto con su documento: no existe fuera de el.
    /// </summary>
    public int NumeroIntento { get; }

    public DateTimeOffset EnviadaEn { get; }

    /// <summary>Lo devuelve el servicio al aceptar. Nulo en los demas casos.</summary>
    public string? IdentificadorSeguimiento { get; }

    public ResultadoTransmision Resultado { get; }

    /// <summary>La respuesta completa, para diagnostico.</summary>
    public string? RespuestaCruda { get; }

    /// <summary>Requerido por Entity Framework.</summary>
    private Transmision() { }

    private Transmision(
        int numeroIntento,
        DateTimeOffset enviadaEn,
        string? identificadorSeguimiento,
        ResultadoTransmision resultado,
        string? respuestaCruda)
    {
        NumeroIntento = numeroIntento;
        EnviadaEn = enviadaEn;
        IdentificadorSeguimiento = identificadorSeguimiento;
        Resultado = resultado;
        RespuestaCruda = respuestaCruda;
    }

    internal static Transmision Registrar(
        int numeroIntento,
        DateTimeOffset enviadaEn,
        ResultadoTransmision resultado,
        string? identificadorSeguimiento = null,
        string? respuestaCruda = null)
    {
        if (resultado == ResultadoTransmision.Aceptada &&
            string.IsNullOrWhiteSpace(identificadorSeguimiento))
        {
            throw new Comun.ExcepcionDominio(
                "TRANSMISION_SIN_SEGUIMIENTO",
                "Una transmision aceptada debe traer identificador de seguimiento: " +
                "sin el no hay forma de consultar el veredicto.");
        }

        return new Transmision(
            numeroIntento,
            enviadaEn,
            identificadorSeguimiento,
            resultado,
            // La respuesta cruda se recorta: sirve para diagnosticar, no para
            // guardar megas de XML repetido en cada intento.
            respuestaCruda is { Length: > 2000 }
                ? respuestaCruda[..2000]
                : respuestaCruda);
    }
}
