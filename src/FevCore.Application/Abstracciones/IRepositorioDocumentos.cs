using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;

namespace FevCore.Application.Abstracciones;

public interface IRepositorioDocumentos
{
    /// <summary>
    /// Trae un documento por su identificador.
    ///
    /// No filtra por integrador, y es correcto: la version 1 tiene una sola
    /// empresa emisora, y los integradores son sus propios sistemas — su
    /// ERP, su tienda, su punto de venta. Un documento pertenece a la
    /// empresa, no al sistema que lo origino. El integrador queda registrado
    /// para poder rastrear quien emitio que (RF-02), no para decidir quien
    /// puede leerlo.
    ///
    /// Esto cambiaria el dia que el sistema admita varias empresas emisoras,
    /// hoy fuera de alcance.
    /// </summary>
    Task<Documento?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Pagina de documentos del integrador, del mas reciente al mas antiguo
    /// (RF-24).
    /// </summary>
    Task<PaginaDe<ResumenDocumento>> ListarAsync(
        FiltroDocumentos filtro,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Busca un documento ya emitido con esa referencia externa (RF-15).
    /// Es lo que permite reintentar una emision sin duplicarla.
    /// </summary>
    Task<Documento?> BuscarPorReferenciaExternaAsync(
        Guid integradorId,
        string referenciaExterna,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Trae el documento con su fila BLOQUEADA hasta el final de la
    /// transaccion en curso.
    ///
    /// Se usa al emitir una nota: mientras se comprueba cuanto se lleva
    /// acreditado de esa factura y se guarda la nota nueva, ninguna otra
    /// transaccion puede hacer lo mismo sobre la misma factura (RN-04).
    ///
    /// Debe llamarse dentro de una transaccion, y ANTES de tomar el rango de
    /// numeracion: todos los casos de uso toman los candados en ese orden
    /// para que dos no puedan quedarse esperandose mutuamente.
    /// </summary>
    Task<Documento?> TomarParaActualizarAsync(
        Guid id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Suma de las notas credito vigentes contra una factura (RN-04).
    /// </summary>
    Task<Dinero> SumarNotasCreditoAsync(
        Guid documentoReferenciadoId,
        CancellationToken cancelacion = default);

    Task AgregarAsync(
        Documento documento,
        CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}

/// <summary>
/// Criterios de busqueda de documentos (RF-24).
///
/// IntegradorId nulo significa "todos los del emisor". El listado por
/// defecto lo rellena con el integrador que pregunta, porque lo habitual es
/// querer lo propio: el punto de venta rara vez necesita paginar entre las
/// facturas del ERP. Pero puede pedirse todo, y no seria coherente impedirlo
/// cuando consultar un documento por identificador esta abierto.
/// </summary>
public sealed record FiltroDocumentos(
    Guid? IntegradorId,
    TipoDocumento? Tipo = null,
    EstadoDocumento? Estado = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int Pagina = 1,
    int TamanoPagina = 20);

/// <summary>
/// Lo que un listado necesita de un documento, y nada mas (RF-24).
///
/// Existe para no devolver documentos enteros. Entity Framework carga
/// siempre las colecciones que pertenecen a una entidad, asi que pedir
/// veinte documentos traeria tambien sus lineas, los impuestos de cada
/// linea, su historial completo y cada intento de transmision: cientos de
/// filas para pintar ocho campos.
/// </summary>
public sealed record ResumenDocumento(
    Guid Id,
    TipoDocumento Tipo,
    EstadoDocumento Estado,
    string Prefijo,
    long Consecutivo,
    DateTimeOffset FechaEmision,
    string AdquirenteRazonSocial,
    Dinero TotalAPagar,
    string? CodigoUnico)
{
    /// <summary>
    /// Repite la forma que define Documento.NumeroCompleto.
    ///
    /// Se duplica a proposito: la alternativa es traer el documento entero
    /// para leer una propiedad calculada, que es justo lo que este tipo
    /// evita. Si el formato cambiara, los dos sitios tienen que cambiar, y
    /// hay una prueba que lo comprueba.
    /// </summary>
    public string NumeroCompleto => $"{Prefijo}{Consecutivo}";
}
