using CryptoTrader.Domain.Common;

namespace CryptoTrader.Exchanges.Okx.Mapping;

internal readonly record struct OkxMarket(string InstId, InstrumentId Instrument, bool IsSpot);

internal static class OkxSymbol
{
    public const string SpotPrefix = "OKX-SPOT-";

    public const string LinearPrefix = "OKX-LINEAR-SWAP-";

    public static InstrumentId SpotId(string instId) => new(SpotPrefix + instId);

    public static InstrumentId LinearId(string baseAsset, string quoteAsset) =>
        new(LinearPrefix + baseAsset + "-" + quoteAsset);

    public static OkxMarket Require(InstrumentId instrument)
    {
        if (!TryParse(instrument, out var market))
        {
            throw new InvalidOperationException("Instrument id must be OKX-SPOT-{instId} or OKX-LINEAR-SWAP-{base}-{quote}.");
        }

        return market;
    }

    public static bool TryParse(InstrumentId instrument, out OkxMarket market)
    {
        var value = instrument.Value;
        if (value.StartsWith(SpotPrefix, StringComparison.Ordinal))
        {
            var instId = value[SpotPrefix.Length..];
            if (string.IsNullOrWhiteSpace(instId))
            {
                market = default;
                return false;
            }

            market = new OkxMarket(instId, instrument, IsSpot: true);
            return true;
        }

        if (value.StartsWith(LinearPrefix, StringComparison.Ordinal))
        {
            var pair = value[LinearPrefix.Length..];
            if (string.IsNullOrWhiteSpace(pair) || !pair.Contains('-', StringComparison.Ordinal))
            {
                market = default;
                return false;
            }

            market = new OkxMarket(pair + "-SWAP", instrument, IsSpot: false);
            return true;
        }

        market = default;
        return false;
    }
}
