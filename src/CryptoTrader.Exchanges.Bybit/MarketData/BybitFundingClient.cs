using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Bybit.Mapping;
using CryptoTrader.Exchanges.Bybit.Rest;
using Microsoft.Extensions.Logging;

namespace CryptoTrader.Exchanges.Bybit.MarketData;

public sealed class BybitFundingClient : IFundingRateClient
{
    private readonly BybitPublicRest _rest;
    private readonly TimeProvider _time;
    private readonly ILogger<BybitFundingClient> _logger;

    internal BybitFundingClient(BybitPublicRest rest, TimeProvider time, ILogger<BybitFundingClient> logger)
    {
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _rest = rest;
        _time = time;
        _logger = logger;
    }

    public Task<FundingRate?> GetCurrentAsync(InstrumentId instrument, CancellationToken cancellationToken)
    {
        var market = BybitSymbol.Require(instrument);
        if (market.IsSpot)
        {
            return Task.FromResult<FundingRate?>(null);
        }

        return GetLinearAsync(market, cancellationToken);
    }

    private async Task<FundingRate?> GetLinearAsync(BybitMarket market, CancellationToken cancellationToken)
    {
        var received = _time.GetUtcNow();
        using (var history = await _rest.GetAsync(BybitPaths.FundingHistory(market.Symbol), cancellationToken))
        {
            var result = BybitResponses.RequireResult(history, _logger);
            var serverTime = BybitJson.ReadOptionalUnixMilliseconds(history.RootElement, "time");
            if (BybitJson.TryFirstObject(result, out var item)
                && TryCreate(item, "fundingRate", "fundingRateTimestamp", serverTime, market.Instrument, received, out var funding))
            {
                return funding;
            }
        }

        using var tickers = await _rest.GetAsync(BybitPaths.Tickers(market.Category, market.Symbol), cancellationToken);
        var tickerResult = BybitResponses.RequireResult(tickers, _logger);
        var tickerServerTime = BybitJson.ReadOptionalUnixMilliseconds(tickers.RootElement, "time");
        if (BybitJson.TryFirstObject(tickerResult, out var ticker)
            && TryCreate(ticker, "fundingRate", "nextFundingTime", tickerServerTime, market.Instrument, received, out var fromTicker))
        {
            return fromTicker;
        }

        return null;
    }

    private static bool TryCreate(
        JsonElement item,
        string rateName,
        string timeName,
        long? serverTimeMs,
        InstrumentId instrument,
        DateTimeOffset receivedAtUtc,
        out FundingRate? funding)
    {
        funding = null;
        if (!item.TryGetProperty(rateName, out var rateElement) || !BybitJson.TryReadDecimal(rateElement, out var rate))
        {
            return false;
        }

        var timeMs = BybitJson.ReadOptionalUnixMilliseconds(item, timeName) ?? serverTimeMs;
        if (timeMs is not long fundingMs)
        {
            return false;
        }

        funding = FundingRate.Create(
            ExchangeId.Bybit,
            instrument,
            rate,
            DateTimeOffset.FromUnixTimeMilliseconds(fundingMs),
            receivedAtUtc);
        return true;
    }
}
