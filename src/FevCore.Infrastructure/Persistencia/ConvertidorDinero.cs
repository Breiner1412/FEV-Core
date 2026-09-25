using FevCore.Domain.Comun;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FevCore.Infrastructure.Persistencia;

/// <summary>
/// Traduce entre el tipo Dinero del dominio y una columna decimal.
///
/// Al leer usa Dinero.Desde, que valida: si alguien metiera un valor
/// negativo directamente en la base de datos, la lectura fallaria en vez
/// de devolver un objeto invalido.
/// </summary>
public sealed class ConvertidorDinero : ValueConverter<Dinero, decimal>
{
    public ConvertidorDinero()
        : base(
            dinero => dinero.Valor,
            valor => Dinero.Desde(valor))
    {
    }
}
