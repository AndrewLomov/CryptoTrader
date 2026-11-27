using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Strategies.TrendFollowing.Indicators;
using Shouldly;

namespace CryptoTrader.Strategies.Tests;

public class IndicatorTests
{
    [Fact]
    public void Ema_period_3_seeds_with_the_simple_average_then_smooths()
    {
        var ema = EmaCalculator.Compute(new decimal[] { 1m, 2m, 3m, 4m, 5m }, 3);

        ema.Count.ShouldBe(5);
        ema[0].ShouldBeNull();
        ema[1].ShouldBeNull();
        ema[2].ShouldBe(2m);
        ema[3].ShouldBe(3m);
        ema[4].ShouldBe(4m);
    }

    [Fact]
    public void Rsi_uses_wilder_smoothing_on_a_sixteen_close_fixture()
    {
        // Period 14. Changes over the first 15 closes: +1,+1,-1,+2,+1,-1,+2,+1,-2,+3,+1,-2,+3,+1.
        // Seed averages: gain 16/14, loss 6/14. RS = 8/3, RSI = 100 - 100 / (1 + 8/3) = 800/11.
        // The 16th close loses 2. Wilder then gives 100 * (104/157).
        var closes = new decimal[]
        {
            100m, 101m, 102m, 101m, 103m, 104m, 103m, 105m, 106m, 104m, 107m, 108m, 106m, 109m, 110m, 108m,
        };

        var rsi = RsiCalculator.Compute(closes, 14);

        rsi.Count.ShouldBe(16);
        for (var index = 0; index < 14; index++)
        {
            rsi[index].ShouldBeNull();
        }

        rsi[14].ShouldBe(72.727272727272727272727272727m);
        rsi[15].ShouldBe(66.242038216560509554140127389m);
    }

    [Fact]
    public void Atr_uses_wilder_smoothing_on_the_same_fixture()
    {
        // High = close + 1 and low = close - 1. True ranges of the 15 steps are
        // 2,2,2,3,2,2,3,2,3,4,2,3,4,2 and then 3.
        // Seed ATR is 36/14. The next Wilder step is (seed * 13 + 3) / 14.
        var atr = AtrCalculator.Compute(AtrCandles(), 14);

        atr.Count.ShouldBe(16);
        for (var index = 0; index < 14; index++)
        {
            atr[index].ShouldBeNull();
        }

        atr[14].ShouldBe(2.5714285714285714285714285714m);
        atr[15].ShouldBe(2.6020408163265306122448979591m);
    }

    private static Candle[] AtrCandles()
    {
        var closes = new decimal[]
        {
            100m, 101m, 102m, 101m, 103m, 104m, 103m, 105m, 106m, 104m, 107m, 108m, 106m, 109m, 110m, 108m,
        };
        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return closes.Select((close, index) => Candle.Create(
            ExchangeId.Bybit,
            new InstrumentId("BTCUSDT"),
            Timeframe.OneHour,
            start.AddHours(index),
            start.AddHours(index + 1),
            close,
            close + 1m,
            close - 1m,
            close,
            volume: 1m,
            isClosed: true)).ToArray();
    }
}
