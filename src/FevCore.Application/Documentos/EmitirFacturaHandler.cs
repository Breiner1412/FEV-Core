using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Caso de uso: emitir una factura electronica de venta.
///
/// Orquesta, no decide. Las reglas de negocio viven en el dominio: este
/// manejador coordina el orden de los pasos y habla con el exterior a
/// traves de interfaces.
/// </summary>
public sealed class EmitirFacturaHandler(
    IRepositorioDocumentos repositorio,
    TimeProvider reloj)
{
    /// <summary>
    /// Prefijo fijo mientras no existan rangos de numeracion. Llega en H3.
    /// </summary>
    private const string PrefijoProvisional = "SETP";

    public async Task<ResultadoEmision> EjecutarAsync(
        ComandoEmitirFactura comando,
        CancellationToken cancelacion = default)
    {
        // RF-15: antes de cualquier otra cosa, verificar si esta peticion ya
        // llego. Va primero, ANTES de tomar consecutivo, para que un reintento
        // no consuma un numero nuevo.
        var existente = await repositorio.BuscarPorReferenciaExternaAsync(
            comando.IntegradorId,
            comando.ReferenciaExterna,
            cancelacion);

        if (existente is not null)
        {
            return new ResultadoEmision(existente, YaExistia: true);
        }

        var lineas = ConstruirLineas(comando.Lineas);

        var consecutivo = await repositorio.ObtenerSiguienteConsecutivoProvisionalAsync(
            PrefijoProvisional,
            cancelacion);

        // El dominio verifica sus propias invariantes. Si algo no cuadra,
        // lanza ExcepcionDominio y la API la traduce a una respuesta.
        var documento = Documento.EmitirFactura(
            integradorId: comando.IntegradorId,
            referenciaExterna: comando.ReferenciaExterna,
            prefijo: PrefijoProvisional,
            consecutivo: consecutivo,
            fechaEmision: comando.FechaEmision ?? reloj.GetUtcNow(),
            lineas: lineas);

        await repositorio.AgregarAsync(documento, cancelacion);
        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoEmision(documento, YaExistia: false);
    }

    private static List<Linea> ConstruirLineas(IReadOnlyList<LineaComando> lineas)
    {
        if (lineas is null || lineas.Count == 0)
        {
            throw new ExcepcionDominio(
                "DOCUMENTO_SIN_LINEAS",
                "Un documento debe tener al menos una linea de detalle.");
        }

        return [.. lineas.Select((linea, indice) => Linea.Crear(
            numero: indice + 1,
            codigo: linea.Codigo,
            descripcion: linea.Descripcion,
            unidadMedida: linea.UnidadMedida,
            cantidad: linea.Cantidad,
            precioUnitario: Dinero.Desde(linea.PrecioUnitario),
            impuestos: (linea.Impuestos ?? []).Select(
                i => new EspecificacionImpuesto(i.Tipo, i.Tarifa)),
            descuento: linea.Descuento is null
                ? null
                : Dinero.Desde(linea.Descuento.Value),
            productoId: linea.ProductoId))];
    }
}
