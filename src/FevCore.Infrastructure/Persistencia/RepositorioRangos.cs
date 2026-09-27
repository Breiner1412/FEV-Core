using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;
using FevCore.Domain.Numeracion;
using Microsoft.EntityFrameworkCore;

namespace FevCore.Infrastructure.Persistencia;

public sealed class RepositorioRangos(FevCoreDbContext contexto) : IRepositorioRangos
{
    /// <summary>
    /// Toma el rango vigente bloqueando su fila.
    ///
    /// Se escribe en SQL porque Entity Framework no ofrece bloqueo pesimista:
    /// no hay forma de expresar FOR UPDATE con LINQ.
    ///
    /// Lo que hace FOR UPDATE: mientras esta transaccion siga abierta,
    /// cualquier otra que intente leer esta misma fila con FOR UPDATE queda
    /// esperando. Cuando la primera confirma, la segunda continua y lee el
    /// contador ya actualizado.
    ///
    /// El resultado se materializa con ToListAsync y no con FirstOrDefaultAsync
    /// a proposito: cualquier operador LINQ despues de FromSql hace que EF
    /// envuelva la consulta en una subconsulta, y FOR UPDATE dentro de una
    /// subconsulta no se comporta igual.
    /// </summary>
    public async Task<RangoNumeracion?> TomarVigenteParaActualizarAsync(
        TipoDocumento tipoDocumento,
        DateOnly fecha,
        CancellationToken cancelacion = default)
    {
        var tipo = tipoDocumento.ToString();

        // La interpolacion produce parametros de consulta, no concatenacion
        // de texto: EF convierte cada valor en un parametro. No hay riesgo
        // de inyeccion aunque lo parezca.
        var encontrados = await contexto.RangosNumeracion
            .FromSql($"""
                SELECT *
                FROM rangos_numeracion
                WHERE "TipoDocumento" = {tipo}
                  AND "VigenteDesde" <= {fecha}
                  AND "VigenteHasta" >= {fecha}
                ORDER BY "VigenteDesde"
                LIMIT 1
                FOR UPDATE
                """)
            .ToListAsync(cancelacion);

        return encontrados.FirstOrDefault();
    }

    public Task<RangoNumeracion?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        contexto.RangosNumeracion.FirstOrDefaultAsync(r => r.Id == id, cancelacion);

    public async Task<IReadOnlyList<RangoNumeracion>> ListarPorTipoAsync(
        TipoDocumento tipoDocumento,
        CancellationToken cancelacion = default) =>
        await contexto.RangosNumeracion
            .Where(r => r.TipoDocumento == tipoDocumento)
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<RangoNumeracion>> ListarAsync(
        CancellationToken cancelacion = default) =>
        await contexto.RangosNumeracion
            .OrderBy(r => r.TipoDocumento)
            .ThenBy(r => r.VigenteDesde)
            .ToListAsync(cancelacion);

    public async Task AgregarAsync(
        RangoNumeracion rango,
        CancellationToken cancelacion = default) =>
        await contexto.RangosNumeracion.AddAsync(rango, cancelacion);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}

/// <summary>
/// Transacciones sobre el contexto de Entity Framework.
/// </summary>
public sealed class UnidadDeTrabajo(FevCoreDbContext contexto) : IUnidadDeTrabajo
{
    public async Task<ITransaccion> IniciarTransaccionAsync(
        CancellationToken cancelacion = default) =>
        new TransaccionEf(await contexto.Database.BeginTransactionAsync(cancelacion));
}

internal sealed class TransaccionEf(
    Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaccion)
    : ITransaccion
{
    public Task ConfirmarAsync(CancellationToken cancelacion = default) =>
        transaccion.CommitAsync(cancelacion);

    // Si nadie confirmo, al descartarse se revierte todo.
    public ValueTask DisposeAsync() => transaccion.DisposeAsync();
}
