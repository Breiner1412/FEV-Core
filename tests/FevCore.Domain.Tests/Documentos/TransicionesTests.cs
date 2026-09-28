using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using static FevCore.Domain.Tests.Documentos.FabricaDocumentos;

namespace FevCore.Domain.Tests.Documentos;

/// <summary>RN-11, RN-12, RF-23, INV-DOC-08, INV-TRA-01, INV-TRA-02.</summary>
public sealed class TransicionesTests
{
    [Fact]
    public void Un_documento_nace_con_su_nacimiento_ya_registrado()
    {
        var factura = Factura();

        var nacimiento = Assert.Single(factura.Transiciones);

        // Nulo porque no venia de ningun estado: acababa de existir.
        Assert.Null(nacimiento.EstadoAnterior);
        Assert.Equal(EstadoDocumento.Recibido, nacimiento.EstadoNuevo);
        Assert.False(string.IsNullOrWhiteSpace(nacimiento.Motivo));
    }

    [Fact]
    public void Transicionar_cambia_el_estado_y_deja_registro()
    {
        var factura = Factura();

        factura.Transicionar(
            EstadoDocumento.EnProceso,
            "Generando XML.",
            Momento,
            detalle: "intento 1");

        Assert.Equal(EstadoDocumento.EnProceso, factura.Estado);
        Assert.Equal(2, factura.Transiciones.Count);

        var ultima = factura.Transiciones[^1];

        Assert.Equal(EstadoDocumento.Recibido, ultima.EstadoAnterior);
        Assert.Equal(EstadoDocumento.EnProceso, ultima.EstadoNuevo);
        Assert.Equal("Generando XML.", ultima.Motivo);
        Assert.Equal("intento 1", ultima.Detalle);
        Assert.Equal(Momento, ultima.OcurridaEn);
    }

    /// <summary>
    /// INV-TRA-02: cada transicion empalma con la anterior. Si el historial
    /// dijera "de Transmitido a Aprobado" justo despues de una que termino
    /// en EnProceso, faltaria un eslabon y el historial seria mentira.
    /// </summary>
    [Fact]
    public void El_historial_forma_una_cadena_sin_huecos()
    {
        var factura = FacturaAprobada();

        Assert.Equal(4, factura.Transiciones.Count);

        for (var i = 1; i < factura.Transiciones.Count; i++)
        {
            Assert.Equal(
                factura.Transiciones[i - 1].EstadoNuevo,
                factura.Transiciones[i].EstadoAnterior);
        }

        Assert.Equal(EstadoDocumento.Aprobado, factura.Transiciones[^1].EstadoNuevo);
    }

    [Fact]
    public void No_se_puede_saltar_un_estado()
    {
        var factura = Factura();

        var error = Assert.Throws<ExcepcionDominio>(() =>
            factura.Transicionar(EstadoDocumento.Aprobado, "Atajo.", Momento));

        Assert.Equal("TRANSICION_INVALIDA", error.Codigo);

        // El mensaje dice que SI se puede hacer, no solo que no se puede.
        Assert.Contains("EnProceso", error.Message);

        // Y el documento no se movio.
        Assert.Equal(EstadoDocumento.Recibido, factura.Estado);
        Assert.Single(factura.Transiciones);
    }

    [Theory]
    [InlineData(EstadoDocumento.Aprobado)]
    [InlineData(EstadoDocumento.Rechazado)]
    public void Un_documento_terminado_no_vuelve_a_cambiar(EstadoDocumento terminal)
    {
        var factura = Factura();
        factura.Transicionar(EstadoDocumento.EnProceso, "Generando.", Momento);
        factura.Transicionar(EstadoDocumento.Transmitido, "Enviado.", Momento);
        factura.Transicionar(terminal, "Veredicto.", Momento);

        var error = Assert.Throws<ExcepcionDominio>(() =>
            factura.Transicionar(EstadoDocumento.EnProceso, "Reintentar.", Momento));

        Assert.Equal("ESTADO_TERMINAL", error.Codigo);
        Assert.Equal(terminal, factura.Estado);
    }

    /// <summary>
    /// RN-07 expresada como consecuencia: un rechazado no se corrige, se
    /// reemplaza con un documento nuevo. La maquina de estados lo impone
    /// sola, sin una regla aparte que alguien pueda olvidar aplicar.
    /// </summary>
    [Fact]
    public void Un_documento_rechazado_no_se_puede_retransmitir()
    {
        var factura = Factura();
        factura.Transicionar(EstadoDocumento.EnProceso, "Generando.", Momento);
        factura.Transicionar(EstadoDocumento.Transmitido, "Enviado.", Momento);
        factura.Transicionar(EstadoDocumento.Rechazado, "Rechazado por la DIAN.", Momento);

        var error = Assert.Throws<ExcepcionDominio>(() =>
            factura.Transicionar(EstadoDocumento.Transmitido, "Otra vez.", Momento));

        Assert.Equal("ESTADO_TERMINAL", error.Codigo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Una_transicion_sin_motivo_no_se_registra(string motivo)
    {
        var factura = Factura();

        var error = Assert.Throws<ExcepcionDominio>(() =>
            factura.Transicionar(EstadoDocumento.EnProceso, motivo, Momento));

        Assert.Equal("TRANSICION_SIN_MOTIVO", error.Codigo);

        // Nada quedo a medias: ni registro ni cambio de estado.
        Assert.Single(factura.Transiciones);
        Assert.Equal(EstadoDocumento.Recibido, factura.Estado);
    }

    [Fact]
    public void Fallido_se_puede_alcanzar_desde_los_dos_estados_intermedios()
    {
        var desdeProceso = Factura();
        desdeProceso.Transicionar(EstadoDocumento.EnProceso, "Generando.", Momento);
        desdeProceso.Transicionar(EstadoDocumento.Fallido, "No se pudo firmar.", Momento);
        Assert.Equal(EstadoDocumento.Fallido, desdeProceso.Estado);

        var desdeTransmitido = Factura(consecutivo: 2);
        desdeTransmitido.Transicionar(EstadoDocumento.EnProceso, "Generando.", Momento);
        desdeTransmitido.Transicionar(EstadoDocumento.Transmitido, "Enviado.", Momento);
        desdeTransmitido.Transicionar(EstadoDocumento.Fallido, "Sin veredicto.", Momento);
        Assert.Equal(EstadoDocumento.Fallido, desdeTransmitido.Estado);
    }
}
