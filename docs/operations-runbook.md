# Эксплуатация CryptoTrader 0.1

Команды Compose выполняются в каталоге `deploy`, где лежат `docker-compose.yml` и `.env`. Live-торговля остаётся выключенной: `Execution__LiveTradingEnabled=false`, режим `Paper`. Эти значения в Compose перекрывают `.env`. Включать их для «проверки связи с биржей» нельзя: процесс с live-конфигурацией не стартует, а приватные заявки в 0.1 не реализованы.

## Состояние дерева

Снимок ветки `feature/okx-multi-exchange`. Метки: Implemented, Partially Implemented, Not Implemented. Стек для этой сверки не запускался.

| Область | Статус |
|---|---|
| Публичные рыночные данные Bybit | Implemented |
| Публичный адаптер OKX | Implemented |
| Live-торговля | Not Implemented |
| Приватный OKX | Not Implemented |
| `GET /health/live` | Implemented |
| `GET /health/ready` | Implemented |
| `GET /health/feeds` | Implemented |

Процесс регистрирует Bybit и OKX (`AddBybit`, `AddOkx`) и выбирает адаптер через `IExchangeRegistry`. Публичный Bybit и публичный OKX API-ключ не используют. Приватный OKX не реализован. Compose ключи бирж в контейнер не передаёт.

Секреты остаются вне git и логов. В сообщения не копируют строку подключения и `Api__AdminApiKey`. `RedactingJsonFormatter` отбрасывает свойства, в имени которых есть key, secret, token, password или signature.

## Состояние

```text
docker compose ps
docker compose logs --tail 200 cryptotrader
docker compose logs --tail 200 postgres
docker compose logs -f cryptotrader
```

Логи приложения пишет Serilog в stdout контейнера. Поля, в имени которых есть key, secret, token, password или signature, маскируются. Имя passphrase само по себе эта маска не ловит. Строку подключения и `Api__AdminApiKey` в сообщения не копируют. Ключ Bybit и ключ OKX в этот контур не передаются.

Политика `restart: unless-stopped` поднимает контейнеры после перезапуска демона. Она не меняет флаги live.

## Живость, готовность, метрики

С хоста, без заголовка `X-Api-Key`:

| URL | Что означает успешный ответ |
|---|---|
| http://127.0.0.1:8080/health/live | Implemented. Процесс обслуживает HTTP |
| http://127.0.0.1:8080/health/ready | Implemented. PostgreSQL доступен |
| http://127.0.0.1:8080/health/feeds | Implemented. Состояние подписок market data, без ключа API |
| http://127.0.0.1:8080/metrics | Текст Prometheus |

```text
curl.exe -D - http://127.0.0.1:8080/health/live
curl.exe -D - http://127.0.0.1:8080/health/ready
curl.exe -D - http://127.0.0.1:8080/metrics
```

Ready не опрашивает Bybit. Биржа может быть недоступна, а `/health/ready` остаётся успешным, пока жива база.

`/metrics` слушает тот же локальный порт 8080. Наружу его не пробрасывают. Набор метрик хоста включает состояние WebSocket, возраст рыночных данных, одобренные и отклонённые сигналы, equity paper. Имена рядов задаёт Host; скрейпер Prometheus ставят только на loopback.

`/health/live` неуспешен, когда процесс не отвечает. Тогда смотрите логи и `docker compose ps`. Частая причина сразу после старта — валидатор конфигурации: пустой `Api__AdminApiKey`, `Execution__LiveTradingEnabled=true`, режим `Live`, `Risk__AllowFuturesLive=true` или `Risk__AllowCrossMargin=true`. Исправьте `.env` и Compose так, чтобы live оставался `false`, и поднимите сервис снова.

`/health/ready` неуспешен, когда нет соединения с PostgreSQL. Смотрите healthcheck базы:

```text
docker compose ps postgres
docker compose logs --tail 100 postgres
```

Миграции применяет сам Host, когда `Persistence__ApplyMigrationsOnStartup=true`. В `appsettings.json` это поле равно `false`. Compose задаёт `true` в `environment` и перекрывает `.env`. Отдельный job migrate не запускают.

Порядок на старте контейнера `cryptotrader`:

1. `depends_on` ждёт `pg_isready` у сервиса `postgres`.
2. Пока флаг истинный, `Program.cs` до `app.Run` вызывает `CryptoTrader.Host.Persistence.DatabaseMigrator.ApplyAsync`.
3. `AddPersistence` регистрирует `PersistenceMigrationHostedService`. Его `StartingAsync` при том же флаге вызывает `CryptoTrader.Infrastructure.Persistence.DatabaseMigrator.ApplyAsync`, а тот — `Database.MigrateAsync`.
4. Второй проход применяет только миграции, которых ещё нет в `__EFMigrationsHistory`.

Стек для описания этого порядка не запускался. После восстановления базы снова дождитесь успешного `/health/ready`.

## Резервная копия

Скрипт `deploy/scripts/backup.sh` вызывает `docker compose exec -T` и `pg_dump` в формате custom (`--format=custom`). Путь к файлу обязателен. Пустой путь завершает скрипт с кодом 1 и не вызывает Docker.

Запускайте bash, в том числе из PowerShell. Перенаправление PowerShell портит двоичный дамп; перенаправление внутри скрипта делает bash.

Git Bash, каталог репозитория, файл вне дерева исходников:

```bash
bash deploy/scripts/backup.sh /d/backups/cryptotrader-$(date +%Y%m%d).dump
```

Linux:

```bash
bash deploy/scripts/backup.sh /var/backups/cryptotrader.dump
```

Скрипт читает `POSTGRES_USER` и `POSTGRES_DB` из `deploy/.env`, не печатает пароль и пишет дамп с `umask 077`. Существующий файл по этому пути заменяется. Рядом на время записи появляется `*.partial`; при ошибке он удаляется.

Нужен уже запущенный сервис `postgres`. Дамп храните вне репозитория. В git его не добавляют.

## Восстановление

`deploy/scripts/restore.sh` принимает тот же custom-дамп. Пустой путь отклоняется до вызова Docker. Несуществующий или нулевой файл тоже отклоняется.

```bash
bash deploy/scripts/restore.sh /d/backups/cryptotrader.dump
```

Скрипт останавливает `cryptotrader`, если сервис был запущен, чтобы снять соединения с базой. Дальше `pg_restore --clean --if-exists --no-owner --no-acl --exit-on-error` выполняет восстановление через stdin (`docker compose exec -T`). Если приложение было запущено, скрипт включает его снова. После старта Host применяет только те миграции, которых нет в восстановленной `__EFMigrationsHistory`.

`Execution__LiveTradingEnabled` восстановление не меняет. После старта флаг по-прежнему `false`.

Проверка:

```text
curl.exe http://127.0.0.1:8080/health/ready
docker compose exec -T postgres psql -U cryptotrader -d cryptotrader -c "\dt"
```

Пользователь и база в команде `psql` — значения из `.env`. В примере это `cryptotrader`.

## Учебное восстановление

Проводите на локальном контуре. Дамп кладите вне репозитория. Live не включайте.

1. Поднимите стек из `deploy` командой `docker compose up --build`.
2. Дождитесь HTTP 200 на http://127.0.0.1:8080/health/ready и в логах `cryptotrader` старта без ошибки миграций.
3. Зафиксируйте список таблиц:

```text
docker compose exec -T postgres psql -U cryptotrader -d cryptotrader -c "\dt"
```

4. Если таблица `candles` уже есть, запомните число строк:

```text
docker compose exec -T postgres psql -U cryptotrader -d cryptotrader -c "SELECT COUNT(*) FROM candles;"
```

5. Снимите копию:

```bash
bash scripts/backup.sh /tmp/cryptotrader-drill.dump
```

Команда `scripts/backup.sh` — из каталога `deploy`. Из корня репозитория путь `deploy/scripts/backup.sh`.

6. Убедитесь, что пустой путь не трогает базу. Ожидается код 1 и текст `target file path is empty`, контейнеры остаются как были:

```bash
bash scripts/backup.sh
bash scripts/restore.sh ""
```

7. Восстановите учебный дамп:

```bash
bash scripts/restore.sh /tmp/cryptotrader-drill.dump
```

8. Дождитесь остановки и повторного старта `cryptotrader`, затем снова проверьте `/health/ready`, `\dt` и `COUNT(*)` по `candles`. Список таблиц и число строк совпадают со снимком из шагов 3–4.
9. Удалите `/tmp/cryptotrader-drill.dump`. В git файл не добавляйте.

Если шаг 7 завершился с ошибкой, стек не используют для разбора стратегии, пока не восстановлен последний удачный дамп. Повторный успешный `pg_restore` того же файла приводит схему к содержимому дампа.

## Штатная остановка

`docker compose stop` посылает SIGTERM. У обоих сервисов `stop_grace_period` равен 30 секундам: за это время Host отменяет фоновые службы через `CancellationToken`, затем Docker посылает SIGKILL, если процесс ещё жив.

Останавливайте приложение раньше базы, чтобы оно успело дописать состояние paper:

```text
docker compose stop cryptotrader
docker compose stop postgres
```

`docker compose stop` без имени сервиса останавливает оба и сохраняет том. `docker kill` используйте только когда процесс не отвечает на SIGTERM до конца grace period.

Повторный запуск: `docker compose up -d` из каталога `deploy`. Флаги live снова приходят из Compose и остаются `false`.

## Bybit недоступен

Процесс не останавливают. `/health/live` остаётся проверкой процесса, `/health/ready` остаётся проверкой PostgreSQL. Пока база здорова, неготовый WebSocket сам по себе приложение не валит. `GET /health/feeds` показывает состояние подписок. Обрыв Bybit не останавливает OKX и не закрывает paper-позиции другой биржи.

Новые paper-входы ждут свежих данных. Риск-менеджер отклоняет сигнал, пока рынок помечен устаревшим или биржа нездорова (`StaleMarketData`, `ExchangeUnhealthy`). Окно устаревания по умолчанию — `MarketData:StaleAfterSeconds` = 180. Уже записанные paper-позиции на биржу не отправляются и сами не закрываются.

Что делать:

1. Убедиться, что `/health/live` и `/health/ready` успешны, а в логах виден обрыв публичного REST или WebSocket, а не ошибка валидатора live.
2. Оставить `cryptotrader` запущенным. Клиент публичного потока переподключается сам. Ключ биржи для этого не нужен и в окружение его не добавляют.
3. Новые входы paper не форсировать. Они снова возможны, когда придут свежие публичные данные.
4. `Execution__LiveTradingEnabled` не переключать. Недоступность Bybit не лечится режимом Live.
5. Импорт свечей и backtest в это время работают из PostgreSQL и сеть биржи не требуют.

Если в логах долго нет попыток переподключения, а база при этом здорова, можно перезапустить только приложение:

```text
docker compose restart cryptotrader
```

Перезапуск не включает live и не удаляет том PostgreSQL. Готовность снова смотрят по `/health/ready`.

## Ручная проверка публичного OKX

В CI этого вызова нет. Ключ биржи не нужен и в запрос его не подставляют.

1. `GET https://openapi.okx.com/api/v5/public/instruments?instType=SPOT` — в `data` есть `BTC-USDT`, `state` читается как есть.
2. `GET https://openapi.okx.com/api/v5/market/ticker?instId=BTC-USDT` — `last` положительный.
3. `GET https://openapi.okx.com/api/v5/market/books?instId=BTC-USDT&sz=50` — снимок, не доказанная непрерывная книга.
4. После запуска стека с `Okx:Enabled=true` и подпиской `OKX` / `BTC-USDT` / `1m`: `GET /health/feeds` без ключа показывает подписку, `GET /api/v1/paper-accounts` с `X-Api-Key` показывает счёт OKX только после закрытой допустимой свечи. Приватные пути OKX не вызывают.
