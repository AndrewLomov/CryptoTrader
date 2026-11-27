# Архитектура CryptoTrader 0.1

## Контекст

CryptoTrader 0.1 — модульный монолит для личного контура: публичные данные Bybit, воспроизводимый backtest и paper trading. Домен общий для spot и linear perpetual. Реальные заявки в этой версии не отправляются. Стратегия не видит биржу, EF Core, сеть и файловую конфигурацию.

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
| CryptoTrader.Strategies.TrendFollowing | Domain, Strategies.Abstractions |
| CryptoTrader.Backtesting | Domain, Application, Strategies.Abstractions |
| CryptoTrader.Host | Application, Infrastructure, Contracts, Exchanges.Bybit, Strategies.TrendFollowing, Backtesting |

Пакеты и их версии заданы в `Directory.Packages.props`. В `PackageReference` версия не указывается. Целевой фреймворк — `net10.0`, SDK — `10.0.403`.

## Направление зависимостей

Зависимости смотрят внутрь. Domain не ссылается на проекты решения, EF Core, ASP.NET Core, Npgsql и SDK биржи. Контракты HTTP не ссылаются на домен: в них только примитивы. Application определяет порты. Infrastructure, Bybit, TrendFollowing и Backtesting их реализуют. Host — composition root.

Вложенные пространства домена: `CryptoTrader.Domain.Common`, `Instruments`, `MarketData`, `Trading`, `Risk`, `Strategies`. Порты приложения: `CryptoTrader.Application.Abstractions` с вложениями `Persistence`, `Risk`, `Execution`, `Backtesting`, `Paper`. Настройки: `CryptoTrader.Application.Configuration`. Алгоритм границы выборки: `CryptoTrader.Application.Backtesting.WalkForwardSplitter`.

## Границы модулей

- Domain хранит инварианты заявки, позиции, книги, риска и сигнала. События копятся на агрегате (`IHasDomainEvents`) и не публикуются шиной.
- Strategies.Abstractions получает уже собранный `StrategyContext` и возвращает `TradeSignal`.
- Exchanges.Abstractions описывает адаптер. Модели адаптера — не JSON Bybit. `SubscribeAsync` отдаёт `IAsyncEnumerable<MarketEvent>`.
- Application не содержит сценариев, риск-менеджера и эндпоинтов: только порты, записи backtest/paper и проверку конфигурации.
- Infrastructure владеет EF Core и paper/market-data реализацией. Репозитории не возвращают `IQueryable`.
- Contracts — запросы и ответы `/health/*` и `/api/v1/*` без логики.

`StartupConfigurationValidator.Validate` вызывается из Host после связывания опций. Регистрация `IValidateOptions<T>` в контейнере сама по себе процесс не останавливает.

## Путь backtest

1. Свечи читаются из хранилища или из тела запроса импорта. Сеть для самого прогона не нужна.
2. `WalkForwardSplitter` при необходимости делит диапазон. Оптимизатора нет.
3. `IBacktestEngine.RunAsync` идёт по закрытым рабочим свечам. Старшие свечи видны только если уже закрыты к моменту бара.
4. Стратегия возвращает сигнал. `IRiskManager` применяет профиль. Размер long spot считает `PositionSizer`.
5. Вход — следующее открытие с неблагоприятным проскальзыванием. В одном баре stop и take profit закрываются по stop.
6. Исполнение проводится через `PortfolioLedger.ApplySpotFill`. Метрики и допущения попадают в `BacktestReport` версии движка `0.1.0`. Funding не моделируется.
7. `IBacktestRunRepository` хранит `BacktestRunRecord`.

## Путь paper

1. Публичный поток Bybit нормализуется в `MarketEvent`. Пропуск свечи обнаруживает `CandleGapDetector`, данные не интерполируются.
2. Неконсистентный стакан (`IsConsistent == false`) для торговли не используется.
3. Запущенный `StrategyInstance` получает закрытые свечи. `Stop` запрещает новые сигналы и не закрывает позицию.
4. Тот же риск-менеджер строит `ApprovedOrder`. Paper venue покупает по last или close последней закрытой свечи с тем же неблагоприятным проскальзыванием и не создаёт реальную заявку.
5. Заявка и исполнение пишутся в общие `orders` / `executions` с `TradingMode.Paper`. Книга сохраняется как `PaperState`.
6. `ReconcileAsync` сверяет книгу с сохранёнными paper-позициями и не ходит на биржу.

## Безопасность по умолчанию

- `Execution:LiveTradingEnabled = false`, `Execution:Mode` не может быть `Live`.
- `Risk:AllowFuturesLive = false`, `Risk:AllowCrossMargin = false`.
- Пустой `Api:AdminApiKey` не даёт процессу стартовать.
- `/health/live` и `/health/ready` публичные. `/api/v1/*` требует `X-Api-Key`.
- `/metrics` не размещается под `/api`.
- Секреты не входят в домен, контракты и логи. Ключ задаётся через окружение или user secrets, не через файл в репозитории.

## Намеренно отсутствует

Реализация Bybit, EF-сущности и миграции, индикаторы, Trend Following, движок backtest, риск-менеджер, paper venue, эндпоинты, `Program.cs`, Docker и CI. Нет live-заявок, подписи приватных методов, CQRS, Mediator, Kafka, RabbitMQ и отдельного агрегата paper-заявок. Линейный контракт описан в позиции, но книга 0.1 проводит только spot, а live futures запрещён.
