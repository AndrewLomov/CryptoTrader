using System.Text.Json;

namespace CryptoTrader.Exchanges.Okx.Rest;

internal static class OkxResponses
{
    public const string SuccessCode = "0";

    public const string RateLimitCode = "50011";

    public static JsonElement RequireData(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data))
        {
            throw new OkxApiException("OKX response did not include data.");
        }

        return data;
    }

    public static string? ReadCode(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("code", out var code))
        {
            return null;
        }

        return code.ValueKind switch
        {
            JsonValueKind.String => code.GetString(),
            JsonValueKind.Number => code.GetRawText(),
            _ => null,
        };
    }

    public static string? ReadMessage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("msg", out var message)
            || message.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return SafeText(message.GetString());
    }

    public static string? SafeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Contains("passphrase", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
            || value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("sign", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Length <= 180 ? value : value[..180];
    }
}
