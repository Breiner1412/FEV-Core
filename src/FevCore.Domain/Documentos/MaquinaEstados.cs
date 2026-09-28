namespace FevCore.Domain.Documentos;

/// <summary>
/// Las transiciones que un documento puede hacer, y ninguna mas.
///
/// Esta tabla es la seccion 6.2 del documento de requerimientos escrita en
/// codigo. Existe en UN solo lugar a proposito: si el estado terminal se
/// declarara aparte, en una lista o un switch, tarde o temprano una de las
/// dos se actualizaria sin la otra. Aqui "terminal" no se declara, se
/// deduce: terminal es el estado del que no sale ninguna transicion.
/// </summary>
public static class MaquinaEstados
{
    private static readonly Dictionary<EstadoDocumento, EstadoDocumento[]> Permitidas = new()
    {
        [EstadoDocumento.Recibido] =
        [
            EstadoDocumento.EnProceso
        ],

        [EstadoDocumento.EnProceso] =
        [
            EstadoDocumento.Transmitido,
            EstadoDocumento.Fallido      // no se pudo generar o firmar
        ],

        [EstadoDocumento.Transmitido] =
        [
            EstadoDocumento.Aprobado,
            EstadoDocumento.Rechazado,
            EstadoDocumento.Fallido      // sin veredicto tras agotar reintentos
        ],

        // Estados terminales: de aqui no se sale (RN-11).
        [EstadoDocumento.Aprobado] = [],
        [EstadoDocumento.Rechazado] = [],
        [EstadoDocumento.Fallido] = []
    };

    public static bool EsTerminal(EstadoDocumento estado) =>
        Permitidas[estado].Length == 0;

    public static bool Permite(EstadoDocumento desde, EstadoDocumento hacia) =>
        Permitidas[desde].Contains(hacia);

    /// <summary>
    /// A donde puede ir un documento desde aqui. Sirve para que el mensaje
    /// de error diga que SI se puede hacer, no solo que no se puede.
    /// </summary>
    public static IReadOnlyList<EstadoDocumento> DestinosDesde(EstadoDocumento estado) =>
        Permitidas[estado];
}
