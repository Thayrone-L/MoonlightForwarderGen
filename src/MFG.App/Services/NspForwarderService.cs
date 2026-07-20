using MFG.Common.Library;
using MFG.Common.Nsp;
using System.IO;

namespace MFG.App.Services;

public sealed class NspForwarderService
{
    private readonly NspGenerationService _planner = new();
    private readonly NspGeneratorBackend _generator = new();
    private readonly NspDeliveryService _delivery = new();
    private readonly LibraryManager _library = new();

    public async Task<NspForwarderResult> GenerateAndDeliverAsync(
        GameRegistration game,
        string hostIp,
        string moonlightNroPath,
        string iconPath,
        string publisher = "MFG",
        CancellationToken cancellationToken = default)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "nsp-generator", "template");
        var plan = _planner.CreatePlan(
            game,
            hostIp,
            moonlightNroPath,
            templatePath,
            iconPath,
            _delivery.GetLocalForwarderDirectory(),
            publisher);
        var generated = await _generator.GenerateAsync(plan, cancellationToken);
        var delivery = await _delivery.DeliverAsync(generated.OutputPath, cancellationToken);
        await _library.MarkForwarderGeneratedAsync(game.Id, generated.OutputPath);
        return new NspForwarderResult(generated, delivery);
    }
}

public sealed record NspForwarderResult(NspGenerationResult Generated, NspDeliveryResult Delivery);
