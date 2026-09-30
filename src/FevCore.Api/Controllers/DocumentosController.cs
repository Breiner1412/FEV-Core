using System.Text.Json;
using FevCore.Api.Autenticacion;
using FevCore.Api.Contratos;
using FevCore.Application.Abstracciones;
using FevCore.Application.Documentos;
using FevCore.Domain.Documentos;
using Microsoft.AspNetCore.Mvc;

namespace FevCore.Api.Controllers;

[ApiController]
[Route("api/v1/documentos")]
public sealed class DocumentosController(
    ConsultarDocumentoHandler manejador,
    ListarDocumentosHandler listado,
    GenerarXmlHandler generador,
    FirmarDocumentoHandler firmador) : ControllerBase
{
    /// <summary>
    /// Lista los documentos del integrador autenticado (RF-24).
    ///
    /// Por defecto devuelve los del integrador que pregunta, que es lo que se
    /// quiere casi siempre: el punto de venta rara vez necesita paginar entre
    /// las facturas del ERP. Con todos=true devuelve los de la empresa
    /// entera.
    ///
    /// El integrador sale de la llave de API y nunca de un parametro. Aunque
    /// aqui no delimite un permiso —consultar por identificador esta
    /// abierto—, aceptarlo por parametro invitaria a construir clientes que
    /// se identifican dos veces y de dos maneras distintas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<RespuestaPagina<RespuestaDocumentoResumen>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? tipo,
        [FromQuery] string? estado,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] bool todos = false,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = ListarDocumentosHandler.TamanoPaginaPorDefecto,
        CancellationToken cancelacion = default)
    {
        if (!TryLeerEnum<TipoDocumento>(tipo, out var tipoFiltro))
        {
            return ValorInvalido("tipo", tipo!, NombresDelContrato<TipoDocumento>());
        }

        if (!TryLeerEnum<EstadoDocumento>(estado, out var estadoFiltro))
        {
            return ValorInvalido("estado", estado!, NombresDelContrato<EstadoDocumento>());
        }

        var resultado = await listado.EjecutarAsync(
            new FiltroDocumentos(
                IntegradorId: todos ? null : User.ObtenerIntegradorId(),
                Tipo: tipoFiltro,
                Estado: estadoFiltro,
                Desde: desde,
                Hasta: hasta,
                Pagina: pagina,
                TamanoPagina: tamanoPagina),
            cancelacion);

        return Ok(new RespuestaPagina<RespuestaDocumentoResumen>
        {
            Pagina = resultado.Pagina,
            TamanoPagina = resultado.TamanoPagina,
            TotalElementos = resultado.TotalElementos,
            TotalPaginas = resultado.TotalPaginas,
            Elementos = [.. resultado.Elementos.Select(RespuestaDocumentoResumen.Desde)]
        });
    }

    /// <summary>
    /// Consulta un documento por su identificador (RF-22).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelacion)
    {
        var documento = await manejador.EjecutarAsync(id, cancelacion);

        if (documento is null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://github.com/Breiner1412/FEV-Core/errors/no-encontrado",
                Title = "Documento no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No existe un documento con el identificador {id}.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["codigo"] = "DOCUMENTO_NO_ENCONTRADO",
                    ["traceId"] = HttpContext.TraceIdentifier
                }
            });
        }

        return Ok(RespuestaDocumento.Desde(documento));
    }

    /// <summary>
    /// Historial completo de estados, del mas antiguo al mas reciente (RF-23).
    ///
    /// Se ordena por secuencia y no por marca de tiempo: dos transiciones
    /// pueden caer en el mismo instante, y entonces ordenar por tiempo
    /// devolveria el historial en un orden arbitrario.
    /// </summary>
    [HttpGet("{id:guid}/historial")]
    [ProducesResponseType<IReadOnlyList<RespuestaTransicion>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Historial(Guid id, CancellationToken cancelacion)
    {
        var documento = await manejador.EjecutarAsync(id, cancelacion);

        return documento is null
            ? NoEncontrado(id)
            : Ok(documento.Transiciones
                .OrderBy(t => t.Secuencia)
                .Select(RespuestaTransicion.Desde)
                .ToList());
    }

    /// <summary>
    /// Genera el XML del documento y lo deja guardado (RF-16).
    ///
    /// Es POST y no GET porque tiene efectos: guarda el XML, fija el codigo
    /// unico y mueve el documento a EN_PROCESO. En H7 esto lo disparara el
    /// proceso en segundo plano y esta ruta dejara de hacer falta.
    /// </summary>
    [HttpPost("{id:guid}/xml")]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerarXml(Guid id, CancellationToken cancelacion)
    {
        var documento = await generador.EjecutarAsync(id, cancelacion);

        return documento is null
            ? NoEncontrado(id)
            : Ok(RespuestaDocumento.Desde(documento));
    }

    /// <summary>
    /// Firma digitalmente el XML del documento (RF-17).
    ///
    /// No cambia el estado: firmar no es una transicion. El documento sigue
    /// EN_PROCESO, que es el estado que los requerimientos definen como "se
    /// esta generando o firmando el XML".
    /// </summary>
    [HttpPost("{id:guid}/firma")]
    [ProducesResponseType<RespuestaDocumento>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Firmar(Guid id, CancellationToken cancelacion)
    {
        var documento = await firmador.EjecutarAsync(id, cancelacion);

        return documento is null
            ? NoEncontrado(id)
            : Ok(RespuestaDocumento.Desde(documento));
    }

    /// <summary>
    /// Descarga el XML ya generado (RF-25).
    ///
    /// Devuelve application/xml y no JSON: es un archivo, y quien lo pide lo
    /// quiere para guardarlo o enviarlo, no para leerlo dentro de un campo.
    /// </summary>
    [HttpGet("{id:guid}/xml")]
    [Produces("application/xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DescargarXml(Guid id, CancellationToken cancelacion)
    {
        var documento = await manejador.EjecutarAsync(id, cancelacion);

        if (documento is null)
        {
            return NoEncontrado(id);
        }

        // Si esta firmado, se devuelve el firmado: es el documento que
        // circula y el que la autoridad recibiria. El sin firmar se conserva
        // para poder reproducir el calculo, no para entregarlo.
        var xml = documento.XmlFirmado ?? documento.Xml;

        if (xml is null)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://github.com/Breiner1412/FEV-Core/errors/xml-no-disponible",
                Title = "La operacion no procede",
                Status = StatusCodes.Status409Conflict,
                Detail = $"El documento {documento.NumeroCompleto} aun no tiene XML generado.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["codigo"] = "XML_NO_DISPONIBLE",
                    ["traceId"] = HttpContext.TraceIdentifier
                }
            });
        }

        return File(
            System.Text.Encoding.UTF8.GetBytes(xml),
            "application/xml",
            $"{documento.NumeroCompleto}.xml");
    }

    /// <summary>
    /// Lee un enumerado escrito como lo escribe el contrato.
    ///
    /// El cuerpo JSON usa MAYUSCULAS_CON_GUION gracias a un convertidor, pero
    /// una cadena de consulta no pasa por JSON: el enlazador busca el nombre
    /// tal como esta en C#. NOTA_CREDITO no coincide con NotaCredito y el
    /// guion bajo lo rompe.
    ///
    /// Que ESTADO funcionara sin esto era casualidad: sus valores no llevan
    /// guion bajo y la comparacion ignora mayusculas. Una casualidad no es un
    /// contrato.
    ///
    /// Devuelve true cuando el valor viene vacio: no filtrar es valido.
    /// </summary>
    private static bool TryLeerEnum<T>(string? valor, out T? resultado)
        where T : struct, Enum
    {
        resultado = null;

        if (string.IsNullOrWhiteSpace(valor))
        {
            return true;
        }

        if (Enum.TryParse<T>(valor.Replace("_", string.Empty), ignoreCase: true, out var leido)
            && Enum.IsDefined(leido))
        {
            resultado = leido;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Los valores del enumerado tal como los escribe el contrato.
    ///
    /// Enum.GetNames devuelve los nombres de C# —NotaCredito—, y decirle al
    /// integrador que use NotaCredito cuando el contrato dice NOTA_CREDITO
    /// convierte un mensaje de ayuda en una pista falsa. Se usa la misma
    /// politica que serializa el cuerpo, asi que no pueden discrepar.
    /// </summary>
    private static string[] NombresDelContrato<T>() where T : struct, Enum =>
        [.. Enum.GetNames<T>().Select(JsonNamingPolicy.SnakeCaseUpper.ConvertName)];

    /// <summary>
    /// Un valor que no se entiende se rechaza diciendo cuales se aceptan.
    ///
    /// Sin esto, el enlazador devolveria un 400 generico sin codigo, y el
    /// integrador tendria que adivinar cual de sus parametros esta mal.
    /// </summary>
    private IActionResult ValorInvalido(string parametro, string valor, string[] aceptados) =>
        BadRequest(new ProblemDetails
        {
            Type = "https://github.com/Breiner1412/FEV-Core/errors/parametro-invalido",
            Title = "Parametro invalido",
            Status = StatusCodes.Status400BadRequest,
            Detail = $"El valor '{valor}' no es valido para '{parametro}'. " +
                     $"Valores aceptados: {string.Join(", ", aceptados)}.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["codigo"] = "PARAMETRO_INVALIDO",
                ["parametro"] = parametro,
                ["traceId"] = HttpContext.TraceIdentifier
            }
        });

    private IActionResult NoEncontrado(Guid id) =>
        NotFound(new ProblemDetails
        {
            Type = "https://github.com/Breiner1412/FEV-Core/errors/no-encontrado",
            Title = "Documento no encontrado",
            Status = StatusCodes.Status404NotFound,
            Detail = $"No existe un documento con el identificador {id}.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["codigo"] = "DOCUMENTO_NO_ENCONTRADO",
                ["traceId"] = HttpContext.TraceIdentifier
            }
        });
}
