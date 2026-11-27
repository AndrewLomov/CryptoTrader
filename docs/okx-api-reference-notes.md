# OKX V5 public market reference

Check date: 27 November 2026.

Scope is the Global site (`www.okx.com`). This note is a reading of the live docs. It is not a client implementation. Direct `WebFetch` of `https://www.okx.com/docs-v5/en/` and of `https://www.okx.com/docs-v5/en/#public-data-rest-api-get-instruments` returned HTTP 502. The English overview, market-data, WebSocket, and changelog text below is from the search capture of those same URLs. That English capture ends inside the order-book channel, before Public Data. Public instruments, funding rate, and the rest of the order-book sequence rules are from the Chinese page `https://www.okx.com/docs-v5/zh/`, which continued through those sections. Where the two languages were both present, the English wording is quoted.

US/AU (`app.okx.com` → `us.okx.com`) and EU (`my.okx.com` → `eea.okx.com`) are different hosts. `openapi.okx.com` is documented as not valid for those registrations.

## Production REST base URL

Documented Global production REST base URL:

`https://openapi.okx.com`

Opened at `https://www.okx.com/docs-v5/en/` (Production Trading Services) and `https://www.okx.com/docs-v5/log_en/` (2026-05-20, “New REST API domain: openapi.okx.com”).

`https://openapi.okx.com` is still the documented base. The changelog says the docs base moved from `https://www.okx.com` to `https://openapi.okx.com`, that `www.okx.com` still works and is not deprecated, and that WebSocket hosts did not change.

Demo trading uses the same REST host. Demo requests add the header `x-simulated-trading: 1`.

## WebSocket URLs

No port is written on the current production or demo URLs. `wss://` uses port 443. The changelog says `:443` also works.

| Use | URL |
|---|---|
| Production public | `wss://ws.okx.com/ws/v5/public` |
| Production private | `wss://ws.okx.com/ws/v5/private` |
| Production business (candles) | `wss://ws.okx.com/ws/v5/business` |
| Demo public | `wss://wspap.okx.com/ws/v5/public` |
| Demo private | `wss://wspap.okx.com/ws/v5/private` |
| Demo business | `wss://wspap.okx.com/ws/v5/business` |

Candle channels are on `/ws/v5/business`, not on `/ws/v5/public`. Tickers, trades, books, books5, and funding-rate are on `/ws/v5/public`.

## GET /api/v5/public/instruments

Page: `https://www.okx.com/docs-v5/zh/` (获取交易产品基础信息). The English capture did not include this section.

Request parameters:

| Parameter | Required | Description |
|---|---|---|
| `instType` | Yes | `SPOT`, `MARGIN`, `SWAP`, `FUTURES`, `OPTION`, `EVENTS` |
| `seriesId` | Optional; required when `instType=EVENTS` | Series id, for example `BTC-ABOVE-DAILY` |
| `instFamily` | No | `FUTURES` / `SWAP` / `OPTION` only |
| `instId` | No | Instrument id |

The private account endpoint `GET /api/v5/account/instruments` is a different call. Its English parameter table marks `instFamily` conditional. The public Chinese table marks `instFamily` optional.

Response is the usual envelope `{ "code", "msg", "data": [ instrument, ... ] }`. `code` `"0"` is success. The published public example is one live `BTC-USDT` spot row. There is no published `BTC-USDT-SWAP` or `BTC-USD-SWAP` JSON row.

Fields requested for this integration:

| Field | What the public page says |
|---|---|
| `instId` | Instrument id, example `BTC-USDT` |
| `instType` | Instrument type |
| `baseCcy` | Base currency, example `BTC` in `BTC-USDT`. `SPOT` / `MARGIN` only |
| `quoteCcy` | Quote currency, example `USDT` in `BTC-USDT`. `SPOT` / `MARGIN` only |
| `settleCcy` | Settlement and margin currency, example `BTC`. `FUTURES` / `SWAP` / `OPTION` only |
| `ctType` | `linear` or `inverse`. `FUTURES` / `SWAP` only. Linear: margin, PnL, and settlement in the quote currency (USDT for `BTC-USDT-SWAP`). Inverse: margin, PnL, and settlement in the underlying (BTC for `BTC-USD-SWAP`) |
| `ctVal` | Face value of one contract. See the formula section |
| `ctMult` | Contract multiplier. `FUTURES` / `SWAP` / `OPTION` only. The page does not give a number and does not put `ctMult` in the notional formula |
| `ctValCcy` | Currency of `ctVal`. `FUTURES` / `SWAP` / `OPTION` only |
| `lotSz` | Size step. Contracts for derivatives, base currency for `SPOT` / `MARGIN`. `sz` must be an integer multiple of `lotSz` |
| `minSz` | Minimum order size, same unit as `lotSz`. Also `sz >= minSz` |
| `tickSz` | Minimum price increment, example `0.0001`. For `OPTION` / `EVENTS` this is the smallest band tick |
| `state` | `live`, `suspend`, `rebase`, `post_only`, `preopen`, `test`, `settling` (`settling` is `EVENTS` only) |

The official spot example returns `ctMult`, `ctType`, `ctVal`, `ctValCcy`, and `settleCcy` as `""`, and `state` as `"live"`.

General Info on the English overview maps settlement currency for BTC:

| Contract | `uly` | `instFamily` | `settleCcy` | Swap `instId` |
|---|---|---|---|---|
| USDT-margined | `BTC-USDT` | `BTC-USDT` | `USDT` | `BTC-USDT-SWAP` |
| Coin-margined | `BTC-USD` | `BTC-USD` | `BTC` | `BTC-USD-SWAP` |

`settleCcy` in that table is defined as the value returned by Get instruments.

A numeric rate limit for `GET /api/v5/public/instruments` was not on the captured section. Unverified. Do not copy the private account rule (`User ID + Instrument Type`) onto this public call. Public unauthenticated REST limits, where the overview states the rule, are per IP.

## Candles and history candles

English pages, both under Market Data on `https://www.okx.com/docs-v5/en/`:

- Recent: `GET /api/v5/market/candles`
- History: `GET /api/v5/market/history-candles`

| | Recent candles | History candles |
|---|---|---|
| How far | Latest 1,440 entries for the requested bar | Recent years. `1s` is only the last 3 months |
| `limit` | Max `300`, default `100` | Max `300`, default `100` |
| Rate limit | 40 requests per 2 seconds | 20 requests per 2 seconds |
| `bar` default | `1m` | `1m` |
| `1s` | Not listed on the recent `bar` enum | Listed |

`bar` values used by this integration:

| Meaning | REST `bar` | WebSocket channel |
|---|---|---|
| 1 minute | `1m` | `candle1m` |
| 5 minutes | `5m` | `candle5m` |
| 15 minutes | `15m` | `candle15m` |
| 1 hour | `1H` | `candle1H` |
| 4 hours | `4H` | `candle4H` |
| 1 day, UTC+8 open | `1D` | `candle1D` |
| 1 day, UTC+0 open | `1Dutc` | `candle1Dutc` |

`1D` / `candle1D` is the UTC+8 daily bar. `1Dutc` / `candle1Dutc` is the UTC+0 daily bar. They are different channels.

Pagination, same text on both endpoints:

- `after`: records earlier than the requested `ts` (older candles). This is the cursor that walks backward.
- `before`: records newer than the requested `ts`. `before` alone returns the latest data.

Array order, both endpoints: `[ts, o, h, l, c, vol, volCcy, volCcyQuote, confirm]`.

Index 0 is `ts` (open time, Unix milliseconds, string). Index 1 is the open price. The last element, index 8, is `confirm`:

- `"0"`: candle is not completed
- `"1"`: candle is completed

The recent-candles example contains one `"0"` row and one `"1"` row. The history example on the same page shows two `"1"` rows. The recent endpoint also says the first returned candle may be incomplete and should not be polled repeatedly.

## Volume units

Same sentences on recent candles, history candles, the candle WebSocket channel, and the ticker `vol24h` / `volCcy24h` fields.

Spot and margin:

- `vol` (ticker: `vol24h`): quantity in base currency. For `BTC-USDT`, BTC.
- `volCcy` (ticker: `volCcy24h`): quantity in quote currency. For `BTC-USDT`, USDT.
- `volCcyQuote`: quantity in quote currency. For `BTC-USDT`, USDT. Tickers do not have `volCcyQuote24h`.

Swap and other derivatives:

- `vol` (ticker: `vol24h`): number of contracts, not coins.
- `volCcy` (ticker: `volCcy24h`): quantity in base currency. The derivatives sentence says “the number of base currency”.
- `volCcyQuote`: quantity in quote currency. USDT for `BTC-USDT` and `BTC-USDT-SWAP`. USD for `BTC-USD-SWAP`.

Trade `sz` matches that split: base currency on spot, contracts on `FUTURES` / `SWAP` / `OPTION`.

## Linear contract formula

Sources:

- English place-order `sz`: notional = `sz × ctVal × markPx` (linear) or `sz × ctVal` (inverse, USD). Read `ctVal` and `ctType` from `GET /api/v5/public/instruments`.
- English position `notionalUsd` and `upl`: linear notional `sz × ctVal × markPx`; inverse notional `sz × ctVal`; linear PnL `(markPx − avgPx) × pos × ctVal`; inverse PnL `(1/avgPx − 1/markPx) × pos × ctVal`.
- Chinese public `ctVal` field, which is the instrument section the English capture did not reach: linear face value is in the underlying currency; inverse face value is in USD. Worked examples in that sentence: `BTC-USDT-SWAP` `ctVal=0.01` BTC, and `BTC-USD-SWAP` `ctVal=100` USD. Notional: linear = contracts × `ctVal` × mark price (quote currency); inverse = contracts × `ctVal` (fixed USD).

`ctValCcy` is the currency of that face value. For the linear example it is BTC. For the inverse example it is USD.

`ctMult` is only described as the contract multiplier. None of those formulas multiply by `ctMult`. Every instruments JSON example on the captured pages sets `ctMult` to `""`.

Do not default a missing value to 1:

- The official spot row returns `""` for `ctVal`, `ctMult`, `ctValCcy`, `ctType`, and `settleCcy`. Empty means the field does not apply. It does not mean 1.
- If a SWAP or FUTURES payload omits `ctVal`, or returns `""`, do not substitute 1. The published BTC linear example is `0.01`, not `1`. Substituting 1 treats one `BTC-USDT-SWAP` contract as 1 BTC instead of 0.01 BTC.
- Do not multiply by a default `ctMult` of 1 when the field is missing or `""`. The published notional and PnL formulas do not use `ctMult`.
- Do not invent `settleCcy`. USDT-margined BTC swap is `USDT`; coin-margined `BTC-USD-SWAP` is `BTC`, from the General Info table and from `ctType`.

The convert endpoint example `GET /api/v5/public/convert-contract-coin?instId=BTC-USD-SWAP&px=35000&sz=0.888` returns `sz` `"311"`. That matches `0.888 × 35000 / 100 = 310.8`, rounded. The page does not print the conversion formula and does not print `ctMult`, so this note does not treat that response as an official `ctMult` value.

## Ticker, trades, books, funding rate

### Ticker

`GET /api/v5/market/ticker?instId=BTC-USD-SWAP`

The single-ticker section has no rate-limit heading. Unverified. The all-tickers endpoint is 20 requests per 2 seconds and requires `instType`.

`ts` is ticker generation time, Unix milliseconds, string, example `1597026383085`.

### Trades

`GET /api/v5/market/trades?instId=BTC-USDT`

Rate limit 100 requests per 2 seconds. `limit` max 500, default 100. At most the 500 most recent public trades.

Fields: `instId`, `tradeId`, `px`, `sz`, `side` (`buy` / `sell`, taker side), `source` (`0` normal, `1` RPI on this endpoint), `ts` (trade time, Unix milliseconds, string).

### Books snapshot

`GET /api/v5/market/books?instId=BTC-USDT`

Rate limit 40 requests per 2 seconds. `sz` is depth per side, max 400, default 1. The book is refreshed about every 50 ms. The call returns the latest cached snapshot; it does not stream increments. Response fields: `asks`, `bids`, `ts`, `seqId`. The English table calls `ts` “Order book generation time”. The example value `1629966436396` is a 13-digit Unix millisecond timestamp. The table does not add the word “milliseconds”.

Each level is `[price, size, "0", orderCount]`. Size is contracts for derivatives and base currency for spot. The third element is deprecated and always `"0"`. `books-full` drops that third element and has no `seqId`. `books-rpi` uses the third element as `nonRpiQty` and is a different book.

WebSocket, URL path `/ws/v5/public`:

- `books`: 400-level snapshot first, then incremental `update` every 100 ms. Has `action` (`snapshot` or `update`), `prevSeqId`, `seqId`.
- `books5`: 5-level snapshot on subscribe, then another snapshot every 100 ms when those five levels change. The published `books5` push has no `action` and no `prevSeqId`.

`checksum` remains on `books`, `books-l2-tbt`, and `books50-l2-tbt` but is fixed at `0` and must not be used. Sequence is `seqId` / `prevSeqId`.

This increment uses snapshot only. Read `GET /api/v5/market/books` or the `books5` snapshot push. Do not apply `books` incremental `action: "update"` rows, and do not stitch `prevSeqId` to `seqId` in this increment.

Sequence rules, from the Chinese order-book channel (the English capture stopped mid-sentence in that same section):

- One `seqId` sequence per `instId`. Separate WebSocket connections to the same channel see the same numbers.
- A snapshot starts at `prevSeqId = -1`. The minimum `seqId` is 0.
- In the normal case `seqId` is greater than `prevSeqId`, and the new `prevSeqId` equals the previous message’s `seqId`.
- If there is no depth change for about 60 seconds, an incremental channel sends `asks: []` and `bids: []` with `prevSeqId == seqId`. A snapshot-style channel repeats the last update.
- Maintenance can reset the sequence. The signal is an incremental message with `seqId` less than `prevSeqId`. Later messages follow the normal rule again.
- Published walk-through: snapshot `(-1, 10)`, update `(10, 15)`, no-change `(15, 15)`, reset `(15, 3)`, update `(3, 5)`.

`books5` in the published example carries `seqId` only.

### Funding rate

`GET /api/v5/public/funding-rate?instId=BTC-USD-SWAP`

`instId` is required. `SWAP`, and X-Perps `FUTURES`. `ANY` returns all of those. No numeric rate limit was printed on the captured section. Unverified.

Timestamp fields, all Unix milliseconds as strings:

- `fundingTime`: funding fee time
- `nextFundingTime`: next funding fee time
- `ts`: data update time

The interval is the difference of `fundingTime` and `nextFundingTime`. It is often 8 hours and can be 6, 4, 2, or 1 hour. `fundingRate` is the predicted rate for the next settlement. `settFundingRate` is the rate used for the current settlement when `settState` is `processing`, and the previous settlement when `settState` is `settled`.

WebSocket channel `funding-rate` on `/ws/v5/public`. Push interval 30 to 90 seconds. The push example repeats `fundingTime`, `nextFundingTime`, and `ts`.

## WebSocket control frames

Subscribe request:

```json
{
  "id": "1512",
  "op": "subscribe",
  "args": [{ "channel": "tickers", "instId": "BTC-USDT" }]
}
```

Success ack:

```json
{
  "id": "1512",
  "event": "subscribe",
  "arg": { "channel": "tickers", "instId": "BTC-USDT" },
  "connId": "a4d3ae55"
}
```

Error ack (published example, code `60012`):

```json
{
  "id": "1512",
  "event": "error",
  "code": "60012",
  "msg": "Invalid request: {\"op\": \"subscribe\", \"argss\":[{ \"channel\" : \"tickers\", \"instId\" : \"LTC-USD-200327\"}]}",
  "connId": "a4d3ae55"
}
```

Unsubscribe request uses `"op": "unsubscribe"` and the same `args` shape. Success ack is `"event": "unsubscribe"` plus `arg` and `connId`. Error uses `"event": "error"`.

`args` across one subscribe message must stay within 64 KB.

Channel names for this integration:

| Data | Channel | URL path |
|---|---|---|
| 1m / 5m / 15m / 1H / 4H | `candle1m`, `candle5m`, `candle15m`, `candle1H`, `candle4H` | `/ws/v5/business` |
| 1D UTC+8 vs 1D UTC+0 | `candle1D` vs `candle1Dutc` | `/ws/v5/business` |
| Tickers | `tickers` | `/ws/v5/public` |
| Trades | `trades` | `/ws/v5/public` |
| 5-level book | `books5` | `/ws/v5/public` |
| Funding | `funding-rate` | `/ws/v5/public` |

Candle push `data` is the same 9-element array as REST, including `confirm`.

## Ping, pong, idle timeout

The wire payload is the raw string `ping`. The server answers with the raw string `pong`. Neither value is a JSON object.

The connection is closed if the subscription is not established, or if no data has been pushed, for more than 30 seconds. The documented keep-alive is: reset a timer of N seconds (N < 30) on every received message; if it fires, send `ping`; if `pong` does not arrive within N seconds, raise an error or reconnect.

## Connection and subscription limits

From `https://www.okx.com/docs-v5/en/` WebSocket Connect:

- New connections: 3 per second, per IP.
- `subscribe` + `unsubscribe` + `login` on one connection: 480 per hour.
- One subscribe payload: multiple channels, total length at most 64 KB.
- The 30-connection cap is per sub-account and only for the listed private channels (orders, account, positions, balance and position, position risk warning, account greeks). It is not documented as a public market-data cap.
- A network problem can disable the connection. Service upgrade sends `event: notice`, code `64008`, about 60 seconds before disconnect.

No separate public-channel connection cap was stated beyond the 3 new connections per second and the 30-second idle rule.

## Rate limits and business code

Overview, `https://www.okx.com/docs-v5/en/` Rate Limits: a rejected request returns error code `50011` (“Rate limit reached. Please refer to API documentation and throttle requests accordingly”). Public unauthenticated REST limits are per IP. WebSocket login and subscription limits are per connection.

The 2021-06-08 changelog changed `50011` from an integer to a string. HTTP status in that table is 429. The changelog message text is “Requests too frequent.” The live overview sentence is the longer “Rate limit reached…” text. No JSON body sample of `50011` was printed. The published failure body shape elsewhere is `{ "code", "msg", "data": [] }` with a non-zero string `code`.

| Endpoint | Limit on the section |
|---|---|
| `GET /api/v5/market/tickers` | 20 requests / 2 seconds |
| `GET /api/v5/market/ticker` | Not stated. Unverified |
| `GET /api/v5/market/books` | 40 requests / 2 seconds |
| `GET /api/v5/market/books-full` | 10 requests / 2 seconds |
| `GET /api/v5/market/candles` | 40 requests / 2 seconds |
| `GET /api/v5/market/history-candles` | 20 requests / 2 seconds |
| `GET /api/v5/market/trades` | 100 requests / 2 seconds |
| `GET /api/v5/public/instruments` | Not stated. Unverified |
| `GET /api/v5/public/funding-rate` | Not stated. Unverified |

## Changes versus older examples

- REST host in the docs is `https://openapi.okx.com`, not `https://www.okx.com`. `www.okx.com` is still documented as working (changelog 2026-05-20).
- Current public WebSocket URLs do not include a port. Older URLs used `:8443` (`wss://ws.okx.com:8443/ws/v5/public`, `wss://wspap.okx.com:8443/ws/v5/public`, and the 2023 business URL `wss://ws.okx.com:8443/ws/v5/business`). Changelog last updated 22 October 2026: public-internet port 8443 stopped on 31 October 2026, colo on 16 November 2026. This check date is after both. A connection that still lands on 8443 before retirement got `event: notice`, code `64010`.
- Candle subscriptions belong on `/ws/v5/business` without `:8443`.
- Order-book `checksum` is deprecated and fixed at 0. The third book element is always `"0"` except on `books-rpi`.
