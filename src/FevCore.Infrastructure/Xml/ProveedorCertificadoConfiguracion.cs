using System.Security.Cryptography.X509Certificates;
using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using Microsoft.Extensions.Configuration;

namespace FevCore.Infrastructure.Xml;

/// <summary>
/// Carga el certificado de firma desde la configuracion (RF-04, RNF-01).
///
/// Se recibe en base 64 por variable de entorno y no como ruta a un archivo:
/// un archivo hay que montarlo en el contenedor, acaba copiado a mano en
/// algun servidor y termina en un repositorio. Una variable de entorno la
/// gestiona el mismo mecanismo que el resto de secretos.
///
/// El certificado NUNCA sale de aqui hacia una respuesta ni hacia un
/// registro de actividad (INV-CER-02). Lo unico que este tipo expone es el
/// certificado mismo, a quien lo necesita para firmar.
/// </summary>
public sealed class ProveedorCertificadoConfiguracion : IProveedorCertificado
{
    private readonly X509Certificate2? _certificado;

    public ProveedorCertificadoConfiguracion(IConfiguration configuracion)
    {
        var base64 = configuracion["Firma:CertificadoBase64"];
        var clave = configuracion["Firma:Clave"];

        if (string.IsNullOrWhiteSpace(base64))
        {
            _certificado = null;
            return;
        }

        try
        {
            _certificado = X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(base64),
                clave,
                X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (Exception error)
        {
            // El mensaje no incluye el contenido ni la clave: un error de
            // carga no puede convertirse en una filtracion (INV-CER-02).
            throw new ExcepcionDominio(
                "CERTIFICADO_INVALIDO",
                "El certificado configurado no se pudo cargar. Verifique que " +
                $"sea un PKCS#12 valido y que la clave sea correcta. ({error.GetType().Name})");
        }
    }

    public X509Certificate2 Obtener(DateTimeOffset momento)
    {
        if (_certificado is null)
        {
            throw new ExcepcionDominio(
                "CERTIFICADO_NO_CONFIGURADO",
                "No hay certificado de firma configurado. Defina " +
                "Firma__CertificadoBase64 y Firma__Clave.");
        }

        var instante = momento.UtcDateTime;

        // INV-CER-01: fuera de su vigencia no firma.
        if (instante < _certificado.NotBefore.ToUniversalTime())
        {
            throw new ExcepcionDominio(
                "CERTIFICADO_NO_VIGENTE",
                $"El certificado empieza a ser valido el " +
                $"{_certificado.NotBefore:yyyy-MM-dd}.");
        }

        if (instante > _certificado.NotAfter.ToUniversalTime())
        {
            throw new ExcepcionDominio(
                "CERTIFICADO_VENCIDO",
                $"El certificado vencio el {_certificado.NotAfter:yyyy-MM-dd}. " +
                "No se puede firmar con el.");
        }

        return _certificado;
    }
}
