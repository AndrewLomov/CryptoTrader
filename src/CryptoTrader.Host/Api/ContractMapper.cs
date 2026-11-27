using CryptoTrader.Application.Abstractions.Backtesting;
using CryptoTrader.Contracts.Responses;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Strategies.Abstractions;

namespace CryptoTrader.Host.Api;

internal static class ContractMapper
{
    public static ExchangeListResponse Exchanges(IEnumerable<string> exchangeCodes, TradingMode mode)
    {
        var tradingMode = mode.ToString();
        var items = exchangeCodes.Select(code => new ExchangeResponse(code, null, tradingMode)).ToArray();
        return new ExchangeListResponse(items);
    }

    public static InstrumentResponse ToResponse(Instrument instrument) =>
        new(
            instrument.Id.Value,
            instrument.Exchange.Value,
            instrument.ExchangeSymbol,
            instrument.BaseAsset,
            instrument.QuoteAsset,
            instrument.Type.ToString(),
            instrument.PriceTick,
            instrument.QuantityStep,
            instrument.MinQuantity,
            instrument.MinNotional,
            instrument.ContractMultiplier,
            instrument.IsTradingEnabled);

    public static CandleResponse ToResponse(Candle candle, string exchange, string instrument, string timeframe) =>
        new(
            exchange,
            instrument,
            timeframe,
            candle.OpenTimeUtc,
            candle.CloseTimeUtc,
            candle.Open,
            candle.High,
            candle.Low,
            candle.Close,
            candle.Volume,
            candle.IsClosed);

    public static StrategyResponse ToResponse(StrategyDescriptor descriptor) =>
        new(descriptor.Id, descriptor.Version, descriptor.DisplayName);

    public static StrategyInstanceResponse ToResponse(StrategyInstance instance, IReadOnlyDictionary<string, string> symbols) =>
        new(
            instance.Id.Value,
            instance.StrategyId,
            instance.StrategyVersion,
            Symbol(symbols, instance.InstrumentId),
            instance.ParametersJson,
            instance.Status.ToString(),
            instance.CreatedAtUtc,
            instance.UpdatedAtUtc);

    public static OrderResponse ToResponse(Order order, IReadOnlyDictionary<string, string> symbols) =>
        new(
            order.Id.Value,
            order.ClientOrderId,
            order.AccountId.Value,
            order.Exchange.Value,
            Symbol(symbols, order.InstrumentId),
            order.InstrumentType.ToString(),
            order.Side.ToString(),
            order.Type.ToString(),
            order.TradingMode.ToString(),
            order.Status.ToString(),
            order.Quantity.Value,
            order.FilledQuantity.Value,
            order.LimitPrice?.Value,
            order.AverageFillPrice,
            order.ReduceOnly,
            order.ExchangeOrderId,
            order.RejectReason,
            order.CreatedAtUtc,
            order.UpdatedAtUtc);

    public static PositionResponse ToResponse(Position position, IReadOnlyDictionary<string, string> symbols) =>
        new(
            position.AccountId.Value,
            position.Exchange.Value,
            Symbol(symbols, position.InstrumentId),
            position.InstrumentType.ToString(),
            position.Side.ToString(),
            position.Quantity.Value,
            position.AveragePrice,
            position.RealizedPnl,
            position.Fees,
            position.Funding,
            position.Leverage,
            position.OpenedAtUtc,
            position.UpdatedAtUtc);

    public static BacktestReportResponse ToResponse(BacktestReport report, IReadOnlyDictionary<string, string> symbols)
    {
        var metrics = report.Metrics;
        return new BacktestReportResponse(
            report.RunId,
            report.StrategyId,
            report.StrategyVersion,
            report.EngineVersion,
            report.ParametersJson,
            report.FromUtc,
            report.ToUtc,
            new BacktestMetricsResponse(
                metrics.InitialEquity,
                metrics.FinalEquity,
                metrics.NetProfit,
                metrics.RoiFraction,
                metrics.MaxDrawdownFraction,
                metrics.Sharpe,
                metrics.Sortino,
                metrics.ProfitFactor,
                metrics.WinRateFraction,
                metrics.Expectancy,
                metrics.TradeCount,
                metrics.TotalFees,
                metrics.TotalSlippage,
                metrics.AverageHoldingTime),
            report.Assumptions.FundingModeled,
            report.Assumptions.FillPriceModel,
            report.Assumptions.StopAndTakeProfitConflict,
            report.Trades.Select(trade => new BacktestTradeResponse(
                trade.Number,
                Symbol(symbols, trade.Instrument),
                trade.EntryTimeUtc,
                trade.ExitTimeUtc,
                trade.EntryPrice,
                trade.ExitPrice,
                trade.Quantity,
                trade.Fees,
                trade.SlippageCost,
                trade.NetPnl,
                trade.ExitReason.ToString())).ToArray(),
            report.EquityCurve.Select(point => new EquityPointResponse(point.TimeUtc, point.Equity)).ToArray());
    }

    public static Dictionary<string, string> Symbols(IEnumerable<Instrument> instruments) =>
        instruments.ToDictionary(instrument => instrument.Id.Value, instrument => instrument.ExchangeSymbol, StringComparer.Ordinal);

    private static string Symbol(IReadOnlyDictionary<string, string> symbols, InstrumentId instrumentId) =>
        symbols.TryGetValue(instrumentId.Value, out var symbol) ? symbol : instrumentId.Value;
}
