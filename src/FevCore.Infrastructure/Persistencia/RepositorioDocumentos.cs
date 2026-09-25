using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;
using Microsoft.EntityFrameworkCore;

namespace FevCore.Infrastructure.Persistencia;

public sealed class RepositorioDocumentos(FevCoreDbContext contexto)
    : IRepositorioDocumentos
{
    public Task<Documento?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        contexto.Documentos
            .FirstOrDefaultAsync(d => d.Id == id, cancelacion);

    public Task<Documento?> BuscarPorReferenciaExternaAsync(
        Guid integradorId,
        string referenciaExterna,
        CancellationToken cancelacion = default) =>
        contexto.Documentos
            .FirstOrDefaultAsync(
                d => d.IntegradorId == integradorId &&
                     d.ReferenciaExterna == referenciaExterna,
                cancelacion);

    public async Task<long> ObtenerSiguienteConsecutivoProvisionalAsync(
        string prefijo,
        CancellationToken cancelacion = default)
    {
        var maximo = await contexto.Documentos
            .Where(d => d.Prefijo == prefijo)
            .MaxAsync(d => (long?)d.Consecutivo, cancelacion);

        return (maximo ?? 0) + 1;
    }

    public async Task AgregarAsync(
        Documento documento,
        CancellationToken cancelacion = default) =>
        await contexto.Documentos.AddAsync(documento, cancelacion);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
