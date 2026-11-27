# План реализации

Контракт зафиксирован в `docs/agents/multi-exchange-contract.md`. Параллельная работа идёт только внутри владения файлами.

| Фаза | Содержание | Статус |
|---|---|---|
| 0 | Baseline, аудит, команда, контракт | Completed |
| 1 | Registry, identity, volume/contract fields, options | In Progress |
| 2 | Спецификация OKX V5 и fixtures | In Progress |
| 3 | OKX REST | In Progress |
| 4 | OKX WebSocket, Bybit ack/backpressure/completeness | In Progress |
| 5 | Схема, checkpoint, конфигурируемый ingestion | In Progress |
| 6 | Тесты, CI, docs, сборка | Not Started |

Критерий конца: Bybit и OKX выбираются через `IExchangeRegistry`; ingestion читает `MarketData:Subscriptions`; свечи двух бирж не делят ключ; OKX public REST и WebSocket покрыты fixture-тестами; private API не вызывается; `dotnet build` и `dotnet test` в Release проходят. Smoke к живой бирже в обычный CI не входит.

## 27 ноября 2026

Фазы таблицы выше относятся к предыдущему инкременту. Текущий проход добавляет paper spot на раздельных счетах и отсекает устаревшие свечи. Критерий этого прохода: Bybit spot и OKX spot ведут независимые виртуальные счета, реальные заявки не отправляются, демо и фьючерсный учёт не реализуются. Решение — ADR 013. Следующие этапы — `docs/backlog.md`.
