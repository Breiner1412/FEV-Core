using FevCore.Domain.Comun;

namespace FevCore.Domain.Tests.Comun;

public sealed class DatosTributariosTests
{
    private static DatosTributarios Crear(
        string tipo = DatosTributarios.TipoNit,
        string identificacion = "800197268",
        string? dv = "4",
        string razonSocial = "Comercializadora del Eje SAS",
        IEnumerable<string>? responsabilidades = null) =>
        DatosTributarios.Crear(
            tipoIdentificacion: tipo,
            identificacion: identificacion,
            razonSocial: razonSocial,
            direccion: "Calle 20 # 8-45",
            municipioCodigo: "66001",
            regimen: "48",
            digitoVerificacion: dv,
            correo: "facturacion@ejemplo.com",
            responsabilidades: responsabilidades ?? ["O-13", "O-15"]);

    // ── Digito de verificacion (INV-EMI-01) ──

    [Theory]
    [InlineData("800197268", 4)]   // DIAN
    [InlineData("890903938", 8)]   // Bancolombia
    [InlineData("860002964", 4)]   // Banco de Bogota
    public void Calcula_el_digito_de_verificacion_de_nit_reales(string nit, int esperado)
    {
        Assert.Equal(esperado, DatosTributarios.CalcularDigitoVerificacion(nit));
    }

    [Fact]
    public void Acepta_un_nit_con_su_digito_correcto()
    {
        var datos = Crear(identificacion: "890903938", dv: "8");

        Assert.Equal("890903938", datos.Identificacion);
        Assert.Equal("8", datos.DigitoVerificacion);
    }

    [Fact]
    public void Rechaza_un_nit_con_digito_equivocado()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => Crear(identificacion: "800197268", dv: "9"));

        Assert.Equal("DIGITO_VERIFICACION_INCORRECTO", error.Codigo);
    }

    [Fact]
    public void Rechaza_un_nit_sin_digito_de_verificacion()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => Crear(dv: null));

        Assert.Equal("DIGITO_VERIFICACION_REQUERIDO", error.Codigo);
    }

    [Fact]
    public void Detecta_dos_digitos_intercambiados()
    {
        // Es para esto que el calculo usa pesos distintos por posicion:
        // el error de digitacion mas comun es cambiar dos digitos de orden,
        // y una suma simple no lo notaria.
        var original = DatosTributarios.CalcularDigitoVerificacion("800197268");
        var trastocado = DatosTributarios.CalcularDigitoVerificacion("800197286");

        Assert.NotEqual(original, trastocado);
    }

    [Fact]
    public void Rechaza_un_nit_con_letras()
    {
        var error = Assert.Throws<ExcepcionDominio>(
            () => Crear(identificacion: "80019A268", dv: "4"));

        Assert.Equal("NIT_INVALIDO", error.Codigo);
    }

    [Fact]
    public void No_exige_digito_de_verificacion_a_una_cedula()
    {
        // El digito de verificacion es propio del NIT. Una cedula no lo lleva.
        var datos = DatosTributarios.Crear(
            tipoIdentificacion: "13",
            identificacion: "1088123456",
            razonSocial: "Juan Perez",
            direccion: "Carrera 10 # 5-20",
            municipioCodigo: "66001",
            regimen: "49");

        Assert.Null(datos.DigitoVerificacion);
    }

    // ── Datos obligatorios ──

    [Fact]
    public void Rechaza_una_razon_social_vacia()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => Crear(razonSocial: "   "));

        Assert.Equal("RAZON_SOCIAL_REQUERIDA", error.Codigo);
    }

    [Fact]
    public void Rechaza_un_correo_sin_arroba()
    {
        var error = Assert.Throws<ExcepcionDominio>(() => DatosTributarios.Crear(
            tipoIdentificacion: "13",
            identificacion: "1088123456",
            razonSocial: "Juan Perez",
            direccion: "Carrera 10 # 5-20",
            municipioCodigo: "66001",
            regimen: "49",
            correo: "esto-no-es-un-correo"));

        Assert.Equal("CORREO_INVALIDO", error.Codigo);
    }

    // ── Normalizacion ──

    [Fact]
    public void Recorta_los_espacios_sobrantes()
    {
        var datos = DatosTributarios.Crear(
            tipoIdentificacion: " 13 ",
            identificacion: " 1088123456 ",
            razonSocial: "  Juan Perez  ",
            direccion: " Carrera 10 # 5-20 ",
            municipioCodigo: " 66001 ",
            regimen: " 49 ");

        Assert.Equal("13", datos.TipoIdentificacion);
        Assert.Equal("Juan Perez", datos.RazonSocial);
        Assert.Equal("66001", datos.MunicipioCodigo);
    }

    [Fact]
    public void No_repite_responsabilidades()
    {
        var datos = Crear(responsabilidades: ["O-13", "O-13", "O-15"]);

        Assert.Equal(2, datos.Responsabilidades.Count);
    }

    // ── Semantica de valor ──

    [Fact]
    public void Dos_datos_iguales_son_iguales()
    {
        // Es un objeto de valor: lo que lo define es su contenido, no su
        // identidad. Eso es lo que permite copiarlo a un documento y que la
        // copia siga siendo "los mismos datos".
        Assert.Equal(Crear(), Crear());
    }

    [Fact]
    public void Dos_datos_con_responsabilidades_distintas_no_son_iguales()
    {
        // El complemento de la prueba anterior. Sin el, una implementacion
        // de Equals que ignorara la lista de responsabilidades pasaria
        // igual: siempre diria "iguales".
        var unos = Crear(responsabilidades: ["O-13"]);
        var otros = Crear(responsabilidades: ["O-13", "O-15"]);

        Assert.NotEqual(unos, otros);
    }

    [Fact]
    public void Dos_datos_iguales_comparten_codigo_de_dispersion()
    {
        // Si Equals dice que son iguales, GetHashCode debe coincidir.
        // Romper esa correspondencia hace que los diccionarios y conjuntos
        // pierdan elementos de formas dificiles de diagnosticar.
        Assert.Equal(Crear().GetHashCode(), Crear().GetHashCode());
    }

    // ── Copia para instantaneas (RN-10) ──

    [Fact]
    public void Copiar_devuelve_una_instancia_distinta()
    {
        var original = Crear();

        var copia = original.Copiar();

        // Que sean objetos distintos es LO QUE SE PRUEBA, no un detalle:
        // si copiar devolviera el mismo objeto, un documento emitido y la
        // parte compartirian datos, y cambiar el maestro reescribiria lo
        // que ya se emitio.
        Assert.NotSame(original, copia);
    }

    [Fact]
    public void Copiar_conserva_todo_el_contenido()
    {
        var original = Crear(responsabilidades: ["O-13", "O-15"]);

        var copia = original.Copiar();

        Assert.Equal(original, copia);
    }

    [Fact]
    public void Copiar_no_comparte_la_lista_de_responsabilidades()
    {
        var original = Crear(responsabilidades: ["O-13", "O-15"]);

        var copia = original.Copiar();

        // Clonar un record clona sus campos, y un campo que es una
        // referencia se clona como referencia: las dos copias acabarian
        // apuntando a la misma lista.
        Assert.NotSame(original.Responsabilidades, copia.Responsabilidades);
        Assert.Equal(original.Responsabilidades, copia.Responsabilidades);
    }
}
