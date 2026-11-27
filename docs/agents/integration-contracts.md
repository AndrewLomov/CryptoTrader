# Integration contracts — CryptoTrader 0.1

Утверждено Lead Agent до параллельной реализации. Сигнатуры ниже являются нормативными. Уточнять внутреннюю реализацию можно. Менять имена типов, пространства имён и семантику переходов нельзя без нового решения Lead.

Общие правила:

- `decimal` для цен, количеств, комиссий, баланса и PnL. `double` в финансовой математике запрещён.
- Время только `DateTimeOffset` в UTC. В бизнес-логике нет `DateTime.Now` и `DateTime.UtcNow`. Источник времени — `TimeProvider`.
- Процент в конфигурации: `0.5` означает 0,5%. В формуле делить на 100.
- Live-заявки в 0.1 не отправляются. Публичные рыночные запросы Bybit разрешены.
- Стратегия не ссылается на биржу, EF Core, сеть и файловую конфигурацию.

## Проекты и ссылки

| Проект | Ссылки |
|---|---|
| CryptoTrader.Domain | нет |
| CryptoTrader.Contracts | нет |
| CryptoTrader.Strategies.Abstractions | Domain |
| CryptoTrader.Exchanges.Abstractions | Domain |
| CryptoTrader.Application | Domain, Strategies.Abstractions, Exchanges.Abstractions |
| CryptoTrader.Infrastructure | Domain, Application |
| CryptoTrader.Exchanges.Bybit | Exchanges.Abstractions |
| CryptoTrader.Strategies.TrendFollowing | Domain, Strategies.Abstractions |
| CryptoTrader.Backtesting | Domain, Application, Strategies.Abstractions |
| CryptoTrader.Host | Application, Infrastructure, Contracts, Exchanges.Bybit, Strategies.TrendFollowing, Backtesting |

Пакеты централизованы в `Directory.Packages.props`. Версии: EF Core `10.0.15`, Npgsql EF `10.0.4`, Extensions `10.0.15`, Serilog.AspNetCore `9.0.0`, Serilog.Sinks.Console `6.1.1`, Serilog.Settings.Configuration `9.0.0`, OpenTelemetry hosting/instrumentation/OTLP `1.21.0`, Prometheus exporter `1.21.0-beta.1`, xUnit `2.9.3`, runner `3.1.5`, Microsoft.NET.Test.Sdk `17.14.1`, Shouldly `4.3.0`, NSubstitute `5.3.0`, Testcontainers.PostgreSql `4.17.0`, NetArchTest.Rules `1.3.2`, FsCheck.Xunit `3.3.1`. SDK `10.0.403`, rollForward `latestPatch`.

Nullable включён. `TreatWarningsAsErrors` выключен.

## Domain

Пространство `CryptoTrader.Domain`.

Идентификаторы — `readonly record struct` с непустым значением: `ExchangeId`, `InstrumentId`, `OrderId` (`Guid`), `ExecutionId`, `StrategyInstanceId`, `TradingAccountId`. У `ExchangeId` есть `ExchangeId.Bybit = new("BYBIT")`.

`Timeframe` хранит код и длительность. Поддерживаются `1m`, `5m`, `15m`, `1h`, `4h`, `1d`. Есть `Parse`, `OneHour`, `FourHours`.

`Money` — amount и непустой asset. `Price` — значение > 0. `Quantity` — значение >= 0.

`DecimalRounding.FloorToStep(decimal value, decimal step)` использует `decimal.Floor(value / step) * step`. Шаг > 0. Отрицательное значение недопустимо.

`InstrumentType`: Spot = 1, LinearPerpetual = 2.

`Instrument` — immutable class или record, фабрика `Create`. `PriceTick`, `QuantityStep`, `MinQuantity` > 0. `ExchangeSymbol`, `BaseAsset`, `QuoteAsset` непустые. `ContractMultiplier` null для Spot и > 0 для LinearPerpetual.

`Candle.Create` проверяет: цены > 0, `High` не ниже Open/Close/Low, `Low` не выше Open/Close/High, `Volume` >= 0, `OpenTimeUtc` < `CloseTimeUtc`.

`CandleGapDetector.FindMissingOpenTimes` принимает свечи одного ряда, отсортированные по `OpenTimeUtc`, и возвращает отсутствующие открытия с шагом таймфрейма. Данные не интерполируются.

`Ticker`, `FundingRate`, `OrderBookLevel`, `OrderBookSnapshot`, `MarketEvent` — неизменяемые модели. `OrderBookSnapshot.IsConsistent == false` означает, что стакан нельзя использовать для торговли.

### Order

Состояния: `PendingSubmit`, `Submitted`, `Accepted`, `PartiallyFilled`, `Filled`, `CancelPending`, `Canceled`, `Rejected`, `Unknown`.

`Order.Create` создаёт `PendingSubmit`. Методы: `MarkSubmitted`, `MarkAccepted`, `MarkUnknown`, `ApplyFill`, `MarkRejected`, `RequestCancel`, `MarkCanceled`, `Rehydrate`.

Разрешённые переходы:

- PendingSubmit → Submitted, Rejected, Unknown
- Submitted → Accepted, Rejected, Unknown, PartiallyFilled, Filled, CancelPending
- Unknown → Accepted, Rejected, PartiallyFilled, Filled, Canceled
- Accepted → PartiallyFilled, Filled, CancelPending, Canceled
- PartiallyFilled → PartiallyFilled, Filled, CancelPending, Canceled
- CancelPending → Canceled, PartiallyFilled, Filled
- Filled, Canceled, Rejected — терминальные

`Filled` нельзя вернуть в `Submitted`. Повторный `ApplyFill` с тем же `ExchangeTradeId` не увеличивает количество. Количество исполнения > остатка отклоняется. После набора полного количества статус `Filled`. Иначе из Accepted/Submitted получается `PartiallyFilled`.

`Execution` хранится отдельно: id, order id, instrument, side, quantity, price, fee, fee asset, time, exchange trade id.

### Position и PortfolioLedger

`Position` ведёт сторону, количество, среднюю цену, реализованный PnL, комиссии, funding, плечо и время. Spot не уходит в short: продажа больше количества выбрасывает `DomainException`. Покупка в long пересчитывает среднюю цену. Продажа фиксирует PnL `(exit - average) * qty - fee` этой продажи. Комиссия покупки уже включена в накопленные fees; при полном закрытии она остаётся в `Fees`.

`UnrealizedPnl(markPrice)` для long: `(mark - average) * quantity`.

`PortfolioLedger.Create(cash, quoteAsset, accountId, exchange, now)` хранит cash, позиции, peak equity, equity на начало дня, флаг emergency stop. `ApplySpotFill` меняет cash на нотиционал и комиссию. Нехватка cash — `DomainException`. `Equity(mark)` = cash + сумма `quantity * mark` по открытым long. `DrawdownFraction` и дневной PnL считаются от peak и day-start equity. `EnsureDay` при смене UTC-даты переносит day-start equity. `Rehydrate` обязателен.

Денежный PnL не смешивается между Spot и Futures в одном среднем значении позиции. Linear perpetual в домене поддерживается отдельным множителем контракта, но Risk Manager в 0.1 отклоняет Live Futures.

### RiskProfile

Фабрика отвергает `AllowFuturesLive` и `AllowCrossMargin`, если они true. `Default` : риск 0,5%, дневной убыток 1%, максимум 2 позиции, экспозиция 100%, комиссия 0,001, проскальзывание 5 bps, усреднение выключено, max drawdown 10%, futures live и cross margin выключены.

`PositionSizer.TrySize` для long Spot:

```text
slippedEntry = entry * (1 + slippageBps / 10000)
unitRisk = slippedEntry - stop
riskAmount = equity * riskPercent / 100
raw = riskAmount / unitRisk
qty = FloorToStep(raw, quantityStep)
```

Отказы: `InvalidStop`, `MinQuantity`, `MinNotional`, `InsufficientBalance`. Проверка cash: `qty * slippedEntry * (1 + feeRate) + qty * stop * feeRate`. Успех возвращает количество и оценку `estimatedMaxLoss = qty * unitRisk + fees`.

### StrategyInstance

Статусы `Created`, `Running`, `Stopped`. `Start` и `Stop` не закрывают позиции. `Stop` только запрещает новые сигналы на уровне приложения.

### События

Базовый `DomainEvent`: `EventId`, `OccurredAtUtc`, `CorrelationId`, `CausationId`. Конкретные события ордера и риска создаются в точках изменения, без отдельной шины.

## Strategies.Abstractions

```csharp
public sealed record StrategyContext(
    Instrument Instrument,
    DateTimeOffset AsOfUtc,
    string ParametersJson,
    IReadOnlyList<Candle> ClosedWorkingCandles,
    IReadOnlyList<Candle> ClosedHigherTimeframeCandles,
    bool HasOpenPosition);

public interface ITradingStrategy
{
    string Id { get; }
    string Version { get; }
    string DisplayName { get; }
    ValueTask<IReadOnlyList<TradeSignal>> EvaluateAsync(
        StrategyContext context,
        CancellationToken cancellationToken);
}

public sealed record StrategyDescriptor(string Id, string Version, string DisplayName);

public interface IStrategyCatalog
{
    IReadOnlyList<StrategyDescriptor> List();
    ITradingStrategy Create(string strategyId, string parametersJson);
}
```

`TradeSignal` в Domain: `SignalId`, `StrategyId`, `StrategyVersion`, `InstrumentId`, `Action`, `OccurredAtUtc`, `ReferencePrice`, `StopLossPrice`, `TakeProfitPrice`, `Atr`, `Explanation`. `SignalAction`: `EnterLong`, `ExitLong`. Short в стратегии 0.1 не генерируется.

## Exchanges.Abstractions

Интерфейсы из ТЗ: `IExchangeAdapter`, `IMarketDataClient`, `ITradingClient`, `IAccountClient`, `IInstrumentMetadataClient`, `IFundingRateClient`.

`IMarketDataClient.GetCandlesAsync` возвращает только закрытые свечи, по возрастанию `OpenTimeUtc`. Дубликаты одного открытия сливаются. `SubscribeAsync` отдаёт `MarketEvent`.

`ITradingClient.SubmitAsync` при выключенном Live не открывает сеть и возвращает `OrderSubmissionResult` с `ErrorCode = "LiveTradingDisabled"`, `UnknownState = false`.

Модели адаптера, не доменные DTO биржи: `ExchangeCapabilities`, `MarketSubscription`, `ExchangeOrderRequest`, `OrderSubmissionResult`, `OrderStatusSnapshot`, `ExchangePositionSnapshot`, `AccountSnapshot`. Время в них уже UTC. В Domain нет JSON-полей Bybit.

`ExchangeOrderRequest` содержит client order id, instrument, side, type, quantity, limit price, reduce-only.

## Application

`IRiskManager.EvaluateAsync(TradeSignal, PortfolioSnapshot, MarketSnapshot, Instrument, RiskProfile, CancellationToken)` возвращает `RiskDecision`.

Коды отказа: `EmergencyStop`, `ExchangeUnhealthy`, `StaleMarketData`, `FuturesLiveForbidden`, `CrossMarginForbidden`, `DailyLossLimit`, `MaxDrawdown`, `MaxOpenPositions`, `MaxExposure`, `DuplicateSignal`, `InsufficientBalance`, `MinQuantity`, `MinNotional`, `InvalidStop`, `AveragingDownForbidden`, `InstrumentDisabled`, `LiveTradingDisabled`.

Порядок проверок: emergency → instrument enabled → futures live → stale/unhealthy → daily loss → drawdown → open positions → exposure → duplicate signal id → existing position / averaging → `PositionSizer`.

`PortfolioSnapshot` и `MarketSnapshot` определены в Domain.Strategies (рядом с сигналом): equity, available cash, quote asset, day PnL, drawdown fraction, open count, gross exposure, has position, position is losing, emergency stop, approved signal ids; для рынка — as of, last event, healthy, stale, bid, ask, last.

`IExecutionVenue` имеет `TradingMode Mode`, `ExecuteAsync(ApprovedOrder, Instrument, MarketSnapshot, CancellationToken)`, `ReconcileAsync`. `VenueExecutionResult` содержит принятый `Order`, необязательный `Execution` и код отказа.

`ApprovedOrder`: approval id, signal, account id, quantity, order type, limit price, client order id, approved at.

Репозитории не возвращают `IQueryable`:

- `ICandleRepository` — `UpsertRangeAsync`, `GetRangeAsync`
- `IInstrumentRepository` — `UpsertAsync`, `FindAsync`, `FindByExchangeSymbolAsync`, `ListAsync`
- `IOrderRepository` — `FindByIdAsync`, `FindByClientOrderIdAsync`, `AddAsync`, `UpdateAsync`, `ListAsync`
- `IPositionRepository` — `FindOpenAsync`, `ListAsync`, `UpsertAsync`
- `IStrategyInstanceRepository` — `AddAsync`, `FindAsync`, `ListAsync`, `UpdateAsync`
- `ITradeSignalRepository` — `AddAsync(signal, strategyInstanceId, decision)`, `ExistsAsync`
- `IBacktestRunRepository` — `AddAsync`, `FindAsync`
- `IPaperStateRepository` — `SaveAsync`, `LoadAsync`
- `IRiskEventRepository` — `AddAsync`
- `ITradingAccountRepository` — `FindAsync`, `UpsertAsync`, `ListAsync`
- `IUnitOfWork.SaveChangesAsync`

Сетевой вызов не выполняется внутри открытой транзакции БД.

### Backtest

`IBacktestEngine.RunAsync(BacktestRequest, ITradingStrategy, CancellationToken)`.

`BacktestRequest`: strategy id и version, parameters JSON, exchange, instrument, from, to, initial equity, fee rate, slippage bps, risk profile, working candles, higher candles, exit on trend break.

`BacktestReport`: run id, версии стратегии и движка `0.1.0`, параметры, диапазон, `BacktestMetrics`, `BacktestAssumptions`, сделки, equity curve.

`BacktestTrade`: номер, инструмент, время входа и выхода, цены, количество, fees, slippage cost, net PnL, exit reason (`StopLoss`, `TakeProfit`, `TrendBreak`, `EndOfData`).

`BacktestMetrics`: initial, final, net profit, ROI fraction, max drawdown fraction, Sharpe, Sortino, profit factor, win rate fraction, expectancy, trade count, total fees, total slippage, average holding time.

Модель исполнения:

1. Рабочие свечи — закрытые, по времени, внутри диапазона.
2. На свече T стратегия видит рабочие свечи с `CloseTime <= T.CloseTime` и старшие свечи с `CloseTime <= T.CloseTime`.
3. Вход Long исполняется на открытии следующей свечи: `open * (1 + slippageBps/10000)`.
4. ATR и стоп пересчитываются от фактической цены входа: `stop = fill - atr * multiplier`, `take = fill + atr * takeMultiplier`, если множитель > 0.
5. На последующей свече, если Low касается стопа и High тейк-профита, выход по стопу.
6. Продажа по стопу или тейку: `price * (1 - slippageBps/10000)`.
7. Комиссия taker на вход и выход: `notional * feeRate`.
8. Незакрытая в конце позиция закрывается по close последней свечи с неблагоприятным проскальзыванием, причина `EndOfData`.
9. Каждый закрытый бар даёт точку equity. Доходности — простые. Sharpe и Sortino при нулевой безрисковой ставке, выборка n-1, аннуализация `sqrt(365.25 * 24 / hours)` для часовых баров. Приstddev 0 или меньше чем двух доходностях коэффициент null. Profit factor null, если нет убыточных сделок.
10. Funding в отчёте 0.1 не моделируется, и это записано в assumptions.
11. Повтор с теми же свечами и параметрами даёт те же числа.

`WalkForwardSplitter.Split(from, to, inSampleFraction)` возвращает границу in-sample. Оптимизатора параметров нет.

### Paper

Paper использует тот же сигнал, риск и `PortfolioLedger`. Покупка по последней цене закрытой свечи или last, с тем же неблагоприятным проскальзыванием. Реальная заявка не создаётся. `ReconcileAsync` для Paper сверяет ledger с сохранёнными paper-позициями и не ходит на биржу.

### Конфигурация

Секции `Execution`, `Risk`, `MarketData`, `Persistence`, `Api`, `Bybit`, `Observability`.

`ExecutionOptions.LiveTradingEnabled` по умолчанию false. `RiskOptions.AllowFuturesLive` по умолчанию false. Валидатор запуска бросает исключение, если любое из этих значений true. Пустой `Api:AdminApiKey` тоже останавливает запуск.

`BybitOptions`: REST `https://api.bybit.com`, spot WS `wss://stream.bybit.com/v5/public/spot`, linear WS `wss://stream.bybit.com/v5/public/linear`, символы `BTCUSDT` и `ETHUSDT`, таймаут 15 секунд.

## Bybit V5

Публичные методы, без ключа:

- `GET /v5/market/instruments-info`
- `GET /v5/market/kline`
- `GET /v5/market/tickers`
- `GET /v5/market/orderbook`
- `GET /v5/market/recent-trade`

Интервалы свечей: `60` = 1h, `240` = 4h. Список kline приходит от новых к старым и сортируется. Числа парсятся в `decimal`, не в `double`. Время биржи — миллисекунды Unix UTC.

WebSocket: подписка `kline.{interval}.{symbol}`, ping `{"op":"ping"}` каждые 20 секунд, backoff с jitter, повторная подписка, отмена через `CancellationToken`. Стакан с разрывом sequence помечается неконсистентным и не используется, пока не будет загружен заново через REST.

Лимит REST — простой интервал между запросами, по умолчанию не чаще 10 запросов в секунду на клиент. Ошибка лимита не превращается в пустой успешный ответ.

Секреты и query с подписью не логируются. В 0.1 подпись приватных ордеров не реализуется.

## PostgreSQL

Имена таблиц в snake_case. Уникальность свечи: exchange, instrument, timeframe, open time. Уникальность заявки: account и client order id. Повторная загрузка свечи обновляет OHLC, не создаёт вторую строку.

Обязательные таблицы вертикального среза: `exchanges`, `trading_accounts`, `instruments`, `candles`, `orders`, `executions`, `positions`, `balances_snapshots`, `strategies`, `strategy_instances`, `strategy_configurations`, `trade_signals`, `risk_decisions`, `risk_events`, `backtest_runs`, `backtest_trades`, `backtest_metrics`, `paper_accounts`.

`orders.trading_mode` различает Paper и Backtest. Отдельные `paper_orders` не дублируют жизненный цикл. Таблицы `api_credentials_metadata`, `market_trades`, `funding_rates`, `order_book_snapshots`, `funding_payments`, `reconciliation_runs` создаются минимальной схемой без рабочего конвейера, если это не раздувает миграцию. Секреты в них не пишутся.

Миграции EF Core. При `Persistence:ApplyMigrationsOnStartup=true` Host применяет их на старте. Репозиторий свечей делает пакетный upsert.

## API

Анонимно: `GET /health/live`, `GET /health/ready`. Ready проверяет PostgreSQL.

С заголовком `X-Api-Key`:

| Метод | Путь |
|---|---|
| GET | `/api/v1/exchanges` |
| GET | `/api/v1/instruments` |
| GET | `/api/v1/market/candles` |
| POST | `/api/v1/market/candles/import` |
| POST | `/api/v1/market/candles/sync` |
| GET | `/api/v1/strategies` |
| POST | `/api/v1/strategies/{id}/instances` |
| POST | `/api/v1/strategy-instances/{id}/start` |
| POST | `/api/v1/strategy-instances/{id}/stop` |
| GET | `/api/v1/strategy-instances` |
| POST | `/api/v1/backtests` |
| GET | `/api/v1/backtests/{id}` |
| GET | `/api/v1/orders` |
| GET | `/api/v1/positions` |
| POST | `/api/v1/risk/emergency-stop` |

Импорт свечей нужен, чтобы backtest работал без сети. Sync ходит в Bybit и сохраняет историю. Остановка стратегии не закрывает позицию. Emergency stop блокирует новые сделки.

Ответы не содержат секретов. DTO лежат в `CryptoTrader.Contracts`.

## Поправка Lead после архитектурного каркаса

`BacktestRequest` содержит и `InstrumentId Instrument`, и `Instrument InstrumentSpec`. `PortfolioSnapshot` содержит `OpenQuantity`: значение больше нуля при открытой позиции и `null` без позиции. Стартовый бумажный капитал задаётся `Execution:PaperInitialEquity` и по умолчанию равен 10000. Подробности в ADR 010.

Регистрация в `Program.cs` вызывает ровно эти методы, каждый в своём проекте:

- `PersistenceServiceCollectionExtensions.AddPersistence(IServiceCollection, IConfiguration)`
- `BybitServiceCollectionExtensions.AddBybit(IServiceCollection, IConfiguration)`
- `PaperServiceCollectionExtensions.AddPaperExecution(IServiceCollection)`
- `TrendFollowingServiceCollectionExtensions.AddTrendFollowing(IServiceCollection)`
- `BacktestServiceCollectionExtensions.AddBacktesting(IServiceCollection)`
- `ApplicationServiceCollectionExtensions.AddApplication(IServiceCollection, IConfiguration)`

## Наблюдаемость

Serilog с маскированием полей, в имени которых есть key, secret, token, password, signature. Метрики: websocket connected, market data age, signals approved/rejected, paper equity. Трассировка ASP.NET Core и HttpClient через OpenTelemetry. Экспорт Prometheus на `/metrics` только внутри хоста.
