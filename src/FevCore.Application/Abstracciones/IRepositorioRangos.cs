using FevCore.Domain.Documentos;
using FevCore.Domain.Numeracion;

namespace FevCore.Application.Abstracciones;

public interface IRepositorioRangos
{
    /// <summary>
    /// Trae el rango vigente para ese tipo de documento y esa fecha, y
    /// BLOQUEA su fila hasta que termine la transaccion en curso.
    ///
    /// Es el mecanismo que garantiza RN-01 bajo concurrencia: si dos
    /// peticiones llegan a la vez, la segunda espera aqui, y cuando entra
    /// lee el contador ya actualizado por la primera (ADR-0009).
    ///
    /// Debe llamarse dentro de una transaccion. Fuera de una, el bloqueo se
    /// suelta al terminar la propia consulta y no sirve de nada.
    /// </summary>
    Task<RangoNumeracion?> TomarVigenteParaActualizarAsync(
        TipoDocumento tipoDocumento,
        DateOnly fecha,
        CancellationToken cancelacion = default);

    /// <summary>
    /// El rango de un prefijo y tipo concretos. Se usa al generar el XML,
    /// que necesita la clave tecnica del rango que numero el documento.
    /// </summary>
    Task<RangoNumeracion?> BuscarPorPrefijoYTipoAsync(
        string prefijo,
        TipoDocumento tipoDocumento,
        CancellationToken cancelacion = default);

    Task<RangoNumeracion?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Todos los rangos del mismo tipo, para comprobar INV-RAN-03 antes de
    /// registrar uno nuevo.
    /// </summary>
    Task<IReadOnlyList<RangoNumeracion>> ListarPorTipoAsync(
        TipoDocumento tipoDocumento,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RangoNumeracion>> ListarAsync(
        CancellationToken cancelacion = default);

    Task AgregarAsync(RangoNumeracion rango, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}
