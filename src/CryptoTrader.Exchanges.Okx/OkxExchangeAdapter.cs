using CryptoTrader.Domain.Common;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.Accounts;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Abstractions.Trading;
using CryptoTrader.Exchanges.Okx.Accounts;
using CryptoTrader.Exchanges.Okx.MarketData;
using CryptoTrader.Exchanges.Okx.Trading;

namespace CryptoTrader.Exchanges.Okx;

public sealed class OkxExchangeAdapter : IExchangeAdapter
{
    public const string Code = "OKX";

    public static readonly ExchangeId Id = new(Code);

    private readonly OkxOrderBookClient _orderBook;

    public static readonly ExchangeCapabilities PublicCapabilities = new(
        SupportsSpot: true,
        SupportsLinearPerpetual: true,
        SupportsLiveOrders: false,
        SupportsOrderBook: true,
        SupportsFundingRates: true,
        SupportsWebSocket: true,
        SupportsPrivateAccount: false);

    public OkxExchangeAdapter(
        OkxMarketDataClient marketData,
        OkxTradingClient trading,
        OkxAccountClient account,
        OkxInstrumentMetadataClient instruments,
        OkxFundingClient funding)
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
        _orderBook = new OkxOrderBookClient(marketData);
    }

    public string ExchangeCode => Code;

    public IMarketDataClient MarketData { get; }

    public ITradingClient Trading { get; }

    public IAccountClient Account { get; }

    public IInstrumentMetadataClient Instruments { get; }

    public OkxFundingClient Funding { get; }

    IOrderBookClient? IExchangeAdapter.OrderBook => _orderBook;

    IFundingRateClient? IExchangeAdapter.FundingRates => Funding;

    public ExchangeCapabilities Capabilities => PublicCapabilities;
}
