using System.Net.Http.Json;
using System.Text.Json;

namespace FevCore.Integration.Tests;

/// <summary>
/// Montaje minimo compartido por las pruebas de integracion.
///
/// Vive aparte para que las tres clases de pruebas de emision no repitan el
/// mismo emisor, el mismo adquirente y el mismo rango.
/// </summary>
internal static class AyudantesPruebas
{
    public const string RutaFacturas = "/api/v1/facturas";
    public const string RutaRangos = "/api/v1/rangos-numeracion";

    public static string Referencia() => $"VTA-{Guid.NewGuid():N}"[..20];

    public static async Task<JsonElement> LeerJson(HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Configura el emisor. Es idempotente, se puede repetir.</summary>
    public static async Task ConfigurarEmisor(HttpClient cliente)
    {
        var respuesta = await cliente.PutAsJsonAsync("/api/v1/emisor", new
        {
            datos = new
            {
                tipoIdentificacion = "31",
                identificacion = "800197268",
                digitoVerificacion = "4",
                razonSocial = "Comercializadora del Eje SAS",
                direccion = "Calle 20 # 8-45",
                municipioCodigo = "66001",
                regimen = "48",
                correo = "facturacion@ejemplo.com",
                responsabilidades = new[] { "O-13" }
            },
            nombreComercial = "Comercializadora del Eje"
        });

        respuesta.EnsureSuccessStatusCode();
    }

    public static async Task<Guid> CrearAdquirente(HttpClient cliente)
    {
        // Identificacion distinta en cada llamada: INV-ADQ-01 no permite
        // dos adquirentes activos con la misma.
        var identificacion = Random.Shared
            .NextInt64(1_000_000_000, 9_999_999_999)
            .ToString();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/adquirentes", new
        {
            datos = new
            {
                tipoIdentificacion = "13",
                identificacion,
                razonSocial = "Juan Perez",
                direccion = "Carrera 10 # 5-20",
                municipioCodigo = "66001",
                regimen = "49"
            }
        });

        respuesta.EnsureSuccessStatusCode();

        return (await LeerJson(respuesta)).GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CrearProducto(
        HttpClient cliente,
        decimal precio = 150_000m,
        decimal tarifaIva = 19m)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/productos", new
        {
            codigo = $"PROD-{Guid.NewGuid():N}"[..12],
            descripcion = "Teclado mecanico",
            unidadMedida = "94",
            precioUnitario = precio,
            impuestos = new[] { new { tipo = "IVA", tarifa = tarifaIva } }
        });

        respuesta.EnsureSuccessStatusCode();

        return (await LeerJson(respuesta)).GetProperty("id").GetGuid();
    }

    public static object SolicitudRango(
        string tipoDocumento = "FACTURA",
        string prefijo = "SETP",
        long numeroInicial = 1,
        long numeroFinal = 100_000,
        DateOnly? vigenteDesde = null,
        DateOnly? vigenteHasta = null)
    {
        var hoy = Hoy();

        return new
        {
            prefijo,
            tipoDocumento,
            numeroInicial,
            numeroFinal,
            vigenteDesde = (vigenteDesde ?? hoy.AddDays(-30)).ToString("yyyy-MM-dd"),
            vigenteHasta = (vigenteHasta ?? hoy.AddDays(365)).ToString("yyyy-MM-dd"),
            numeroAutorizacion = "18760000001",
            claveTecnica = "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c"
        };
    }

    public static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Deja registrado un rango vigente de facturas si aun no existe.
    ///
    /// Hace falta comprobar antes de crear porque dos rangos del mismo tipo
    /// no pueden regir al mismo tiempo (INV-RAN-03): un segundo POST
    /// responderia 409.
    /// </summary>
    public static async Task AsegurarRangoFacturas(HttpClient cliente)
    {
        var existentes = await LeerJson(await cliente.GetAsync(RutaRangos));

        foreach (var rango in existentes.EnumerateArray())
        {
            if (rango.GetProperty("tipoDocumento").GetString() == "FACTURA")
            {
                return;
            }
        }

        var respuesta = await cliente.PostAsJsonAsync(RutaRangos, SolicitudRango());
        respuesta.EnsureSuccessStatusCode();
    }

    public static object SolicitudFactura(
        string referencia,
        Guid adquirenteId,
        IEnumerable<(Guid ProductoId, decimal Cantidad, decimal? Precio, decimal? Descuento)> lineas) =>
        new
        {
            referenciaExterna = referencia,
            adquirenteId,
            lineas = lineas.Select(l => new
            {
                productoId = l.ProductoId,
                cantidad = l.Cantidad,
                precioUnitario = l.Precio,
                descuento = l.Descuento
            }).ToArray()
        };
}
