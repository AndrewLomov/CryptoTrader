using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptoTrader.Application.Abstractions.Backtesting;

namespace CryptoTrader.Backtesting;

internal static class BacktestRunIdentity
{
    public static Guid Create(BacktestRequest request)
    {
        var payload = string.Join(
            '\n',
            request.StrategyId,
            request.StrategyVersion,
            request.ParametersJson,
            request.Instrument.Value,
            FormatUtc(request.FromUtc),
            FormatUtc(request.ToUtc),
            FormatDecimal(request.InitialEquity),
            FormatDecimal(request.FeeRate),
            FormatDecimal(request.SlippageBps));
        return HashGuid(payload);
    }

    public static Guid HashGuid(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffff'Z'", CultureInfo.InvariantCulture);

    public static string FormatDecimal(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);
}

internal static class ExitMultiplierReader
{
    public static (decimal StopMultiplier, decimal TakeMultiplier) Read(string json)
    {
        const decimal defaultStop = 2m;
        const decimal defaultTake = 4m;
        if (string.IsNullOrWhiteSpace(json))
        {
            return (defaultStop, defaultTake);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Backtest parameters JSON must be an object.", nameof(json));
            }

            var stop = defaultStop;
            var take = defaultTake;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("AtrStopMultiplier", StringComparison.OrdinalIgnoreCase))
                {
                    stop = ReadDecimal(property.Value, defaultStop);
                }
                else if (property.Name.Equals("AtrTakeProfitMultiplier", StringComparison.OrdinalIgnoreCase))
                {
                    take = ReadDecimal(property.Value, defaultTake);
                }
            }

            return (stop, take);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Backtest parameters JSON is invalid.", nameof(json), exception);
        }
    }

    private static decimal ReadDecimal(JsonElement value, decimal fallback)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return fallback;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var parsed))
        {
            throw new ArgumentException("ATR multipliers must be JSON numbers.");
        }

        return parsed;
    }
}

internal static class BacktestAssumptionsFactory
{
    public static BacktestAssumptions Create() =>
        new(
            BacktestEngineInfo.Version,
            FundingModeled: false,
            PartialFillsModeled: false,
            FillPriceModel: "Next-open entry at open * (1 + slippageBps/10000); exits at trigger * (1 - slippageBps/10000). The exact slipped price is used.",
            StopAndTakeProfitConflict: "Stop wins when both stop and take are touched.",
            Notes: "next-open entry, adverse slippage, stop wins when both stop and take are touched, funding not modeled, partial fills not modeled, end-of-data liquidation");
}

internal static class BacktestMetricsCalculator
{
    public static BacktestMetrics Compute(
        decimal initialEquity,
        IReadOnlyList<EquityPoint> equityCurve,
        IReadOnlyList<BacktestTrade> trades,
        TimeSpan barDuration)
    {
        var finalEquity = equityCurve.Count == 0 ? initialEquity : equityCurve[^1].Equity;
        var netProfit = finalEquity - initialEquity;
        var roi = initialEquity == 0 ? 0m : netProfit / initialEquity;
        var maxDrawdown = MaxDrawdown(initialEquity, equityCurve);
        var returns = SimpleReturns(initialEquity, equityCurve);
        var annualization = AnnualizationFactor(barDuration);
        var (sharpe, sortino) = Ratios(returns, annualization);
        var profitFactor = ProfitFactor(trades);
        var wins = 0;
        decimal netSum = 0;
        decimal totalFees = 0;
        decimal totalSlippage = 0;
        decimal holdingTicks = 0;
        foreach (var trade in trades)
        {
            if (trade.NetPnl > 0)
            {
                wins++;
            }

            netSum += trade.NetPnl;
            totalFees += trade.Fees;
            totalSlippage += trade.SlippageCost;
            holdingTicks += (trade.ExitTimeUtc - trade.EntryTimeUtc).Ticks;
        }

        var tradeCount = trades.Count;
        var winRate = tradeCount == 0 ? 0m : wins / (decimal)tradeCount;
        var expectancy = tradeCount == 0 ? 0m : netSum / tradeCount;
        var averageHolding = tradeCount == 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks((long)(holdingTicks / tradeCount));

        return new BacktestMetrics(
            initialEquity,
            finalEquity,
            netProfit,
            roi,
            maxDrawdown,
            sharpe,
            sortino,
            profitFactor,
            winRate,
            expectancy,
            tradeCount,
            totalFees,
            totalSlippage,
            averageHolding);
    }

    private static List<decimal> SimpleReturns(decimal initialEquity, IReadOnlyList<EquityPoint> equityCurve)
    {
        var returns = new List<decimal>(equityCurve.Count);
        var previous = initialEquity;
        foreach (var point in equityCurve)
        {
            if (previous > 0)
            {
                returns.Add((point.Equity - previous) / previous);
            }

            previous = point.Equity;
        }

        return returns;
    }

    private static decimal MaxDrawdown(decimal initialEquity, IReadOnlyList<EquityPoint> equityCurve)
    {
        var peak = initialEquity;
        decimal maxDrawdown = 0;
        foreach (var point in equityCurve)
        {
            if (point.Equity > peak)
            {
                peak = point.Equity;
            }

            if (peak > 0)
            {
                var drawdown = (peak - point.Equity) / peak;
                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }
        }

        return maxDrawdown;
    }

    private static decimal? ProfitFactor(IReadOnlyList<BacktestTrade> trades)
    {
        decimal grossProfit = 0;
        decimal grossLoss = 0;
        foreach (var trade in trades)
        {
            if (trade.NetPnl > 0)
            {
                grossProfit += trade.NetPnl;
            }
            else if (trade.NetPnl < 0)
            {
                grossLoss -= trade.NetPnl;
            }
        }

        return grossLoss == 0 ? null : grossProfit / grossLoss;
    }

    private static (decimal? Sharpe, decimal? Sortino) Ratios(IReadOnlyList<decimal> returns, decimal annualization)
    {
        if (returns.Count < 2 || annualization <= 0)
        {
            return (null, null);
        }

        decimal sum = 0;
        var hasNegative = false;
        foreach (var value in returns)
        {
            sum += value;
            if (value < 0)
            {
                hasNegative = true;
            }
        }

        var mean = sum / returns.Count;
        decimal varianceSum = 0;
        decimal downsideSum = 0;
        foreach (var value in returns)
        {
            var delta = value - mean;
            varianceSum += delta * delta;
            if (value < 0)
            {
                downsideSum += value * value;
            }
        }

        var sampleSize = returns.Count - 1;
        var variance = varianceSum / sampleSize;
        decimal? sharpe = null;
        if (variance > 0)
        {
            var stdDev = Sqrt(variance);
            if (stdDev > 0)
            {
                sharpe = (mean / stdDev) * annualization;
            }
        }

        decimal? sortino = null;
        if (hasNegative)
        {
            var downside = Sqrt(downsideSum / sampleSize);
            if (downside > 0)
            {
                sortino = (mean / downside) * annualization;
            }
        }

        return (sharpe, sortino);
    }

    private static decimal AnnualizationFactor(TimeSpan barDuration)
    {
        if (barDuration <= TimeSpan.Zero)
        {
            return 0;
        }

        var hours = (decimal)barDuration.Ticks / TimeSpan.TicksPerHour;
        if (hours <= 0)
        {
            return 0;
        }

        return Sqrt((365.25m * 24m) / hours);
    }

    private static decimal Sqrt(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Square root is not defined for a negative value.");
        }

        if (value == 0)
        {
            return 0;
        }

        decimal guess;
        try
        {
            var root = Math.Sqrt((double)value);
            guess = root > 0d && !double.IsInfinity(root) ? (decimal)root : value;
        }
        catch (OverflowException)
        {
            guess = value;
        }

        if (guess <= 0)
        {
            guess = 1;
        }

        for (var iteration = 0; iteration < 50; iteration++)
        {
            var next = (guess + (value / guess)) / 2m;
            if (next == guess)
            {
                return next;
            }

            guess = next;
        }

        return guess;
    }
}
