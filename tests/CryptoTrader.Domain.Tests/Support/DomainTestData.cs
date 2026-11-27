using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Trading;

namespace CryptoTrader.Domain.Tests.Support;

internal static class DomainTestData
{
    public static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static Instrument SpotBtc(bool tradingEnabled = true) => Instrument.Create(
        new InstrumentId("BTCUSDT"),
        ExchangeId.Bybit,
        "BTCUSDT",
        "BTC",
        "USDT",
        InstrumentType.Spot,
        priceTick: 0.01m,
        quantityStep: 0.001m,
        minQuantity: 0.001m,
        minNotional: 5m,
        contractMultiplier: null,
        tradingEnabled);

    public static RiskProfile Profile(
        decimal riskPerTradePercent = 1m,
        decimal feeRate = 0m,
        decimal slippageBps = 0m) => RiskProfile.Create(
        riskPerTradePercent,
        maxDailyLossPercent: 1m,
        maxOpenPositions: 2,
        maxExposurePercent: 100m,
        feeRate,
        slippageBps,
        averagingDownEnabled: false,
        maxDrawdownPercent: 10m,
        allowFuturesLive: false,
        allowCrossMargin: false);

    public static Order MarketBuy(Quantity quantity, string clientOrderId = "client-1") => Order.Create(
        OrderId.New(),
        clientOrderId,
        TradingAccountId.New(),
        ExchangeId.Bybit,
        SpotBtc(),
        OrderSide.Buy,
        OrderType.Market,
        quantity,
        limitPrice: null,
        reduceOnly: false,
        TradingMode.Paper,
        Now);

    public static Candle Hour(DateTimeOffset openTimeUtc, decimal price = 100m, decimal? volume = null) =>
        Candle.Create(
            ExchangeId.Bybit,
            new InstrumentId("BTCUSDT"),
            Timeframe.OneHour,
            openTimeUtc,
            openTimeUtc.AddHours(1),
            price,
            price,
            price,
            price,
            volume ?? 1m,
            isClosed: true);
}
