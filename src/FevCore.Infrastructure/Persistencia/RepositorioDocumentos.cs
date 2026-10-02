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

    /// <summary>
    /// Pagina de documentos del integrador (RF-24).
    ///
    /// Las fechas del filtro son fechas civiles colombianas, no instantes en
    /// UTC. Un documento emitido a las 20:00 del 5 de marzo en Pereira son
    /// las 01:00 del 6 en UTC: filtrar por UTC lo dejaria fuera de una
    /// busqueda del dia 5, y para quien emitio esa factura ese dia fue el 5.
    /// Colombia no aplica horario de verano, asi que el desfase es fijo y la
    /// conversion no necesita una base de datos de zonas horarias.
    ///
    /// Hasta es INCLUSIVE, como dice el contrato, y por eso se compara contra
    /// el principio del dia siguiente en vez de contra el final del mismo:
    /// un documento de las 23:59:59.7 no se escapa por las decimas.
    /// </summary>
    public async Task<PaginaDe<ResumenDocumento>> ListarAsync(
        FiltroDocumentos filtro,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Documentos.AsNoTracking();

        if (filtro.IntegradorId is { } integradorId)
        {
            consulta = consulta.Where(d => d.IntegradorId == integradorId);
        }

        if (filtro.Tipo is { } tipo)
        {
            consulta = consulta.Where(d => d.Tipo == tipo);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(d => d.Estado == estado);
        }

        if (filtro.Desde is { } desde)
        {
            // ToUniversalTime no cambia el instante, solo como se escribe.
            // Hace falta porque la columna es timestamptz y Npgsql exige que
            // el parametro lleve desfase cero: pasarle uno en -05:00 lanza.
            var inicio = HoraColombia.InicioDe(desde).ToUniversalTime();

            consulta = consulta.Where(d => d.FechaEmision >= inicio);
        }

        if (filtro.Hasta is { } hasta)
        {
            var finExclusivo = HoraColombia.InicioDe(hasta.AddDays(1)).ToUniversalTime();

            consulta = consulta.Where(d => d.FechaEmision < finExclusivo);
        }

        // Se cuenta antes de paginar y sobre la MISMA consulta filtrada: el
        // total que se devuelve tiene que ser el de los resultados del
        // filtro, no el de la tabla.
        var total = await consulta.LongCountAsync(cancelacion);

        // Del mas reciente al mas antiguo, y con el identificador como
        // desempate. Sin el, dos documentos del mismo instante podrian
        // repartirse entre dos paginas o aparecer dos veces: PostgreSQL no
        // promete un orden estable para las filas empatadas.
        var elementos = await consulta
            .OrderByDescending(d => d.FechaEmision)
            .ThenByDescending(d => d.Id)
            .Skip((filtro.Pagina - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .Select(d => new ResumenDocumento(
                d.Id,
                d.Tipo,
                d.Estado,
                d.Prefijo,
                d.Consecutivo,
                d.FechaEmision,
                d.AdquirenteSnapshot.RazonSocial,
                d.Totales.TotalAPagar,
                d.CodigoUnico))
            .ToListAsync(cancelacion);

        return new PaginaDe<ResumenDocumento>(
            elementos, filtro.Pagina, filtro.TamanoPagina, total);
    }

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

    /// <summary>
    /// Guarda, y traduce la violacion del indice de RF-15 a
    /// ExcepcionReferenciaDuplicada: dos solicitudes con la misma referencia
    /// llegaron a la vez y esta perdio la carrera. Cualquier otra violacion
    /// sigue siendo un error y se propaga tal cual.
    /// </summary>
    public async Task GuardarCambiosAsync(CancellationToken cancelacion = default)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException error) when (TraduccionUnicidad.EsViolacionDe(
            error, ConfiguracionDocumento.IndiceReferenciaExterna))
        {
            throw new ExcepcionReferenciaDuplicada(error);
        }
    }
}
