using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Caso de uso: firmar digitalmente el XML de un documento (RF-17).
///
/// Lo llama el procesador de la bandeja de salida, despues de generar el
/// XML y antes de transmitir. Fuera de produccion tambien lo expone un
/// endpoint de desarrollo, para firmar a mano con el trabajador apagado.
/// </summary>
public sealed class FirmarDocumentoHandler(
    IRepositorioDocumentos repositorio,
    IFirmadorXml firmador,
    TimeProvider reloj)
{
    public async Task<Documento?> EjecutarAsync(
        Guid documentoId,
        CancellationToken cancelacion = default)
    {
        var documento = await repositorio.ObtenerPorIdAsync(documentoId, cancelacion);

        if (documento is null)
        {
            return null;
        }

        // El dominio decide si procede: sin XML generado, ya firmado, o en un
        // estado que no lo permite, lanza antes de que se toque nada.
        if (documento.Xml is null)
        {
            throw new Domain.Comun.ExcepcionDominio(
                "XML_NO_DISPONIBLE",
                $"El documento {documento.NumeroCompleto} no tiene XML generado. " +
                "Se firma despues de generarlo; no hay nada que firmar.");
        }

        var firmado = firmador.Firmar(documento.Xml, reloj.GetUtcNow());

        documento.RegistrarFirma(firmado);

        await repositorio.GuardarCambiosAsync(cancelacion);

        return documento;
    }
}
