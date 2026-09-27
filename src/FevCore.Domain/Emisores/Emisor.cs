using FevCore.Domain.Comun;

namespace FevCore.Domain.Emisores;

/// <summary>
/// La empresa que expide los documentos. En la version 1 existe uno solo
/// por instalacion.
///
/// El certificado de firma NO vive aqui: se aprovisiona por configuracion
/// y se verifica en H6, cuando la firma lo necesite. Ver la nota de alcance
/// en el pull request de H2.
/// </summary>
public sealed class Emisor
{
    public Guid Id { get; }
    public DatosTributarios Datos { get; private set; }
    public string? NombreComercial { get; private set; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>Requerido por Entity Framework.</summary>
    private Emisor()
    {
        Datos = null!;
    }

    private Emisor(
        Guid id,
        DatosTributarios datos,
        string? nombreComercial,
        DateTimeOffset actualizadoEn)
    {
        Id = id;
        Datos = datos;
        NombreComercial = nombreComercial;
        ActualizadoEn = actualizadoEn;
    }

    public static Emisor Crear(
        DatosTributarios datos,
        DateTimeOffset momento,
        string? nombreComercial = null)
    {
        // INV-EMI-03: al menos una responsabilidad tributaria declarada.
        if (datos.Responsabilidades.Count == 0)
        {
            throw new ExcepcionDominio(
                "EMISOR_SIN_RESPONSABILIDADES",
                "El emisor debe declarar al menos una responsabilidad tributaria.");
        }

        return new Emisor(
            id: Guid.CreateVersion7(),
            datos: datos,
            nombreComercial: Normalizar(nombreComercial),
            actualizadoEn: momento);
    }

    /// <summary>
    /// Reemplaza los datos del emisor.
    ///
    /// No afecta documentos ya emitidos: cada uno guarda su propia copia
    /// de estos datos (RN-10).
    /// </summary>
    public void Actualizar(
        DatosTributarios datos,
        DateTimeOffset momento,
        string? nombreComercial = null)
    {
        if (datos.Responsabilidades.Count == 0)
        {
            throw new ExcepcionDominio(
                "EMISOR_SIN_RESPONSABILIDADES",
                "El emisor debe declarar al menos una responsabilidad tributaria.");
        }

        Datos = datos;
        NombreComercial = Normalizar(nombreComercial);
        ActualizadoEn = momento;
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
