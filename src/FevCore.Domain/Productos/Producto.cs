using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Domain.Productos;

/// <summary>
/// Un elemento del catalogo del emisor: bien o servicio.
///
/// Su precio es DE REFERENCIA. Es el valor que se propone al emitir, no el
/// que queda en la factura: la linea copia el precio y desde ahi ya no se
/// mueve, aunque el catalogo cambie (RN-10, seccion 6.1 del modelo de dominio).
/// </summary>
public sealed class Producto
{
    private readonly List<EspecificacionImpuesto> _impuestos;

    public Guid Id { get; }
    public string Codigo { get; private set; }
    public string Descripcion { get; private set; }
    public string UnidadMedida { get; private set; }

    /// <summary>Precio de referencia. Ver la nota de la clase.</summary>
    public Dinero PrecioUnitario { get; private set; }

    public IReadOnlyList<EspecificacionImpuesto> Impuestos => _impuestos;

    public bool Activo { get; private set; }
    public DateTimeOffset CreadoEn { get; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>Requerido por Entity Framework.</summary>
    private Producto()
    {
        _impuestos = [];
        Codigo = null!;
        Descripcion = null!;
        UnidadMedida = null!;
    }

    private Producto(
        Guid id,
        string codigo,
        string descripcion,
        string unidadMedida,
        Dinero precioUnitario,
        List<EspecificacionImpuesto> impuestos,
        bool activo,
        DateTimeOffset creadoEn)
    {
        Id = id;
        Codigo = codigo;
        Descripcion = descripcion;
        UnidadMedida = unidadMedida;
        PrecioUnitario = precioUnitario;
        _impuestos = impuestos;
        Activo = activo;
        CreadoEn = creadoEn;
        ActualizadoEn = creadoEn;
    }

    public static Producto Crear(
        string codigo,
        string descripcion,
        string unidadMedida,
        Dinero precioUnitario,
        DateTimeOffset momento,
        IEnumerable<EspecificacionImpuesto>? impuestos = null) =>
        new(
            id: Guid.CreateVersion7(),
            codigo: ExigirTexto(codigo, "PRODUCTO_CODIGO_REQUERIDO",
                "El producto debe tener codigo."),
            descripcion: ExigirTexto(descripcion, "PRODUCTO_DESCRIPCION_REQUERIDA",
                "El producto debe tener descripcion."),
            unidadMedida: ExigirTexto(unidadMedida, "PRODUCTO_UNIDAD_REQUERIDA",
                "El producto debe tener unidad de medida."),
            precioUnitario: precioUnitario,
            impuestos: ValidarImpuestos(impuestos),
            activo: true,
            creadoEn: momento);

    public void Actualizar(
        string codigo,
        string descripcion,
        string unidadMedida,
        Dinero precioUnitario,
        DateTimeOffset momento,
        IEnumerable<EspecificacionImpuesto>? impuestos = null)
    {
        Codigo = ExigirTexto(codigo, "PRODUCTO_CODIGO_REQUERIDO",
            "El producto debe tener codigo.");

        Descripcion = ExigirTexto(descripcion, "PRODUCTO_DESCRIPCION_REQUERIDA",
            "El producto debe tener descripcion.");

        UnidadMedida = ExigirTexto(unidadMedida, "PRODUCTO_UNIDAD_REQUERIDA",
            "El producto debe tener unidad de medida.");

        PrecioUnitario = precioUnitario;

        _impuestos.Clear();
        _impuestos.AddRange(ValidarImpuestos(impuestos));

        ActualizadoEn = momento;
    }

    public void Desactivar(DateTimeOffset momento)
    {
        Activo = false;
        ActualizadoEn = momento;
    }

    public void Reactivar(DateTimeOffset momento)
    {
        Activo = true;
        ActualizadoEn = momento;
    }

    private static List<EspecificacionImpuesto> ValidarImpuestos(
        IEnumerable<EspecificacionImpuesto>? impuestos)
    {
        var lista = impuestos?.ToList() ?? [];

        var duplicados = lista
            .GroupBy(i => i.Tipo)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicados.Count > 0)
        {
            throw new ExcepcionDominio(
                "PRODUCTO_IMPUESTO_DUPLICADO",
                $"El producto tiene mas de un impuesto de tipo " +
                $"{string.Join(", ", duplicados)}.");
        }

        var negativas = lista.Where(i => i.Tarifa < 0m).ToList();

        if (negativas.Count > 0)
        {
            throw new ExcepcionDominio(
                "IMPUESTO_TARIFA_INVALIDA",
                "Las tarifas de impuesto no pueden ser negativas.");
        }

        return lista;
    }

    private static string ExigirTexto(string valor, string codigo, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ExcepcionDominio(codigo, mensaje);
        }

        return valor.Trim();
    }
}
