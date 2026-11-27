using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Trading;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Application.Configuration;

public sealed class ExecutionOptionsValidator : IValidateOptions<ExecutionOptions>
{
    public ValidateOptionsResult Validate(string? name, ExecutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (!Enum.IsDefined(options.Mode))
        {
            failures.Add("Execution:Mode is not a supported trading mode.");
        }

        if (options.LiveTradingEnabled)
        {
            failures.Add("Execution:LiveTradingEnabled must be false in CryptoTrader 0.1.");
        }

        if (options.Mode == TradingMode.Live)
        {
            failures.Add("Execution:Mode cannot be Live in CryptoTrader 0.1.");
        }

        if (options.PaperInitialEquity <= 0)
        {
            failures.Add("Execution:PaperInitialEquity must be greater than zero.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public sealed class RiskOptionsValidator : IValidateOptions<RiskOptions>
{
    public ValidateOptionsResult Validate(string? name, RiskOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.AllowFuturesLive)
        {
            failures.Add("Risk:AllowFuturesLive must be false in CryptoTrader 0.1.");
        }

        if (options.AllowCrossMargin)
        {
            failures.Add("Risk:AllowCrossMargin must be false in CryptoTrader 0.1.");
        }

        RequirePercent(options.RiskPerTradePercent, "Risk:RiskPerTradePercent", failures);
        RequirePercent(options.MaxDailyLossPercent, "Risk:MaxDailyLossPercent", failures);
        RequirePercent(options.MaxExposurePercent, "Risk:MaxExposurePercent", failures);
        RequirePercent(options.MaxDrawdownPercent, "Risk:MaxDrawdownPercent", failures);
        if (options.MaxOpenPositions < 1 || options.MaxOpenPositions > 100)
        {
            failures.Add("Risk:MaxOpenPositions must be from 1 through 100.");
        }

        if (options.FeeRate < 0 || options.FeeRate >= 1)
        {
            failures.Add("Risk:FeeRate must be greater than or equal to 0 and less than 1.");
        }

        if (options.SlippageBps < 0 || options.SlippageBps > 10000)
        {
            failures.Add("Risk:SlippageBps must be from 0 through 10000.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePercent(decimal value, string name, List<string> failures)
    {
        if (value <= 0 || value > 100)
        {
            failures.Add($"{name} must be greater than 0 and less than or equal to 100.");
        }
    }
}

public sealed class MarketDataOptionsValidator : IValidateOptions<MarketDataOptions>
{
    public ValidateOptionsResult Validate(string? name, MarketDataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (!options.UseClosedCandlesForStrategies)
        {
            failures.Add("MarketData:UseClosedCandlesForStrategies must be true in CryptoTrader 0.1.");
        }

        if (options.StaleAfterSeconds <= 0)
        {
            failures.Add("MarketData:StaleAfterSeconds must be greater than zero.");
        }

        if (options.DefaultTimeframes is null || options.DefaultTimeframes.Length == 0)
        {
            failures.Add("MarketData:DefaultTimeframes requires at least one timeframe.");
        }
        else
        {
            foreach (var timeframe in options.DefaultTimeframes)
            {
                try
                {
                    _ = Timeframe.Parse(timeframe);
                }
                catch (DomainException exception)
                {
                    failures.Add($"MarketData:DefaultTimeframes contains '{timeframe}': {exception.Message}");
                }
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public sealed class PersistenceOptionsValidator : IValidateOptions<PersistenceOptions>
{
    public ValidateOptionsResult Validate(string? name, PersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Persistence:ConnectionString is required.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class ApiOptionsValidator : IValidateOptions<ApiOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.AdminApiKey))
        {
            return ValidateOptionsResult.Fail("Api:AdminApiKey must be configured.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class BybitOptionsValidator : IValidateOptions<BybitOptions>
{
    public ValidateOptionsResult Validate(string? name, BybitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (!IsScheme(options.RestBaseUrl, "https"))
        {
            failures.Add("Bybit:RestBaseUrl must be an absolute https URL.");
        }

        if (!IsScheme(options.SpotWebSocketUrl, "wss"))
        {
            failures.Add("Bybit:SpotWebSocketUrl must be an absolute wss URL.");
        }

        if (!IsScheme(options.LinearWebSocketUrl, "wss"))
        {
            failures.Add("Bybit:LinearWebSocketUrl must be an absolute wss URL.");
        }

        if (options.Symbols is null || options.Symbols.Length == 0 || options.Symbols.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Bybit:Symbols requires at least one symbol.");
        }

        if (options.TimeoutSeconds <= 0 || options.TimeoutSeconds > 120)
        {
            failures.Add("Bybit:TimeoutSeconds must be from 1 through 120.");
        }

        if (options.MaxRequestsPerSecond <= 0 || options.MaxRequestsPerSecond > 10)
        {
            failures.Add("Bybit:MaxRequestsPerSecond must be from 1 through 10.");
        }

        if (options.ChannelCapacity <= 0)
        {
            failures.Add("Bybit:ChannelCapacity must be greater than zero.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsScheme(string? value, string scheme) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase);
}

public sealed class ObservabilityOptionsValidator : IValidateOptions<ObservabilityOptions>
{
    private static readonly HashSet<string> Levels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose",
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Fatal",
        "Trace",
    };

    public ValidateOptionsResult Validate(string? name, ObservabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            failures.Add("Observability:ServiceName is required.");
        }

        if (string.IsNullOrWhiteSpace(options.MinimumLevel) || !Levels.Contains(options.MinimumLevel))
        {
            failures.Add("Observability:MinimumLevel is not a supported log level.");
        }

        if (string.IsNullOrWhiteSpace(options.PrometheusPath) || !options.PrometheusPath.StartsWith('/'))
        {
            failures.Add("Observability:PrometheusPath must start with '/'.");
        }
        else if (options.PrometheusPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Observability:PrometheusPath must stay outside /api.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public sealed class OkxOptionsValidator : IValidateOptions<OkxOptions>
{
    public ValidateOptionsResult Validate(string? name, OkxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (!IsScheme(options.RestBaseUrl, "https"))
        {
            failures.Add("Okx:RestBaseUrl must be an absolute https URL.");
        }

        if (!IsScheme(options.PublicWebSocketUrl, "wss"))
        {
            failures.Add("Okx:PublicWebSocketUrl must be an absolute wss URL.");
        }

        if (!IsScheme(options.BusinessWebSocketUrl, "wss"))
        {
            failures.Add("Okx:BusinessWebSocketUrl must be an absolute wss URL.");
        }

        if (options.RequestTimeoutSeconds <= 0 || options.RequestTimeoutSeconds > 120)
        {
            failures.Add("Okx:RequestTimeoutSeconds must be from 1 through 120.");
        }

        if (options.MaxCandlesPerRequest <= 0 || options.MaxCandlesPerRequest > 300)
        {
            failures.Add("Okx:MaxCandlesPerRequest must be from 1 through 300.");
        }

        if (options.MaxHistoryCandlesPerRequest <= 0 || options.MaxHistoryCandlesPerRequest > 300)
        {
            failures.Add("Okx:MaxHistoryCandlesPerRequest must be from 1 through 300.");
        }

        if (options.MaxPages <= 0)
        {
            failures.Add("Okx:MaxPages must be greater than zero.");
        }

        if (options.ChannelCapacity <= 0)
        {
            failures.Add("Okx:ChannelCapacity must be greater than zero.");
        }

        if (options.AckTimeoutSeconds <= 0 || options.AckTimeoutSeconds > 120)
        {
            failures.Add("Okx:AckTimeoutSeconds must be from 1 through 120.");
        }

        if (options.PingIntervalSeconds <= 0 || options.PingIntervalSeconds >= 30)
        {
            failures.Add("Okx:PingIntervalSeconds must be from 1 through 29.");
        }

        if (options.InstrumentTypes is null || options.InstrumentTypes.Length == 0)
        {
            failures.Add("Okx:InstrumentTypes requires at least one instrument type.");
        }
        else
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in options.InstrumentTypes)
            {
                if (string.IsNullOrWhiteSpace(type)
                    || (!string.Equals(type, "SPOT", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(type, "SWAP", StringComparison.OrdinalIgnoreCase)))
                {
                    failures.Add($"Okx:InstrumentTypes contains '{type}', which is not SPOT or SWAP.");
                    continue;
                }

                if (!seen.Add(type))
                {
                    failures.Add($"Okx:InstrumentTypes contains duplicate '{type}'.");
                }
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsScheme(string? value, string scheme) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase);
}
