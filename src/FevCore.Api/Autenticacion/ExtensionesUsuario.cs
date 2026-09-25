using System.Security.Claims;

namespace FevCore.Api.Autenticacion;

public static class ExtensionesUsuario
{
    /// <summary>
    /// Identificador del integrador que hizo la peticion (RF-02).
    ///
    /// Solo se llama desde codigo que ya paso por autenticacion, asi que si
    /// el dato falta es un error de programacion, no de la peticion.
    /// </summary>
    public static Guid ObtenerIntegradorId(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirstValue(
            ManejadorAutenticacionLlaveApi.TipoClaimIntegrador);

        if (valor is null || !Guid.TryParse(valor, out var id))
        {
            throw new InvalidOperationException(
                "La peticion no tiene integrador autenticado. Revise que el " +
                "endpoint exija autenticacion.");
        }

        return id;
    }
}
