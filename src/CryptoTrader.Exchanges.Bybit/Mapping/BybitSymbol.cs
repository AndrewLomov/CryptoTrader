using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;

namespace CryptoTrader.Exchanges.Bybit.Mapping;

internal readonly record struct BybitMarket(string Category, string Symbol, InstrumentId Instrument, InstrumentType Type)
{
    public bool IsSpot => Type == InstrumentType.Spot;
}

internal static class BybitSymbol
{
    private const string SpotPrefix = "BYBIT-SPOT-";
    private const string LinearPrefix = "BYBIT-LINEAR-";

    public static InstrumentId SpotId(string symbol) => new(SpotPrefix + symbol);

    public static InstrumentId LinearId(string symbol) => new(LinearPrefix + symbol);

    public static BybitMarket Require(InstrumentId instrument)
    {
        if (!TryParse(instrument, out var market))
        {
            throw new InvalidOperationException("Instrument id must be BYBIT-SPOT-{symbol} or BYBIT-LINEAR-{symbol}.");
        }

        return market;
    }

    public static bool TryParse(InstrumentId instrument, out BybitMarket market)
    {
        var value = instrument.Value;
        if (value.StartsWith(SpotPrefix, StringComparison.Ordinal))
        {
            return TryCreate(value[SpotPrefix.Length..], "spot", InstrumentType.Spot, instrument, out market);
        }

        if (value.StartsWith(LinearPrefix, StringComparison.Ordinal))
        {
            return TryCreate(value[LinearPrefix.Length..], "linear", InstrumentType.LinearPerpetual, instrument, out market);
        }

        market = default;
        return false;
    }

    private static bool TryCreate(
        string symbol,
        string category,
        InstrumentType type,
        InstrumentId instrument,
        out BybitMarket market)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            market = default;
            return false;
        }

        market = new BybitMarket(category, symbol, instrument, type);
        return true;
    }
}
