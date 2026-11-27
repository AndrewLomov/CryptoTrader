namespace CryptoTrader.Contracts.Responses;

public sealed record OrderResponse(
    Guid Id,
    string ClientOrderId,
    Guid AccountId,
    string Exchange,
    string Instrument,
    string InstrumentType,
    string Side,
    string OrderType,
    string TradingMode,
    string Status,
    decimal Quantity,
    decimal FilledQuantity,
    decimal? LimitPrice,
    decimal? AverageFillPrice,
    bool ReduceOnly,
    string? ExchangeOrderId,
    string? RejectReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record OrderListResponse(IReadOnlyList<OrderResponse> Items);

public sealed record PositionResponse(
    Guid AccountId,
    string Exchange,
    string Instrument,
    string InstrumentType,
    string Side,
    decimal Quantity,
    decimal AveragePrice,
    decimal RealizedPnl,
    decimal Fees,
    decimal Funding,
    decimal Leverage,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PositionListResponse(IReadOnlyList<PositionResponse> Items);

public sealed record EmergencyStopResponse(bool EmergencyStop, DateTimeOffset OccurredAtUtc);

public sealed record PaperAccountResponse(
    Guid AccountId,
    string Exchange,
    string QuoteAsset,
    decimal Cash,
    decimal Equity,
    decimal DailyPnl,
    decimal DrawdownFraction,
    bool EmergencyStop,
    int OpenPositions);

public sealed record PaperAccountListResponse(IReadOnlyList<PaperAccountResponse> Items);
