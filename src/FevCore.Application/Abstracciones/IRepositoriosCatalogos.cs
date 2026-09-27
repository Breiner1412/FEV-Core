using FevCore.Domain.Adquirentes;
using FevCore.Domain.Emisores;
using FevCore.Domain.Productos;

namespace FevCore.Application.Abstracciones;

/// <summary>
/// El emisor es uno solo por instalacion, asi que no hay busqueda por
/// identificador: o esta configurado o no lo esta.
/// </summary>
public interface IRepositorioEmisor
{
    Task<Emisor?> ObtenerAsync(CancellationToken cancelacion = default);

    Task AgregarAsync(Emisor emisor, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}

public interface IRepositorioAdquirentes
{
    Task<Adquirente?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Busca por tipo y numero de identificacion. Sirve para hacer cumplir
    /// INV-ADQ-01 antes de intentar guardar.
    /// </summary>
    Task<Adquirente?> BuscarActivoPorIdentificacionAsync(
        string tipoIdentificacion,
        string identificacion,
        CancellationToken cancelacion = default);

    Task<PaginaDe<Adquirente>> ListarAsync(
        bool? activo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancelacion = default);

    Task AgregarAsync(Adquirente adquirente, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}

public interface IRepositorioProductos
{
    Task<Producto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Trae varios productos de una sola consulta.
    ///
    /// Emitir una factura de N lineas necesita N productos. Pedirlos uno por
    /// uno haria N viajes a la base de datos por cada emision — el problema
    /// llamado "N+1". Con una sola consulta son dos viajes en total, sin
    /// importar cuantas lineas tenga la factura.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Producto>> ObtenerPorIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancelacion = default);

    Task<Producto?> BuscarActivoPorCodigoAsync(
        string codigo,
        CancellationToken cancelacion = default);

    Task<PaginaDe<Producto>> ListarAsync(
        bool? activo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancelacion = default);

    Task AgregarAsync(Producto producto, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}

/// <summary>
/// Una pagina de resultados con el total, para que quien consulte sepa
/// cuantas paginas hay sin tener que recorrerlas.
/// </summary>
public sealed record PaginaDe<T>(
    IReadOnlyList<T> Elementos,
    int Pagina,
    int TamanoPagina,
    long TotalElementos)
{
    public int TotalPaginas => TamanoPagina <= 0
        ? 0
        : (int)Math.Ceiling(TotalElementos / (double)TamanoPagina);
}
