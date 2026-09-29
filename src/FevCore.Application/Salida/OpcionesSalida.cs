namespace FevCore.Application.Salida;

/// <summary>
/// Como se comporta la bandeja de salida (RNF-04, RNF-05).
///
/// Todo configurable: los valores buenos para produccion —esperar minutos
/// entre reintentos— harian que las pruebas tardaran minutos. Y los valores
/// buenos para pruebas —reintentar al instante— convertirian un servicio
/// caido en una tormenta de peticiones.
/// </summary>
public sealed class OpcionesSalida
{
    public const string Seccion = "Salida";

    /// <summary>Cada cuanto mira el trabajador si hay algo que hacer.</summary>
    public int IntervaloSondeoSegundos { get; set; } = 2;

    /// <summary>
    /// Cuantas veces se intenta antes de rendirse. Al agotarse, el documento
    /// pasa a FALLIDO, que significa resultado desconocido (RN-13).
    /// </summary>
    public int MaximoIntentos { get; set; } = 5;

    /// <summary>
    /// Techo de la espera creciente. Sin el, el intento numero quince
    /// esperaria nueve horas.
    /// </summary>
    public int EsperaMaximaSegundos { get; set; } = 300;

    /// <summary>
    /// Cuanto puede estar tomada una tarea antes de considerarla abandonada.
    ///
    /// Es el mecanismo de recuperacion ante un proceso que murio a mitad: su
    /// marca envejece y la tarea vuelve a estar disponible sola. Demasiado
    /// corto, y dos trabajadores procesarian la misma tarea a la vez;
    /// demasiado largo, y un reinicio dejaria documentos parados.
    /// </summary>
    public int TiempoDeAbandonoSegundos { get; set; } = 120;

    /// <summary>
    /// Permite apagar el trabajador. Las pruebas que quieren controlar
    /// cuando se procesa lo ponen en falso y llaman al procesador a mano.
    /// </summary>
    public bool Habilitado { get; set; } = true;

    public TimeSpan IntervaloSondeo => TimeSpan.FromSeconds(IntervaloSondeoSegundos);
    public TimeSpan EsperaMaxima => TimeSpan.FromSeconds(EsperaMaximaSegundos);
    public TimeSpan TiempoDeAbandono => TimeSpan.FromSeconds(TiempoDeAbandonoSegundos);
}
