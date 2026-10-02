using FevCore.Api.Contratos;
using FevCore.Application.Documentos;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Configuracion;

/// <summary>
/// Endpoints que NO existen en produccion.
///
/// Permiten empujar a mano lo que en produccion solo hace el sistema:
///
/// - Forzar un estado. Sin el, una prueba que necesita una factura aprobada
///   tendria que esperar a que el trabajador la transmita y el servicio la
///   apruebe; con el, una nota credito se prueba en una linea (RN-03).
/// - Generar y firmar el XML. En H5 y H6 era la unica forma de disparar esos
///   pasos. Desde H7 los hace el trabajador en segundo plano (ADR-0005), y
///   dejarlos abiertos en produccion permitia a un integrador competir con
///   el por el mismo documento. Pasaron aqui en la auditoria final.
///
/// Se registran con una lista de entornos permitidos y no descartando
/// "Production": si manana alguien despliega con el entorno llamado
/// "Prod" o "produccion", una lista negra lo dejaria pasar. La lista blanca
/// falla del lado seguro. EndpointsSoloDesarrolloTests comprueba las dos
/// mitades: que estan bajo Testing y que no estan en Production.
///
/// Todos llevan la etiqueta Desarrollo, que es lo que los deja fuera del
/// contrato publicado (ContratoGeneradoTests).
/// </summary>
public static class EndpointsDesarrollo
{
    public const string Etiqueta = "Desarrollo";

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
            HttpContext contexto,
            CancellationToken cancelacion) =>
            Responder(
                await manejador.EjecutarAsync(
                    id, solicitud.Estado, solicitud.Motivo, solicitud.Detalle, cancelacion),
                id,
                contexto))
        .WithTags(Etiqueta)
        .WithSummary("Fuerza una transicion de estado. Solo fuera de produccion.");

        // Generar el XML (RF-16). POST porque tiene efectos: guarda el XML,
        // fija el codigo unico y deja el documento en EN_PROCESO.
        app.MapPost("/api/v1/documentos/{id:guid}/xml", async (
            Guid id,
            GenerarXmlHandler generador,
            HttpContext contexto,
            CancellationToken cancelacion) =>
            Responder(await generador.EjecutarAsync(id, cancelacion), id, contexto))
        .WithTags(Etiqueta)
        .WithSummary("Genera el XML a mano. Solo fuera de produccion; lo hace el trabajador.");

        // Firmar (RF-17). No cambia el estado: firmar no es una transicion.
        app.MapPost("/api/v1/documentos/{id:guid}/firma", async (
            Guid id,
            FirmarDocumentoHandler firmador,
            HttpContext contexto,
            CancellationToken cancelacion) =>
            Responder(await firmador.EjecutarAsync(id, cancelacion), id, contexto))
        .WithTags(Etiqueta)
        .WithSummary("Firma el XML a mano. Solo fuera de produccion; lo hace el trabajador.");

        app.Logger.LogWarning(
            "Endpoints de desarrollo activos. El entorno es {Entorno}.",
            app.Environment.EnvironmentName);
    }

    /// <summary>
    /// El documento, o un 404 con la misma forma que el resto de la API.
    /// </summary>
    private static IResult Responder(
        Domain.Documentos.Documento? documento,
        Guid id,
        HttpContext contexto) =>
        documento is null
            ? Results.NotFound(new ProblemDetails
            {
                Type = "https://github.com/Breiner1412/FEV-Core/errors/no-encontrado",
                Title = "Documento no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No existe un documento con el identificador {id}.",
                Instance = contexto.Request.Path,
                Extensions =
                {
                    ["codigo"] = "DOCUMENTO_NO_ENCONTRADO",
                    ["traceId"] = contexto.TraceIdentifier
                }
            })
            : Results.Ok(RespuestaDocumento.Desde(documento));
}
