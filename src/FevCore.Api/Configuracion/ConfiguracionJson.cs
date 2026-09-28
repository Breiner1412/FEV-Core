using System.Text.Json;
using System.Text.Json.Serialization;

namespace FevCore.Api.Configuracion;

/// <summary>
/// Como serializa JSON toda la API.
///
/// Existe como metodo compartido porque ASP.NET Core tiene DOS
/// configuraciones de JSON independientes:
///
/// - Mvc.JsonOptions, que usan los controladores.
/// - Http.Json.JsonOptions, que usan los endpoints de API minima.
///
/// Configurar solo una deja la otra con los valores por defecto, y el
/// resultado es que la misma peticion se entiende en una ruta y responde
/// 400 en otra. Aqui se define una vez y se aplica a las dos, de modo que
/// no puedan separarse.
/// </summary>
public static class ConfiguracionJson
{
    public static void Aplicar(JsonSerializerOptions opciones)
    {
        // Las enumeraciones viajan como texto, no como numeros: "APROBADO"
        // se entiende solo, y un 3 obliga a consultar una tabla de codigos.
        // La politica de mayusculas con guion bajo produce exactamente los
        // valores del contrato: Iva -> "IVA", NotaCredito -> "NOTA_CREDITO".
        opciones.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    }
}
