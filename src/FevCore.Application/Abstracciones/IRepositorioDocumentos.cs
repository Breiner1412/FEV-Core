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

    /// <summary>
    /// PROVISIONAL. Consecutivo calculado como el mayor emitido mas uno.
    ///
    /// Es incorrecto bajo concurrencia: dos emisiones simultaneas leen el
    /// mismo maximo y generan el mismo numero. Se reemplaza en H3 por la
    /// asignacion con bloqueo del rango de numeracion (ADR-0009, RNF-06).
    ///
    /// Mientras tanto, el indice unico sobre (prefijo, consecutivo) impide
    /// que el duplicado llegue a persistirse.
    /// </summary>
    Task<long> ObtenerSiguienteConsecutivoProvisionalAsync(
        string prefijo,
        CancellationToken cancelacion = default);

    Task AgregarAsync(
        Documento documento,
        CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}
