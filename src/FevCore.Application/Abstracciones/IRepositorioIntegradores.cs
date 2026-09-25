using FevCore.Domain.Integradores;

namespace FevCore.Application.Abstracciones;

/// <summary>
/// Lo que la capa de aplicacion necesita para trabajar con integradores.
///
/// La declara Application, la implementa Infrastructure. Asi el dominio y
/// los casos de uso no dependen de la base de datos (ADR-0002).
/// </summary>
public interface IRepositorioIntegradores
{
    /// <summary>
    /// Busca por la huella de la llave. Devuelve null si no existe.
    /// Incluye los inactivos: quien llama decide que hacer con ellos.
    /// </summary>
    Task<Integrador?> BuscarPorHashLlaveAsync(
        string llaveHash,
        CancellationToken cancelacion = default);

    Task AgregarAsync(
        Integrador integrador,
        CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}
