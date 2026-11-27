using CryptoTrader.Domain.Common;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.Accounts;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Abstractions.Trading;
using Shouldly;

namespace CryptoTrader.Exchanges.ContractTests;

public class ExchangeRegistryOrderTests
{
    [Fact]
    public void Lookup_by_exchange_id_does_not_depend_on_registration_order()
    {
        var bybit = new StubAdapter("BYBIT");
        var okx = new StubAdapter("OKX");
        var bybitFirst = new ExchangeRegistry([bybit, okx]);
        var okxFirst = new ExchangeRegistry([okx, bybit]);

        bybitFirst.GetRequired(ExchangeId.Okx).ShouldBeSameAs(okx);
        okxFirst.GetRequired(ExchangeId.Okx).ShouldBeSameAs(okx);
        bybitFirst.GetRequired(ExchangeId.Bybit).ShouldBeSameAs(bybit);
        okxFirst.GetRequired(ExchangeId.Bybit).ShouldBeSameAs(bybit);
        bybitFirst.GetRequired(new ExchangeId("OKX")).ExchangeCode.ShouldBe("OKX");
        okxFirst.GetRequired(new ExchangeId("BYBIT")).ExchangeCode.ShouldBe("BYBIT");
    }

    private sealed class StubAdapter : IExchangeAdapter
    {
        public StubAdapter(string code) => ExchangeCode = code;

        public string ExchangeCode { get; }

        public IMarketDataClient MarketData => throw new NotSupportedException();

        public ITradingClient Trading => throw new NotSupportedException();

        public IAccountClient Account => throw new NotSupportedException();

        public IInstrumentMetadataClient Instruments => throw new NotSupportedException();

        public ExchangeCapabilities Capabilities => new(
            SupportsSpot: true,
            SupportsLinearPerpetual: false,
            SupportsLiveOrders: false,
            SupportsOrderBook: false,
            SupportsFundingRates: false,
            SupportsWebSocket: false,
            SupportsPrivateAccount: false);
    }
}
