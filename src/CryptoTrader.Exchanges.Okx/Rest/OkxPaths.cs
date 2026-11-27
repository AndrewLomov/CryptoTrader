using System.Globalization;

namespace CryptoTrader.Exchanges.Okx.Rest;

internal static class OkxPaths
{
    /// <summary>REST <c>/api/v5/market/books</c> snapshot size. This is not an incremental book.</summary>
    public const int OrderBookDepth = 50;

    /// <summary>Public <c>books5</c> channel. Five levels, snapshot pushes, no sequence-gap recovery.</summary>
    public const string WebsocketBookChannel = "books5";

    public const int WebsocketBookDepth = 5;

    public static string Instruments(string instType, string? instId = null)
    {
        var path = "/api/v5/public/instruments?instType=" + Escape(instType);
        if (!string.IsNullOrWhiteSpace(instId))
        {
            path += "&instId=" + Escape(instId);
        }

        return path;
    }

    public static string Candles(bool history, string instId, string bar, long afterMs, int limit)
    {
        var endpoint = history ? "history-candles" : "candles";
        return $"/api/v5/market/{endpoint}?instId={Escape(instId)}&bar={Escape(bar)}&after={afterMs.ToString(CultureInfo.InvariantCulture)}&limit={limit.ToString(CultureInfo.InvariantCulture)}";
    }

    public static string Ticker(string instId) => "/api/v5/market/ticker?instId=" + Escape(instId);

    public static string Books(string instId) =>
        $"/api/v5/market/books?instId={Escape(instId)}&sz={OrderBookDepth.ToString(CultureInfo.InvariantCulture)}";

    public static string FundingRate(string instId) => "/api/v5/public/funding-rate?instId=" + Escape(instId);

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
