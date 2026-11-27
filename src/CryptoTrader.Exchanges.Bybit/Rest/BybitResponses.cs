using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CryptoTrader.Exchanges.Bybit.Rest;

internal static class BybitResponses
{
    public static JsonElement RequireResult(JsonDocument document, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = document.RootElement;
        if (!TryReadCode(root, out var code))
        {
            throw new InvalidOperationException("Bybit response did not include retCode.");
        }

        if (code != 0)
        {
            logger.LogWarning("Bybit public request was rejected with retCode {RetCode}.", code);
            throw new InvalidOperationException(ReadMessage(root) ?? Fallback(code));
        }

        if (!root.TryGetProperty("result", out var result))
        {
            throw new InvalidOperationException("Bybit response did not include a result.");
        }

        return result;
    }

    public static string? ReadMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return ReadMessage(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? ReadMessage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("retMsg", out var message)
            || message.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = message.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool TryReadCode(JsonElement root, out int code)
    {
        code = 0;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("retCode", out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetInt32(out code);
        }

        return element.ValueKind == JsonValueKind.String
            && int.TryParse(element.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out code);
    }

    private static string Fallback(int code) =>
        code == 10006 ? "Bybit rate limit (retCode 10006)." : $"Bybit retCode {code}.";
}
