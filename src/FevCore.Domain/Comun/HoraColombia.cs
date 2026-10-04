namespace FevCore.Domain.Comun;

/// <summary>
/// La hora civil de Colombia, que es la que declara un documento.
///
/// Colombia esta en UTC-05:00 y no aplica horario de verano, asi que el
/// desfase es fijo y no hace falta una base de datos de zonas horarias.
///
/// Existe como un solo lugar porque la fecha de un documento se usa en
/// varios sitios que tienen que coincidir: el XML y el codigo unico la
/// escriben, la vigencia del rango se evalua con ella y el listado filtra
/// por ella. Si uno la calculara en UTC, una factura de las 19:30 del 31 de
/// diciembre se numeraria con el rango del 1 de enero mientras su XML dice
/// 31 de diciembre.
/// </summary>
public static class HoraColombia
{
    public static readonly TimeSpan Desfase = TimeSpan.FromHours(-5);

    /// <summary>El mismo instante, escrito en hora colombiana.</summary>
    public static DateTimeOffset En(DateTimeOffset instante) => instante.ToOffset(Desfase);

    /// <summary>La fecha civil colombiana de un instante.</summary>
    public static DateOnly Fecha(DateTimeOffset instante) =>
        DateOnly.FromDateTime(En(instante).DateTime);

    /// <summary>El instante en que empieza una fecha civil colombiana.</summary>
    public static DateTimeOffset InicioDe(DateOnly fecha) =>
        new(fecha.ToDateTime(TimeOnly.MinValue), Desfase);
}
