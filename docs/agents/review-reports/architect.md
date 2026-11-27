# Architect

Date: 2026-11-27. Owned files only. No commit. `dotnet build` and `dotnet test` were not run.

## Public types added

- `VolumeUnit`
- `IExchangeRegistry`
- `ExchangeRegistry`
- `IOrderBookClient`
- `CandleHistoryCompleteness`
- `CandleHistory`
- `IncompleteCandleHistoryException`
- `ICandleConsumerCheckpoint`
- `CandleConsumerState`
- `MarketDataSubscriptionOptions`
- `OkxOptions`
- `OkxOptionsValidator`

`ExchangeId.Okx` is `OKX`. `Candle.Create`, `Instrument.Create`, and `ExchangeCapabilities` only gained trailing optional parameters.

## StartupConfigurationValidator.Validate

```csharp
public static void Validate(
    ExecutionOptions execution,
    RiskOptions risk,
    MarketDataOptions marketData,
    PersistenceOptions persistence,
    ApiOptions api,
    BybitOptions bybit,
    ObservabilityOptions observability,
    OkxOptions okx)
```

`OkxOptions` is the last argument. The only caller in this agent's files is `ApplicationServiceCollectionExtensions.AddApplication`. `IOptions<OkxOptions>` is registered there.

## Files changed

- `src/CryptoTrader.Domain/Common/Identifiers.cs`
- `src/CryptoTrader.Domain/MarketData/VolumeUnit.cs` (new)
- `src/CryptoTrader.Domain/MarketData/Candle.cs`
- `src/CryptoTrader.Domain/Instruments/Instrument.cs`
- `src/CryptoTrader.Exchanges.Abstractions/IExchangeAdapter.cs`
- `src/CryptoTrader.Exchanges.Abstractions/IExchangeRegistry.cs` (new)
- `src/CryptoTrader.Exchanges.Abstractions/ExchangeRegistry.cs` (new)
- `src/CryptoTrader.Exchanges.Abstractions/MarketData/IOrderBookClient.cs` (new)
- `src/CryptoTrader.Exchanges.Abstractions/MarketData/CandleHistory.cs` (new)
- `src/CryptoTrader.Exchanges.Abstractions/MarketData/IMarketDataClient.cs`
- `src/CryptoTrader.Exchanges.Abstractions/Models/ExchangeModels.cs`
- `src/CryptoTrader.Application/Abstractions/MarketData/ICandleConsumerCheckpoint.cs` (new)
- `src/CryptoTrader.Application/Configuration/Options.cs`
- `src/CryptoTrader.Application/Configuration/OptionsValidators.cs`
- `src/CryptoTrader.Application/Configuration/StartupConfigurationValidator.cs`
- `src/CryptoTrader.Application/DependencyInjection/ApplicationOptionsReader.cs`
- `src/CryptoTrader.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `docs/architecture.md`
- `docs/adr/011-exchange-registry.md` (new)
- `docs/adr/012-volume-and-contract-metadata.md` (new)
- `tests/CryptoTrader.Domain.Tests/CandleTests.cs`
- `tests/CryptoTrader.Domain.Tests/InstrumentTests.cs` (new)
- `tests/CryptoTrader.Architecture.Tests/ExchangeRegistryTests.cs` (new)
- `docs/agents/review-reports/architect.md` (new)

Bybit, OKX, Host, Infrastructure, Backtesting, and `DependencyRuleTests.cs` were not edited.

## Deviations

- `docs/okx-api-reference-notes.md` replaces the placeholder hosts. Application defaults are `https://openapi.okx.com` and `wss://ws.okx.com/ws/v5/public`. Port `8443` is not the default; the notes say it was retired.
- `MaxHistoryCandlesPerRequest` stays `100`, matching the task and the notes' default page size. The notes set the history `limit` maximum at `300`, same as recent candles, so startup accepts `1` through `300` rather than capping the property at `100`.
- Startup bounds that the contract did not spell out: request and ack timeouts `1` through `120`, recent and history candle limits `1` through `300`, ping interval `1` through `29` (notes: `N < 30`), `MaxPages` and both channel capacities greater than zero. `InstrumentTypes` must be a non-empty set of `SPOT` and/or `SWAP` with no duplicates. Both types are the default; a single type is allowed.
- There is no `BusinessWebSocketUrl`. The notes put candle channels on `wss://ws.okx.com/ws/v5/business`, not on the public socket. Domain `1d` is a UTC day and maps to OKX `1Dutc`, not `1D` (UTC+8).
- Subscription duplicates compare the exchange case-insensitively, the timeframe through `Timeframe.Parse`, and the instrument after trim with ordinal case. Empty `Subscriptions` are valid.
- `CryptoTrader.Exchanges.Okx` had no source files when this note was written. `docs/architecture.md` marks public OKX data Not Implemented. Host's csproj and `Program.cs` already reference `AddOkx`. No private trading, secrets, or futures accounting were added. `SupportsFuturesAccounting` defaults to false. Live orders stay disabled.
- EF maps the new candle, instrument, and checkpoint columns. The only migration is still `Initial`, so that row is Partially Implemented. That migration was not edited here.
