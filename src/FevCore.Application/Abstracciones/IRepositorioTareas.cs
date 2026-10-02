using FevCore.Domain.Salida;

namespace FevCore.Application.Abstracciones;

public interface IRepositorioTareas
{
    /// <summary>
    /// Toma en exclusiva la siguiente tarea que toque, o nada si no hay.
    ///
    /// "Que toque" significa: no completada, con su momento de proximo
    /// intento ya cumplido, y libre — o tomada hace tanto que quien la tenia
    /// evidentemente murio (RNF-04).
    ///
    /// La toma es atomica: dos trabajadores no pueden llevarse la misma.
    /// </summary>
    Task<TareaSalida?> TomarSiguienteAsync(
        DateTimeOffset momento,
        TimeSpan tiempoDeAbandono,
        CancellationToken cancelacion = default);

    Task AgregarAsync(TareaSalida tarea, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}
