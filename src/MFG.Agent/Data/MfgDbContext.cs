using Microsoft.EntityFrameworkCore;

namespace MFG.Agent.Data;

public sealed class MfgDbContext : DbContext
{
    public DbSet<GameEntry> Games => Set<GameEntry>();
    public DbSet<SunshineInstallation> SunshineInstallations => Set<SunshineInstallation>();

    private MfgDbContext(DbContextOptions<MfgDbContext> options) : base(options)
    {
    }

    public static MfgDbContext Create()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoonlightForwarderGenerator");
        Directory.CreateDirectory(dataDirectory);

        var databasePath = Path.Combine(dataDirectory, "mfg.db");
        var options = new DbContextOptionsBuilder<MfgDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new MfgDbContext(options);
    }

    public static async Task InitializeAsync()
    {
        using var database = Create();
        await database.Database.EnsureCreatedAsync();

        // Mantém compatibilidade com bancos criados antes da tabela de instalação do Sunshine.
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS SunshineInstallations (
                Id INTEGER NOT NULL CONSTRAINT PK_SunshineInstallations PRIMARY KEY,
                IsInstalled INTEGER NOT NULL,
                ExecutablePath TEXT NULL,
                LastCheckedAt TEXT NOT NULL
            );
            """);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var game = modelBuilder.Entity<GameEntry>();
        game.ToTable("Games");
        game.HasKey(entry => entry.Id);
        game.Property(entry => entry.Id).HasMaxLength(128);
        game.Property(entry => entry.ExecutablePath).IsRequired();

        var sunshine = modelBuilder.Entity<SunshineInstallation>();
        sunshine.ToTable("SunshineInstallations");
        sunshine.HasKey(installation => installation.Id);
        sunshine.Property(installation => installation.IsInstalled).IsRequired();
        sunshine.Property(installation => installation.LastCheckedAt).IsRequired();
    }
}
