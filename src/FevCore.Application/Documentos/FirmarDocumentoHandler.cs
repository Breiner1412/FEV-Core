using FevCore.Application.Abstracciones;
using FevCore.Domain.Documentos;

namespace FevCore.Application.Documentos;

/// <summary>
/// Caso de uso: firmar digitalmente el XML de un documento (RF-17).
///
/// En H7 lo llamara el proceso en segundo plano, justo despues de generar
/// el XML y antes de transmitir. El caso de uso sera el mismo.
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
                "Generelo primero con POST /api/v1/documentos/{id}/xml.");
        }

        var firmado = firmador.Firmar(documento.Xml, reloj.GetUtcNow());

        documento.RegistrarFirma(firmado);

        await repositorio.GuardarCambiosAsync(cancelacion);

        return documento;
    }
}
