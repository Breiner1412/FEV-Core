using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FevCore.Application.Abstracciones;
using FevCore.Domain.Integradores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FevCore.Api.Autenticacion;

/// <summary>
/// Autentica al sistema integrador por su llave de API (RF-01, RF-02, ADR-0008).
///
/// La llave viaja en la cabecera X-Api-Key. Nunca se guarda en claro: se
/// calcula su huella y se busca esa huella en la base de datos.
/// </summary>
public sealed class ManejadorAutenticacionLlaveApi(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory registrador,
    UrlEncoder codificador,
    IRepositorioIntegradores repositorio,
    TimeProvider reloj)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, registrador, codificador)
{
    public const string Esquema = "LlaveApi";
    public const string Cabecera = "X-Api-Key";
    public const string TipoClaimIntegrador = "integrador_id";

    /// <summary>
    /// Cada cuanto se actualiza la marca de ultimo acceso.
    ///
    /// Actualizarla en cada peticion significaria una escritura en base de
    /// datos por cada lectura, que es un cuello de botella innecesario para
    /// un dato que nadie consulta al segundo.
    /// </summary>
    private static readonly TimeSpan IntervaloRegistroAcceso = TimeSpan.FromMinutes(5);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Cabecera, out var valores))
        {
            // No se presento credencial. No es un fallo: puede ser una ruta
            // publica. Quien decide es la politica de autorizacion.
            return AuthenticateResult.NoResult();
        }

        var llave = valores.ToString();

        if (string.IsNullOrWhiteSpace(llave))
        {
            return AuthenticateResult.Fail("Llave vacia.");
        }

        var huella = Integrador.CalcularHash(llave);
        var integrador = await repositorio.BuscarPorHashLlaveAsync(
            huella, Context.RequestAborted);

        if (integrador is null)
        {
            // El mensaje no distingue entre "no existe" y "esta desactivada":
            // decirlo seria confirmarle a quien prueba llaves cuales existen.
            return AuthenticateResult.Fail("Credencial invalida.");
        }

        if (!integrador.Activo)
        {
            return AuthenticateResult.Fail("Credencial invalida.");
        }

        await ActualizarUltimoAccesoSiCorresponde(integrador);

        var identidad = new ClaimsIdentity(
        [
            new Claim(TipoClaimIntegrador, integrador.Id.ToString()),
            new Claim(ClaimTypes.Name, integrador.Nombre)
        ],
        Esquema);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema);

        return AuthenticateResult.Success(ticket);
    }

    private async Task ActualizarUltimoAccesoSiCorresponde(Integrador integrador)
    {
        var ahora = reloj.GetUtcNow();

        var haceFalta =
            integrador.UltimoAccesoEn is null ||
            ahora - integrador.UltimoAccesoEn.Value >= IntervaloRegistroAcceso;

        if (!haceFalta)
        {
            return;
        }

        integrador.RegistrarAcceso(ahora);
        await repositorio.GuardarCambiosAsync(Context.RequestAborted);
    }

    /// <summary>
    /// Respuesta cuando falta la credencial o no sirve.
    ///
    /// Sin esto ASP.NET devuelve un 401 con el cuerpo vacio. El contrato de
    /// la API promete formato Problem Details en todos los errores (RNF-11).
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties propiedades)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;

        var problema = new ProblemDetails
        {
            Type = "https://github.com/Breiner1412/FEV-Core/errors/no-autenticado",
            Title = "Credencial invalida",
            Status = StatusCodes.Status401Unauthorized,
            Detail = $"Envie una llave de API valida en la cabecera {Cabecera}.",
            Instance = Request.Path
        };

        problema.Extensions["codigo"] = "LLAVE_INVALIDA";
        problema.Extensions["traceId"] = Context.TraceIdentifier;

        // La sobrecarga que fija el tipo de contenido exige tambien las
        // opciones de serializacion; null deja las que trae la aplicacion.
        await Response.WriteAsJsonAsync(
            problema,
            options: (JsonSerializerOptions?)null,
            contentType: "application/problem+json",
            cancellationToken: Context.RequestAborted);
    }
}
