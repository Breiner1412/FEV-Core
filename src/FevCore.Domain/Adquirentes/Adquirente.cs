using FevCore.Domain.Comun;

namespace FevCore.Domain.Adquirentes;

/// <summary>
/// Quien recibe el documento y adquiere el bien o servicio.
///
/// Nunca se elimina, solo se desactiva: los documentos ya emitidos
/// conservan su referencia, y borrarlo romperia la trazabilidad de
/// operaciones que ya tuvieron efectos fiscales (INV-ADQ-02).
/// </summary>
public sealed class Adquirente
{
    public Guid Id { get; }
    public DatosTributarios Datos { get; private set; }
    public bool Activo { get; private set; }
    public DateTimeOffset CreadoEn { get; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>Requerido por Entity Framework.</summary>
    private Adquirente()
    {
        Datos = null!;
    }

    private Adquirente(
        Guid id,
        DatosTributarios datos,
        bool activo,
        DateTimeOffset creadoEn)
    {
        Id = id;
        Datos = datos;
        Activo = activo;
        CreadoEn = creadoEn;
        ActualizadoEn = creadoEn;
    }

    public static Adquirente Crear(DatosTributarios datos, DateTimeOffset momento) =>
        new(
            id: Guid.CreateVersion7(),
            datos: datos,
            activo: true,
            creadoEn: momento);

    /// <summary>
    /// Cambia los datos del adquirente.
    ///
    /// Si se muda, sus facturas anteriores conservan la direccion que se
    /// declaro en su momento (RN-10).
    /// </summary>
    public void Actualizar(DatosTributarios datos, DateTimeOffset momento)
    {
        Datos = datos;
        ActualizadoEn = momento;
    }

    public void Desactivar(DateTimeOffset momento)
    {
        Activo = false;
        ActualizadoEn = momento;
    }
}
