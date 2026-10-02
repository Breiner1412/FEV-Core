namespace FevCore.Integration.Tests;

/// <summary>
/// Un reloj que siempre da la misma hora. Para las pruebas que dependen de
/// "ahora", como los dias que faltan para que venza un rango (RF-10).
/// </summary>
public sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora.ToUniversalTime();
}
