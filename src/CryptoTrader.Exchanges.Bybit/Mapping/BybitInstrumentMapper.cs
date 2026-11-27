using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Exchanges.Bybit.Rest;

namespace CryptoTrader.Exchanges.Bybit.Mapping;

internal static class BybitInstrumentMapper
{
    public static Instrument Map(JsonElement item, string category)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Bybit instrument payload was not an object.");
        }

        var symbol = RequiredText(item, "symbol");
        var baseAsset = RequiredText(item, "baseCoin");
        var quoteAsset = RequiredText(item, "quoteCoin");
        if (!item.TryGetProperty("priceFilter", out var priceFilter) || priceFilter.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Bybit instrument {symbol} is missing priceFilter.");
        }

        if (!item.TryGetProperty("lotSizeFilter", out var lotSize) || lotSize.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Bybit instrument {symbol} is missing lotSizeFilter.");
        }

        var type = category switch
        {
            "spot" => InstrumentType.Spot,
            "linear" => InstrumentType.LinearPerpetual,
            _ => throw new InvalidOperationException($"Bybit category '{category}' is not supported."),
        };
        var status = item.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String
            ? statusElement.GetString()
            : null;
        var id = type == InstrumentType.Spot ? BybitSymbol.SpotId(symbol) : BybitSymbol.LinearId(symbol);
        return Instrument.Create(
            id,
            ExchangeId.Bybit,
            symbol,
            baseAsset,
            quoteAsset,
            type,
            RequiredPositive(priceFilter, "tickSize", symbol),
            QuantityStep(lotSize, symbol),
            RequiredPositive(lotSize, "minOrderQty", symbol),
            MinNotional(lotSize),
            ContractMultiplier(item, type),
            string.Equals(status, "Trading", StringComparison.OrdinalIgnoreCase));
    }

    private static decimal QuantityStep(JsonElement lotSize, string symbol)
    {
        if (TryPositive(lotSize, "qtyStep", out var qtyStep))
        {
            return qtyStep;
        }

        // Spot payloads publish the quantity step as basePrecision when qtyStep is absent.
        if (TryPositive(lotSize, "basePrecision", out var basePrecision))
        {
            return basePrecision;
        }

        throw new InvalidOperationException($"Bybit instrument {symbol} is missing lotSizeFilter.qtyStep.");
    }

    private static decimal? MinNotional(JsonElement lotSize)
    {
        foreach (var name in new[] { "minNotionalValue", "minNotional", "minOrderAmt" })
        {
            if (TryPositive(lotSize, name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static decimal? ContractMultiplier(JsonElement item, InstrumentType type)
    {
        if (type == InstrumentType.Spot)
        {
            return null;
        }

        return TryPositive(item, "contractMultiplier", out var multiplier) ? multiplier : 1m;
    }

    private static decimal RequiredPositive(JsonElement parent, string propertyName, string symbol)
    {
        if (!TryPositive(parent, propertyName, out var value))
        {
            throw new InvalidOperationException($"Bybit instrument {symbol} is missing {propertyName}.");
        }

        return value;
    }

    private static bool TryPositive(JsonElement parent, string propertyName, out decimal value)
    {
        value = 0m;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(propertyName, out var element)
            && BybitJson.TryReadDecimal(element, out value)
            && value > 0m;
    }

    private static string RequiredText(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new InvalidOperationException($"Bybit instrument is missing {propertyName}.");
        }

        return element.GetString()!.Trim();
    }
}
