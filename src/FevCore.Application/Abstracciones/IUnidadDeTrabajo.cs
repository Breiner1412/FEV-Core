namespace FevCore.Application.Abstracciones;

/// <summary>
/// Una transaccion en curso.
///
/// Si se descarta sin confirmar, todo lo hecho dentro se revierte. Por eso
/// el caso de uso la declara con "await using": si algo lanza una excepcion
/// a mitad, el numero consumido y el documento a medio guardar desaparecen
/// juntos.
/// </summary>
public interface ITransaccion : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken cancelacion = default);
}

/// <summary>
/// Permite a la capa de aplicacion abrir una transaccion sin saber que
/// existe Entity Framework (ADR-0002).
///
/// Hace falta porque el bloqueo del rango y el guardado del documento deben
/// ocurrir dentro de la MISMA transaccion: si el documento no llega a
/// guardarse, el consecutivo no debe quedar consumido.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancelacion = default);
}
