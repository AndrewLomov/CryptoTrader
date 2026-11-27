# OKX public market data

Check date: 27 November 2026. Global site only. No private trading, no demo header, no exchange SDK.

The adapter reads the live English V5 pages. `docs/okx-api-reference-notes.md` was written from a truncated English capture plus the Chinese page, and it leaves three rate limits unverified. Those limits are stated on the full English page, so the adapter follows the English page.

## Doc check

| Topic | Official page |
|---|---|
| Index, REST host, WebSocket URLs, ping, rate-limit code `50011` | https://www.okx.com/docs-v5/en/ |
| Ticker, 20 requests / 2 seconds, IP | https://www.okx.com/docs-v5/en/#order-book-trading-market-data-get-ticker |
| Order book, 40 requests / 2 seconds, `sz` max 400 | https://www.okx.com/docs-v5/en/#order-book-trading-market-data-get-order-book |
| Recent candles, 40 requests / 2 seconds, latest 1,440, `limit` max 300 | https://www.okx.com/docs-v5/en/#order-book-trading-market-data-get-candlesticks |
| History candles, 20 requests / 2 seconds, `limit` max 300 | https://www.okx.com/docs-v5/en/#order-book-trading-market-data-get-candlesticks-history |
| Public instruments, 20 requests / 2 seconds, IP + instrument type | https://www.okx.com/docs-v5/en/#public-data-rest-api-get-instruments |
| Funding rate, 10 requests / 2 seconds, IP + instrument id | https://www.okx.com/docs-v5/en/#public-data-rest-api-get-funding-rate |
| Port 8443 retirement | https://www.okx.com/docs-v5/log_en/ |

REST base: `https://openapi.okx.com`. Public WebSocket: `wss://ws.okx.com/ws/v5/public`. Business WebSocket for candles: `wss://ws.okx.com/ws/v5/business`. No `:8443`. `www.okx.com` still works and is not the host this adapter calls.

Conflicts with `docs/okx-api-reference-notes.md` (the notes do not name a different number; they say the number was not captured):

- `GET /api/v5/market/ticker` is 20 requests / 2 seconds on the English page. The notes mark it unverified.
- `GET /api/v5/public/instruments` is 20 requests / 2 seconds, rule IP + instrument type. The notes mark it unverified.
- `GET /api/v5/public/funding-rate` is 10 requests / 2 seconds, rule IP + instrument id. The notes mark it unverified.

The multi-exchange contract's placeholder host `https://www.okx.com` and `wss://ws.okx.com:8443/ws/v5/public` are not used. Port 8443 left the public internet on 31 October 2026. Candle channels on the public socket are the wrong URL (`64001` on the business/public split). This adapter adds `BusinessWebSocketUrl` for that reason.

History `limit` maximum on the live page is 300, not an older 100-row assumption.

## Implemented

Public market data for spot and linear perpetual swaps when `Okx:Enabled` is true. The default is false, and a disabled section does not register `IExchangeAdapter`.

| Call | Path |
|---|---|
| Instruments | `GET /api/v5/public/instruments?instType=SPOT\|SWAP` |
| Recent candles | `GET /api/v5/market/candles` |
| History candles | `GET /api/v5/market/history-candles` |
| Ticker | `GET /api/v5/market/ticker?instId=` |
| Order book snapshot | `GET /api/v5/market/books?instId=&sz=50` |
| Funding rate | `GET /api/v5/public/funding-rate?instId=` |

Named `HttpClient` is `okx`. Envelope `code` other than `"0"` throws `OkxApiException` even on HTTP 200, including a missing `code` or `data`. HTTP errors, timeouts, cancellation, malformed JSON, and missing required fields throw. Cancellation stays `OperationCanceledException`. Idempotent GETs retry at most three times on network failure, timeout, HTTP 429/502/503/504, and business code `50011`. `Retry-After` is honored and capped at 30 seconds. Otherwise the wait is 200 ms, doubled, capped at 5 seconds, plus 0–249 ms of jitter. Secrets are not logged.

The shared limiter is 10 requests per 2 seconds (`Okx:MaxRequestsPerWindow` / `Okx:RateLimitWindowSeconds`). That is the funding-rate limit, which is stricter than the other calls above. It is a sliding window, not 10 requests per second.

Instrument ids are `OKX-SPOT-{instId}` and `OKX-LINEAR-SWAP-{base}-{quote}`. The exchange symbol stays the OKX `instId` (`BTC-USDT`, `BTC-USDT-SWAP`). Inverse, dated futures, options, and margin are skipped. A linear swap is kept only when `ctType` is `linear`, `instId` ends with `-SWAP`, `ctVal > 0`, and `ctValCcy`, `settleCcy`, `tickSz`, `lotSz`, and `minSz` are present. `ContractValue` is `ctVal`. A positive `ctMult` is stored as `ContractMultiplier`. An empty `ctMult` stays null and is not replaced with 1: the official linear notional is `sz × ctVal × markPx` and does not use `ctMult`. `HasCompleteLinearContractSpec` is true for that face-value spec. A non-positive explicit `ctMult` is skipped. Spot contract fields stay null. `state` `live` sets `IsTradingEnabled`. Known other states (`suspend`, `rebase`, `post_only`, `preopen`, `test`, `settling`) and any unknown state stay not trading-enabled. `minNotional` is not on this public payload and stays null.

Candles paginate backward with `after`. The recent endpoint is used until it returns an empty page, then history continues from the same cursor. Rows are deduplicated by open time, sorted ascending, and kept when `confirm` is `"1"`, open is inside the range, and close is inside the range. `confirm` `"0"` is not returned as a closed candle. A cursor that does not move, or `Okx:MaxPages` (500), returns `Incomplete` with the candles already collected. After the exchange has no more pages, missing confirmed candles (internal, leading, or trailing) are also `Incomplete`. `GetCandlesAsync` returns the list only for `Complete` or `Empty`. `Incomplete` throws `IncompleteCandleHistoryException`. `from > to` is `Empty` and does not call the network.

Volume:

- Spot `vol` is `Volume` with `VolumeUnit.BaseAsset`. Quote volume is `volCcy`, or `volCcyQuote` when `volCcy` is absent. `BaseVolume` stays null.
- Swap `vol` is `Volume` with `VolumeUnit.Contract`. `BaseVolume` is `volCcy` (base currency). `QuoteVolume` is `volCcyQuote`. The contract count is not copied into base volume.

Prices and volumes are `decimal` in invariant culture. Daily bars use `1Dutc` / channel `candle1Dutc` (UTC+0). `1D` is UTC+8 and is not used. Domain bars: `1m`, `5m`, `15m`, `1h` → `1H`, `4h` → `4H`, `1d` → `1Dutc`.

Ticker `last` is required and must be positive. `bidPx` and `askPx` are optional. Spot funding does not call the network and returns null. Linear funding maps `fundingRate` and `fundingTime` (or `nextFundingTime` when `fundingTime` is absent). An empty `data` array returns null. A row that is present but missing the rate or both times throws.

WebSocket channels actually opened:

| Channel | Socket |
|---|---|
| `tickers` | `/ws/v5/public` |
| `trades` | `/ws/v5/public` |
| `books5` | `/ws/v5/public` |
| `candle1m`, `candle5m`, `candle15m`, `candle1H`, `candle4H`, `candle1Dutc` | `/ws/v5/business` |

`books5` is the snapshot channel. A push with `action` other than `snapshot` is ignored. REST `books` with `sz=50` feeds `IOrderBookClient.GetSnapshotAsync`. An empty book, a parse failure, a duplicate price, a non-positive price, or a negative size returns `IsConsistent` false and no invented levels. A bid at or above the ask is left as the exchange sent it. Zero-size levels are dropped. `seqId` is the sequence when it fits in a 64-bit integer.

Trades map onto `PublicTradeObserved` (`px`, `sz`, taker `side` `buy`/`sell`, `ts`). REST `GET /api/v5/market/trades` is not called.

The public socket sends raw text `ping` and expects raw text `pong`. The interval is `Okx:PingIntervalSeconds` (20, and it must be below 30). A missing `pong` before the next ping reconnects. Subscribe acks are matched on `event=subscribe` plus `arg.channel` and `arg.instId`. `event=error` fails the session. No matching ack within `Okx:AckTimeoutSeconds` (10) fails the session. Three sessions that never ack fail the reader with `InvalidOperationException` so ingestion can mark the feed unhealthy. A session that did ack, then dropped, reconnects and does not count toward those three. Backoff starts at 1 second, doubles, caps at 30 seconds, and adds 0–249 ms. Reconnect sends subscribe again. Shutdown follows `CancellationToken`. The socket URL is not logged.

The event channel holds `Okx:ChannelCapacity` items (256) and waits when it is full. Candles, trades, and book snapshots are not dropped. A wait is logged. Tickers are not waited on: only the latest ticker is kept, and a flush about every 50 ms writes it when the channel has room. A full channel therefore does not block candles, trades, or books behind a ticker. Bursts of ticker updates collapse to the newest value.

`OkxTradingClient.SubmitAsync` returns `Accepted` false, `UnknownState` false, `ErrorCode` `LiveTradingDisabled`, and does not open a connection. `GetOrderAsync` returns null. Open orders and positions return empty. `CancelAsync` throws `NotSupportedException`. `OkxAccountClient.GetAccountAsync` throws `NotSupportedException` and does not invent balances.

Capabilities: spot, linear market data, order book, funding, and WebSocket are true. Live orders, private account, and futures accounting are false. The adapter is registered only as `IExchangeAdapter`. `IMarketDataClient` is not registered as a default single service.

HTTP tests can pass an `HttpClient`. WebSocket tests can pass `IOkxSocketConnector`. The default connector uses `ClientWebSocket`. Internals are visible to `CryptoTrader.Exchanges.ContractTests`.

## Partially implemented

- Order book depth is the REST snapshot (`sz=50`) plus the 5-level `books5` push. The 400-level `books` stream is not applied.
- Candle history treats an empty recent page as the handoff to history. That matches the documented 1,440-row recent window without assuming a clock offset. A range the exchange cannot fill is `Incomplete` when some closed candles were returned, and `Empty` when none were.
- The rate limiter is one process-wide window at the strictest endpoint this adapter calls. It does not track the separate per-instrument funding budget or the higher per-endpoint budgets.
- Ticker WebSocket delivery is coalesced, as described above.
- Keepalive pings on a fixed interval. The docs describe resetting that timer on every received message. Pinging at least that often still satisfies the 30-second idle close.

## Not implemented

- Private REST, private WebSocket, login, order placement, cancel, balances, and positions with network I/O.
- Incremental books (`books`, `books-l2-tbt`, `books50-l2-tbt`) and checksum checks. Checksum is deprecated and fixed at 0.
- `funding-rate` WebSocket, `GET /api/v5/public/funding-rate-history`, and `settFundingRate`.
- REST trades, `trades-all`, and `GET /api/v5/market/tickers`.
- Margin, inverse, dated futures, options, and `EVENTS`.
- Demo header `x-simulated-trading: 1`, AWS and colo hosts, and regional US/EU hosts.
- `Host` does not reference this project yet. `tests/CryptoTrader.Architecture.Tests/DependencyRuleTests.cs` already expects that reference.
