namespace FevCore.Domain.Documentos;

/// <summary>
/// Que impuesto aplicar y con que tarifa. Es la entrada del calculo,
/// no el resultado: el resultado es ImpuestoLinea.
/// </summary>
/// <param name="Tipo">Tipo de impuesto.</param>
/// <param name="Tarifa">Porcentaje, expresado como numero. Para el 19% de IVA, 19.0.</param>
public sealed record EspecificacionImpuesto(TipoImpuesto Tipo, decimal Tarifa);
