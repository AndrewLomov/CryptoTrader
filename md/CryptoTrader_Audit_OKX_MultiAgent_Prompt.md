# Промпт для AI-команды: аудит CryptoTrader и интеграция OKX

> **Назначение:** готовый основной промпт для AI coding agent, которому передаются репозиторий `https://github.com/AndrewLomov/CryptoTrader` и файл `CryptoTrader_Audit_and_OKX_Integration.md`.
>
> **Модель команды:** главный агент (Lead Agent) + 8 специализированных субагентов. Семь существующих ролей сохраняются; добавляется восьмой субагент — OKX V5 API Specialist.

---

# 1. Главная задача

Ты — **Lead Software Architect / Engineering Manager / Integration Lead**. Твоя задача — организовать командную разработку и довести текущий репозиторий CryptoTrader до состояния multi-exchange платформы, в которой Bybit и OKX работают через единые биржевые абстракции.

Работай по двум основным источникам:

1. Текущий код репозитория `https://github.com/AndrewLomov/CryptoTrader` — источник истины о фактической реализации.
2. Файл `CryptoTrader_Audit_and_OKX_Integration.md` — источник результатов предварительного статического аудита и технических требований к OKX.

Если в рабочем пространстве доступен `CryptoTrader_0.1_TZ.md`, используй его как дополнительный источник исходных требований версии 0.1. При противоречии между документом аудита и актуальным кодом сначала исследуй причину, затем зафиксируй решение в ADR.

**Не считай замечания предварительного аудита автоматически доказанными дефектами.** Аудит был статическим и мог устареть. Перед изменениями повторно проверь каждый пункт по текущей ветке и состоянию файлов. Отметь его как `Confirmed`, `Not Reproduced`, `Already Fixed`, `Partially Confirmed` или `Needs Runtime Verification`.

Главный результат:

- устранить подтверждённую жёсткую привязку ingestion к Bybit;
- внедрить предсказуемую multi-exchange регистрацию и выбор адаптера;
- добавить отдельный проект `CryptoTrader.Exchanges.Okx`;
- реализовать OKX V5 public REST и public WebSocket market data в пределах требований аудита;
- сохранить работоспособность существующего Bybit адаптера;
- корректно сохранять и различать рыночные данные нескольких бирж;
- покрыть изменения тестами и обновить документацию.

**В рамках этой задачи не реализовывать private API OKX, реальные ордера, отмену реальных ордеров, вывод средств, Futures execution, Futures Backtesting или Futures Paper Trading.** OKX SWAP допускается только как public market data при корректной идентификации и metadata; получение SWAP-котировок не означает поддержку торговли деривативами.

---

# 2. Обязательная модель Multi-Agent Team

Используй реальный механизм создания субагентов / agent teams, если он доступен в среде. Не имитируй наличие субагентов обычным текстовым рассуждением.

Создай команду из восьми специализированных субагентов. Семь первоначальных ролей сохраняются, восьмая роль добавляется для независимой проверки специфики OKX V5 API.

Если платформа не поддерживает субагентов:

1. Честно сообщи об ограничении.
2. Не заявляй, что агенты были созданы.
3. Продолжи работу в рамках восьми функциональных ролей последовательно.
4. Сохраняй отдельные задания, отчёты и статусы ролей в `docs/agents/`.

## 2.1. Главный агент — Lead Agent

**Роль:** Lead Software Architect / Engineering Manager / Integration Lead.

Ответственность:

- прочитать оба документа требований и изучить фактическое состояние репозитория;
- создать субагентов и назначить им роли;
- декомпозировать работу на независимые задачи;
- согласовать архитектуру и межмодульные контракты до параллельной реализации;
- определить владельца каждого модуля;
- избежать одновременного редактирования одних и тех же файлов;
- разрешать конфликты между субагентами;
- принимать изменения после code review и тестов;
- собирать результаты в рабочую интегрированную ветку;
- контролировать scope и не допускать незапрошенных функций;
- вести журнал решений и статусов;
- отвечать за финальный сквозной сценарий.

Lead Agent не должен без необходимости переписывать модули, назначенные профильным специалистам. Он отвечает за согласование и интеграцию результата.

## 2.2. Agent 1 — Senior .NET Architect

**Компетенции:** .NET 10, C#, Clean Architecture, Modular Monolith, DDD, SOLID, Hexagonal Architecture, Ports and Adapters, Dependency Injection, composition root, design patterns, domain modeling, API contracts, ADR, architecture testing, dependency rules.

**Задачи в рамках этой работы:**

1. Повторно проверить архитектуру текущей Solution.
2. Спроектировать multi-exchange architecture.
3. Определить контракт `IExchangeRegistry` и правила выбора адаптера по `ExchangeId`.
4. Проверить текущие `IExchangeAdapter`, `IMarketDataClient`, `IExchangeCapabilities` и специализированные интерфейсы.
5. Предложить capability-oriented подход для Order Book и Funding Rate, не заставляя биржу реализовывать неподдерживаемые функции.
6. Проверить границы Domain, Application, Infrastructure, Host и exchange projects.
7. Обеспечить отсутствие зависимости общих слоёв от Bybit/OKX SDK и DTO.
8. Проверить уникальность и стабильность `InstrumentId` между биржами.
9. Согласовать изменения `Instrument` / `InstrumentMetadata`, необходимые для contract metadata, не допуская фиктивных default values.
10. Проводить архитектурное ревью всех общих контрактов.

**Артефакты:**

- `docs/architecture.md`;
- ADR для registry/capabilities/instrument identity, если решения архитектурно значимы;
- схема зависимостей;
- заключение по AUD-01, AUD-05, AUD-06, AUD-07 и AUD-09.

**Полномочия:** отклонять изменения, привязывающие общий ingestion, Domain или стратегию к конкретной бирже, создающие неоднозначный DI resolution либо нарушающие направление зависимостей.

## 2.3. Agent 2 — Senior C# Developer

**Компетенции:** C#, .NET 10, ASP.NET Core, Generic Host, BackgroundService, async/await, CancellationToken, Channels, DI, HttpClientFactory, Options Pattern, EF Core integration, API, concurrency, thread safety, structured logging, xUnit.

**Задачи:**

1. Рефакторить Host composition root для регистрации нескольких бирж.
2. Реализовать общие application-level сценарии выбора биржи.
3. Убрать неоднозначное разрешение единственного `IMarketDataClient` при наличии нескольких адаптеров.
4. Переработать `CandleIngestionService` на конфигурационные subscriptions `Exchange + Instrument + Timeframe`.
5. Убрать зависимость общего ingestion от `BybitOptions` и hard-coded `ExchangeId.Bybit`.
6. Реализовать/интегрировать `IExchangeRegistry` согласно утверждённому контракту.
7. Сохранить существующее поведение Bybit.
8. Настроить options validation и понятные ошибки конфигурации.
9. Реализовать Health/diagnostic API по каждому exchange feed, если это входит в существующий API стиль.
10. Обеспечить CancellationToken и graceful shutdown для новых компонентов.

**Артефакты:** рабочие изменения Host/Application, интеграционные тесты своих сценариев, описание новых конфигураций.

## 2.4. Agent 3 — Quantitative Developer

**Компетенции:** quantitative finance, probability, statistics, time-series analysis, market data semantics, trading volume units, contract accounting concepts, funding rates, backtesting bias, Python, NumPy, pandas, SciPy.

**Задачи:**

1. Проверить семантику `Candle.Volume` и определить, допустимо ли единое поле для Spot и SWAP.
2. Определить безопасную политику обработки derivative volume: base volume, quote volume и contract volume нельзя смешивать.
3. Проверить mapping OKX funding rate и timestamps.
4. Проверить, какие поля instrument metadata обязательны для математически корректной интерпретации linear SWAP.
5. Подготовить формальные примеры unit conversion на реальных/официальных примерах OKX.
6. Подтвердить, что SWAP market data не попадает в Spot Backtesting/Paper Execution.
7. Оценить полноту исторических свечей и требования к отсутствию gaps/duplicates для Backtesting.
8. Проверить, что исторические свечи и live candle updates не создают look-ahead bias.

**Артефакты:** `docs/market-data-semantics.md` или соответствующий раздел документации, математические тест-кейсы, ревью mapping volume/contract metadata/funding.

**Граница ответственности:** не реализовывать общую интеграцию бирж и не менять самостоятельно доменную модель. Все изменения согласовывать с Agent 1 и Agent 2.

## 2.5. Agent 4 — Algorithmic Trading Systems Engineer

**Компетенции:** exchange API integration, market microstructure, REST, WebSocket, candles, trades, order book, event streams, reconnect, acknowledgements, subscription lifecycle, sequence IDs, rate limits, data normalization, idempotency, fault tolerance, market data health.

**Задачи:**

1. Владеть общей реализацией exchange adapter patterns.
2. Проанализировать текущий Bybit adapter и не допустить его регрессии.
3. Реализовать `CryptoTrader.Exchanges.Okx` совместно с Agent 8.
4. Реализовать общие OKX REST market-data клиенты и mapping.
5. Реализовать public WebSocket, subscription manager, ack processing, reconnect, heartbeat, resubscribe.
6. Ввести контролируемые очереди с bounded capacity/backpressure для потоков, где это необходимо.
7. Не терять беззвучно критичные события: candles, trades, order book updates.
8. Реализовать корректный per-exchange feed health status.
9. Реализовать безопасное восстановление после разрыва и обнаружения пропусков.
10. Поддержать snapshot order book; incremental order book включать только при корректной реализации sequence rules.
11. Реализовать funding rate public data для поддерживаемых SWAP instruments.
12. Обеспечить, чтобы OKX Trading/Account capabilities явно оставались unsupported в версии 0.1.

**Артефакты:** проект `CryptoTrader.Exchanges.Okx`, adapter implementation, fixture-based tests (совместно с QA), `docs/okx-integration.md`.

## 2.6. Agent 5 — Database Engineer

**Компетенции:** PostgreSQL, SQL, EF Core, Npgsql, migrations, indexes, constraints, transaction isolation, unique keys, bulk ingestion, time-series storage, data retention, checkpoint/replay, query performance, backup/restore.

**Задачи:**

1. Проверить, что instrument/candle identity учитывает биржу.
2. Проверить уникальность свечи по комбинации `Exchange + Instrument + Timeframe + OpenTime` (или согласованному эквиваленту).
3. При необходимости подготовить миграции для дополнительных metadata и volume semantics.
4. Проверить индексы и запросы с учётом нескольких бирж.
5. Предложить минимальный durable checkpoint/replay механизм для закрытых свечей, если аудит подтвердится.
6. Обеспечить идемпотентную запись данных и отсутствие смешивания свечей разных бирж.
7. Проверить migration compatibility с существующими данными Bybit.
8. Добавить integration tests на реальной PostgreSQL через Testcontainers, если среда это позволяет.
9. Обновить схему БД и документацию.

**Артефакты:** migration(s), database schema documentation, индексы, integration tests, backup/restore note при затрагивании эксплуатации.

**Ограничение:** не выполнять destructive migration и не удалять существующие данные. Миграции должны сохранять уже накопленную историю.

## 2.7. Agent 6 — QA Automation Engineer

**Компетенции:** test strategy, xUnit, FluentAssertions, Testcontainers, contract testing, API testing, WebSocket fixtures, property-based testing, regression testing, fault injection, deterministic tests, financial data testing.

**Задачи:**

1. Проверить каждый пункт предварительного аудита и присвоить ему фактический статус.
2. Подготовить тест-план для multi-exchange и OKX.
3. Создать локальные JSON fixtures на основе официальных форматов OKX V5.
4. Проверить parsing/mapping инструментов, свечей, тикеров, trades, book snapshots и funding rate.
5. Проверить pagination progress, completeness, duplicate handling и gaps.
6. Проверить `confirm = 0/1` и запрет использования незакрытых свечей в сценариях, требующих закрытия.
7. Проверить negative subscription acknowledgement, timeout ack, reconnect/resubscribe, heartbeat, malformed messages.
8. Проверить bounded-channel backpressure и отсутствие молчаливой потери критичных событий.
9. Проверить Registry: Bybit и OKX выбираются по ExchangeId независимо от порядка регистрации.
10. Проверить отсутствие смешивания данных двух бирж в БД.
11. Проверить отсутствие реальных торговых вызовов и private API.
12. Выполнить regression tests для Bybit.
13. Провести независимый review после интеграции.

**Артефакты:** `docs/testing-strategy.md`, тесты, QA report и перечень известных рисков.

**Полномочия:** блокировать завершение этапа при критичных дефектах, потере данных, неверном mapping финансовых величин или возможности непреднамеренно отправить реальный ордер.

## 2.8. Agent 7 — DevOps Engineer

**Компетенции:** Docker, Docker Compose, Linux, .NET deployment, CI/CD, GitHub Actions, PostgreSQL operations, secrets, health checks, OpenTelemetry, Prometheus, structured logs, backups, graceful shutdown, container security.

**Задачи:**

1. Проверить текущий CI pipeline и Docker configuration.
2. Обеспечить restore/build/test новых проектов в CI.
3. Добавить `dotnet format --verify-no-changes` или эквивалентный quality gate, если он не создаёт неразрешимый legacy failure.
4. Проверить .NET analyzers и стратегию warnings-as-errors поэтапно.
5. Добавить/проверить health status отдельно для Bybit и OKX feeds.
6. Убедиться, что конфигурация OKX public endpoints не требует credentials.
7. Проверить, что secrets не попадают в env examples, логи и Git.
8. Проверить запуск Docker Compose и migrations.
9. Обновить deployment/operations документацию.

**Артефакты:** CI changes, deployment docs, runbook, проверенные команды запуска.

## 2.9. Agent 8 — OKX V5 API Specialist (новый субагент)

**Зачем создаётся:** Agent 4 отвечает за инженерную реализацию адаптера в проекте CryptoTrader. Новый Agent 8 независимо проверяет специфику OKX V5, чтобы не переносить предположения из Bybit API и не допускать ошибок в форматах сообщений, пагинации, контрактной metadata и WebSocket protocol.

**Компетенции:**

- OKX API V5 REST;
- OKX API V5 WebSocket;
- public market data endpoints;
- instruments metadata (`SPOT`, `SWAP`);
- candlesticks и history-candles;
- candle `confirm` semantics;
- ticker/trades/order book;
- funding rate;
- `ctVal`, `ctMult`, `ctValCcy`, `settleCcy`, `ctType`, `lotSz`, `minSz`, `tickSz`;
- REST pagination (`after`/`before`) и rate limits;
- WebSocket subscribe/unsubscribe acknowledgements;
- heartbeat, ping/pong, reconnect и ограничения соединений;
- order book sequence semantics;
- API changelog и миграции протокола.

**Задачи:**

1. Использовать только официальную актуальную документацию OKX как первичный источник.
2. Проверить endpoint paths, query parameters, response schemas, candle intervals, pagination semantics и актуальные rate limits.
3. Проверить актуальные production public REST и WebSocket URLs.
4. Проверить отличие `market/candles` от `market/history-candles` и корректную стратегию загрузки полного диапазона.
5. Проверить точную семантику свечных полей и `confirm`.
6. Проверить units для Spot и SWAP volume.
7. Проверить contract metadata и ограничения инструментов.
8. Проверить актуальные имена WS channels и формат subscribe ack.
9. Проверить ограничения WebSocket connections/subscriptions, heartbeat и актуальные sequence rules order book.
10. Подготовить fixtures и mapping notes для Agent 4 и Agent 6.
11. Проверить реализацию Agent 4 на соответствие официальной спецификации.
12. Указать дату проверки документации и ссылки на точные разделы.

**Артефакты:** `docs/okx-api-reference-notes.md`, mapping tables, JSON fixtures или их спецификация, независимый API conformance review.

**Граница ответственности:** Agent 8 не переписывает самостоятельно общий exchange framework и не меняет общие контракты. Он предоставляет проверенные спецификации и ревью OKX implementation. Изменение общего API требует согласования с Agent 1.

---

# 3. Матрица ответственности (RACI-style)

| Область | Accountable / владелец | Обязательные reviewers |
|---|---|---|
| Multi-exchange architecture | Agent 1 | Lead, Agent 2, Agent 4, Agent 6 |
| Exchange registry | Agent 1 | Agent 2, Agent 4, Agent 6 |
| Host / DI / ingestion orchestration | Agent 2 | Agent 1, Agent 4, Agent 6 |
| OKX API specification | Agent 8 | Agent 4, Agent 6 |
| OKX adapter implementation | Agent 4 | Agent 8, Agent 2, Agent 6 |
| Bybit regression | Agent 4 | Agent 6 |
| Instrument identity / capabilities | Agent 1 | Agent 3, Agent 4, Agent 5, Agent 6 |
| Volume and contract semantics | Agent 3 | Agent 1, Agent 4, Agent 5, Agent 8 |
| PostgreSQL schema / migrations | Agent 5 | Agent 1, Agent 2, Agent 6 |
| Checkpoint / replay | Agent 5 | Agent 2, Agent 4, Agent 6 |
| Automated tests | Agent 6 | All agents provide test cases |
| CI / Docker / operations | Agent 7 | Agent 2, Agent 5, Agent 6 |
| Final integration and release decision | Lead | All agents |

Один модуль — один владелец. Reviewers проверяют результат, но не должны параллельно переписывать те же файлы без явного согласования.

---

# 4. Обязательный стартовый процесс

Перед написанием кода выполни следующие действия.

## 4.1. Проверка рабочего репозитория

1. Открой репозиторий и определи текущую ветку, commit SHA и состояние `git status`.
2. Убедись, что рабочая копия не содержит пользовательских изменений, которые будут затёрты.
3. Не выполняй `reset --hard`, `clean -fd`, force push или другие разрушительные Git-команды.
4. Создай отдельную рабочую ветку, например `feature/okx-multi-exchange`, если это соответствует принятому workflow.
5. Если ветка или рабочее дерево уже используются, не перезаписывай чужую работу.
6. Запиши baseline commit SHA в `docs/agents/development-status.md`.

## 4.2. Повторный аудит

Повторно проверь все AUD-01 — AUD-12 из `CryptoTrader_Audit_and_OKX_Integration.md` на актуальном коде.

Для каждого пункта создай запись:

| ID | Статус проверки | Файл/строка | Подтверждение | Решение | Owner |
|---|---|---|---|---|---|

Не меняй код из-за неподтверждённого предположения, пока не определён фактический риск.

Runtime-сценарии, которые нельзя подтвердить статически, пометь `Needs Runtime Verification` и добавь соответствующий тест или проверку.

## 4.3. Создание субагентов

Создай Agent 1–8 с ролями выше. Передай каждому:

- исходный audit/OKX документ;
- его персональную зону ответственности;
- список задач;
- перечень ожидаемых артефактов;
- список файлов/проектов, за которые он отвечает;
- запрет на несанкционированные изменения чужих модулей.

Попроси каждого агента сначала представить короткий implementation proposal и список затрагиваемых контрактов. Не запускай параллельное изменение shared interfaces до их утверждения Agent 1 и Lead.

## 4.4. Подготовка общего плана

Создай:

```text
docs/agents/
├── team-structure.md
├── responsibility-matrix.md
├── development-status.md
├── audit-verification.md
├── integration-contracts.md
├── implementation-plan.md
└── review-reports/
```

Статус задачи должен быть одним из:

- `Not Started`;
- `In Progress`;
- `Review`;
- `Blocked`;
- `Completed`.

Не ставь `Completed`, пока фактические изменения не проверены и тесты не пройдены.

---

# 5. Общие архитектурные и инженерные правила

Соблюдай существующий стиль и стандарты проекта. Не переписывай весь репозиторий ради идеальной архитектуры.

Обязательные принципы:

- SOLID;
- DDD — прагматично, без превращения каждой таблицы в агрегат;
- Clean Architecture;
- Ports and Adapters;
- KISS, DRY, YAGNI;
- Composition over Inheritance;
- Dependency Injection;
- explicit dependencies;
- async/await и `CancellationToken`;
- `decimal` для финансовых величин;
- UTC и `DateTimeOffset` для времени;
- structured logging;
- idempotent processing;
- fail-safe behavior;
- nullable reference types;
- deterministic tests.

Запрещено:

- переносить DTO или enum конкретной биржи в Domain;
- определять биржу по порядку регистрации DI;
- использовать service locator;
- делать singleton с незащищённым изменяемым торговым состоянием;
- молча проглатывать ошибки API или WebSocket;
- считать незакрытую свечу закрытой;
- смешивать Spot quantity и contract quantity;
- подставлять фиктивные contract metadata defaults;
- silently drop critical market events;
- считать REST HTTP 200 доказательством успешного OKX API response;
- повторять неидемпотентные операции без проверки их результата;
- включать private API или live orders в этой задаче.

Не вводи Kafka, RabbitMQ, Kubernetes или микросервисы. Для durable replay используй минимальный механизм, согласованный с текущей архитектурой и объёмом MVP.

---

# 6. Требования к multi-exchange архитектуре

## 6.1. Registry

Добавь `IExchangeRegistry` или эквивалентный контракт, согласованный Agent 1.

Ожидаемое поведение:

- `GetAll()` возвращает все доступные адаптеры;
- `GetRequired(exchangeId)` возвращает строго нужный адаптер либо бросает ясную configuration/domain error;
- `TryGet(exchangeId, out adapter)` позволяет проверить наличие;
- повторная регистрация одного `ExchangeId` — ошибка startup/configuration, не молчаливое перезаписывание;
- выбор адаптера не зависит от порядка DI-регистрации.

Не используй один общий `GetRequiredService<IMarketDataClient>()` для выбора биржи.

## 6.2. Capabilities

Возможности адаптера должны разделять как минимум:

- public market data;
- candles;
- ticker;
- trades;
- order book snapshot/stream;
- funding rate;
- public instruments metadata;
- private account;
- live execution.

Отсутствующая возможность должна явно возвращать `Unsupported`/`NotSupported` или отсутствовать в capability set. Нельзя создавать впечатление, что OKX account/live execution готов, если реализован только public feed.

## 6.3. Конфигурируемый ingestion

Общий ingestion получает subscriptions из конфигурации, например:

```json
{
  "MarketData": {
    "Subscriptions": [
      {
        "Exchange": "BYBIT",
        "Instrument": "BTCUSDT",
        "Timeframes": ["1h"]
      },
      {
        "Exchange": "OKX",
        "Instrument": "BTC-USDT",
        "Timeframes": ["1H", "4H"]
      }
    ]
  }
}
```

Это пример, а не требование дословно копировать схему. Согласуй naming с текущими options и доменной моделью.

Каждая subscription однозначно определяет Exchange, Instrument и Timeframe. Добавь startup validation:

- ExchangeId существует и включён;
- Instrument корректно разрешается для этой биржи;
- timeframe поддерживается;
- дубликаты subscriptions обнаруживаются;
- неизвестная биржа вызывает понятную ошибку конфигурации.

## 6.4. Candle identity и история

Для каждой свечи должны различаться:

- биржа;
- инструмент;
- timeframe;
- open time.

Уникальный ключ должен соответствовать этой комбинации. Не допускай перезаписи Bybit-свечи OKX-свечой.

Историческая загрузка должна возвращать явное состояние полноты: complete/incomplete либо эквивалент. Если период загружен не полностью, вызывающий код не должен считать его обычным успешным результатом. Backtesting должен отказываться работать с неполной историей без явно документированного override.

## 6.5. Backpressure и обработка событий

Для входящих потоков использовать bounded channel или эквивалентную управляемую очередь там, где producer может опережать consumer.

Политика зависит от типа события:

- закрытые candles, trades и incremental book updates нельзя молча терять;
- заменяемые snapshot-значения (например, последнее состояние ticker) могут coalesce только если это документировано и не ломает downstream semantics;
- при длительном перегрузе выставлять unhealthy/degraded состояние;
- блокировать стратегии, если market data stale или sequence invalid.

---

# 7. Требования к OKX V5 adapter

## 7.1. Новый проект

Создай:

```text
src/CryptoTrader.Exchanges.Okx/
    CryptoTrader.Exchanges.Okx.csproj
```

Namespace: `CryptoTrader.Exchanges.Okx`.

Добавь проект в текущую `CryptoTrader.slnx`, Package References централизуй в `Directory.Packages.props`, SDK pinning не меняй без необходимости.

Ориентируйся на стиль существующего `CryptoTrader.Exchanges.Bybit`, но не копируй биржеспецифичные предположения.

Предпочтительно использовать `HttpClient`/`IHttpClientFactory` и `ClientWebSocket`, если это соответствует существующему проекту. Не добавляй OKX SDK, если он не даёт доказанного преимущества. Если выбираешь стороннюю библиотеку, зафиксируй ADR, лицензию, совместимость .NET 10 и границы её DTO.

## 7.2. Scope API

В версии этой задачи реализовать только public endpoints:

- instruments metadata;
- recent market candles;
- historical market candles;
- ticker;
- recent trades, если это нужно существующим абстракциям;
- order book snapshot;
- funding rate для подходящих linear SWAP;
- public WebSocket channels.

Не реализовывать private REST, private WebSocket, balances, user orders, live order submission/cancellation, transfer или withdrawal.

Любой обязательный `ITradingClient` в интерфейсе должен fail-closed без сетевого вызова создания/отмены ордера и явно сообщать, что live trading disabled/unsupported.

## 7.3. Официальная спецификация

Agent 8 должен сверить endpoint paths, схемы JSON, параметры, интервалы, rate limits, channel names и sequence semantics с актуальной официальной OKX V5 documentation и changelog непосредственно перед реализацией.

Основной источник: `https://www.okx.com/docs-v5/en/` и официальный API changelog.

Не полагаться только на этот промпт или старые примеры. Все версии/изменения API фиксировать с датой проверки и ссылкой на официальный раздел в `docs/okx-api-reference-notes.md`.

## 7.4. Instruments и mapping

Поддержать public metadata для `SPOT` и `SWAP`.

Для Spot и Linear SWAP сохранять точный OKX `instId`, например `BTC-USDT` и `BTC-USDT-SWAP`. Не нормализовать symbol в Bybit-формат.

Проверять как минимум:

- `instId`;
- `instType`;
- `baseCcy`;
- `quoteCcy`;
- `settleCcy`;
- `ctType`;
- `ctVal`;
- `ctMult`;
- `ctValCcy`;
- `lotSz`;
- `minSz`;
- `tickSz`;
- `state`.

SWAP допускать к категории supported market data только если contract type и metadata корректно определены. Не считать inverse/dated/options supported.

InstrumentId должен быть уникальным между биржами и рынками. Используй принятый в Domain формат или предложи ADR, если текущая модель недостаточна.

Не допускать неизвестные состояния инструмента к торговой eligibility. Даже если live trading не входит в scope, корректный state должен быть отражён в metadata.

## 7.5. Contract metadata и объём

Не предполагай, что все OKX volume values имеют одну единицу.

Spot `vol` и SWAP `vol` имеют разную семантику. Сохраняй/нормализуй base volume, quote volume и contract volume раздельно либо явно ограничь общий candle feed так, чтобы `Candle.Volume` не имел неоднозначной единицы.

Если доменная модель не позволяет корректно хранить volume units и contract metadata:

1. Agent 3 описывает требуемую семантику.
2. Agent 1 предлагает минимальное расширение Domain.
3. Agent 5 реализует безопасную миграцию.
4. Agent 6 добавляет тесты.
5. SWAP не допускается в расчёты/backtesting/paper до завершения всех пунктов.

Не подставляй `1` для отсутствующих `ctVal`/`ctMult` и не считай SWAP contract quantity базовой валютой без доказанной формулы.

## 7.6. Candles и pagination

Использовать текущий candle endpoint для недавних данных и history endpoint для старого диапазона в соответствии с актуальной документацией.

OKX документирует ограничение количества свечей на запрос; перепроверь актуальный лимит при реализации. Реализовать pagination, которая:

- загружает весь запрошенный диапазон;
- учитывает семантику `after`/`before` именно OKX;
- не зацикливается при отсутствии продвижения cursor;
- дедуплицирует по времени;
- сортирует результат в требуемом порядке;
- проверяет границы from/to;
- определяет полноту диапазона и пропуски;
- не сообщает `Complete`, если достигнут искусственный лимит итераций или API больше не отдаёт ожидаемые страницы.

Парсить цены/объёмы из строк в `decimal` с invariant culture. Время преобразовывать в UTC.

Для стратегий, использующих закрытые свечи, учитывать `confirm` и не выдавать незакрытую свечу за финальную. Не использовать незакрытые свечи в Backtesting.

## 7.7. REST response/error handling

OKX response envelope содержит бизнес-код и сообщение. Успешный HTTP status сам по себе не доказывает успешность операции.

Обрабатывать:

- network errors;
- timeout;
- cancellation;
- non-success HTTP;
- OKX non-zero business code;
- malformed JSON;
- missing required fields;
- rate limit;
- empty data;
- incomplete pagination.

Логировать endpoint, status/code, correlation id и безопасное описание, но не credentials/secrets/signatures.

Retry разрешён только для подходящих идемпотентных public GET запросов и с bounded exponential backoff + jitter. Учитывать актуальные headers/Retry-After, если предоставляются.

Не переносить лимиты Bybit на OKX. `OkxRateLimiter` должен учитывать endpoint-specific scopes/buckets согласно актуальной документации.

## 7.8. WebSocket

Использовать актуальные production/demo public WebSocket URLs из официальной документации. Не полагаться на устаревший endpoint/port.

Поддержать необходимые каналы из существующих потребностей приложения: свечи, ticker, trades, snapshot order book; точные channel names и поддерживаемые intervals Agent 8 обязан проверить.

Subscription manager должен:

- держать желаемый список подписок;
- отправлять subscribe/unsubscribe с rate limiting;
- различать event ack и market data payload;
- сопоставлять ack с ожидаемым channel/instrument;
- обрабатывать отрицательный ack как реальную ошибку;
- обрабатывать отсутствие ack по timeout;
- поддерживать ping/pong/heartbeat;
- reconnection с bounded exponential backoff и jitter;
- повторно подписываться после reconnect;
- применять CancellationToken и корректный shutdown;
- выставлять per-channel/per-feed health state.

Если реализуется incremental order book, обязательно следовать актуальным sequence rules и восстанавливать полный snapshot при sequence gap. Если этого нельзя гарантировать в scope, оставить только snapshot/snapshot-style канал и явно указать ограничение.

## 7.9. Capabilities и запрет торговли

OKX в рамках этой задачи:

- Public REST market data: supported;
- Public WebSocket market data: supported;
- Spot instrument metadata: supported;
- Linear SWAP metadata/market data: только при корректных units/contract metadata;
- Funding rate: supported для подходящих SWAP;
- Private account: unsupported;
- Private WebSocket: unsupported;
- Live execution: disabled/unsupported.

Не реализовывать заглушки, которые возвращают фиктивный account/balance/order state. При вызове неподдерживаемого private/live метода возвращать ясный результат/исключение, соответствующее текущему проектному стилю, без сетевого обращения.

---

# 8. Исправление аудиторских замечаний

Используй матрицу ниже. Изменения выполнять только после подтверждения актуальности замечания.

| ID | Ожидаемое действие | Владелец | Reviewers |
|---|---|---|---|
| AUD-01 | Убрать Bybit coupling в Host/ingestion | Agent 2 | Agent 1, 4, 6 |
| AUD-02 | Полная pagination + explicit completeness для Bybit и OKX | Agent 4 | Agent 8, 3, 6 |
| AUD-03 | Bounded queues/backpressure и telemetry | Agent 4 | Agent 2, 6 |
| AUD-04 | Subscription ack lifecycle/retry/health | Agent 4 | Agent 8, 6 |
| AUD-05 | IExchangeRegistry, uniqueness, deterministic resolution | Agent 1 | Agent 2, 4, 6 |
| AUD-06 | Capability-oriented OrderBook/Funding interfaces | Agent 1 | Agent 3, 4, 8 |
| AUD-07 | Config-driven multi-exchange ingestion | Agent 2 | Agent 1, 4, 6 |
| AUD-08 | Durable checkpoint/replay или эквивалент | Agent 5 | Agent 2, 4, 6 |
| AUD-09 | Явно ограничить SWAP до data/metadata без futures accounting | Agent 3 | Agent 1, 4, 5, 6 |
| AUD-10 | Обновить docs по реальному состоянию | Agent 7 (координация), владельцы разделов | Agent 1, 6 |
| AUD-11 | Добавить recovery/API/data pipeline tests | Agent 6 | Все владельцы модулей |
| AUD-12 | Улучшить format/analyzer/CI quality gates постепенно | Agent 7 | Agent 2, 6 |

Для каждого замечания обнови `docs/agents/audit-verification.md` и укажи итог: исправлено, уже исправлено, не воспроизведено или отложено с обоснованием.

---

# 9. Фазы реализации и параллельная работа

## Phase 0 — Repository baseline и команда

**Lead + все агенты.**

- Проверить `git status`, branch и baseline commit.
- Прочитать документы.
- Создать восемь субагентов.
- Выполнить повторную проверку AUD-01…AUD-12.
- Зафиксировать ownership и backlog.
- Подготовить ADR proposal.

**Выход:** утверждённый implementation plan и перечень подтверждённых задач.

## Phase 1 — Архитектура multi-exchange

**Основные:** Agent 1, Agent 2, Agent 4. **Review:** Agent 3, Agent 5, Agent 6, Agent 8.

- Согласовать ExchangeId и registry.
- Согласовать capabilities.
- Согласовать instrument identity и metadata.
- Переработать ingestion subscriptions.
- Определить error/health semantics.
- Утвердить contracts до реализации адаптера.

**Критерий:** Bybit работает через новый registry; никаких неоднозначных DI resolutions; общие компоненты не содержат hard-coded Bybit dependency.

## Phase 2 — OKX specification и adapter skeleton

**Основные:** Agent 8 и Agent 4. **Review:** Agent 1, Agent 6.

- Проверить официальный OKX V5 API.
- Создать новый project.
- Подготовить options, REST client, error model и mappers.
- Подготовить JSON fixtures.
- Создать registration extension.

**Критерий:** проект входит в Solution и проходит build; endpoint/schema assumptions документированы и подтверждены официальной спецификацией.

## Phase 3 — OKX REST market data

**Основные:** Agent 4, Agent 8. **Support:** Agent 3, Agent 5, Agent 6.

- Instruments metadata.
- Candles + full pagination.
- Ticker.
- Trades при необходимости контракта.
- Order book snapshot.
- Funding rate.
- Volume and contract metadata semantics.
- REST rate limiting and error handling.

**Критерий:** все mapping/unit tests проходят; smoke test public REST выполнен отдельно от стандартного CI и использует только публичные endpoints.

## Phase 4 — OKX WebSocket

**Основные:** Agent 4, Agent 8. **Review:** Agent 6, Agent 2.

- Public connection.
- Subscription manager.
- Ack/error handling.
- Heartbeat.
- Reconnect/resubscribe.
- Bounded queues/backpressure.
- Feed health.
- Snapshot order book или корректный incremental order book.

**Критерий:** fixture/fake WebSocket tests проходят; после disconnect/reconnect ожидаемые subscriptions восстановлены; stale data не помечаются healthy.

## Phase 5 — Database и end-to-end ingestion

**Основные:** Agent 2, Agent 5. **Support:** Agent 4, Agent 6.

- Multi-exchange candle key.
- Конфигурируемые subscriptions.
- Персистентность Bybit и OKX.
- Checkpoint/replay, если подтверждена потребность.
- Per-exchange health.
- Idempotent processing.

**Критерий:** Host одновременно получает и сохраняет свечи обеих бирж; данные не смешиваются; restart/replay не создаёт дубликатов или пропуска закрытых свечей.

## Phase 6 — Regression, docs, CI и release readiness

**Основные:** Agent 6, Agent 7, Agent 1. **Участники:** все агенты.

- Полный regression Bybit.
- Contract tests для обеих бирж.
- Integration tests с PostgreSQL.
- CI formatting/analyzers.
- Docker Compose.
- Миграции.
- Обновление README/docs.
- Security review.
- Release report.

**Критерий:** сборка/тесты/compose/health checks пройдены там, где среда поддерживает их выполнение; оставшиеся непроверенные runtime-сценарии явно перечислены.

---

# 10. Требования к тестированию

Минимальный список тестов:

## 10.1. Registry и multi-exchange

- Bybit и OKX зарегистрированы одновременно.
- Lookup по ExchangeId возвращает нужный adapter.
- Результат не зависит от порядка регистрации.
- Дубликат ExchangeId приводит к понятной ошибке.
- Неизвестный ExchangeId приводит к понятной ошибке.
- Отключённый adapter не начинает ingestion.

## 10.2. Instrument и persistence

- одинаковая торговая пара разных бирж имеет разную identity;
- Spot и SWAP instruments различаются;
- существующие Bybit IDs не ломаются без необходимости миграции;
- unique candle key включает exchange;
- повторная запись той же свечи идемпотентна;
- свечи двух бирж не перезаписывают друг друга.

## 10.3. REST и pagination

- OKX success envelope;
- non-zero business code;
- HTTP error;
- timeout/cancellation;
- malformed JSON;
- missing fields;
- decimal parsing;
- timestamp UTC;
- correct recent/history endpoint selection;
- pagination reaches requested range;
- cursor progress check prevents infinite loop;
- incomplete range never appears complete;
- duplicates removed;
- gaps detected or surfaced according to documented policy;
- rate-limit/backoff behavior.

## 10.4. Market data semantics

- Spot `vol` semantics;
- SWAP contract volume semantics;
- quote/base volume normalization;
- contract metadata mapping;
- unsupported inverse/dated instruments are rejected or capability-filtered;
- no fallback fake multiplier;
- candle `confirm` parsing;
- ticker/trades/book mapping;
- funding rate/time mapping.

## 10.5. WebSocket

- subscribe ack success;
- subscribe ack rejection;
- ack timeout;
- market update parse;
- ping/pong;
- reconnect;
- resubscribe;
- malformed event;
- duplicate message behavior;
- stale feed status;
- bounded queue backpressure;
- cancellation/shutdown;
- order book sequence gap and snapshot recovery, если incremental book реализован.

## 10.6. Regression и безопасность

- существующие Bybit tests проходят;
- Bybit ingestion остаётся рабочим через Registry;
- private API не вызывается;
- нет реального order submission/cancellation;
- неподдерживаемые capabilities явно disabled/unsupported;
- secrets не присутствуют в logs, fixtures, git diff и docs.

Тесты по умолчанию не должны зависеть от внешней сети. Используй fixtures, mocks/fakes и Testcontainers. Public API smoke tests должны быть отдельным opt-in test category/profile и не запускаться автоматически в обычном CI.

---

# 11. Definition of Done

Задача может быть объявлена завершённой только при выполнении всех применимых критериев:

1. Все AUD пункты перепроверены и имеют фактический статус.
2. `CryptoTrader.Exchanges.Okx` добавлен в Solution.
3. Bybit и OKX используют единый registry.
4. Общий ingestion не зависит от конкретной биржи.
5. Subscriptions конфигурируются по Exchange/Instrument/Timeframe.
6. Registry не зависит от порядка DI registrations.
7. OKX REST public endpoints реализованы и протестированы.
8. OKX WebSocket public channels реализованы и протестированы.
9. Pagination корректна и сообщает о неполной истории.
10. `confirm` учитывается корректно.
11. Volume и contract metadata имеют однозначную семантику.
12. SWAP не попадает в неподдерживаемые Spot расчёты.
13. OKX data сохраняются с `ExchangeId.Okx`.
14. Данные Bybit и OKX не смешиваются.
15. Ошибки subscribe/reconnect обрабатываются явно.
16. Очереди имеют управляемый размер или документированную стратегию backpressure.
17. Funding Rate доступен как capability для поддерживаемых SWAP.
18. Private API и реальные торговые операции отсутствуют.
19. Существующие Bybit tests проходят.
20. Новые unit/contract/integration tests проходят.
21. Документация соответствует фактическому коду.
22. CI проверяет новые проекты.
23. `dotnet build -c Release` проходит.
24. `dotnet test -c Release` проходит.
25. Docker Compose и migrations проверены, если среда поддерживает Docker.
26. Нет секретов в diff и логах.
27. QA выдал acceptance report.
28. Lead Agent подтвердил сквозной сценарий multi-exchange ingestion.

---

# 12. Команды проверки

Выполни команды из корня актуальной Solution, адаптируя их к фактическому имени файла:

```bash
dotnet restore CryptoTrader.slnx
dotnet build CryptoTrader.slnx -c Release --no-restore
dotnet test CryptoTrader.slnx -c Release --no-build
```

Дополнительно:

```bash
dotnet format CryptoTrader.slnx --verify-no-changes
```

Если format/analyzer gate выявляет существующие legacy warnings, не отключай его без анализа: зафиксируй baseline, отдели новые нарушения от старых и предложи поэтапное исправление.

Выполни проверку Docker Compose и миграций, если доступны Docker и нужные зависимости. Не заявляй об успешной проверке, если команда фактически не запускалась.

---

# 13. Документация и отчётность

Обновить по фактическому состоянию:

- `README.md`;
- `docs/architecture.md`;
- `docs/testing-strategy.md`;
- `docs/security.md`;
- `docs/operations-runbook.md`;
- создать/обновить `docs/exchange-integration.md`;
- создать `docs/okx-integration.md`;
- создать `docs/okx-api-reference-notes.md`;
- создать `docs/market-data-semantics.md`, если модель volume/contract metadata этого требует.

В документации использовать статусы `Implemented`, `Partially Implemented`, `Not Implemented`.

В конце работы Lead Agent предоставляет отчёт:

1. Baseline commit и рабочая ветка.
2. Какие субагенты реально созданы.
3. Что проверено по каждому AUD-01…AUD-12.
4. Какие замечания подтверждены/не воспроизведены/исправлены.
5. Какие файлы и проекты изменены.
6. Какая реализация OKX добавлена.
7. Какие OKX endpoints/channels реально поддерживаются.
8. Ограничения Spot/SWAP и volume semantics.
9. Результаты build/test/format/Compose.
10. Результаты QA и security review.
11. Непроверенные runtime-сценарии и технический долг.
12. Точные команды запуска и smoke tests.
13. Следующие рекомендованные задачи.

Не скрывай проваленные тесты или непроверенные сценарии. Не придумывай результаты.

---

# 14. Правила взаимодействия команды

1. Сначала согласовать общие contracts, затем реализовывать адаптеры.
2. Не допускать нескольких владельцев одного файла/модуля.
3. Использовать отдельные ветки/worktrees, если платформа поддерживает безопасную параллельную работу.
4. Не объединять изменения без review и тестов.
5. Архитектурные изменения фиксировать ADR.
6. Изменения общей модели согласовывать с Agent 1.
7. OKX protocol questions адресовать Agent 8.
8. Финансовую семантику volume/contract metadata проверяет Agent 3.
9. Persistence и миграции проверяет Agent 5.
10. Критичные сценарии независимо проверяет Agent 6.
11. CI/Compose/operability проверяет Agent 7.
12. Финальную интеграцию и scope контролирует Lead Agent.

Если агент заблокирован, Lead Agent должен определить причину, перераспределить зависимые задачи или дать точечное решение. Не оставляй всю команду в ожидании одного несвязанного компонента.

---

# 15. Финальная команда

**Начинай с проверки текущего репозитория и создания восьми специализированных субагентов. Затем выполни повторную верификацию статического аудита, утверди multi-exchange контракты и только после этого приступай к реализации.**

Не ограничивайся планом, отчётом или пустым проектом. Доведи изменения до интегрированного и протестированного состояния в пределах доступной среды.

**Ключевое условие успеха:** Bybit и OKX одновременно работают через единый exchange registry и конфигурируемый ingestion pipeline; данные обеих бирж корректно нормализуются и сохраняются отдельно; торговая логика не зависит от конкретной биржи; private API и live trading остаются выключенными.
