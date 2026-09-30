using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FevCore.Domain.Comun;

namespace FevCore.Domain.Documentos;

/// <summary>
/// Los quince valores que entran en el codigo unico, ya convertidos a texto.
///
/// Este tipo existe por una razon concreta: el codigo se calcula sobre los
/// valores TAL COMO aparecen escritos en el XML. Si el hash se calculara a
/// partir de los numeros del dominio y el XML se escribiera por otro camino,
/// bastaria un formato distinto —una hora en otra zona horaria, un decimal
/// de mas— para que la DIAN recalculara el codigo desde el XML y obtuviera
/// otro. El documento seria rechazado y el error seria de los que cuesta
/// dias encontrar.
///
/// Al pasar por aqui, el generador de XML y el calculo del hash beben de las
/// MISMAS cadenas. No pueden discrepar.
/// </summary>
public sealed record ValoresCufe
{
    public required string NumeroFactura { get; init; }
    public required string Fecha { get; init; }
    public required string Hora { get; init; }
    public required string ValorBruto { get; init; }
    public required string ValorIva { get; init; }
    public required string ValorInc { get; init; }
    public required string ValorIca { get; init; }
    public required string ValorTotal { get; init; }
    public required string NitEmisor { get; init; }
    public required string IdentificacionAdquirente { get; init; }
    public required string ClaveTecnica { get; init; }
    public required string Ambiente { get; init; }

    public static ValoresCufe Para(
        Documento documento,
        string claveTecnica,
        AmbienteDian ambiente)
    {
        ArgumentNullException.ThrowIfNull(documento);

        if (string.IsNullOrWhiteSpace(claveTecnica))
        {
            throw new ExcepcionDominio(
                "CLAVE_TECNICA_REQUERIDA",
                "El codigo unico no se puede calcular sin la clave tecnica del " +
                "rango de numeracion.");
        }

        // Las fechas del documento se escriben en hora colombiana, no en UTC.
        var enColombia = HoraColombia.En(documento.FechaEmision);

        return new ValoresCufe
        {
            NumeroFactura = documento.NumeroCompleto,
            Fecha = enColombia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Hora = enColombia.ToString("HH:mm:sszzz", CultureInfo.InvariantCulture),

            // ValFac: la suma de las bases de linea, antes de impuestos y
            // despues de descuentos. Es lo que UBL declara como
            // LegalMonetaryTotal/LineExtensionAmount.
            ValorBruto = Importe(documento.Totales.TotalBaseImponible),

            ValorIva = Importe(SumarImpuesto(documento, TipoImpuesto.Iva)),
            ValorInc = Importe(SumarImpuesto(documento, TipoImpuesto.Inc)),

            // El ICA es un impuesto municipal que este proyecto no maneja.
            // El campo NO se omite: la formula exige los tres codigos siempre,
            // con valor cero cuando no aplican.
            ValorIca = Importe(Dinero.Cero),

            ValorTotal = Importe(documento.Totales.TotalAPagar),
            NitEmisor = documento.EmisorSnapshot.Identificacion,
            IdentificacionAdquirente = documento.AdquirenteSnapshot.Identificacion,
            ClaveTecnica = claveTecnica,
            Ambiente = ((int)ambiente).ToString(CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// El formato vive en Dinero, no aqui. Es el MISMO texto con el que se
    /// escribe el XML, y tiene que serlo: la autoridad recalcula el codigo
    /// leyendo el XML.
    /// </summary>
    private static string Importe(Dinero valor) => valor.ParaDocumento();

    private static Dinero SumarImpuesto(Documento documento, TipoImpuesto tipo) =>
        documento.Lineas
            .SelectMany(l => l.Impuestos)
            .Where(i => i.Tipo == tipo)
            .Aggregate(Dinero.Cero, (suma, i) => suma + i.Valor)
            .Redondear();

    /// <summary>
    /// Los quince valores, uno detras de otro, sin separadores.
    ///
    /// Los codigos 01, 04 y 03 son constantes de la norma: IVA, impuesto al
    /// consumo e ICA. Van siempre, aunque su valor sea cero.
    /// </summary>
    public string Concatenar() =>
        string.Concat(
            NumeroFactura,
            Fecha,
            Hora,
            ValorBruto,
            "01", ValorIva,
            "04", ValorInc,
            "03", ValorIca,
            ValorTotal,
            NitEmisor,
            IdentificacionAdquirente,
            ClaveTecnica,
            Ambiente);
}

/// <summary>
/// Calculo del CUFE, el codigo unico de la factura electronica.
///
/// ADVERTENCIA. Este algoritmo es normativo y NO se ha podido contrastar
/// contra un ejemplo oficial de la DIAN: el anexo tecnico que lo define pasa
/// de setecientas paginas y no se dispuso de un caso de prueba publicado con
/// sus datos de entrada y su codigo esperado. El orden de los campos se tomo
/// de dos fuentes independientes que coinciden, pero las pruebas de este
/// proyecto solo pueden demostrar que el calculo es COHERENTE —que ningun
/// campo se quedo fuera y que cambiar cualquiera cambia el resultado—, no
/// que sea el que la DIAN espera.
///
/// Cualquier uso real exige verificarlo contra el anexo vigente y contra un
/// documento realmente aceptado en el entorno de pruebas.
///
/// El CUDE, el codigo equivalente de las notas credito y debito, NO esta
/// implementado. Las fuentes consultadas coinciden en que su formula es casi
/// identica sustituyendo la clave tecnica por el PIN del software, pero
/// ninguna lo afirma con autoridad suficiente. Se prefiere un hueco
/// declarado a un algoritmo normativo escrito de memoria.
/// </summary>
public static class CodigoUnico
{
    /// <summary>Nombre del esquema que se declara en el XML.</summary>
    public const string EsquemaCufe = "CUFE-SHA384";

    /// <summary>
    /// Las notas no llevan CUFE sino CUDE. El NOMBRE es seguro; la formula
    /// con la que hoy se calcula no lo es —es la de factura— y eso queda
    /// declarado en docs/07-cobertura-ubl.md.
    /// </summary>
    public const string EsquemaCude = "CUDE-SHA384";

    public static string EsquemaPara(TipoDocumento tipo) =>
        tipo == TipoDocumento.Factura ? EsquemaCufe : EsquemaCude;

    public static string CalcularCufe(ValoresCufe valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        var bytes = SHA384.HashData(Encoding.UTF8.GetBytes(valores.Concatenar()));

        return Convert.ToHexStringLower(bytes);
    }
}
