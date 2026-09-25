using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Caso de uso: consultar un documento por su identificador (RF-22).
/// </summary>
public sealed class ConsultarDocumentoHandler(IRepositorioDocumentos repositorio)
{
    public Task<Documento?> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        repositorio.ObtenerPorIdAsync(id, cancelacion);
}
