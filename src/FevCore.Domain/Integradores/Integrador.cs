using System.Security.Cryptography;
using System.Text;
using FevCore.Domain.Comun;

namespace FevCore.Domain.Integradores;

/// <summary>
/// Un sistema externo autorizado a consumir la API.
///
/// No es una persona: es un ERP, un e-commerce o un punto de venta. Por eso
/// se identifica con una llave fija y no con una sesion que expira (ADR-0008).
/// </summary>
public sealed class Integrador
{
    /// <summary>Prefijo visible de las llaves, para reconocerlas de un vistazo.</summary>
    public const string PrefijoLlave = "fev_";

    public Guid Id { get; }
    public string Nombre { get; }

    /// <summary>
    /// Huella criptografica de la llave. La llave en claro NUNCA se guarda:
    /// se muestra una sola vez al crearse y no se puede recuperar (INV-INT-01).
    /// </summary>
    public string LlaveHash { get; }

    public bool Activo { get; private set; }
    public DateTimeOffset CreadoEn { get; }
    public DateTimeOffset? UltimoAccesoEn { get; private set; }

    /// <summary>Requerido por Entity Framework.</summary>
    private Integrador()
    {
        Nombre = null!;
        LlaveHash = null!;
    }

    private Integrador(
        Guid id,
        string nombre,
        string llaveHash,
        bool activo,
        DateTimeOffset creadoEn)
    {
        Id = id;
        Nombre = nombre;
        LlaveHash = llaveHash;
        Activo = activo;
        CreadoEn = creadoEn;
    }

    /// <summary>
    /// Crea un integrador y su llave.
    ///
    /// Devuelve la llave en claro junto con el integrador porque es la unica
    /// oportunidad de verla: despues solo queda su huella.
    /// </summary>
    public static NuevoIntegrador Crear(string nombre, DateTimeOffset momento)
    {
        var llaveEnClaro = GenerarLlave();

        return new NuevoIntegrador(
            CrearConLlave(nombre, llaveEnClaro, momento),
            llaveEnClaro);
    }

    /// <summary>
    /// Crea un integrador con una llave conocida de antemano.
    ///
    /// Sirve para aprovisionar una llave especifica: migrar un integrador
    /// existente, o preparar un entorno de desarrollo con una llave fija.
    /// No devuelve la llave porque quien llama ya la tiene.
    /// </summary>
    public static Integrador CrearConLlave(
        string nombre,
        string llaveEnClaro,
        DateTimeOffset momento)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ExcepcionDominio(
                "INTEGRADOR_NOMBRE_REQUERIDO",
                "El integrador debe tener nombre.");
        }

        if (string.IsNullOrWhiteSpace(llaveEnClaro) || llaveEnClaro.Length < 16)
        {
            throw new ExcepcionDominio(
                "INTEGRADOR_LLAVE_DEBIL",
                "La llave debe tener al menos 16 caracteres.");
        }

        return new Integrador(
            id: Guid.CreateVersion7(),
            nombre: nombre.Trim(),
            llaveHash: CalcularHash(llaveEnClaro),
            activo: true,
            creadoEn: momento);
    }

    /// <summary>
    /// Genera una llave aleatoria de 256 bits.
    ///
    /// Usa el generador criptografico del sistema, no Random: este ultimo es
    /// predecible y bastaria conocer una llave para adivinar las siguientes.
    /// </summary>
    private static string GenerarLlave()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return PrefijoLlave + Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Huella SHA-256 de una llave, en hexadecimal.
    ///
    /// Se usa un hash rapido a proposito, no uno lento como los de
    /// contrasenas. Esos existen porque las personas eligen contrasenas
    /// adivinables y hay que encarecer cada intento. Una llave de 256 bits
    /// aleatorios no se adivina, y en cambio esta huella se calcula en cada
    /// peticion: hacerla lenta seria pagar un costo sin ganar seguridad.
    /// </summary>
    public static string CalcularHash(string llaveEnClaro)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(llaveEnClaro));
        return Convert.ToHexStringLower(bytes);
    }

    public void RegistrarAcceso(DateTimeOffset momento) => UltimoAccesoEn = momento;

    /// <summary>
    /// Un integrador nunca se elimina, para no perder la trazabilidad de los
    /// documentos que emitio (INV-INT-03).
    /// </summary>
    public void Desactivar() => Activo = false;
}

/// <summary>
/// Un integrador recien creado junto con su llave en claro.
/// Es la unica vez que la llave existe fuera de la cabeza de quien la recibe.
/// </summary>
public sealed record NuevoIntegrador(Integrador Integrador, string LlaveEnClaro);
