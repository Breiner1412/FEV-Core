using System.Net.Http.Json;
using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;
using Microsoft.Extensions.DependencyInjection;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// El codigo unico se calcula con la clave tecnica del rango que numero el
/// documento, y no con la de otro rango del mismo prefijo (RF-20).
///
/// Dos rangos pueden compartir prefijo y tipo si no comparten numeros ni
/// fechas (INV-RAN-03): la autorizacion de este ano y la del anterior. Cada
/// uno trae su propia clave tecnica. Buscar "el rango del prefijo" devuelve
/// uno cualquiera, y con la clave equivocada el CUFE sale mal sin que nada
/// falle: el documento se genera, se firma y se transmite.
///
/// En su propia clase porque necesita una base con exactamente estos dos
/// rangos, y ningun otro de facturas.
/// </summary>
public sealed class RangoDelDocumentoTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private const string ClaveVieja = "clave-tecnica-del-rango-vencido";
    private const string ClaveVigente = "clave-tecnica-del-rango-vigente";

    private static object Rango(
        long inicial, long final, DateOnly desde, DateOnly hasta, string clave) => new
    {
        prefijo = "SETP",
        tipoDocumento = "FACTURA",
        numeroInicial = inicial,
        numeroFinal = final,
        vigenteDesde = desde.ToString("yyyy-MM-dd"),
        vigenteHasta = hasta.ToString("yyyy-MM-dd"),
        numeroAutorizacion = "18760000001",
        claveTecnica = clave
    };

    /// <summary>RF-20, INV-RAN-03.</summary>
    [Fact]
    public async Task El_codigo_unico_usa_la_clave_del_rango_que_numero_el_documento()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var hoy = Hoy();

        await ConfigurarEmisor(cliente);

        // El vencido se registra PRIMERO: es el que una busqueda sin orden
        // tiende a devolver.
        (await cliente.PostAsJsonAsync(RutaRangos, Rango(
            1, 1_000, hoy.AddYears(-2), hoy.AddYears(-1), ClaveVieja)))
            .EnsureSuccessStatusCode();

        (await cliente.PostAsJsonAsync(RutaRangos, Rango(
            1_001, 100_000, hoy.AddDays(-30), hoy.AddDays(365), ClaveVigente)))
            .EnsureSuccessStatusCode();

        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        respuesta.EnsureSuccessStatusCode();

        var id = (await LeerJson(respuesta)).GetProperty("id").GetGuid();

        await fabrica.ProcesarTodoAsync();

        using var alcance = fabrica.Services.CreateScope();

        var documento = await alcance.ServiceProvider
            .GetRequiredService<IRepositorioDocumentos>()
            .ObtenerPorIdAsync(id);

        Assert.NotNull(documento);
        Assert.Equal(1_001, documento.Consecutivo);

        var esperado = CodigoUnico.CalcularCufe(
            ValoresCufe.Para(documento, ClaveVigente, AmbienteDian.Pruebas));

        Assert.Equal(esperado, documento.CodigoUnico);
    }
}
