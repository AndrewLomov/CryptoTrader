using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Trading;

namespace CryptoTrader.Application.Abstractions.Paper;

public sealed record PaperPositionState(
    InstrumentId InstrumentId,
    InstrumentType InstrumentType,
    PositionSide Side,
    decimal Quantity,
    decimal AveragePrice,
    decimal RealizedPnl,
    decimal Fees,
    decimal Funding,
    string? FeeAsset,
    decimal? ContractMultiplier,
    decimal? Margin,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PaperState(
    TradingAccountId AccountId,
    ExchangeId Exchange,
    string QuoteAsset,
    decimal Cash,
    decimal PeakEquity,
    decimal DayStartEquity,
    decimal LastEquity,
    DateOnly DayUtc,
    bool EmergencyStop,
    IReadOnlyList<PaperPositionState> Positions,
    IReadOnlyList<string> AppliedExchangeTradeIds,
    IReadOnlyDictionary<InstrumentId, decimal> MarksByInstrument,
    DateTimeOffset UpdatedAtUtc);
