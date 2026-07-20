namespace MFG.Common.Library;

public enum HealthCheckState
{
    Ready,
    Missing,
    Unavailable,
    Invalid
}

public sealed record HealthCheckItem(HealthCheckState State, string Message)
{
    public bool IsReady => State == HealthCheckState.Ready;
}

public sealed record GameHealthCheck(
    GameRegistration Game,
    HealthCheckItem Executable,
    HealthCheckItem Sunshine,
    HealthCheckItem MoonlightFavorite,
    HealthCheckItem Nsp,
    HealthCheckItem SdCard)
{
    public bool IsReady => Executable.IsReady && Sunshine.IsReady && MoonlightFavorite.IsReady && Nsp.IsReady && SdCard.IsReady;
}
