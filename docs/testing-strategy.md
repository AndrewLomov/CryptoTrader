# Стратегия тестирования CryptoTrader

Пирамида идёт снизу вверх: инварианты домена, контракт адаптера без сети, архитектурные правила, затем PostgreSQL в контейнере. Дымовой вызов живой биржи в этот набор не входит.

## Проекты

| Проект | Что проверяет | Статус в обычном прогоне |
|---|---|---|
| `tests/CryptoTrader.Domain.Tests` | Заявка, позиция, книга, округление, свеча, риск-профиль | Покрыто |
| `tests/CryptoTrader.Application.Tests` | `RiskManager`, граница walk-forward | Покрыто |
| `tests/CryptoTrader.Strategies.Tests` | EMA, RSI, ATR, `TrendFollowingStrategy` | Покрыто |
| `tests/CryptoTrader.Backtesting.Tests` | Повтор, стоп/тейк, комиссия, отсутствие look-ahead, отказ linear и contract volume | Покрыто |
| `tests/CryptoTrader.Architecture.Tests` | Граф проектов и `ExchangeRegistry` | Покрыто |
| `tests/CryptoTrader.Exchanges.ContractTests` | Bybit и OKX без сети; реестр независимо от порядка | Частично |
| `tests/CryptoTrader.IntegrationTests` | PostgreSQL через Testcontainers, метка `Category=Integration` | Покрыто, отдельно от модульного CI |

Обычный `dotnet test` без фильтра запускает всё, включая интеграционные тесты. Им нужен Docker. Подключение к базе разработчика не используется.

CI разделён:

- модульный job: `dotnet test CryptoTrader.slnx -c Release --filter "Category!=Integration"`;
- интеграционный job: `--filter "Category=Integration"`.

`Category!=Integration` не отсекает другие категории. Поэтому публичный smoke к бирже в репозиторий не добавляется. Если такой тест появится, он обязан иметь `[Trait("Category", "Network")]` и не должен попадать в фильтр CI. Ручной запуск, не CI:

```text
dotnet test CryptoTrader.slnx -c Release --filter "Category=Network"
```

Сейчас тестов с `Category=Network` нет. Это opt-in, не часть приёмки.

## Живая биржа

Тесты этого набора не открывают сокет к Bybit или OKX и не читают API-ключ.

- `BybitTradingClient.SubmitAsync` возвращает `LiveTradingDisabled`, `Accepted = false`, `UnknownState = false`.
- Разбор kline идёт через `HttpClient` на `http://127.0.0.1` и локальный JSON.
- Неполный часовой диапазон Bybit даёт `IncompleteCandleHistoryException`, а не успешный список. Плотный диапазон одной свечи остаётся `Complete`, decimal и UTC проверяются по-прежнему.
- OKX-тесты читают `tests/CryptoTrader.Exchanges.ContractTests/Fixtures/Okx/` и вызывают реальные `OkxMarketDataClient`, `OkxPublicRest`, `OkxTradingClient`, `OkxAccountClient`. WebSocket идёт через внутренний `IOkxSocketConnector` на `wss://127.0.0.1`. Ссылка на проект включается, когда csproj есть; сейчас он в дереве. `dotnet test` в этом проходе не запускался.
- Интеграционные тесты не создают биржевой клиент.

## API smoke

`src/CryptoTrader.Host/Program.cs` есть, в конце файла объявлен `public partial class Program`. Теста `WebApplicationFactory<Program>` в решении нет, пакет `Microsoft.AspNetCore.Mvc.Testing` не подключён. Статус: Не покрыто. Это не сетевой smoke биржи.

## Случаи

### Модульные тесты

| Случай | Где | Статус |
|---|---|---|
| Заявка не возвращается из `Filled` в `Submitted` | `OrderLifecycleTests` | Покрыто |
| Частичное исполнение и повторный `ExchangeTradeId` | `OrderLifecycleTests` | Покрыто |
| Исполнение больше остатка отклоняется | `OrderLifecycleTests` | Покрыто |
| Spot не продаёт больше, чем держит | `PositionTests` | Покрыто |
| Покупка уменьшает кэш; повторный trade id не меняет кэш | `PortfolioLedgerTests` | Покрыто |
| Размер к шагу, стоп, малое количество, нехватка кэша | `PositionSizerTests` | Покрыто |
| `FloorToStep(1.29, 0.1) = 1.2` | `DecimalRoundingTests` | Покрыто |
| Свеча с `high < low` и отрицательный объём | `CandleTests` | Покрыто |
| Пропуск открытия | `CandleTests` | Покрыто |
| `AllowFuturesLive` и `AllowCrossMargin` нельзя включить | `RiskProfileTests` | Покрыто |
| Дневной убыток, устаревшие данные, лимит позиций, аварийная остановка, выход без позиции | `RiskManagerTests` | Покрыто |
| EMA(3), RSI и ATR по Уайлдеру | `IndicatorTests` | Покрыто |
| Прогрев, `EnterLong`, отсутствие будущей свечи | `TrendFollowingStrategyTests` | Покрыто |
| Повтор backtest, стоп вместо тейка, комиссия, нет look-ahead | `BacktestEngineTests` | Покрыто |
| Linear и contract volume не идут как spot backtest | `BacktestEngineTests.Linear_instrument_or_contract_volume_candle_does_not_run_as_a_spot_backtest` | Покрыто |
| Граница walk-forward | `WalkForwardSplitterTests` | Покрыто |
| Граф проектов и центральные версии пакетов | `DependencyRuleTests` | Покрыто |
| Реестр: id, дубликат, неизвестный id, checkpoint, неподтверждённая история | `ExchangeRegistryTests` | Покрыто |
| Реестр не зависит от порядка регистрации | `ExchangeRegistryOrderTests` | Покрыто |

### Интеграционные тесты

| Случай | Где | Статус |
|---|---|---|
| Миграция на пустой базе | `DatabaseTests.Migration_applies_on_an_empty_database` | Покрыто |
| Повторная запись свечи обновляет одну строку | `DatabaseTests.Candle_upsert_keeps_one_row_and_updates_ohlc` | Покрыто |
| Повторный `ClientOrderId` отклоняется | `DatabaseTests.Duplicate_client_order_id_is_rejected` | Покрыто |
| Replay checkpoint после ошибки paper | нет теста | Не покрыто |
| Отказ PostgreSQL и восстановление из backup | нет теста | Не покрыто |

Метка каждого интеграционного теста: `[Trait("Category", "Integration")]`.

### Контракт биржи

| Случай | Где | Статус |
|---|---|---|
| Decimal и UTC для Bybit kline, диапазон одной свечи `Complete` | `BybitContractTests.Kline_numeric_strings_parse_to_decimal_and_utc` | Покрыто |
| Неполный диапазон Bybit не выглядит успехом | `BybitContractTests.Incomplete_kline_range_throws` | Покрыто |
| Bybit-заявка не уходит в сеть | `BybitContractTests.SubmitAsync_returns_live_trading_disabled_and_known_state` | Покрыто |
| OKX envelope, объём, confirm, пагинация, инструменты, запрет сети, subscribe и ping/pong | `OkxContractTests` | Частично: код есть и смотрит на текущий адаптер; прогон не запускался |
| Bybit reconnect, отрицательный ack, разрыв стакана | нет теста | Не покрыто |
| Неизвестный статус заявки, reconciliation, секрет в логах | нет теста | Не покрыто |
| Публичный smoke Bybit или OKX | нет теста | Opt-in, не CI |

Восстановление заявки, paper execution и отказ API-ключа в этом наборе не автоматизированы.
