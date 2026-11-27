using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Exchanges.Bybit.Mapping;

internal static class BybitIntervals
{
    public static string ToExchange(Timeframe timeframe) => timeframe.Code switch
    {
        "1m" => "1",
        "5m" => "5",
        "15m" => "15",
        "1h" => "60",
        "4h" => "240",
        "1d" => "D",
        _ => throw new InvalidOperationException($"Timeframe '{timeframe.Code}' is not a Bybit V5 interval."),
    };
}
