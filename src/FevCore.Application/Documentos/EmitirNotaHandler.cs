using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Domain.Salida;
using Microsoft.Extensions.Logging;

namespace FevCore.Application.Documentos;

public sealed record ComandoEmitirNota(
    TipoDocumento Tipo,
    Guid IntegradorId,
    string ReferenciaExterna,
    Guid DocumentoReferenciadoId,
    MotivoNota Motivo,
    string? Observaciones,
    DateTimeOffset? FechaEmision,
    IReadOnlyList<LineaComando> Lineas);

/// <summary>
/// Caso de uso: emitir una nota credito o debito contra una factura (RF-12, RF-13).
///
/// Las lineas se resuelven contra el catalogo igual que en una factura, y se
/// copian igual (RN-10). La diferencia esta en lo que hay que comprobar antes:
/// que la factura exista, sea factura, este aprobada, y que el acumulado de
/// notas credito no la supere.
/// </summary>
public sealed class EmitirNotaHandler(
    IRepositorioDocumentos repositorio,
    IRepositorioEmisor repositorioEmisor,
    IRepositorioProductos repositorioProductos,
    IRepositorioRangos repositorioRangos,
    IUnidadDeTrabajo unidadDeTrabajo,
    IRepositorioTareas tareas,
    TimeProvider reloj,
    ILogger<EmitirNotaHandler> registrador)
{
    /// <summary>RF-15: ver EmisionIdempotente.</summary>
    public Task<ResultadoEmision> EjecutarAsync(
        ComandoEmitirNota comando,
        CancellationToken cancelacion = default) =>
        EmisionIdempotente.EjecutarAsync(
            repositorio,
            registrador,
            comando.IntegradorId,
            comando.ReferenciaExterna,
            () => EmitirNuevaAsync(comando, cancelacion),
            cancelacion);

    private async Task<Documento> EmitirNuevaAsync(
        ComandoEmitirNota comando,
        CancellationToken cancelacion)
    {
        var emisor = await repositorioEmisor.ObtenerAsync(cancelacion)
            ?? throw new ExcepcionDominio(
                "EMISOR_INCOMPLETO",
                "El emisor no ha sido configurado. Configurelo en PUT /api/v1/emisor " +
                "antes de emitir documentos.");

        var productos = await repositorioProductos.ObtenerPorIdsAsync(
            comando.Lineas.Select(l => l.ProductoId), cancelacion);

        var lineas = ConstructorLineas.Construir(comando.Lineas, productos);
        var fechaEmision = comando.FechaEmision ?? reloj.GetUtcNow();

        // La vigencia del rango se evalua con la fecha que el documento
        // declara, que es la colombiana, no la UTC (RN-02, INV-RAN-04).
        var fechaCivil = HoraColombia.Fecha(fechaEmision);

        // ── 1. Dos candados, siempre en este orden ──
        //
        // Primero la factura, despues el rango. El orden importa: si un caso
        // de uso tomara el rango antes que la factura y otro al reves, dos
        // transacciones simultaneas podrian quedarse esperando la una a la
        // otra para siempre. Eso es un interbloqueo, y se evita con una
        // regla sencilla: todos los caminos toman los candados en el mismo
        // orden. La emision de factura solo toma el rango, asi que no rompe
        // la regla.
        await using var transaccion =
            await unidadDeTrabajo.IniciarTransaccionAsync(cancelacion);

        var factura = await repositorio.TomarParaActualizarAsync(
            comando.DocumentoReferenciadoId, cancelacion)
            ?? throw new ExcepcionDominio(
                "DOCUMENTO_REFERENCIADO_NO_ENCONTRADO",
                $"No existe un documento con el identificador " +
                $"{comando.DocumentoReferenciadoId}.");

        // RN-04: cuanto se lleva acreditado de esta factura. Es la unica
        // cifra que el dominio no puede averiguar solo, por eso se calcula
        // aqui y se le entrega.
        var notasPrevias = comando.Tipo == TipoDocumento.NotaCredito
            ? await repositorio.SumarNotasCreditoAsync(factura.Id, cancelacion)
            : Dinero.Cero;

        var rango = await repositorioRangos.TomarVigenteParaActualizarAsync(
            comando.Tipo,
            fechaCivil,
            cancelacion)
            ?? throw new ExcepcionDominio(
                "RANGO_NO_DISPONIBLE",
                $"No hay un rango de numeracion vigente para {comando.Tipo} en esa " +
                "fecha. Registrelo en POST /api/v1/rangos-numeracion.");

        var consecutivo = rango.TomarSiguienteConsecutivo(
            fechaCivil);

        // Aqui se verifican RN-03, RN-04 y RN-05, dentro del dominio.
        var nota = Documento.EmitirNota(
            tipo: comando.Tipo,
            integradorId: comando.IntegradorId,
            referenciaExterna: comando.ReferenciaExterna,
            prefijo: rango.Prefijo,
            consecutivo: consecutivo,
            fechaEmision: fechaEmision,
            facturaReferenciada: factura,
            motivo: comando.Motivo,
            observaciones: comando.Observaciones,
            // El emisor, con sus datos de hoy: es quien expide la nota. El
            // adquirente no se pasa: lo hereda de la factura (ADR-0017). Por
            // eso tampoco se consulta el catalogo, ni importa si el
            // adquirente sigue activo: desactivarlo impide venderle de nuevo,
            // no corregir lo que ya se le vendio.
            emisorSnapshot: emisor.Datos.Copiar(),
            lineas: lineas,
            notasCreditoPrevias: notasPrevias);

        await repositorio.AgregarAsync(nota, cancelacion);

        // La tarea de la bandeja entra en la MISMA transaccion que el
        // documento (ADR-0006). Ese es todo el punto del patron: si el
        // documento se guardara aqui y el trabajo se encolara aparte,
        // existiria un instante en que uno de los dos podria perderse. Un
        // documento sin tarea nunca se procesaria y nadie se enteraria; una
        // tarea sin documento fallaria para siempre. Con los dos en la misma
        // transaccion, o entran ambos o no entra ninguno.
        await tareas.AgregarAsync(
            TareaSalida.Crear(nota.Id, TipoTarea.Emitir, fechaEmision),
            cancelacion);
        await repositorio.GuardarCambiosAsync(cancelacion);

        await transaccion.ConfirmarAsync(cancelacion);

        return nota;
    }
}
