using FevCore.Application.Abstracciones;
using FevCore.Domain.Comun;
using FevCore.Domain.Documentos;
using FevCore.Domain.Numeracion;

namespace FevCore.Application.Numeracion;

/// <summary>
/// Casos de uso de rangos de numeracion (RF-08, RF-10).
/// </summary>
public sealed class GestionRangos(
    IRepositorioRangos repositorio,
    TimeProvider reloj)
{
    /// <summary>
    /// Registra un rango autorizado por la DIAN.
    ///
    /// Antes de guardarlo comprueba INV-RAN-03 contra los rangos que ya
    /// existen del mismo tipo. Esa comprobacion no la puede hacer la entidad
    /// sola: un rango no conoce a los demas. Es la razon por la que existe
    /// esta clase y no basta con el constructor del dominio.
    /// </summary>
    public async Task<RangoNumeracion> RegistrarAsync(
        string prefijo,
        TipoDocumento tipoDocumento,
        long numeroInicial,
        long numeroFinal,
        DateOnly vigenteDesde,
        DateOnly vigenteHasta,
        string numeroAutorizacion,
        string claveTecnica,
        CancellationToken cancelacion = default)
    {
        var nuevo = RangoNumeracion.Crear(
            prefijo,
            tipoDocumento,
            numeroInicial,
            numeroFinal,
            vigenteDesde,
            vigenteHasta,
            numeroAutorizacion,
            claveTecnica,
            reloj.GetUtcNow());

        var existentes = await repositorio.ListarPorTipoAsync(tipoDocumento, cancelacion);

        var conflicto = existentes.FirstOrDefault(e => nuevo.SeSolapaCon(e));

        if (conflicto is not null)
        {
            throw new ExcepcionDominio(
                "RANGO_SOLAPADO",
                $"El rango solicitado se solapa con el rango {conflicto.Prefijo} " +
                $"{conflicto.NumeroInicial}-{conflicto.NumeroFinal}, vigente del " +
                $"{conflicto.VigenteDesde:yyyy-MM-dd} al {conflicto.VigenteHasta:yyyy-MM-dd}. " +
                "Dos rangos del mismo tipo no pueden compartir numeros ni regir al " +
                "mismo tiempo.");
        }

        await repositorio.AgregarAsync(nuevo, cancelacion);
        await repositorio.GuardarCambiosAsync(cancelacion);

        return nuevo;
    }

    public Task<IReadOnlyList<RangoNumeracion>> ListarAsync(
        CancellationToken cancelacion = default) =>
        repositorio.ListarAsync(cancelacion);

    public Task<RangoNumeracion?> ObtenerAsync(
        Guid id,
        CancellationToken cancelacion = default) =>
        repositorio.ObtenerPorIdAsync(id, cancelacion);

    /// <summary>
    /// La fecha de hoy en Colombia, para RF-10. En UTC, despues de las 19:00
    /// "hoy" ya seria manana y los dias para vencer saldrian con uno menos.
    /// </summary>
    public DateOnly Hoy() => HoraColombia.Fecha(reloj.GetUtcNow());
}
