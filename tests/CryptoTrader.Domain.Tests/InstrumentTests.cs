using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Tests.Support;
using Shouldly;

namespace CryptoTrader.Domain.Tests;

public class InstrumentTests
{
    [Fact]
    public void Okx_exchange_id_is_OKX()
    {
        ExchangeId.Okx.Value.ShouldBe("OKX");
        ExchangeId.Okx.ShouldBe(new ExchangeId("OKX"));
        ExchangeId.Bybit.Value.ShouldBe("BYBIT");
    }

    [Fact]
    public void Spot_leaves_contract_metadata_empty()
    {
        var spot = DomainTestData.SpotBtc();

        spot.ContractMultiplier.ShouldBeNull();
        spot.ContractValue.ShouldBeNull();
        spot.ContractValueCurrency.ShouldBeNull();
        spot.SettlementCurrency.ShouldBeNull();
        spot.ContractKind.ShouldBeNull();
        spot.HasCompleteLinearContractSpec.ShouldBeFalse();
    }

    [Fact]
    public void Spot_rejects_contract_metadata()
    {
        var exception = Should.Throw<DomainException>(() => Spot(contractValue: 0.01m));

        exception.Message.ShouldContain("Spot");
        Should.Throw<DomainException>(() => Spot(contractKind: "linear")).Message.ShouldContain("Spot");
        Should.Throw<DomainException>(() => Spot(settlementCurrency: "USDT")).Message.ShouldContain("Spot");
    }

    [Fact]
    public void Missing_contract_value_stays_null()
    {
        var instrument = Linear();

        instrument.ContractValue.ShouldBeNull();
        instrument.ContractMultiplier.ShouldBe(1m);
        instrument.HasCompleteLinearContractSpec.ShouldBeFalse();
    }

    [Fact]
    public void Complete_linear_spec_stores_kind_as_linear_and_does_not_replace_value_with_one()
    {
        var instrument = Linear(
            contractValue: 0.01m,
            contractValueCurrency: " BTC ",
            settlementCurrency: "USDT",
            contractKind: " Linear ");

        instrument.ContractValue.ShouldBe(0.01m);
        instrument.ContractValueCurrency.ShouldBe("BTC");
        instrument.SettlementCurrency.ShouldBe("USDT");
        instrument.ContractKind.ShouldBe("linear");
        instrument.HasCompleteLinearContractSpec.ShouldBeTrue();
    }

    [Fact]
    public void Partial_linear_spec_is_not_complete()
    {
        var instrument = Linear(
            contractValue: 0.01m,
            contractValueCurrency: "BTC",
            contractKind: "linear");

        instrument.HasCompleteLinearContractSpec.ShouldBeFalse();
    }

    [Fact]
    public void Linear_rejects_non_positive_contract_value_and_non_linear_kind()
    {
        Should.Throw<DomainException>(() => Linear(contractValue: 0m)).Message.ShouldContain("greater than zero");
        Should.Throw<DomainException>(() => Linear(contractValue: -1m)).Message.ShouldContain("greater than zero");
        Should.Throw<DomainException>(() => Linear(contractKind: "inverse")).Message.ShouldContain("linear");
        Should.Throw<DomainException>(() => Linear(contractValueCurrency: " ")).Message.ShouldContain("contractValueCurrency");
        Should.Throw<DomainException>(() => Linear(contractMultiplier: null))
            .Message.ShouldContain("contract multiplier");
    }

    private static Instrument Spot(
        decimal? contractValue = null,
        string? settlementCurrency = null,
        string? contractKind = null) =>
        Instrument.Create(
            new InstrumentId("BTCUSDT"),
            ExchangeId.Bybit,
            "BTCUSDT",
            "BTC",
            "USDT",
            InstrumentType.Spot,
            0.01m,
            0.001m,
            0.001m,
            5m,
            null,
            true,
            contractValue,
            null,
            settlementCurrency,
            contractKind);

    private static Instrument Linear(
        decimal? contractValue = null,
        string? contractValueCurrency = null,
        string? settlementCurrency = null,
        string? contractKind = null,
        decimal? contractMultiplier = 1m) =>
        Instrument.Create(
            new InstrumentId("OKX-LINEAR-SWAP-BTC-USDT"),
            ExchangeId.Okx,
            "BTC-USDT-SWAP",
            "BTC",
            "USDT",
            InstrumentType.LinearPerpetual,
            0.1m,
            1m,
            1m,
            null,
            contractMultiplier,
            true,
            contractValue,
            contractValueCurrency,
            settlementCurrency,
            contractKind);
}
