using FevCore.Domain.Comun;
using Microsoft.EntityFrameworkCore;
// ValueComparer vive en ChangeTracking, no junto a ValueConverter: uno
// traduce valores hacia y desde la base de datos, el otro le sirve al
// rastreador de cambios para decidir si un valor se modifico.
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FevCore.Infrastructure.Persistencia;

/// <summary>
/// Mapeo de DatosTributarios, que se usa como objeto incrustado en el
/// emisor, en los adquirentes y en los documentos.
///
/// Esta en un solo lugar para que las tres tablas guarden estos datos de
/// forma identica: si manana se agrega un campo, se agrega una vez.
/// </summary>
internal static class ConfiguracionDatosTributarios
{
    /// <summary>
    /// Las responsabilidades tributarias son una lista corta de codigos.
    /// Se guardan en una sola columna separadas por punto y coma, en vez de
    /// una tabla aparte: nunca se consultan por separado ni se filtran por
    /// ellas, asi que una tabla solo agregaria una union a cada consulta.
    /// </summary>
    private static readonly ValueConverter<IReadOnlyList<string>, string> Convertidor =
        new(
            lista => string.Join(';', lista),
            texto => (IReadOnlyList<string>)texto
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList());

    /// <summary>
    /// Sin este comparador, EF compararia las listas por REFERENCIA para
    /// decidir si algo cambio. Como al leer de la base de datos crea una
    /// lista nueva cada vez, o bien creeria que todo cambio siempre, o bien
    /// no detectaria un cambio real. Es el mismo problema de igualdad que
    /// hubo que resolver en el propio DatosTributarios.
    /// </summary>
    private static readonly ValueComparer<IReadOnlyList<string>> Comparador =
        new(
            (una, otra) => una!.SequenceEqual(otra!),
            lista => lista.Aggregate(0, (acumulado, item) =>
                HashCode.Combine(acumulado, item.GetHashCode())),
            lista => (IReadOnlyList<string>)lista.ToList());

    public static void Configurar<T>(
        OwnedNavigationBuilder<T, DatosTributarios> datos)
        where T : class
    {
        datos.Property(d => d.TipoIdentificacion)
            .HasMaxLength(5)
            .IsRequired();

        datos.Property(d => d.Identificacion)
            .HasMaxLength(20)
            .IsRequired();

        datos.Property(d => d.DigitoVerificacion)
            .HasMaxLength(1);

        datos.Property(d => d.RazonSocial)
            .HasMaxLength(300)
            .IsRequired();

        datos.Property(d => d.Direccion)
            .HasMaxLength(300)
            .IsRequired();

        datos.Property(d => d.MunicipioCodigo)
            .HasMaxLength(10)
            .IsRequired();

        datos.Property(d => d.Correo)
            .HasMaxLength(200);

        datos.Property(d => d.Telefono)
            .HasMaxLength(30);

        datos.Property(d => d.Regimen)
            .HasMaxLength(10)
            .IsRequired();

        // Sin HasColumnName: EF antepone el nombre de la navegacion que la
        // contiene, asi que un documento puede llevar DOS copias de estos
        // datos — la del emisor y la del adquirente — sin que las columnas
        // choquen entre si.
        datos.Property(d => d.Responsabilidades)
            .HasConversion(Convertidor, Comparador)
            .HasMaxLength(200)
            .IsRequired();

        // Se calcula desde tipo e identificacion. No se guarda.
    }
}
