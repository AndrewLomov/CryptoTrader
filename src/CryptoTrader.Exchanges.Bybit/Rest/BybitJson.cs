using System.Globalization;
using System.Text.Json;

namespace CryptoTrader.Exchanges.Bybit.Rest;

internal static class BybitJson
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

    public static bool TryFirstObject(JsonElement result, out JsonElement item)
    {
        item = default;
        if (result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("list", out var list)
            || list.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var candidate in list.EnumerateArray())
        {
            if (candidate.ValueKind == JsonValueKind.Object)
            {
                item = candidate;
                return true;
            }
        }

        return false;
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

    public static DateTimeOffset ReadUnixTime(
        JsonElement primary,
        string propertyName,
        JsonElement fallback,
        TimeProvider time)
    {
        var milliseconds = ReadOptionalUnixMilliseconds(primary, propertyName)
            ?? ReadOptionalUnixMilliseconds(fallback, "time")
            ?? ReadOptionalUnixMilliseconds(fallback, "ts");
        return milliseconds is long value
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : time.GetUtcNow();
    }
}
