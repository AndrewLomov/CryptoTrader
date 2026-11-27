# Development status

| Поле | Значение |
|---|---|
| Ветка | `feature/okx-multi-exchange` |
| Baseline | `8130a9e2d582fab944d0c9db0a0c8983e9f09cae` (`main`, сообщение `grok-1`) |
| Рабочее дерево на старте | чистое, кроме неотслеживаемых `CryptoTrader_Audit_and_OKX_Integration.md` и `CryptoTrader_Audit_OKX_MultiAgent_Prompt.md` |
| Контракт | `docs/agents/multi-exchange-contract.md` утверждён Lead до реализации адаптера |

Предварительная сверка аудита (Lead, до правок кода). Официальная таблица — `docs/agents/audit-verification.md`.

| ID | Предварительный статус | Основание |
|---|---|---|
| AUD-01 | Confirmed | `CandleIngestionService` зависит от `BybitOptions` и `ExchangeId.Bybit` |
| AUD-02 | Confirmed | `BybitMarketDataClient.MaxKlinePages = 20`, при упоре только warning |
| AUD-03 | Confirmed | `BybitPublicStream` создаёт `Channel.CreateUnbounded` |
| AUD-04 | Confirmed | отрицательный subscribe ack только пишется в лог |
| AUD-05 | Confirmed | `IExchangeRegistry` нет; `IMarketDataClient` регистрируется как один сервис |
| AUD-06 | Partially Confirmed | `IFundingRateClient` есть, но не на адаптере; стакан есть у Bybit-клиента, не у общего порта |
| AUD-07 | Confirmed | ingestion жёстко берёт символы Bybit и `1h`. Уникальный ключ свечи в БД уже включает биржу |
| AUD-08 | Confirmed | после сохранения свечи ошибка paper логируется, повтора нет |
| AUD-09 | Partially Confirmed | backtest и paper venue уже отвергают не-spot; экономики контракта и явного объёма нет |
| AUD-10 | Confirmed | `docs/architecture.md` всё ещё пишет, что реализация отсутствует |
| AUD-11 | Partially Confirmed | контрактные и integration-тесты есть; recovery/ack/backpressure не закрыты |
| AUD-12 | Confirmed | `TreatWarningsAsErrors` выключен, в CI нет `dotnet format --verify-no-changes` |

## Статусы задач

| Задача | Статус |
|---|---|
| Baseline и контракт | Completed |
| Повторная верификация AUD | Completed |
| Контракты Domain / registry | Completed |
| Bybit backpressure, ack, completeness | Completed |
| OKX REST и WebSocket | Completed |
| Ingestion и health | Completed |
| Миграция и checkpoint | Completed |
| Тесты | Completed |
| CI и документация | Completed |
| Сборка и приёмка | Completed |

## 27 ноября 2026

Таблица выше — статус предыдущего инкремента и не переписывается. Этот проход закрывает устаревшие свечи, раздельные paper-счета Bybit и OKX, нормализацию типов инструментов, явный отказ приватных чтений, снятие `.vs` с индекса и обязательный format. Демо, учёт фьючерсов и новые стратегии остаются в `docs/backlog.md`. База до правок записана в `docs/agents/preflight.md`.
