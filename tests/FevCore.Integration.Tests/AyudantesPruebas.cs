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
    public const string RutaNotasCredito = "/api/v1/notas-credito";
    public const string RutaNotasDebito = "/api/v1/notas-debito";
    public const string RutaDocumentos = "/api/v1/documentos";

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
    public static async Task AsegurarRango(
        HttpClient cliente,
        string tipoDocumento,
        string prefijo)
    {
        var existentes = await LeerJson(await cliente.GetAsync(RutaRangos));

        foreach (var rango in existentes.EnumerateArray())
        {
            if (rango.GetProperty("tipoDocumento").GetString() == tipoDocumento)
            {
                return;
            }
        }

        var respuesta = await cliente.PostAsJsonAsync(
            RutaRangos, SolicitudRango(tipoDocumento, prefijo));

        respuesta.EnsureSuccessStatusCode();
    }

    public static Task AsegurarRangoFacturas(HttpClient cliente) =>
        AsegurarRango(cliente, "FACTURA", "SETP");

    /// <summary>Rangos para los tres tipos de documento.</summary>
    public static async Task AsegurarTodosLosRangos(HttpClient cliente)
    {
        await AsegurarRango(cliente, "FACTURA", "SETP");
        await AsegurarRango(cliente, "NOTA_CREDITO", "NCA");
        await AsegurarRango(cliente, "NOTA_DEBITO", "NDA");
    }

    /// <summary>
    /// Lleva un documento a un estado usando el endpoint de desarrollo.
    ///
    /// Ese endpoint solo se registra fuera de produccion. Si algun dia
    /// alguien lo expusiera de mas, estas pruebas seguirian pasando: quien
    /// lo vigila es AutenticacionTests, no estas.
    /// </summary>
    public static async Task ForzarEstado(
        HttpClient cliente,
        Guid documentoId,
        string estado,
        string motivo)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/desarrollo/documentos/{documentoId}/estado",
            new { estado, motivo });

        respuesta.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Recorre la maquina de estados hasta Aprobado. No hay atajo: cada
    /// salto es una transicion valida de la seccion 6.2.
    /// </summary>
    public static async Task Aprobar(HttpClient cliente, Guid documentoId)
    {
        await ForzarEstado(cliente, documentoId, "EN_PROCESO", "Generando XML.");
        await ForzarEstado(cliente, documentoId, "TRANSMITIDO", "Enviado al validador.");
        await ForzarEstado(cliente, documentoId, "APROBADO", "Validado por la autoridad.");
    }

    /// <summary>Emite una factura y la deja aprobada. Devuelve su id y su total.</summary>
    public static async Task<(Guid Id, decimal Total)> FacturaAprobada(
        HttpClient cliente,
        Guid adquirenteId,
        Guid productoId,
        decimal cantidad = 2m)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirenteId, [(productoId, cantidad, null, null)]));

        respuesta.EnsureSuccessStatusCode();

        var json = await LeerJson(respuesta);
        var id = json.GetProperty("id").GetGuid();
        var total = json.GetProperty("totales").GetProperty("totalAPagar").GetDecimal();

        await Aprobar(cliente, id);

        return (id, total);
    }

    public static object SolicitudNota(
        Guid facturaId,
        Guid productoId,
        decimal cantidad,
        string referencia,
        string motivo = "DEVOLUCION_PARCIAL") =>
        new
        {
            referenciaExterna = referencia,
            documentoReferenciadoId = facturaId,
            motivo,
            lineas = new[] { new { productoId, cantidad } }
        };

    /// <summary>
    /// El detalle de la ultima transicion del historial. En un FALLIDO es lo
    /// que dice cual de los desenlaces fue (RN-13, ADR-0015).
    /// </summary>
    public static async Task<string> DetalleDeLaUltimaTransicion(HttpClient cliente, Guid id)
    {
        var historial = await LeerJson(await cliente.GetAsync($"{RutaDocumentos}/{id}/historial"));

        return historial.EnumerateArray().Last().GetProperty("detalle").GetString()!;
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
