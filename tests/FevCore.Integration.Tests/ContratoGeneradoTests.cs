using System.Text.Json;
using FevCore.Api.Configuracion;
using YamlDotNet.RepresentationModel;

namespace FevCore.Integration.Tests;

/// <summary>
/// El contrato escrito a mano contra el que genera el codigo (RNF-07).
///
/// api/openapi.yaml se escribio en H0, antes que la implementacion, y ha
/// guiado cada hito desde entonces. Eso tiene una ventaja —se implementa
/// contra algo en vez de inventar— y un riesgo: un documento escrito a mano
/// puede desviarse de lo que el sistema hace de verdad, y entonces miente
/// con toda la autoridad de estar versionado en el repositorio.
///
/// Ya paso una vez en H7: el contrato prometia erroresValidacion como
/// objetos con codigo y descripcion, y la API devolvia textos. Nadie lo vio
/// hasta que alguien miro los dos a la vez.
///
/// Esta prueba convierte ese cotejo en algo que corre en cada integracion.
/// No compara los esquemas —eso seria comparar dos formas de decir lo
/// mismo— sino la superficie: que rutas existen y con que metodos. Es donde
/// una divergencia rompe a un integrador.
/// </summary>
public sealed class ContratoGeneradoTests(FabricaApiConBaseDeDatos fabrica)
    : IClassFixture<FabricaApiConBaseDeDatos>
{
    private static readonly string[] MetodosHttp =
        ["get", "post", "put", "patch", "delete"];

    /// <summary>El prefijo que el contrato declara en servers.</summary>
    private const string Base = "/api/v1";

    /// <summary>
    /// Rutas que el documento generado tiene y el contrato no, con razon.
    ///
    /// No es una lista para ir engordando cuando algo no cuadre: cada entrada
    /// necesita un motivo por el que esa ruta NO forma parte de lo que se le
    /// promete a un integrador.
    /// </summary>
    private static bool FueraDelContrato(string ruta, JsonElement operacion) =>
        // Vive fuera de /api/v1 y por eso no se puede expresar en un
        // documento cuyo servidor ya incluye ese prefijo. Es de operacion,
        // no del contrato de emision.
        ruta == "/health"
        // No existe en produccion: solo se registra bajo Development y
        // Testing. Documentarlo invitaria a construir contra el. Se reconoce
        // por la etiqueta y no por la ruta, porque POST /documentos/{id}/xml
        // es de desarrollo y GET de la misma ruta es del contrato (RF-25).
        || (operacion.TryGetProperty("tags", out var etiquetas)
            && etiquetas.EnumerateArray().Any(e =>
                e.GetString() == EndpointsDesarrollo.Etiqueta));

    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "api", "openapi.yaml")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException(
            "No se encuentra api/openapi.yaml subiendo desde " + AppContext.BaseDirectory);
    }

    private static SortedSet<string> OperacionesDelContrato()
    {
        var yaml = new YamlStream();

        using (var lector = new StreamReader(
            Path.Combine(RaizDelRepositorio(), "api", "openapi.yaml")))
        {
            yaml.Load(lector);
        }

        var raiz = (YamlMappingNode)yaml.Documents[0].RootNode;
        var rutas = (YamlMappingNode)raiz.Children["paths"];
        var operaciones = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (clave, valor) in rutas.Children)
        {
            var ruta = ((YamlScalarNode)clave).Value!;

            foreach (var (metodoNodo, _) in ((YamlMappingNode)valor).Children)
            {
                var metodo = ((YamlScalarNode)metodoNodo).Value!;

                // Una ruta puede declarar parameters a su nivel, fuera de
                // cualquier metodo. No es una operacion.
                if (MetodosHttp.Contains(metodo))
                {
                    operaciones.Add($"{metodo.ToUpperInvariant()} {Base}{ruta}");
                }
            }
        }

        return operaciones;
    }

    private async Task<SortedSet<string>> OperacionesGeneradas()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/openapi/v1.json");
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(
            await respuesta.Content.ReadAsStringAsync());

        var operaciones = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var ruta in documento.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var metodo in ruta.Value.EnumerateObject())
            {
                if (MetodosHttp.Contains(metodo.Name)
                    && !FueraDelContrato(ruta.Name, metodo.Value))
                {
                    operaciones.Add($"{metodo.Name.ToUpperInvariant()} {ruta.Name}");
                }
            }
        }

        return operaciones;
    }

    [Fact]
    public async Task El_documento_generado_se_sirve()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/openapi/v1.json");

        // Sin llave: el contrato es publico. Exigir autenticacion para leer
        // la documentacion obligaria a tener credenciales antes de saber que
        // se puede hacer con ellas.
        respuesta.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task El_contrato_escrito_a_mano_no_promete_rutas_que_no_existen()
    {
        var aMano = OperacionesDelContrato();
        var generadas = await OperacionesGeneradas();

        var sobran = aMano.Except(generadas).ToList();

        Assert.True(
            sobran.Count == 0,
            "El contrato documenta operaciones que la API no expone:\n  " +
            string.Join("\n  ", sobran) +
            "\n\nO se implementan, o se quitan del contrato. Un documento que " +
            "promete rutas inexistentes es peor que no tenerlo.");
    }

    [Fact]
    public async Task La_api_no_expone_rutas_que_el_contrato_no_documenta()
    {
        var aMano = OperacionesDelContrato();
        var generadas = await OperacionesGeneradas();

        var faltan = generadas.Except(aMano).ToList();

        Assert.True(
            faltan.Count == 0,
            "La API expone operaciones que el contrato no documenta:\n  " +
            string.Join("\n  ", faltan) +
            "\n\nO se documentan, o se declaran fuera del contrato en " +
            nameof(FueraDelContrato) + " con un motivo.");
    }

    [Fact]
    public async Task La_comparacion_no_esta_comparando_dos_listas_vacias()
    {
        var aMano = OperacionesDelContrato();
        var generadas = await OperacionesGeneradas();

        // Sin esto, las dos pruebas de arriba pasarian aunque el lector de
        // YAML devolviera nada y el documento generado llegara sin rutas:
        // dos conjuntos vacios no tienen diferencias entre si. Una prueba
        // que no puede fallar no esta probando.
        Assert.True(aMano.Count > 20, $"El contrato solo tiene {aMano.Count} operaciones.");
        Assert.True(generadas.Count > 20, $"La API solo expone {generadas.Count} operaciones.");

        // Y un ancla concreta en cada lado: emitir una factura es la razon de
        // ser del sistema. Si esa operacion desapareciera de cualquiera de
        // los dos documentos, algo muy gordo se rompio.
        Assert.Contains("POST /api/v1/facturas", aMano);
        Assert.Contains("POST /api/v1/facturas", generadas);
        Assert.Contains("GET /api/v1/documentos", generadas);
    }

}
