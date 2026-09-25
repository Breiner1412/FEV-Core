using FevCore.Application.Abstracciones;
using FevCore.Domain.Integradores;
using Microsoft.EntityFrameworkCore;

namespace FevCore.Infrastructure.Persistencia;

public sealed class RepositorioIntegradores(FevCoreDbContext contexto)
    : IRepositorioIntegradores
{
    public Task<Integrador?> BuscarPorHashLlaveAsync(
        string llaveHash,
        CancellationToken cancelacion = default) =>
        contexto.Integradores
            .FirstOrDefaultAsync(i => i.LlaveHash == llaveHash, cancelacion);

    public async Task AgregarAsync(
        Integrador integrador,
        CancellationToken cancelacion = default) =>
        await contexto.Integradores.AddAsync(integrador, cancelacion);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
