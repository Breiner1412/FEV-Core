using FevCore.Domain.Numeracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FevCore.Infrastructure.Persistencia;

public sealed class ConfiguracionRangoNumeracion
    : IEntityTypeConfiguration<RangoNumeracion>
{
    public void Configure(EntityTypeBuilder<RangoNumeracion> rango)
    {
        rango.ToTable("rangos_numeracion");

        rango.HasKey(r => r.Id);

        rango.Property(r => r.Prefijo)
            .HasMaxLength(10)
            .IsRequired();

        rango.Property(r => r.TipoDocumento)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        rango.Property(r => r.NumeroInicial).IsRequired();
        rango.Property(r => r.NumeroFinal).IsRequired();

        // DateOnly se guarda como date, sin hora ni zona horaria: una
        // autorizacion vale "hasta el 31 de diciembre", no hasta un instante.
        rango.Property(r => r.VigenteDesde).HasColumnType("date").IsRequired();
        rango.Property(r => r.VigenteHasta).HasColumnType("date").IsRequired();

        rango.Property(r => r.UltimoAsignado);

        rango.Property(r => r.NumeroAutorizacion)
            .HasMaxLength(50)
            .IsRequired();

        rango.Property(r => r.ClaveTecnica)
            .HasMaxLength(200)
            .IsRequired();

        rango.Property(r => r.CreadoEn).IsRequired();

        // Valores calculados desde otros campos. No se guardan.
        rango.Ignore(r => r.NumerosDisponibles);
        rango.Ignore(r => r.Agotado);

        // La emision busca por tipo y fecha en cada factura.
        rango.HasIndex(r => new { r.TipoDocumento, r.VigenteDesde, r.VigenteHasta })
            .HasDatabaseName("ix_rangos_tipo_vigencia");

        rango.HasIndex(r => new { r.Prefijo, r.TipoDocumento })
            .HasDatabaseName("ix_rangos_prefijo_tipo");
    }
}
