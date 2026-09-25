using FevCore.Domain.Documentos;
using FevCore.Domain.Integradores;
using Microsoft.EntityFrameworkCore;

namespace FevCore.Infrastructure.Persistencia;

/// <summary>
/// Sesion de trabajo contra la base de datos.
///
/// Solo expone los agregados raiz. Las lineas y los impuestos se alcanzan
/// a traves de su documento, nunca por separado: eso es lo que significa
/// que Documento sea la raiz de su agregado (seccion 5 del modelo de dominio).
/// </summary>
public sealed class FevCoreDbContext(DbContextOptions<FevCoreDbContext> opciones)
    : DbContext(opciones)
{
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<Integrador> Integradores => Set<Integrador>();

    protected override void OnModelCreating(ModelBuilder constructor)
    {
        // Toda la configuracion del mapeo vive en esta carpeta, no en las
        // entidades: el dominio no sabe que existe una base de datos (ADR-0004).
        constructor.ApplyConfigurationsFromAssembly(
            typeof(FevCoreDbContext).Assembly);
    }
}
