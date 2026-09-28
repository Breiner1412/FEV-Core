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

        // La clave tecnica no se guarda con el documento: vive en el rango que
        // le dio su numero. Se busca por prefijo y tipo, que es justamente la
        // pareja por la que el rango es unico.
        var rango = await repositorioRangos.BuscarPorPrefijoYTipoAsync(
            documento.Prefijo, documento.Tipo, cancelacion)
            ?? throw new ExcepcionDominio(
                "RANGO_NO_DISPONIBLE",
                $"No se encuentra el rango {documento.Prefijo} para {documento.Tipo}. " +
                "Sin su clave tecnica no se puede calcular el codigo unico.");

        var resultado = generador.Generar(documento, rango.ClaveTecnica);

        // El dominio decide si esto procede: si el documento ya tiene XML, o
        // si no esta en un estado desde el que se pueda avanzar, lanza.
        documento.RegistrarXmlGenerado(
            resultado.Xml,
            resultado.CodigoUnico,
            reloj.GetUtcNow());

        await repositorio.GuardarCambiosAsync(cancelacion);

        return documento;
    }
}
