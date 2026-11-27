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

    [Fact]
    public void Create_defaults_volume_to_the_base_asset()
    {
        var candle = DomainTestData.Hour(DomainTestData.Now, price: 10m, volume: 2m);

        candle.Volume.ShouldBe(2m);
        candle.VolumeUnit.ShouldBe(VolumeUnit.BaseAsset);
        candle.QuoteVolume.ShouldBeNull();
        candle.BaseVolume.ShouldBeNull();
        ((int)VolumeUnit.BaseAsset).ShouldBe(1);
        ((int)VolumeUnit.QuoteAsset).ShouldBe(2);
        ((int)VolumeUnit.Contract).ShouldBe(3);
    }

    [Fact]
    public void Create_keeps_contract_count_out_of_base_volume()
    {
        var open = DomainTestData.Now;
        var candle = Candle.Create(
            ExchangeId.Okx,
            new InstrumentId("OKX-LINEAR-SWAP-BTC-USDT"),
            Timeframe.OneHour,
            open,
            open.AddHours(1),
            open: 10m,
            high: 12m,
            low: 9m,
            close: 11m,
            volume: 5m,
            isClosed: true,
            volumeUnit: VolumeUnit.Contract,
            quoteVolume: 0m,
            baseVolume: null);

        candle.Volume.ShouldBe(5m);
        candle.VolumeUnit.ShouldBe(VolumeUnit.Contract);
        candle.QuoteVolume.ShouldBe(0m);
        candle.BaseVolume.ShouldBeNull();
    }

    [Fact]
    public void Create_stores_spot_quote_turnover_without_replacing_base_volume()
    {
        var open = DomainTestData.Now;
        var candle = Candle.Create(
            ExchangeId.Bybit,
            new InstrumentId("BTCUSDT"),
            Timeframe.OneHour,
            open,
            open.AddHours(1),
            open: 10m,
            high: 12m,
            low: 9m,
            close: 11m,
            volume: 1.5m,
            isClosed: true,
            quoteVolume: 15m);

        candle.VolumeUnit.ShouldBe(VolumeUnit.BaseAsset);
        candle.Volume.ShouldBe(1.5m);
        candle.QuoteVolume.ShouldBe(15m);
        candle.BaseVolume.ShouldBeNull();
    }

    [Fact]
    public void Create_rejects_negative_extra_volumes_and_unknown_unit()
    {
        var open = DomainTestData.Now;
        Should.Throw<DomainException>(() => Hour(open, quoteVolume: -0.01m)).Message.ShouldContain("quoteVolume");
        Should.Throw<DomainException>(() => Hour(open, baseVolume: -1m)).Message.ShouldContain("baseVolume");
        Should.Throw<DomainException>(() => Hour(open, volumeUnit: (VolumeUnit)0)).Message.ShouldContain("volumeUnit");
    }

    private static Candle Hour(
        DateTimeOffset open,
        VolumeUnit volumeUnit = VolumeUnit.BaseAsset,
        decimal? quoteVolume = null,
        decimal? baseVolume = null) =>
        Candle.Create(
            ExchangeId.Bybit,
            new InstrumentId("BTCUSDT"),
            Timeframe.OneHour,
            open,
            open.AddHours(1),
            10m,
            12m,
            9m,
            11m,
            1m,
            true,
            volumeUnit,
            quoteVolume,
            baseVolume);
}
