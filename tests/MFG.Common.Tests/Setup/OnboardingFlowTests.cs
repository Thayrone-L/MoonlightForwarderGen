using MFG.Common.Setup;
using Xunit;

namespace MFG.Common.Tests.Setup;

public sealed class OnboardingFlowTests
{
    [Fact]
    public void StartsAtFirstIncompleteRequirement()
    {
        Assert.Equal(OnboardingStep.Computer, new OnboardingFlow(false, false, false).Step);
        Assert.Equal(OnboardingStep.Key, new OnboardingFlow(true, false, false).Step);
        Assert.Equal(OnboardingStep.Switch, new OnboardingFlow(true, true, false).Step);
        Assert.Equal(OnboardingStep.Finish, new OnboardingFlow(true, true, true).Step);
    }

    [Fact]
    public void RestoresAnEarlierSavedStep()
    {
        var flow = new OnboardingFlow(true, true, false, OnboardingStep.Key);
        Assert.Equal(OnboardingStep.Key, flow.Step);
    }

    [Fact]
    public void DoesNotSkipAnIncompleteRequirementFromSavedState()
    {
        var flow = new OnboardingFlow(true, false, false, OnboardingStep.Finish);
        Assert.Equal(OnboardingStep.Key, flow.Step);
        Assert.False(flow.CanContinue);
    }

    [Fact]
    public void ContinuesOnlyWhenCurrentRequirementIsReady()
    {
        var flow = new OnboardingFlow(false, false, false);
        Assert.False(flow.MoveNext());
        flow.UpdateReadiness(true, false, false);
        Assert.True(flow.MoveNext());
        Assert.Equal(OnboardingStep.Key, flow.Step);
        Assert.False(flow.MoveNext());
    }

    [Fact]
    public void NavigatesBackWithoutLeavingTheValidRange()
    {
        var flow = new OnboardingFlow(true, true, true, OnboardingStep.Key);
        Assert.True(flow.MoveBack());
        Assert.Equal(OnboardingStep.Computer, flow.Step);
        Assert.False(flow.MoveBack());
    }
}
