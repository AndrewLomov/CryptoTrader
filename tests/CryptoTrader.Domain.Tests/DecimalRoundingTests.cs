using CryptoTrader.Domain.Common;
using Shouldly;

namespace CryptoTrader.Domain.Tests;

public class DecimalRoundingTests
{
    [Fact]
    public void FloorToStep_rounds_1_29_down_to_the_tenth()
    {
        DecimalRounding.FloorToStep(1.29m, 0.1m).ShouldBe(1.2m);
    }
}
