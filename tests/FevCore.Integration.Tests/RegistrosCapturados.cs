using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FevCore.Integration.Tests;

/// <summary>
/// Un registro tal como lo veria quien lo busca: su categoria, su mensaje y
/// todos sus campos, los del propio mensaje y los de los scopes activos.
/// </summary>
public sealed record RegistroCapturado(
    string Categoria,
    string Mensaje,
    IReadOnlyDictionary<string, object?> Campos);

/// <summary>
/// Proveedor de registros que guarda lo que se escribe, con sus scopes.
///
/// Los scopes son la mitad de RNF-10: el traceId y el documento llegan a un
/// registro por el scope en el que se escribio, no por su mensaje. Un
/// proveedor que no los mirara haria pasar la prueba aunque nadie los
/// abriera.
/// </summary>
public sealed class RegistrosCapturados : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public ConcurrentQueue<RegistroCapturado> Registros { get; } = new();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public ILogger CreateLogger(string categoryName) => new Registrador(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Registrador(RegistrosCapturados dueno, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            dueno._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var campos = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            Agregar(campos, state);
            dueno._scopes.ForEachScope((scope, acumulado) => Agregar(acumulado, scope), campos);

            dueno.Registros.Enqueue(new RegistroCapturado(
                categoria, formatter(state, exception), campos));
        }

        private static void Agregar(Dictionary<string, object?> campos, object? estado)
        {
            if (estado is IEnumerable<KeyValuePair<string, object?>> pares)
            {
                foreach (var (clave, valor) in pares)
                {
                    campos[clave] = valor;
                }
            }
            else if (estado is IEnumerable<KeyValuePair<string, object>> paresNoNulos)
            {
                foreach (var (clave, valor) in paresNoNulos)
                {
                    campos[clave] = valor;
                }
            }
        }
    }
}
