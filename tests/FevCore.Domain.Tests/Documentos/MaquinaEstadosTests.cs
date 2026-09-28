using FevCore.Domain.Documentos;

namespace FevCore.Domain.Tests.Documentos;

/// <summary>
/// La tabla de transiciones contra la seccion 6.2 de los requerimientos.
///
/// Se prueban las seis transiciones validas UNA POR UNA en lugar de recorrer
/// la tabla en un bucle: un bucle sobre la misma tabla que se quiere
/// verificar no prueba nada, solo comprueba que la tabla es igual a si
/// misma. Estas seis lineas estan copiadas del documento, no del codigo.
/// </summary>
public sealed class MaquinaEstadosTests
{
    [Theory]
    [InlineData(EstadoDocumento.Recibido, EstadoDocumento.EnProceso)]
    [InlineData(EstadoDocumento.EnProceso, EstadoDocumento.Transmitido)]
    [InlineData(EstadoDocumento.EnProceso, EstadoDocumento.Fallido)]
    [InlineData(EstadoDocumento.Transmitido, EstadoDocumento.Aprobado)]
    [InlineData(EstadoDocumento.Transmitido, EstadoDocumento.Rechazado)]
    [InlineData(EstadoDocumento.Transmitido, EstadoDocumento.Fallido)]
    public void Las_transiciones_del_documento_de_requerimientos_estan_permitidas(
        EstadoDocumento desde, EstadoDocumento hacia) =>
        Assert.True(MaquinaEstados.Permite(desde, hacia));

    [Theory]
    [InlineData(EstadoDocumento.Recibido, EstadoDocumento.Transmitido)]  // se salta EnProceso
    [InlineData(EstadoDocumento.Recibido, EstadoDocumento.Aprobado)]     // se salta todo
    [InlineData(EstadoDocumento.EnProceso, EstadoDocumento.Aprobado)]    // aprobar sin transmitir
    [InlineData(EstadoDocumento.EnProceso, EstadoDocumento.Recibido)]    // marcha atras
    [InlineData(EstadoDocumento.Transmitido, EstadoDocumento.EnProceso)] // marcha atras
    public void Los_atajos_y_las_marchas_atras_no_estan_permitidos(
        EstadoDocumento desde, EstadoDocumento hacia) =>
        Assert.False(MaquinaEstados.Permite(desde, hacia));

    [Theory]
    [InlineData(EstadoDocumento.Aprobado)]
    [InlineData(EstadoDocumento.Rechazado)]
    [InlineData(EstadoDocumento.Fallido)]
    public void Los_estados_finales_no_tienen_salida(EstadoDocumento estado)
    {
        Assert.True(MaquinaEstados.EsTerminal(estado));
        Assert.Empty(MaquinaEstados.DestinosDesde(estado));
    }

    [Theory]
    [InlineData(EstadoDocumento.Recibido)]
    [InlineData(EstadoDocumento.EnProceso)]
    [InlineData(EstadoDocumento.Transmitido)]
    public void Los_estados_intermedios_si_tienen_salida(EstadoDocumento estado)
    {
        Assert.False(MaquinaEstados.EsTerminal(estado));
        Assert.NotEmpty(MaquinaEstados.DestinosDesde(estado));
    }

    /// <summary>
    /// Ningun estado del enum puede quedarse fuera de la tabla. Si manana
    /// alguien agrega uno y olvida declarar sus transiciones, esta prueba
    /// falla con KeyNotFoundException en vez de que el sistema reviente la
    /// primera vez que un documento llegue ahi.
    /// </summary>
    [Fact]
    public void Todos_los_estados_estan_en_la_tabla()
    {
        foreach (var estado in Enum.GetValues<EstadoDocumento>())
        {
            MaquinaEstados.DestinosDesde(estado);
        }
    }
}
