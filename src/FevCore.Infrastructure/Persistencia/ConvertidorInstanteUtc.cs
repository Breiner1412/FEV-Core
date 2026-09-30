using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FevCore.Infrastructure.Persistencia;

/// <summary>
/// Escribe un DateTimeOffset con desfase cero, sin cambiar el instante.
/// Ver FevCoreDbContext.ConfigureConventions.
/// </summary>
public sealed class ConvertidorInstanteUtc : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public ConvertidorInstanteUtc()
        : base(
            instante => instante.ToUniversalTime(),
            instante => instante)
    {
    }
}
