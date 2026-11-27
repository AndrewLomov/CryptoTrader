using Serilog.Events;

namespace CryptoTrader.Host.Observability;

internal static class LogLevelParser
{
    public static LogEventLevel Parse(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "verbose" or "trace" => LogEventLevel.Verbose,
        "debug" => LogEventLevel.Debug,
        "warning" => LogEventLevel.Warning,
        "error" => LogEventLevel.Error,
        "fatal" => LogEventLevel.Fatal,
        _ => LogEventLevel.Information,
    };
}
