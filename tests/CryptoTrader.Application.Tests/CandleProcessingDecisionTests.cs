using CryptoTrader.Application.MarketData;
using Shouldly;

namespace CryptoTrader.Application.Tests;

public sealed class CandleProcessingDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 3, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(180);

    [Fact]
    public void Close_age_equal_to_the_window_can_update_paper()
    {
        var decision = CandleProcessingDecision.Evaluate(true, Now - Window, Now, Now, Window);

        decision.CanPersistHistory.ShouldBeTrue();
        decision.CanUpdatePaper.ShouldBeTrue();
        decision.Admission.ShouldBe(CandleAdmission.Eligible);
    }

    [Fact]
    public void Close_age_beyond_the_window_is_stored_and_not_tradable()
    {
        var decision = CandleProcessingDecision.Evaluate(true, Now - Window - TimeSpan.FromTicks(1), Now, Now, Window);

        decision.CanPersistHistory.ShouldBeTrue();
        decision.CanUpdatePaper.ShouldBeFalse();
        decision.Admission.ShouldBe(CandleAdmission.StaleClose);
        decision.Reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Queue_lag_equal_to_the_window_can_update_paper()
    {
        var decision = CandleProcessingDecision.Evaluate(true, Now, Now - Window, Now, Window);

        decision.CanUpdatePaper.ShouldBeTrue();
        decision.Admission.ShouldBe(CandleAdmission.Eligible);
    }

    [Fact]
    public void Queue_lag_beyond_the_window_is_stored_and_not_tradable()
    {
        var decision = CandleProcessingDecision.Evaluate(true, Now, Now - Window - TimeSpan.FromTicks(1), Now, Window);

        decision.CanPersistHistory.ShouldBeTrue();
        decision.CanUpdatePaper.ShouldBeFalse();
        decision.Admission.ShouldBe(CandleAdmission.ConsumerBehind);
    }

    [Fact]
    public void Open_candle_is_not_history_and_not_tradable()
    {
        var decision = CandleProcessingDecision.Evaluate(false, Now, Now, Now, Window);

        decision.CanPersistHistory.ShouldBeFalse();
        decision.CanUpdatePaper.ShouldBeFalse();
        decision.Admission.ShouldBe(CandleAdmission.NotClosed);
    }
}
