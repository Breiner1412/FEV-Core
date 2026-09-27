using System.ComponentModel.DataAnnotations;
using FevCore.Domain.Adquirentes;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Domain.Emisores;
using FevCore.Domain.Productos;

namespace FevCore.Api.Contratos;

// ─────────────────────────────  Entrada  ─────────────────────────────

/// <summary>
/// Datos de identificacion tributaria que llegan por la API.
/// Se comparten entre emisor y adquirentes.
/// </summary>
public sealed record DatosTributariosSolicitud
{
    [Required]
    [StringLength(5, MinimumLength = 1)]
    public required string TipoIdentificacion { get; init; }

    [Required]
    [StringLength(20, MinimumLength = 1)]
    public required string Identificacion { get; init; }

    [StringLength(1)]
    public string? DigitoVerificacion { get; init; }

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public required string RazonSocial { get; init; }

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public required string Direccion { get; init; }

    [Required]
    [StringLength(10, MinimumLength = 1)]
    public required string MunicipioCodigo { get; init; }

    [StringLength(200)]
    public string? Correo { get; init; }

    [StringLength(30)]
    public string? Telefono { get; init; }

    [Required]
    [StringLength(10, MinimumLength = 1)]
    public required string Regimen { get; init; }

    public IReadOnlyList<string>? Responsabilidades { get; init; }

    /// <summary>
    /// Traduce al tipo del dominio, que es quien valida de verdad:
    /// el digito de verificacion del NIT, el formato del correo y demas.
    /// </summary>
    public DatosTributarios ADominio() => DatosTributarios.Crear(
        tipoIdentificacion: TipoIdentificacion,
        identificacion: Identificacion,
        razonSocial: RazonSocial,
        direccion: Direccion,
        municipioCodigo: MunicipioCodigo,
        regimen: Regimen,
        digitoVerificacion: DigitoVerificacion,
        correo: Correo,
        telefono: Telefono,
        responsabilidades: Responsabilidades);
}

public sealed record ConfigurarEmisorSolicitud
{
    [Required]
    public required DatosTributariosSolicitud Datos { get; init; }

    [StringLength(300)]
    public string? NombreComercial { get; init; }
}

public sealed record AdquirenteSolicitud
{
    [Required]
    public required DatosTributariosSolicitud Datos { get; init; }
}

public sealed record ProductoSolicitud
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string Codigo { get; init; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string Descripcion { get; init; }

    [Required]
    [StringLength(10, MinimumLength = 1)]
    public required string UnidadMedida { get; init; }

    // Los limites se convierten con cultura invariante: sin eso, en una
    // maquina con coma decimal la conversion de "0.000001" falla.
    [Range(typeof(decimal), "0", "999999999999",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "El precio unitario no puede ser negativo.")]
    public required decimal PrecioUnitario { get; init; }

    public IReadOnlyList<ImpuestoSolicitud>? Impuestos { get; init; }

    public IEnumerable<EspecificacionImpuesto> ImpuestosADominio() =>
        (Impuestos ?? []).Select(i => new EspecificacionImpuesto(i.Tipo, i.Tarifa));
}

// ─────────────────────────────  Salida  ─────────────────────────────

public sealed record RespuestaDatosTributarios
{
    public required string TipoIdentificacion { get; init; }
    public required string Identificacion { get; init; }
    public string? DigitoVerificacion { get; init; }
    public required string RazonSocial { get; init; }
    public required string Direccion { get; init; }
    public required string MunicipioCodigo { get; init; }
    public string? Correo { get; init; }
    public string? Telefono { get; init; }
    public required string Regimen { get; init; }
    public required IReadOnlyList<string> Responsabilidades { get; init; }

    public static RespuestaDatosTributarios Desde(DatosTributarios datos) => new()
    {
        TipoIdentificacion = datos.TipoIdentificacion,
        Identificacion = datos.Identificacion,
        DigitoVerificacion = datos.DigitoVerificacion,
        RazonSocial = datos.RazonSocial,
        Direccion = datos.Direccion,
        MunicipioCodigo = datos.MunicipioCodigo,
        Correo = datos.Correo,
        Telefono = datos.Telefono,
        Regimen = datos.Regimen,
        Responsabilidades = datos.Responsabilidades
    };
}

public sealed record RespuestaEmisor
{
    public required Guid Id { get; init; }
    public required RespuestaDatosTributarios Datos { get; init; }
    public string? NombreComercial { get; init; }
    public required DateTimeOffset ActualizadoEn { get; init; }

    /// <summary>
    /// Si es falso, la emision sera rechazada con EMISOR_INCOMPLETO (RF-05).
    /// </summary>
    public required bool ConfiguracionCompleta { get; init; }

    public static RespuestaEmisor Desde(Emisor emisor) => new()
    {
        Id = emisor.Id,
        Datos = RespuestaDatosTributarios.Desde(emisor.Datos),
        NombreComercial = emisor.NombreComercial,
        ActualizadoEn = emisor.ActualizadoEn,
        // En H2 basta con que el emisor exista y tenga sus datos. La
        // verificacion del certificado llega en H6, cuando la firma lo use.
        ConfiguracionCompleta = true
    };
}

public sealed record RespuestaAdquirente
{
    public required Guid Id { get; init; }
    public required RespuestaDatosTributarios Datos { get; init; }
    public required bool Activo { get; init; }
    public required DateTimeOffset CreadoEn { get; init; }
    public required DateTimeOffset ActualizadoEn { get; init; }

    public static RespuestaAdquirente Desde(Adquirente adquirente) => new()
    {
        Id = adquirente.Id,
        Datos = RespuestaDatosTributarios.Desde(adquirente.Datos),
        Activo = adquirente.Activo,
        CreadoEn = adquirente.CreadoEn,
        ActualizadoEn = adquirente.ActualizadoEn
    };
}

public sealed record RespuestaProducto
{
    public required Guid Id { get; init; }
    public required string Codigo { get; init; }
    public required string Descripcion { get; init; }
    public required string UnidadMedida { get; init; }
    public required decimal PrecioUnitario { get; init; }
    public required IReadOnlyList<RespuestaEspecificacionImpuesto> Impuestos { get; init; }
    public required bool Activo { get; init; }
    public required DateTimeOffset CreadoEn { get; init; }
    public required DateTimeOffset ActualizadoEn { get; init; }

    public static RespuestaProducto Desde(Producto producto) => new()
    {
        Id = producto.Id,
        Codigo = producto.Codigo,
        Descripcion = producto.Descripcion,
        UnidadMedida = producto.UnidadMedida,
        PrecioUnitario = producto.PrecioUnitario.Valor,
        Impuestos = [.. producto.Impuestos.Select(
            i => new RespuestaEspecificacionImpuesto
            {
                Tipo = i.Tipo,
                Tarifa = i.Tarifa
            })],
        Activo = producto.Activo,
        CreadoEn = producto.CreadoEn,
        ActualizadoEn = producto.ActualizadoEn
    };
}

public sealed record RespuestaEspecificacionImpuesto
{
    public required TipoImpuesto Tipo { get; init; }
    public required decimal Tarifa { get; init; }
}

/// <summary>
/// Envoltura de un listado paginado.
/// </summary>
public sealed record RespuestaPagina<T>
{
    public required int Pagina { get; init; }
    public required int TamanoPagina { get; init; }
    public required long TotalElementos { get; init; }
    public required int TotalPaginas { get; init; }
    public required IReadOnlyList<T> Elementos { get; init; }
}
