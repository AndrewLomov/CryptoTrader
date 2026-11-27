# Развёртывание CryptoTrader 0.1

Стек — два контейнера на одном хосте: приложение и PostgreSQL. Kubernetes, Kafka и Terraform не используются.

`Execution:LiveTradingEnabled` должен оставаться `false`. В `deploy/docker-compose.yml` это `Execution__LiveTradingEnabled=false`. Рядом зафиксированы `Execution__Mode=Paper`, `Risk__AllowFuturesLive=false`, `Risk__AllowCrossMargin=false` и `MarketData__UseClosedCandlesForStrategies=true`. Секция `environment` перекрывает `env_file`. Валидатор всё равно останавливает запуск, если live-флаг, режим `Live`, futures live или cross margin включены.

Ключи Bybit в Compose и в образ не попадают. Версия 0.1 ходит только в публичные рыночные методы, а backtest умеет работать по импортированным свечам.

## Требования

- Docker Compose v2.
- Для локальной сборки без Docker — .NET SDK 10. `global.json` фиксирует `10.0.403` и `rollForward: latestPatch`.
- Образ приложения собирается из `mcr.microsoft.com/dotnet/sdk:10.0` и запускается на `mcr.microsoft.com/dotnet/aspnet:10.0`.

Контекст сборки — корень репозитория, файл инструкции — `deploy/Dockerfile`. Публикуется `src/CryptoTrader.Host/CryptoTrader.Host.csproj` в конфигурации Release. Процесс в контейнере — пользователь `app` (`USER $APP_UID`), порт `ASPNETCORE_URLS=http://+:8080`. Файл `.env` в образ не копируется.

## Подготовка окружения

Команды ниже выполняются в каталоге `deploy`.

PowerShell:

```powershell
Copy-Item .env.example .env
```

bash:

```bash
cp .env.example .env
```

В `.env` задайте длинный локальный `Api__AdminApiKey`. Это секрет заголовка `X-Api-Key` для `/api/v1/*`. Это не ключ биржи. Пустое значение не даёт процессу стартовать.

`POSTGRES_USER`, `POSTGRES_PASSWORD` и `POSTGRES_DB` в примере равны `cryptotrader`. Это локальный placeholder. Пароль попадёт в строку Npgsql, поэтому не используйте в нём `;`, `$` и кавычки.

`Persistence__ConnectionString` указывает на хост `postgres` — имя сервиса Compose, не `localhost`. Compose заново собирает ту же строку из `POSTGRES_*`, чтобы приложение и база использовали одни учётные данные.

`deploy/.env` не коммитится. В репозитории остаётся только `deploy/.env.example`.

## Запуск

```text
docker compose up --build
```

Сервис `postgres` — образ `postgres:16-alpine`, именованный том `cryptotrader-postgres`, проверка `pg_isready`. Порт базы на хосте: `127.0.0.1:5432`.

Сервис `cryptotrader` ждёт здоровой базы, затем стартует. Порт приложения на хосте: `127.0.0.1:8080`.

Проверка:

```text
curl.exe http://127.0.0.1:8080/health/live
curl.exe http://127.0.0.1:8080/health/ready
curl.exe http://127.0.0.1:8080/metrics
```

`/health/live` и `/health/ready` анонимны. Ready проверяет PostgreSQL и не проверяет Bybit. `/metrics` остаётся на loopback вместе с портом 8080.

Остановка без удаления данных:

```text
docker compose stop
```

`docker compose down` удаляет контейнеры и сеть, том базы сохраняется. `docker compose down -v` удаляет том `cryptotrader-postgres` вместе с данными.

Повторный запуск из `deploy`:

```text
docker compose up --build
```

Тот же стек из корня репозитория:

```text
docker compose --project-directory deploy -f deploy/docker-compose.yml --env-file deploy/.env up --build
```

Файл `env_file` у сервиса приложения всё равно `deploy/.env`.

## Миграции

Отдельного контейнера migrate нет. `Persistence__ApplyMigrationsOnStartup=true` заставляет Host применить миграции EF Core на старте. К этому моменту `depends_on` уже дождался `pg_isready`.

Повторный старт применяет только ещё не применённые миграции. Схема не накатывается вручную скриптом.

Если база недоступна, смотрите `docker compose ps` и `docker compose logs postgres`. Пока PostgreSQL нездоров, приложение не считается готовым.

## Импорт свечей и backtest без биржи

Сценарий не требует учётной записи Bybit и не включает live-торговлю. `Execution__LiveTradingEnabled` остаётся `false`.

Управляющие запросы несут заголовок `X-Api-Key`. Подставьте значение `Api__AdminApiKey` из своего `.env`.

Список стратегий:

```text
curl.exe http://127.0.0.1:8080/api/v1/strategies -H "X-Api-Key: replace-with-a-long-local-secret"
```

Импорт закрытых свечей, файл `candles.json`:

```json
{
    "exchange": "BYBIT",
    "symbol": "BTCUSDT",
    "timeframe": "1h",
    "candles": [
        {
            "openTimeUtc": "2024-01-01T00:00:00Z",
            "closeTimeUtc": "2024-01-01T01:00:00Z",
            "open": 42000,
            "high": 42150,
            "low": 41950,
            "close": 42100,
            "volume": 12.5
        },
        {
            "openTimeUtc": "2024-01-01T01:00:00Z",
            "closeTimeUtc": "2024-01-01T02:00:00Z",
            "open": 42100,
            "high": 42200,
            "low": 42050,
            "close": 42180,
            "volume": 9.25
        }
    ]
}
```

```text
curl.exe -X POST http://127.0.0.1:8080/api/v1/market/candles/import -H "Content-Type: application/json" -H "X-Api-Key: replace-with-a-long-local-secret" --data-binary @candles.json
```

Две свечи показывают форму запроса. Рабочий прогон стратегии требует ряда, который покрывает её окно. Цены больше нуля, high не ниже open/close/low, low не выше open/close/high, объём не отрицательный.

Backtest по импортированному диапазону. `strategyId` и `strategyVersion` возьмите из `GET /api/v1/strategies`. `parametersJson` — строка JSON с параметрами этой стратегии. `instrument` — тот же символ, что и `symbol` в импорте.

Файл `backtest.json`:

```json
{
    "strategyId": "strategy-id-from-catalog",
    "strategyVersion": "strategy-version-from-catalog",
    "parametersJson": "{}",
    "exchange": "BYBIT",
    "instrument": "BTCUSDT",
    "fromUtc": "2024-01-01T00:00:00Z",
    "toUtc": "2024-01-01T02:00:00Z",
    "initialEquity": 10000,
    "feeRate": 0.001,
    "slippageBps": 5,
    "exitOnTrendBreak": false,
    "risk": {
        "riskPerTradePercent": 0.5,
        "maxDailyLossPercent": 1,
        "maxOpenPositions": 2,
        "maxExposurePercent": 100,
        "feeRate": 0.001,
        "slippageBps": 5,
        "averagingDownEnabled": false,
        "maxDrawdownPercent": 10
    }
}
```

```text
curl.exe -X POST http://127.0.0.1:8080/api/v1/backtests -H "Content-Type: application/json" -H "X-Api-Key: replace-with-a-long-local-secret" --data-binary @backtest.json
```

Ответ содержит `runId`. Отчёт:

```text
curl.exe http://127.0.0.1:8080/api/v1/backtests/{runId} -H "X-Api-Key: replace-with-a-long-local-secret"
```

`POST /api/v1/market/candles/sync` для этого пути не вызывается: sync читает публичный Bybit. Импорт и backtest остаются локальными, даже когда биржа недоступна.

Проценты в риск-профиле задаются как `0.5` = 0,5%. Live-флаги в тело backtest не передаются и в Compose остаются выключенными.

## CI

Workflow `.github/workflows/ci.yml` запускается на `push` и `pull_request`, runner `ubuntu-latest`.

Job `build`: `actions/setup-dotnet` со SDK `10.0.x` и версией из `global.json`, затем `dotnet restore CryptoTrader.slnx`, `dotnet build CryptoTrader.slnx -c Release`, `dotnet test CryptoTrader.slnx -c Release --filter "Category!=Integration"`.

Job `integration`: тот же SDK, проверка `docker version`, тесты `--filter "Category=Integration"`. Trait в тестах — `Category=Integration`. На `ubuntu-latest` Docker уже есть, отдельные Windows-раннеры не используются.

## Чего в поставке нет

Образ не содержит `.env` и ключей биржи. Порты 5432 и 8080 не публикуются на всех интерфейсах. Live-заявки конфигурацией этого файла не включаются.
