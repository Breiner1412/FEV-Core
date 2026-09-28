using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Domain.Productos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Construye las lineas de un documento COPIANDO los datos del producto.
///
/// Esto es RN-10 hecho codigo: la linea se lleva el codigo, la descripcion,
/// la unidad, el precio y las tarifas, y desde ese momento ya no dependen
/// del catalogo. El identificador del producto queda solo como referencia
/// informativa (INV-LIN-04).
///
/// Vive aparte desde H4 porque facturas y notas lo necesitan igual. Si cada
/// caso de uso tuviera su copia, la regla de copiar en vez de referenciar
/// podria terminar aplicada en uno y olvidada en el otro.
/// </summary>
public static class ConstructorLineas
{
    public static List<Linea> Construir(
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
