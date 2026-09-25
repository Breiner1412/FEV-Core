using FevCore.Domain.Comun;
using FevCore.Domain.Integradores;

namespace FevCore.Domain.Tests.Integradores;

public sealed class IntegradorTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 9, 25, 17, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void Se_crea_activo_y_con_los_datos_dados()
    {
        var (integrador, _) = Integrador.Crear("ERP Contable", Ahora);

        Assert.Equal("ERP Contable", integrador.Nombre);
        Assert.True(integrador.Activo);
        Assert.Equal(Ahora, integrador.CreadoEn);
        Assert.Null(integrador.UltimoAccesoEn);
    }

    [Fact]
    public void Rechaza_un_nombre_vacio()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => Integrador.Crear("   ", Ahora));

        Assert.Equal("INTEGRADOR_NOMBRE_REQUERIDO", error.Codigo);
    }

    // ── INV-INT-01: la llave en claro no se guarda ──

    [Fact]
    public void La_llave_en_claro_no_queda_almacenada_en_la_entidad()
    {
        var (integrador, llaveEnClaro) = Integrador.Crear("ERP", Ahora);

        Assert.NotEqual(llaveEnClaro, integrador.LlaveHash);
        Assert.DoesNotContain(llaveEnClaro, integrador.LlaveHash);
    }

    [Fact]
    public void La_llave_lleva_el_prefijo_que_la_identifica()
    {
        var (_, llaveEnClaro) = Integrador.Crear("ERP", Ahora);

        Assert.StartsWith(Integrador.PrefijoLlave, llaveEnClaro);
    }

    [Fact]
    public void Dos_integradores_nunca_reciben_la_misma_llave()
    {
        var llaves = Enumerable.Range(0, 200)
            .Select(_ => Integrador.Crear("ERP", Ahora).LlaveEnClaro)
            .ToHashSet();

        Assert.Equal(200, llaves.Count);
    }

    [Fact]
    public void La_huella_de_una_llave_siempre_es_la_misma()
    {
        var (integrador, llaveEnClaro) = Integrador.Crear("ERP", Ahora);

        // Asi es como la autenticacion reconoce una llave: vuelve a calcular
        // su huella y la compara con la almacenada.
        Assert.Equal(integrador.LlaveHash, Integrador.CalcularHash(llaveEnClaro));
    }

    [Fact]
    public void Llaves_distintas_producen_huellas_distintas()
    {
        var huellaUna = Integrador.CalcularHash("fev_una");
        var huellaOtra = Integrador.CalcularHash("fev_otra");

        Assert.NotEqual(huellaUna, huellaOtra);
    }

    [Fact]
    public void La_huella_es_un_sha256_en_hexadecimal()
    {
        var (integrador, _) = Integrador.Crear("ERP", Ahora);

        Assert.Equal(64, integrador.LlaveHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", integrador.LlaveHash);
    }

    // ── Ciclo de vida ──

    [Fact]
    public void Registrar_acceso_deja_la_marca_de_tiempo()
    {
        var (integrador, _) = Integrador.Crear("ERP", Ahora);
        var despues = Ahora.AddMinutes(5);

        integrador.RegistrarAcceso(despues);

        Assert.Equal(despues, integrador.UltimoAccesoEn);
    }

    [Fact]
    public void Desactivar_lo_deja_inactivo()
    {
        var (integrador, _) = Integrador.Crear("ERP", Ahora);

        integrador.Desactivar();

        Assert.False(integrador.Activo);
    }
}
