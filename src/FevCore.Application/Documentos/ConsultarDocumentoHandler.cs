using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Caso de uso: consultar un documento por su identificador (RF-22).
/// </summary>
public sealed class ConsultarDocumentoHandler(IRepositorioDocumentos repositorio)
{
    public Task<Documento?> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        repositorio.ObtenerPorIdAsync(id, cancelacion);
}

/// <summary>
/// Caso de uso: listar los documentos de un integrador (RF-24).
///
/// Los limites de pagina se imponen aqui y no solo en el controlador. El
/// controlador es una via de entrada; si manana hay otra, el limite tiene
/// que seguir en pie. Un tamano de pagina sin techo es una forma comoda de
/// pedirle a la base de datos que traiga cien mil documentos de una vez.
/// </summary>
public sealed class ListarDocumentosHandler(IRepositorioDocumentos repositorio)
{
    public const int TamanoPaginaMaximo = 100;
    public const int TamanoPaginaPorDefecto = 20;

    public Task<PaginaDe<ResumenDocumento>> EjecutarAsync(
        FiltroDocumentos filtro,
        CancellationToken cancelacion = default) =>
        repositorio.ListarAsync(
            filtro with
            {
                Pagina = Math.Max(1, filtro.Pagina),
                TamanoPagina = Math.Clamp(filtro.TamanoPagina, 1, TamanoPaginaMaximo)
            },
            cancelacion);
}
