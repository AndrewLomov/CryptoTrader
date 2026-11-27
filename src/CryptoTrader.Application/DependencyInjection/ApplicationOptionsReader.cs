using System.Globalization;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Domain.Trading;
using Microsoft.Extensions.Configuration;

namespace CryptoTrader.Application.DependencyInjection;

public static class ApplicationOptionsReader
{
    private const NumberStyles DecimalStyle =
        NumberStyles.AllowLeadingSign
        | NumberStyles.AllowDecimalPoint
        | NumberStyles.AllowLeadingWhite
        | NumberStyles.AllowTrailingWhite;

    public static ExecutionOptions ReadExecution(IConfiguration configuration) =>
        Read(configuration, static (section, errors) =>
        {
            var options = new ExecutionOptions();
            options.Mode = ReadEnum(section, "Mode", options.Mode, errors, "Execution:Mode");
            options.LiveTradingEnabled = ReadBool(section, "LiveTradingEnabled", options.LiveTradingEnabled, errors, "Execution:LiveTradingEnabled");
            options.PaperInitialEquity = ReadDecimal(section, "PaperInitialEquity", options.PaperInitialEquity, errors, "Execution:PaperInitialEquity");
            return options;
        });

    public static RiskOptions ReadRisk(IConfiguration configuration) =>
        Read(configuration, static (section, errors) =>
        {
            var options = new RiskOptions();
            options.RiskPerTradePercent = ReadDecimal(section, "RiskPerTradePercent", options.RiskPerTradePercent, errors, "Risk:RiskPerTradePercent");
            options.MaxDailyLossPercent = ReadDecimal(section, "MaxDailyLossPercent", options.MaxDailyLossPercent, errors, "Risk:MaxDailyLossPercent");
            options.MaxOpenPositions = ReadInt(section, "MaxOpenPositions", options.MaxOpenPositions, errors, "Risk:MaxOpenPositions");
            options.MaxExposurePercent = ReadDecimal(section, "MaxExposurePercent", options.MaxExposurePercent, errors, "Risk:MaxExposurePercent");
            options.FeeRate = ReadDecimal(section, "FeeRate", options.FeeRate, errors, "Risk:FeeRate");
            options.SlippageBps = ReadDecimal(section, "SlippageBps", options.SlippageBps, errors, "Risk:SlippageBps");
            options.AveragingDownEnabled = ReadBool(section, "AveragingDownEnabled", options.AveragingDownEnabled, errors, "Risk:AveragingDownEnabled");
            options.MaxDrawdownPercent = ReadDecimal(section, "MaxDrawdownPercent", options.MaxDrawdownPercent, errors, "Risk:MaxDrawdownPercent");
            options.AllowFuturesLive = ReadBool(section, "AllowFuturesLive", options.AllowFuturesLive, errors, "Risk:AllowFuturesLive");
            options.AllowCrossMargin = ReadBool(section, "AllowCrossMargin", options.AllowCrossMargin, errors, "Risk:AllowCrossMargin");
            return options;
        });

    public static MarketDataOptions ReadMarketData(IConfiguration configuration) =>
        Read(configuration, static (section, errors) =>
        {
            var options = new MarketDataOptions();
            var timeframes = ReadArray(section, "DefaultTimeframes");
            if (timeframes is not null)
            {
                options.DefaultTimeframes = timeframes;
            }

            options.UseClosedCandlesForStrategies = ReadBool(
                section,
                "UseClosedCandlesForStrategies",
                options.UseClosedCandlesForStrategies,
                errors,
                "MarketData:UseClosedCandlesForStrategies");
            options.StaleAfterSeconds = ReadInt(section, "StaleAfterSeconds", options.StaleAfterSeconds, errors, "MarketData:StaleAfterSeconds");
            return options;
        });

    public static PersistenceOptions ReadPersistence(IConfiguration configuration) =>
        Read(configuration, static (section, errors) =>
        {
            var options = new PersistenceOptions();
            if (section["ConnectionString"] is string connectionString)
            {
                options.ConnectionString = connectionString;
            }

            options.ApplyMigrationsOnStartup = ReadBool(
                section,
                "ApplyMigrationsOnStartup",
                options.ApplyMigrationsOnStartup,
                errors,
                "Persistence:ApplyMigrationsOnStartup");
            return options;
        });

    public static ApiOptions ReadApi(IConfiguration configuration) =>
        Read(configuration, static (section, _) => new ApiOptions { AdminApiKey = section["AdminApiKey"] });

    public static BybitOptions ReadBybit(IConfiguration configuration) =>
        Read(configuration, static (section, errors) =>
        {
            var options = new BybitOptions();
            options.RestBaseUrl = ReadString(section, "RestBaseUrl", options.RestBaseUrl);
            options.SpotWebSocketUrl = ReadString(section, "SpotWebSocketUrl", options.SpotWebSocketUrl);
            options.LinearWebSocketUrl = ReadString(section, "LinearWebSocketUrl", options.LinearWebSocketUrl);
            var symbols = ReadArray(section, "Symbols");
            if (symbols is not null)
            {
                options.Symbols = symbols;
            }

            options.TimeoutSeconds = ReadInt(section, "TimeoutSeconds", options.TimeoutSeconds, errors, "Bybit:TimeoutSeconds");
            options.MaxRequestsPerSecond = ReadInt(section, "MaxRequestsPerSecond", options.MaxRequestsPerSecond, errors, "Bybit:MaxRequestsPerSecond");
            return options;
        });

    public static ObservabilityOptions ReadObservability(IConfiguration configuration) =>
        Read(configuration, static (section, errors) =>
        {
            var options = new ObservabilityOptions();
            options.ServiceName = ReadString(section, "ServiceName", options.ServiceName);
            options.MinimumLevel = ReadString(section, "MinimumLevel", options.MinimumLevel);
            options.ExposePrometheus = ReadBool(section, "ExposePrometheus", options.ExposePrometheus, errors, "Observability:ExposePrometheus");
            options.PrometheusPath = ReadString(section, "PrometheusPath", options.PrometheusPath);
            return options;
        });

    private static T Read<T>(IConfiguration configuration, Func<IConfigurationSection, List<string>, T> factory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var errors = new List<string>();
        var sectionName = SectionName(typeof(T));
        var options = factory(configuration.GetSection(sectionName), errors);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("CryptoTrader configuration is invalid: " + string.Join(" | ", errors));
        }

        return options;
    }

    private static string SectionName(Type optionsType) => optionsType.Name switch
    {
        nameof(ExecutionOptions) => ExecutionOptions.SectionName,
        nameof(RiskOptions) => RiskOptions.SectionName,
        nameof(MarketDataOptions) => MarketDataOptions.SectionName,
        nameof(PersistenceOptions) => PersistenceOptions.SectionName,
        nameof(ApiOptions) => ApiOptions.SectionName,
        nameof(BybitOptions) => BybitOptions.SectionName,
        nameof(ObservabilityOptions) => ObservabilityOptions.SectionName,
        _ => throw new InvalidOperationException($"Options section for {optionsType.Name} is not known."),
    };

    private static string[]? ReadArray(IConfiguration section, string key)
    {
        var child = section.GetSection(key);
        if (!child.Exists())
        {
            return null;
        }

        var values = child.GetChildren()
            .Select(item => item.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        if (values.Length == 0 && !string.IsNullOrWhiteSpace(child.Value))
        {
            return [child.Value.Trim()];
        }

        return values;
    }

    private static string ReadString(IConfiguration section, string key, string fallback)
    {
        var text = section[key];
        return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    }

    private static bool ReadBool(IConfiguration section, string key, bool fallback, List<string> errors, string path)
    {
        var text = section[key];
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (bool.TryParse(text, out var value))
        {
            return value;
        }

        errors.Add($"{path} value '{text}' is not a boolean.");
        return fallback;
    }

    private static int ReadInt(IConfiguration section, string key, int fallback, List<string> errors, string path)
    {
        var text = section[key];
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{path} value '{text}' is not an integer.");
        return fallback;
    }

    private static decimal ReadDecimal(IConfiguration section, string key, decimal fallback, List<string> errors, string path)
    {
        var text = section[key];
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (decimal.TryParse(text, DecimalStyle, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{path} value '{text}' is not a decimal.");
        return fallback;
    }

    private static TradingMode ReadEnum(IConfiguration section, string key, TradingMode fallback, List<string> errors, string path)
    {
        var text = section[key];
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (Enum.TryParse<TradingMode>(text, true, out var value) && Enum.IsDefined(value))
        {
            return value;
        }

        errors.Add($"{path} value '{text}' is not a supported trading mode.");
        return fallback;
    }
}
