using FevCore.Domain.Documentos;

namespace FevCore.Application.Abstracciones;

public interface IRepositorioDocumentos
{
    Task<Documento?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Busca un documento ya emitido con esa referencia externa (RF-15).
    /// Es lo que permite reintentar una emision sin duplicarla.
    /// </summary>
    Task<Documento?> BuscarPorReferenciaExternaAsync(
        Guid integradorId,
        string referenciaExterna,
        CancellationToken cancelacion = default);

    Task AgregarAsync(
        Documento documento,
        CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}
