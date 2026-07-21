namespace MFG.Common.Setup;

public enum OnboardingStep
{
    Computer,
    Key,
    Switch,
    Finish
}

public sealed class OnboardingFlow
{
    public OnboardingFlow(bool computerReady, bool keyReady, bool switchReady, OnboardingStep? savedStep = null)
    {
        ComputerReady = computerReady;
        KeyReady = keyReady;
        SwitchReady = switchReady;
        Step = FirstIncompleteStep();

        if (savedStep is not null && savedStep.Value < Step)
            Step = savedStep.Value;
    }

    public OnboardingStep Step { get; private set; }
    public bool ComputerReady { get; private set; }
    public bool KeyReady { get; private set; }
    public bool SwitchReady { get; private set; }
    public bool CanGoBack => Step > OnboardingStep.Computer;
    public bool CanContinue => Step switch
    {
        OnboardingStep.Computer => ComputerReady,
        OnboardingStep.Key => KeyReady,
        OnboardingStep.Switch => SwitchReady,
        _ => true
    };

    public void UpdateReadiness(bool computerReady, bool keyReady, bool switchReady)
    {
        ComputerReady = computerReady;
        KeyReady = keyReady;
        SwitchReady = switchReady;
    }

    public bool MoveNext()
    {
        if (!CanContinue || Step == OnboardingStep.Finish)
            return false;

        Step++;
        return true;
    }

    public bool MoveBack()
    {
        if (!CanGoBack)
            return false;

        Step--;
        return true;
    }

    private OnboardingStep FirstIncompleteStep()
    {
        if (!ComputerReady) return OnboardingStep.Computer;
        if (!KeyReady) return OnboardingStep.Key;
        if (!SwitchReady) return OnboardingStep.Switch;
        return OnboardingStep.Finish;
    }
}
