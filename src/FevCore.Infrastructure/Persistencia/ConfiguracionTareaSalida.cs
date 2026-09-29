using FevCore.Domain.Salida;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FevCore.Infrastructure.Persistencia;

public sealed class ConfiguracionTareaSalida : IEntityTypeConfiguration<TareaSalida>
{
    public void Configure(EntityTypeBuilder<TareaSalida> tarea)
    {
        tarea.ToTable("tareas_salida");

        tarea.HasKey(t => t.Id);

        tarea.Property(t => t.DocumentoId).IsRequired();

        tarea.Property(t => t.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        tarea.Property(t => t.CreadaEn).IsRequired();
        tarea.Property(t => t.Intentos).IsRequired();
        tarea.Property(t => t.ProximoIntentoEn).IsRequired();
        tarea.Property(t => t.TomadaEn);
        tarea.Property(t => t.CompletadaEn);
        tarea.Property(t => t.UltimoError).HasMaxLength(1000);

        tarea.Ignore(t => t.Completada);

        // Integridad referencial sin navegacion: una tarea no arrastra su
        // documento entero cada vez que se carga. Es el mismo criterio que
        // con el documento referenciado de las notas, en H4.
        tarea.HasOne<Domain.Documentos.Documento>()
            .WithMany()
            .HasForeignKey(t => t.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        // EL indice que importa.
        //
        // La consulta del trabajador se ejecuta cada pocos segundos, para
        // siempre, y filtra por tarea pendiente cuyo momento ya llego. Sin
        // este indice seria un recorrido completo de una tabla que solo
        // crece, y el coste aumentaria con cada documento emitido en la
        // historia del sistema.
        //
        // El filtro parcial deja fuera las completadas, que son la inmensa
        // mayoria: el indice se mantiene pequeno aunque la tabla no lo sea.
        tarea.HasIndex(t => t.ProximoIntentoEn)
            .HasDatabaseName("ix_tareas_pendientes")
            .HasFilter("\"CompletadaEn\" IS NULL");

        tarea.HasIndex(t => new { t.DocumentoId, t.Tipo })
            .HasDatabaseName("ix_tareas_documento_tipo");
    }
}
