using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Exchanges.Okx.Rest;

namespace CryptoTrader.Exchanges.Okx.Mapping;

internal static class OkxInstrumentMapper
{
    public static Instrument? Map(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var instType = OkxJson.ReadString(item, "instType");
        var instId = OkxJson.ReadString(item, "instId");
        if (instId is null || instType is null)
        {
            return null;
        }

        var trading = string.Equals(OkxJson.ReadString(item, "state"), "live", StringComparison.Ordinal);
        if (string.Equals(instType, "SPOT", StringComparison.Ordinal))
        {
            return MapSpot(item, instId, trading);
        }

        if (string.Equals(instType, "SWAP", StringComparison.Ordinal))
        {
            return MapLinearSwap(item, instId, trading);
        }

        return null;
    }

    private static Instrument? MapSpot(JsonElement item, string instId, bool trading)
    {
        var baseAsset = OkxJson.ReadString(item, "baseCcy");
        var quoteAsset = OkxJson.ReadString(item, "quoteCcy");
        if (baseAsset is null || quoteAsset is null || string.Equals(baseAsset, quoteAsset, StringComparison.Ordinal))
        {
            return null;
        }

        if (!TryPositive(item, "tickSz", out var tick)
            || !TryPositive(item, "lotSz", out var step)
            || !TryPositive(item, "minSz", out var minQuantity))
        {
            return null;
        }

        try
        {
            return Instrument.Create(
                OkxSymbol.SpotId(instId),
                OkxExchangeAdapter.Id,
                instId,
                baseAsset,
                quoteAsset,
                InstrumentType.Spot,
                tick,
                step,
                minQuantity,
                minNotional: null,
                contractMultiplier: null,
                isTradingEnabled: trading,
                contractValue: null,
                contractValueCurrency: null,
                settlementCurrency: null,
                contractKind: null);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private static Instrument? MapLinearSwap(JsonElement item, string instId, bool trading)
    {
        if (!instId.EndsWith("-SWAP", StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.Equals(OkxJson.ReadString(item, "ctType"), "linear", StringComparison.Ordinal))
        {
            return null;
        }

        if (!TryPositive(item, "ctVal", out var contractValue)
            || !TryPositive(item, "tickSz", out var tick)
            || !TryPositive(item, "lotSz", out var step)
            || !TryPositive(item, "minSz", out var minQuantity))
        {
            return null;
        }

        decimal? contractMultiplier = null;
        if (item.TryGetProperty("ctMult", out var multiplierElement)
            && multiplierElement.ValueKind != JsonValueKind.Null
            && OkxJson.TryReadDecimal(multiplierElement, out var parsedMultiplier))
        {
            if (parsedMultiplier <= 0m)
            {
                return null;
            }

            contractMultiplier = parsedMultiplier;
        }

        var contractCurrency = OkxJson.ReadString(item, "ctValCcy");
        var settlementCurrency = OkxJson.ReadString(item, "settleCcy");
        if (contractCurrency is null || settlementCurrency is null)
        {
            return null;
        }

        var pair = instId[..^"-SWAP".Length];
        var split = pair.LastIndexOf('-');
        if (split <= 0 || split >= pair.Length - 1)
        {
            return null;
        }

        var baseAsset = pair[..split];
        var quoteAsset = pair[(split + 1)..];
        if (string.Equals(baseAsset, quoteAsset, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return Instrument.Create(
                OkxSymbol.LinearId(baseAsset, quoteAsset),
                OkxExchangeAdapter.Id,
                instId,
                baseAsset,
                quoteAsset,
                InstrumentType.LinearPerpetual,
                tick,
                step,
                minQuantity,
                null,
                contractMultiplier,
                trading,
                contractValue,
                contractCurrency,
                settlementCurrency,
                contractKind: "linear");
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private static bool TryPositive(JsonElement item, string name, out decimal value)
    {
        value = 0m;
        return item.TryGetProperty(name, out var element)
            && OkxJson.TryReadDecimal(element, out value)
            && value > 0m;
    }
}
