using System.Reflection;
using FevCore.Domain.Documentos;
using static FevCore.Domain.Tests.Documentos.FabricaDocumentos;

namespace FevCore.Domain.Tests.Documentos;

/// <summary>
/// Pruebas del CUFE.
///
/// Lo que estas pruebas NO pueden demostrar: que el codigo calculado sea el
/// que la DIAN espera. Para eso haria falta un ejemplo oficial con sus datos
/// de entrada y su resultado, y no se dispone de uno. Queda registrado en la
/// documentacion de CodigoUnico y en el hito.
///
/// Lo que SI demuestran, que es lo que atrapa el error mas comun y mas
/// silencioso: que ninguno de los campos se quedo fuera de la concatenacion.
/// </summary>
public sealed class CodigoUnicoTests
{
    private const string ClaveTecnica = "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c";

    private static ValoresCufe Valores() =>
        ValoresCufe.Para(Factura(), ClaveTecnica, AmbienteDian.Pruebas);

    [Fact]
    public void El_codigo_es_un_sha384_en_hexadecimal_minusculo()
    {
        var codigo = CodigoUnico.CalcularCufe(Valores());

        // SHA-384 son 48 bytes, o 96 caracteres hexadecimales.
        Assert.Equal(96, codigo.Length);
        Assert.Matches("^[0-9a-f]{96}$", codigo);
    }

    [Fact]
    public void Los_mismos_valores_producen_siempre_el_mismo_codigo()
    {
        Assert.Equal(
            CodigoUnico.CalcularCufe(Valores()),
            CodigoUnico.CalcularCufe(Valores()));
    }

    /// <summary>
    /// Los campos se enumeran por reflexion y no a mano.
    ///
    /// Asi, el dia que alguien agregue un valor a ValoresCufe y olvide
    /// incluirlo en Concatenar, esta prueba lo detecta sola: el campo nuevo
    /// aparecera en la lista, cambiarlo no alterara el hash, y la prueba
    /// fallara. Una lista escrita a mano solo probaria lo que ya sabiamos.
    /// </summary>
    public static TheoryData<string> Campos()
    {
        var datos = new TheoryData<string>();

        foreach (var propiedad in typeof(ValoresCufe)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string)))
        {
            datos.Add(propiedad.Name);
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Campos))]
    public void Cambiar_cualquier_campo_cambia_el_codigo(string campo)
    {
        var originales = Valores();
        var modificados = originales with { };

        var propiedad = typeof(ValoresCufe).GetProperty(campo)!;
        propiedad.SetValue(modificados, (string)propiedad.GetValue(modificados)! + "9");

        Assert.NotEqual(
            CodigoUnico.CalcularCufe(originales),
            CodigoUnico.CalcularCufe(modificados));
    }

    [Fact]
    public void Produccion_y_pruebas_producen_codigos_distintos()
    {
        var factura = Factura();

        Assert.NotEqual(
            CodigoUnico.CalcularCufe(
                ValoresCufe.Para(factura, ClaveTecnica, AmbienteDian.Produccion)),
            CodigoUnico.CalcularCufe(
                ValoresCufe.Para(factura, ClaveTecnica, AmbienteDian.Pruebas)));
    }

    // ── Formato de los valores ──

    [Fact]
    public void La_hora_se_expresa_en_la_zona_horaria_de_Colombia()
    {
        // Fecha de emision declarada en UTC: las 15:30 UTC son las 10:30 en
        // Colombia, y es esa la hora que debe entrar en el codigo, porque es
        // la que se escribe en el XML.
        var factura = Documento.EmitirFactura(
            integradorId: Integrador,
            referenciaExterna: "VTA-UTC",
            prefijo: "SETP",
            consecutivo: 1,
            fechaEmision: new DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.Zero),
            adquirenteId: AdquirenteId,
            emisorSnapshot: DatosEmisor(),
            adquirenteSnapshot: DatosAdquirente(),
            lineas: [Linea1()]);

        var valores = ValoresCufe.Para(factura, ClaveTecnica, AmbienteDian.Pruebas);

        Assert.Equal("2026-09-28", valores.Fecha);
        Assert.Equal("10:30:00-05:00", valores.Hora);
    }

    [Fact]
    public void Los_importes_llevan_dos_decimales_y_punto()
    {
        var valores = Valores();

        // La factura de referencia vale 357.000, con base 300.000 e IVA 57.000.
        Assert.Equal("300000.00", valores.ValorBruto);
        Assert.Equal("57000.00", valores.ValorIva);
        Assert.Equal("357000.00", valores.ValorTotal);
    }

    /// <summary>
    /// Los impuestos que el documento no lleva valen cero, no desaparecen.
    /// La formula exige los tres codigos siempre.
    /// </summary>
    [Fact]
    public void Los_impuestos_ausentes_entran_como_cero()
    {
        var valores = Valores();

        Assert.Equal("0.00", valores.ValorInc);
        Assert.Equal("0.00", valores.ValorIca);
    }

    /// <summary>
    /// El tramo de impuestos, comprobado como un todo.
    ///
    /// Buscar "01" suelto no probaria nada: aparece en cualquier importe. Lo
    /// que importa es que los tres codigos y sus tres valores queden en el
    /// orden exacto, pegados y sin separadores.
    /// </summary>
    [Fact]
    public void El_tramo_de_impuestos_va_en_el_orden_de_la_norma()
    {
        // "01" + IVA + "04" + INC + "03" + ICA
        Assert.Contains("0157000.00040.00030.00", Valores().Concatenar());
    }

    [Fact]
    public void La_clave_tecnica_y_el_ambiente_cierran_la_concatenacion()
    {
        // Ambiente de pruebas es 2. Que vaya al final no es un detalle: si
        // se colara antes, el codigo cambiaria sin que ningun campo cambiara.
        Assert.EndsWith(ClaveTecnica + "2", Valores().Concatenar());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_clave_tecnica_no_se_puede_calcular(string clave)
    {
        var error = Assert.Throws<FevCore.Domain.Comun.ExcepcionDominio>(() =>
            ValoresCufe.Para(Factura(), clave, AmbienteDian.Pruebas));

        Assert.Equal("CLAVE_TECNICA_REQUERIDA", error.Codigo);
    }
}
