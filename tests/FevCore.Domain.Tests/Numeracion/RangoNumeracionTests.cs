using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Domain.Numeracion;

namespace FevCore.Domain.Tests.Numeracion;

public sealed class RangoNumeracionTests
{
    private static readonly DateTimeOffset Momento =
        new(2026, 9, 27, 14, 0, 0, TimeSpan.FromHours(-5));

    private static readonly DateOnly Hoy = new(2026, 9, 27);

    private static RangoNumeracion Crear(
        string prefijo = "SETP",
        long numeroInicial = 990_000_001,
        long numeroFinal = 990_001_000,
        DateOnly? desde = null,
        DateOnly? hasta = null,
        TipoDocumento tipo = TipoDocumento.Factura) =>
        RangoNumeracion.Crear(
            prefijo: prefijo,
            tipoDocumento: tipo,
            numeroInicial: numeroInicial,
            numeroFinal: numeroFinal,
            vigenteDesde: desde ?? new DateOnly(2026, 1, 1),
            vigenteHasta: hasta ?? new DateOnly(2026, 12, 31),
            numeroAutorizacion: "18760000001",
            claveTecnica: "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c",
            momento: Momento);

    // ── Creacion ──

    [Fact]
    public void Se_crea_sin_haber_entregado_numeros()
    {
        var rango = Crear();

        Assert.Null(rango.UltimoAsignado);
        Assert.Equal(1_000, rango.NumerosDisponibles);
        Assert.False(rango.Agotado);
    }

    [Fact]
    public void El_prefijo_se_normaliza_a_mayusculas()
    {
        var rango = Crear(prefijo: " setp ");

        Assert.Equal("SETP", rango.Prefijo);
    }

    // ── INV-RAN-01: el final debe ser mayor que el inicial ──

    [Theory]
    [InlineData(100, 100)]   // iguales
    [InlineData(100, 99)]    // invertidos
    public void Rechaza_limites_invalidos(long inicial, long final)
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => Crear(numeroInicial: inicial, numeroFinal: final));

        Assert.Equal("RANGO_LIMITES_INVALIDOS", error.Codigo);
    }

    [Fact]
    public void Rechaza_una_vigencia_invertida()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => Crear(
            desde: new DateOnly(2026, 12, 31),
            hasta: new DateOnly(2026, 1, 1)));

        Assert.Equal("RANGO_VIGENCIA_INVALIDA", error.Codigo);
    }

    [Fact]
    public void Rechaza_un_rango_sin_clave_tecnica()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => RangoNumeracion.Crear(
            prefijo: "SETP",
            tipoDocumento: TipoDocumento.Factura,
            numeroInicial: 1,
            numeroFinal: 100,
            vigenteDesde: new DateOnly(2026, 1, 1),
            vigenteHasta: new DateOnly(2026, 12, 31),
            numeroAutorizacion: "18760000001",
            claveTecnica: "  ",
            momento: Momento));

        Assert.Equal("RANGO_CLAVE_TECNICA_REQUERIDA", error.Codigo);
    }

    // ── Entrega de consecutivos ──

    [Fact]
    public void El_primer_numero_entregado_es_el_inicial()
    {
        var rango = Crear(numeroInicial: 990_000_001);

        Assert.Equal(990_000_001, rango.TomarSiguienteConsecutivo(Hoy));
    }

    [Fact]
    public void Los_numeros_se_entregan_en_orden_y_sin_huecos()
    {
        var rango = Crear(numeroInicial: 1, numeroFinal: 5);

        var entregados = Enumerable.Range(0, 5)
            .Select(_ => rango.TomarSiguienteConsecutivo(Hoy))
            .ToArray();

        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, entregados);
    }

    [Fact]
    public void Cada_numero_entregado_reduce_los_disponibles()
    {
        var rango = Crear(numeroInicial: 1, numeroFinal: 10);

        rango.TomarSiguienteConsecutivo(Hoy);
        rango.TomarSiguienteConsecutivo(Hoy);
        rango.TomarSiguienteConsecutivo(Hoy);

        Assert.Equal(3, rango.UltimoAsignado);
        Assert.Equal(7, rango.NumerosDisponibles);
    }

    // ── INV-RAN-04: agotado y vencido no entregan ──

    [Fact]
    public void Un_rango_agotado_no_entrega_mas_numeros()
    {
        var rango = Crear(numeroInicial: 1, numeroFinal: 3);

        rango.TomarSiguienteConsecutivo(Hoy);
        rango.TomarSiguienteConsecutivo(Hoy);
        rango.TomarSiguienteConsecutivo(Hoy);

        Assert.True(rango.Agotado);
        Assert.Equal(0, rango.NumerosDisponibles);

        var error = Assert.Throws<ExcepcionDominio>(
            () => rango.TomarSiguienteConsecutivo(Hoy));

        Assert.Equal("RANGO_AGOTADO", error.Codigo);
    }

    [Fact]
    public void El_ultimo_numero_del_rango_si_se_entrega()
    {
        // El limite es inclusivo: si la autorizacion va del 1 al 3, el 3 se
        // puede usar. Probar el borde exacto es lo que distingue < de <=.
        var rango = Crear(numeroInicial: 1, numeroFinal: 3);

        rango.TomarSiguienteConsecutivo(Hoy);
        rango.TomarSiguienteConsecutivo(Hoy);

        Assert.Equal(3, rango.TomarSiguienteConsecutivo(Hoy));
    }

    [Fact]
    public void Un_rango_vencido_no_entrega_numeros()
    {
        var rango = Crear(
            desde: new DateOnly(2025, 1, 1),
            hasta: new DateOnly(2025, 12, 31));

        var error = Assert.Throws<ExcepcionDominio>(
            () => rango.TomarSiguienteConsecutivo(Hoy));

        Assert.Equal("RANGO_VENCIDO", error.Codigo);
    }

    [Fact]
    public void Un_rango_que_aun_no_empieza_no_entrega_numeros()
    {
        var rango = Crear(
            desde: new DateOnly(2027, 1, 1),
            hasta: new DateOnly(2027, 12, 31));

        var error = Assert.Throws<ExcepcionDominio>(
            () => rango.TomarSiguienteConsecutivo(Hoy));

        Assert.Equal("RANGO_VENCIDO", error.Codigo);
    }

    [Theory]
    [InlineData("2026-01-01", true)]    // primer dia, inclusive
    [InlineData("2026-06-15", true)]
    [InlineData("2026-12-31", true)]    // ultimo dia, inclusive
    [InlineData("2025-12-31", false)]   // un dia antes
    [InlineData("2027-01-01", false)]   // un dia despues
    public void La_vigencia_incluye_sus_dos_extremos(string fecha, bool vigente)
    {
        var rango = Crear(
            desde: new DateOnly(2026, 1, 1),
            hasta: new DateOnly(2026, 12, 31));

        Assert.Equal(vigente, rango.EstaVigenteEn(DateOnly.Parse(fecha)));
    }

    // ── RF-10: avisos preventivos ──

    [Fact]
    public void Informa_cuantos_dias_faltan_para_vencer()
    {
        var rango = Crear(hasta: new DateOnly(2026, 10, 27));

        Assert.Equal(30, rango.DiasParaVencimiento(Hoy));
    }

    [Fact]
    public void Los_dias_son_negativos_si_ya_vencio()
    {
        var rango = Crear(
            desde: new DateOnly(2025, 1, 1),
            hasta: new DateOnly(2026, 9, 20));

        Assert.Equal(-7, rango.DiasParaVencimiento(Hoy));
    }

    // ── INV-RAN-03: no se solapan ──

    [Fact]
    public void Dos_rangos_con_numeros_cruzados_se_solapan()
    {
        var uno = Crear(numeroInicial: 1, numeroFinal: 100);
        var otro = Crear(numeroInicial: 50, numeroFinal: 200);

        Assert.True(uno.SeSolapaCon(otro));
    }

    [Fact]
    public void Dos_rangos_vigentes_al_mismo_tiempo_se_solapan()
    {
        // Aunque los numeros no se crucen: la emision no debe tener que
        // elegir entre dos rangos vigentes del mismo tipo.
        var uno = Crear(
            prefijo: "SETP",
            numeroInicial: 1, numeroFinal: 100,
            desde: new DateOnly(2026, 1, 1), hasta: new DateOnly(2026, 12, 31));

        var otro = Crear(
            prefijo: "FE",
            numeroInicial: 500, numeroFinal: 600,
            desde: new DateOnly(2026, 6, 1), hasta: new DateOnly(2027, 5, 31));

        Assert.True(uno.SeSolapaCon(otro));
    }

    [Fact]
    public void Dos_rangos_de_periodos_distintos_no_se_solapan()
    {
        var esteAno = Crear(
            numeroInicial: 1, numeroFinal: 100,
            desde: new DateOnly(2026, 1, 1), hasta: new DateOnly(2026, 12, 31));

        var proximo = Crear(
            numeroInicial: 101, numeroFinal: 200,
            desde: new DateOnly(2027, 1, 1), hasta: new DateOnly(2027, 12, 31));

        Assert.False(esteAno.SeSolapaCon(proximo));
    }

    [Fact]
    public void Rangos_de_tipos_distintos_no_se_solapan()
    {
        var facturas = Crear(tipo: TipoDocumento.Factura);
        var notas = Crear(tipo: TipoDocumento.NotaCredito);

        Assert.False(facturas.SeSolapaCon(notas));
    }

    [Fact]
    public void Un_rango_no_se_solapa_consigo_mismo()
    {
        var rango = Crear();

        Assert.False(rango.SeSolapaCon(rango));
    }
}
