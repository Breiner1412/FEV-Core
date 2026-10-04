using FevCore.Domain.Adquirentes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FevCore.Infrastructure.Persistencia;

public sealed class ConfiguracionAdquirente : IEntityTypeConfiguration<Adquirente>
{
    /// <summary>
    /// Nombre del indice de INV-ADQ-01. Publico porque el repositorio lo
    /// reconoce al traducir una violacion de unicidad.
    /// </summary>
    public const string IndiceIdentificacion = "ix_adquirentes_identificacion";

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
            //
            // Unico y PARCIAL, solo sobre los activos. La aplicacion lo
            // comprueba antes para poder dar un error con sentido, pero esa
            // comprobacion no resiste dos altas simultaneas: las dos pasan y
            // se guardan las dos. La base es la que cierra la carrera
            // (CLAUDE.md: la unicidad se duplica en la base a proposito).
            datos.HasIndex(d => new { d.TipoIdentificacion, d.Identificacion })
                .IsUnique()
                .HasFilter("\"Activo\"")
                .HasDatabaseName(IndiceIdentificacion);
        });

        adquirente.Navigation(a => a.Datos).IsRequired();
    }
}
