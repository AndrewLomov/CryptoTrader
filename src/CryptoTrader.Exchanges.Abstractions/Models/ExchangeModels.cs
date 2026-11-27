using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Trading;

namespace CryptoTrader.Exchanges.Abstractions.Models;

/// <summary>
/// Public adapter capabilities. SupportsLinearPerpetual is public linear market data.
/// SupportsFuturesAccounting stays false until futures accounting exists.
/// </summary>
public sealed record ExchangeCapabilities(
    bool SupportsSpot,
    bool SupportsLinearPerpetual,
    bool SupportsLiveOrders,
    bool SupportsOrderBook,
    bool SupportsFundingRates,
    bool SupportsWebSocket,
    bool SupportsPrivateAccount,
    bool SupportsFuturesAccounting = false);

public sealed record MarketSubscription(
    InstrumentId Instrument,
    Timeframe? Timeframe,
    bool IncludeCandles,
    bool IncludeTicker,
    bool IncludeTrades,
    bool IncludeOrderBook);

public sealed record ExchangeOrderRequest(
    string ClientOrderId,
    InstrumentId Instrument,
    OrderSide Side,
    OrderType Type,
    Quantity Quantity,
    Price? LimitPrice,
    bool ReduceOnly);

public sealed record OrderSubmissionResult(
    bool Accepted,
    string? ExchangeOrderId,
    string? ErrorCode,
    bool UnknownState,
    string? Message);

public sealed record OrderStatusSnapshot(
    string ClientOrderId,
    string? ExchangeOrderId,
    InstrumentId Instrument,
    OrderStatus Status,
    Quantity OriginalQuantity,
    Quantity FilledQuantity,
    decimal? AveragePrice,
    DateTimeOffset UpdatedAtUtc);

public sealed record ExchangePositionSnapshot(
    InstrumentId Instrument,
    PositionSide Side,
    Quantity Quantity,
    decimal? AveragePrice,
    decimal? UnrealizedPnl,
    decimal Leverage,
    DateTimeOffset UpdatedAtUtc);

public sealed record AccountSnapshot(
    ExchangeId Exchange,
    string QuoteAsset,
    decimal? Equity,
    decimal? AvailableCash,
    DateTimeOffset AsOfUtc,
    bool PrivateDataAvailable);
