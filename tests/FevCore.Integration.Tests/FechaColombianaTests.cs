using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// Todo lo que depende de "que dia es" usa la fecha civil colombiana, la
/// misma que el documento declara en su XML (RN-02, INV-RAN-04, RF-10).
///
/// Las 19:30 del 31 de diciembre en Colombia son las 00:30 del 1 de enero
/// en UTC. Si la vigencia se mira en UTC, ese documento se numera con el
/// rango del ano siguiente mientras su XML dice 31 de diciembre, y un rango
/// que vence ese dia aparece ya vencido.
///
/// Un caso por camino: factura, nota y RF-10 usan la misma funcion, pero
/// que la use la factura no prueba que la use la nota. Cubrir un camino de
/// rebote con la prueba de otro es exactamente el hueco que la revision de
/// trazabilidad encontro en los catalogos.
///
/// Las fechas son fijas, no relativas a hoy: ninguna prueba depende del dia
/// en que corra. En su propia clase porque necesita exactamente estos
/// rangos, y ningun otro.
/// </summary>
public sealed class FechaColombianaTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private const string NocheDel31 = "2026-12-31T19:30:00-05:00";

    private static object Rango(string tipo, string prefijo, int anio) => new
    {
        prefijo,
        tipoDocumento = tipo,
        numeroInicial = 1,
        numeroFinal = 100_000,
        vigenteDesde = $"{anio}-01-01",
        vigenteHasta = $"{anio}-12-31",
        numeroAutorizacion = "18760000001",
        claveTecnica = "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c"
    };

    /// <summary>
    /// Un rango de 2026 y otro de 2027 por tipo, con prefijo distinto para
    /// saber cual numero cada documento. Idempotente: ningun caso depende de
    /// que otro haya corrido antes.
    /// </summary>
    private static async Task AsegurarRangosFijos(HttpClient cliente)
    {
        var existentes = (await LeerJson(await cliente.GetAsync(RutaRangos)))
            .EnumerateArray()
            .Select(r => r.GetProperty("prefijo").GetString())
            .ToHashSet();

        (string Tipo, string Prefijo, int Anio)[] rangos =
        [
            ("FACTURA", "SETA", 2026),
            ("FACTURA", "SETB", 2027),
            ("NOTA_CREDITO", "NCA", 2026),
            ("NOTA_CREDITO", "NCB", 2027)
        ];

        foreach (var (tipo, prefijo, anio) in rangos.Where(r => !existentes.Contains(r.Prefijo)))
        {
            (await cliente.PostAsJsonAsync(RutaRangos, Rango(tipo, prefijo, anio)))
                .EnsureSuccessStatusCode();
        }
    }

    private static Task<HttpResponseMessage> EmitirFactura(
        HttpClient cliente, Guid adquirente, Guid producto, string fecha) =>
        cliente.PostAsJsonAsync(RutaFacturas, new
        {
            referenciaExterna = Referencia(),
            adquirenteId = adquirente,
            fechaEmision = fecha,
            lineas = new[] { new { productoId = producto, cantidad = 1m } }
        });

    /// <summary>RN-02, INV-RAN-04.</summary>
    [Fact]
    public async Task Una_factura_de_la_noche_del_31_de_diciembre_usa_el_rango_de_ese_ano()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangosFijos(cliente);

        var respuesta = await EmitirFactura(
            cliente, await CrearAdquirente(cliente), await CrearProducto(cliente), NocheDel31);

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        Assert.Equal("SETA", (await LeerJson(respuesta)).GetProperty("prefijo").GetString());
    }

    /// <summary>RN-02, INV-RAN-04, RF-12.</summary>
    [Fact]
    public async Task Una_nota_de_la_noche_del_31_de_diciembre_usa_el_rango_de_ese_ano()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarRangosFijos(cliente);

        var producto = await CrearProducto(cliente);

        // La factura, a mediados de ano: lo que se prueba es la fecha de la nota.
        var factura = await EmitirFactura(
            cliente, await CrearAdquirente(cliente), producto, "2026-06-01T10:00:00-05:00");

        factura.EnsureSuccessStatusCode();

        var facturaId = (await LeerJson(factura)).GetProperty("id").GetGuid();

        await Aprobar(cliente, facturaId);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/notas-credito", new
        {
            referenciaExterna = Referencia(),
            documentoReferenciadoId = facturaId,
            motivo = "DEVOLUCION_PARCIAL",
            fechaEmision = NocheDel31,
            lineas = new[] { new { productoId = producto, cantidad = 1m } }
        });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        Assert.Equal("NCA", (await LeerJson(respuesta)).GetProperty("prefijo").GetString());
    }

    /// <summary>
    /// RF-10. A las 19:30 del ultimo dia de vigencia, al rango le quedan cero
    /// dias, no menos uno: todavia se puede emitir con el.
    /// </summary>
    [Fact]
    public async Task El_ultimo_dia_de_vigencia_un_rango_no_aparece_vencido_por_la_noche()
    {
        await AsegurarRangosFijos(fabrica.CrearClienteAutenticado());

        using var conRelojFijo = fabrica.WithWebHostBuilder(constructor =>
            constructor.ConfigureTestServices(servicios =>
                servicios.AddSingleton<TimeProvider>(
                    new RelojFijo(DateTimeOffset.Parse(NocheDel31)))));

        var cliente = conRelojFijo.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", FabricaApiConBaseDeDatos.LlaveDePrueba);

        var rangos = await LeerJson(await cliente.GetAsync(RutaRangos));

        var seta = rangos.EnumerateArray()
            .Single(r => r.GetProperty("prefijo").GetString() == "SETA");

        Assert.Equal(0, seta.GetProperty("diasParaVencimiento").GetInt32());
    }
}
