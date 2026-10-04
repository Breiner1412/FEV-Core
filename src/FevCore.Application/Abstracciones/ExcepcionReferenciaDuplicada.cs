namespace FevCore.Application.Abstracciones;

/// <summary>
/// Otra solicitud guardo primero un documento con la misma referencia
/// externa del mismo integrador (RF-15, INV-DOC-06).
///
/// No es un error para el integrador: es lo que pasa cuando reintenta
/// mientras la primera solicitud sigue en curso. La lanza el repositorio al
/// traducir la violacion del indice unico, para que la aplicacion no tenga
/// que conocer los codigos de PostgreSQL, y el caso de uso la convierte en
/// "ya existia".
/// </summary>
public sealed class ExcepcionReferenciaDuplicada(Exception causa)
    : Exception("Ya existe un documento con esa referencia externa.", causa);
