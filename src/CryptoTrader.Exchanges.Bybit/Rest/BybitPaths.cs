using System.Globalization;

namespace CryptoTrader.Exchanges.Bybit.Rest;

internal static class BybitPaths
{
    public static string Instruments(string category, string symbol) =>
        $"/v5/market/instruments-info?category={Escape(category)}&symbol={Escape(symbol)}";

    public static string Kline(string category, string symbol, string interval, long startMs, long endMs) =>
        $"/v5/market/kline?category={Escape(category)}&symbol={Escape(symbol)}&interval={Escape(interval)}&start={startMs.ToString(CultureInfo.InvariantCulture)}&end={endMs.ToString(CultureInfo.InvariantCulture)}&limit=1000";

    public static string Tickers(string category, string symbol) =>
        $"/v5/market/tickers?category={Escape(category)}&symbol={Escape(symbol)}";

    public static string OrderBook(string category, string symbol) =>
        $"/v5/market/orderbook?category={Escape(category)}&symbol={Escape(symbol)}&limit=50";

    public static string FundingHistory(string symbol) =>
        $"/v5/market/funding/history?category=linear&symbol={Escape(symbol)}&limit=1";

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
