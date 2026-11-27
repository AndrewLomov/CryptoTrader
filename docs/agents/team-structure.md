# Команда CryptoTrader 0.1

Lead Agent — архитектор интеграции и технический руководитель. Он утверждает контракты, распределяет файлы и принимает итоговый результат.

Субагенты запускаются через механизм Task платформы Cursor. Это отдельные исполнители, а не ролевая имитация в одном контексте. Изолированные git worktree не используются: параллельная работа идёт в одном рабочем дереве с единственным владельцем каждого файла. Общие контракты после утверждения не меняются исполнителем модуля.

## Состав

| Agent | Роль | Владеет файлами |
|---|---|---|
| Senior .NET Architect | Архитектура, Domain, порты, ADR | `src/CryptoTrader.Domain/**`, `src/CryptoTrader.Contracts/**`, `src/CryptoTrader.Exchanges.Abstractions/**`, `src/CryptoTrader.Strategies.Abstractions/**`, `src/CryptoTrader.Application/Abstractions/**`, `src/CryptoTrader.Application/Configuration/**`, `tests/CryptoTrader.Architecture.Tests/**`, `*.csproj`, `CryptoTrader.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`, `.gitignore`, `docs/architecture.md`, `docs/adr/**` |
| Senior C# Developer | Application, Risk Manager, API, composition root | `src/CryptoTrader.Application/**` кроме `Abstractions` и `Configuration`, `src/CryptoTrader.Host/**`, `docs/api.md` |
| Quantitative Developer | Индикаторы, стратегия, backtesting | `src/CryptoTrader.Strategies.TrendFollowing/**`, `src/CryptoTrader.Backtesting/**`, `docs/trading-strategy.md` |
| Algorithmic Trading Systems Engineer | Bybit и Paper Execution | `src/CryptoTrader.Exchanges.Bybit/**`, `src/CryptoTrader.Infrastructure/Paper/**`, `src/CryptoTrader.Infrastructure/MarketData/**`, `docs/exchange-integration.md` |
| Database Engineer | PostgreSQL, EF Core, миграции | `src/CryptoTrader.Infrastructure/Persistence/**`, `docs/database-schema.md`, `deploy/postgres/**` |
| QA Automation Engineer | Тесты и приёмка | `tests/**` кроме `CryptoTrader.Architecture.Tests`, `docs/testing-strategy.md`, `docs/agents/review-reports/**` |
| DevOps Engineer | Сборка, контейнеры, эксплуатация | `deploy/Dockerfile`, `deploy/docker-compose.yml`, `deploy/.env.example`, `deploy/scripts/**`, `.github/**`, `docs/deployment.md`, `docs/operations-runbook.md`, `docs/security.md` |

`src/CryptoTrader.Infrastructure/DependencyInjection.cs` собирает Lead после появления модулей Persistence и Paper, если владельцы не предоставят собственные методы расширения `AddPersistence` и `AddPaperExecution`.

## Уже принятые решения Lead

Они обязательны для всех агентов и зафиксированы в `docs/agents/integration-contracts.md` и ADR.

1. Modular monolith и Clean Architecture. CQRS и Mediator не внедряются.
2. Live Trading в 0.1 недоступен. `Execution:LiveTradingEnabled=true` или `Risk:AllowFuturesLive=true` останавливают запуск.
3. Bybit подключается собственным REST/WebSocket-клиентом на публичных методах V5. SDK биржи не подключается, пока не понадобится приватная торговля.
4. Bybit-клиент заявок не выполняет сетевой вызов, пока Live выключен.
5. Финансовая арифметика — `decimal`. Время — `DateTimeOffset` UTC. Проценты в конфигурации задаются как `0.5` = 0,5%.
6. Backtest и Paper используют доменный `PortfolioLedger` и один `IRiskManager`.
7. В одной свече одновременное касание Stop Loss и Take Profit закрывается по Stop Loss.
8. Сигнал строится только по закрытым свечам. Вход в backtest — открытие следующей свечи с неблагоприятным проскальзыванием.
9. Готовность приложения (`/health/ready`) зависит от PostgreSQL. Недоступность биржи блокирует новые сделки и не валит весь процесс.
10. Управляющие и читающие `/api/*` требуют заголовок `X-Api-Key`. Health-эндпоинты публичные.
