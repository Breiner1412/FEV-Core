using FevCore.Domain.Documentos;
using static FevCore.Domain.Tests.Documentos.FabricaDocumentos;

namespace FevCore.Domain.Tests.Documentos;

/// <summary>
/// FALLIDO significa que el documento no llego a un desenlace y hace falta
/// una persona. El historial dice cual de los casos es, porque solo uno
/// obliga a verificar ante la DIAN antes de reemplazarlo (RN-13, ADR-0015).
///
/// Antes habia dos textos: "resultado desconocido" y "no consta que
/// llegara". El segundo se escribia tambien cuando la autoridad SI habia
/// recibido el documento y devuelto con que consultarlo.
/// </summary>
public sealed class DesenlaceFallidoTests
{
    private static Documento EnProceso()
    {
        var factura = Factura();
        factura.IniciarProceso(Momento);
        return factura;
    }

    private static string DetalleDelFallo(Documento documento)
    {
        documento.RegistrarFallo("Se agotaron los intentos.", Momento);

        Assert.Equal(EstadoDocumento.Fallido, documento.Estado);

        return documento.Transiciones[^1].Detalle!;
    }

    /// <summary>RN-13. Nunca se envio: se reemplaza sin verificar nada.</summary>
    [Fact]
    public void Sin_ningun_envio_el_historial_dice_que_no_salio_de_aqui()
    {
        var detalle = DetalleDelFallo(EnProceso());

        Assert.StartsWith("NO SALIO DE AQUI", detalle);
    }

    /// <summary>RN-13. Los envios fallaron antes de llegar: tampoco salio.</summary>
    [Fact]
    public void Con_envios_que_no_llegaron_el_historial_dice_que_no_salio_de_aqui()
    {
        var documento = EnProceso();
        documento.RegistrarTransmision(ResultadoTransmision.ErrorTransitorio, Momento);
        documento.RegistrarTransmision(ResultadoTransmision.ErrorTransitorio, Momento);

        Assert.StartsWith("NO SALIO DE AQUI", DetalleDelFallo(documento));
    }

    /// <summary>
    /// RN-13. Llego, y el servicio rechazo la entrega (ADR-0014): no salio
    /// de aqui seria falso, pero tampoco quedo radicado.
    /// </summary>
    [Fact]
    public void Con_la_entrega_rechazada_el_historial_dice_que_no_quedo_radicado()
    {
        var documento = EnProceso();
        documento.RegistrarTransmision(ResultadoTransmision.ErrorDefinitivo, Momento);

        Assert.StartsWith("NO RADICADO", DetalleDelFallo(documento));
    }

    /// <summary>RN-13. Se sabe que llego: hay identificador de seguimiento.</summary>
    [Fact]
    public void Con_una_entrega_aceptada_el_historial_dice_que_esta_radicado_sin_veredicto()
    {
        var documento = EnProceso();
        documento.RegistrarTransmision(
            ResultadoTransmision.Aceptada, Momento, identificadorSeguimiento: "SEG-001");

        var detalle = DetalleDelFallo(documento);

        Assert.StartsWith("RADICADO SIN VEREDICTO", detalle);
        Assert.Contains("SEG-001", detalle);
    }

    /// <summary>RN-13. El unico caso que obliga a verificar ante la DIAN.</summary>
    [Fact]
    public void Con_un_envio_sin_respuesta_el_historial_dice_que_el_resultado_es_desconocido()
    {
        var documento = EnProceso();
        documento.RegistrarTransmision(ResultadoTransmision.SinRespuesta, Momento);

        var detalle = DetalleDelFallo(documento);

        Assert.StartsWith("RESULTADO DESCONOCIDO", detalle);
        Assert.Contains("DIAN", detalle);
    }
}
