using CryptoTrader.Application.Backtesting;
using Shouldly;

namespace CryptoTrader.Application.Tests;

public class WalkForwardSplitterTests
{
    [Fact]
    public void Split_returns_a_boundary_inside_the_range()
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(10);

        var boundary = WalkForwardSplitter.Split(from, to, 0.7m);

        boundary.ShouldBeGreaterThan(from);
        boundary.ShouldBeLessThan(to);
        boundary.ShouldBe(from.AddDays(7));
        boundary.Offset.ShouldBe(TimeSpan.Zero);
    }
}
