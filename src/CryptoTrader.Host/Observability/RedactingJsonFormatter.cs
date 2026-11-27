using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;

namespace CryptoTrader.Host.Observability;

internal sealed class RedactingJsonFormatter : ITextFormatter
{
    private readonly JsonFormatter _formatter = new(Environment.NewLine, renderMessage: true, formatProvider: null);

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);
        var properties = logEvent.Properties
            .Where(property => !IsSensitive(property.Key))
            .Select(property => new LogEventProperty(property.Key, Redact(property.Value)));
        var redacted = new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            logEvent.Exception,
            logEvent.MessageTemplate,
            properties);
        _formatter.Format(redacted, output);
    }

    private static LogEventPropertyValue Redact(LogEventPropertyValue value) => value switch
    {
        StructureValue structure => new StructureValue(
            structure.Properties
                .Where(property => !IsSensitive(property.Name))
                .Select(property => new LogEventProperty(property.Name, Redact(property.Value))),
            structure.TypeTag),
        SequenceValue sequence => new SequenceValue(sequence.Elements.Select(Redact)),
        DictionaryValue dictionary => new DictionaryValue(
            dictionary.Elements
                .Where(pair => pair.Key.Value is not string key || !IsSensitive(key))
                .Select(pair => new KeyValuePair<ScalarValue, LogEventPropertyValue>(pair.Key, Redact(pair.Value)))),
        _ => value,
    };

    internal static bool IsSensitive(string name) =>
        name.Contains("key", StringComparison.OrdinalIgnoreCase)
        || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("token", StringComparison.OrdinalIgnoreCase)
        || name.Contains("password", StringComparison.OrdinalIgnoreCase)
        || name.Contains("signature", StringComparison.OrdinalIgnoreCase);
}
