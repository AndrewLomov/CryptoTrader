using System.Globalization;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Tests.Support;
using Shouldly;

namespace CryptoTrader.Domain.Tests;

public class PositionSizerTests
{
    [Fact]
    public void TrySize_floors_quantity_to_the_step()
    {
        // equity 1290 * 1% = 12.9 risk; unit risk 10; raw 1.29 floored to 0.1 is 1.2.
        var result = PositionSizer.TrySize(
            new Price(100m),
            new Price(90m),
            equity: 1_290m,
            availableCash: 10_000m,
            DomainTestData.Profile(),
            quantityStep: 0.1m,
            minQuantity: 0.01m,
            minNotional: null);

        result.IsApproved.ShouldBeTrue();
        result.RejectionCode.ShouldBeNull();
        result.Quantity.ShouldNotBeNull();
        result.Quantity.Value.Value.ShouldBe(1.2m);
        result.EstimatedMaxLoss.ShouldBe(12m);
    }

    [Theory]
    [InlineData("100.5")]
    [InlineData("101")]
    public void TrySize_rejects_stop_at_or_above_the_slipped_entry(string stop)
    {
        // 50 bps on 100 slips the entry to 100.5.
        var result = PositionSizer.TrySize(
            new Price(100m),
            new Price(decimal.Parse(stop, CultureInfo.InvariantCulture)),
            equity: 10_000m,
            availableCash: 10_000m,
            DomainTestData.Profile(slippageBps: 50m),
            quantityStep: 0.001m,
            minQuantity: 0.001m,
            minNotional: null);

        result.IsApproved.ShouldBeFalse();
        result.Quantity.ShouldBeNull();
        result.RejectionCode.ShouldBe(RiskRejectionCodes.InvalidStop);
    }

    [Fact]
    public void TrySize_rejects_quantity_below_the_minimum()
    {
        // raw 0.1 floored to the 0.1 step stays 0.1, under the minimum of 1.
        var result = PositionSizer.TrySize(
            new Price(100m),
            new Price(90m),
            equity: 100m,
            availableCash: 10_000m,
            DomainTestData.Profile(),
            quantityStep: 0.1m,
            minQuantity: 1m,
            minNotional: null);

        result.IsApproved.ShouldBeFalse();
        result.RejectionCode.ShouldBe(RiskRejectionCodes.MinQuantity);
    }

    [Fact]
    public void TrySize_rejects_insufficient_cash()
    {
        var result = PositionSizer.TrySize(
            new Price(100m),
            new Price(90m),
            equity: 1_290m,
            availableCash: 119m,
            DomainTestData.Profile(),
            quantityStep: 0.1m,
            minQuantity: 0.01m,
            minNotional: null);

        result.IsApproved.ShouldBeFalse();
        result.Quantity.ShouldBeNull();
        result.RejectionCode.ShouldBe(RiskRejectionCodes.InsufficientBalance);
    }
}
