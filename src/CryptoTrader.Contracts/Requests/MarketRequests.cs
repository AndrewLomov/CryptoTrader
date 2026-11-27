namespace CryptoTrader.Contracts.Requests;

public sealed record GetInstrumentsQuery(string? Exchange, string? Symbol);

public sealed record GetCandlesQuery(
    string Exchange,
    string Instrument,
    string Timeframe,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc);

public sealed record ImportCandleItem(
    DateTimeOffset OpenTimeUtc,
    DateTimeOffset CloseTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);

public sealed record ImportCandlesRequest(
    string Exchange,
    string Symbol,
    string Timeframe,
    IReadOnlyList<ImportCandleItem> Candles);

public sealed record SyncCandlesRequest(
    string Exchange,
    string Symbol,
    string Timeframe,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc);
