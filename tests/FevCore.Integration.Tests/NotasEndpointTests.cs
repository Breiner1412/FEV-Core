using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// El camino de H4 de punta a punta: RF-12, RF-13, RF-23, RN-03, RN-04,
/// RN-05 y RN-11 pasando por HTTP y por PostgreSQL real.
/// </summary>
public sealed class NotasEndpointTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private async Task<(HttpClient Cliente, Guid Adquirente, Guid Producto)> Montar()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await ConfigurarEmisor(cliente);
        await AsegurarTodosLosRangos(cliente);

        return (cliente, await CrearAdquirente(cliente), await CrearProducto(cliente));
    }

    // ── Lo que debe funcionar ──

    [Fact]
    public async Task Emitir_una_nota_credito_contra_una_factura_aprobada_responde_202()
    {
        var (cliente, adquirente, producto) = await Montar();
        var (facturaId, _) = await FacturaAprobada(cliente, adquirente, producto);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaNotasCredito,
            SolicitudNota(facturaId, producto, cantidad: 1m, Referencia()));

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var nota = await LeerJson(respuesta);

        Assert.Equal("NOTA_CREDITO", nota.GetProperty("tipo").GetString());
        Assert.Equal("RECIBIDO", nota.GetProperty("estado").GetString());
        Assert.Equal(facturaId, nota.GetProperty("documentoReferenciadoId").GetGuid());
        Assert.Equal("DEVOLUCION_PARCIAL", nota.GetProperty("motivo").GetString());

        // Numerada con su propio rango, no con el de facturas.
        Assert.Equal("NCA", nota.GetProperty("prefijo").GetString());
    }

    [Fact]
    public async Task La_nota_hereda_el_adquirente_de_la_factura()
    {
        var (cliente, adquirente, producto) = await Montar();
        var (facturaId, _) = await FacturaAprobada(cliente, adquirente, producto);

        var nota = await LeerJson(await cliente.PostAsJsonAsync(
            RutaNotasCredito,
            SolicitudNota(facturaId, producto, cantidad: 1m, Referencia())));

        // La solicitud nunca menciono al adquirente: sale de la factura.
        Assert.Equal(adquirente, nota.GetProperty("adquirenteId").GetGuid());
    }

    [Fact]
    public async Task Una_nota_debito_puede_superar_el_valor_de_la_factura()
    {
        var (cliente, adquirente, producto) = await Montar();
        var (facturaId, total) = await FacturaAprobada(cliente, adquirente, producto);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaNotasDebito,
            SolicitudNota(facturaId, producto, cantidad: 10m, Referencia(), "AJUSTE_PRECIO"));

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var nota = await LeerJson(respuesta);

        Assert.True(
            nota.GetProperty("totales").GetProperty("totalAPagar").GetDecimal() > total);
    }

    // ── RN-03 y RN-05 ──

    [Fact]
    public async Task No_se_puede_corregir_una_factura_que_no_esta_aprobada()
    {
        var (cliente, adquirente, producto) = await Montar();

        var factura = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 2m, null, null)])));

        var respuesta = await cliente.PostAsJsonAsync(
            RutaNotasCredito,
            SolicitudNota(factura.GetProperty("id").GetGuid(), producto, 1m, Referencia()));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "DOCUMENTO_REFERENCIADO_NO_APROBADO",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Una_nota_no_puede_referenciar_otra_nota()
    {
        var (cliente, adquirente, producto) = await Montar();
        var (facturaId, _) = await FacturaAprobada(cliente, adquirente, producto);

        var primera = await LeerJson(await cliente.PostAsJsonAsync(
            RutaNotasCredito,
            SolicitudNota(facturaId, producto, cantidad: 1m, Referencia())));

        var notaId = primera.GetProperty("id").GetGuid();
        await Aprobar(cliente, notaId);

        var respuesta = await cliente.PostAsJsonAsync(
            RutaNotasCredito,
            SolicitudNota(notaId, producto, cantidad: 1m, Referencia()));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "DOCUMENTO_REFERENCIADO_INVALIDO",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Referenciar_una_factura_inexistente_responde_409()
    {
        var (cliente, _, producto) = await Montar();

        var respuesta = await cliente.PostAsJsonAsync(
            RutaNotasCredito,
            SolicitudNota(Guid.NewGuid(), producto, cantidad: 1m, Referencia()));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "DOCUMENTO_REFERENCIADO_NO_ENCONTRADO",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    // ── RN-04: el acumulado ──

    [Fact]
    public async Task El_acumulado_de_notas_credito_no_puede_superar_la_factura()
    {
        var (cliente, adquirente, producto) = await Montar();

        // Factura de 2 unidades. Cada nota de 1 unidad acredita la mitad.
        var (facturaId, _) = await FacturaAprobada(cliente, adquirente, producto);

        var primera = await cliente.PostAsJsonAsync(
            RutaNotasCredito, SolicitudNota(facturaId, producto, 1m, Referencia()));
        Assert.Equal(HttpStatusCode.Accepted, primera.StatusCode);

        var segunda = await cliente.PostAsJsonAsync(
            RutaNotasCredito, SolicitudNota(facturaId, producto, 1m, Referencia()));
        Assert.Equal(HttpStatusCode.Accepted, segunda.StatusCode);

        // Las dos primeras suman justo el total. La tercera sobra.
        var tercera = await cliente.PostAsJsonAsync(
            RutaNotasCredito, SolicitudNota(facturaId, producto, 1m, Referencia()));

        Assert.Equal(HttpStatusCode.Conflict, tercera.StatusCode);
        Assert.Equal(
            "NOTA_EXCEDE_VALOR_FACTURA",
            (await LeerJson(tercera)).GetProperty("codigo").GetString());
    }

    // ── RF-15: reintentar no duplica ──

    [Fact]
    public async Task Reenviar_una_nota_con_la_misma_referencia_devuelve_la_misma()
    {
        var (cliente, adquirente, producto) = await Montar();
        var (facturaId, _) = await FacturaAprobada(cliente, adquirente, producto);

        var referencia = Referencia();
        var solicitud = SolicitudNota(facturaId, producto, cantidad: 1m, referencia);

        var una = await cliente.PostAsJsonAsync(RutaNotasCredito, solicitud);
        var otra = await cliente.PostAsJsonAsync(RutaNotasCredito, solicitud);

        Assert.Equal(HttpStatusCode.Accepted, una.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otra.StatusCode);

        var unaJson = await LeerJson(una);
        var otraJson = await LeerJson(otra);

        Assert.Equal(
            unaJson.GetProperty("id").GetGuid(),
            otraJson.GetProperty("id").GetGuid());

        // Y sobre todo: no consumio un consecutivo nuevo.
        Assert.Equal(
            unaJson.GetProperty("consecutivo").GetInt64(),
            otraJson.GetProperty("consecutivo").GetInt64());
    }

    // ── RF-23: el historial ──

    [Fact]
    public async Task El_historial_muestra_la_secuencia_completa_de_transiciones()
    {
        var (cliente, adquirente, producto) = await Montar();
        var (facturaId, _) = await FacturaAprobada(cliente, adquirente, producto);

        var historial = await LeerJson(
            await cliente.GetAsync($"{RutaDocumentos}/{facturaId}/historial"));

        var entradas = historial.EnumerateArray().ToList();

        // Nacimiento mas las tres transiciones hasta Aprobado.
        Assert.Equal(4, entradas.Count);

        // La primera no viene de ningun estado.
        Assert.Equal(JsonValueKind.Null, entradas[0].GetProperty("estadoAnterior").ValueKind);
        Assert.Equal("RECIBIDO", entradas[0].GetProperty("estadoNuevo").GetString());
        Assert.Equal(1, entradas[0].GetProperty("secuencia").GetInt32());

        // INV-TRA-02: cada una empalma con la anterior.
        for (var i = 1; i < entradas.Count; i++)
        {
            Assert.Equal(
                entradas[i - 1].GetProperty("estadoNuevo").GetString(),
                entradas[i].GetProperty("estadoAnterior").GetString());

            Assert.Equal(i + 1, entradas[i].GetProperty("secuencia").GetInt32());
        }

        Assert.Equal("APROBADO", entradas[^1].GetProperty("estadoNuevo").GetString());
        Assert.All(entradas, e =>
            Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("motivo").GetString())));
    }

    [Fact]
    public async Task El_historial_de_un_documento_inexistente_responde_404()
    {
        var (cliente, _, _) = await Montar();

        var respuesta = await cliente.GetAsync(
            $"{RutaDocumentos}/{Guid.NewGuid()}/historial");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ── RN-11: lo terminal es terminal, tambien por HTTP ──

    [Fact]
    public async Task Un_documento_aprobado_no_admite_mas_transiciones()
    {
        var (cliente, adquirente, producto) = await Montar();
        var (facturaId, _) = await FacturaAprobada(cliente, adquirente, producto);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/desarrollo/documentos/{facturaId}/estado",
            new { estado = "EN_PROCESO", motivo = "Reprocesar." });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "ESTADO_TERMINAL",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task No_se_puede_saltar_un_estado_por_HTTP()
    {
        var (cliente, adquirente, producto) = await Montar();

        var factura = await LeerJson(await cliente.PostAsJsonAsync(
            RutaFacturas,
            SolicitudFactura(Referencia(), adquirente, [(producto, 1m, null, null)])));

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/desarrollo/documentos/{factura.GetProperty("id").GetGuid()}/estado",
            new { estado = "APROBADO", motivo = "Atajo." });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "TRANSICION_INVALIDA",
            (await LeerJson(respuesta)).GetProperty("codigo").GetString());
    }
}
