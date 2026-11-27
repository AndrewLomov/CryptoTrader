# Схема PostgreSQL — CryptoTrader 0.1

Схема описана моделью EF Core `CryptoTrader.Infrastructure.Persistence.CryptoTraderDbContext`. Имена таблиц и колонок — snake_case. Миграции `Initial` и `MultiExchangeMarketData` лежат в `src/CryptoTrader.Infrastructure/Persistence/Migrations`. Вторая только добавляет колонки и таблицу `candle_consumer_checkpoints`.

Пароли, строки подключения и секреты биржи в этот документ не входят. Строка подключения приложения — `Persistence:ConnectionString`. Design-time фабрика читает переменную окружения `CRYPTO_TRADER_CONNECTION`.

## Соглашения

- `ExchangeId` и `InstrumentId` — `text`.
- Идентификаторы домена с типом `Guid` — `uuid`. У позиции нет отдельного id: первичный ключ `(trading_account_id, instrument_id)`.
- `Timeframe` хранится кодом (`1m`, `5m`, `15m`, `1h`, `4h`, `1d`) в `candles.timeframe`.
- Перечисления хранятся как `integer` со значениями домена, не как строки.
- Деньги, цены, количества и доли — `numeric` без урезанной шкалы `(18,2)`.
- Время — `timestamptz` (UTC). `paper_accounts.day_utc` — `date`.
- Коллекции, у которых нет своего запроса по элементам, — `jsonb`. Текст параметров стратегии и отчёта backtest — `text`, чтобы PostgreSQL не переписывал JSON.
- Репозитории возвращают доменные объекты через `Create` / `Rehydrate` и не отдают `IQueryable`.
- Запись репозитория сама вызывает `SaveChanges`. `IUnitOfWork.SaveChangesAsync` сохраняет тот же scoped `DbContext`.
- Репозиторий не открывает сеть. Транзакция свечей охватывает только чтение и запись строк.

Целые значения перечислений:

| Перечисление | Значения |
|---|---|
| `InstrumentType` | Spot = 1, LinearPerpetual = 2 |
| `TradingMode` | Backtest = 1, Paper = 2, Testnet = 3, Live = 4 |
| `AccountConnectionStatus` | Connected = 1, Degraded = 2, Disconnected = 3, ConfigurationError = 4 |
| `OrderSide` | Buy = 1, Sell = 2 |
| `OrderType` | Market = 1, Limit = 2 |
| `OrderStatus` | PendingSubmit = 1, Submitted = 2, Accepted = 3, PartiallyFilled = 4, Filled = 5, CancelPending = 6, Canceled = 7, Rejected = 8, Unknown = 9 |
| `PositionSide` | Flat = 0, Long = 1, Short = 2 |
| `StrategyInstanceStatus` | Created = 1, Running = 2, Stopped = 3 |
| `SignalAction` | EnterLong = 1, ExitLong = 2 |
| `BacktestExitReason` | StopLoss = 1, TakeProfit = 2, TrendBreak = 3, EndOfData = 4 |
| `VolumeUnit` | BaseAsset = 1, QuoteAsset = 2, Contract = 3 |
| `CandleConsumerState` | Pending = 1, Processed = 2 |

Live в 0.1 домен не создаёт. Колонка `orders.trading_mode` всё равно есть: Paper и Backtest пишутся в одну таблицу `orders`.

## Уникальность и upsert свечей

```sql
CREATE UNIQUE INDEX ux_candles_identity
ON candles (exchange_id, instrument_id, timeframe, open_time_utc);

CREATE UNIQUE INDEX ux_orders_account_client_order_id
ON orders (trading_account_id, client_order_id);
```

Повторная загрузка свечи с тем же ключом не создаёт вторую строку. Ключ включает `exchange_id`, поэтому свечи разных бирж не смешиваются. Индекс `ux_candles_identity` остаётся уникальным ключом `(exchange_id, instrument_id, timeframe, open_time_utc)`. `ICandleRepository.UpsertRangeAsync` в одной транзакции находит существующие строки и обновляет `open`, `high`, `low`, `close`, `volume`, `volume_unit`, `quote_volume`, `base_volume`, `close_time_utc` и `is_closed`, затем один раз вызывает `SaveChanges`. Объём, единица объёма и время закрытия обновляются вместе с OHLC. Идентичность и суррогатный `id` не меняются.

Гонка двух вставок одного ключа упирается в `ux_candles_identity`. Повторный вызов после этого находит строку и обновляет её. Транзакция не держится вокруг HTTP.

`GetRangeAsync` возвращает свечи с `open_time_utc` от `fromUtc` до `toUtc` включительно, по возрастанию времени открытия.

Другие уникальные ключи:

| Индекс или ключ | Колонки |
|---|---|
| `pk_positions` | `trading_account_id`, `instrument_id` |
| `ux_executions_order_trade` | `order_id`, `exchange_trade_id` |
| `pk_funding_rates` | `exchange_id`, `instrument_id`, `funding_time_utc` |
| `ux_market_trades_identity` | `exchange_id`, `instrument_id`, `exchange_trade_id` |
| `pk_strategies` | `strategy_id`, `version` |
| `pk_trade_signals` / `pk_risk_decisions` | `signal_id` |
| `ux_backtest_trades_run_number` | `run_id`, `trade_number` |
| `pk_paper_accounts` | `account_id` |
| `pk_candle_consumer_checkpoints` | `exchange_id`, `instrument_id`, `timeframe`, `open_time_utc`, `consumer` |

Индекс сигналов: `ix_trade_signals_strategy_instrument_time` по `(strategy_id, instrument_id, occurred_at_utc)`. У временных рядов `market_trades`, `funding_rates`, `order_book_snapshots` есть индекс по инструменту и времени. Диапазон свечей обслуживает `ux_candles_identity`.

## Таблицы вертикального среза

### exchanges

`id text` PK. Строка появляется при сохранении торгового счёта, инструмента или paper-состояния. Отдельного справочного конвейера нет.

### trading_accounts

`id uuid` PK, `exchange_id`, `display_name`, `mode`, `status`, `created_at_utc`, `updated_at_utc`.

### instruments

`id text` PK, `exchange_id`, `exchange_symbol`, `base_asset`, `quote_asset`, `instrument_type`, `price_tick`, `quantity_step`, `min_quantity`, `min_notional`, `contract_multiplier`, `contract_value`, `contract_value_currency`, `settlement_currency`, `contract_kind`, `is_trading_enabled`.

`contract_value` (`numeric`), `contract_value_currency`, `settlement_currency` и `contract_kind` допускают `null`. Уже сохранённые строки Bybit остаются с `null` в этих колонках и по-прежнему читаются. Отсутствующее контрактное значение не записывается как 1.

Символ биржи не уникален: spot и linear могут называться одинаково. `FindByExchangeSymbolAsync` возвращает одну строку, сначала с меньшим `instrument_type`, затем по `id`. Индекс `ix_instruments_exchange_symbol` не уникальный.

### candles

`id bigint` identity PK, `exchange_id`, `instrument_id`, `timeframe`, `open_time_utc`, `close_time_utc`, `open`, `high`, `low`, `close`, `volume`, `volume_unit`, `quote_volume`, `base_volume`, `is_closed`. Уникальность — `ux_candles_identity` по `(exchange_id, instrument_id, timeframe, open_time_utc)`.

`volume_unit integer not null default 1` — это `VolumeUnit.BaseAsset`. Существующие свечи получают 1. `quote_volume` и `base_volume` — nullable `numeric`. Пустой дополнительный объём остаётся `null` и не копируется из `volume`.

### candle_consumer_checkpoints

Первичный ключ `(exchange_id, instrument_id, timeframe, open_time_utc, consumer)`. Колонки: `state integer` (`CandleConsumerState`: Pending = 1, Processed = 2, Skipped = 3), `skip_reason varchar(512)` и `updated_at_utc`.

Строку пишет `MarkPendingAsync`, `MarkProcessedAsync` или `MarkSkippedAsync`. Уже завершённое состояние (Processed или Skipped) повторный `MarkPendingAsync` не открывает заново. Исторические свечи сами в Pending не ставятся. `ListPendingCandlesAsync` соединяет чекпоинт со `candles` по бирже, инструменту, таймфрейму и времени открытия, отдаёт доменные свечи со `state = Pending`, по возрастанию `open_time_utc`, не больше `limit`.

Внешнего ключа на `candles` нет. Удаление чекпоинта не удаляет свечу, каскада на свечи нет. Соединение включает `exchange_id`, биржи не смешиваются.

### orders

`id uuid` PK, `client_order_id varchar(64)`, `trading_account_id`, `exchange_id`, `instrument_id`, `instrument_type`, `side`, `order_type`, `quantity`, `limit_price`, `reduce_only`, `trading_mode`, `status`, `filled_quantity`, `filled_notional`, `applied_exchange_trade_ids jsonb`, `exchange_order_id`, `reject_reason`, `created_at_utc`, `updated_at_utc`.

`filled_notional` нужен `Order.Rehydrate`. У агрегата нет публичного свойства для этого поля, поэтому репозиторий читает его при записи и восстанавливает заявку через `Rehydrate`. Средняя цена из частного не хранится отдельно. `applied_exchange_trade_ids` — массив строк для идемпотентности fill. `UpdateAsync` обновляет строку и не удаляет её.

Отдельных `paper_orders` нет.

### executions

`id uuid` PK, `order_id`, `instrument_id`, `side`, `quantity`, `price`, `fee`, `fee_asset`, `time_utc`, `exchange_trade_id`. Уникальность пары заявка и биржевой trade id — `ux_executions_order_trade`.

В 0.1 нет порта записи исполнений: у `Order` после fill нет комиссии отдельной сделкой, её нельзя восстановить из заявки. Таблица есть для аудиторской истории. Идентификаторы уже применённых сделок лежат на строке заявки.

### positions

PK `(trading_account_id, instrument_id)`. Колонки: `exchange_id`, `instrument_type`, `side`, `quantity`, `average_price`, `realized_pnl`, `fees`, `funding`, `fee_asset`, `leverage`, `margin`, `contract_multiplier`, `opened_at_utc`, `updated_at_utc`.

`UpsertAsync` пишет и плоскую позицию. `FindOpenAsync` возвращает `null`, если строки нет, сторона `Flat` или количество 0. `ListAsync` возвращает все строки счёта, включая закрытые.

### balances_snapshots

`id uuid` PK, `trading_account_id`, `asset`, `total`, `available`, `captured_at_utc`. Записи в 0.1 не пишутся. Снимок paper-капитала лежит в `paper_accounts`.

### strategies

PK `(strategy_id, version)`, `display_name` nullable. Строка без отображаемого имени создаётся при `Add`/`Update` экземпляра стратегии.

### strategy_instances

`id uuid` PK, `strategy_id`, `strategy_version`, `instrument_id`, `parameters_json text`, `status`, `created_at_utc`, `updated_at_utc`.

### strategy_configurations

`id uuid` PK, `strategy_id`, `strategy_version`, `parameters_json text`, `created_at_utc`. Именованные наборы параметров в 0.1 не пишутся: текущие параметры живут на экземпляре.

### trade_signals

PK `signal_id uuid`, `strategy_instance_id`, `strategy_id`, `strategy_version`, `instrument_id`, `action`, `occurred_at_utc`, `reference_price`, `stop_loss_price`, `take_profit_price`, `atr`, `explanation`.

### risk_decisions

PK `signal_id` — одно решение на сигнал. `strategy_instance_id`, `approved`, `approved_quantity`, `estimated_max_loss`, `applied_constraints jsonb`, `rejection_code`, `reason`, `occurred_at_utc`.

У `RiskDecision` нет своего времени. `occurred_at_utc` копируется из сигнала. `AddAsync` пишет сигнал и решение одним `SaveChanges`.

### risk_events

`id uuid` PK равен `DomainEvent.EventId`. `event_type` — полное CLR-имя типа. `occurred_at_utc`, `correlation_id`, `causation_id`, `payload jsonb`.

Репозиторий только вставляет. Повтор того же `EventId` нарушает первичный ключ и не затирает прежнюю строку. Обновление заявки, позиции или счёта не удаляет события: внешнего ключа на них нет.

### backtest_runs

`run_id uuid` PK, `strategy_id`, `strategy_version`, `engine_version`, `parameters_json text`, `from_utc`, `to_utc`, `assumptions_engine_version`, `funding_modeled`, `partial_fills_modeled`, `fill_price_model`, `stop_and_take_profit_conflict`, `assumptions_notes`, `equity_curve jsonb`, `created_at_utc`.

### backtest_metrics

PK и FK `run_id` → `backtest_runs`. Колонки метрик отчёта: `initial_equity`, `final_equity`, `net_profit`, `roi_fraction`, `max_drawdown_fraction`, `sharpe`, `sortino`, `profit_factor`, `win_rate_fraction`, `expectancy`, `trade_count`, `total_fees`, `total_slippage`, `average_holding_ticks`.

`average_holding_ticks` — `bigint`, точное `TimeSpan.Ticks`. Sharpe, Sortino и profit factor допускают `null`.

### backtest_trades

`id bigint` identity, `run_id` FK, `trade_number`, `instrument_id`, `entry_time_utc`, `exit_time_utc`, `entry_price`, `exit_price`, `quantity`, `fees`, `slippage_cost`, `net_pnl`, `exit_reason`.

`FindAsync` собирает `BacktestRunRecord`: метрики из колонок, сделки из `backtest_trades`, допущения из колонок прогона, кривую капитала из `equity_curve`. Удаление прогона каскадом удаляет метрики и сделки. События риска так не удаляются.

### paper_accounts

Одна строка на пару `(exchange_id, quote_asset)`. Уникальный индекс `ux_paper_accounts_exchange_quote`. Колонки для запросов: `account_id`, `exchange_id`, `quote_asset`, `cash`, `peak_equity`, `day_start_equity`, `last_equity`, `day_utc`, `emergency_stop`, `updated_at_utc`.

`jsonb`: `positions`, `marks`, `applied_exchange_trade_ids`, `applied_candle_keys` (по умолчанию `[]`). `SaveAsync` обновляет эту строку. `LoadAsync` возвращает `PaperState`. Если в базе уже есть старый счёт с той же парой, координатор продолжает его, а не создаёт второй. Две строки с одной парой миграция индекса не примет.

## Таблицы без конвейера

Схема есть, записи из приложения 0.1 нет.

| Таблица | Колонки |
|---|---|
| `api_credentials_metadata` | `id`, `trading_account_id`, `exchange_id`, `key_hint`, `permissions`. Секрета, токена и API-ключа нет |
| `market_trades` | `id`, `exchange_id`, `instrument_id`, `exchange_trade_id`, `price`, `quantity`, `side`, `time_utc` |
| `funding_rates` | `exchange_id`, `instrument_id`, `funding_time_utc`, `rate`, `received_at_utc` |
| `order_book_snapshots` | `id`, `exchange_id`, `instrument_id`, `sequence`, `is_consistent`, `exchange_time_utc`, `received_at_utc`, `bids jsonb`, `asks jsonb` |
| `funding_payments` | `id`, `trading_account_id`, `exchange_id`, `instrument_id`, `amount`, `funding_time_utc` |
| `reconciliation_runs` | `id`, `trading_account_id`, `started_at_utc`, `finished_at_utc`, `status`, `summary` |

Внешних ключей между агрегатами нет, кроме состава backtest. Свечи и заявки можно сохранить без заранее вставленной строки `exchanges`.

## Миграции и проверка

`DatabaseMigrator.ApplyAsync` вызывает `MigrateAsync`. Репозитории миграцию не запускают.

`MultiExchangeMarketData` добавляет колонки `instruments` и `candles` и таблицу `candle_consumer_checkpoints`. Колонки не удаляются, миграция `Initial` не переписывается. Уже загруженные свечи и инструменты остаются на месте: `volume_unit` у старых свечей становится 1, контрактные поля старых инструментов остаются `null`.

`20261127123818_IsolatePaperAccounts` добавляет `paper_accounts.applied_candle_keys`, `candle_consumer_checkpoints.skip_reason` и уникальный индекс `(exchange_id, quote_asset)`. Строки не удаляются. Обновление с `Initial` сохраняет уже записанные свечи Bybit.

`Persistence:ApplyMigrationsOnStartup=true` включает hosted service. Он применяет миграции в `StartingAsync`, пишет в лог и пробрасывает исключение дальше, без строки подключения в сообщении. При `false` сервис ничего не делает.

Проверка готовности — health check с именем `database` (`AddDbContextCheck<CryptoTraderDbContext>`).

История миграций — служебная таблица EF Core `__EFMigrationsHistory`. Её нужно включать в резервную копию вместе с данными.

## Резервное копирование

Порядок, расписание и проверка восстановления принадлежат DevOps. Короткая отсылка: [deploy/postgres/backup.md](../deploy/postgres/backup.md).

Этот порядок не меняется. Полная копия базы забирает новые колонки и `candle_consumer_checkpoints` вместе с остальными данными. Отдельный сценарий резервного копирования не нужен, документ `deploy/postgres/backup.md` остаётся в силе. Миграция данные не уничтожает.
