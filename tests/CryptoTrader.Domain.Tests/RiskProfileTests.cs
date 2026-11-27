using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Risk;
using Shouldly;

namespace CryptoTrader.Domain.Tests;

public class RiskProfileTests
{
    [Fact]
    public void Create_throws_when_futures_live_is_allowed()
    {
        var exception = Should.Throw<DomainException>(() => RiskProfile.Create(
            0.5m,
            1m,
            2,
            100m,
            0.001m,
            5m,
            averagingDownEnabled: false,
            10m,
            allowFuturesLive: true,
            allowCrossMargin: false));

        exception.Message.ShouldContain("Futures");
    }

    [Fact]
    public void Create_throws_when_cross_margin_is_allowed()
    {
        var exception = Should.Throw<DomainException>(() => RiskProfile.Create(
            0.5m,
            1m,
            2,
            100m,
            0.001m,
            5m,
            averagingDownEnabled: false,
            10m,
            allowFuturesLive: false,
            allowCrossMargin: true));

        exception.Message.ShouldContain("Cross margin");
    }
}
