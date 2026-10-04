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

    /// <summary>
    /// Nombre del indice de RF-15. Publico porque el repositorio lo reconoce
    /// al traducir una violacion de unicidad.
    /// </summary>
    public const string IndiceReferenciaExterna = "ix_documentos_integrador_referencia";

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

        documento.Property(d => d.AdquirenteId).IsRequired();

        // ── Solo en notas (INV-DOC-03, INV-DOC-04) ──
        // Nulables porque una factura no referencia nada. La obligatoriedad
        // segun el tipo la impone el dominio, no la base: una restriccion
        // CHECK duplicaria la regla en dos sitios que pueden divergir.
        documento.Property(d => d.DocumentoReferenciadoId);

        documento.Property(d => d.Motivo)
            .HasConversion<string>()
            .HasMaxLength(30);

        documento.Property(d => d.Observaciones)
            .HasMaxLength(500);

        // Clave foranea a la propia tabla, sin navegacion en la entidad.
        // El dominio no tiene una propiedad Documento apuntando a la factura
        // a proposito: una nota no arrastra su factura entera cada vez que se
        // carga. Pero la base si garantiza que ese identificador exista.
        documento
            .HasOne<Documento>()
            .WithMany()
            .HasForeignKey(d => d.DocumentoReferenciadoId)
            .OnDelete(DeleteBehavior.Restrict);

        // RN-04 suma las notas credito de una factura en cada emision de
        // nota. Sin este indice seria un recorrido de toda la tabla.
        documento.HasIndex(d => d.DocumentoReferenciadoId)
            .HasDatabaseName("ix_documentos_referenciado");

        // EsNota se deduce del tipo. No se guarda.
        documento.Ignore(d => d.EsNota);

        // ── Resultado de la generacion del XML (H5) ──

        documento.Property(d => d.CodigoUnico)
            .HasMaxLength(96);

        // text y no varchar(n): un XML con muchas lineas no tiene un tamano
        // maximo razonable que se pueda fijar de antemano. En PostgreSQL,
        // text no es mas lento que varchar.
        documento.Property(d => d.Xml)
            .HasColumnType("text");

        documento.Property(d => d.XmlFirmado)
            .HasColumnType("text");

        // ── Las dos copias de datos tributarios (RN-10) ──
        // Cada una en sus propias columnas, prefijadas por EF con el nombre
        // de su navegacion: EmisorSnapshot_RazonSocial, etc.
        //
        // Se guardan con el documento y no como referencia al catalogo: si
        // el emisor o el adquirente cambian sus datos, este documento sigue
        // declarando los que se firmaron.
        documento.OwnsOne(d => d.EmisorSnapshot, ConfiguracionDatosTributarios.Configurar);
        documento.Navigation(d => d.EmisorSnapshot).IsRequired();

        documento.OwnsOne(d => d.AdquirenteSnapshot, ConfiguracionDatosTributarios.Configurar);
        documento.Navigation(d => d.AdquirenteSnapshot).IsRequired();

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
            .HasDatabaseName(IndiceReferenciaExterna);

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

        // ── Historial de estados: pertenece al documento (RN-12, RF-23) ──
        // Va dentro del agregado y no en una tabla suelta porque INV-TRA-02
        // —que cada transicion empalme con la anterior— no se puede verificar
        // sin ver el documento completo.
        documento.OwnsMany(d => d.Transiciones, transicion =>
        {
            transicion.ToTable("transiciones_estado");
            transicion.WithOwner().HasForeignKey("DocumentoId");

            // ValueGeneratedNever porque, por convencion, Entity Framework
            // trata un entero que forma parte de la clave como generado por
            // la base. Aqui no: la secuencia la asigna el documento, y debe
            // empezar en 1 para cada uno. Dejandolo a PostgreSQL seria un
            // contador global y el historial de cada documento empezaria en
            // un numero cualquiera.
            transicion.Property(t => t.Secuencia)
                .ValueGeneratedNever()
                .IsRequired();

            // La identidad natural: documento mas posicion en su historial.
            // Sin esto, Entity Framework inventa una columna de identidad
            // propia, que no significa nada y que nadie consulta jamas.
            transicion.HasKey("DocumentoId", nameof(TransicionEstado.Secuencia));

            transicion.Property(t => t.EstadoAnterior)
                .HasConversion<string>()
                .HasMaxLength(20);

            transicion.Property(t => t.EstadoNuevo)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            transicion.Property(t => t.OcurridaEn).IsRequired();

            transicion.Property(t => t.Motivo)
                .HasMaxLength(200)
                .IsRequired();

            transicion.Property(t => t.Detalle)
                .HasMaxLength(500);
        });

        documento.Navigation(d => d.Transiciones)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ── Transmisiones: cada intento de entrega (RF-18, RN-13) ──
        // Van dentro del agregado porque solo significan algo junto a su
        // documento, y porque conservarlas TODAS es lo que permite saber si
        // hubo un envio cuyo destino se desconoce.
        documento.OwnsMany(d => d.Transmisiones, transmision =>
        {
            transmision.ToTable("transmisiones");
            transmision.WithOwner().HasForeignKey("DocumentoId");

            // Misma identidad natural que las transiciones: documento mas
            // posicion. Un intento no existe fuera de su documento.
            transmision.Property(t => t.NumeroIntento)
                .ValueGeneratedNever()
                .IsRequired();

            transmision.HasKey("DocumentoId", nameof(Domain.Documentos.Transmision.NumeroIntento));

            transmision.Property(t => t.EnviadaEn).IsRequired();

            transmision.Property(t => t.IdentificadorSeguimiento)
                .HasMaxLength(Domain.Documentos.Transmision.LongitudMaximaSeguimiento);

            transmision.Property(t => t.Resultado)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            transmision.Property(t => t.RespuestaCruda).HasMaxLength(2000);
        });

        documento.Navigation(d => d.Transmisiones)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ── Errores de validacion (RF-21) ──
        // Una lista de texto en una sola columna JSON, no una tabla aparte:
        // solo se leen enteros, junto al documento, y nadie consulta por un
        // error suelto.
        documento.PrimitiveCollection(d => d.ErroresValidacion)
            .HasColumnType("jsonb")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
