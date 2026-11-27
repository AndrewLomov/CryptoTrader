# Архитектура CryptoTrader 0.1

## Контекст

CryptoTrader 0.1 — модульный монолит для личного контура: публичные данные Bybit и OKX, воспроизводимый backtest и paper trading spot. Домен общий для spot и linear perpetual. Реальные заявки в этой версии не отправляются. Стратегия не видит биржу, EF Core, сеть и файловую конфигурацию.

Поток операции:

```text
Market Data -> Strategy -> TradeSignal -> Risk Manager
    -> ApprovedOrder -> Execution Venue -> Order / Execution
    -> PortfolioLedger -> Persistence
```

## Граф проектов

| Проект | Ссылки |
|---|---|
| CryptoTrader.Domain | нет |
| CryptoTrader.Contracts | нет |
| CryptoTrader.Strategies.Abstractions | Domain |
| CryptoTrader.Exchanges.Abstractions | Domain |
| CryptoTrader.Application | Domain, Strategies.Abstractions, Exchanges.Abstractions |
| CryptoTrader.Infrastructure | Domain, Application |
| CryptoTrader.Exchanges.Bybit | Exchanges.Abstractions |
| CryptoTrader.Exchanges.Okx | Exchanges.Abstractions |
| CryptoTrader.Strategies.TrendFollowing | Domain, Strategies.Abstractions |
| CryptoTrader.Backtesting | Domain, Application, Strategies.Abstractions |
| CryptoTrader.Host | Application, Infrastructure, Contracts, Exchanges.Bybit, Exchanges.Okx, Strategies.TrendFollowing, Backtesting |

`Program.cs` вызывает `AddBybit`, `AddOkx` и собирает `ExchangeRegistry`.

Пакеты и их версии заданы в `Directory.Packages.props`. В `PackageReference` версия не указывается. Целевой фреймворк — `net10.0`, SDK — `10.0.403`.

## Направление зависимостей

Зависимости смотрят внутрь. Domain не ссылается на проекты решения, EF Core, ASP.NET Core, Npgsql и SDK биржи. Контракты HTTP не ссылаются на домен: в них только примитивы. Application определяет порты. Infrastructure, Bybit, TrendFollowing и Backtesting их реализуют. Host — composition root.

Вложенные пространства домена: `CryptoTrader.Domain.Common`, `Instruments`, `MarketData`, `Trading`, `Risk`, `Strategies`. Порты приложения: `CryptoTrader.Application.Abstractions` с вложениями `Persistence`, `Risk`, `Execution`, `Backtesting`, `Paper`, `MarketData`. Настройки: `CryptoTrader.Application.Configuration`. Алгоритм границы выборки: `CryptoTrader.Application.Backtesting.WalkForwardSplitter`.

## Границы модулей

- Domain хранит инварианты заявки, позиции, книги, риска и сигнала. События копятся на агрегате (`IHasDomainEvents`) и не публикуются шиной.
- Strategies.Abstractions получает уже собранный `StrategyContext` и возвращает `TradeSignal`.
- Exchanges.Abstractions описывает адаптер, реестр и возможности. Модели адаптера — не JSON биржи. `SubscribeAsync` отдаёт `IAsyncEnumerable<MarketEvent>`.
- Application содержит порты, проверку конфигурации, риск-менеджер и сценарии администрирования свечей, стратегий, backtest и paper. HTTP-эндпоинты остаются в Host.
- Infrastructure владеет EF Core и paper/market-data реализацией. Репозитории не возвращают `IQueryable`.
- Contracts — запросы и ответы `/health/*` и `/api/v1/*` без логики.

`AddApplication` читает опции через `ApplicationOptionsReader` и вызывает `StartupConfigurationValidator.Validate` до регистрации `IOptions<T>`. Последний аргумент — `OkxOptions`. Регистрация `IValidateOptions<T>` в контейнере сама по себе процесс не останавливает.

## Путь backtest

1. Свечи читаются из хранилища или из тела запроса импорта. Сеть для самого прогона не нужна.
2. `WalkForwardSplitter` при необходимости делит диапазон. Оптимизатора нет.
3. `IBacktestEngine.RunAsync` идёт по закрытым рабочим свечам. Старшие свечи видны только если уже закрыты к моменту бара.
4. Стратегия возвращает сигнал. `IRiskManager` применяет профиль. Размер long spot считает `PositionSizer`.
5. Вход — следующее открытие с неблагоприятным проскальзыванием. В одном баре stop и take profit закрываются по stop.
6. Исполнение проводится через `PortfolioLedger.ApplySpotFill`. Метрики и допущения попадают в `BacktestReport` версии движка `0.1.0`. Funding не моделируется.
7. `IBacktestRunRepository` хранит `BacktestRunRecord`.

## Путь paper

1. Публичный поток Bybit или OKX нормализуется в `MarketEvent`. Пропуск свечи обнаруживает `CandleGapDetector`, данные не интерполируются.
2. `CandleProcessingDecision` решает, можно ли обновить paper. Закрытая свеча старше `StaleAfterSeconds` и свеча с очередью старше этого окна пишутся в историю и не меняют капитал, PnL, mark, сигнал, заявку или позицию. Граница «возраст равен окну» ещё допустима.
3. Счёт выбирается по `(ExchangeId, QuoteAsset)`. Bybit USDT и OKX USDT — разные `PortfolioLedger`. Конвертации котировок нет. Глобальный аварийный стоп лежит в `paper_trading_control`: без биржи и котировки он останавливает все текущие и будущие paper-счета. Явная пара останавливает только её. Снятие глобального стопа — отдельный вызов.
4. Допуск — spot и `VolumeUnit.BaseAsset`. Фьючерс и объём в контрактах отсекаются до изменения книги.
5. Неконсистентный стакан (`IsConsistent == false`) для торговли не используется. `IsConsistent == true` у OKX означает только успешный разбор снимка.
6. Запущенный `StrategyInstance` получает допустимую закрытую свечу своего `InstrumentId`. `Stop` запрещает новые сигналы и не закрывает позицию.
7. Тот же риск-менеджер строит `ApprovedOrder`. Paper venue покупает по last или close последней закрытой свечи с тем же неблагоприятным проскальзыванием и не создаёт реальную заявку. Идентификатор заявки и сделки детерминирован, повтор после сбоя не удваивает fill.
8. Заявка пишется в `orders` с `TradingMode.Paper`. Книга сохраняется как `PaperState` вместе с ключом применённой свечи. Закрытая свеча и checkpoint `Pending` коммитятся одной транзакцией. Повтор pending выполняется при старте и далее в том же процессе.
9. `ReconcileAsync` сверяет книгу с сохранёнными paper-позициями и не ходит на биржу.

## Безопасность по умолчанию

- `Execution:LiveTradingEnabled = false`, `Execution:Mode` не может быть `Live`.
- `Risk:AllowFuturesLive = false`, `Risk:AllowCrossMargin = false`.
- Пустой `Api:AdminApiKey` не даёт процессу стартовать.
- `/health/live` и `/health/ready` публичные. `/api/v1/*` требует `X-Api-Key`.
- `/metrics` не размещается под `/api`.
- Секреты не входят в домен, контракты и логи. Ключ задаётся через окружение или user secrets, не через файл в репозитории.

## Реестр и возможности

`ExchangeRegistry` хранит `IExchangeAdapter` в словаре по `ExchangeId`. Повтор одного идентификатора даёт `InvalidOperationException` (`Exchange '{id}' is registered more than once.`). Неизвестная биржа — `Exchange '{id}' is not registered.` Порядок DI на поиск не влияет. `ExchangeId` адаптера равен `new ExchangeId(ExchangeCode)`: `BYBIT` и `OKX`.

`OrderBook` и `FundingRates` по умолчанию `null`. `SupportsLinearPerpetual` означает публичные linear-данные. `SupportsFuturesAccounting` по умолчанию `false` и не включает учёт фьючерсов. `SupportsLiveOrders` и `SupportsPrivateAccount` остаются `false`. Live-заявки выключены.

`GetCandleHistoryAsync` по умолчанию вызывает `GetCandlesAsync` и возвращает `Unverified`, либо `Empty`, если свечей нет. Адаптер при этом не сообщает, полный ли запрошенный диапазон. Незакрытая свеча не входит в backtest и во вход стратегии. `ICandleConsumerCheckpoint` повторяет только строки, которые этот конвейер пометил pending. Имя paper-консьюмера — `paper`.

`MarketData:Subscriptions` задаёт биржу `BYBIT` или `OKX`, инструмент и доменные коды `1m`, `5m`, `15m`, `1h`, `4h`, `1d`. Пустой список допустим: тогда ingestion может вернуться к `Bybit:Symbols` на `1h`, если Bybit включён. Дубликат после обрезки пробелов, пустое поле, неизвестный таймфрейм и подписка на выключенную биржу останавливают старт. `Bybit:Enabled` по умолчанию `true`, `Okx:Enabled` — `false`. Секция `Okx` проверяется при старте и не содержит секретов. База REST по заметкам OKX — `https://openapi.okx.com`. Публичный WebSocket — `wss://ws.okx.com/ws/v5/public`. Порт `8443` в этих URL не используется. Каналы свечей OKX лежат на `wss://ws.okx.com/ws/v5/business`. Доменный `1d` — сутки UTC; на OKX это бар `1Dutc`, не `1D` (UTC+8).

Host собирает реестр после `AddBybit` и `AddOkx`: `new ExchangeRegistry(sp.GetServices<IExchangeAdapter>())`. `CandleIngestionService` читает `MarketData:Subscriptions`. Пустой список при включённом Bybit даёт `Bybit:Symbols` на `1h`.

## Статус контура

| Область | Статус |
|---|---|
| Домен, риск-менеджер, книга spot, paper venue | Implemented |
| Публичные данные Bybit | Implemented |
| EF Core, миграции, репозитории | Implemented |
| Host, `Program.cs`, health и `/api/v1/*` | Implemented |
| Индикаторы, Trend Following, backtest spot | Implemented |
| Docker Compose и CI | Implemented |
| Реестр, возможности, порт стакана, полнота истории свечей | Implemented |
| Объём свечи и спецификация linear-контракта в домене | Implemented |
| Проверка `OkxOptions` и `MarketData:Subscriptions` | Implemented |
| Порт checkpoint потребителя свечей | Implemented |
| Ingestion по списку подписок и сборка реестра в Host | Implemented |
| Публичный адаптер OKX | Implemented |
| Paper spot Bybit и OKX на раздельных счетах | Implemented |
| Приватная торговля OKX и live-заявки | Not Implemented |
| OKX Demo Trading | Not Implemented |
| Учёт фьючерсов | Not Implemented |
| Инкрементальный стакан | Not Implemented |
| Колонки объёма, контракта и таблица checkpoint | Implemented |
| Kafka, RabbitMQ, микросервисы, Mediator, CQRS | Not Implemented |

Публичный OKX — REST и WebSocket market data. Приватный REST, приватный WebSocket, вывод средств и live-заявки отсутствуют. Книга 0.1 проводит spot. Линейный контракт хранит `ctVal` и валюты и не включает futures PnL. Миграция `20261127104212_MultiExchangeMarketData` добавляет колонки и `candle_consumer_checkpoints`, не удаляя историю Bybit. Стакан OKX в этом инкременте — снимок (`books` и `books5`), не инкрементальный поток.
