using FevCore.Domain.Emisores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FevCore.Infrastructure.Persistencia;

public sealed class ConfiguracionEmisor : IEntityTypeConfiguration<Emisor>
{
    public void Configure(EntityTypeBuilder<Emisor> emisor)
    {
        emisor.ToTable("emisores");

        emisor.HasKey(e => e.Id);

        emisor.Property(e => e.NombreComercial).HasMaxLength(300);
        emisor.Property(e => e.ActualizadoEn).IsRequired();

        emisor.OwnsOne(e => e.Datos, ConfiguracionDatosTributarios.Configurar);
        emisor.Navigation(e => e.Datos).IsRequired();
    }
}
