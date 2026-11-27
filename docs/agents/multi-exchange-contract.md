# Multi-exchange contract (Lead, 2026-11-27)

Approved before adapter implementation. Do not rename existing Bybit instrument ids. Do not add an OKX SDK. Do not implement private REST, private WebSocket, live orders, withdrawals, futures accounting, futures backtesting, or futures paper trading.

Baseline: `8130a9e2d582fab944d0c9db0a0c8983e9f09cae` on `feature/okx-multi-exchange`.

## Identity

- `ExchangeId.Okx` is `new("OKX")`. `ExchangeId.Bybit` stays `BYBIT`.
- Adapter `ExchangeCode` equals `ExchangeId.Value` (`BYBIT`, `OKX`).
- Bybit instrument ids stay `BYBIT-SPOT-{symbol}` and `BYBIT-LINEAR-{symbol}`.
- OKX spot id: `OKX-SPOT-{instId}` with the exact instId, for example `OKX-SPOT-BTC-USDT`. `ExchangeSymbol` is `BTC-USDT`.
- OKX linear swap id: `OKX-LINEAR-SWAP-{base}-{quote}`, for example `OKX-LINEAR-SWAP-BTC-USDT`. `ExchangeSymbol` stays `BTC-USDT-SWAP`.
- Do not strip hyphens into Bybit symbols.

## Domain additions (append optional parameters only)

`VolumeUnit` in `CryptoTrader.Domain.MarketData`: `BaseAsset = 1`, `QuoteAsset = 2`, `Contract = 3`.

`Candle.Create` gains trailing optional parameters:

```csharp
VolumeUnit volumeUnit = VolumeUnit.BaseAsset,
decimal? quoteVolume = null,
decimal? baseVolume = null
```

New properties: `VolumeUnit`, `QuoteVolume`, `BaseVolume`. Null extra volumes must be `>= 0` when set. `Volume` remains the exchange quantity in `VolumeUnit`. Spot stores base size in `Volume` and quote turnover in `QuoteVolume` when the exchange sends it. SWAP stores contract count in `Volume` (`VolumeUnit.Contract`), base size in `BaseVolume` when provided, quote turnover in `QuoteVolume`. Never copy contract count into a base-volume field and never substitute `1` for a missing contract value.

`Instrument.Create` gains trailing optional parameters:

```csharp
decimal? contractValue = null,
string? contractValueCurrency = null,
string? settlementCurrency = null,
string? contractKind = null
```

Spot requires all four to stay null, and `ContractMultiplier` stays null. A linear perpetual is valid with `ContractMultiplier > 0`, or with a complete face value and a null multiplier. An explicit multiplier `<= 0` is rejected. Missing `ctVal` stays null and is not replaced with 1. Empty OKX `ctMult` stays null and is not replaced with 1.

```csharp
public bool HasCompleteLinearContractSpec { get; }
```

True only when type is linear perpetual, multiplier `> 0`, contract value `> 0`, both currencies are non-empty, and kind is `linear`.

`ExchangeCapabilities` gains a trailing optional parameter `bool supportsFuturesAccounting = false`. OKX and Bybit leave it false. `SupportsLinearPerpetual` means public linear market data, not futures accounting or live trading. `SupportsLiveOrders` and `SupportsPrivateAccount` stay false for OKX.

## Registry and capabilities

`CryptoTrader.Exchanges.Abstractions`:

```csharp
public interface IExchangeRegistry
{
    IReadOnlyCollection<IExchangeAdapter> GetAll();
    IExchangeAdapter GetRequired(ExchangeId exchangeId);
    bool TryGet(ExchangeId exchangeId, out IExchangeAdapter? adapter);
}
```

`ExchangeRegistry` takes `IEnumerable<IExchangeAdapter>`. Duplicate `ExchangeId` throws `InvalidOperationException` (`Exchange '{id}' is registered more than once.`). Unknown id throws `InvalidOperationException` (`Exchange '{id}' is not registered.`). Lookup is dictionary-based, not DI order.

`IExchangeAdapter` default members:

```csharp
ExchangeId ExchangeId => new(ExchangeCode);
IOrderBookClient? OrderBook => null;
IFundingRateClient? FundingRates => null;
```

```csharp
public interface IOrderBookClient
{
    Task<OrderBookSnapshot> GetSnapshotAsync(InstrumentId instrument, CancellationToken cancellationToken);
}
```

```csharp
public enum CandleHistoryCompleteness
{
    Complete = 1,
    Incomplete = 2,
    Empty = 3,
    Unverified = 4,
}

public sealed record CandleHistory(
    IReadOnlyList<Candle> Candles,
    CandleHistoryCompleteness Completeness,
    string? Reason);

public sealed class IncompleteCandleHistoryException : InvalidOperationException
{
    public IncompleteCandleHistoryException(string message) : base(message) {}
}
```

`IMarketDataClient` keeps `GetCandlesAsync`. Add `GetCandleHistoryAsync` with a default implementation that delegates to `GetCandlesAsync` and returns `Unverified` (or `Empty` when the list is empty) and reason `Adapter did not report whether the requested range was complete.` Bybit and OKX override it. Their `GetCandlesAsync` returns candles only for `Complete` or `Empty`; `Incomplete` throws `IncompleteCandleHistoryException` and does not look like success.

Closed candles only. `confirm = 0` is not closed. Unclosed candles never enter backtest or strategy input.

## Checkpoint port

`CryptoTrader.Application.Abstractions.MarketData.ICandleConsumerCheckpoint`:

```csharp
public const string PaperConsumer = "paper";

Task MarkPendingAsync(Candle candle, string consumer, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken);
Task MarkProcessedAsync(Candle candle, string consumer, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken);
Task<IReadOnlyList<Candle>> ListPendingCandlesAsync(string consumer, int limit, CancellationToken cancellationToken);
```

States: `CandleConsumerState.Pending = 1`, `Processed = 2`. Implementations persist their own `SaveChanges`. Replay only rows this pipeline marked pending. Do not replay every historical candle.

## Configuration

`MarketDataOptions.Subscriptions` is `MarketDataSubscriptionOptions[]`, default empty.

```csharp
public sealed class MarketDataSubscriptionOptions
{
    public string Exchange { get; set; } = "";
    public string Instrument { get; set; } = "";
    public string[] Timeframes { get; set; } = [];
}
```

Timeframes are domain codes (`1m`, `5m`, `15m`, `1h`, `4h`, `1d`), not OKX bar names. Validation:

- duplicate `(exchange, instrument, timeframe)` after trim fails startup;
- empty exchange, instrument, or timeframe fails;
- unknown timeframe fails;
- exchange must be `BYBIT` or `OKX`;
- a subscription whose exchange is disabled fails;
- when `Subscriptions` is empty, ingestion falls back to `Bybit:Symbols` at `1h` if Bybit is enabled.

Application `BybitOptions` and the Bybit project `BybitOptions` both gain `bool Enabled { get; set; } = true` and `int ChannelCapacity { get; set; } = 256`.

Application `OkxOptions` (startup validation, no project reference to the adapter) and `CryptoTrader.Exchanges.Okx.OkxOptions` share section `Okx`:

- `Enabled` default false until config sets true. `appsettings.json` may set true for public data.
- `RestBaseUrl` default comes from `docs/okx-api-reference-notes.md` after the doc check. Until that file exists, use `https://www.okx.com`.
- `PublicWebSocketUrl` default from the same notes. Placeholder `wss://ws.okx.com:8443/ws/v5/public` only if the notes confirm it.
- `RequestTimeoutSeconds` 15.
- `MaxCandlesPerRequest` 300 for recent candles; history limit is whatever the notes say (do not assume 300).
- `MaxPages` 500.
- `ChannelCapacity` 256.
- `AckTimeoutSeconds` 10.
- `PingIntervalSeconds` from the notes (expected around 20, below the server idle timeout).
- `InstrumentTypes` `SPOT` and `SWAP`.
- No API key, secret, or passphrase.

`StartupConfigurationValidator.Validate` gains `OkxOptions okx` as the last argument. `AddApplication` reads and validates it. Cross-check subscriptions against `Bybit:Enabled` and `Okx:Enabled`.

## Ingestion

`CandleIngestionService` depends on `IOptions<MarketDataOptions>`, `IExchangeRegistry`, and `ICandleConsumerCheckpoint`. It does not take `BybitOptions` and does not call `GetService<IMarketDataClient>()`.

For each subscription, resolve the adapter with `GetRequired`, resolve the instrument for that exchange, subscribe to closed candles for that timeframe, upsert, mark pending, save, call paper, mark processed. Paper failure leaves the row pending. Startup replays pending candles before the live loop. Paper returns without throwing for non-Bybit and non-spot; those candles are still marked processed.

`CandleAdminService.SyncAsync` uses `registry.GetRequired(exchange).MarketData.GetCandleHistoryAsync`. Incomplete history throws and is not stored.

`/api/v1/exchanges` lists registry adapters. `GET /health/feeds` is anonymous and returns per-subscription status (`Healthy`, `Degraded`, `Unhealthy`).

Bybit DI stops registering `IMarketDataClient`, `ITradingClient`, `IAccountClient`, `IInstrumentMetadataClient`, and `IFundingRateClient` as single default services. It still registers `IExchangeAdapter`. `AddOkx` does the same. Host builds the registry after both `AddBybit` and `AddOkx`:

```csharp
services.AddSingleton<IExchangeRegistry>(sp => new ExchangeRegistry(sp.GetServices<IExchangeAdapter>()));
```

## Persistence

Additive nullable instrument columns: `contract_value`, `contract_value_currency`, `settlement_currency`, `contract_kind`.

Candle columns: `volume_unit integer not null default 1`, `quote_volume numeric null`, `base_volume numeric null`.

Table `candle_consumer_checkpoints` primary key `(exchange_id, instrument_id, timeframe, open_time_utc, consumer)` plus `state` and `updated_at_utc`. No drops. Existing Bybit rows stay valid: volume unit defaults to base asset.

Unique candle key remains `(exchange, instrument, timeframe, open_time)`.

## OKX public behavior

- Success is OKX body `code == "0"`. HTTP 200 with any other code is a failure.
- Decimal strings use invariant culture. Timestamps become UTC.
- Recent candles: `/api/v5/market/candles`. Older than the documented recent window: `/api/v5/market/history-candles`.
- Pagination follows OKX `after` / `before` (not Bybit start/end). Stop when the cursor does not move. Deduplicate by open time. Sort ascending. Filter to the requested range. Gaps or a page cap yield `Incomplete`, never `Complete`.
- Spot `vol` is base. SWAP `vol` is contracts. Do not mix them.
- Linear USDT SWAP is market-data eligible when `ctType=linear`, `ctVal > 0`, and the value and settlement currencies are present. Empty `ctMult` does not block that. Inverse, futures, options, and margin are skipped. `state=live` sets `IsTradingEnabled`. Other states are stored only if mapped, with `IsTradingEnabled=false`, and normal subscriptions reject them.
- `ContractMultiplier` stores `ctMult` only when `ctMult > 0`. `ContractValue` stores `ctVal`. Never invent either.
- Order book in this increment is snapshot only (`books` REST and WS `books5` or the documented snapshot channel). No incremental book unless sequence rules are implemented and tested.
- Funding rate public endpoint for linear SWAP only.
- Trading client returns `LiveTradingDisabled` and `UnknownState=false` with no network call. Cancel and account reads throw `NotSupportedException` with no network call.
- WebSocket: track desired subscriptions, match acks, treat error acks and ack timeouts as failures, ping/pong, reconnect with bounded backoff and jitter, resubscribe, bounded channel (`Wait`) for candles, trades, and book snapshots. Ticker may keep only the latest value if that coalesce is documented. Overload marks the feed degraded or unhealthy.

## Projects

`src/CryptoTrader.Exchanges.Okx` references only `CryptoTrader.Exchanges.Abstractions` and the same extension packages as Bybit, without package versions. Add it to `CryptoTrader.slnx` and to Host. Update `DependencyRuleTests.ExpectedReferences`: OKX references Abstractions only; Host also references OKX.

## Out of scope

Kafka, RabbitMQ, Kubernetes, microservices, private OKX API, live orders, futures PnL, liquidation, and margin.
