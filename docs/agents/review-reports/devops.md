# DevOps: CI, формат, поставка

Дата: 2026-11-27. Ветка: `feature/okx-multi-exchange`. Агент правил workflow, `deploy/` и перечисленные документы. Коммита нет. Полный `dotnet test` не запускался. `docker compose` не запускался.

## Команды и результаты

SDK: `dotnet --version` → `10.0.403`. Ветка: `git rev-parse --abbrev-ref HEAD` → `feature/okx-multi-exchange`.

Проверка форматирования, каталог репозитория, код выхода 2, около 25 с:

```text
dotnet format CryptoTrader.slnx --verify-no-changes
```

Журнал — 668 строк, все про один файл:

`src/CryptoTrader.Infrastructure/Persistence/Migrations/20261127085645_Initial.cs`

- `ENDOFLINE` на строках 1–667: «Замените символы 2 на `\n`» (CRLF).
- `CHARSET` в позиции (1,1): «Исправить кодировку файла».

`git ls-files --eol` по каталогу миграций:

| Файл | Индекс | Рабочая копия |
|---|---|---|
| `20261127085645_Initial.cs` | `i/lf` | `w/crlf` |
| `20261127085645_Initial.Designer.cs` | `i/lf` | `w/crlf` |
| `CryptoTraderDbContextModelSnapshot.cs` | `i/lf` | `w/crlf` |

В отчёте `dotnet format` есть только `Initial.cs`. Designer и снимок в журнал не попали.

`git config --get core.autocrlf` → `true`. Конфиг git не менялся. `git diff --stat` по `Initial.cs` пустой: для git содержимое совпадает с индексом, отличие рабочей копии — концы строк.

Байты `Initial.cs`:

| Источник | Байт | CR | LF | UTF-8 BOM |
|---|---|---|---|---|
| Рабочая копия | 36339 | 667 | 667 | да (`EF BB BF`) |
| Blob `HEAD` (`git cat-file`, `core.autocrlf=false`) | 35672 | 0 | 667 | да (`EF BB BF`) |

`ENDOFLINE` на этой машине даёт `core.autocrlf=true`. `CHARSET` есть и в blob: файл в git уже UTF-8 с BOM, а `.editorconfig` задаёт `charset = utf-8`. На `ubuntu-latest` checkout возьмёт LF и BOM, поэтому обязательный шаг format валит ветку на уже лежащем в git расхождении. C#-дерево не форматировалось: миграция принадлежит слою persistence, её правят другие агенты.

`dotnet test` не запускался. `docker compose` не запускался.

## CI

`.github/workflows/ci.yml`.

Job `build` и `integration` оставлены. Обе работают с `CryptoTrader.slnx`: restore, build Release, SDK `10.0.x` и `global.json`. Unit-тесты: `--filter "Category!=Integration"`. Интеграция: `docker version`, затем `--filter "Category=Integration"`. Живых вызовов OKX workflow не содержит. Единственный класс с trait `Category=Integration` в дереве на момент сверки — `tests/CryptoTrader.IntegrationTests/DatabaseTests.cs` (Testcontainers PostgreSQL).

Добавлена job `format` (`format (non-blocking)`): checkout, setup-dotnet, `dotnet restore CryptoTrader.slnx`, затем `dotnet format CryptoTrader.slnx --verify-no-changes` с `continue-on-error: true`. Шаг не обязательный. `Directory.Build.props` не менялся: `TreatWarningsAsErrors` остаётся `false`, `AnalysisLevel` остаётся `latest`.

Дальше, чтобы сделать шаг обязательным: убрать BOM у `20261127085645_Initial.cs`, повторить ту же команду на дереве с LF до кода 0, снять `continue-on-error`. Локальный Windows с `core.autocrlf=true` после снятия BOM всё ещё может дать `ENDOFLINE` на рабочей копии. Ориентир для обязательного шага — checkout CI, где blob уже LF.

## Дерево на момент документа

- Публичный Bybit: Implemented. Проект `src/CryptoTrader.Exchanges.Bybit` в `CryptoTrader.slnx`. `Program.cs` вызывает `AddBybit`. `BybitOptions` — публичный REST `https://api.bybit.com`, публичные WebSocket spot и linear, символы, таймаут, лимит. Поля API-ключа нет. `BybitTradingClient.SubmitAsync` возвращает `LiveTradingDisabled` без сети.
- Адаптер OKX: Not Implemented. Каталога `src/CryptoTrader.Exchanges.Okx` нет, `AddOkx` нет, секции `Okx` в `appsettings.json` нет. `docs/okx-api-reference-notes.md` нет. Эндпоинты OKX в deploy-документы не переносились.
- Live-торговля: Not Implemented. Compose держит `Execution__LiveTradingEnabled=false`, `Execution__Mode=Paper`, `Risk__AllowFuturesLive=false`, `Risk__AllowCrossMargin=false`.
- Приватный OKX: Not Implemented.
- `GET /health/live` и `GET /health/ready`: Implemented в `Program.cs`. Ready — `DatabaseReadyProbe` и PostgreSQL. `GET /health/feeds`: Not Implemented, маршрут не сопоставлен.
- Миграции: `appsettings.json` держит `Persistence:ApplyMigrationsOnStartup` равным `false`. Compose задаёт `Persistence__ApplyMigrationsOnStartup=true` и перекрывает `env_file`. При `true` `Program.cs` до `app.Run` вызывает host `DatabaseMigrator.ApplyAsync`, а `PersistenceMigrationHostedService.StartingAsync` вызывает infrastructure `DatabaseMigrator.ApplyAsync` (`Database.MigrateAsync`). Отдельного сервиса migrate нет. Стек не поднимался.
- Секреты: `deploy/.env.example` по-прежнему содержит только placeholder `cryptotrader` и `Api__AdminApiKey=replace-with-a-long-local-secret`. Ключ, secret и passphrase OKX не добавлены. Compose не задаёт `Okx__Enabled`, `Okx__RestBaseUrl`, `Okx__PublicWebSocketUrl`. Комментарий в Compose и в примере называет эти три имени как незаданные публичные настройки.
- Логи: `RedactingJsonFormatter.IsSensitive` смотрит подстроки key, secret, token, password, signature. Подстрока `passphrase` сама по себе не входит. Перед приватным OKX её нужно добавить в проверку.

## Файлы

- `.github/workflows/ci.yml` — job `format` с `continue-on-error`.
- `deploy/docker-compose.yml` — комментарий, переменные OKX не заданы, ключей нет.
- `deploy/.env.example` — комментарий без новых секретов.
- `README.md`, `docs/deployment.md`, `docs/operations-runbook.md`, `docs/security.md` — статусы Implemented / Partially Implemented / Not Implemented по фактическому дереву.
- `Directory.Build.props` — без изменений.

## 27 ноября 2026

Текст выше описывает неблокирующую job. Сейчас job называется `format`, `continue-on-error` снят. BOM снят с миграций `Initial`, Designer и снимка модели. Добавлен `.gitattributes` с `eol=lf` для `*.cs`. `TreatWarningsAsErrors` не включали. `.vs` снят с индекса, локальные файлы и правило `.gitignore` остаются. Ключи биржи в workflow не добавлялись.
