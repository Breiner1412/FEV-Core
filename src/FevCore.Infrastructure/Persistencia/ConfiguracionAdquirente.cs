using FevCore.Domain.Adquirentes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FevCore.Infrastructure.Persistencia;

public sealed class ConfiguracionAdquirente : IEntityTypeConfiguration<Adquirente>
{
    public void Configure(EntityTypeBuilder<Adquirente> adquirente)
    {
        adquirente.ToTable("adquirentes");

        adquirente.HasKey(a => a.Id);

        adquirente.Property(a => a.Activo).IsRequired();
        adquirente.Property(a => a.CreadoEn).IsRequired();
        adquirente.Property(a => a.ActualizadoEn).IsRequired();

        adquirente.OwnsOne(a => a.Datos, datos =>
        {
            ConfiguracionDatosTributarios.Configurar(datos);

            // ── INV-ADQ-01 ──
            // No pueden existir dos adquirentes ACTIVOS con la misma
            // identificacion. Uno desactivado no estorba, porque ya no se
            // puede usar para emitir.
            //
            // El indice se declara sobre la tabla del adquirente aunque las
            // columnas vengan del objeto incrustado: para la base de datos
            // son columnas de la misma fila.
            datos.HasIndex(d => new { d.TipoIdentificacion, d.Identificacion })
                .HasDatabaseName("ix_adquirentes_identificacion");
        });

        adquirente.Navigation(a => a.Datos).IsRequired();
    }
}
