using CryptoTrader.Application.Abstractions.Backtesting;
using CryptoTrader.Application.Abstractions.Paper;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Domain.Trading;

namespace CryptoTrader.Application.Abstractions.Persistence;

public interface ICandleRepository
{
    Task UpsertRangeAsync(IReadOnlyList<Candle> candles, CancellationToken cancellationToken);

    Task<IReadOnlyList<Candle>> GetRangeAsync(
        ExchangeId exchange,
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

public interface IInstrumentRepository
{
    Task UpsertAsync(Instrument instrument, CancellationToken cancellationToken);

    Task<Instrument?> FindAsync(InstrumentId id, CancellationToken cancellationToken);

    Task<Instrument?> FindByExchangeSymbolAsync(
        ExchangeId exchange,
        string exchangeSymbol,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Instrument>> ListAsync(ExchangeId? exchange, CancellationToken cancellationToken);
}

public interface IOrderRepository
{
    Task<Order?> FindByIdAsync(OrderId id, CancellationToken cancellationToken);

    Task<Order?> FindByClientOrderIdAsync(
        TradingAccountId accountId,
        string clientOrderId,
        CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task UpdateAsync(Order order, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> ListAsync(
        TradingAccountId? accountId,
        TradingMode? tradingMode,
        CancellationToken cancellationToken);
}

public interface IPositionRepository
{
    Task<Position?> FindOpenAsync(
        TradingAccountId accountId,
        InstrumentId instrumentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Position>> ListAsync(TradingAccountId accountId, CancellationToken cancellationToken);

    Task UpsertAsync(Position position, CancellationToken cancellationToken);
}

public interface IStrategyInstanceRepository
{
    Task AddAsync(StrategyInstance instance, CancellationToken cancellationToken);

    Task<StrategyInstance?> FindAsync(StrategyInstanceId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<StrategyInstance>> ListAsync(CancellationToken cancellationToken);

    Task UpdateAsync(StrategyInstance instance, CancellationToken cancellationToken);
}

public interface ITradeSignalRepository
{
    Task AddAsync(
        TradeSignal signal,
        StrategyInstanceId strategyInstanceId,
        RiskDecision decision,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid signalId, CancellationToken cancellationToken);
}

public interface IBacktestRunRepository
{
    Task AddAsync(BacktestRunRecord run, CancellationToken cancellationToken);

    Task<BacktestRunRecord?> FindAsync(Guid runId, CancellationToken cancellationToken);
}

public interface IPaperStateRepository
{
    Task SaveAsync(PaperState state, CancellationToken cancellationToken);

    Task<PaperState?> LoadAsync(TradingAccountId accountId, CancellationToken cancellationToken);
}

public interface IRiskEventRepository
{
    Task AddAsync(DomainEvent riskEvent, CancellationToken cancellationToken);
}

public interface ITradingAccountRepository
{
    Task<TradingAccount?> FindAsync(TradingAccountId id, CancellationToken cancellationToken);

    Task UpsertAsync(TradingAccount account, CancellationToken cancellationToken);

    Task<IReadOnlyList<TradingAccount>> ListAsync(CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
