using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Caso de uso: generar el XML de un documento (RF-16).
///
/// En H5 se dispara a peticion. En H7 lo llamara el proceso en segundo plano
/// que recoge los documentos recien recibidos; el caso de uso sera el mismo.
/// </summary>
public sealed class GenerarXmlHandler(
    IRepositorioDocumentos repositorio,
    IRepositorioRangos repositorioRangos,
    IGeneradorXml generador,
    TimeProvider reloj)
{
    public async Task<Documento?> EjecutarAsync(
        Guid documentoId,
        CancellationToken cancelacion = default)
    {
        var documento = await repositorio.ObtenerPorIdAsync(documentoId, cancelacion);

        if (documento is null)
        {
            return null;
        }

        // EN_PROCESO antes de generar, y guardado antes de intentarlo: si la
        // generacion falla, el documento tiene que haber quedado donde la
        // maquina de estados le permite llegar a FALLIDO (seccion 6.2). Un
        // reintento lo encuentra ya en EnProceso y no repite la transicion.
        if (documento.Estado == EstadoDocumento.Recibido)
        {
            documento.IniciarProceso(reloj.GetUtcNow());
            await repositorio.GuardarCambiosAsync(cancelacion);
        }

        // La clave tecnica no se guarda con el documento: vive en el rango que
        // le dio su numero. Se busca por prefijo, tipo y NUMERO: prefijo y
        // tipo solos pueden coincidir con la autorizacion de otro ano, que
        // tiene otra clave.
        var rango = await repositorioRangos.BuscarQueNumeroAsync(
            documento.Prefijo, documento.Tipo, documento.Consecutivo, cancelacion)
            ?? throw new ExcepcionDominio(
                "RANGO_NO_DISPONIBLE",
                $"No se encuentra el rango que entrego el numero " +
                $"{documento.NumeroCompleto} para {documento.Tipo}. " +
                "Sin su clave tecnica no se puede calcular el codigo unico.");

        var resultado = generador.Generar(documento, rango.ClaveTecnica);

        // El dominio decide si esto procede: si el documento ya tiene XML, o
        // si no esta en EnProceso, lanza.
        documento.RegistrarXmlGenerado(resultado.Xml, resultado.CodigoUnico);

        await repositorio.GuardarCambiosAsync(cancelacion);

        return documento;
    }
}
