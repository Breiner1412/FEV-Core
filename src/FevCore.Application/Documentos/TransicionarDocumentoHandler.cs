using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Caso de uso: llevar un documento a un estado nuevo (RN-11, RN-12).
///
/// Su unico cliente es el endpoint de desarrollo que fuerza un estado, que
/// no existe en produccion. El procesador de la bandeja de salida no lo usa:
/// mueve los documentos con los metodos que ademas registran lo que paso
/// (RegistrarXmlGenerado, RegistrarTransmision, RegistrarFallo...).
///
/// Toda la validacion vive en Documento.Transicionar. Aqui no hay ni un if
/// sobre estados.
/// </summary>
public sealed class TransicionarDocumentoHandler(
    IRepositorioDocumentos repositorio,
    TimeProvider reloj)
{
    public async Task<Documento?> EjecutarAsync(
        Guid documentoId,
        EstadoDocumento nuevoEstado,
        string motivo,
        string? detalle,
        CancellationToken cancelacion = default)
    {
        var documento = await repositorio.ObtenerPorIdAsync(documentoId, cancelacion);

        if (documento is null)
        {
            return null;
        }

        documento.Transicionar(nuevoEstado, motivo, reloj.GetUtcNow(), detalle);

        await repositorio.GuardarCambiosAsync(cancelacion);

        return documento;
    }
}
