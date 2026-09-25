using FevCore.Domain.Integradores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FevCore.Infrastructure.Persistencia;

public sealed class ConfiguracionIntegrador : IEntityTypeConfiguration<Integrador>
{
    public void Configure(EntityTypeBuilder<Integrador> integrador)
    {
        integrador.ToTable("integradores");

        integrador.HasKey(i => i.Id);

        integrador.Property(i => i.Nombre)
            .HasMaxLength(100)
            .IsRequired();

        // SHA-256 en hexadecimal: siempre 64 caracteres.
        integrador.Property(i => i.LlaveHash)
            .HasMaxLength(64)
            .IsRequired();

        integrador.Property(i => i.Activo).IsRequired();
        integrador.Property(i => i.CreadoEn).IsRequired();
        integrador.Property(i => i.UltimoAccesoEn);

        // La busqueda por huella ocurre en CADA peticion autenticada.
        // Sin este indice seria un recorrido completo de la tabla cada vez.
        integrador.HasIndex(i => i.LlaveHash)
            .IsUnique()
            .HasDatabaseName("ix_integradores_llave_hash");
    }
}
