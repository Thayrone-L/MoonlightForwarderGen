using Microsoft.EntityFrameworkCore;
using MFG.App.Data;
using MFG.Common.Library;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;

namespace MFG.App.Services;

public sealed class LibraryManager
{
    private readonly SunshineManager _sunshineManager = new();
    private readonly SunshineApplicationManager _sunshineApplications = new();
    private readonly MoonlightSwitchManager _moonlightSwitchManager = new();
    private readonly NspDeliveryService _nspDelivery = new();
    private readonly MoonlightAppIdResolver _moonlightAppIds = new();

    public async Task<GameRegistration> RegisterAsync(string name, string executablePath, string? coverPath = null)
    {
        name = name.Trim();
        executablePath = executablePath.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Informe o nome do jogo.");
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("O executável selecionado não foi encontrado.", executablePath);

        await using var database = MfgDbContext.Create();
        var game = await database.Games.FirstOrDefaultAsync(item => item.ExecutablePath == executablePath);
        var now = DateTimeOffset.UtcNow;
        if (game is null)
        {
            game = new LibraryGame
            {
                Id = Guid.NewGuid(),
                NspTitleId = await CreateUniqueTitleIdAsync(database),
                CreatedAt = now
            };
            database.Games.Add(game);
        }
        else
        {
            // O usuário pediu uma nova preparação: o executável pode ter sido atualizado ou movido.
            // Não reaproveitamos um NSP antigo, nem seu Title ID ou estado de favorito.
            await _nspDelivery.RemovePreviousVersionsAsync(game.Name, game.NspPath);
            game.NspPath = null;
            game.MoonlightFavoriteId = null;

        }

        game.Name = name;
        game.ExecutablePath = executablePath;
        game.CoverPath = string.IsNullOrWhiteSpace(coverPath) || !File.Exists(coverPath) ? null : coverPath;
        game.SunshineApplicationName = name;
        game.Status = GameStatus.NeedsSunshineSync;
        game.UpdatedAt = now;
        await database.SaveChangesAsync();

        var sunshine = _sunshineManager.Check();
        if (sunshine.IsInstalled && sunshine.ExecutablePath is not null)
        {
            await _sunshineApplications.SyncAsync(sunshine.ExecutablePath, game);
            // O Sunshine carrega apps.json na inicialização. Reiniciamos agora para
            // que o AppID consultado pelo Moonlight corresponda ao jogo recém-gravado.
            await _sunshineManager.RestartElevatedAsync();
            game.Status = GameStatus.SunshineApplicationReady;
            game.UpdatedAt = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync();
        }

        return ToRegistration(game);
    }

    public async Task<IReadOnlyList<GameRegistration>> HealthCheckAsync()
    {
        var checks = await RunHealthCheckAsync();
        return checks.Select(check => check.Game).ToList();
    }

    public async Task ResetActiveSessionAsync()
    {
        await using var database = MfgDbContext.Create();
        var games = await database.Games.ToListAsync();
        var stopped = 0;
        foreach (var game in games)
        {
            if (TryStopManagedGame(game.ExecutablePath))
                stopped++;
        }

        // Regrava os registros existentes para aplicar auto-detach=false inclusive
        // aos jogos criados antes desta correção. O Sunshine será iniciado depois,
        // já lendo a configuração atualizada.
        var sunshine = _sunshineManager.Check();
        if (sunshine.ExecutablePath is not null)
        {
            foreach (var game in games.Where(game => File.Exists(game.ExecutablePath)))
                await _sunshineApplications.SyncAsync(sunshine.ExecutablePath, game);
        }

        AppLog.Info(stopped == 0
            ? "Sessão anterior do Sunshine limpa; nenhum jogo MFG permaneceu aberto."
            : $"Sessão anterior do Sunshine limpa; {stopped} jogo(s) MFG encerrado(s).");
    }

    public async Task<SdLibrarySyncResult> SyncSdLibraryAsync(CancellationToken cancellationToken = default)
    {
        await using var database = MfgDbContext.Create();
        var games = await database.Games.OrderBy(game => game.Name).ToListAsync(cancellationToken);
        // SQLite não oferece ORDER BY para DateTimeOffset. A lista de remoções é
        // pequena, então a ordenação ocorre depois da leitura sem comprometer o banco.
        var removed = (await database.RemovedForwarders.ToListAsync(cancellationToken))
            .OrderBy(item => item.RemovedAt)
            .ToList();
        return await _nspDelivery.SyncLibraryAsync(games, removed, cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryGame>> GetSwitchPreviewGamesAsync(CancellationToken cancellationToken = default)
    {
        await using var database = MfgDbContext.Create();
        var games = await database.Games
            .AsNoTracking()
            .Where(game => game.NspPath != null)
            .OrderBy(game => game.Name)
            .ToListAsync(cancellationToken);
        return games.Where(game => File.Exists(game.NspPath)).ToList();
    }

    public async Task RemoveAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        await using var database = MfgDbContext.Create();
        var game = await database.Games.FirstOrDefaultAsync(item => item.ExecutablePath == executablePath, cancellationToken);
        if (game is null)
            throw new InvalidOperationException("Este jogo ainda não possui um atalho MFG.");

        await _nspDelivery.RemovePreviousVersionsAsync(game.Name, game.NspPath, cancellationToken);
        var sunshine = _sunshineManager.Check();
        if (sunshine.ExecutablePath is not null)
            await _sunshineApplications.RemoveAsync(sunshine.ExecutablePath, game.SunshineApplicationName, game.ExecutablePath);

        var removed = await database.RemovedForwarders.FindAsync([game.NspTitleId], cancellationToken);
        if (removed is null)
        {
            removed = new RemovedForwarder { TitleId = game.NspTitleId };
            database.RemovedForwarders.Add(removed);
        }
        removed.Name = game.Name;
        removed.RemovedAt = DateTimeOffset.UtcNow;
        database.Games.Remove(game);
        await database.SaveChangesAsync(cancellationToken);
        await SyncSdLibraryAsync(cancellationToken);
        AppLog.Info($"Jogo removido da biblioteca MFG: {game.Name} ({game.NspTitleId}).");
    }

    public async Task<GameRegistration> ResolveMoonlightApplicationIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var database = MfgDbContext.Create();
        var game = await database.Games.FindAsync([gameId], cancellationToken)
            ?? throw new InvalidOperationException("O jogo não existe mais na biblioteca.");
        game.MoonlightFavoriteId = await _moonlightAppIds.ResolveAsync(game.SunshineApplicationName, cancellationToken);
        game.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return ToRegistration(game);
    }

    public async Task<IReadOnlyList<GameHealthCheck>> RunHealthCheckAsync()
    {
        await using var database = MfgDbContext.Create();
        var games = await database.Games.OrderBy(game => game.Name).ToListAsync();
        var sunshine = _sunshineManager.Check();
        var moonlightFavorites = _moonlightSwitchManager.ReadFavorites();
        var checks = new List<GameHealthCheck>();
        foreach (var game in games)
        {
            var executable = CheckExecutable(game);
            var sunshineApplication = CheckSunshineApplication(game, sunshine);
            var moonlightFavorite = CheckMoonlightFavorite(game, moonlightFavorites);
            var nsp = CheckNsp(game);
            var sdCard = _nspDelivery.CheckSdForwarder(game.NspPath);
            game.Status = GetOverallStatus(executable, sunshineApplication, moonlightFavorite, nsp);
            game.UpdatedAt = DateTimeOffset.UtcNow;
            checks.Add(new GameHealthCheck(
                ToRegistration(game),
                executable,
                sunshineApplication,
                moonlightFavorite,
                nsp,
                sdCard));
        }
        await database.SaveChangesAsync();
        return checks;
    }

    public async Task MarkForwarderGeneratedAsync(Guid gameId, string nspPath, string iconPath)
    {
        if (!File.Exists(nspPath))
            throw new FileNotFoundException("O atalho gerado nao foi encontrado.", nspPath);

        await using var database = MfgDbContext.Create();
        var game = await database.Games.FindAsync(gameId)
            ?? throw new InvalidOperationException("O jogo nao existe mais na biblioteca.");
        game.NspPath = nspPath;
        game.CoverPath = File.Exists(iconPath) ? iconPath : game.CoverPath;
        game.Status = GameStatus.ReadyForForwarder;
        game.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync();
    }

    public async Task GrantSunshineConfigurationAccessAsync()
    {
        var sunshine = _sunshineManager.Check();
        if (sunshine.ExecutablePath is null)
            throw new InvalidOperationException("O Sunshine nao esta instalado.");
        await _sunshineApplications.GrantCurrentUserModifyAccessAsync(sunshine.ExecutablePath);
        AppLog.Info("Permissao de gravacao para a configuracao do Sunshine foi solicitada.");
    }

    private static GameRegistration ToRegistration(LibraryGame game) => new(
        game.Id,
        game.Name,
        game.ExecutablePath,
        game.SunshineApplicationName,
        game.MoonlightFavoriteId,
        game.NspTitleId,
        game.NspPath,
        game.Status);

    private static HealthCheckItem CheckExecutable(LibraryGame game) => File.Exists(game.ExecutablePath)
        ? new HealthCheckItem(HealthCheckState.Ready, "Executavel encontrado.")
        : new HealthCheckItem(HealthCheckState.Missing, "O executavel do jogo nao foi encontrado.");

    private static bool TryStopManagedGame(string executablePath)
    {
        if (!File.Exists(executablePath))
            return false;

        var executableName = Path.GetFileNameWithoutExtension(executablePath);
        var terminated = false;
        foreach (var process in Process.GetProcessesByName(executableName))
        {
            try
            {
                var processPath = process.MainModule?.FileName;
                if (!string.Equals(processPath, executablePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                process.Kill(true);
                terminated = true;
            }
            catch (InvalidOperationException)
            {
                // O processo terminou entre a busca e a tentativa de encerramento.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Sem acesso ao processo: a reinicialização do Sunshine ainda limpa a sessão.
            }
            finally
            {
                process.Dispose();
            }
        }

        return terminated;
    }

    private HealthCheckItem CheckSunshineApplication(LibraryGame game, SunshineHealth sunshine)
    {
        if (!sunshine.IsInstalled || sunshine.ExecutablePath is null)
            return new HealthCheckItem(HealthCheckState.Missing, "Sunshine nao esta instalado.");
        if (!sunshine.IsRunning || !sunshine.IsReady)
            return new HealthCheckItem(HealthCheckState.Missing, "Sunshine nao esta em execucao ou ainda nao esta pronto.");
        return _sunshineApplications.ContainsApplication(sunshine.ExecutablePath, game.SunshineApplicationName)
            ? new HealthCheckItem(HealthCheckState.Ready, "Aplicacao registrada no Sunshine.")
            : new HealthCheckItem(HealthCheckState.Missing, "A aplicacao deste jogo nao existe mais no Sunshine.");
    }

    private static HealthCheckItem CheckMoonlightFavorite(LibraryGame game, MoonlightFavoritesStatus favorites)
    {
        if (string.IsNullOrWhiteSpace(game.MoonlightFavoriteId))
            return new HealthCheckItem(HealthCheckState.Missing, "O identificador do jogo no Sunshine ainda não foi obtido.");
        if (favorites.State != HealthCheckState.Ready)
            return new HealthCheckItem(HealthCheckState.Ready, "Identificador do Sunshine salvo; conecte o SD para validar o pareamento.");
        return new HealthCheckItem(HealthCheckState.Ready, "Identificador do Sunshine pronto para o atalho.");
    }

    private static HealthCheckItem CheckNsp(LibraryGame game)
    {
        if (string.IsNullOrWhiteSpace(game.NspPath) || !File.Exists(game.NspPath))
            return new HealthCheckItem(HealthCheckState.Missing, "O NSP do jogo nao foi encontrado na biblioteca local.");
        if (!game.NspPath.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(game.NspPath).StartsWith("MFG - ", StringComparison.OrdinalIgnoreCase) ||
            new FileInfo(game.NspPath).Length == 0)
            return new HealthCheckItem(HealthCheckState.Invalid, "O arquivo NSP nao corresponde ao Title ID registrado.");
        return new HealthCheckItem(HealthCheckState.Ready, "NSP local encontrado e consistente.");
    }

    private static GameStatus GetOverallStatus(
        HealthCheckItem executable,
        HealthCheckItem sunshine,
        HealthCheckItem favorite,
        HealthCheckItem nsp)
    {
        if (!executable.IsReady)
            return GameStatus.ExecutableMissing;
        if (!sunshine.IsReady)
            return sunshine.Message.Contains("Aplicacao", StringComparison.OrdinalIgnoreCase)
                ? GameStatus.SunshineApplicationMissing
                : GameStatus.NeedsSunshineSync;
        if (!nsp.IsReady)
            return GameStatus.NspMissing;
        return GameStatus.ReadyForForwarder;
    }

    private static async Task<string> CreateUniqueTitleIdAsync(MfgDbContext database, Guid? excludedGameId = null)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var randomPart = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            var titleId = $"0100{randomPart}0000";
            if (!await database.Games.AnyAsync(game => game.NspTitleId == titleId && game.Id != excludedGameId))
                return titleId;
        }

        throw new InvalidOperationException("Não foi possível reservar um identificador único para o atalho.");
    }
}
