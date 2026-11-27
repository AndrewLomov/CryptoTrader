using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Trading;

namespace CryptoTrader.Application.Configuration;

public sealed class ExecutionOptions
{
    public const string SectionName = "Execution";

    public TradingMode Mode { get; set; } = TradingMode.Paper;

    public bool LiveTradingEnabled { get; set; }

    public decimal PaperInitialEquity { get; set; } = 10_000m;
}

public sealed class RiskOptions
{
    public const string SectionName = "Risk";

    public decimal RiskPerTradePercent { get; set; } = 0.5m;

    public decimal MaxDailyLossPercent { get; set; } = 1m;

    public int MaxOpenPositions { get; set; } = 2;

    public decimal MaxExposurePercent { get; set; } = 100m;

    public decimal FeeRate { get; set; } = 0.001m;

    public decimal SlippageBps { get; set; } = 5m;

    public bool AveragingDownEnabled { get; set; }

    public decimal MaxDrawdownPercent { get; set; } = 10m;

    public bool AllowFuturesLive { get; set; }

    public bool AllowCrossMargin { get; set; }

    public RiskProfile ToProfile() => RiskProfile.Create(
        RiskPerTradePercent,
        MaxDailyLossPercent,
        MaxOpenPositions,
        MaxExposurePercent,
        FeeRate,
        SlippageBps,
        AveragingDownEnabled,
        MaxDrawdownPercent,
        AllowFuturesLive,
        AllowCrossMargin);
}

public sealed class MarketDataOptions
{
    public const string SectionName = "MarketData";

    public string[] DefaultTimeframes { get; set; } = ["1h", "4h"];

    public bool UseClosedCandlesForStrategies { get; set; } = true;

    public int StaleAfterSeconds { get; set; } = 180;
}

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string ConnectionString { get; set; } = "";

    public bool ApplyMigrationsOnStartup { get; set; }
}

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public string? AdminApiKey { get; set; }
}

public sealed class BybitOptions
{
    public const string SectionName = "Bybit";

    public string RestBaseUrl { get; set; } = "https://api.bybit.com";

    public string SpotWebSocketUrl { get; set; } = "wss://stream.bybit.com/v5/public/spot";

    public string LinearWebSocketUrl { get; set; } = "wss://stream.bybit.com/v5/public/linear";

    public string[] Symbols { get; set; } = ["BTCUSDT", "ETHUSDT"];

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxRequestsPerSecond { get; set; } = 10;
}

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string ServiceName { get; set; } = "CryptoTrader";

    public string MinimumLevel { get; set; } = "Information";

    public bool ExposePrometheus { get; set; } = true;

    public string PrometheusPath { get; set; } = "/metrics";
}
