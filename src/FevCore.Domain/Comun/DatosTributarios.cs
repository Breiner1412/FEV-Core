namespace FevCore.Domain.Comun;

/// <summary>
/// Datos de identificacion tributaria de una parte de la operacion.
///
/// Lo usan el emisor y el adquirente, y tambien los documentos: cada uno
/// guarda una COPIA de estos datos tal como estaban al momento de emitir
/// (RN-10). Por eso es un objeto de valor y no una entidad: no tiene
/// identidad propia, solo contenido.
/// </summary>
public sealed record DatosTributarios
{
    /// <summary>Codigo del tipo de identificacion. "31" es NIT.</summary>
    public const string TipoNit = "31";

    public string TipoIdentificacion { get; }
    public string Identificacion { get; }
    public string? DigitoVerificacion { get; }
    public string RazonSocial { get; }
    public string Direccion { get; }
    public string MunicipioCodigo { get; }
    public string? Correo { get; }
    public string? Telefono { get; }
    public string Regimen { get; }
    public IReadOnlyList<string> Responsabilidades { get; }

    /// <summary>
    /// Identifica de forma unica a una parte. Es lo que no puede repetirse
    /// entre adquirentes activos (INV-ADQ-01).
    /// </summary>
    public string ClaveIdentidad => $"{TipoIdentificacion}:{Identificacion}";

    /// <summary>Requerido por Entity Framework.</summary>
    private DatosTributarios()
    {
        TipoIdentificacion = null!;
        Identificacion = null!;
        RazonSocial = null!;
        Direccion = null!;
        MunicipioCodigo = null!;
        Regimen = null!;
        Responsabilidades = [];
    }

    private DatosTributarios(
        string tipoIdentificacion,
        string identificacion,
        string? digitoVerificacion,
        string razonSocial,
        string direccion,
        string municipioCodigo,
        string? correo,
        string? telefono,
        string regimen,
        IReadOnlyList<string> responsabilidades)
    {
        TipoIdentificacion = tipoIdentificacion;
        Identificacion = identificacion;
        DigitoVerificacion = digitoVerificacion;
        RazonSocial = razonSocial;
        Direccion = direccion;
        MunicipioCodigo = municipioCodigo;
        Correo = correo;
        Telefono = telefono;
        Regimen = regimen;
        Responsabilidades = responsabilidades;
    }

    public static DatosTributarios Crear(
        string tipoIdentificacion,
        string identificacion,
        string razonSocial,
        string direccion,
        string municipioCodigo,
        string regimen,
        string? digitoVerificacion = null,
        string? correo = null,
        string? telefono = null,
        IEnumerable<string>? responsabilidades = null)
    {
        Exigir(tipoIdentificacion, "TIPO_IDENTIFICACION_REQUERIDO",
            "El tipo de identificacion es obligatorio.");

        Exigir(identificacion, "IDENTIFICACION_REQUERIDA",
            "El numero de identificacion es obligatorio.");

        Exigir(razonSocial, "RAZON_SOCIAL_REQUERIDA",
            "La razon social es obligatoria.");

        Exigir(direccion, "DIRECCION_REQUERIDA",
            "La direccion es obligatoria.");

        Exigir(municipioCodigo, "MUNICIPIO_REQUERIDO",
            "El codigo de municipio es obligatorio.");

        Exigir(regimen, "REGIMEN_REQUERIDO",
            "El regimen tributario es obligatorio.");

        var numero = identificacion.Trim();
        var tipo = tipoIdentificacion.Trim();
        var dv = string.IsNullOrWhiteSpace(digitoVerificacion)
            ? null
            : digitoVerificacion.Trim();

        if (tipo == TipoNit)
        {
            ValidarNit(numero, dv);
        }

        if (correo is not null && correo.Trim().Length > 0 && !correo.Contains('@'))
        {
            throw new ExcepcionDominio(
                "CORREO_INVALIDO",
                $"El correo '{correo}' no tiene forma de direccion electronica.");
        }

        return new DatosTributarios(
            tipoIdentificacion: tipo,
            identificacion: numero,
            digitoVerificacion: dv,
            razonSocial: razonSocial.Trim(),
            direccion: direccion.Trim(),
            municipioCodigo: municipioCodigo.Trim(),
            correo: string.IsNullOrWhiteSpace(correo) ? null : correo.Trim(),
            telefono: string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim(),
            regimen: regimen.Trim(),
            responsabilidades: [.. (responsabilidades ?? []).Select(r => r.Trim())
                .Where(r => r.Length > 0)
                .Distinct()]);
    }

    // ── Igualdad por valor ──

    /// <summary>
    /// Un record compara sus campos con el comparador por defecto de cada
    /// tipo. Para texto y numeros eso compara contenido; para una COLECCION
    /// compara REFERENCIAS.
    ///
    /// Sin esta implementacion, dos DatosTributarios con exactamente el
    /// mismo contenido saldrian distintos solo porque sus listas de
    /// responsabilidades son dos objetos distintos en memoria. Y eso importa:
    /// la copia que guarda un documento debe poder compararse contra los
    /// datos vivos del adquirente y dar "iguales" cuando nada ha cambiado.
    /// </summary>
    public bool Equals(DatosTributarios? otro) =>
        otro is not null
        && TipoIdentificacion == otro.TipoIdentificacion
        && Identificacion == otro.Identificacion
        && DigitoVerificacion == otro.DigitoVerificacion
        && RazonSocial == otro.RazonSocial
        && Direccion == otro.Direccion
        && MunicipioCodigo == otro.MunicipioCodigo
        && Correo == otro.Correo
        && Telefono == otro.Telefono
        && Regimen == otro.Regimen
        && Responsabilidades.SequenceEqual(otro.Responsabilidades);

    public override int GetHashCode()
    {
        var acumulado = new HashCode();

        acumulado.Add(TipoIdentificacion);
        acumulado.Add(Identificacion);
        acumulado.Add(DigitoVerificacion);
        acumulado.Add(RazonSocial);
        acumulado.Add(Direccion);
        acumulado.Add(MunicipioCodigo);
        acumulado.Add(Correo);
        acumulado.Add(Telefono);
        acumulado.Add(Regimen);

        foreach (var responsabilidad in Responsabilidades)
        {
            acumulado.Add(responsabilidad);
        }

        return acumulado.ToHashCode();
    }

    // ── Digito de verificacion del NIT (INV-EMI-01) ──

    /// <summary>
    /// Pesos que la DIAN define para el calculo, aplicados de derecha a
    /// izquierda sobre los digitos del NIT.
    /// </summary>
    private static readonly int[] PesosNit =
        [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];

    /// <summary>
    /// Calcula el digito de verificacion de un NIT.
    ///
    /// No es una suma cualquiera: cada digito se multiplica por un peso
    /// distinto segun su posicion, para que intercambiar dos digitos —el
    /// error de digitacion mas comun— cambie el resultado y se detecte.
    /// </summary>
    public static int CalcularDigitoVerificacion(string nit)
    {
        var suma = 0;
        var digitos = nit.Reverse().ToArray();

        for (var i = 0; i < digitos.Length; i++)
        {
            suma += (digitos[i] - '0') * PesosNit[i];
        }

        var resto = suma % 11;

        return resto < 2 ? resto : 11 - resto;
    }

    private static void ValidarNit(string numero, string? dv)
    {
        if (!numero.All(char.IsAsciiDigit))
        {
            throw new ExcepcionDominio(
                "NIT_INVALIDO",
                $"El NIT '{numero}' debe contener solo digitos.");
        }

        if (numero.Length > PesosNit.Length)
        {
            throw new ExcepcionDominio(
                "NIT_INVALIDO",
                $"El NIT '{numero}' excede la longitud maxima.");
        }

        if (dv is null)
        {
            throw new ExcepcionDominio(
                "DIGITO_VERIFICACION_REQUERIDO",
                "Un NIT debe venir con su digito de verificacion.");
        }

        var esperado = CalcularDigitoVerificacion(numero).ToString();

        if (dv != esperado)
        {
            throw new ExcepcionDominio(
                "DIGITO_VERIFICACION_INCORRECTO",
                $"El digito de verificacion de {numero} deberia ser {esperado}, no {dv}.");
        }
    }

    private static void Exigir(string valor, string codigo, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ExcepcionDominio(codigo, mensaje);
        }
    }
}
