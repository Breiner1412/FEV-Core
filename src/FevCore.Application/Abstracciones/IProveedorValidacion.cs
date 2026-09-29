using FevCore.Domain.Documentos;

namespace FevCore.Application.Abstracciones;

/// <summary>
/// El servicio de validacion de la autoridad (RNF-02, ADR-0007).
///
/// Existe como abstraccion para que cambiar del simulador al servicio real
/// sea escribir otra implementacion, no tocar la logica de emision. El
/// protocolo verdadero es SOAP; el simulador habla REST. Nada de eso llega
/// hasta aqui.
/// </summary>
public interface IProveedorValidacion
{
    /// <summary>Entrega el documento firmado (RF-18).</summary>
    Task<ResultadoEnvio> TransmitirAsync(
        string numeroDocumento,
        string xmlFirmado,
        CancellationToken cancelacion = default);

    /// <summary>Consulta el veredicto de un documento ya entregado (RF-19).</summary>
    Task<ResultadoConsulta> ConsultarAsync(
        string identificadorSeguimiento,
        CancellationToken cancelacion = default);
}

public sealed record ResultadoEnvio(
    ResultadoTransmision Resultado,
    string? IdentificadorSeguimiento = null,
    string? RespuestaCruda = null);

public enum VeredictoAutoridad
{
    /// <summary>Aun no hay veredicto. Hay que volver a preguntar.</summary>
    EnProceso,

    Aprobado,
    Rechazado,

    /// <summary>No se pudo consultar. No dice nada sobre el documento.</summary>
    NoDisponible
}

public sealed record ResultadoConsulta(
    VeredictoAutoridad Veredicto,
    IReadOnlyList<string> Errores,
    string? RespuestaCruda = null)
{
    public static ResultadoConsulta NoDisponible(string? respuesta = null) =>
        new(VeredictoAutoridad.NoDisponible, [], respuesta);
}
