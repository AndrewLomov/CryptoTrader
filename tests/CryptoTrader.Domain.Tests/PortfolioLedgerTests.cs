using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Tests.Support;
using CryptoTrader.Domain.Trading;
using Shouldly;

namespace CryptoTrader.Domain.Tests;

public class PortfolioLedgerTests
{
    [Fact]
    public void Buy_reduces_cash_by_notional_plus_fee_and_duplicate_trade_is_ignored()
    {
        var ledger = PortfolioLedger.Create(
            cash: 10_000m,
            quoteAsset: "USDT",
            TradingAccountId.New(),
            ExchangeId.Bybit,
            DomainTestData.Now);
        var instrument = DomainTestData.SpotBtc();
        const decimal quantity = 2m;
        const decimal price = 100m;
        const decimal fee = 1m;

        var position = ledger.ApplySpotFill(
            instrument,
            OrderSide.Buy,
            new Quantity(quantity),
            new Price(price),
            fee,
            feeAsset: "USDT",
            DomainTestData.Now,
            exchangeTradeId: "trade-1");

        position.ShouldNotBeNull();
        ledger.Cash.ShouldBe(10_000m - ((price * quantity) + fee));
        position.Quantity.Value.ShouldBe(quantity);

        var duplicate = ledger.ApplySpotFill(
            instrument,
            OrderSide.Buy,
            new Quantity(quantity),
            new Price(price),
            fee,
            feeAsset: "USDT",
            DomainTestData.Now.AddMinutes(1),
            exchangeTradeId: "trade-1");

        duplicate.ShouldBeNull();
        ledger.Cash.ShouldBe(9_799m);
        ledger.FindPosition(instrument.Id)!.Quantity.Value.ShouldBe(quantity);
        ledger.AppliedExchangeTradeIds.ShouldHaveSingleItem().ShouldBe("trade-1");
    }
}
