using System.Globalization;
using System.Text.Json;

namespace CryptoTrader.Exchanges.Okx.Rest;

internal static class OkxJson
{
    public static bool TryReadDecimal(JsonElement element, out decimal value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var text = element.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    value = 0m;
                    return false;
                }

                return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            case JsonValueKind.Number:
                return element.TryGetDecimal(out value);
            default:
                value = 0m;
                return false;
        }
    }

    public static bool TryReadInt64(JsonElement element, out long value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var text = element.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    value = 0;
                    return false;
                }

                return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            case JsonValueKind.Number:
                return element.TryGetInt64(out value);
            default:
                value = 0;
                return false;
        }
    }

    public static string? ReadString(JsonElement item, string name)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty(name, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = element.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public static long? ReadOptionalUnixMilliseconds(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || !TryReadInt64(property, out var milliseconds))
        {
            return null;
        }

        return milliseconds;
    }

    public static DateTimeOffset ReadUnixTime(JsonElement element, string propertyName, TimeProvider time)
    {
        var milliseconds = ReadOptionalUnixMilliseconds(element, propertyName);
        return milliseconds is long value
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : time.GetUtcNow();
    }
}
