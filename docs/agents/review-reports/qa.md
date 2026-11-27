# QA: контрактные тесты и повтор AUD-01 … AUD-12

Дата: 2026-11-27. Ветка: `feature/okx-multi-exchange`. `dotnet build` и `dotnet test` не запускались. Production-код Domain, Abstractions, Bybit, OKX, Host и Infrastructure не менялся. `DependencyRuleTests.cs` и `ExchangeRegistryTests.cs` не менялись.

Официальная сверка аудита: `docs/agents/audit-verification.md`.

## Добавленные случаи

`tests/CryptoTrader.Exchanges.ContractTests/OkxContractTests.cs` (xUnit, Shouldly, без сети). Файл читает JSON из `Fixtures/Okx/`, если файл есть, иначе берёт встроенный минимальный конверт. Каталог фикстур уже создан, новые файлы туда не писались.

| Метод | Что фиксирует |
|---|---|
| `Closed_spot_candle_uses_decimal_utc_base_volume_and_drops_the_unclosed_duplicate` | `code = 0`, decimal, UTC, spot `vol` как `VolumeUnit.BaseAsset`, `confirm = 0` не закрытая свеча |
| `Swap_candle_volume_is_contract_and_confirm_0_is_not_closed` | SWAP `vol` как `VolumeUnit.Contract`, `volCcy` в `BaseVolume`, `volCcyQuote` в `QuoteVolume` |
| `Nonzero_envelope_code_fails` | HTTP 200 и `code != 0` — `OkxApiException` |
| `Http_error_fails` | HTTP 500 не разбирается как успех |
| `Malformed_json_fails` | Обрезанный JSON — ошибка |
| `Pagination_cursor_that_does_not_advance_is_not_complete` | Курсор, который не сдвинулся, даёт `Incomplete`; `GetCandlesAsync` бросает `IncompleteCandleHistoryException` |
| `Incomplete_range_is_not_complete` | Дыра после последней свечи не даёт `Complete` |
| `Duplicate_open_times_collapse` | Две закрытые строки с одним open time остаются одной свечой |
| `Supported_instrument_ids_skip_inverse_option_dated_and_missing_ctVal` | `OKX-SPOT-BTC-USDT`, `OKX-LINEAR-SWAP-BTC-USDT`; inverse, option, dated и пустой `ctVal` не попадают в список; `ctVal` не становится 1 |
| `Empty_ctMult_fixture_does_not_become_one` | `instruments-swap-linear.json` с пустым `ctMult` не становится multiplier 1 |
| `SubmitAsync_returns_live_trading_disabled_without_calling_the_handler` | `LiveTradingDisabled`, `UnknownState = false`, HTTP-обработчик не вызывается |
| `Cancel_and_account_do_not_call_the_network` | Cancel и account бросают `NotSupportedException` без сети |
| `Subscribe_ack_success_is_recognized` | Успешный subscribe ack не рвёт сессию и не открывает второй сокет |
| `Subscribe_error_is_a_failure` | `event=error` после повторов даёт `subscription failed` |
| `Ping_pong_is_recognized` | Входящий `ping` отвечает `pong`; ответ на клиентский `ping` позволяет послать следующий |

`ExchangeRegistryOrderTests.Lookup_by_exchange_id_does_not_depend_on_registration_order` — два фейковых адаптера, `GetRequired` в обоих порядках регистрации. Дубликат id и неизвестный id уже есть в `tests/CryptoTrader.Architecture.Tests/ExchangeRegistryTests.cs`, повторно не добавлялись.

`BybitContractTests` обновлён, потому что `GetCandlesAsync` теперь бросает `IncompleteCandleHistoryException` на неполном диапазоне. Проверки decimal и UTC сохранены: успешный вызов сужен до одной закрытой свечи и требует `CandleHistoryCompleteness.Complete`. `SubmitAsync_returns_live_trading_disabled_and_known_state` оставлен. Добавлен `Incomplete_kline_range_throws`.

Интеграционный тест не добавлялся. Существующие `DatabaseTests` уже покрывают миграцию, upsert свечи и повторный client order id. Живая биржа для такого теста не нужна и не подключалась.

## Сборка против текущих типов

`src/CryptoTrader.Exchanges.Okx` уже в дереве. `InternalsVisibleTo` открывает его для `CryptoTrader.Exchanges.ContractTests`. Тесты вызывают реальные типы, а не предполагаемые имена:

- `OkxMarketDataClient` и внутренний `OkxPublicRest` с `HttpClient` на `http://127.0.0.1`;
- `OkxTradingClient.SubmitAsync` / `CancelAsync` и `OkxAccountClient.GetAccountAsync`;
- `OkxCandleMapper.Parse` для `confirm` 0 и 1;
- `ListInstrumentsAsync` для спота и linear swap;
- WebSocket через внутренний `IOkxSocketConnector`: фикстуры `ws-subscribe-ack.json`, `ws-subscribe-error.json` и текст `pong` из `ws-pong.json`.

`dotnet build` и `dotnet test` не запускались, поэтому компиляция этого файла на текущем адаптере не проверена прогоном. Условный `ProjectReference` остаётся: если csproj OKX исчезнет, `OkxContractTests.cs` снова исключается и набор Bybit собирается.

Пропущенных случаев из-за отсутствующих типов нет.

## Остаточные риски

- Живая сеть Bybit и OKX не вызывалась. Публичный smoke в CI не входит и в репозитории его нет.
- Docker и Testcontainers в этом проходе не запускались. Отдельного теста на отказ PostgreSQL и backup нет.
- Инкрементальный стакан OKX не собирается: `OkxPublicStream` принимает только `action=snapshot`, `prevSeqId` не сверяется. `seqId` читается в snapshot и этим проходом не тестировался.
- Миграция `20261127104212_MultiExchangeMarketData` добавляет колонки объёма и `candle_consumer_checkpoints`, но против PostgreSQL она здесь не прогонялась.
- Формат в CI не блокирует сборку (`continue-on-error: true`). `TreatWarningsAsErrors` выключен.
- `docs/architecture.md` всё ещё пишет, что каталога OKX нет и отдельной миграции после `Initial` нет. Код этому противоречит.

## 27 ноября 2026

Текст выше — прошлый прогон. Этот проход добавил тесты допуска свечи, нормализации `InstrumentTypes`, отказа приватных чтений, раздельных счетов, идемпотентного повтора и обновления PostgreSQL. Формат в CI обязательный. Живая биржа в CI по-прежнему не вызывается. Счётчики команд — в сообщении к PR после фактического прогона, а не из этого исторического абзаца.
