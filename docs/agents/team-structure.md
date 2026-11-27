# Команда CryptoTrader — multi-exchange и OKX

Lead Agent утверждает контракты и интегрирует результат. Субагенты запущены через механизм Task. Общее рабочее дерево одно: изолированные worktree не использовались, потому что параллельные роли делят репозиторий, а не конкурируют за один и тот же результат. У каждого файла один владелец.

Нормативный контракт этой итерации: `docs/agents/multi-exchange-contract.md`. Решения версии 0.1 ниже остаются в силе.

## Состав

| Agent | Роль | Владеет файлами |
|---|---|---|
| 1 Senior .NET Architect | Контракты, Domain, реестр, ADR | `src/CryptoTrader.Domain/**`, `src/CryptoTrader.Exchanges.Abstractions/**`, `src/CryptoTrader.Application/Configuration/**`, `src/CryptoTrader.Application/Abstractions/**`, `src/CryptoTrader.Application/DependencyInjection/ApplicationOptionsReader.cs`, `src/CryptoTrader.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`, `docs/architecture.md`, `docs/adr/011-*`, `docs/adr/012-*`, новые тесты реестра в `tests/CryptoTrader.Architecture.Tests/ExchangeRegistryTests.cs`, доменные тесты новых полей |
| 2 Senior C# Developer | Host, ingestion, admin sync | `src/CryptoTrader.Host/**`, `src/CryptoTrader.Application/MarketData/CandleAdminService.cs`, точечная защита в `src/CryptoTrader.Application/Paper/PaperTradingCoordinator.cs`, `src/CryptoTrader.Contracts/Responses/FeedHealthResponses.cs` |
| 3 Quantitative Developer | Семантика объёма и запрет SWAP в spot backtest | `docs/market-data-semantics.md`, `src/CryptoTrader.Backtesting/**`, `tests/CryptoTrader.Backtesting.Tests/**` |
| 4 Algorithmic Trading Systems Engineer | Bybit regression и OKX adapter | `src/CryptoTrader.Exchanges.Bybit/**`, `src/CryptoTrader.Exchanges.Okx/**`, `CryptoTrader.slnx`, словарь ссылок в `tests/CryptoTrader.Architecture.Tests/DependencyRuleTests.cs`, `docs/okx-integration.md`, `docs/exchange-integration.md` |
| 5 Database Engineer | Схема и checkpoint | `src/CryptoTrader.Infrastructure/Persistence/**`, `docs/database-schema.md`, `deploy/postgres/**` |
| 6 QA Automation Engineer | Тесты и верификация аудита | `tests/**` кроме файлов Agent 1 и словаря `DependencyRuleTests`, `docs/testing-strategy.md`, `docs/agents/audit-verification.md` |
| 7 DevOps Engineer | CI, Docker, эксплуатация | `.github/**`, `deploy/Dockerfile`, `deploy/docker-compose.yml`, `deploy/.env.example`, `deploy/scripts/**`, `README.md`, `docs/deployment.md`, `docs/operations-runbook.md`, `docs/security.md` |
| 8 OKX V5 API Specialist | Официальная спецификация и fixtures | `docs/okx-api-reference-notes.md`, `tests/CryptoTrader.Exchanges.ContractTests/Fixtures/Okx/**` |

`DependencyRuleTests` меняет только Agent 4. Остальные архитектурные тесты не переписывать.

## Решения 0.1, которые остаются

1. Модульный монолит. CQRS и Mediator не внедряются.
2. Live Trading выключен. `Execution:LiveTradingEnabled=true` или `Risk:AllowFuturesLive=true` останавливают запуск.
3. Биржевые клиенты — собственный REST/WebSocket. SDK биржи не подключается.
4. Клиент заявок не выполняет сетевой вызов создания или отмены ордера.
5. Финансовая арифметика — `decimal`. Время — `DateTimeOffset` UTC. Процент `0.5` означает 0,5%.
6. Backtest и Paper используют доменный `PortfolioLedger`. Paper по-прежнему ведёт только spot Bybit.
7. Сигнал строится только по закрытым свечам.
8. Готовность `/health/ready` зависит от PostgreSQL. Биржа не валит весь процесс.
9. `/api/*` требует `X-Api-Key`. Health-эндпоинты публичные.
