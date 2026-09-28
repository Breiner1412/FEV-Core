using FevCore.Domain.Comun;
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
    /// Trae el documento con su fila BLOQUEADA hasta el final de la
    /// transaccion en curso.
    ///
    /// Se usa al emitir una nota: mientras se comprueba cuanto se lleva
    /// acreditado de esa factura y se guarda la nota nueva, ninguna otra
    /// transaccion puede hacer lo mismo sobre la misma factura (RN-04).
    ///
    /// Debe llamarse dentro de una transaccion, y ANTES de tomar el rango de
    /// numeracion: todos los casos de uso toman los candados en ese orden
    /// para que dos no puedan quedarse esperandose mutuamente.
    /// </summary>
    Task<Documento?> TomarParaActualizarAsync(
        Guid id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Suma de las notas credito vigentes contra una factura (RN-04).
    /// </summary>
    Task<Dinero> SumarNotasCreditoAsync(
        Guid documentoReferenciadoId,
        CancellationToken cancelacion = default);

    Task AgregarAsync(
        Documento documento,
        CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}
