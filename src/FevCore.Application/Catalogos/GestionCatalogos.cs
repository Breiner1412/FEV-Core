using FevCore.Application.Abstracciones;
using FevCore.Domain.Adquirentes;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Domain.Emisores;
using FevCore.Domain.Productos;

namespace FevCore.Application.Catalogos;

/// <summary>
/// Casos de uso del emisor (RF-03).
/// </summary>
public sealed class GestionEmisor(IRepositorioEmisor repositorio, TimeProvider reloj)
{
    public Task<Emisor?> ObtenerAsync(CancellationToken cancelacion = default) =>
        repositorio.ObtenerAsync(cancelacion);

    /// <summary>
    /// Configura el emisor, o reemplaza su configuracion si ya existe.
    ///
    /// No afecta documentos ya emitidos: cada uno guarda su propia copia
    /// de estos datos (RN-10).
    /// </summary>
    public async Task<Emisor> ConfigurarAsync(
        DatosTributarios datos,
        string? nombreComercial,
        CancellationToken cancelacion = default)
    {
        var ahora = reloj.GetUtcNow();
        var existente = await repositorio.ObtenerAsync(cancelacion);

        if (existente is null)
        {
            var nuevo = Emisor.Crear(datos, ahora, nombreComercial);
            await repositorio.AgregarAsync(nuevo, cancelacion);
            await repositorio.GuardarCambiosAsync(cancelacion);
            return nuevo;
        }

        existente.Actualizar(datos, ahora, nombreComercial);
        await repositorio.GuardarCambiosAsync(cancelacion);
        return existente;
    }
}

/// <summary>
/// Casos de uso de adquirentes (RF-06).
/// </summary>
public sealed class GestionAdquirentes(
    IRepositorioAdquirentes repositorio,
    TimeProvider reloj)
{
    public Task<Adquirente?> ObtenerAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        repositorio.ObtenerPorIdAsync(id, cancelacion);

    public Task<PaginaDe<Adquirente>> ListarAsync(
        bool? activo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancelacion = default) =>
        repositorio.ListarAsync(activo, pagina, tamanoPagina, cancelacion);

    public async Task<Adquirente> CrearAsync(
        DatosTributarios datos,
        CancellationToken cancelacion = default)
    {
        await ExigirIdentificacionLibre(datos, ignorarId: null, cancelacion);

        var adquirente = Adquirente.Crear(datos, reloj.GetUtcNow());

        await repositorio.AgregarAsync(adquirente, cancelacion);
        await repositorio.GuardarCambiosAsync(cancelacion);

        return adquirente;
    }

    public async Task<Adquirente?> ActualizarAsync(
        Guid id,
        DatosTributarios datos,
        CancellationToken cancelacion = default)
    {
        var adquirente = await repositorio.ObtenerPorIdAsync(id, cancelacion);

        if (adquirente is null)
        {
            return null;
        }

        await ExigirIdentificacionLibre(datos, ignorarId: id, cancelacion);

        adquirente.Actualizar(datos, reloj.GetUtcNow());
        await repositorio.GuardarCambiosAsync(cancelacion);

        return adquirente;
    }

    /// <summary>
    /// Desactiva, no elimina. Los documentos que lo referencian conservan
    /// su trazabilidad (INV-ADQ-02).
    /// </summary>
    public async Task<bool> DesactivarAsync(
        Guid id,
        CancellationToken cancelacion = default)
    {
        var adquirente = await repositorio.ObtenerPorIdAsync(id, cancelacion);

        if (adquirente is null)
        {
            return false;
        }

        adquirente.Desactivar(reloj.GetUtcNow());
        await repositorio.GuardarCambiosAsync(cancelacion);

        return true;
    }

    /// <summary>
    /// INV-ADQ-01: no pueden existir dos adquirentes activos con la misma
    /// identificacion.
    ///
    /// Se comprueba aqui para poder devolver un error con sentido. La base
    /// de datos no lo impide por si sola, porque la restriccion solo aplica
    /// a los activos y eso requiere un indice parcial.
    /// </summary>
    private async Task ExigirIdentificacionLibre(
        DatosTributarios datos,
        Guid? ignorarId,
        CancellationToken cancelacion)
    {
        var existente = await repositorio.BuscarActivoPorIdentificacionAsync(
            datos.TipoIdentificacion,
            datos.Identificacion,
            cancelacion);

        if (existente is not null && existente.Id != ignorarId)
        {
            throw new ExcepcionDominio(
                "IDENTIFICACION_DUPLICADA",
                $"Ya existe un adquirente activo con la identificacion " +
                $"{datos.TipoIdentificacion}-{datos.Identificacion}.");
        }
    }
}

/// <summary>
/// Casos de uso de productos (RF-07).
/// </summary>
public sealed class GestionProductos(
    IRepositorioProductos repositorio,
    TimeProvider reloj)
{
    public Task<Producto?> ObtenerAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        repositorio.ObtenerPorIdAsync(id, cancelacion);

    public Task<PaginaDe<Producto>> ListarAsync(
        bool? activo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancelacion = default) =>
        repositorio.ListarAsync(activo, pagina, tamanoPagina, cancelacion);

    public async Task<Producto> CrearAsync(
        string codigo,
        string descripcion,
        string unidadMedida,
        Dinero precioUnitario,
        IEnumerable<EspecificacionImpuesto> impuestos,
        CancellationToken cancelacion = default)
    {
        await ExigirCodigoLibre(codigo, ignorarId: null, cancelacion);

        var producto = Producto.Crear(
            codigo, descripcion, unidadMedida, precioUnitario,
            reloj.GetUtcNow(), impuestos);

        await repositorio.AgregarAsync(producto, cancelacion);
        await repositorio.GuardarCambiosAsync(cancelacion);

        return producto;
    }

    /// <summary>
    /// Cambia el producto, incluido su precio.
    ///
    /// Las facturas ya emitidas NO cambian: cada linea guarda una copia del
    /// precio que se cobro (RN-10). Es la demostracion del hito.
    /// </summary>
    public async Task<Producto?> ActualizarAsync(
        Guid id,
        string codigo,
        string descripcion,
        string unidadMedida,
        Dinero precioUnitario,
        IEnumerable<EspecificacionImpuesto> impuestos,
        CancellationToken cancelacion = default)
    {
        var producto = await repositorio.ObtenerPorIdAsync(id, cancelacion);

        if (producto is null)
        {
            return null;
        }

        await ExigirCodigoLibre(codigo, ignorarId: id, cancelacion);

        producto.Actualizar(
            codigo, descripcion, unidadMedida, precioUnitario,
            reloj.GetUtcNow(), impuestos);

        await repositorio.GuardarCambiosAsync(cancelacion);

        return producto;
    }

    public async Task<bool> DesactivarAsync(
        Guid id,
        CancellationToken cancelacion = default)
    {
        var producto = await repositorio.ObtenerPorIdAsync(id, cancelacion);

        if (producto is null)
        {
            return false;
        }

        producto.Desactivar(reloj.GetUtcNow());
        await repositorio.GuardarCambiosAsync(cancelacion);

        return true;
    }

    private async Task ExigirCodigoLibre(
        string codigo,
        Guid? ignorarId,
        CancellationToken cancelacion)
    {
        var existente = await repositorio.BuscarActivoPorCodigoAsync(
            codigo.Trim(), cancelacion);

        if (existente is not null && existente.Id != ignorarId)
        {
            throw new ExcepcionDominio(
                "CODIGO_PRODUCTO_DUPLICADO",
                $"Ya existe un producto activo con el codigo {codigo}.");
        }
    }
}
