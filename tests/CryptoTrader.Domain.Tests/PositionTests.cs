using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Tests.Support;
using CryptoTrader.Domain.Trading;
using Shouldly;

namespace CryptoTrader.Domain.Tests;

public class PositionTests
{
    [Fact]
    public void Spot_position_cannot_sell_more_than_it_holds()
    {
        var account = TradingAccountId.New();
        var instrument = DomainTestData.SpotBtc();
        var position = Position.Create(
            account,
            ExchangeId.Bybit,
            instrument.Id,
            InstrumentType.Spot,
            contractMultiplier: null,
            DomainTestData.Now);
        position.ApplyFill(
            OrderSide.Buy,
            new Quantity(1m),
            new Price(50m),
            fee: 0.1m,
            feeAsset: "USDT",
            DomainTestData.Now);

        Should.Throw<DomainException>(() => position.ApplyFill(
            OrderSide.Sell,
            new Quantity(1.1m),
            new Price(60m),
            fee: 0m,
            feeAsset: "USDT",
            DomainTestData.Now.AddMinutes(1)));

        position.Side.ShouldBe(PositionSide.Long);
        position.Quantity.Value.ShouldBe(1m);
        position.AveragePrice.ShouldBe(50m);
    }
}
