# ADR 012. Объём свечи и спецификация контракта

## Статус

Принято

## Контекст

Spot измеряет объём в базовом активе, linear swap — в контрактах. Подстановка `1` вместо отсутствующего `ctVal` или запись числа контрактов в базовый объём ломает размер позиции. У spot-строки OKX поля контракта пустые, и пустота не означает единицу.

## Решение

`VolumeUnit`: `BaseAsset = 1`, `QuoteAsset = 2`, `Contract = 3`. `Candle.Volume` — количество в `VolumeUnit`. `QuoteVolume` и `BaseVolume` необязательны и не отрицательны, если заданы. Spot пишет базу в `Volume` и оборот котировки в `QuoteVolume`, когда биржа его прислала. SWAP пишет контракты в `Volume` (`VolumeUnit.Contract`), базу в `BaseVolume` и оборот в `QuoteVolume` только если биржа их дала. Отсутствующее значение остаётся `null`.

`Instrument.Create` принимает в конце необязательные `contractValue`, `contractValueCurrency`, `settlementCurrency`, `contractKind`. Spot оставляет их пустыми, как и `ContractMultiplier`. Linear perpetual допустим в двух формах: прежний множитель Bybit больше нуля, либо полная стоимость контракта (`contractValue > 0`, обе валюты, вид `linear`) без множителя. Явно заданный множитель `<= 0` отвергается. Пустой OKX `ctMult` не заменяется на `1`: официальная номинальная стоимость linear — `sz × ctVal × markPx` и не умножается на `ctMult`. `HasCompleteLinearContractSpec` истинен только при полной стоимости контракта; множитель может отсутствовать, но не может быть неположительным.

## Последствия

Старые вызовы `Candle.Create` и `Instrument.Create` не меняют порядок аргументов. Существующие свечи Bybit остаются базовым объёмом (`VolumeUnit` по умолчанию `BaseAsset`). Спецификация не включает futures PnL, liquidation и margin.
