# CryptoTrader: аудит первой версии и техническое задание на интеграцию OKX

**Дата анализа:** 27 ноября 2026 г.  
**Репозиторий:** <https://github.com/AndrewLomov/CryptoTrader>  
**Целевая ветка:** `main`  
**Новый проект:** `CryptoTrader.Exchanges.Okx`  
**Целевая платформа:** .NET 10

---

## 1. Цель документа

Документ решает две задачи:

1. Фиксирует результаты статического анализа первой опубликованной версии CryptoTrader: найденные недостатки, архитектурные риски и ограничения текущего MVP.
2. Определяет конкретный объём работ по добавлению адаптера биржи OKX в виде отдельного проекта `CryptoTrader.Exchanges.Okx`, с повторным использованием существующих абстракций и без дублирования торгового ядра.

Документ предназначен для разработчика или AI coding agent, который будет непосредственно изменять репозиторий.

## 2. Область и ограничения аудита

Анализ выполнен по доступным исходным файлам и документации публичной ветки `main`: структуре Solution, проектам Domain/Application/Infrastructure/Host, Bybit-адаптеру, ingestion worker, стратегии, Backtesting, тестам, CI и deployment-документации.

Это **статический code review**. В рамках данного анализа не выполнялись `dotnet build`, `dotnet test`, запуск Docker Compose, подключение к бирже или нагрузочные испытания. Поэтому выводы о поведении в runtime, которые требуют исполнения, отмечены как риски или рекомендации, а не как подтверждённые runtime-дефекты.

Перед слиянием изменений обязательно выполнить локальную сборку и тесты на актуальном состоянии репозитория.

---

# Часть I. Результаты аудита

## 3. Краткая оценка текущего состояния

В репозитории уже есть полезный фундамент:

- .NET 10 и централизованное управление NuGet-пакетами;
- модульный монолит с разделением Domain, Application, Infrastructure, Host;
- отдельные абстракции бирж и стратегий;
- Bybit market-data adapter на REST и WebSocket;
- PostgreSQL и EF Core;
- Trend Following strategy;
- Backtesting и Paper Trading;
- API Host, Docker Compose, CI и набор тестовых проектов.

Это хорошая основа для расширения. Однако текущая реализация ещё не является полностью multi-exchange платформой: значительная часть orchestration и конфигурации привязана к Bybit. Добавление одного нового adapter project без рефакторинга этих связей не обеспечит полноценную работу OKX в ingestion pipeline.

## 4. Сводная таблица замечаний

| ID | Приоритет | Замечание | Последствие | Рекомендуемое действие |
|---|---|---|---|---|
| AUD-01 | P1 | Host и ingestion жёстко привязаны к Bybit | OKX можно зарегистрировать, но он не станет полноценным источником данных автоматически | Ввести registry адаптеров и конфигурируемые subscriptions по биржам |
| AUD-02 | P1 | Историческая загрузка Bybit может вернуть неполный диапазон без ошибки | Backtest может получить неполные данные и дать неверный результат | Явно сигнализировать о неполной истории; добавить cursor-based pagination до границы диапазона |
| AUD-03 | P1 | WebSocket stream использует unbounded channel | При замедлении consumer очередь может расти без ограничения | Ввести bounded channel и политику backpressure; не терять критичные события |
| AUD-04 | P1 | Ошибка подтверждения WebSocket subscription не должна оставаться только записью в лог | Клиент может оставаться подключённым, но не получать ожидаемые данные | Проверять subscription acknowledgements и восстанавливать неуспешные подписки |
| AUD-05 | P1 | Нет унифицированного реестра адаптеров; регистрация общих интерфейсов нескольких бирж станет неоднозначной | `GetService<IMarketDataClient>()` не определяет конкретную биржу предсказуемо | Ввести `IExchangeRegistry`, обращаться к client по `ExchangeId` |
| AUD-06 | P2 | Общие контракты не полностью покрывают Order Book и Funding Rate | Разные адаптеры будут экспонировать возможности разными способами | Добавить capability-oriented интерфейсы и предоставить их через адаптер/registry |
| AUD-07 | P2 | Ingestion worker ориентирован на одну биржу и один жёстко заданный набор timeframe | Невозможно независимо конфигурировать источники, инструменты и интервалы | Сделать subscriptions конфигурационными: Exchange + Instrument + Timeframe |
| AUD-08 | P2 | В pipeline нет явно описанного durable replay для ошибки после сохранения свечи | Возможна потеря обработки сигнала при временной ошибке downstream-компонента | Добавить checkpoint/outbox или повторяемую обработку сохранённых закрытых свечей |
| AUD-09 | P2 | Backtesting фактически поддерживает Spot, а не полноценные Futures | Нельзя считать Futures-торговлю реализованной только из-за наличия типа LinearPerpetual в Domain | Явно ограничить Futures в Backtest/Paper до реализации margin, funding и contract accounting |
| AUD-10 | P2 | Документация архитектуры и тестирования не соответствует текущему коду | Разработчик и агент могут принять реализованные компоненты за отсутствующие | Синхронизировать `docs/architecture.md`, `docs/testing-strategy.md` и README с кодом |
| AUD-11 | P2 | Недостаточно автоматизированных тестов сценариев восстановления и API | Критичные ошибки могут остаться незамеченными | Добавить тесты reconciliation, reconnect, paper execution и API authorization |
| AUD-12 | P2 | CI не обеспечивает строгую проверку форматирования и warnings-as-errors | Ошибки анализаторов и предупреждения могут попадать в main | Постепенно включить строгие quality gates и форматирование в CI |

Приоритеты: **P1** — исправить до полноценного подключения OKX; **P2** — желательно исправить в рамках этой интеграции или ближайшего технического релиза.

## 5. Подробные замечания

### AUD-01. Привязка Host и ingestion к Bybit — P1

**Наблюдение**

- В `CryptoTrader.Host/Program.cs` вызывается регистрация `AddBybit(...)`.
- `CandleIngestionService` получает `BybitOptions` и использует конфигурацию Bybit.
- В worker присутствуют обращения, предполагающие `ExchangeId.Bybit`.
- Worker разрешает общий `IMarketDataClient` через DI, что станет неоднозначным после регистрации нескольких реализаций одного интерфейса.

**Риск**

Добавление `AddOkx()` не гарантирует, что ingestion начнёт получать и сохранять данные OKX. При регистрации нескольких `IMarketDataClient` обычное разрешение одного сервиса из DI может вернуть последнюю регистрацию, а не нужную биржу.

**Исправление**

1. Добавить `IExchangeRegistry`.
2. Регистрировать каждый адаптер по уникальному `ExchangeCode` / `ExchangeId`.
3. Убрать из `CandleIngestionService` зависимость от `BybitOptions`.
4. Убрать hard-coded `ExchangeId.Bybit`.
5. Хранить список подписок в общей конфигурации `MarketData:Subscriptions`.
6. Каждая подписка должна однозначно указывать биржу, инструмент и timeframe.

Пример контракта:

```csharp
public interface IExchangeRegistry
{
    IReadOnlyCollection<IExchangeAdapter> GetAll();

    IExchangeAdapter GetRequired(ExchangeId exchangeId);

    bool TryGet(ExchangeId exchangeId, out IExchangeAdapter? adapter);
}
```

Не использовать `IEnumerable<IMarketDataClient>` как единственный механизм выбора: связь между client и конкретной биржей должна оставаться явной.

### AUD-02. Ограниченная историческая выгрузка — P1

**Наблюдение**

В Bybit market-data client задан предел `MaxKlinePages = 20`. При достижении лимита код пишет предупреждение, но уже загруженные строки могут быть возвращены вызывающему коду как обычный результат.

**Риск**

Для большого временного диапазона backtest может незаметно работать на неполной истории. Это способно исказить доходность, drawdown и количество сделок.

**Исправление**

- Сделать полноту диапазона явной частью результата загрузки.
- Если диапазон не загружен полностью, возвращать диагностическую ошибку/статус `Incomplete`, а не обычный успешный результат.
- Использовать cursor-based pagination до достижения `from`/`to`.
- Контролировать прогресс курсора, чтобы избежать бесконечного цикла.
- Дедуплицировать свечи по `(exchange, instrument, timeframe, openTime)`.
- Проверять пропуски и дубликаты.
- Для backtest запрещать использование неполного набора без явного override.

OKX имеет собственные ограничения пагинации: endpoint `market/candles` возвращает максимум 300 свечей за запрос и ограничен последними 1 440 свечами; для более старых данных предназначен `market/history-candles`. Это должно быть учтено в OKX adapter, а не скрыто за одним REST-вызовом.

### AUD-03. Неограниченная WebSocket-очередь — P1

**Наблюдение**

В Bybit stream используется `Channel.CreateUnbounded`.

**Риск**

Если скорость поступления market-data сообщений превысит скорость обработки, очередь будет расти без верхнего лимита и может привести к чрезмерному расходу памяти.

**Исправление**

- Для критичных событий использовать bounded channel с backpressure (`BoundedChannelFullMode.Wait`) либо эквивалентную управляемую очередь.
- Не выбрасывать свечи, исполнения и изменения стакана молча.
- Для coalescible telemetry (например, последний ticker snapshot) допускается отдельная политика замены устаревшего значения, если это явно документировано.
- Добавить метрики глубины очереди, времени ожидания и переполнения.
- При устойчивом перегрузе фиксировать деградацию и блокировать сигналы, если актуальность данных потеряна.

Не применять одну политику drop для всех типов market events.

### AUD-04. Подтверждение WebSocket подписки — P1

**Наблюдение**

В Bybit stream обработка отрицательного ответа на subscribe логирует ошибку, но не должна считаться успешным завершением подписки.

**Исправление**

- Вести состояние каждой ожидаемой подписки: `Pending`, `Active`, `Rejected`.
- Сопоставлять ack с ожидаемым topic/channel.
- По таймауту отсутствующего ack считать подписку неуспешной.
- Повторять подписку с ограниченным backoff и jitter.
- Если обязательная подписка отклонена, сообщать об этом ingestion service и переводить источник в unhealthy.
- Не считать соединение healthy только потому, что WebSocket TCP-сессия открыта.

### AUD-05. Реестр адаптеров — P1

После добавления OKX нужно отказаться от предположения, что в приложении существует только один `IMarketDataClient`.

**Требования**

- `ExchangeId` должен поддерживать `Okx` наряду с `Bybit`.
- `IExchangeRegistry` должен хранить адаптеры по уникальному идентификатору биржи.
- Не должно быть зависимости от порядка DI-регистраций.
- При неизвестном `ExchangeId` возвращать явную ошибку конфигурации.
- При дублировании одного `ExchangeId` регистрация должна завершаться ошибкой, а не молча перезаписывать адаптер.

### AUD-06. Пробелы в биржевых абстракциях — P2

В текущих контрактах `IMarketDataClient` покрывает базовые market-data операции, но получение стакана и Funding Rate доступны не как полностью унифицированные возможности общего адаптера.

**Рекомендация**

Применить Interface Segregation Principle и выделить специализированные интерфейсы:

```csharp
public interface IOrderBookClient
{
    Task<OrderBookSnapshot> GetSnapshotAsync(
        InstrumentId instrument,
        CancellationToken cancellationToken);
}

public interface IFundingRateClient
{
    Task<FundingRate?> GetCurrentAsync(
        InstrumentId instrument,
        CancellationToken cancellationToken);
}
```

Не заставлять все биржи реализовывать неподдерживаемые операции. Доступность возможностей должна отражаться в `ExchangeCapabilities` и/или capability registry.

Если `IFundingRateClient` уже существует, не создавать его дубликат: расширить регистрацию и способ доступа через биржевой registry.

### AUD-07. Конфигурируемый ingestion — P2

Вместо отдельных worker для каждой биржи использовать общий механизм подписок.

Пример целевой конфигурации:

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

Фактическая схема должна соответствовать существующей системе options/validation. Нельзя жёстко кодировать список символов в worker.

Ключ хранения свечи должен включать биржу, чтобы BTC-USDT с разных площадок никогда не смешивались.

### AUD-08. Повторяемая обработка событий — P2

В ingestion pipeline нужно отдельно определить поведение при ситуации:

1. Свеча успешно сохранена.
2. Обработка сигнала или обновление Paper Trading завершились ошибкой.
3. Worker записал ошибку и продолжил обработку следующей свечи.

Нужно гарантировать, что закрытая свеча не потеряется для downstream-обработки.

Варианты:

- таблица `MarketDataProcessingCheckpoint`;
- transactional outbox для события о сохранённой свече;
- повторно запускаемый consumer, читающий необработанные закрытые свечи;
- идемпотентный обработчик с уникальным ключом `(strategyInstance, instrument, timeframe, candleOpenTime)`.

Для MVP достаточно простого устойчивого checkpoint/replay механизма. Внедрение отдельного брокера сообщений не требуется.

### AUD-09. Futures не реализованы на уровне экономики портфеля — P2

В Domain присутствует тип `LinearPerpetual`, однако текущий Backtesting engine ограничен Spot, а funding и полноценная модель маржи/ликвидации не реализованы.

Это важно учитывать при OKX:

- получение SWAP-инструментов и funding data не означает, что платформа умеет корректно симулировать perpetual trading;
- нельзя рассчитывать PnL линейного контракта как обычный Spot PnL;
- нужно учитывать `ctVal`, `ctMult`, `ctValCcy`, размер контракта и валюту расчёта;
- необходимы initial/maintenance margin, unrealized PnL, funding payments и liquidation rules.

**В рамках текущего расширения:** разрешить public market data и instrument metadata для OKX SPOT и подходящих linear SWAP, но не передавать SWAP в Spot Backtesting/Paper Execution. Futures simulation должна оставаться явно неподдерживаемой до отдельной реализации и тестирования.

### AUD-10. Устаревшая документация — P2

`docs/architecture.md` перечисляет компоненты как отсутствующие, хотя в репозитории уже есть соответствующие проекты и реализации: Bybit adapter, EF Core, strategy, Backtesting, Risk Manager/Paper components, Host и deployment files.

`docs/testing-strategy.md` также содержит утверждения, которые необходимо перепроверить относительно текущего `Program.cs` и существующих тестов.

**Исправление**

После OKX интеграции обновить:

- `README.md`;
- `docs/architecture.md`;
- `docs/testing-strategy.md`;
- `docs/exchange-integration.md`;
- `docs/security.md`;
- `docs/operations-runbook.md`.

Документы должны описывать фактический код и явно разделять `Implemented`, `Partially Implemented`, `Not Implemented`.

### AUD-11. Тестовые пробелы — P2

Расширить тестирование следующими сценариями:

- WebSocket reconnect и восстановление подписок;
- отрицательный subscribe ack;
- потеря последовательности стакана;
- неизвестный статус ордера после timeout;
- reconciliation после перезапуска;
- повторная обработка закрытой свечи;
- Paper Execution и частичное исполнение;
- API authentication/authorization;
- отказ PostgreSQL;
- восстановление из backup;
- отсутствие секретов в логах.

Для OKX все тесты парсинга и mapping должны работать на локальных JSON fixtures и не обращаться к реальной бирже.

### AUD-12. Quality gates в CI — P2

В `Directory.Build.props` сейчас не включён режим `TreatWarningsAsErrors`. CI должен постепенно стать строже.

Рекомендуемые шаги:

1. Добавить `dotnet format --verify-no-changes`.
2. Включить анализаторы .NET и фиксировать их severity.
3. Устранять предупреждения поэтапно.
4. После очистки включить `TreatWarningsAsErrors=true` хотя бы для новых проектов, затем для Solution целиком.
5. Добавить проверку зависимостей и уязвимостей NuGet.
6. Добавить API smoke test после запуска Host.

Не включать жёсткий режим предупреждений до устранения существующих предупреждений без анализа: это может необоснованно заблокировать разработку.

---

# Часть II. Техническое задание на OKX Adapter

## 6. Цель интеграции

Добавить отдельный проект:

```text
src/CryptoTrader.Exchanges.Okx/
    CryptoTrader.Exchanges.Okx.csproj
```

Namespace:

```csharp
CryptoTrader.Exchanges.Okx
```

Адаптер должен подключаться к публичным API OKX, нормализовать market data и предоставлять её через существующие абстракции CryptoTrader.

**В версии 0.1 OKX интегрируется только для публичных рыночных данных. Реальные ордера, приватный аккаунт API и вывод средств не реализовывать.**

## 7. Объём поддержки OKX в версии 0.1

| Возможность | Требование |
|---|---|
| Public REST | Реализовать |
| Public WebSocket | Реализовать |
| Список Spot instruments | Реализовать |
| Список linear SWAP instruments | Реализовать с корректной metadata |
| Исторические свечи | Реализовать с pagination |
| Ticker | Реализовать |
| Recent trades | Реализовать, если общий контракт использует этот feed |
| Order book snapshot | Реализовать |
| Order book incremental stream | Не обязателен для первого инкремента; если реализован, соблюдать sequence rules |
| Funding rate | Реализовать для подходящих SWAP instruments |
| Private REST | Не реализовывать |
| Private WebSocket | Не реализовывать |
| Account/balance | Не реализовывать |
| Live order submission | Запрещён |
| Live order cancellation | Запрещён |
| Futures Backtesting/Paper | Не включать до отдельной модели деривативов |

Адаптер должен корректно сообщать capabilities:

- Spot market data: supported;
- Linear SWAP market data: supported, если инструмент прошёл проверку типа контракта;
- Public WebSocket: supported;
- Funding rates: supported для подходящих SWAP;
- Private account: unsupported;
- Live orders: disabled.

Если существующая модель `ExchangeCapabilities` не разделяет market-data support и execution support, расширить её так, чтобы нельзя было спутать получение SWAP-котировок с готовностью торговать SWAP.

## 8. Архитектура проекта

Рекомендуемая структура:

```text
src/CryptoTrader.Exchanges.Okx/
│
├── Configuration/
│   ├── OkxOptions.cs
│   └── OkxEnvironment.cs
│
├── DependencyInjection.cs
├── OkxExchangeAdapter.cs
│
├── Rest/
│   ├── OkxPublicRestClient.cs
│   ├── OkxApiResponse.cs
│   ├── OkxApiError.cs
│   ├── OkxEndpoints.cs
│   ├── OkxRateLimiter.cs
│   └── OkxPagination.cs
│
├── Mapping/
│   ├── OkxInstrumentMapper.cs
│   ├── OkxCandleMapper.cs
│   ├── OkxTickerMapper.cs
│   ├── OkxTradeMapper.cs
│   ├── OkxOrderBookMapper.cs
│   └── OkxFundingRateMapper.cs
│
├── MarketData/
│   ├── OkxMarketDataClient.cs
│   ├── OkxInstrumentMetadataClient.cs
│   ├── OkxOrderBookClient.cs
│   └── OkxFundingRateClient.cs
│
├── WebSocket/
│   ├── OkxPublicWebSocketClient.cs
│   ├── OkxSubscriptionManager.cs
│   ├── OkxWebSocketMessageParser.cs
│   ├── OkxWebSocketConnection.cs
│   └── OkxOrderBookState.cs
│
├── Trading/
│   └── OkxTradingClient.cs
│
└── Accounts/
    └── OkxAccountClient.cs
```

Допускается небольшое изменение структуры, если существующий Bybit проект использует более простой и последовательный подход. Не создавать файлы ради соблюдения структуры: каждый класс должен иметь конкретную ответственность.

## 9. Зависимости проекта

Новый project должен ссылаться на:

```xml
<ProjectReference Include="..\CryptoTrader.Exchanges.Abstractions\CryptoTrader.Exchanges.Abstractions.csproj" />
```

Добавить необходимые Microsoft.Extensions пакеты по образцу `CryptoTrader.Exchanges.Bybit` и включить их версии в существующий `Directory.Packages.props`.

Рекомендуемый первичный подход — использовать стандартные `HttpClient` и `ClientWebSocket`, как в существующем Bybit adapter. Не добавлять сторонний OKX SDK без обоснования в ADR и проверки соответствия его моделей общим контрактам.

Если сторонняя библиотека всё же используется:

- проверить поддержку .NET 10;
- проверить актуальность и лицензию;
- проверить корректность обработки OKX V5 API;
- не протаскивать типы SDK за пределы проекта адаптера;
- покрыть mapping контрактными тестами.

Добавить новый проект в `CryptoTrader.slnx` и ссылку из `CryptoTrader.Host`.

## 10. Exchange Identity и идентификаторы инструментов

Добавить в Domain:

```csharp
public static readonly ExchangeId Okx = new("OKX");
```

Идентификатор биржи должен быть независим от названия конкретного SDK.

### 10.1. Exchange symbol

Сохранять точный symbol OKX в `Instrument.ExchangeSymbol`:

- Spot: `BTC-USDT`;
- Linear perpetual swap: `BTC-USDT-SWAP`.

Не пытаться преобразовывать формат OKX в Bybit-формат вида `BTCUSDT` на уровне Domain.

### 10.2. InstrumentId

`InstrumentId` должен быть уникальным между биржами и типами рынка. Использовать согласованный с текущим доменом формат, например:

- `OKX-SPOT-BTC-USDT`;
- `OKX-LINEAR-SWAP-BTC-USDT`.

Формат должен быть стабильным, детерминированным и покрытым тестами. Не использовать только `instId` как глобальный ID: одинаковые символы могут существовать на разных биржах.

### 10.3. Типы инструментов

В версии 0.1 разрешить:

- `SPOT`;
- `SWAP` только при подтверждённом `ctType = linear` и ожидаемой валюте расчёта (для первичной реализации — USDT-settled linear contracts, если это соответствует domain model).

Не включать в торговые расчёты:

- inverse SWAP;
- dated FUTURES;
- OPTIONS;
- MARGIN instruments.

Инструмент должен быть в состоянии `live`, чтобы считаться доступным для обычной рыночной подписки. Статусы `suspend`, `preopen`, `post_only`, `rebase` и другие не должны безусловно трактоваться как `live`.

### 10.4. Contract metadata

OKX предоставляет для деривативов отдельные поля `ctVal`, `ctMult`, `ctValCcy`, `settleCcy`, `ctType`, `lotSz`, `minSz`, `tickSz`.

Текущий `Instrument.ContractMultiplier` может оказаться недостаточным для корректного представления всей экономики контракта.

До включения SWAP в расчёт размера позиции необходимо:

1. Проанализировать текущий `Instrument` и `InstrumentMetadata`.
2. При необходимости добавить отдельные поля `ContractValue`, `ContractMultiplier`, `ContractValueCurrency`, `SettlementCurrency`, `ContractType`.
3. Установить точную формулу количества и notional для linear contract.
4. Добавить тесты на официальных примерах OKX.
5. Не подставлять `1` в качестве contract multiplier/value при отсутствии metadata.

Если данных недостаточно, инструмент можно показать в каталоге и получать по нему market data, но нельзя допускать его в Risk Manager, Paper Execution или Backtesting.

## 11. Конфигурация OKX

Добавить валидируемую options-модель `OkxOptions`.

Рекомендуемый пример конфигурации:

```json
{
  "Okx": {
    "Enabled": true,
    "Environment": "Production",
    "RestBaseUrl": "https://openapi.okx.com",
    "PublicWebSocketUrl": "wss://ws.okx.com/ws/v5/public",
    "DemoPublicWebSocketUrl": "wss://wspap.okx.com/ws/v5/public",
    "InstrumentTypes": ["SPOT", "SWAP"],
    "Symbols": ["BTC-USDT", "ETH-USDT"],
    "RequestTimeoutSeconds": 15,
    "MaxCandlesPerRequest": 300,
    "Reconnect": {
      "InitialDelaySeconds": 1,
      "MaxDelaySeconds": 30
    }
  }
}
```

Это пример формы конфигурации; имена полей нужно согласовать с текущим стилем options в репозитории.

Требования:

- все URL и лимиты конфигурируемые;
- валидация на старте приложения;
- запрещать неизвестный `Environment`;
- production и demo WebSocket URL не путать;
- не хранить API secret, passphrase или API key: публичному адаптеру они не нужны;
- не включать private endpoint configuration в MVP.

`Enabled` должно контролировать регистрацию/активацию источника, а не приводить к неявной попытке реальной торговли.

## 12. OKX REST API

Базовый production REST URL согласно документации OKX:

```text
https://openapi.okx.com
```

В версии 0.1 использовать только публичные endpoints.

### 12.1. Instruments

Получение Spot instruments:

```http
GET /api/v5/public/instruments?instType=SPOT
```

Получение Swap instruments:

```http
GET /api/v5/public/instruments?instType=SWAP
```

Маппить как минимум:

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

Для Spot:

- `PriceTick` ← `tickSz`;
- `QuantityStep` ← `lotSz`;
- `MinQuantity` ← `minSz`.

Для SWAP дополнительно проверить contract metadata и единицы измерения количества.

### 12.2. Candles

Текущая история:

```http
GET /api/v5/market/candles?instId=BTC-USDT&bar=1H&limit=300
```

Историческая история:

```http
GET /api/v5/market/history-candles?instId=BTC-USDT&bar=1H&limit=300
```

Реализовать cursor pagination с `after` / `before` в соответствии с семантикой OKX API. Проверять прогресс курсора и отсутствие дубликатов.

OKX возвращает до 300 свечей за запрос. `market/candles` предназначен для недавних данных и ограничен последними 1 440 записями; для более старого диапазона использовать `market/history-candles`.

Поля строки свечи содержат:

- timestamp;
- open;
- high;
- low;
- close;
- volume;
- volume in currency;
- volume in quote currency;
- `confirm`.

**Важное требование:** использовать только закрытые свечи (`confirm = "1"`) для исторического Backtesting и для стратегии, ожидающей закрытия бара. `confirm = "0"` означает незакрытую свечу и не должна считаться завершённой.

Все значения парсить из строк в `decimal` с invariant culture. Timestamp нормализовать в UTC.

### 12.3. Нормализация volume

У OKX единицы `vol` различаются по типу инструмента: для Spot это объём базовой валюты, для деривативов это количество контрактов. Для деривативов `volCcy` представляет объём в валюте инструмента, а `volCcyQuote` — объём в quote currency.

Нельзя безусловно записывать `vol` в общий `Candle.Volume` для всех типов инструментов.

Нужно определить и задокументировать каноническую семантику `Candle.Volume`. Если текущая модель не позволяет сохранить base volume, quote volume и contract volume отдельно, расширить модель либо ограничить нормализованный candle feed Spot до отдельного ADR.

### 12.4. Ticker

Реализовать публичный ticker endpoint:

```http
GET /api/v5/market/ticker?instId=BTC-USDT
```

Нормализовать last, bid, ask, timestamp и доступные объёмы. Не использовать ticker как замену order book.

### 12.5. Trades

При необходимости общего контракта:

```http
GET /api/v5/market/trades?instId=BTC-USDT
```

Нормализовать сторону сделки, цену, размер, timestamp и trade ID. Учитывать, что размер для деривативов может быть выражен в контрактах.

### 12.6. Order book snapshot

Использовать публичный endpoint:

```http
GET /api/v5/market/books?instId=BTC-USDT
```

Сохранять источник, timestamp, bid/ask levels и точность цены/объёма.

Не утверждать, что snapshot является актуальным после превышения допустимого Market Data Age.

### 12.7. Funding Rate

Для SWAP:

```http
GET /api/v5/public/funding-rate?instId=BTC-USDT-SWAP
```

Сохранять:

- funding rate;
- funding time;
- next funding time;
- formula/method, если есть;
- settlement funding rate/state, если есть;
- исходный instrument.

Не предполагать, что funding всегда происходит каждые 8 часов. Интервал может меняться; определять его по timestamps и данным биржи.

### 12.8. Проверка REST response

OKX использует JSON envelope с `code`, `msg`, `data`.

Успешный HTTP status не означает успешный OKX API response. Считать запрос успешным только при корректном HTTP status и успешном бизнес-коде OKX (`code = "0"`).

Ошибки должны включать:

- HTTP status;
- OKX error code;
- безопасное описание ошибки;
- endpoint;
- correlation/request identifier, если доступен.

Не логировать секреты, подписи и приватные заголовки.

## 13. OKX WebSocket

### 13.1. URL

Production public WebSocket:

```text
wss://ws.okx.com/ws/v5/public
```

Demo public WebSocket:

```text
wss://wspap.okx.com/ws/v5/public
```

Не использовать порт `8443`: OKX объявила прекращение его поддержки для WebSocket на публичном интернете с 31 октября 2026 года. Использовать стандартный HTTPS/WSS порт, если официальный региональный endpoint не требует иного.

### 13.2. Каналы

Для первого инкремента поддержать:

- `tickers`;
- `trades`;
- `candle1m` / `candle5m` / `candle1H` / `candle4H` — только необходимые приложению интервалы;
- `books5` для snapshot-style order book, если нужен общий orderbook feed.

Пример subscribe:

```json
{
  "op": "subscribe",
  "args": [
    { "channel": "candle1H", "instId": "BTC-USDT" },
    { "channel": "tickers", "instId": "BTC-USDT" },
    { "channel": "trades", "instId": "BTC-USDT" }
  ]
}
```

Проверить фактические названия каналов и допустимые интервалы по официальной документации при реализации; не собирать имя канала конкатенацией неподтверждённых строк.

### 13.3. Subscription manager

В отличие от открытия отдельного сокета на каждый инструмент, OKX adapter должен иметь manager, который:

- держит ограниченное число WebSocket connections;
- объединяет subscriptions на одном соединении, где это допускается API;
- хранит список желаемых subscriptions;
- сопоставляет ack с запросами;
- восстанавливает подписки после reconnect;
- отслеживает ping/pong и отсутствие данных;
- ограничивает частоту connect/subscribe;
- корректно завершает соединения при CancellationToken и shutdown.

По документации OKX нужно учитывать ограничения WebSocket: не более 3 connection attempts в секунду на IP и лимиты операций subscribe/unsubscribe/login на соединение. Применять лимиты консервативно и конфигурируемо.

### 13.4. Формат сообщений

Парсер должен различать:

- event ack (`subscribe`, `unsubscribe`, `error`, `login`);
- market data update;
- heartbeat/ping/pong;
- error/connection close.

Не считать event ack market-data сообщением.

Проверять `arg.channel` и `arg.instId`. Данные передавать в общие нормализованные market events.

### 13.5. Order book sequence

Если реализуется инкрементальный стакан (`books`), необходимо соблюдать порядок обновлений и проверять `seqId` / `prevSeqId`. При разрыве последовательности локальную книгу нельзя продолжать использовать как корректную: запросить новый snapshot и восстановить поток.

Не полагаться на старую checksum-логику без проверки текущей документации OKX: в актуальных изменениях OKX указала изменение правил sequence/checksum для ряда order book channels.

Для первой версии предпочтительно реализовать безопасный snapshot feed `books5`, если полноценное состояние инкрементальной книги не требуется.

## 14. Rate limiting и retry policy

Не переиспользовать `BybitRateGate` как будто лимиты у бирж одинаковые.

Создать `OkxRateLimiter`, который учитывает endpoint-specific limits и rate-limit scope, указанный в документации OKX.

Минимальные правила:

- ограничивать параллелизм запросов;
- учитывать разные buckets для instrument metadata, candles, funding rate и других endpoint groups;
- обрабатывать HTTP 429 и OKX rate-limit errors;
- применять bounded exponential backoff с jitter;
- учитывать `Retry-After` / rate-limit headers, если они предоставляются;
- не повторять неидемпотентные операции автоматически (в данном проекте private trading operations отсутствуют);
- логировать throttling и метрики ожидания.

Документированные лимиты могут меняться. Значения должны быть проверены непосредственно перед релизом и не должны считаться вечными константами.

## 15. Реализация общих абстракций

`OkxExchangeAdapter` должен реализовать существующий `IExchangeAdapter` и возвращать OKX-реализации общих клиентов.

Ожидаемые компоненты:

- `OkxMarketDataClient`;
- `OkxInstrumentMetadataClient`;
- `OkxOrderBookClient`, если для него выделен отдельный интерфейс;
- `OkxFundingRateClient`;
- `OkxTradingClient` — безопасная заглушка;
- `OkxAccountClient` — публичный режим, приватный аккаунт не реализован.

### 15.1. Запрет реальной торговли

`OkxTradingClient` обязан вести себя так же безопасно, как текущий Bybit public-only adapter:

- `SubmitAsync` возвращает явный результат `LiveTradingDisabled` / `NotSupported`;
- не выполняет HTTP запрос на создание ордера;
- `CancelAsync` не делает private API вызов;
- `GetOrder` не симулирует наличие реального состояния.

`OkxAccountClient` должен явно сообщать, что private account API не поддержан в версии 0.1.

Capabilities должны отражать отсутствие private account и live order support.

### 15.2. Dependency Injection

Создать:

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddOkx(
        this IServiceCollection services,
        IConfiguration configuration);
}
```

Внутри:

- зарегистрировать `OkxOptions` и валидировать их на старте;
- зарегистрировать typed/named HttpClient;
- зарегистрировать rate limiter;
- зарегистрировать REST и WebSocket services;
- зарегистрировать mapper/client implementations;
- зарегистрировать `IExchangeAdapter` с `ExchangeId.Okx`;
- не перезаписывать адаптеры других бирж.

Добавить `AddOkx(...)` в Host composition root.

## 16. Multi-exchange refactoring, необходимый для OKX

Интеграция OKX считается завершённой только после устранения жёсткой привязки ingestion к Bybit.

Обязательные изменения:

1. Добавить `ExchangeId.Okx`.
2. Ввести `IExchangeRegistry` и реализацию на основе зарегистрированных `IExchangeAdapter`.
3. Зарегистрировать Bybit и OKX через общий registry.
4. Переработать `CandleIngestionService` на конфигурационные subscriptions.
5. Убрать hard-coded `ExchangeId.Bybit` из общего ingestion flow.
6. Убрать неоднозначное разрешение одного `IMarketDataClient` через `GetService<T>()`.
7. Обеспечить уникальность `InstrumentId` между биржами.
8. Обеспечить, чтобы `Candles` ключевались с учётом `ExchangeId`.
9. В API синхронизации/импорта передавать биржу явно.
10. Добавить health status для каждого exchange feed отдельно.

Пример интерфейса registry приведён в разделе AUD-01.

## 17. Тестирование OKX

Расширить существующий `CryptoTrader.Exchanges.ContractTests` (или создать выделенный test project, если это соответствует текущей организации тестов).

### 17.1. Unit tests

Обязательные сценарии:

- parse `code = "0"`;
- parse OKX API error code;
- HTTP error + valid JSON body;
- malformed JSON;
- missing/invalid required fields;
- Spot instrument mapping;
- linear SWAP mapping;
- reject/skip inverse contract;
- reject unsupported instrument types;
- reject non-live instrument state for trading eligibility;
- `tickSz`, `lotSz`, `minSz` mapping;
- contract value metadata mapping;
- candle decimal parsing;
- candle UTC timestamp;
- `confirm = "1"` closed candle;
- `confirm = "0"` incomplete candle;
- Spot and derivative volume normalization;
- ticker mapping;
- trade side and quantity mapping;
- funding rate mapping;
- pagination cursor progress;
- no duplicated candles;
- rate limiter windows;
- 429/backoff handling.

### 17.2. WebSocket tests

Использовать fake WebSocket/fixture messages. Не подключаться к публичной бирже в обычном CI.

Проверить:

- successful subscription ack;
- rejected subscription ack;
- missing ack timeout;
- candle event;
- ticker event;
- trade event;
- orderbook snapshot;
- reconnect;
- resubscribe;
- ping/pong;
- malformed message;
- sequence gap for incremental book;
- CancellationToken shutdown;
- bounded queue backpressure.

### 17.3. Contract tests

Одинаковые тесты на общие контракты должны выполняться для Bybit и OKX там, где capabilities совпадают:

- получение свечей;
- корректные InstrumentId;
- корректные UTC timestamps;
- корректные decimal values;
- отсутствие неявной реальной торговли;
- правильная информация о capabilities.

### 17.4. Integration tests

- Host регистрирует оба адаптера;
- Registry возвращает правильный adapter по ExchangeId;
- ingestion получает candles с Bybit и OKX без пересечения данных;
- одинаковый base asset на разных биржах не вызывает конфликт ключей;
- migrations проходят;
- API может явно выбрать биржу;
- ошибки одного адаптера не маскируются как успешный feed другого.

Все тесты должны быть воспроизводимыми и не использовать реальные API credentials.

## 18. Документация

Обновить/создать:

- `docs/architecture.md` — registry и multi-exchange topology;
- `docs/exchange-integration.md` — общие правила добавления биржи;
- `docs/okx-integration.md` — OKX endpoints, mapping и ограничения;
- `docs/testing-strategy.md` — новые contract/integration tests;
- `docs/security.md` — public-only OKX, live disabled;
- `docs/operations-runbook.md` — настройка и проверка OKX feed;
- `README.md` — актуальный список поддерживаемых бирж и возможностей.

В документации явно указать:

- OKX public market data поддерживается;
- private account API в версии 0.1 не реализован;
- live orders выключены;
- Futures/SWAP market data не означает поддержку Futures execution/backtesting;
- доступность OKX для пользователя зависит от его юрисдикции и условий самой биржи; техническая возможность подключения не является юридическим подтверждением права пользоваться сервисом.

Не пытаться обходить региональные ограничения, KYC или правила обслуживания OKX.

---

# Часть III. План реализации

## 19. Порядок работ

### Этап A. Подготовка multi-exchange архитектуры

Ответственные: Senior .NET Architect, Senior C# Developer, Algorithmic Trading Systems Engineer, QA.

- [ ] Добавить `ExchangeId.Okx`.
- [ ] Согласовать capability model.
- [ ] Добавить `IExchangeRegistry`.
- [ ] Реализовать registry и DI registration для Bybit.
- [ ] Рефакторить `CandleIngestionService` на subscriptions.
- [ ] Убрать hard-coded Bybit из общего ingestion pipeline.
- [ ] Устранить неоднозначные DI resolutions.
- [ ] Добавить тесты на два адаптера.

**Критерий завершения:** Bybit работает через новый registry и ingestion не зависит от конкретной биржи.

### Этап B. Создание OKX project и REST market data

Ответственные: Algorithmic Trading Systems Engineer, Senior C# Developer, Database Engineer, QA.

- [ ] Создать `CryptoTrader.Exchanges.Okx`.
- [ ] Добавить проект в `CryptoTrader.slnx`.
- [ ] Добавить project reference в Host.
- [ ] Реализовать options и validation.
- [ ] Реализовать REST response parser.
- [ ] Реализовать rate limiter.
- [ ] Реализовать instrument metadata.
- [ ] Реализовать mapping Spot/linear SWAP.
- [ ] Реализовать candle history с pagination.
- [ ] Реализовать ticker.
- [ ] Реализовать order book snapshot.
- [ ] Реализовать funding rate.
- [ ] Добавить unit/contract tests.

**Критерий завершения:** локальные fixtures проходят тесты, OKX REST возвращает корректно нормализованные данные в ручном smoke test без приватного ключа.

### Этап C. WebSocket

Ответственные: Algorithmic Trading Systems Engineer, Senior C# Developer, QA.

- [ ] Реализовать public WebSocket connection.
- [ ] Реализовать Subscription Manager.
- [ ] Реализовать ack/error parser.
- [ ] Реализовать heartbeat.
- [ ] Реализовать reconnect с jitter.
- [ ] Реализовать resubscribe.
- [ ] Реализовать bounded queue/backpressure.
- [ ] Добавить метрики задержки и здоровья feed.
- [ ] Добавить WebSocket fixture tests.

**Критерий завершения:** при отключении и восстановлении соединения адаптер восстанавливает ожидаемые подписки и не выдаёт старые данные за актуальные.

### Этап D. Host, ingestion и multi-exchange persistence

Ответственные: Senior C# Developer, Database Engineer, Trading Systems Engineer, QA.

- [ ] Зарегистрировать OKX в Host.
- [ ] Включить OKX в ExchangeRegistry.
- [ ] Добавить конфигурационные subscriptions.
- [ ] Сохранить candles OKX с правильным ExchangeId.
- [ ] Проверить уникальные ключи и индексы.
- [ ] Добавить replay/checkpoint для downstream processing.
- [ ] Добавить per-exchange health status.
- [ ] Добавить integration tests для Bybit + OKX.

**Критерий завершения:** Host одновременно получает данные обеих бирж без конфликтов в БД и без выбора адаптера по порядку DI-регистрации.

### Этап E. QA, документация и release readiness

Ответственные: QA Automation Engineer, DevOps Engineer, Senior .NET Architect.

- [ ] Обновить документацию.
- [ ] Добавить CI проверки форматирования/анализаторов.
- [ ] Выполнить restore/build/test.
- [ ] Проверить Docker Compose.
- [ ] Проверить migrations.
- [ ] Проверить health endpoints.
- [ ] Проверить отсутствие secrets.
- [ ] Проверить, что live trading disabled.
- [ ] Подготовить отчёт о тестировании.

**Критерий завершения:** OKX adapter готов для public market data в рамках ограничений CryptoTrader 0.1.

## 20. Критерии приёмки OKX интеграции

Работа считается завершённой, если выполнены все пункты:

1. В Solution существует отдельный проект `CryptoTrader.Exchanges.Okx`.
2. Host регистрирует Bybit и OKX через единый registry.
3. `CandleIngestionService` не содержит hard-coded Bybit dependencies.
4. Для OKX доступны public REST market data методы.
5. Для OKX доступны public WebSocket subscriptions.
6. REST pagination покрывает запрошенный диапазон или явно сообщает о неполноте.
7. Незакрытые свечи не используются там, где требуются закрытые.
8. Все денежные и ценовые значения корректно преобразуются в `decimal`.
9. Volume units однозначно определены и протестированы.
10. Spot и SWAP instruments не смешиваются.
11. Contract metadata не подменяется фиктивными default values.
12. OKX candles сохраняются с `ExchangeId.Okx`.
13. Одинаковые инструменты с разных бирж не конфликтуют.
14. WebSocket subscription rejection обрабатывается явно.
15. WebSocket reconnect восстанавливает подписки.
16. Очередь market-data событий имеет управляемое ограничение.
17. Funding Rate доступен через общий capability/contract.
18. Live order submission отсутствует и явно запрещён.
19. Private account API не вызывается.
20. Все новые unit/contract/integration tests проходят.
21. Существующие Bybit tests продолжают проходить.
22. `dotnet build -c Release` проходит.
23. `dotnet test -c Release` проходит.
24. Docker Compose запускается и health checks проходят.
25. Документация отражает фактические capabilities.

## 21. Команды проверки

После реализации выполнить из корня репозитория:

```bash
dotnet restore CryptoTrader.slnx
dotnet build CryptoTrader.slnx -c Release --no-restore
dotnet test CryptoTrader.slnx -c Release --no-build
```

Если интеграционные тесты требуют Docker, запустить Docker и выполнить соответствующий test project отдельно.

Добавить проект в Solution командой, подходящей для используемой версии `dotnet` и формата `.slnx`, затем проверить, что он действительно отображается в Solution.

## 22. Что не входит в эту задачу

Не реализовывать в рамках данного документа:

- OKX private authentication;
- получение пользовательских балансов;
- реальные Spot ордера;
- реальные Futures/SWAP ордера;
- отмену реальных ордеров;
- private WebSocket;
- вывод/перевод средств;
- copy trading;
- управление средствами других пользователей;
- полноценный Futures Backtesting;
- полноценный Futures Paper ledger;
- автоматическую оптимизацию стратегий;
- SaaS billing и multi-tenant коммерческие функции.

Эти функции требуют отдельного ТЗ, security review, юридической оценки и тестовой программы.

---

# Часть IV. Источники

## 23. Исходный репозиторий

Все ссылки ниже относятся к ветке `main` и должны быть перепроверены, если код изменился после даты аудита.

- [Repository](https://github.com/AndrewLomov/CryptoTrader)
- [README](https://github.com/AndrewLomov/CryptoTrader/blob/main/README.md)
- [Solution](https://github.com/AndrewLomov/CryptoTrader/blob/main/CryptoTrader.slnx)
- [Directory.Build.props](https://github.com/AndrewLomov/CryptoTrader/blob/main/Directory.Build.props)
- [Directory.Packages.props](https://github.com/AndrewLomov/CryptoTrader/blob/main/Directory.Packages.props)
- [Host Program.cs](https://github.com/AndrewLomov/CryptoTrader/blob/main/src/CryptoTrader.Host/Program.cs)
- [Bybit project](https://github.com/AndrewLomov/CryptoTrader/tree/main/src/CryptoTrader.Exchanges.Bybit)
- [Exchange abstractions](https://github.com/AndrewLomov/CryptoTrader/tree/main/src/CryptoTrader.Exchanges.Abstractions)
- [Application](https://github.com/AndrewLomov/CryptoTrader/tree/main/src/CryptoTrader.Application)
- [Backtesting](https://github.com/AndrewLomov/CryptoTrader/tree/main/src/CryptoTrader.Backtesting)
- [Tests](https://github.com/AndrewLomov/CryptoTrader/tree/main/tests)
- [Architecture docs](https://github.com/AndrewLomov/CryptoTrader/blob/main/docs/architecture.md)
- [Testing strategy](https://github.com/AndrewLomov/CryptoTrader/blob/main/docs/testing-strategy.md)
- [Security docs](https://github.com/AndrewLomov/CryptoTrader/blob/main/docs/security.md)

## 24. Официальная документация OKX

Использовать официальную V5 API документацию как источник истины. Перед релизом сверить endpoint paths, параметры, каналы и лимиты с актуальной версией.

- [OKX API Documentation V5](https://www.okx.com/docs-v5/en/)
- [OKX API changelog](https://www.okx.com/docs-v5/log_en/)
- [Public instruments](https://www.okx.com/docs-v5/en/#rest-api-public-data-get-instruments)
- [Market candles](https://www.okx.com/docs-v5/en/#rest-api-market-data-get-candlesticks)
- [History candles](https://www.okx.com/docs-v5/en/#rest-api-market-data-get-candlesticks-history)
- [Market ticker](https://www.okx.com/docs-v5/en/#rest-api-market-data-get-ticker)
- [Order book](https://www.okx.com/docs-v5/en/#rest-api-market-data-get-order-book)
- [Public trades](https://www.okx.com/docs-v5/en/#rest-api-market-data-get-trades)
- [Funding rate](https://www.okx.com/docs-v5/en/#rest-api-public-data-get-funding-rate)
- [WebSocket public channels](https://www.okx.com/docs-v5/en/#websocket-api-public-channel)

Важные детали, которые нужно повторно проверить при реализации:

- для публичного WebSocket использовать актуальные endpoints без устаревшего `:8443`;
- учитывать подтверждения `confirm` для закрытия свечи;
- учитывать лимит 300 свечей на REST request и отдельный history endpoint;
- учитывать endpoint-specific rate limits;
- учитывать разные единицы volume для Spot и derivatives;
- для incremental order book соблюдать текущие sequence rules (`seqId` / `prevSeqId`);
- не считать funding interval фиксированным восьмичасовым.

---

# 25. Итоговое указание разработчику / AI coding agent

Сначала исправь multi-exchange архитектурную привязку и согласуй общие контракты. Затем создай `CryptoTrader.Exchanges.Okx` и реализуй public REST/WebSocket market data. Не добавляй private API и реальные торговые операции.

Не ограничивайся созданием пустого проекта или интерфейсов. Реализуй рабочие компоненты, тесты и интеграцию в Host.

Не меняй существующую Bybit реализацию без необходимости. Любое изменение общих контрактов должно сохранять обратную совместимость Bybit либо сопровождаться обновлением Bybit adapter и его тестов.

Не объявляй OKX integration готовой, пока одновременно не работают Bybit и OKX через общий registry, а свечи обеих бирж корректно сохраняются и различаются в PostgreSQL.

**Главный результат:** CryptoTrader становится multi-exchange платформой для public market data, где Bybit и OKX подключаются через единые контракты, а торговое ядро, стратегия и Backtesting не зависят от конкретной биржи.
