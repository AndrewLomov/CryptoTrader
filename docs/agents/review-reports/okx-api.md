# OKX V5 API check

Check date: 27 November 2026. Spec: `docs/okx-api-reference-notes.md`. Fixtures: `tests/CryptoTrader.Exchanges.ContractTests/Fixtures/Okx/`. No production code was edited.

## URLs opened

| URL | Result |
|---|---|
| `https://www.okx.com/docs-v5/en/` | Direct fetch HTTP 502. Search capture of the same URL supplied Overview, Production and Demo URLs, WebSocket connect/subscribe, Rate Limits, and Market Data through the start of the order-book channel. The capture ends mid-sentence in that channel, before Public Data. |
| `https://www.okx.com/docs-v5/en/#public-data-rest-api-get-instruments` | Direct fetch HTTP 502. Public instruments were not read from this anchor. |
| `https://www.okx.com/docs-v5/log_en/` | Changelog capture. Used for `openapi.okx.com` (2026-05-20), WebSocket port 8443 retirement (last update 22 October 2026), and `50011` changed from int to string (2021-06-08). |
| `https://www.okx.com/docs-v5/zh/` | Full enough to include Public Data. Used for `GET /api/v5/public/instruments`, the `ctVal` worked examples, `GET /api/v5/public/funding-rate`, and the order-book `seqId` rules the English capture cut off. |
| `https://app.okx.com/docs-v5/en/` | Opened only to confirm it is the US/AU site (`https://us.okx.com`, `wss://wsus.okx.com`). Not used as the Global base URL. |

## What is verified

- Global REST base is `https://openapi.okx.com`. It is still the documented host. `https://www.okx.com` is documented as still working and not deprecated.
- Production public WebSocket is `wss://ws.okx.com/ws/v5/public`. Demo public WebSocket is `wss://wspap.okx.com/ws/v5/public`. Neither URL includes a port. `:8443` is past both retirement dates on this check date (31 October 2026 public internet, 16 November 2026 colo).
- History candle `limit` maximum is 300. Recent candles cover the latest 1,440 rows. `after` returns older rows.
- Spot `vol` is base currency and `volCcy` is quote currency. Swap `vol` is contracts, `volCcy` is base currency, `volCcyQuote` is quote currency (USDT on `BTC-USDT-SWAP`, USD on `BTC-USD-SWAP`).
- Linear notional on the English order and position text is `sz × ctVal × markPx`. The Chinese instrument field adds the worked example `BTC-USDT-SWAP` `ctVal=0.01` BTC and `BTC-USD-SWAP` `ctVal=100` USD. `ctMult` is not in that formula.
- Rate-limit business code is string `50011`.

## Uncertainty

- Numeric rate limits are missing on the captured sections for `GET /api/v5/market/ticker`, `GET /api/v5/public/instruments`, and `GET /api/v5/public/funding-rate`. Not invented.
- No official instruments JSON example is a SWAP row. `instruments-spot.json` is the published `BTC-USDT` row, including empty contract fields. Linear and inverse fixtures keep that object shape and fill only values the pages state: `ctVal` `0.01` / `100`, `ctValCcy` `BTC` / `USD`, `ctType` `linear` / `inverse`, `settleCcy` `USDT` / `BTC`. `ctMult` stays `""` because every published instruments example is `""` and no official example uses `1`. `lotSz`, `minSz`, and `lever` on the swap fixtures are `""` for the same reason. `tickSz` `0.1` is the BTC-USDT example’s tick, not a captured swap tick. `groupId` `4` is the documented “perpetual futures group one” enum, not a captured row. `instIdCode` is `null`, which the page allows when a code is not generated.
- `candles-spot.json`, `ticker.json`, `trades.json`, `books-snapshot.json`, `funding-rate.json`, `envelope-error.json`, `ws-subscribe-ack.json`, `ws-subscribe-error.json`, and `ws-candle.json` copy published examples. `candles-swap.json` uses the official 9-element order but the numbers are an illustration of contract vs base vs quote (`10` contracts, `0.1` BTC, `370.8` USDT at `ctVal` `0.01`). They are not a captured `BTC-USDT-SWAP` print.
- `envelope-error.json` copies the published non-zero body (`51000`). A `50011` JSON body was not printed. The live overview describes that code in prose; the 2021 changelog message is the shorter “Requests too frequent.”
- `ws-pong.json` is the JSON string `"pong"`. On the wire the server sends the raw text `pong`, not an object.
- English and Chinese order-book sequence text was not compared past the point where the English capture stopped. The sequence rules in the spec are the Chinese section.
