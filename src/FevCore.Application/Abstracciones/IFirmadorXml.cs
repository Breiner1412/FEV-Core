using System.Security.Cryptography.X509Certificates;

namespace FevCore.Application.Abstracciones;

/// <summary>
/// Firma digitalmente un XML ya generado (RF-17).
/// </summary>
public interface IFirmadorXml
{
    /// <summary>
    /// Devuelve el XML con la firma incrustada.
    ///
    /// La firma no se devuelve aparte porque no tiene sentido separada: va
    /// dentro del documento, y es el documento firmado lo que se transmite.
    /// </summary>
    string Firmar(string xml, DateTimeOffset momento);
}

/// <summary>
/// Da acceso al certificado del emisor (RF-04, INV-CER-01, INV-CER-02).
/// </summary>
public interface IProveedorCertificado
{
    /// <summary>
    /// El certificado, si esta configurado y vigente.
    /// Lanza si esta vencido o si aun no ha entrado en vigencia.
    /// </summary>
    X509Certificate2 Obtener(DateTimeOffset momento);
}
