using System.Net;
using System.Net.Http.Json;
using static FevCore.Integration.Tests.AyudantesPruebas;

namespace FevCore.Integration.Tests;

/// <summary>
/// La unicidad de los catalogos resiste solicitudes simultaneas
/// (INV-ADQ-01, RF-06, RF-07).
///
/// La aplicacion comprobaba que no hubiera otro activo con la misma
/// identificacion, o el mismo codigo, y despues guardaba. Dos altas a la
/// vez pasaban las dos la comprobacion y quedaban dos registros activos
/// iguales: la base no lo impedia, porque el indice no era unico. CLAUDE.md
/// pide duplicar la unicidad en la base precisamente para esto.
///
/// Tiene su propia base de datos, como toda prueba de concurrencia.
/// </summary>
public sealed class UnicidadCatalogosTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private const int AltasSimultaneas = 10;

    private static object Adquirente(string identificacion) => new
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
    };

    private static object Producto(string codigo) => new
    {
        codigo,
        descripcion = "Teclado mecanico",
        unidadMedida = "94",
        precioUnitario = 150_000m,
        impuestos = new[] { new { tipo = "IVA", tarifa = 19m } }
    };

    private static string Unico() =>
        Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString();

    /// <summary>
    /// Exactamente un 201, y todas las demas 409 con el codigo de la regla.
    /// Un 500 tambien seria "no se duplico", pero no le diria al integrador
    /// que paso.
    /// </summary>
    private static async Task AssertUnaSolaAlta(
        HttpResponseMessage[] respuestas, string codigoEsperado)
    {
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);

        foreach (var rechazada in respuestas.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            Assert.Equal(HttpStatusCode.Conflict, rechazada.StatusCode);
            Assert.Equal(
                codigoEsperado,
                (await LeerJson(rechazada)).GetProperty("codigo").GetString());
        }
    }

    /// <summary>INV-ADQ-01, RF-06.</summary>
    [Fact]
    public async Task Altas_simultaneas_de_la_misma_identificacion_crean_un_solo_adquirente()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var identificacion = Unico();

        var respuestas = await Task.WhenAll(
            Enumerable.Range(0, AltasSimultaneas)
                .Select(_ => cliente.PostAsJsonAsync("/api/v1/adquirentes", Adquirente(identificacion))));

        await AssertUnaSolaAlta(respuestas, "IDENTIFICACION_DUPLICADA");
    }

    /// <summary>RF-07.</summary>
    [Fact]
    public async Task Altas_simultaneas_del_mismo_codigo_crean_un_solo_producto()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var codigo = $"PROD-{Unico()}";

        var respuestas = await Task.WhenAll(
            Enumerable.Range(0, AltasSimultaneas)
                .Select(_ => cliente.PostAsJsonAsync("/api/v1/productos", Producto(codigo))));

        await AssertUnaSolaAlta(respuestas, "CODIGO_PRODUCTO_DUPLICADO");
    }

    /// <summary>
    /// INV-ADQ-01 es sobre adquirentes ACTIVOS: uno desactivado no estorba.
    /// Vigila que el indice de la base sea parcial; uno unico sin filtro
    /// impediria volver a registrar a alguien que se dio de baja.
    /// </summary>
    [Fact]
    public async Task Un_adquirente_desactivado_no_impide_registrar_otro_con_su_identificacion()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var identificacion = Unico();

        var primero = await cliente.PostAsJsonAsync("/api/v1/adquirentes", Adquirente(identificacion));
        primero.EnsureSuccessStatusCode();

        var id = (await LeerJson(primero)).GetProperty("id").GetGuid();

        (await cliente.DeleteAsync($"/api/v1/adquirentes/{id}")).EnsureSuccessStatusCode();

        var segundo = await cliente.PostAsJsonAsync("/api/v1/adquirentes", Adquirente(identificacion));

        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);
    }
}
