using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Strategies.Abstractions;
using CryptoTrader.Strategies.TrendFollowing.Indicators;

namespace CryptoTrader.Strategies.TrendFollowing;

public sealed class TrendFollowingStrategy : ITradingStrategy
{
    public const string StrategyId = "trend-following";

    public const string StrategyVersion = "0.1.0";

    public const string StrategyDisplayName = "Trend Following Swing";

    private static readonly IReadOnlyList<TradeSignal> NoSignals = [];

    public TrendFollowingStrategy(string parametersJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parametersJson);
        ParametersJson = parametersJson;
        Parameters = TrendFollowingParameters.Parse(parametersJson);
    }

    public string Id => StrategyId;

    public string Version => StrategyVersion;

    public string DisplayName => StrategyDisplayName;

    public string ParametersJson { get; }

    public TrendFollowingParameters Parameters { get; }

    public ValueTask<IReadOnlyList<TradeSignal>> EvaluateAsync(
        StrategyContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Instrument);
        ArgumentNullException.ThrowIfNull(context.ClosedWorkingCandles);
        ArgumentNullException.ThrowIfNull(context.ClosedHigherTimeframeCandles);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Evaluate(context));
    }

    private IReadOnlyList<TradeSignal> Evaluate(StrategyContext context)
    {
        var working = SelectVisible(context.ClosedWorkingCandles, context.AsOfUtc);
        var higher = SelectVisible(context.ClosedHigherTimeframeCandles, context.AsOfUtc);
        if (working.Count == 0 || higher.Count == 0)
        {
            return NoSignals;
        }

        var higherCloses = new decimal[higher.Count];
        for (var i = 0; i < higher.Count; i++)
        {
            higherCloses[i] = higher[i].Close;
        }

        var emaFast = EmaCalculator.Compute(higherCloses, Parameters.EmaFastPeriod);
        var emaSlow = EmaCalculator.Compute(higherCloses, Parameters.EmaSlowPeriod);
        var lastHigher = higher.Count - 1;
        if (emaFast[lastHigher] is not decimal fast || emaSlow[lastHigher] is not decimal slow)
        {
            return NoSignals;
        }

        var higherClose = higher[lastHigher].Close;
        var workingCandle = working[^1];
        var trendUp = fast > slow && higherClose > slow;
        if (!trendUp)
        {
            if (!context.HasOpenPosition)
            {
                return NoSignals;
            }

            var exitExplanation =
                $"Exit long: higher-timeframe trend is broken. EMA fast {Format(fast)}, EMA slow {Format(slow)}, higher close {Format(higherClose)}, working close {Format(workingCandle.Close)}.";
            return
            [
                TradeSignal.Create(
                    CreateSignalId(context.Instrument.Id, workingCandle.OpenTimeUtc),
                    Id,
                    Version,
                    context.Instrument.Id,
                    SignalAction.ExitLong,
                    workingCandle.CloseTimeUtc,
                    new Price(workingCandle.Close),
                    null,
                    null,
                    null,
                    exitExplanation),
            ];
        }

        if (context.HasOpenPosition)
        {
            return NoSignals;
        }

        var workingCloses = new decimal[working.Count];
        for (var i = 0; i < working.Count; i++)
        {
            workingCloses[i] = working[i].Close;
        }

        var rsi = RsiCalculator.Compute(workingCloses, Parameters.RsiPeriod);
        var atr = AtrCalculator.Compute(working, Parameters.AtrPeriod);
        var last = working.Count - 1;
        if (last < 1
            || rsi[last] is not decimal currentRsi
            || rsi[last - 1] is not decimal previousRsi
            || atr[last] is not decimal currentAtr)
        {
            return NoSignals;
        }

        if (previousRsi >= Parameters.RsiCrossThreshold || currentRsi < Parameters.RsiCrossThreshold)
        {
            return NoSignals;
        }

        var reference = workingCandle.Close;
        var stop = reference - (currentAtr * Parameters.AtrStopMultiplier);
        if (stop <= 0 || stop >= reference)
        {
            return NoSignals;
        }

        Price? take = null;
        string takeText = "none";
        if (Parameters.AtrTakeProfitMultiplier > 0)
        {
            var takePrice = reference + (currentAtr * Parameters.AtrTakeProfitMultiplier);
            if (takePrice <= reference)
            {
                return NoSignals;
            }

            take = new Price(takePrice);
            takeText = Format(takePrice);
        }

        var explanation =
            $"Long signal: EMA fast {Format(fast)}, EMA slow {Format(slow)}, higher close {Format(higherClose)}, RSI {Format(previousRsi)} -> {Format(currentRsi)} crossed {Format(Parameters.RsiCrossThreshold)}, ATR {Format(currentAtr)}, close {Format(reference)}, stop {Format(stop)}, take {takeText}.";
        return
        [
            TradeSignal.Create(
                CreateSignalId(context.Instrument.Id, workingCandle.OpenTimeUtc),
                Id,
                Version,
                context.Instrument.Id,
                SignalAction.EnterLong,
                workingCandle.CloseTimeUtc,
                new Price(reference),
                new Price(stop),
                take,
                currentAtr,
                explanation),
        ];
    }

    private static List<Candle> SelectVisible(IReadOnlyList<Candle> candles, DateTimeOffset asOfUtc)
    {
        var filtered = new List<Candle>(candles.Count);
        foreach (var candle in candles)
        {
            if (!candle.IsClosed || candle.CloseTimeUtc > asOfUtc)
            {
                continue;
            }

            filtered.Add(candle);
        }

        var ordered = filtered.OrderBy(candle => candle.OpenTimeUtc).ToList();
        var result = new List<Candle>(ordered.Count);
        DateTimeOffset? seenOpen = null;
        foreach (var candle in ordered)
        {
            if (seenOpen == candle.OpenTimeUtc)
            {
                continue;
            }

            seenOpen = candle.OpenTimeUtc;
            result.Add(candle);
        }

        return result;
    }

    private static Guid CreateSignalId(InstrumentId instrumentId, DateTimeOffset openTimeUtc)
    {
        var payload = string.Join(
            '\n',
            StrategyId,
            instrumentId.Value,
            FormatUtc(openTimeUtc),
            StrategyVersion);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffff'Z'", CultureInfo.InvariantCulture);

    private static string Format(decimal value) =>
        value.ToString("G29", CultureInfo.InvariantCulture);
}
