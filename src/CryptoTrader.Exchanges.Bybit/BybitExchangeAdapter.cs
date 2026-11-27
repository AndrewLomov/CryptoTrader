using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.Accounts;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Abstractions.Trading;
using CryptoTrader.Exchanges.Bybit.Accounts;
using CryptoTrader.Exchanges.Bybit.MarketData;
using CryptoTrader.Exchanges.Bybit.Trading;

namespace CryptoTrader.Exchanges.Bybit;

public sealed class BybitExchangeAdapter : IExchangeAdapter
{
    public const string Code = "BYBIT";

    public static readonly ExchangeCapabilities PublicCapabilities = new(
        SupportsSpot: true,
        SupportsLinearPerpetual: true,
        SupportsLiveOrders: false,
        SupportsOrderBook: true,
        SupportsFundingRates: true,
        SupportsWebSocket: true,
        SupportsPrivateAccount: false);

    public BybitExchangeAdapter(
        BybitMarketDataClient marketData,
        BybitTradingClient trading,
        BybitAccountClient account,
        BybitInstrumentMetadataClient instruments,
        BybitFundingClient funding)
    {
        ArgumentNullException.ThrowIfNull(marketData);
        ArgumentNullException.ThrowIfNull(trading);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(instruments);
        ArgumentNullException.ThrowIfNull(funding);
        MarketData = marketData;
        Trading = trading;
        Account = account;
        Instruments = instruments;
        Funding = funding;
    }

    public string ExchangeCode => Code;

    public IMarketDataClient MarketData { get; }

    public ITradingClient Trading { get; }

    public IAccountClient Account { get; }

    public IInstrumentMetadataClient Instruments { get; }

    public BybitFundingClient Funding { get; }

    public ExchangeCapabilities Capabilities => PublicCapabilities;
}
