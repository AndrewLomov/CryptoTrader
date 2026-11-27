# Adapter report

Date: 27 November 2026. Agent 4. No commit. `dotnet build` and `dotnet test` were not run.

## Official pages

Checked against the live English V5 HTML, not the truncated capture.

| Page | What was taken from it |
|---|---|
| https://www.okx.com/docs-v5/en/ | REST `https://openapi.okx.com`, public `wss://ws.okx.com/ws/v5/public`, business `wss://ws.okx.com/ws/v5/business`, text `ping` / `pong`, idle close at 30 seconds, code `50011` |
| https://www.okx.com/docs-v5/en/#order-book-trading-market-data-get-ticker | `GET /api/v5/market/ticker`, 20 requests / 2 seconds, IP |
| https://www.okx.com/docs-v5/en/#order-book-trading-market-data-get-order-book | `GET /api/v5/market/books`, 40 requests / 2 seconds, `sz` max 400, default 1 |
| https://www.okx.com/docs-v5/en/#order-book-trading-market-data-get-candlesticks | `GET /api/v5/market/candles`, latest 1,440, `limit` max 300, 40 requests / 2 seconds, `after` is older than `ts` |
| https://www.okx.com/docs-v5/en/#order-book-trading-market-data-get-candlesticks-history | `GET /api/v5/market/history-candles`, `limit` max 300, 20 requests / 2 seconds |
| https://www.okx.com/docs-v5/en/#public-data-rest-api-get-instruments | `GET /api/v5/public/instruments`, 20 requests / 2 seconds, IP + instrument type. `ctVal` example `0.01` BTC for `BTC-USDT-SWAP`. States `live`, `suspend`, `rebase`, `post_only`, `preopen`, `test`, `settling` |
| https://www.okx.com/docs-v5/en/#public-data-rest-api-get-funding-rate | `GET /api/v5/public/funding-rate`, 10 requests / 2 seconds, IP + instrument id. `fundingRate` is the predicted upcoming rate. `fundingTime` is the settlement time in milliseconds |
| https://www.okx.com/docs-v5/log_en/ | `openapi.okx.com` (2026-05-20). Port 8443 stopped for the public internet on 31 October 2026 |

`books5` stays on `/ws/v5/public` and is a 5-level snapshot. Candle channels stay on `/ws/v5/business`.

## Conflicts

`docs/okx-api-reference-notes.md` does not quote a different host, port, history limit, or volume rule. It does say the ticker, public instruments, and funding-rate numeric limits were not on the captured text. The full English page states them: 20/2s, 20/2s (IP + instrument type), and 10/2s (IP + instrument id). The adapter uses those English figures. The shared REST window is the strictest of the calls it makes: 10 requests per 2 seconds.

The multi-exchange contract placeholder `https://www.okx.com` and `wss://ws.okx.com:8443/ws/v5/public` are not used. Candle streams need `BusinessWebSocketUrl` (`wss://ws.okx.com/ws/v5/business`), which was not in the contract's option list. History `limit` is 300. Daily bars use `1Dutc` so the open is UTC+0. `1D` is UTC+8.

## Endpoints and channels in code

REST: instruments (`SPOT`, `SWAP`), candles, history-candles, ticker, books `sz=50`, funding-rate.

WebSocket: `tickers`, `trades`, `books5` on the public socket. `candle1m`, `candle5m`, `candle15m`, `candle1H`, `candle4H`, `candle1Dutc` on the business socket.

Not called: private REST, private WebSocket, `books` incremental, `funding-rate` socket, funding-rate history, REST trades, `trades-all`, tickers (plural), margin, inverse, dated futures, options.

Order books are snapshots only. An inconsistent or empty book sets `IsConsistent` false and does not invent levels. Tickers on the socket collapse to the latest value. Candles, trades, and book events wait on a bounded channel.

## Bybit behavior

`BybitOptions.Enabled` defaults to true. `ChannelCapacity` defaults to 256. `Enabled` false binds options and the `bybit` `HttpClient` and does not register the adapter. The ambiguous `IMarketDataClient`, `ITradingClient`, `IAccountClient`, `IInstrumentMetadataClient`, and `IFundingRateClient` singletons are gone. `IExchangeAdapter` remains. `OrderBook` and `FundingRates` are explicit on the adapter. The public stream is a bounded `Wait` channel and logs when a write waits. A subscribe `success: false`, or no success within 10 seconds, fails the session. Three unacked sessions fault the reader. Reconnect subscribes again. `MaxKlinePages` is 500. Hitting it, a stalled cursor, or a confirmed gap returns `Incomplete`. `GetCandlesAsync` throws `IncompleteCandleHistoryException` for that result. A warning is not success.

`Kline_numeric_strings_parse_to_decimal_and_utc` requests one hour, so the cursor lands on `to` and the single closed candle can still be `Complete`. `Incomplete_kline_range_throws` requests two hours against a stub that repeats one candle and expects the throw. Those tests were not edited.

## Files

Created `src/CryptoTrader.Exchanges.Okx/` (`CryptoTrader.Exchanges.Okx.csproj`, `AssemblyInfo.cs`, options, exception, adapter, DI, REST, mapping, market data, trading, account, WebSocket). Created `src/CryptoTrader.Exchanges.Bybit/MarketData/BybitOrderBookClient.cs`. Updated Bybit options, DI, adapter, market-data client, and public stream. Added the OKX project under `/src/` in `CryptoTrader.slnx`. Added `CryptoTrader.Exchanges.Okx` → `CryptoTrader.Exchanges.Abstractions` and added OKX to the expected Host reference list in `DependencyRuleTests.cs`. Wrote `docs/okx-integration.md` and `docs/agents/review-reports/adapter.md`. Updated `docs/exchange-integration.md`.

`CryptoTrader.Host.csproj` was not edited. The architecture test will fail on the Host reference until the Host agent adds `CryptoTrader.Exchanges.Okx`. Domain and Abstractions were not edited. The OKX project compiles only if those contracts are present (`VolumeUnit`, candle and instrument optional arguments, `IOrderBookClient`, `CandleHistory`, `GetCandleHistoryAsync`, adapter `OrderBook` / `FundingRates`). They were present in the tree at the end of this pass. This agent did not build.

## Left incomplete

Private trading and account data, incremental books, funding history, the funding-rate channel, REST trades, demo trading, and non-linear products. The rate limiter is one global 10-per-2-seconds window, not a separate budget per endpoint. Ticker updates can be coalesced. Host does not reference the OKX project yet. No build and no tests were run.
