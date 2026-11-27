using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Application.Common;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions.MarketData;

namespace CryptoTrader.Application.MarketData;

public sealed class CandleAdminService(
    ICandleRepository candles,
    IInstrumentRepository instruments,
    IMarketDataClient marketData,
    IEnumerable<IInstrumentMetadataClient> metadataClients,
    IUnitOfWork unitOfWork)
{
    private readonly ICandleRepository _candles = candles ?? throw new ArgumentNullException(nameof(candles));
    private readonly IInstrumentRepository _instruments = instruments ?? throw new ArgumentNullException(nameof(instruments));
    private readonly IMarketDataClient _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
    private readonly IReadOnlyList<IInstrumentMetadataClient> _metadataClients = (metadataClients ?? throw new ArgumentNullException(nameof(metadataClients))).ToArray();
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async Task<IReadOnlyList<Instrument>> ListInstrumentsAsync(
        string? exchange,
        string? symbol,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExchangeId? exchangeId = string.IsNullOrWhiteSpace(exchange) ? null : ExchangeCode.Parse(exchange);
        var items = await _instruments.ListAsync(exchangeId, cancellationToken);
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return items;
        }

        var key = symbol.Trim();
        return items
            .Where(instrument => string.Equals(instrument.ExchangeSymbol, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(instrument.Id.Value, key, StringComparison.Ordinal))
            .ToArray();
    }

    public async Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string exchange,
        string instrument,
        string timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolved = await ResolveRangeAsync(exchange, instrument, timeframe, fromUtc, toUtc, cancellationToken);
        return await _candles.GetRangeAsync(
            resolved.Instrument.Exchange,
            resolved.Instrument.Id,
            resolved.Timeframe,
            resolved.From,
            resolved.To,
            cancellationToken);
    }

    public async Task<int> ImportAsync(
        string exchange,
        string symbol,
        string timeframe,
        IReadOnlyList<ImportedCandle> candlesToImport,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (candlesToImport is null)
        {
            throw new InvalidOperationException("Candles are required.");
        }

        var exchangeId = ExchangeCode.Parse(exchange);
        var parsedTimeframe = Timeframe.Parse(timeframe);
        var instrument = await InstrumentLookup.RequireAsync(_instruments, symbol, exchangeId, cancellationToken);
        var imported = candlesToImport
            .Select(item => Candle.Create(
                instrument.Exchange,
                instrument.Id,
                parsedTimeframe,
                UtcTime.Normalize(item.OpenTimeUtc),
                UtcTime.Normalize(item.CloseTimeUtc),
                item.Open,
                item.High,
                item.Low,
                item.Close,
                item.Volume,
                isClosed: true))
            .ToArray();
        await _candles.UpsertRangeAsync(imported, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return imported.Length;
    }

    public async Task<int> SyncAsync(
        string exchange,
        string symbol,
        string timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exchangeId = ExchangeCode.Parse(exchange);
        var parsedTimeframe = Timeframe.Parse(timeframe);
        var from = UtcTime.Normalize(fromUtc);
        var to = UtcTime.Normalize(toUtc);
        if (to <= from)
        {
            throw new InvalidOperationException("Candle sync end must be after the start.");
        }

        var instrument = await FindLocalAsync(symbol, exchangeId, cancellationToken)
            ?? await DownloadInstrumentAsync(symbol, exchangeId, cancellationToken)
            ?? throw new NotFoundException($"Instrument '{symbol.Trim()}' on {exchangeId.Value} was not found.");
        var downloaded = await _marketData.GetCandlesAsync(instrument.Id, parsedTimeframe, from, to, cancellationToken);
        await _candles.UpsertRangeAsync(downloaded, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return downloaded.Count;
    }

    private async Task<(Instrument Instrument, Timeframe Timeframe, DateTimeOffset From, DateTimeOffset To)> ResolveRangeAsync(
        string exchange,
        string symbol,
        string timeframeCode,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var exchangeId = ExchangeCode.Parse(exchange);
        var timeframe = Timeframe.Parse(timeframeCode);
        var from = UtcTime.Normalize(fromUtc);
        var to = UtcTime.Normalize(toUtc);
        if (to <= from)
        {
            throw new InvalidOperationException("Candle range end must be after the start.");
        }

        var instrument = await InstrumentLookup.RequireAsync(_instruments, symbol, exchangeId, cancellationToken);
        return (instrument, timeframe, from, to);
    }

    private async Task<Instrument?> FindLocalAsync(string symbol, ExchangeId exchange, CancellationToken cancellationToken)
    {
        try
        {
            return await InstrumentLookup.RequireAsync(_instruments, symbol, exchange, cancellationToken);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private async Task<Instrument?> DownloadInstrumentAsync(
        string symbol,
        ExchangeId exchange,
        CancellationToken cancellationToken)
    {
        var key = symbol.Trim();
        foreach (var client in _metadataClients)
        {
            var remote = await client.FindByExchangeSymbolAsync(key, cancellationToken);
            if (remote is null && !string.Equals(key, key.ToUpperInvariant(), StringComparison.Ordinal))
            {
                remote = await client.FindByExchangeSymbolAsync(key.ToUpperInvariant(), cancellationToken);
            }

            if (remote is null || remote.Exchange != exchange)
            {
                continue;
            }

            await _instruments.UpsertAsync(remote, cancellationToken);
            return remote;
        }

        return null;
    }
}

public readonly record struct ImportedCandle(
    DateTimeOffset OpenTimeUtc,
    DateTimeOffset CloseTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);
