using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Domain.Productos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Caso de uso: emitir una factura electronica de venta.
///
/// Desde H2 resuelve las lineas contra el catalogo y COPIA sus datos al
/// documento. Esa copia es lo que hace que una factura emitida no cambie
/// nunca, aunque el catalogo si (RN-10).
///
/// Desde H3 el numero sale de un rango autorizado por la DIAN, y se toma
/// dentro de una transaccion con la fila del rango bloqueada (ADR-0009).
/// </summary>
public sealed class EmitirFacturaHandler(
    IRepositorioDocumentos repositorio,
    IRepositorioEmisor repositorioEmisor,
    IRepositorioAdquirentes repositorioAdquirentes,
    IRepositorioProductos repositorioProductos,
    IRepositorioRangos repositorioRangos,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    public async Task<ResultadoEmision> EjecutarAsync(
        ComandoEmitirFactura comando,
        CancellationToken cancelacion = default)
    {
        // ── 1. RF-15: antes de nada, verificar si esta peticion ya llego ──
        // Va primero, ANTES de tomar consecutivo, para que un reintento no
        // consuma un numero nuevo.
        var existente = await repositorio.BuscarPorReferenciaExternaAsync(
            comando.IntegradorId,
            comando.ReferenciaExterna,
            cancelacion);

        if (existente is not null)
        {
            return new ResultadoEmision(existente, YaExistia: true);
        }

        // ── 2. RF-05: el emisor debe estar configurado ──
        var emisor = await repositorioEmisor.ObtenerAsync(cancelacion)
            ?? throw new ExcepcionDominio(
                "EMISOR_INCOMPLETO",
                "El emisor no ha sido configurado. Configurelo en PUT /api/v1/emisor " +
                "antes de emitir documentos.");

        // ── 3. El adquirente debe existir y estar activo ──
        var adquirente = await repositorioAdquirentes.ObtenerPorIdAsync(
            comando.AdquirenteId, cancelacion)
            ?? throw new ExcepcionDominio(
                "ADQUIRENTE_NO_ENCONTRADO",
                $"No existe un adquirente con el identificador {comando.AdquirenteId}.");

        if (!adquirente.Activo)
        {
            throw new ExcepcionDominio(
                "ADQUIRENTE_INACTIVO",
                $"El adquirente {adquirente.Datos.RazonSocial} esta desactivado " +
                "y no puede recibir documentos nuevos.");
        }

        // ── 4. Los productos, en UNA sola consulta ──
        // Pedirlos uno por uno dentro del bucle de lineas seria el problema
        // N+1: una factura de diez lineas haria diez viajes a la base.
        var productos = await repositorioProductos.ObtenerPorIdsAsync(
            comando.Lineas.Select(l => l.ProductoId),
            cancelacion);

        var lineas = ConstruirLineas(comando.Lineas, productos);

        var fechaEmision = comando.FechaEmision ?? reloj.GetUtcNow();

        // ── 5. Numero y guardado, en una sola transaccion ──
        //
        // Todo lo anterior (validaciones, catalogo, calculo de lineas) quedo
        // FUERA a proposito. Dentro de la transaccion la fila del rango esta
        // bloqueada y cualquier otra emision espera: lo unico que debe pasar
        // aqui es tomar el numero y guardar. Cuanto menos tiempo dure, mas
        // facturas por segundo aguanta el sistema (RNF-01).
        await using var transaccion =
            await unidadDeTrabajo.IniciarTransaccionAsync(cancelacion);

        var rango = await repositorioRangos.TomarVigenteParaActualizarAsync(
            TipoDocumento.Factura,
            DateOnly.FromDateTime(fechaEmision.UtcDateTime),
            cancelacion)
            ?? throw new ExcepcionDominio(
                "RANGO_NO_DISPONIBLE",
                "No hay un rango de numeracion vigente para facturas de venta " +
                "en esa fecha. Registrelo en POST /api/v1/rangos-numeracion.");

        // Si el rango esta vencido o agotado, esto lanza y la transaccion se
        // revierte al descartarse: no queda numero consumido ni documento.
        var consecutivo = rango.TomarSiguienteConsecutivo(
            DateOnly.FromDateTime(fechaEmision.UtcDateTime));

        var documento = Documento.EmitirFactura(
            integradorId: comando.IntegradorId,
            referenciaExterna: comando.ReferenciaExterna,
            prefijo: rango.Prefijo,
            consecutivo: consecutivo,
            fechaEmision: fechaEmision,
            adquirenteId: adquirente.Id,
            emisorSnapshot: emisor.Datos,
            adquirenteSnapshot: adquirente.Datos,
            lineas: lineas);

        await repositorio.AgregarAsync(documento, cancelacion);

        // Un solo SaveChanges guarda las dos cosas: el documento nuevo y el
        // contador del rango, que Entity Framework ya tiene marcado como
        // modificado porque la entidad vino rastreada de la consulta.
        await repositorio.GuardarCambiosAsync(cancelacion);

        await transaccion.ConfirmarAsync(cancelacion);

        return new ResultadoEmision(documento, YaExistia: false);
    }

    /// <summary>
    /// Construye las lineas COPIANDO los datos del producto.
    ///
    /// Esto es RN-10 hecho codigo: la linea se lleva el codigo, la
    /// descripcion, la unidad, el precio y las tarifas, y desde ese momento
    /// ya no dependen del catalogo. El identificador del producto queda solo
    /// como referencia informativa (INV-LIN-04).
    /// </summary>
    private static List<Linea> ConstruirLineas(
        IReadOnlyList<LineaComando> solicitadas,
        IReadOnlyDictionary<Guid, Producto> productos)
    {
        if (solicitadas is null || solicitadas.Count == 0)
        {
            throw new ExcepcionDominio(
                "DOCUMENTO_SIN_LINEAS",
                "Un documento debe tener al menos una linea de detalle.");
        }

        var lineas = new List<Linea>(solicitadas.Count);

        for (var indice = 0; indice < solicitadas.Count; indice++)
        {
            var solicitada = solicitadas[indice];

            if (!productos.TryGetValue(solicitada.ProductoId, out var producto))
            {
                throw new ExcepcionDominio(
                    "PRODUCTO_NO_ENCONTRADO",
                    $"La linea {indice + 1} referencia el producto " +
                    $"{solicitada.ProductoId}, que no existe.");
            }

            if (!producto.Activo)
            {
                throw new ExcepcionDominio(
                    "PRODUCTO_INACTIVO",
                    $"La linea {indice + 1} referencia el producto " +
                    $"{producto.Codigo}, que esta desactivado.");
            }

            lineas.Add(Linea.Crear(
                numero: indice + 1,

                // ── Copias del catalogo ──
                codigo: producto.Codigo,
                descripcion: producto.Descripcion,
                unidadMedida: producto.UnidadMedida,
                impuestos: producto.Impuestos,

                // El precio se puede negociar; si no se indica, se copia el
                // de referencia. En ambos casos queda fijo en la linea.
                precioUnitario: solicitada.PrecioUnitario is null
                    ? producto.PrecioUnitario
                    : Dinero.Desde(solicitada.PrecioUnitario.Value),

                cantidad: solicitada.Cantidad,
                descuento: solicitada.Descuento is null
                    ? null
                    : Dinero.Desde(solicitada.Descuento.Value),

                // Referencia informativa al origen (INV-LIN-04).
                productoId: producto.Id));
        }

        return lineas;
    }
}
