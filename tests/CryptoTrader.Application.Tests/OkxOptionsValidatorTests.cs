using CryptoTrader.Application.Configuration;
using Shouldly;

namespace CryptoTrader.Application.Tests;

public sealed class OkxOptionsValidatorTests
{
    [Theory]
    [InlineData("SPOT", "SWAP")]
    [InlineData("spot", "swap")]
    [InlineData(" SPOT ", " swap ")]
    public void Known_types_pass_regardless_of_case_and_surrounding_spaces(string spot, string swap)
    {
        var result = new OkxOptionsValidator().Validate(null, Options(spot, swap));

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Case_insensitive_duplicates_fail()
    {
        var result = new OkxOptionsValidator().Validate(null, Options("SPOT", "spot"));

        result.Succeeded.ShouldBeFalse();
        result.FailureMessage.ShouldNotBeNull().ShouldContain("duplicate");
    }

    [Fact]
    public void Unknown_and_blank_values_fail_at_validation()
    {
        var unknown = new OkxOptionsValidator().Validate(null, Options("FUTURES"));
        var blank = new OkxOptionsValidator().Validate(null, Options(" "));

        unknown.Succeeded.ShouldBeFalse();
        blank.Succeeded.ShouldBeFalse();
        unknown.FailureMessage.ShouldNotBeNull().ShouldContain("FUTURES");
    }

    private static OkxOptions Options(params string[] instrumentTypes) => new()
    {
        InstrumentTypes = instrumentTypes,
    };
}
