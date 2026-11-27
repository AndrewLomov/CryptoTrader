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
        ObservabilityOptions observability)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(risk);
        ArgumentNullException.ThrowIfNull(marketData);
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(bybit);
        ArgumentNullException.ThrowIfNull(observability);

        var failures = new List<string>();
        Collect(new ExecutionOptionsValidator().Validate(null, execution), failures);
        Collect(new RiskOptionsValidator().Validate(null, risk), failures);
        Collect(new MarketDataOptionsValidator().Validate(null, marketData), failures);
        Collect(new PersistenceOptionsValidator().Validate(null, persistence), failures);
        Collect(new ApiOptionsValidator().Validate(null, api), failures);
        Collect(new BybitOptionsValidator().Validate(null, bybit), failures);
        Collect(new ObservabilityOptionsValidator().Validate(null, observability), failures);
        if (failures.Count > 0)
        {
            throw new InvalidOperationException("CryptoTrader configuration is invalid: " + string.Join(" | ", failures));
        }
    }

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
