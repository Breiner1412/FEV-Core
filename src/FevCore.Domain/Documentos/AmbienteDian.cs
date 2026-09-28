namespace FevCore.Domain.Documentos;

/// <summary>
/// Entorno de la DIAN contra el que se emite.
///
/// Entra en el calculo del codigo unico, asi que un documento de pruebas y
/// uno de produccion con exactamente los mismos datos producen codigos
/// distintos. Es deliberado por parte de la norma: impide que un documento
/// generado en pruebas pueda hacerse pasar por uno real.
/// </summary>
public enum AmbienteDian
{
    Produccion = 1,
    Pruebas = 2
}
