using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Tests.Support;
using CryptoTrader.Domain.Trading;
using Shouldly;

namespace CryptoTrader.Domain.Tests;

public class OrderLifecycleTests
{
    [Fact]
    public void Filled_order_cannot_return_to_submitted()
    {
        var order = DomainTestData.MarketBuy(new Quantity(1m));
        var submittedAt = DomainTestData.Now.AddMinutes(1);
        order.MarkSubmitted(submittedAt, "ex-1");
        var execution = order.ApplyFill(
            ExecutionId.New(),
            new Quantity(1m),
            new Price(100m),
            fee: 0.1m,
            feeAsset: "USDT",
            submittedAt.AddMinutes(1),
            "trade-1");

        execution.ShouldNotBeNull();
        order.Status.ShouldBe(OrderStatus.Filled);

        Should.Throw<DomainException>(() => order.MarkSubmitted(submittedAt.AddMinutes(2), "ex-1"));

        order.Status.ShouldBe(OrderStatus.Filled);
        order.FilledQuantity.Value.ShouldBe(1m);
    }

    [Fact]
    public void Partial_fill_then_remainder_reaches_filled_and_duplicate_trade_does_not_increase_quantity()
    {
        var order = DomainTestData.MarketBuy(new Quantity(1m));
        var filledAt = DomainTestData.Now.AddMinutes(1);
        order.MarkSubmitted(filledAt);

        var first = order.ApplyFill(
            ExecutionId.New(),
            new Quantity(0.4m),
            new Price(100m),
            fee: 0m,
            feeAsset: "USDT",
            filledAt,
            "trade-1");

        first.ShouldNotBeNull();
        order.Status.ShouldBe(OrderStatus.PartiallyFilled);
        order.FilledQuantity.Value.ShouldBe(0.4m);

        var duplicate = order.ApplyFill(
            ExecutionId.New(),
            new Quantity(0.4m),
            new Price(100m),
            fee: 0m,
            feeAsset: "USDT",
            filledAt.AddSeconds(1),
            "trade-1");

        duplicate.ShouldBeNull();
        order.Status.ShouldBe(OrderStatus.PartiallyFilled);
        order.FilledQuantity.Value.ShouldBe(0.4m);

        var second = order.ApplyFill(
            ExecutionId.New(),
            new Quantity(0.6m),
            new Price(110m),
            fee: 0m,
            feeAsset: "USDT",
            filledAt.AddSeconds(2),
            "trade-2");

        second.ShouldNotBeNull();
        order.Status.ShouldBe(OrderStatus.Filled);
        order.FilledQuantity.Value.ShouldBe(1m);
        order.RemainingQuantity.Value.ShouldBe(0m);
    }

    [Fact]
    public void ApplyFill_larger_than_the_remainder_throws()
    {
        var order = DomainTestData.MarketBuy(new Quantity(1m));
        var filledAt = DomainTestData.Now.AddMinutes(1);
        order.MarkSubmitted(filledAt);
        order.ApplyFill(
            ExecutionId.New(),
            new Quantity(0.4m),
            new Price(100m),
            fee: 0m,
            feeAsset: "USDT",
            filledAt,
            "trade-1");

        Should.Throw<DomainException>(() => order.ApplyFill(
            ExecutionId.New(),
            new Quantity(0.7m),
            new Price(100m),
            fee: 0m,
            feeAsset: "USDT",
            filledAt.AddSeconds(1),
            "trade-2"));

        order.Status.ShouldBe(OrderStatus.PartiallyFilled);
        order.FilledQuantity.Value.ShouldBe(0.4m);
    }
}
