namespace FevCore.Api.Contratos;

/// <summary>
/// Respuesta del endpoint de salud.
/// </summary>
/// <param name="Estado">Siempre "ok" si el servicio responde.</param>
/// <param name="Momento">Marca de tiempo del servidor, en UTC.</param>
public sealed record RespuestaSalud(string Estado, DateTimeOffset Momento);
