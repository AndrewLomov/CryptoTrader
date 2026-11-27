# Итог ревью, 27 ноября 2026

Независимая проверка правок поверх `79367b2`. Исторические отчёты агентов выше по файлам описывают дерево до этих правок. Этот файл их не заменяет.

## Что закрыто

| Пункт | Результат |
|---|---|
| P1.1 устаревшие свечи | Допуск общий. История пишется. Paper не меняется. `Skipped` терминальный |
| P1.2 раздельные счета | Пара биржа и котировка. Bybit и OKX не делят cash |
| P2.1 `.vs` | Снят с индекса, правило `.gitignore` остаётся |
| P2.2 типы инструментов | Обрезка, верхний регистр, дубликат и неизвестный тип отвергаются |
| P2.3 приватные чтения | `NotSupportedException` на обеих биржах. `SubmitAsync` без HTTP |
| P2.4 документы | Статусы приведены к коду. Host ссылается на OKX |
| P2.5 формат | BOM снят, job `format` обязательный. `TreatWarningsAsErrors` не включали |
| P2.6 стакан | Снимок `books5` и REST 50. Непрерывность не заявляется |
| Checkpoint | `Processed` и `Skipped` не переоткрываются. Fill идемпотентен |

## Готовность

| Контур | Статус |
|---|---|
| Paper spot Bybit | Implemented |
| Paper spot OKX | Implemented |
| OKX Demo Trading | Not Implemented |
| Учёт фьючерсов | Not Implemented |
| Новые стратегии | Not Implemented |
| Инкрементальный стакан | Not Implemented |

Реальные деньги не используются. Прибыльность не оценивалась. Один gauge equity показывает последний обновлённый счёт. Cash и позиции при этом лежат в разных строках `paper_accounts`.

Локальный прогон 27 ноября 2026, Release, до коммита:

| Команда | Результат |
|---|---|
| `dotnet test CryptoTrader.slnx -c Release --filter Category!=Integration` | 99 passed, 0 failed. Domain 28, Strategies 5, Application 25, Backtesting 5, Architecture 13, Contract 23 |
| `dotnet test CryptoTrader.slnx -c Release --filter Category=Integration` | 5 passed, 0 failed. PostgreSQL 16 через Testcontainers |
| `dotnet format CryptoTrader.slnx --verify-no-changes` | код 0 |

Удалённый прогон GitHub Actions по финальному коммиту в этот отчёт не вписан, пока workflow не наблюдался.
