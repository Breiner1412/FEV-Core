using FevCore.Domain.Productos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FevCore.Infrastructure.Persistencia;

public sealed class ConfiguracionProducto : IEntityTypeConfiguration<Producto>
{
    private const string TipoColumnaDinero = "numeric(18,6)";

    /// <summary>
    /// Nombre del indice de codigo unico entre activos. Publico porque el
    /// repositorio lo reconoce al traducir una violacion de unicidad.
    /// </summary>
    public const string IndiceCodigo = "ix_productos_codigo";

    public void Configure(EntityTypeBuilder<Producto> producto)
    {
        producto.ToTable("productos");

        producto.HasKey(p => p.Id);

        producto.Property(p => p.Codigo)
            .HasMaxLength(50)
            .IsRequired();

        producto.Property(p => p.Descripcion)
            .HasMaxLength(500)
            .IsRequired();

        producto.Property(p => p.UnidadMedida)
            .HasMaxLength(10)
            .IsRequired();

        producto.Property(p => p.PrecioUnitario)
            .HasConversion<ConvertidorDinero>()
            .HasColumnType(TipoColumnaDinero)
            .IsRequired();

        producto.Property(p => p.Activo).IsRequired();
        producto.Property(p => p.CreadoEn).IsRequired();
        producto.Property(p => p.ActualizadoEn).IsRequired();

        // Dos productos activos no pueden compartir codigo. Unico y parcial,
        // por la misma razon que el de adquirentes: la comprobacion de la
        // aplicacion no resiste dos altas simultaneas.
        producto.HasIndex(p => p.Codigo)
            .IsUnique()
            .HasFilter("\"Activo\"")
            .HasDatabaseName(IndiceCodigo);

        // ── Impuestos aplicables: pertenecen al producto ──
        producto.OwnsMany(p => p.Impuestos, impuesto =>
        {
            impuesto.ToTable("impuestos_producto");

            impuesto.Property(i => i.Tipo)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            impuesto.Property(i => i.Tarifa)
                .HasColumnType("numeric(8,4)")
                .IsRequired();
        });

        // Despues de OwnsMany: EF exige que la navegacion exista antes de
        // configurarla. Escribe directo en el campo, porque la propiedad
        // es de solo lectura.
        producto.Navigation(p => p.Impuestos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
