# QA: первый прогон

Дата: 2026-11-27. Конфигурация: `dotnet test -c Release`. MSB3027 не было. `src`, csproj и версии пакетов не менялись.

## Команды и счётчики

| Проект | Результат |
|---|---|
| `tests/CryptoTrader.Domain.Tests` | пройдено 17, не пройдено 0, пропущено 0 |
| `tests/CryptoTrader.Application.Tests` | пройдено 6, не пройдено 0, пропущено 0 |
| `tests/CryptoTrader.Strategies.Tests` | пройдено 5, не пройдено 0, пропущено 0 |
| `tests/CryptoTrader.Backtesting.Tests` | пройдено 4, не пройдено 0, пропущено 0 |
| `tests/CryptoTrader.Exchanges.ContractTests` | пройдено 2, не пройдено 0, пропущено 0 |
| `tests/CryptoTrader.IntegrationTests` | тесты не запускались: Host не собрался |
| API smoke | не писался и не запускался |

Команды успешных прогонов:

```text
dotnet test "d:\Новая папка\projects\CryptoTrader\tests\CryptoTrader.Domain.Tests\CryptoTrader.Domain.Tests.csproj" -c Release
dotnet test "d:\Новая папка\projects\CryptoTrader\tests\CryptoTrader.Application.Tests\CryptoTrader.Application.Tests.csproj" -c Release
dotnet test "d:\Новая папка\projects\CryptoTrader\tests\CryptoTrader.Strategies.Tests\CryptoTrader.Strategies.Tests.csproj" -c Release
dotnet test "d:\Новая папка\projects\CryptoTrader\tests\CryptoTrader.Backtesting.Tests\CryptoTrader.Backtesting.Tests.csproj" -c Release
dotnet test "d:\Новая папка\projects\CryptoTrader\tests\CryptoTrader.Exchanges.ContractTests\CryptoTrader.Exchanges.ContractTests.csproj" -c Release
```

Итог выполненных тестов: 34 пройдено, 0 не пройдено.

Первая сборка Application и Backtesting остановилась на `RiskManager.cs` (`Quantity * decimal`) и `PaperTradingCoordinator.cs` (`Quantity?`). После того как Application собрался, эти два проекта были прогнаны: 6 и 4 теста прошли. У Backtesting первая попытка упала в фикстуре свечи (`high` меньше `close`) до входа в движок; фикстура исправлена, повторный прогон — 4 из 4.

## Не запускалось

**Интеграция.** Команда:

```text
dotnet test "d:\Новая папка\projects\CryptoTrader\tests\CryptoTrader.IntegrationTests\CryptoTrader.IntegrationTests.csproj" -c Release
```

Сборка остановилась на Host:

`src/CryptoTrader.Host/MarketData/CandleIngestionService.cs(193,31): error CS0104` — имя `Instrument` неоднозначно между `CryptoTrader.Domain.Instruments.Instrument` и `System.Diagnostics.Metrics.Instrument`.

Docker не вызывался. Три теста уже написаны и помечены `Category=Integration`: миграция пустой базы, upsert свечи, повторный client order id. Они используют Testcontainers и не содержат запасного подключения к базе разработчика. Bybit из них не вызывается. Повторный прогон не делался.

**API smoke.** `Program.cs` нет, Host при его отсутствии остаётся библиотекой. `Microsoft.AspNetCore.Mvc.Testing` в IntegrationTests не подключён. `Program.cs` не менялся. `WebApplicationFactory<Program>` не использовался.

**Bybit SubmitAsync.** У `BybitTradingClient` нет зависимости от `HttpClient`, поэтому обработчик в него не передаётся. Тест проверяет `LiveTradingDisabled`, `Accepted = false` и `UnknownState = false`. Сетевого вызова в методе нет.

**Kline.** Публичного маппера нет, `BybitCandleMapper` внутренний. Проверка идёт через `GetCandlesAsync`: фабрика отдаёт `HttpClient` на `http://127.0.0.1:9/`, обработчик получает фикстуру с числовыми строками. Реальный сокет и API-ключ не используются. Оба контрактных теста прошли.

## Пробелы относительно ТЗ §21

Не автоматизированы: восстановление состояния заявки, paper execution, endpoints и авторизация, каталог символов, rate limit, недоступность API, reconnect, частичное биржевое исполнение, неизвестный статус, отсутствие секретов в логах. Отдельного теста на testnet нет и не требуется для обычного прогона.
