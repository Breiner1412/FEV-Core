using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using Microsoft.EntityFrameworkCore;

namespace FevCore.Infrastructure.Persistencia;

public sealed class RepositorioDocumentos(FevCoreDbContext contexto)
    : IRepositorioDocumentos
{
    public Task<Documento?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        contexto.Documentos
            .FirstOrDefaultAsync(d => d.Id == id, cancelacion);

    public Task<Documento?> BuscarPorReferenciaExternaAsync(
        Guid integradorId,
        string referenciaExterna,
        CancellationToken cancelacion = default) =>
        contexto.Documentos
            .FirstOrDefaultAsync(
                d => d.IntegradorId == integradorId &&
                     d.ReferenciaExterna == referenciaExterna,
                cancelacion);

    /// <summary>
    /// Bloquea la fila del documento y lo devuelve completo.
    ///
    /// Son dos pasos a proposito. El candado se toma con una consulta que
    /// solo pide el identificador: un documento arrastra sus lineas, los
    /// impuestos de cada linea y su historial, todos en tablas aparte, y un
    /// FOR UPDATE sobre una consulta con esas uniones intentaria bloquear
    /// tambien esas filas, que no hace falta y que PostgreSQL rechaza en
    /// algunas formas de union.
    ///
    /// Una vez tomado el candado, cargar el agregado es una lectura normal:
    /// la fila ya esta protegida hasta que termine la transaccion.
    /// </summary>
    public async Task<Documento?> TomarParaActualizarAsync(
        Guid id,
        CancellationToken cancelacion = default)
    {
        // SqlQuery exige que la columna se llame Value.
        var bloqueadas = await contexto.Database
            .SqlQuery<Guid>($"""
                SELECT "Id" AS "Value"
                FROM documentos
                WHERE "Id" = {id}
                FOR UPDATE
                """)
            .ToListAsync(cancelacion);

        return bloqueadas.Count == 0
            ? null
            : await ObtenerPorIdAsync(id, cancelacion);
    }

    /// <summary>
    /// Cuanto suman las notas credito ya emitidas contra una factura (RN-04).
    ///
    /// Las rechazadas NO cuentan: la autoridad las nego, asi que no
    /// acreditaron nada y no deben consumir capacidad de la factura.
    ///
    /// Las fallidas SI cuentan. Un documento en FALLIDO es un resultado
    /// desconocido, no un documento inexistente (RN-13): puede haber llegado
    /// a la autoridad. Contarlas es la opcion conservadora, y ante la duda,
    /// impedir de mas es preferible a acreditar de mas.
    ///
    /// Va en SQL porque la suma es sobre una columna que en el modelo es un
    /// objeto de valor con convertidor, y LINQ no puede sumar a traves de el.
    /// </summary>
    public async Task<Dinero> SumarNotasCreditoAsync(
        Guid documentoReferenciadoId,
        CancellationToken cancelacion = default)
    {
        var tipoNotaCredito = TipoDocumento.NotaCredito.ToString();
        var estadoRechazado = EstadoDocumento.Rechazado.ToString();

        var sumas = await contexto.Database
            .SqlQuery<decimal>($"""
                SELECT COALESCE(SUM("TotalAPagar"), 0) AS "Value"
                FROM documentos
                WHERE "DocumentoReferenciadoId" = {documentoReferenciadoId}
                  AND "Tipo" = {tipoNotaCredito}
                  AND "Estado" <> {estadoRechazado}
                """)
            .ToListAsync(cancelacion);

        return Dinero.Desde(sumas.Single());
    }

    public async Task AgregarAsync(
        Documento documento,
        CancellationToken cancelacion = default) =>
        await contexto.Documentos.AddAsync(documento, cancelacion);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
