using FevCore.Application.Abstracciones;
using FevCore.Domain.Salida;
using Microsoft.EntityFrameworkCore;

namespace FevCore.Infrastructure.Persistencia;

public sealed class RepositorioTareas(FevCoreDbContext contexto) : IRepositorioTareas
{
    /// <summary>
    /// Toma la siguiente tarea con SELECT ... FOR UPDATE SKIP LOCKED.
    ///
    /// SKIP LOCKED es la diferencia entre una cola y un atasco. Con FOR
    /// UPDATE a secas —el de H3 y H4— un segundo trabajador ESPERARIA a que
    /// el primero suelte la fila, y como cada uno quiere una tarea distinta,
    /// todos acabarian en fila india detras del mismo candado. SKIP LOCKED
    /// le dice a PostgreSQL que salte las filas bloqueadas y siga buscando:
    /// cada trabajador se lleva una tarea diferente sin esperar a nadie.
    ///
    /// La condicion de TomadaEn es la recuperacion de tareas abandonadas: si
    /// un proceso murio a mitad, su marca envejece y la tarea vuelve a estar
    /// disponible sola, sin que nadie tenga que limpiarla (RNF-04).
    ///
    /// La transaccion que envuelve esto dura lo que tarda marcar la tarea,
    /// no lo que tarda el trabajo. Mantenerla abierta durante una llamada a
    /// un servicio externo retendria una conexion de base de datos por cada
    /// tarea en vuelo.
    /// </summary>
    public async Task<TareaSalida?> TomarSiguienteAsync(
        DateTimeOffset momento,
        TimeSpan tiempoDeAbandono,
        CancellationToken cancelacion = default)
    {
        var limiteAbandono = momento - tiempoDeAbandono;

        await using var transaccion =
            await contexto.Database.BeginTransactionAsync(cancelacion);

        var candidatas = await contexto.TareasSalida
            .FromSql($"""
                SELECT *
                FROM tareas_salida
                WHERE "CompletadaEn" IS NULL
                  AND "ProximoIntentoEn" <= {momento}
                  AND ("TomadaEn" IS NULL OR "TomadaEn" < {limiteAbandono})
                ORDER BY "ProximoIntentoEn"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancelacion);

        var tarea = candidatas.FirstOrDefault();

        if (tarea is null)
        {
            return null;
        }

        tarea.Tomar(momento);

        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return tarea;
    }

    public async Task AgregarAsync(
        TareaSalida tarea,
        CancellationToken cancelacion = default) =>
        await contexto.TareasSalida.AddAsync(tarea, cancelacion);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
