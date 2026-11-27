# CryptoTrader 0.1

Модульный монолит для личного контура: публичные рыночные данные Bybit, paper trading и воспроизводимый backtest. Реальные заявки в этой версии не отправляются.

`Execution:LiveTradingEnabled` должен оставаться `false`. То же самое задано переменной `Execution__LiveTradingEnabled=false` в `deploy/docker-compose.yml`. Если флаг истинный, режим `Live`, `Risk:AllowFuturesLive` или `Risk:AllowCrossMargin`, процесс не стартует.

## Требования

- .NET SDK 10. В `global.json` указан SDK `10.0.403` с `rollForward: latestPatch` (тот же feature band).
- Docker Compose v2, если стек запускается в контейнерах.

Сборка решения:

```text
dotnet restore CryptoTrader.slnx
dotnet build CryptoTrader.slnx -c Release
dotnet test CryptoTrader.slnx -c Release --filter "Category!=Integration"
```

Тесты с trait `Category=Integration` ждут Docker. На GitHub Actions их выполняет отдельная job на `ubuntu-latest`.

## Запуск стека

Из каталога `deploy`:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

```bash
cp .env.example .env
docker compose up --build
```

Перед запуском замените `Api__AdminApiKey` в `deploy/.env`. Это локальный секрет заголовка `X-Api-Key`. Это не ключ биржи Bybit. Ключи Bybit в Compose не задаются.

Образ собирается из корня репозитория (`deploy/Dockerfile`, SDK `mcr.microsoft.com/dotnet/sdk:10.0`). Файл `.env` в образ не копируется. Приложение слушает `http://+:8080` и публикуется только на `127.0.0.1:8080`. PostgreSQL 16 публикуется только на `127.0.0.1:5432`.

`Execution__LiveTradingEnabled` в Compose равен `false` и перекрывает значение из `.env`. Менять его на `true` нельзя.

## Проверка

- http://127.0.0.1:8080/health/live — процесс жив, без ключа API.
- http://127.0.0.1:8080/health/ready — PostgreSQL доступен, без ключа API.
- http://127.0.0.1:8080/metrics — Prometheus внутри хоста. Наружу порт не открывается.

```text
curl.exe http://127.0.0.1:8080/health/live
curl.exe http://127.0.0.1:8080/health/ready
```

## Миграции

`Persistence__ApplyMigrationsOnStartup=true` задан в `deploy/docker-compose.yml`. Host применяет миграции EF Core при старте, после того как PostgreSQL проходит `pg_isready`. Отдельная команда migrate не нужна. Строка подключения использует хост `postgres` и учётные данные `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`.

## Свечи и backtest без живой биржи

Импорт `POST /api/v1/market/candles/import` записывает свечи в PostgreSQL и не ходит на биржу. Backtest читает уже сохранённые свечи. Синхронизация `POST /api/v1/market/candles/sync` для этого сценария не нужна: она обращается к публичному Bybit.

Заголовок управляющих маршрутов: `X-Api-Key: <Api__AdminApiKey>`.

1. Узнайте идентификатор стратегии: `GET /api/v1/strategies`.
2. Импортируйте закрытые свечи. Время в UTC, `openTimeUtc` раньше `closeTimeUtc`. Для часа `1h` закрытие на час позже открытия. Биржа в контракте — `BYBIT`.
3. Запустите прогон `POST /api/v1/backtests` по тому же символу и диапазону.
4. Прочитайте отчёт `GET /api/v1/backtests/{id}`.

Пример тела импорта лежит в [docs/deployment.md](docs/deployment.md). `Execution__LiveTradingEnabled` на время backtest остаётся `false`: прогон не отправляет заявки.

## Дальше

- [Развёртывание](docs/deployment.md)
- [Эксплуатация](docs/operations-runbook.md)
- [Безопасность](docs/security.md)
