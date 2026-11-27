# Host ingestion

Date: 2026-11-27. No commit. `dotnet build` and `dotnet test` were not run.

## Files

- `src/CryptoTrader.Host/CryptoTrader.Host.csproj` — project reference to `CryptoTrader.Exchanges.Okx`.
- `src/CryptoTrader.Host/Program.cs` — `AddOkx` after `AddBybit`, `IExchangeRegistry`, `MarketDataFeedHealth`, anonymous `GET /health/feeds`.
- `src/CryptoTrader.Host/MarketData/CandleIngestionService.cs` — multi-exchange closed-candle ingestion.
- `src/CryptoTrader.Host/MarketData/MarketDataFeedHealth.cs` — per-subscription feed status.
- `src/CryptoTrader.Host/Api/TradingApiExtensions.cs` — `GET /api/v1/exchanges` reads the registry.
- `src/CryptoTrader.Host/Api/ContractMapper.cs` — exchange list from codes, connection status null, trading mode from options.
- `src/CryptoTrader.Application/MarketData/CandleAdminService.cs` — history sync and metadata through `IExchangeRegistry`.
- `src/CryptoTrader.Application/Paper/PaperTradingCoordinator.cs` — skip non-spot and non-base volume. BYBIT-only paper remains.
- `src/CryptoTrader.Contracts/Responses/FeedHealthResponses.cs` — `FeedHealthResponse` and `FeedHealthItemResponse`.

## Behavior

`CandleIngestionService` is constructed with `IServiceScopeFactory`, `IOptions<MarketDataOptions>`, `IOptions<BybitOptions>` (empty-subscription fallback only), `TimeProvider`, `ILogger`, and `MarketDataFeedHealth`. It does not resolve `IMarketDataClient`.

Subscriptions come from `MarketData:Subscriptions`. Each entry is an exchange, an exchange symbol (hyphens kept, symbol case kept), and one or more domain timeframes. An empty array with `Bybit:Enabled` true synthesizes `BYBIT` plus each `Bybit:Symbols` entry at `1h`. Disabled Bybit and an empty list leave ingestion idle after replay.

Each target resolves `IExchangeRegistry.GetRequired` inside a scope, loads the instrument with `IInstrumentRepository.FindByExchangeSymbolAsync`, and otherwise asks that adapter's `Instruments.FindByExchangeSymbolAsync` and upserts. A mismatched exchange, `IsTradingEnabled == false`, or a linear perpetual whose `HasCompleteLinearContractSpec` is false is logged and retried with backoff. The process stays up.

The subscription is `MarketSubscription` for that timeframe with candles included and ticker, trades, and order book excluded. Only closed candles for that timeframe are upserted through `ICandleRepository.UpsertRangeAsync`, then `ICandleConsumerCheckpoint.MarkPendingAsync`, `PaperTradingCoordinator.OnClosedCandleAsync`, and `MarkProcessedAsync`. A paper exception is logged and the row stays pending. Shutdown observes `CancellationToken`.

Startup calls `ListPendingCandlesAsync(PaperConsumer, 500)` before the live loops. The checkpoint is required: a missing `ICandleConsumerCheckpoint` fails the hosted service. Other replay failures retry with backoff. Pages continue only while a full page is fully marked processed, so one failing candle does not spin. Paper that returns without throwing (non-BYBIT, non-spot, or non-base volume) is marked processed.

`MarketDataFeedHealth` is a singleton `ConcurrentDictionary`. `Healthy` means a closed candle was seen inside `MarketData:StaleAfterSeconds`. `Degraded` means the consumer is behind, the candle is older than that window, the subscription is waiting or has ended, or the instrument was rejected. `Unhealthy` means subscribe failed, acknowledgement failed, or the adapter is missing. `GET /health/feeds` is next to `GET /health/live`, outside `/api`, so it is anonymous. The body is `FeedHealthResponse`: overall status plus items (`exchange`, `instrument`, `timeframe`, `status`, `detail`, `lastEventUtc`). The HTTP status is 200.

Gauges stay on meter `CryptoTrader.MarketData`: `cryptotrader.market_data.websocket_connected` and `cryptotrader.market_data.age_seconds`. The descriptions are for any candle subscription, not Bybit-only.

`GET /api/v1/exchanges` lists `IExchangeRegistry.GetAll()` exchange codes, sorted by code. `ConnectionStatus` is null. `TradingMode` is `ExecutionOptions.Mode`. Live trading is not enabled.

`CandleAdminService.SyncAsync` calls `registry.GetRequired(exchange).MarketData.GetCandleHistoryAsync`. Completeness other than `Complete` or `Empty` throws `IncompleteCandleHistoryException` and does not upsert. Instrument download uses that exchange's adapter. List, get, and import are unchanged.

Paper returns immediately after a stored instrument is found when `instrument.Type != Spot` or `candle.VolumeUnit != VolumeUnit.BaseAsset`, with an information log. The existing non-BYBIT return remains. OKX paper trading is not enabled.

## Assumptions

- `AddOkx` is `AddOkx(this IServiceCollection, IConfiguration)` in `CryptoTrader.Exchanges.Okx`, matching `AddBybit`. The OKX project is not in the tree yet. Host references `src/CryptoTrader.Exchanges.Okx/CryptoTrader.Exchanges.Okx.csproj`.
- The contract text says ingestion depends on `IExchangeRegistry` and `ICandleConsumerCheckpoint` and does not take `BybitOptions`. This task's constructor is the one implemented: Bybit options are used only for the empty-subscription fallback, and the registry and checkpoint are resolved inside a scope.
- There is no shared acknowledgement exception. A subscribe exception whose type name or message contains `ack` is reported as an acknowledgement failure. Any other subscribe exception is `Unhealthy` with detail `Subscribe failed.`
- Channel depth is not visible on `IMarketDataClient`. The feed is `Degraded` with `Channel consumer is behind.` when `utcNow - ReceivedAtUtc` exceeds `StaleAfterSeconds`. A closed candle whose close time is older than that window is `Degraded` even if it was just read.
- Instrument rejection is `Degraded` and retried. `Unhealthy` is used for a missing adapter, a failed subscribe, and a failed acknowledgement.
- The item record is named `FeedHealthItemResponse`. The contract named `FeedHealthResponse` and the item fields.
- With the default `StaleAfterSeconds` of 180, a 1h subscription becomes `Degraded` between hourly closes. That is the stale rule as specified.
- Configured symbols are trimmed and not rewritten, so `BTC-USDT` stays intact. The Bybit fallback still uppercases `Bybit:Symbols`.
- `ICandleRepository.UpsertRangeAsync` is followed by `IUnitOfWork.SaveChangesAsync` before `MarkPendingAsync`. The current repository also saves inside its own transaction. Checkpoint methods save themselves.
- If paper succeeds and the following `MarkProcessedAsync` throws, the row stays pending and a later replay can run paper again.

## 27 ноября 2026

Текст выше — срез до правки. Сейчас ключ свечи пишется в книгу вместе с fill. Повтор ключа стратегию не вызывает. Заявка, уже сохранённая без книги, дочитывается по детерминированному client order id и проводится один раз. Устаревшая свеча помечается `Skipped` и в paper не идёт. Остановка одной пары биржа/котировка не останавливает другую. `GET /api/v1/paper-accounts` отдаёт список счетов.
