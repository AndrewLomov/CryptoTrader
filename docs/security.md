# Безопасность CryptoTrader 0.1

Контур 0.1 — локальный или один VPS. Реальные заявки не отправляются. `Execution:LiveTradingEnabled` должен оставаться `false`.

## Состояние дерева

Снимок ветки `feature/okx-multi-exchange`. Метки: Implemented, Partially Implemented, Not Implemented.

| Область | Статус |
|---|---|
| Публичные рыночные данные Bybit | Implemented |
| Публичный адаптер OKX | Implemented |
| Live-торговля | Not Implemented |
| Приватный OKX | Not Implemented |
| Секреты вне git и логов | Implemented |
| `GET /health/live` | Implemented, анонимный |
| `GET /health/ready` | Implemented, анонимный, PostgreSQL |
| `GET /health/feeds` | Implemented, анонимный |

`BybitOptions` хранит публичный REST, публичные WebSocket, список символов, таймаут и лимит запросов. API-ключа биржи в этом типе нет. `BybitTradingClient.SubmitAsync` возвращает `LiveTradingDisabled` и сеть не открывает. Чтение приватного счёта Bybit и отмена заявки бросают `NotSupportedException`.

Публичный OKX реализован в `src/CryptoTrader.Exchanges.Okx`. Приватный REST, приватный WebSocket, API key, secret и passphrase в этом проекте отсутствуют. `OkxTradingClient.SubmitAsync` возвращает `LiveTradingDisabled` без сети. `OkxAccountClient` бросает `NotSupportedException` и не возвращает фиктивный баланс. `deploy/.env.example` и `deploy/docker-compose.yml` ключей OKX не содержат. Публичные URL задаются в `appsettings.json`; Compose их не перекрывает секретами.

`Program.cs` сопоставляет `/health/live`, `/health/ready` и `/health/feeds`. Ответы health и API секрет биржи не возвращают: секрета биржи в конфигурации нет.

## Секреты не попадают в git

В репозитории нет пароля базы, административного ключа и ключей биржи. `deploy/.env.example` содержит только placeholder `cryptotrader` и строку `Api__AdminApiKey=replace-with-a-long-local-secret`.

Рабочий файл — `deploy/.env`. Его копируют из примера на машине оператора. `.gitignore` игнорирует `.env` и `.env.*`, кроме `!.env.example`, а также `appsettings.*.local.json`, `secrets.json` и каталог `secrets/`.

Dockerfile не копирует `.env` в слои образа. В образ попадает результат `dotnet publish` проекта Host. Секреты приходят в контейнер через `env_file` и секцию `environment` Compose в момент запуска.

Дампы `pg_dump` лежат вне дерева исходников. Их не коммитят: в базе есть paper-состояние, свечи и отчёты backtest.

Пароль базы и `Api__AdminApiKey` не вставляют в issues, логи и скриншоты. `RedactingJsonFormatter` отбрасывает свойства, в имени которых есть key, secret, token, password или signature. Имя `passphrase` само по себе в этот список не входит. Приватного OKX в дереве нет, и passphrase в конфигурацию не добавляют. Перед появлением приватного контура в проверку имени нужно включить `passphrase`.

## Ключ управляющего API

Маршруты `/api/v1/*` требуют заголовок `X-Api-Key`. Host сравнивает его с `Api:AdminApiKey` и не пишет ключ в лог. Пустое значение останавливает запуск.

`Api__AdminApiKey` — локальный секрет этого API. Это не ключ биржи Bybit и не ключ OKX. В запросах к публичному Bybit он не используется. Отдельной пользовательской учётной записи в 0.1 нет.

`GET /health/live`, `GET /health/ready` и `GET /health/feeds` анонимны. Ready зависит от PostgreSQL. Ответы API не содержат секретов.

Перед первым запуском placeholder в `.env` заменяют на длинную случайную строку. Ключ хранится в `.env` или в секрет-хранилище хоста, не в `appsettings.json`.

## Контейнеры не от root

Образ приложения основан на `mcr.microsoft.com/dotnet/aspnet:10.0`. Dockerfile завершается инструкцией `USER $APP_UID`: это встроенный пользователь `app`, не root. Опубликованные файлы доступны ему на чтение.

Серверный процесс PostgreSQL в `postgres:16-alpine` работает от пользователя `postgres`. Порт приложения в контейнере — 8080, без повышения привилегий в Compose.

Секреты в файловую систему образа не кладут.

## PostgreSQL не опубликован наружу

Проброс базы в Compose: `127.0.0.1:5432:5432`. Приложение с хоста доступно как `127.0.0.1:8080:8080`. С других машин эти порты не открываются.

Внутри сети Compose приложение подключается к хосту `postgres`. Для доступа с хоста к клиенту psql достаточно loopback.

Менять привязку на `0.0.0.0` или публиковать 5432 наружу не нужно. Если позже появится внешний HTTPS, его ставит обратный прокси на этот хост. `/metrics` при этом наружу не отдают: путь Prometheus по умолчанию `/metrics` и не начинается с `/api`.

## Live-торговля отвергается конфигурацией

Безопасные значения заданы в коде и продублированы в Compose:

- `Execution__Mode=Paper`
- `Execution__LiveTradingEnabled=false`
- `Risk__AllowFuturesLive=false`
- `Risk__AllowCrossMargin=false`
- `MarketData__UseClosedCandlesForStrategies=true`

Секция `environment` перекрывает `.env`. Даже при ошибочном `true` в файле окружения контейнер получает `false` из Compose. Если флаг всё же истинный, `Execution:Mode` равен `Live`, либо разрешены futures live или cross margin, валидатор не даёт процессу стартовать.

`ITradingClient.SubmitAsync` при выключенном live не открывает сеть и возвращает отказ `LiveTradingDisabled`. Доменные заявки и счета в режиме Live не создаются.

Paper и backtest этот запрет не обходят: paper не создаёт биржевую заявку, backtest читает свечи из хранилища.

## Вывод средств

Приватная торговля в 0.1 — Not Implemented. Подпись приватных методов Bybit отсутствует, ключ биржи в конфигурации не заводится. Приватный OKX — Not Implemented. Право вывода средств (withdrawal) не запрашивается и не используется, потому что приватного торгового контура нет.

Публичные методы Bybit ключ не требуют. Публичная конфигурация OKX по контракту ветки тоже обходится без API key, secret и passphrase. Импорт свечей и backtest выполняются без биржевого секрета.

Когда приватная торговля появится отдельным решением, ключ биржи по-прежнему не кладут в git, образ и логи, а разрешение withdrawal не добавляют.
