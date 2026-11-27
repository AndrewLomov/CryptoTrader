using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Application.Configuration;

public static class StartupConfigurationValidator
{
    public static void Validate(
        ExecutionOptions execution,
        RiskOptions risk,
        MarketDataOptions marketData,
        PersistenceOptions persistence,
        ApiOptions api,
        BybitOptions bybit,
        ObservabilityOptions observability,
        OkxOptions okx)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(risk);
        ArgumentNullException.ThrowIfNull(marketData);
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(bybit);
        ArgumentNullException.ThrowIfNull(observability);
        ArgumentNullException.ThrowIfNull(okx);

        var failures = new List<string>();
        Collect(new ExecutionOptionsValidator().Validate(null, execution), failures);
        Collect(new RiskOptionsValidator().Validate(null, risk), failures);
        Collect(new MarketDataOptionsValidator().Validate(null, marketData), failures);
        Collect(new PersistenceOptionsValidator().Validate(null, persistence), failures);
        Collect(new ApiOptionsValidator().Validate(null, api), failures);
        Collect(new BybitOptionsValidator().Validate(null, bybit), failures);
        Collect(new ObservabilityOptionsValidator().Validate(null, observability), failures);
        Collect(new OkxOptionsValidator().Validate(null, okx), failures);
        CollectSubscriptionFailures(marketData, bybit, okx, failures);
        if (failures.Count > 0)
        {
            throw new InvalidOperationException("CryptoTrader configuration is invalid: " + string.Join(" | ", failures));
        }
    }

    private static void CollectSubscriptionFailures(
        MarketDataOptions marketData,
        BybitOptions bybit,
        OkxOptions okx,
        List<string> failures)
    {
        if (marketData.Subscriptions is null)
        {
            failures.Add("MarketData:Subscriptions is required.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < marketData.Subscriptions.Length; index++)
        {
            var subscription = marketData.Subscriptions[index];
            if (subscription is null)
            {
                failures.Add($"MarketData:Subscriptions[{index}] is required.");
                continue;
            }

            var exchange = subscription.Exchange?.Trim() ?? "";
            var instrument = subscription.Instrument?.Trim() ?? "";
            if (exchange.Length == 0)
            {
                failures.Add($"MarketData:Subscriptions[{index}]:Exchange is required.");
            }

            if (instrument.Length == 0)
            {
                failures.Add($"MarketData:Subscriptions[{index}]:Instrument is required.");
            }

            var exchangeCode = exchange.ToUpperInvariant();
            var known = exchangeCode is "BYBIT" or "OKX";
            if (exchange.Length > 0 && !known)
            {
                failures.Add($"MarketData:Subscriptions[{index}]:Exchange must be BYBIT or OKX.");
            }
            else if (known && !IsExchangeEnabled(exchangeCode, bybit, okx))
            {
                failures.Add($"MarketData:Subscriptions[{index}]:Exchange '{exchangeCode}' is disabled.");
            }

            if (subscription.Timeframes is null || subscription.Timeframes.Length == 0)
            {
                failures.Add($"MarketData:Subscriptions[{index}]:Timeframes requires at least one timeframe.");
                continue;
            }

            foreach (var timeframe in subscription.Timeframes)
            {
                if (string.IsNullOrWhiteSpace(timeframe))
                {
                    failures.Add($"MarketData:Subscriptions[{index}]:Timeframes contains an empty timeframe.");
                    continue;
                }

                string code;
                try
                {
                    code = Timeframe.Parse(timeframe).Code;
                }
                catch (DomainException exception)
                {
                    failures.Add($"MarketData:Subscriptions[{index}]:Timeframes contains '{timeframe.Trim()}': {exception.Message}");
                    continue;
                }

                if (exchange.Length == 0 || instrument.Length == 0)
                {
                    continue;
                }

                var key = exchangeCode + "\u001f" + instrument + "\u001f" + code;
                if (!seen.Add(key))
                {
                    failures.Add($"MarketData:Subscriptions duplicate '{exchangeCode}', '{instrument}', '{code}'.");
                }
            }
        }
    }

    private static bool IsExchangeEnabled(string exchangeCode, BybitOptions bybit, OkxOptions okx) =>
        exchangeCode switch
        {
            "BYBIT" => bybit.Enabled,
            "OKX" => okx.Enabled,
            _ => false,
        };

    private static void Collect(ValidateOptionsResult result, List<string> failures)
    {
        if (!result.Failed || result.Failures is null)
        {
            return;
        }

        foreach (var failure in result.Failures)
        {
            if (!string.IsNullOrWhiteSpace(failure))
            {
                failures.Add(failure);
            }
        }
    }
}
