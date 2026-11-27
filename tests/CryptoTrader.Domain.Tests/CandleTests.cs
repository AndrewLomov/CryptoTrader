using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Tests.Support;
using Shouldly;

namespace CryptoTrader.Domain.Tests;

public class CandleTests
{
    [Fact]
    public void Create_rejects_high_below_low()
    {
        var open = DomainTestData.Now;
        var exception = Should.Throw<DomainException>(() => Candle.Create(
            ExchangeId.Bybit,
            new InstrumentId("BTCUSDT"),
            Timeframe.OneHour,
            open,
            open.AddHours(1),
            open: 10m,
            high: 11m,
            low: 12m,
            close: 10m,
            volume: 1m,
            isClosed: true));

        exception.Message.ShouldContain("high");
    }

    [Fact]
    public void Create_rejects_negative_volume()
    {
        var open = DomainTestData.Now;
        var exception = Should.Throw<DomainException>(() => Candle.Create(
            ExchangeId.Bybit,
            new InstrumentId("BTCUSDT"),
            Timeframe.OneHour,
            open,
            open.AddHours(1),
            open: 10m,
            high: 12m,
            low: 9m,
            close: 11m,
            volume: -1m,
            isClosed: true));

        exception.Message.ShouldContain("volume");
    }

    [Fact]
    public void Gap_detector_finds_a_missing_open()
    {
        var first = DomainTestData.Now;
        var candles = new[]
        {
            DomainTestData.Hour(first),
            DomainTestData.Hour(first.AddHours(2)),
        };

        var missing = CandleGapDetector.FindMissingOpenTimes(candles);

        missing.ShouldBe([first.AddHours(1)]);
    }

    [Fact]
    public void Gap_detector_returns_nothing_for_a_continuous_pair()
    {
        var first = DomainTestData.Now;
        var candles = new[]
        {
            DomainTestData.Hour(first),
            DomainTestData.Hour(first.AddHours(1)),
        };

        CandleGapDetector.FindMissingOpenTimes(candles).ShouldBeEmpty();
    }
}
