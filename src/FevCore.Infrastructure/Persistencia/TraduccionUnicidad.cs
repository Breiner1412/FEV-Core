using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FevCore.Infrastructure.Persistencia;

/// <summary>
/// Convierte la violacion de un indice unico concreto en la excepcion que
/// corresponde a esa regla.
///
/// Solo la de ESE indice: cualquier otra violacion, o cualquier otro error,
/// sigue siendo un error y se propaga tal cual. Reconocerla por el nombre
/// del indice evita que un fallo distinto se disfrace de regla de negocio.
/// </summary>
internal static class TraduccionUnicidad
{
    public static async Task GuardarAsync(
        DbContext contexto,
        string indice,
        Func<Exception> traducir,
        CancellationToken cancelacion)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException error) when (EsViolacionDe(error, indice))
        {
            throw traducir();
        }
    }

    public static bool EsViolacionDe(DbUpdateException error, string indice) =>
        error.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } violacion
        && violacion.ConstraintName == indice;
}
