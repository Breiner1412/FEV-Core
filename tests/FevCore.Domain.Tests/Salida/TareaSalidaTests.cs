using FevCore.Domain.Comun;
using FevCore.Domain.Salida;

namespace FevCore.Domain.Tests.Salida;

/// <summary>
/// La tarea de la bandeja de salida: cuando se puede tomar, cuanto espera
/// tras fallar, y cuando se da por abandonada (RNF-04, RNF-05).
///
/// Estas reglas se prueban aqui y no contra la base de datos por una razon
/// practica: la espera creciente llega a minutos. Probarla de verdad seria
/// esperar minutos. En el dominio el reloj es un parametro, asi que el
/// intento numero diez ocurre ahora mismo.
/// </summary>
public sealed class TareaSalidaTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 3, 15, 10, 0, 0, TimeSpan.FromHours(-5));

    private static readonly TimeSpan SinTecho = TimeSpan.FromDays(1);

    private static TareaSalida Crear() =>
        TareaSalida.Crear(Guid.CreateVersion7(), TipoTarea.Emitir, Ahora);

    [Fact]
    public void Una_tarea_recien_creada_esta_lista_de_inmediato()
    {
        var tarea = Crear();

        // Sin espera inicial: el documento acaba de emitirse y lo que se
        // quiere es transmitirlo ya.
        Assert.Equal(Ahora, tarea.ProximoIntentoEn);
        Assert.Equal(0, tarea.Intentos);
        Assert.Null(tarea.TomadaEn);
        Assert.False(tarea.Completada);
    }

    [Fact]
    public void Una_tarea_sin_documento_no_tiene_sentido()
    {
        var error = Assert.Throws<ExcepcionDominio>(() =>
            TareaSalida.Crear(Guid.Empty, TipoTarea.Emitir, Ahora));

        Assert.Equal("TAREA_SIN_DOCUMENTO", error.Codigo);
    }

    [Fact]
    public void Tomar_una_tarea_cuenta_el_intento()
    {
        var tarea = Crear();

        tarea.Tomar(Ahora);

        // El intento se cuenta al TOMAR, no al terminar. Si se contara al
        // terminar, un proceso que muere a mitad no dejaria rastro y la
        // tarea podria reintentarse para siempre.
        Assert.Equal(1, tarea.Intentos);
        Assert.Equal(Ahora, tarea.TomadaEn);
    }

    [Fact]
    public void Una_tarea_completada_no_se_vuelve_a_tomar()
    {
        var tarea = Crear();
        tarea.Tomar(Ahora);
        tarea.Completar(Ahora);

        var error = Assert.Throws<ExcepcionDominio>(() => tarea.Tomar(Ahora));

        Assert.Equal("TAREA_COMPLETADA", error.Codigo);
    }

    [Fact]
    public void Completar_libera_la_tarea_y_borra_el_ultimo_error()
    {
        var tarea = Crear();
        tarea.Tomar(Ahora);
        tarea.Reprogramar("se cayo", Ahora, SinTecho);
        tarea.Tomar(Ahora);

        tarea.Completar(Ahora);

        Assert.True(tarea.Completada);
        Assert.Null(tarea.TomadaEn);

        // El error de un intento anterior que acabo bien no es informacion,
        // es ruido para quien despues mire por que fallo algo.
        Assert.Null(tarea.UltimoError);
    }

    // ── Espera creciente (RNF-05) ──

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    public void La_espera_se_duplica_con_cada_intento(int intentos, int segundosEsperados)
    {
        var tarea = Crear();

        for (var i = 0; i < intentos; i++)
        {
            tarea.Tomar(Ahora);
            tarea.Reprogramar("el servicio no responde", Ahora, SinTecho);
        }

        // Duplicar y no sumar: un servicio caido recibe peticiones de TODOS
        // los documentos pendientes a la vez. Con espera lineal, el sistema
        // sigue martilleando a un servicio que ya estaba teniendo un mal dia.
        Assert.Equal(Ahora.AddSeconds(segundosEsperados), tarea.ProximoIntentoEn);
    }

    [Fact]
    public void La_espera_nunca_pasa_del_techo()
    {
        var tarea = Crear();
        var techo = TimeSpan.FromMinutes(5);

        for (var i = 0; i < 12; i++)
        {
            tarea.Tomar(Ahora);
            tarea.Reprogramar("el servicio no responde", Ahora, techo);
        }

        // Sin techo, 2^12 segundos son mas de una hora, y 2^15 mas de nueve.
        // Un documento no puede quedarse esperando media jornada porque el
        // servicio estuvo caido un rato por la manana.
        Assert.Equal(Ahora + techo, tarea.ProximoIntentoEn);
    }

    [Fact]
    public void Reprogramar_libera_la_tarea_y_guarda_el_motivo()
    {
        var tarea = Crear();
        tarea.Tomar(Ahora);

        tarea.Reprogramar("503 Service Unavailable", Ahora, SinTecho);

        Assert.Null(tarea.TomadaEn);
        Assert.Equal("503 Service Unavailable", tarea.UltimoError);
    }

    [Fact]
    public void Un_error_larguisimo_no_desborda_la_columna()
    {
        var tarea = Crear();
        tarea.Tomar(Ahora);

        tarea.Reprogramar(new string('x', 5000), Ahora, SinTecho);

        // Un servicio puede devolver una pagina de error entera. Guardarla
        // completa haria fallar la escritura, y entonces se perderia tambien
        // el contador de intentos: la tarea reintentaria para siempre.
        Assert.Equal(1000, tarea.UltimoError!.Length);
    }

    // ── Rendirse ──

    [Fact]
    public void Agotar_cierra_la_tarea_conservando_el_motivo()
    {
        var tarea = Crear();
        tarea.Tomar(Ahora);

        tarea.Agotar("sin respuesta tras 5 intentos", Ahora);

        Assert.True(tarea.Completada);
        Assert.Null(tarea.TomadaEn);
        Assert.Equal("sin respuesta tras 5 intentos", tarea.UltimoError);
    }

    [Theory]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    public void Agoto_intentos_compara_contra_el_maximo(int intentos, int maximo, bool esperado)
    {
        var tarea = Crear();

        for (var i = 0; i < intentos; i++)
        {
            tarea.Tomar(Ahora);
            tarea.Reprogramar("fallo", Ahora, SinTecho);
        }

        Assert.Equal(esperado, tarea.AgotoIntentos(maximo));
    }
}
