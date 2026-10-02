using FevCore.Domain.Adquirentes;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Domain.Emisores;
using FevCore.Domain.Productos;

namespace FevCore.Domain.Tests.Catalogos;

public sealed class EmisorTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 9, 27, 14, 0, 0, TimeSpan.FromHours(-5));

    private static DatosTributarios DatosValidos(
        IEnumerable<string>? responsabilidades = null) =>
        DatosTributarios.Crear(
            tipoIdentificacion: DatosTributarios.TipoNit,
            identificacion: "800197268",
            razonSocial: "Comercializadora del Eje SAS",
            direccion: "Calle 20 # 8-45",
            municipioCodigo: "66001",
            regimen: "48",
            digitoVerificacion: "4",
            responsabilidades: responsabilidades ?? ["O-13"]);

    [Fact]
    public void Se_crea_con_sus_datos()
    {
        var emisor = Emisor.Crear(DatosValidos(), Ahora, "Comercializadora del Eje");

        Assert.Equal("Comercializadora del Eje SAS", emisor.Datos.RazonSocial);
        Assert.Equal("Comercializadora del Eje", emisor.NombreComercial);
        Assert.Equal(Ahora, emisor.ActualizadoEn);
    }

    // ── INV-EMI-03 ──

    [Fact]
    public void Rechaza_un_emisor_sin_responsabilidades_tributarias()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => Emisor.Crear(DatosValidos(responsabilidades: []), Ahora));

        Assert.Equal("EMISOR_SIN_RESPONSABILIDADES", error.Codigo);
    }

    [Fact]
    public void Actualizar_cambia_los_datos_y_la_marca_de_tiempo()
    {
        var emisor = Emisor.Crear(DatosValidos(), Ahora);
        var despues = Ahora.AddDays(1);

        var nuevos = DatosTributarios.Crear(
            tipoIdentificacion: DatosTributarios.TipoNit,
            identificacion: "890903938",
            razonSocial: "Nueva Razon Social SAS",
            direccion: "Avenida 30 de Agosto # 40-20",
            municipioCodigo: "66001",
            regimen: "48",
            digitoVerificacion: "8",
            responsabilidades: ["O-13", "O-15"]);

        emisor.Actualizar(nuevos, despues);

        Assert.Equal("Nueva Razon Social SAS", emisor.Datos.RazonSocial);
        Assert.Equal(despues, emisor.ActualizadoEn);
    }
}

public sealed class AdquirenteTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 9, 27, 14, 0, 0, TimeSpan.FromHours(-5));

    private static DatosTributarios Datos(string identificacion = "1088123456") =>
        DatosTributarios.Crear(
            tipoIdentificacion: "13",
            identificacion: identificacion,
            razonSocial: "Juan Perez",
            direccion: "Carrera 10 # 5-20",
            municipioCodigo: "66001",
            regimen: "49");

    [Fact]
    public void Se_crea_activo()
    {
        var adquirente = Adquirente.Crear(Datos(), Ahora);

        Assert.True(adquirente.Activo);
        Assert.Equal(Ahora, adquirente.CreadoEn);
    }

    // ── INV-ADQ-02: nunca se elimina, solo se desactiva ──

    [Fact]
    public void Desactivar_lo_saca_de_circulacion_sin_borrarlo()
    {
        var adquirente = Adquirente.Crear(Datos(), Ahora);
        var despues = Ahora.AddDays(1);

        adquirente.Desactivar(despues);

        Assert.False(adquirente.Activo);
        // Sigue existiendo, con sus datos intactos: los documentos que lo
        // referencian no pierden la trazabilidad.
        Assert.Equal("Juan Perez", adquirente.Datos.RazonSocial);
        Assert.Equal(despues, adquirente.ActualizadoEn);
    }
}

public sealed class ProductoTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 9, 27, 14, 0, 0, TimeSpan.FromHours(-5));

    private static Producto Crear(
        decimal precio = 150_000m,
        IEnumerable<EspecificacionImpuesto>? impuestos = null) =>
        Producto.Crear(
            codigo: "PROD-001",
            descripcion: "Teclado mecanico",
            unidadMedida: "94",
            precioUnitario: Dinero.Desde(precio),
            momento: Ahora,
            impuestos: impuestos ?? [new EspecificacionImpuesto(TipoImpuesto.Iva, 19m)]);

    [Fact]
    public void Se_crea_activo_con_su_precio_de_referencia()
    {
        var producto = Crear();

        Assert.True(producto.Activo);
        Assert.Equal(150_000m, producto.PrecioUnitario.Valor);
        Assert.Single(producto.Impuestos);
    }

    [Fact]
    public void Un_producto_puede_no_llevar_impuestos()
    {
        var producto = Crear(impuestos: []);

        Assert.Empty(producto.Impuestos);
    }

    [Fact]
    public void Rechaza_dos_impuestos_del_mismo_tipo()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => Crear(impuestos:
        [
            new EspecificacionImpuesto(TipoImpuesto.Iva, 19m),
            new EspecificacionImpuesto(TipoImpuesto.Iva, 5m)
        ]));

        Assert.Equal("PRODUCTO_IMPUESTO_DUPLICADO", error.Codigo);
    }

    [Fact]
    public void Rechaza_una_tarifa_negativa()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => Crear(impuestos:
        [
            new EspecificacionImpuesto(TipoImpuesto.Iva, -19m)
        ]));

        Assert.Equal("IMPUESTO_TARIFA_INVALIDA", error.Codigo);
    }

    [Fact]
    public void Rechaza_un_codigo_vacio()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => Producto.Crear(
            codigo: "   ",
            descripcion: "Teclado mecanico",
            unidadMedida: "94",
            precioUnitario: Dinero.Desde(1_000m),
            momento: Ahora));

        Assert.Equal("PRODUCTO_CODIGO_REQUERIDO", error.Codigo);
    }

    [Fact]
    public void Actualizar_cambia_el_precio_de_referencia()
    {
        var producto = Crear(precio: 150_000m);
        var despues = Ahora.AddDays(1);

        producto.Actualizar(
            codigo: "PROD-001",
            descripcion: "Teclado mecanico",
            unidadMedida: "94",
            precioUnitario: Dinero.Desde(180_000m),
            momento: despues,
            impuestos: [new EspecificacionImpuesto(TipoImpuesto.Iva, 19m)]);

        Assert.Equal(180_000m, producto.PrecioUnitario.Valor);
        Assert.Equal(despues, producto.ActualizadoEn);
    }

    [Fact]
    public void Actualizar_reemplaza_la_lista_de_impuestos_completa()
    {
        var producto = Crear(impuestos:
        [
            new EspecificacionImpuesto(TipoImpuesto.Iva, 19m),
            new EspecificacionImpuesto(TipoImpuesto.Inc, 8m)
        ]);

        producto.Actualizar(
            codigo: "PROD-001",
            descripcion: "Teclado mecanico",
            unidadMedida: "94",
            precioUnitario: Dinero.Desde(150_000m),
            momento: Ahora.AddDays(1),
            impuestos: [new EspecificacionImpuesto(TipoImpuesto.Iva, 19m)]);

        Assert.Single(producto.Impuestos);
        Assert.Equal(TipoImpuesto.Iva, producto.Impuestos.Single().Tipo);
    }

    [Fact]
    public void Desactivar_lo_saca_del_catalogo_sin_borrarlo()
    {
        var producto = Crear();

        producto.Desactivar(Ahora.AddDays(1));

        Assert.False(producto.Activo);
        Assert.Equal("PROD-001", producto.Codigo);
    }
}
