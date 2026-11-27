# ADR 009. Общая книга для paper и backtest

## Статус

Принято

## Контекст

Две формулы cash и PnL разойдутся между симулятором и историческим прогоном.

## Решение

Paper и backtest применяют спотовый fill через `PortfolioLedger.ApplySpotFill` и один `IRiskManager`. Книга хранит cash, spot-позиции, peak equity, equity на начало UTC-дня, последнюю equity и флаг emergency stop. Покупка уменьшает cash на нотионал и комиссию в котируемом активе. Продажа увеличивает cash на нотионал минус комиссия. Нехватка cash — `DomainException`. Повтор того же `ExchangeTradeId` не проводится второй раз.

`Equity(mark)` — cash плюс `quantity * mark` по единственному открытому long. Для нескольких инструментов используется `Equity(marks)`. `EnsureDay` при смене UTC-даты ставит day-start равным последней наблюдаемой equity до нового fill. `DrawdownFraction` считается от peak и последней equity. Снимок `PaperState` — форма сохранения книги, не второй агрегат. `PortfolioSnapshot.HasPosition` и `PositionIsLosing` относятся к инструменту сигнала; остальные поля — к счёту.

Линейный контракт считается на `Position` через множитель, но книга 0.1 принимает только spot. Денежный PnL spot и futures не усредняется в одной позиции.

## Последствия

Метрики Sharpe и отчёт backtest остаются в движке. Кассовый результат раунда равен изменению cash книги, а не поля `RealizedPnl` целиком: комиссия входа в `RealizedPnl` не входит.
