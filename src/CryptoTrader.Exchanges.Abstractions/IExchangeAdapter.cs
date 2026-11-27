using CryptoTrader.Domain.Common;
using CryptoTrader.Exchanges.Abstractions.Accounts;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Abstractions.Trading;

namespace CryptoTrader.Exchanges.Abstractions;

public interface IExchangeAdapter
{
    string ExchangeCode { get; }

    IMarketDataClient MarketData { get; }

    ITradingClient Trading { get; }

    IAccountClient Account { get; }

    IInstrumentMetadataClient Instruments { get; }

    ExchangeCapabilities Capabilities { get; }

    ExchangeId ExchangeId => new(ExchangeCode);

    IOrderBookClient? OrderBook => null;

    IFundingRateClient? FundingRates => null;
}
