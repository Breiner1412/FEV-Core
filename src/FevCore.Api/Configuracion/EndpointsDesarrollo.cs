using FevCore.Api.Contratos;
using FevCore.Application.Documentos;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Configuracion;

/// <summary>
/// Endpoints que NO existen en produccion.
///
/// Mientras la transmision real no exista (llega en H7), no hay forma de que
/// un documento alcance el estado Aprobado, y sin una factura aprobada no se
/// puede emitir ni probar una nota credito (RN-03). Este endpoint cubre ese
/// hueco.
///
/// Se registra con una lista de entornos permitidos y no descartando
/// "Production": si manana alguien despliega con el entorno llamado
/// "Prod" o "produccion", una lista negra lo dejaria pasar. La lista blanca
/// falla del lado seguro.
/// </summary>
public static class EndpointsDesarrollo
{
    private static readonly string[] EntornosPermitidos = ["Development", "Testing"];

    public static bool EstaPermitidoEn(IWebHostEnvironment entorno) =>
        EntornosPermitidos.Contains(entorno.EnvironmentName);

    public static void Mapear(WebApplication app)
    {
        if (!EstaPermitidoEn(app.Environment))
        {
            return;
        }

        app.MapPost("/api/v1/desarrollo/documentos/{id:guid}/estado", async (
            Guid id,
            [FromBody] TransicionSolicitud solicitud,
            TransicionarDocumentoHandler manejador,
            CancellationToken cancelacion) =>
        {
            var documento = await manejador.EjecutarAsync(
                id, solicitud.Estado, solicitud.Motivo, solicitud.Detalle, cancelacion);

            return documento is null
                ? Results.NotFound(new ProblemDetails
                {
                    Title = "Documento no encontrado",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"No existe un documento con el identificador {id}.",
                    Extensions = { ["codigo"] = "DOCUMENTO_NO_ENCONTRADO" }
                })
                : Results.Ok(RespuestaDocumento.Desde(documento));
        })
        .WithTags("Desarrollo")
        .WithSummary("Fuerza una transicion de estado. Solo fuera de produccion.");

        app.Logger.LogWarning(
            "Endpoints de desarrollo activos. El entorno es {Entorno}.",
            app.Environment.EnvironmentName);
    }
}
