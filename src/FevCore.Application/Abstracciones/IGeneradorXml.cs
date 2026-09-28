using FevCore.Domain.Documentos;

namespace FevCore.Application.Abstracciones;

/// <summary>
/// Produce el XML de un documento en el formato exigido por la DIAN (RF-16).
///
/// Vive como abstraccion para que la aplicacion no dependa de como se
/// construye el XML. En H6 la firma se insertara dentro de este mismo
/// documento, y en H7 se transmitira tal cual: el resultado de aqui es el
/// artefacto que circula, no un borrador.
/// </summary>
public interface IGeneradorXml
{
    /// <summary>
    /// Genera el XML y devuelve tambien el codigo unico que quedo dentro.
    ///
    /// El codigo sale junto con el XML y no por separado porque los dos
    /// tienen que corresponderse: el codigo se calcula sobre los valores tal
    /// como quedaron escritos. Devolverlos juntos impide que alguien guarde
    /// un codigo calculado aparte.
    /// </summary>
    ResultadoXml Generar(Documento documento, string claveTecnica);
}

public sealed record ResultadoXml(string Xml, string CodigoUnico);
