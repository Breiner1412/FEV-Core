using System.Net;
using System.Net.Http.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// RF-08 y RF-10: registro y consulta de rangos.
///
/// Esta clase NO registra ningun rango de facturas a proposito. Solo usa
/// rangos de notas credito, y asi la prueba de "emitir sin rango" puede
/// comprobar el rechazo sin depender de en que orden corran las demas.
/// </summary>
public sealed class RangosNumeracionEndpointTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    [Fact]
    public async Task Registrar_un_rango_responde_201_con_los_numeros_disponibles()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaRangos,
            SolicitudRango(
                tipoDocumento: "NOTA_CREDITO",
                prefijo: "NCA",
                numeroInicial: 500,
                numeroFinal: 1_500));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var json = await LeerJson(respuesta);

        Assert.Equal("NCA", json.GetProperty("prefijo").GetString());
        Assert.Equal("NOTA_CREDITO", json.GetProperty("tipoDocumento").GetString());

        // Ninguno asignado todavia: quedan los 1001 del rango completo.
        Assert.Equal(1_001, json.GetProperty("numerosDisponibles").GetInt64());
        Assert.False(json.GetProperty("agotado").GetBoolean());
        Assert.True(json.GetProperty("vigente").GetBoolean());
        Assert.True(json.GetProperty("diasParaVencimiento").GetInt32() > 0);
    }

    [Fact]
    public async Task La_clave_tecnica_nunca_vuelve_en_la_respuesta()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var creacion = await cliente.PostAsJsonAsync(
            RutaRangos,
            SolicitudRango(
                tipoDocumento: "NOTA_DEBITO",
                prefijo: "NDA",
                numeroInicial: 1,
                numeroFinal: 100));

        creacion.EnsureSuccessStatusCode();

        var cuerpoCreacion = await creacion.Content.ReadAsStringAsync();
        var cuerpoLista = await (await cliente.GetAsync(RutaRangos)).Content.ReadAsStringAsync();

        // La clave tecnica es un secreto entregado por la DIAN. Entra, se
        // guarda y no sale. Se busca en el texto crudo porque asi tambien se
        // detecta si apareciera con otro nombre de campo.
        Assert.DoesNotContain("fc8eac422eba16e22ffd8c6f94b3f40a6e38162c", cuerpoCreacion);
        Assert.DoesNotContain("claveTecnica", cuerpoCreacion);
        Assert.DoesNotContain("fc8eac422eba16e22ffd8c6f94b3f40a6e38162c", cuerpoLista);
    }

    [Fact]
    public async Task Dos_rangos_del_mismo_tipo_vigentes_a_la_vez_se_rechazan()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var primero = await cliente.PostAsJsonAsync(
            RutaRangos,
            SolicitudRango(
                tipoDocumento: "NOTA_CREDITO",
                prefijo: "NCB",
                numeroInicial: 10_000,
                numeroFinal: 20_000,
                // Ventana futura: asi este par no choca con el rango de
                // NOTA_CREDITO que registra otra prueba de esta misma clase.
                vigenteDesde: Hoy().AddDays(400),
                vigenteHasta: Hoy().AddDays(500)));

        primero.EnsureSuccessStatusCode();

        // Numeros y prefijo distintos, pero las mismas fechas: la emision no
        // debe tener que elegir entre dos rangos (INV-RAN-03).
        var segundo = await cliente.PostAsJsonAsync(
            RutaRangos,
            SolicitudRango(
                tipoDocumento: "NOTA_CREDITO",
                prefijo: "NCC",
                numeroInicial: 90_000,
                numeroFinal: 99_000,
                vigenteDesde: Hoy().AddDays(400),
                vigenteHasta: Hoy().AddDays(500)));

        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Equal(
            "RANGO_SOLAPADO",
            (await LeerJson(segundo)).GetProperty("codigo").GetString());
    }

    [Theory]
    [InlineData(100, 100)]   // final igual al inicial
    [InlineData(500, 499)]   // final menor que el inicial
    public async Task Un_rango_sin_numeros_se_rechaza(long inicial, long final)
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaRangos,
            SolicitudRango(
                tipoDocumento: "NOTA_DEBITO",
                prefijo: "NDX",
                numeroInicial: inicial,
                numeroFinal: final,
                vigenteDesde: Hoy().AddDays(600),
                vigenteHasta: Hoy().AddDays(700)));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "RANGO_LIMITES_INVALIDOS",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Consultar_un_rango_que_no_existe_responde_404()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.GetAsync($"{RutaRangos}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(
            "RANGO_NO_ENCONTRADO",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    /// <summary>
    /// RN-02: sin rango vigente de facturas no se emite.
    ///
    /// Antes de H3 esta peticion habria salido con el consecutivo 1 y prefijo
    /// fijo. Ahora se rechaza, que es lo correcto: un numero sin autorizacion
    /// de la DIAN no vale.
    /// </summary>
    [Fact]
    public async Task Sin_rango_vigente_la_emision_se_rechaza()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        await ConfigurarEmisor(cliente);
        var adquirente = await CrearAdquirente(cliente);
        var producto = await CrearProducto(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "RANGO_NO_DISPONIBLE",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }
}
