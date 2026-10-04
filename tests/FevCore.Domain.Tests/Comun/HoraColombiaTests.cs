using FevCore.Domain.Comun;

namespace FevCore.Domain.Tests.Comun;

/// <summary>
/// La fecha civil colombiana de un instante (RN-02, RF-10, RF-24).
///
/// Es la frontera que importa: entre las 19:00 y la medianoche en Colombia,
/// UTC ya esta en el dia siguiente.
/// </summary>
public sealed class HoraColombiaTests
{
    [Fact]
    public void Las_19_30_del_31_de_diciembre_en_Colombia_siguen_siendo_31_aunque_en_UTC_sea_1_de_enero()
    {
        var instante = new DateTimeOffset(2027, 1, 1, 0, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 12, 31), HoraColombia.Fecha(instante));
    }

    [Fact]
    public void La_fecha_no_depende_del_desfase_con_que_venga_escrito_el_instante()
    {
        var enUtc = new DateTimeOffset(2026, 9, 25, 3, 0, 0, TimeSpan.Zero);
        var enColombia = enUtc.ToOffset(TimeSpan.FromHours(-5));

        Assert.Equal(HoraColombia.Fecha(enUtc), HoraColombia.Fecha(enColombia));
        Assert.Equal(new DateOnly(2026, 9, 24), HoraColombia.Fecha(enUtc));
    }

    [Fact]
    public void Un_dia_colombiano_empieza_a_las_cinco_de_la_manana_en_UTC()
    {
        var inicio = HoraColombia.InicioDe(new DateOnly(2026, 3, 5));

        Assert.Equal(
            new DateTimeOffset(2026, 3, 5, 5, 0, 0, TimeSpan.Zero),
            inicio.ToUniversalTime());
    }
}
