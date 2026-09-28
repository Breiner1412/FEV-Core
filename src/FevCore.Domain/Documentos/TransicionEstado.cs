namespace FevCore.Domain.Documentos;

/// <summary>
/// Un cambio de estado que ya ocurrio (RN-12, RF-23).
///
/// Es inmutable: no tiene un solo metodo que la modifique, y ninguna
/// propiedad tiene setter (INV-TRA-01). Una transicion registrada es un
/// hecho historico; corregirla seria reescribir lo que paso.
///
/// La primera transicion de todo documento tiene EstadoAnterior nulo: el
/// documento no venia de ningun estado, acababa de nacer.
/// </summary>
public sealed class TransicionEstado
{
    /// <summary>
    /// Posicion en el historial, empezando en 1.
    ///
    /// Existe porque la marca de tiempo no alcanza para ordenar: dos
    /// transiciones pueden caer en el mismo instante, y entonces el
    /// historial saldria en un orden u otro segun le pareciera a la base de
    /// datos. Con la secuencia, la cadena de INV-TRA-02 siempre se lee en el
    /// orden en que ocurrio.
    ///
    /// Es tambien la identidad de la transicion: documento mas posicion. Una
    /// transicion no existe fuera de su documento, asi que no necesita un
    /// identificador propio.
    /// </summary>
    public int Secuencia { get; }

    /// <summary>Nulo solo en la primera transicion, la del nacimiento.</summary>
    public EstadoDocumento? EstadoAnterior { get; }

    public EstadoDocumento EstadoNuevo { get; }
    public DateTimeOffset OcurridaEn { get; }

    /// <summary>Por que ocurrio. Obligatorio: una transicion sin motivo no se registra.</summary>
    public string Motivo { get; }

    /// <summary>Informacion adicional, como el identificador de seguimiento.</summary>
    public string? Detalle { get; }

    /// <summary>Requerido por Entity Framework.</summary>
    private TransicionEstado()
    {
        Motivo = null!;
    }

    private TransicionEstado(
        int secuencia,
        EstadoDocumento? estadoAnterior,
        EstadoDocumento estadoNuevo,
        DateTimeOffset ocurridaEn,
        string motivo,
        string? detalle)
    {
        Secuencia = secuencia;
        EstadoAnterior = estadoAnterior;
        EstadoNuevo = estadoNuevo;
        OcurridaEn = ocurridaEn;
        Motivo = motivo;
        Detalle = detalle;
    }

    internal static TransicionEstado Registrar(
        int secuencia,
        EstadoDocumento? estadoAnterior,
        EstadoDocumento estadoNuevo,
        DateTimeOffset ocurridaEn,
        string motivo,
        string? detalle = null)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new Comun.ExcepcionDominio(
                "TRANSICION_SIN_MOTIVO",
                "Toda transicion debe registrar por que ocurrio (RN-12).");
        }

        return new TransicionEstado(
            secuencia,
            estadoAnterior,
            estadoNuevo,
            ocurridaEn,
            motivo.Trim(),
            string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim());
    }
}
