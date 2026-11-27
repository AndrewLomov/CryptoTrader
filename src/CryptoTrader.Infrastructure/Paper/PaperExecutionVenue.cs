using CryptoTrader.Application.Abstractions.Execution;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Domain.Trading;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Infrastructure.Paper;

public sealed class PaperExecutionVenue : IExecutionVenue
{
    private readonly IOptions<RiskOptions> _risk;
    private readonly TimeProvider _time;

    public PaperExecutionVenue(IOptions<RiskOptions> risk, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(risk);
        ArgumentNullException.ThrowIfNull(time);
        _risk = risk;
        _time = time;
    }

    public TradingMode Mode => TradingMode.Paper;

    public Task<VenueExecutionResult> ExecuteAsync(
        ApprovedOrder order,
        Instrument instrument,
        MarketSnapshot market,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(market);

        if (instrument.Type == InstrumentType.LinearPerpetual)
        {
            return Task.FromResult(Reject(RiskRejectionCodes.FuturesLiveForbidden));
        }

        if (market.Stale)
        {
            return Task.FromResult(Reject(RiskRejectionCodes.StaleMarketData));
        }

        if (!market.Healthy)
        {
            return Task.FromResult(Reject(RiskRejectionCodes.ExchangeUnhealthy));
        }

        var side = order.Signal.Action switch
        {
            SignalAction.EnterLong => OrderSide.Buy,
            SignalAction.ExitLong => OrderSide.Sell,
            _ => throw new InvalidOperationException("Paper execution supports enter long and exit long only."),
        };
        var risk = _risk.Value;
        var raw = RawPrice(market, side, order.Signal);
        var slipped = side == OrderSide.Buy
            ? raw * (1m + (risk.SlippageBps / 10000m))
            : raw * (1m - (risk.SlippageBps / 10000m));
        if (slipped <= 0m)
        {
            throw new InvalidOperationException("Paper fill price must be greater than zero.");
        }

        var fee = slipped * order.Quantity.Value * risk.FeeRate;
        if (fee < 0m)
        {
            throw new InvalidOperationException("Paper fee cannot be negative.");
        }

        var now = _time.GetUtcNow();
        var paperOrder = Order.Create(
            OrderId.New(),
            order.ClientOrderId,
            order.AccountId,
            instrument.Exchange,
            instrument,
            side,
            order.OrderType,
            order.Quantity,
            order.LimitPrice,
            reduceOnly: false,
            TradingMode.Paper,
            now);
        paperOrder.MarkSubmitted(now);
        paperOrder.MarkAccepted(now, "paper-" + order.ClientOrderId);
        var execution = paperOrder.ApplyFill(
            ExecutionId.New(),
            order.Quantity,
            new Price(slipped),
            fee,
            instrument.QuoteAsset,
            now,
            "paper-fill-" + order.ClientOrderId);
        if (execution is null)
        {
            throw new InvalidOperationException("Paper fill was not applied.");
        }

        return Task.FromResult(new VenueExecutionResult(paperOrder, execution, null));
    }

    public Task ReconcileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static decimal RawPrice(MarketSnapshot market, OrderSide side, TradeSignal signal)
    {
        if (market.Last is decimal last)
        {
            return last;
        }

        if (side == OrderSide.Buy && market.Ask is decimal ask)
        {
            return ask;
        }

        if (side == OrderSide.Sell && market.Bid is decimal bid)
        {
            return bid;
        }

        return signal.ReferencePrice.Value;
    }

    private static VenueExecutionResult Reject(string code) => new(null, null, code);
}
