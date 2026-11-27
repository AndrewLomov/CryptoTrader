using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Exchanges.Okx.Mapping;

internal static class OkxIntervals
{
    public static string ToBar(Timeframe timeframe) => timeframe.Code switch
    {
        "1m" => "1m",
        "5m" => "5m",
        "15m" => "15m",
        "1h" => "1H",
        "4h" => "4H",
        "1d" => "1Dutc",
        _ => throw new InvalidOperationException($"Timeframe '{timeframe.Code}' is not an OKX bar."),
    };

    public static string ToCandleChannel(Timeframe timeframe) => "candle" + ToBar(timeframe);
}
