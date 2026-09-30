using System.Net;
using System.Net.Http.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// La vigencia del rango se evalua con la fecha civil colombiana, la misma
/// que el documento declara en su XML (RN-02, INV-RAN-04).
///
/// Las 19:30 del 31 de diciembre en Colombia son las 00:30 del 1 de enero
/// en UTC. Si la vigencia se mira en UTC, esa factura se numera con el rango
/// del ano siguiente —o se rechaza por falta de rango— mientras su XML dice
/// 31 de diciembre. La autoridad veria un documento fechado antes de la
/// autorizacion que lo numero.
///
/// Las fechas son fijas, no relativas a hoy: la vigencia se compara con la
/// fecha de emision, no con el reloj, asi que la prueba no depende del dia
/// en que corra.
///
/// En su propia clase porque necesita exactamente estos dos rangos de
/// facturas, y ningun otro.
/// </summary>
public sealed class FechaColombianaTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private static object Rango(string prefijo, string desde, string hasta) => new
    {
        prefijo,
        tipoDocumento = "FACTURA",
        numeroInicial = 1,
        numeroFinal = 100_000,
        vigenteDesde = desde,
        vigenteHasta = hasta,
        numeroAutorizacion = "18760000001",
        claveTecnica = "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c"
    };

    /// <summary>RN-02, INV-RAN-04.</summary>
    [Fact]
    public async Task Una_factura_de_la_noche_del_31_de_diciembre_usa_el_rango_de_ese_ano()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);

        (await cliente.PostAsJsonAsync(RutaRangos, Rango("SETA", "2026-01-01", "2026-12-31")))
            .EnsureSuccessStatusCode();

        (await cliente.PostAsJsonAsync(RutaRangos, Rango("SETB", "2027-01-01", "2027-12-31")))
            .EnsureSuccessStatusCode();

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(RutaFacturas, new
        {
            referenciaExterna = Referencia(),
            adquirenteId = adquirente,
            fechaEmision = "2026-12-31T19:30:00-05:00",
            lineas = new[] { new { productoId = producto, cantidad = 1m } }
        });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var documento = await LeerJson(respuesta);

        Assert.Equal("SETA", documento.GetProperty("prefijo").GetString());
    }
}
