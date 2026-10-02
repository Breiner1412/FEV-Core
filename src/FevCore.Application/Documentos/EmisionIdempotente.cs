using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;
using Microsoft.Extensions.Logging;

namespace FevCore.Application.Documentos;

/// <summary>
/// RF-15: una solicitud repetida devuelve el documento ya creado en vez de
/// crear otro. Lo comparten facturas y notas, para que la regla no pueda
/// cumplirse en un caso de uso y olvidarse en el otro.
///
/// Hay dos formas de que la solicitud ya haya llegado, y las dos terminan
/// igual:
///
/// - Llego antes y ya termino: se encuentra al buscar, ANTES de tomar
///   consecutivo, para que el reintento no consuma un numero nuevo.
/// - Llego a la vez y gano la carrera: las dos pasaron la busqueda, y esta
///   choca con el indice unico al guardar. Su transaccion ya se revirtio
///   —numero incluido— cuando se captura la excepcion, y el documento
///   ganador ya esta confirmado: PostgreSQL no informa la violacion hasta
///   que la otra transaccion confirma.
/// </summary>
internal static class EmisionIdempotente
{
    public static async Task<ResultadoEmision> EjecutarAsync(
        IRepositorioDocumentos repositorio,
        ILogger registrador,
        Guid integradorId,
        string referenciaExterna,
        Func<Task<Documento>> emitir,
        CancellationToken cancelacion)
    {
        var resultado = await EmitirUnaVezAsync(
            repositorio, integradorId, referenciaExterna, emitir, cancelacion);

        // RNF-10: el registro que une el documento con la peticion que lo
        // creo. Se escribe dentro del scope de la peticion, que lleva su
        // traceId; desde ahi se llega al resto de lo que paso en ella.
        registrador.LogInformation(
            "Documento {documentoId} ({numero}) recibido. Ya existia: {yaExistia}.",
            resultado.Documento.Id,
            resultado.Documento.NumeroCompleto,
            resultado.YaExistia);

        return resultado;
    }

    private static async Task<ResultadoEmision> EmitirUnaVezAsync(
        IRepositorioDocumentos repositorio,
        Guid integradorId,
        string referenciaExterna,
        Func<Task<Documento>> emitir,
        CancellationToken cancelacion)
    {
        var existente = await repositorio.BuscarPorReferenciaExternaAsync(
            integradorId, referenciaExterna, cancelacion);

        if (existente is not null)
        {
            return new ResultadoEmision(existente, YaExistia: true);
        }

        try
        {
            return new ResultadoEmision(await emitir(), YaExistia: false);
        }
        catch (ExcepcionReferenciaDuplicada)
        {
            var ganador = await repositorio.BuscarPorReferenciaExternaAsync(
                integradorId, referenciaExterna, cancelacion)
                ?? throw new InvalidOperationException(
                    $"El indice de RF-15 rechazo la referencia {referenciaExterna}, " +
                    "pero no se encuentra el documento que la tiene.");

            return new ResultadoEmision(ganador, YaExistia: true);
        }
    }
}
