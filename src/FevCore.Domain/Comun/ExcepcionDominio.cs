namespace FevCore.Domain.Comun;

/// <summary>
/// Una regla de negocio fue violada.
///
/// Se distingue de las excepciones normales de .NET a proposito:
///
/// - ArgumentOutOfRangeException y similares señalan un error del programador:
///   codigo que llamo mal a otro codigo. No deberian llegarle al usuario.
///
/// - ExcepcionDominio señala que el usuario pidio algo que el negocio no
///   permite. La capa de API la traduce a una respuesta con su codigo,
///   segun el catalogo de errores del contrato.
/// </summary>
public sealed class ExcepcionDominio : Exception
{
    /// <summary>
    /// Codigo estable de la causa. Es lo que el integrador usa para decidir;
    /// el mensaje puede cambiar de redaccion.
    /// </summary>
    public string Codigo { get; }

    public ExcepcionDominio(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}
