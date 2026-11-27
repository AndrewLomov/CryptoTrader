using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Okx.Mapping;
using CryptoTrader.Exchanges.Okx.Rest;
using Microsoft.Extensions.Logging;

namespace CryptoTrader.Exchanges.Okx.MarketData;

public sealed class OkxFundingClient : IFundingRateClient
{
    private readonly OkxPublicRest _rest;
    private readonly TimeProvider _time;
    private readonly ILogger<OkxFundingClient> _logger;

    internal OkxFundingClient(OkxPublicRest rest, TimeProvider time, ILogger<OkxFundingClient> logger)
    {
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _rest = rest;
        _time = time;
        _logger = logger;
    }

    public async Task<FundingRate?> GetCurrentAsync(InstrumentId instrument, CancellationToken cancellationToken)
    {
        var market = OkxSymbol.Require(instrument);
        if (market.IsSpot)
        {
            return null;
        }

        using var document = await _rest.GetAsync(OkxPaths.FundingRate(market.InstId), cancellationToken);
        var data = OkxResponses.RequireData(document);
        if (data.ValueKind != JsonValueKind.Array)
        {
            throw new OkxApiException("OKX funding rate response did not include data.");
        }

        JsonElement? row = null;
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                row = item;
                break;
            }
        }

        if (row is null)
        {
            _logger.LogDebug("OKX funding rate list was empty.");
            return null;
        }

        var itemRow = row.Value;
        if (!itemRow.TryGetProperty("fundingRate", out var rateElement) || !OkxJson.TryReadDecimal(rateElement, out var rate))
        {
            throw new OkxApiException("OKX funding rate is missing fundingRate.");
        }

        var timeMs = OkxJson.ReadOptionalUnixMilliseconds(itemRow, "fundingTime")
            ?? OkxJson.ReadOptionalUnixMilliseconds(itemRow, "nextFundingTime");
        if (timeMs is not long fundingMs)
        {
            throw new OkxApiException("OKX funding rate is missing fundingTime.");
        }

        DateTimeOffset fundingTime;
        try
        {
            fundingTime = DateTimeOffset.FromUnixTimeMilliseconds(fundingMs);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new OkxApiException("OKX funding time is invalid.", exception);
        }

        return FundingRate.Create(
            OkxExchangeAdapter.Id,
            instrument,
            rate,
            fundingTime,
            _time.GetUtcNow());
    }
}
