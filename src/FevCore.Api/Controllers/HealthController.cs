using Microsoft.AspNetCore.Mvc;
using FevCore.Api.Contratos;

namespace FevCore.Api.Controllers;

/// <summary>
/// Confirma que el servicio esta vivo y respondiendo.
/// No consulta base de datos ni servicios externos: si lo hiciera,
/// una caida de la base de datos haria ver el servicio como muerto
/// cuando en realidad esta en pie.
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Obtener()
    {
        var respuesta = new RespuestaSalud("ok", DateTimeOffset.UtcNow);
        return Ok(respuesta);
    }
}
