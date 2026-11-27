namespace CryptoTrader.Contracts.Responses;

public sealed record ExchangeResponse(string Code, string? ConnectionStatus, string? TradingMode);

public sealed record ExchangeListResponse(IReadOnlyList<ExchangeResponse> Items);

public sealed record InstrumentResponse(
    string Id,
    string Exchange,
    string Symbol,
    string BaseAsset,
    string QuoteAsset,
    string Type,
    decimal PriceTick,
    decimal QuantityStep,
    decimal MinQuantity,
    decimal? MinNotional,
    decimal? ContractMultiplier,
    bool IsTradingEnabled);

public sealed record InstrumentListResponse(IReadOnlyList<InstrumentResponse> Items);

public sealed record CandleResponse(
    string Exchange,
    string Instrument,
    string Timeframe,
    DateTimeOffset OpenTimeUtc,
    DateTimeOffset CloseTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    bool IsClosed);

public sealed record CandleListResponse(IReadOnlyList<CandleResponse> Items);

public sealed record ImportCandlesResponse(int UpsertedCount);

public sealed record SyncCandlesResponse(int UpsertedCount);
