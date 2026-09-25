using System.Collections.ObjectModel;
using FevCore.Domain.Comun;

namespace FevCore.Domain.Documentos;

/// <summary>
/// Un renglon de detalle de un documento.
///
/// Contiene COPIAS de los datos del producto, no referencias. Si el precio
/// o la descripcion del producto cambian despues, esta linea no se entera
/// (INV-LIN-03, seccion 6.1 del modelo de dominio).
///
/// Vive dentro del agregado Documento y no tiene sentido fuera de el.
/// </summary>
public sealed class Linea
{
    private readonly ReadOnlyCollection<ImpuestoLinea> _impuestos;

    public int Numero { get; }

    /// <summary>
    /// Referencia informativa al producto de origen. Puede apuntar a un
    /// producto desactivado, o incluso inexistente, sin que la linea
    /// pierda validez (INV-LIN-04).
    /// </summary>
    public Guid? ProductoId { get; }

    public string Codigo { get; }
    public string Descripcion { get; }
    public string UnidadMedida { get; }
    public decimal Cantidad { get; }
    public Dinero PrecioUnitario { get; }
    public Dinero Descuento { get; }

    /// <summary>Cantidad x precio unitario, menos el descuento.</summary>
    public Dinero BaseGravable { get; }

    /// <summary>
    /// Solo lectura de verdad: el tipo que se devuelve no se puede
    /// convertir de vuelta a List para modificarlo desde afuera.
    /// </summary>
    public IReadOnlyList<ImpuestoLinea> Impuestos => _impuestos;

    /// <summary>Base gravable mas la suma de los impuestos.</summary>
    public Dinero Total { get; }

    private Linea(
        int numero,
        Guid? productoId,
        string codigo,
        string descripcion,
        string unidadMedida,
        decimal cantidad,
        Dinero precioUnitario,
        Dinero descuento,
        Dinero baseGravable,
        ReadOnlyCollection<ImpuestoLinea> impuestos,
        Dinero total)
    {
        Numero = numero;
        ProductoId = productoId;
        Codigo = codigo;
        Descripcion = descripcion;
        UnidadMedida = unidadMedida;
        Cantidad = cantidad;
        PrecioUnitario = precioUnitario;
        Descuento = descuento;
        BaseGravable = baseGravable;
        _impuestos = impuestos;
        Total = total;
    }

    /// <summary>
    /// Unica forma de construir una linea. Verifica todas sus invariantes
    /// antes de devolverla: una linea que existe es una linea valida.
    /// </summary>
    public static Linea Crear(
        int numero,
        string codigo,
        string descripcion,
        string unidadMedida,
        decimal cantidad,
        Dinero precioUnitario,
        IEnumerable<EspecificacionImpuesto> impuestos,
        Dinero? descuento = null,
        Guid? productoId = null)
    {
        if (numero < 1)
        {
            throw new ExcepcionDominio(
                "LINEA_NUMERO_INVALIDO",
                $"El numero de linea debe ser mayor que cero. Recibido: {numero}.");
        }

        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new ExcepcionDominio(
                "LINEA_CODIGO_REQUERIDO",
                $"La linea {numero} no tiene codigo de producto.");
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            throw new ExcepcionDominio(
                "LINEA_DESCRIPCION_REQUERIDA",
                $"La linea {numero} no tiene descripcion.");
        }

        if (string.IsNullOrWhiteSpace(unidadMedida))
        {
            throw new ExcepcionDominio(
                "LINEA_UNIDAD_REQUERIDA",
                $"La linea {numero} no tiene unidad de medida.");
        }

        // INV-LIN-01 (RN-08)
        if (cantidad <= 0m)
        {
            throw new ExcepcionDominio(
                "LINEA_CANTIDAD_INVALIDA",
                $"La cantidad de la linea {numero} debe ser mayor que cero. Recibida: {cantidad}.");
        }

        // INV-LIN-01 (RN-08)
        if (precioUnitario <= Dinero.Cero)
        {
            throw new ExcepcionDominio(
                "LINEA_PRECIO_INVALIDO",
                $"El precio unitario de la linea {numero} debe ser mayor que cero.");
        }

        var descuentoAplicado = descuento ?? Dinero.Cero;
        var valorBruto = precioUnitario * cantidad;

        // INV-LIN-02
        if (descuentoAplicado > valorBruto)
        {
            throw new ExcepcionDominio(
                "LINEA_DESCUENTO_EXCESIVO",
                $"El descuento de la linea {numero} ({descuentoAplicado}) supera " +
                $"su valor bruto ({valorBruto}).");
        }

        var listaImpuestos = impuestos?.ToList() ?? [];

        // INV-IMP-03
        var tiposDuplicados = listaImpuestos
            .GroupBy(i => i.Tipo)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (tiposDuplicados.Count > 0)
        {
            throw new ExcepcionDominio(
                "LINEA_IMPUESTO_DUPLICADO",
                $"La linea {numero} tiene mas de un impuesto de tipo " +
                $"{string.Join(", ", tiposDuplicados)}.");
        }

        var baseGravable = valorBruto - descuentoAplicado;

        var impuestosCalculados = listaImpuestos
            .Select(e => ImpuestoLinea.Calcular(e.Tipo, e.Tarifa, baseGravable))
            .ToList();

        var total = impuestosCalculados.Aggregate(
            baseGravable,
            (acumulado, impuesto) => acumulado + impuesto.Valor);

        return new Linea(
            numero,
            productoId,
            codigo.Trim(),
            descripcion.Trim(),
            unidadMedida.Trim(),
            cantidad,
            precioUnitario,
            descuentoAplicado,
            baseGravable,
            impuestosCalculados.AsReadOnly(),
            total);
    }
}
