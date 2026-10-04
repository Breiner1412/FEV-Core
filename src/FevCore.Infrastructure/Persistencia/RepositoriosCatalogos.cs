using FevCore.Application.Abstracciones;
using FevCore.Domain.Adquirentes;
using FevCore.Domain.Emisores;
using FevCore.Domain.Productos;
using FevCore.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace FevCore.Infrastructure.Persistencia;

public sealed class RepositorioEmisor(FevCoreDbContext contexto) : IRepositorioEmisor
{
    public Task<Emisor?> ObtenerAsync(CancellationToken cancelacion = default) =>
        contexto.Emisores.FirstOrDefaultAsync(cancelacion);

    public async Task AgregarAsync(Emisor emisor, CancellationToken cancelacion = default) =>
        await contexto.Emisores.AddAsync(emisor, cancelacion);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}

public sealed class RepositorioAdquirentes(FevCoreDbContext contexto)
    : IRepositorioAdquirentes
{
    public Task<Adquirente?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        contexto.Adquirentes.FirstOrDefaultAsync(a => a.Id == id, cancelacion);

    public Task<Adquirente?> BuscarActivoPorIdentificacionAsync(
        string tipoIdentificacion,
        string identificacion,
        CancellationToken cancelacion = default) =>
        contexto.Adquirentes.FirstOrDefaultAsync(
            a => a.Activo
                 && a.Datos.TipoIdentificacion == tipoIdentificacion
                 && a.Datos.Identificacion == identificacion,
            cancelacion);

    public async Task<PaginaDe<Adquirente>> ListarAsync(
        bool? activo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Adquirentes.AsNoTracking();

        if (activo is not null)
        {
            consulta = consulta.Where(a => a.Activo == activo);
        }

        // El total se cuenta antes de paginar: es cuantos hay en total,
        // no cuantos caben en esta pagina.
        var total = await consulta.LongCountAsync(cancelacion);

        var elementos = await consulta
            .OrderBy(a => a.Datos.RazonSocial)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancelacion);

        return new PaginaDe<Adquirente>(elementos, pagina, tamanoPagina, total);
    }

    public async Task AgregarAsync(
        Adquirente adquirente,
        CancellationToken cancelacion = default) =>
        await contexto.Adquirentes.AddAsync(adquirente, cancelacion);

    /// <summary>
    /// Guarda, y traduce la violacion de INV-ADQ-01 al mismo error que da la
    /// comprobacion previa. Esa comprobacion no resiste dos altas a la vez;
    /// el indice si, y quien pierde la carrera recibe el mismo 409 que si
    /// hubiera llegado despues.
    /// </summary>
    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        TraduccionUnicidad.GuardarAsync(
            contexto,
            ConfiguracionAdquirente.IndiceIdentificacion,
            () => new ExcepcionDominio(
                "IDENTIFICACION_DUPLICADA",
                "Ya existe un adquirente activo con esa identificacion. " +
                "Consultelo y use ese, o desactivelo antes de registrar otro."),
            cancelacion);
}

public sealed class RepositorioProductos(FevCoreDbContext contexto)
    : IRepositorioProductos
{
    public Task<Producto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        contexto.Productos.FirstOrDefaultAsync(p => p.Id == id, cancelacion);

    public async Task<IReadOnlyDictionary<Guid, Producto>> ObtenerPorIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancelacion = default)
    {
        var buscados = ids.Distinct().ToArray();

        if (buscados.Length == 0)
        {
            return new Dictionary<Guid, Producto>();
        }

        // Una sola consulta con IN (...), no una por producto.
        var encontrados = await contexto.Productos
            .Where(p => buscados.Contains(p.Id))
            .ToListAsync(cancelacion);

        return encontrados.ToDictionary(p => p.Id);
    }

    public Task<Producto?> BuscarActivoPorCodigoAsync(
        string codigo,
        CancellationToken cancelacion = default) =>
        contexto.Productos.FirstOrDefaultAsync(
            p => p.Activo && p.Codigo == codigo,
            cancelacion);

    public async Task<PaginaDe<Producto>> ListarAsync(
        bool? activo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Productos.AsNoTracking();

        if (activo is not null)
        {
            consulta = consulta.Where(p => p.Activo == activo);
        }

        var total = await consulta.LongCountAsync(cancelacion);

        var elementos = await consulta
            .OrderBy(p => p.Codigo)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancelacion);

        return new PaginaDe<Producto>(elementos, pagina, tamanoPagina, total);
    }

    public async Task AgregarAsync(
        Producto producto,
        CancellationToken cancelacion = default) =>
        await contexto.Productos.AddAsync(producto, cancelacion);

    /// <summary>
    /// Guarda, y traduce la violacion del codigo unico al mismo error que da
    /// la comprobacion previa. Ver RepositorioAdquirentes.GuardarCambiosAsync.
    /// </summary>
    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        TraduccionUnicidad.GuardarAsync(
            contexto,
            ConfiguracionProducto.IndiceCodigo,
            () => new ExcepcionDominio(
                "CODIGO_PRODUCTO_DUPLICADO",
                "Ya existe un producto activo con ese codigo. Use otro codigo, " +
                "o desactive el existente antes de reutilizarlo."),
            cancelacion);
}
