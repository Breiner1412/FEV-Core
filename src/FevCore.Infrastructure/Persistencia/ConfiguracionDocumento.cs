using FevCore.Domain.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FevCore.Infrastructure.Persistencia;

/// <summary>
/// Mapeo del agregado Documento: el documento, sus totales, sus lineas
/// y los impuestos de cada linea.
/// </summary>
public sealed class ConfiguracionDocumento : IEntityTypeConfiguration<Documento>
{
    /// <summary>
    /// Precision de las columnas monetarias.
    ///
    /// Seis decimales, no dos: los impuestos de linea se guardan SIN
    /// redondear (RN-06). Un IVA de 190,0019 debe conservar sus cuatro
    /// decimales, porque el total del documento se calcula a partir de
    /// esos valores exactos.
    /// </summary>
    private const string TipoColumnaDinero = "numeric(18,6)";

    public void Configure(EntityTypeBuilder<Documento> documento)
    {
        documento.ToTable("documentos");

        documento.HasKey(d => d.Id);

        documento.Property(d => d.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        documento.Property(d => d.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        documento.Property(d => d.ReferenciaExterna)
            .HasMaxLength(64)
            .IsRequired();

        documento.Property(d => d.IntegradorId).IsRequired();

        documento.Property(d => d.Prefijo)
            .HasMaxLength(10)
            .IsRequired();

        documento.Property(d => d.Consecutivo).IsRequired();
        documento.Property(d => d.FechaEmision).IsRequired();

        documento.Property(d => d.Moneda)
            .HasMaxLength(3)
            .IsRequired();

        // NumeroCompleto se calcula desde prefijo y consecutivo. No se guarda.
        documento.Ignore(d => d.NumeroCompleto);

        // ── INV-DOC-05 (RN-01): un numero se asigna una sola vez ──
        // Esta restriccion es una red de seguridad. El mecanismo principal
        // es el bloqueo del rango de numeracion, que llega en H3 (ADR-0009).
        documento.HasIndex(d => new { d.Prefijo, d.Consecutivo })
            .IsUnique()
            .HasDatabaseName("ix_documentos_prefijo_consecutivo");

        // ── INV-DOC-06 (RF-15): reintentar no duplica ──
        documento.HasIndex(d => new { d.IntegradorId, d.ReferenciaExterna })
            .IsUnique()
            .HasDatabaseName("ix_documentos_integrador_referencia");

        // ── Totales: viven dentro del documento, en sus mismas columnas ──
        documento.OwnsOne(d => d.Totales, totales =>
        {
            totales.Property(t => t.TotalBruto)
                .HasConversion<ConvertidorDinero>()
                .HasColumnType(TipoColumnaDinero)
                .HasColumnName("TotalBruto");

            totales.Property(t => t.TotalDescuentos)
                .HasConversion<ConvertidorDinero>()
                .HasColumnType(TipoColumnaDinero)
                .HasColumnName("TotalDescuentos");

            totales.Property(t => t.TotalBaseImponible)
                .HasConversion<ConvertidorDinero>()
                .HasColumnType(TipoColumnaDinero)
                .HasColumnName("TotalBaseImponible");

            totales.Property(t => t.TotalImpuestos)
                .HasConversion<ConvertidorDinero>()
                .HasColumnType(TipoColumnaDinero)
                .HasColumnName("TotalImpuestos");

            totales.Property(t => t.TotalAPagar)
                .HasConversion<ConvertidorDinero>()
                .HasColumnType(TipoColumnaDinero)
                .HasColumnName("TotalAPagar");
        });

        documento.Navigation(d => d.Totales).IsRequired();

        // ── Lineas: pertenecen al documento y no existen sin el ──
        documento.OwnsMany(d => d.Lineas, linea =>
        {
            linea.ToTable("lineas");
            linea.WithOwner().HasForeignKey("DocumentoId");

            linea.Property(l => l.Numero).IsRequired();
            linea.Property(l => l.ProductoId);

            linea.Property(l => l.Codigo).HasMaxLength(50).IsRequired();
            linea.Property(l => l.Descripcion).HasMaxLength(500).IsRequired();
            linea.Property(l => l.UnidadMedida).HasMaxLength(10).IsRequired();

            linea.Property(l => l.Cantidad)
                .HasColumnType("numeric(18,6)")
                .IsRequired();

            linea.Property(l => l.PrecioUnitario)
                .HasConversion<ConvertidorDinero>()
                .HasColumnType(TipoColumnaDinero)
                .IsRequired();

            linea.Property(l => l.Descuento)
                .HasConversion<ConvertidorDinero>()
                .HasColumnType(TipoColumnaDinero)
                .IsRequired();

            linea.Property(l => l.BaseGravable)
                .HasConversion<ConvertidorDinero>()
                .HasColumnType(TipoColumnaDinero)
                .IsRequired();

            linea.Property(l => l.Total)
                .HasConversion<ConvertidorDinero>()
                .HasColumnType(TipoColumnaDinero)
                .IsRequired();

            // ── Impuestos: pertenecen a la linea ──
            linea.OwnsMany(l => l.Impuestos, impuesto =>
            {
                impuesto.ToTable("impuestos_linea");

                impuesto.Property(i => i.Tipo)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                impuesto.Property(i => i.Tarifa)
                    .HasColumnType("numeric(8,4)")
                    .IsRequired();

                impuesto.Property(i => i.BaseGravable)
                    .HasConversion<ConvertidorDinero>()
                    .HasColumnType(TipoColumnaDinero)
                    .IsRequired();

                impuesto.Property(i => i.Valor)
                    .HasConversion<ConvertidorDinero>()
                    .HasColumnType(TipoColumnaDinero)
                    .IsRequired();
            });

            // Debe ir DESPUES de OwnsMany: EF exige que la navegacion exista
            // antes de configurarla. Le dice a EF que escriba directo en el
            // campo _impuestos, porque la propiedad es de solo lectura.
            linea.Navigation(l => l.Impuestos)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        documento.Navigation(d => d.Lineas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
