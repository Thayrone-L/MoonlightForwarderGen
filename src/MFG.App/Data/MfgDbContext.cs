using Microsoft.EntityFrameworkCore;
using System.IO;

namespace MFG.App.Data;

public sealed class MfgDbContext : DbContext
{
    public DbSet<LibraryGame> Games => Set<LibraryGame>();
    public DbSet<DiscoveredGame> DiscoveredGames => Set<DiscoveredGame>();
    public DbSet<SecureKeySetting> SecureKeySettings => Set<SecureKeySetting>();
    public DbSet<RemovedForwarder> RemovedForwarders => Set<RemovedForwarder>();

    private MfgDbContext(DbContextOptions<MfgDbContext> options) : base(options)
    {
    }

    public static MfgDbContext Create()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoonlightForwarderGenerator");
        Directory.CreateDirectory(directory);

        var options = new DbContextOptionsBuilder<MfgDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "mfg.db")}")
            .Options;
        return new MfgDbContext(options);
    }

    public static async Task InitializeAsync()
    {
        await using var database = Create();
        await database.Database.EnsureCreatedAsync();
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS LibraryGames (
                Id TEXT NOT NULL CONSTRAINT PK_LibraryGames PRIMARY KEY,
                Name TEXT NOT NULL,
                ExecutablePath TEXT NOT NULL,
                SunshineApplicationName TEXT NOT NULL,
                MoonlightFavoriteId TEXT NULL,
                NspTitleId TEXT NULL,
                NspPath TEXT NULL,
                CoverPath TEXT NULL,
                Status INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """);
        await EnsureNspTitleIdColumnAsync(database);
        await EnsureLibraryGameColumnAsync(database, "CoverPath");
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS SecureKeySettings (
                Id INTEGER NOT NULL CONSTRAINT PK_SecureKeySettings PRIMARY KEY,
                Sha256 TEXT NOT NULL,
                ImportedAt TEXT NOT NULL
            );
            """);
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS RemovedForwarders (
                TitleId TEXT NOT NULL CONSTRAINT PK_RemovedForwarders PRIMARY KEY,
                Name TEXT NOT NULL,
                RemovedAt TEXT NOT NULL
            );
            """);
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS DiscoveredGames (
                Id TEXT NOT NULL CONSTRAINT PK_DiscoveredGames PRIMARY KEY,
                Source INTEGER NOT NULL,
                SourceId TEXT NOT NULL,
                Name TEXT NOT NULL,
                InstallDirectory TEXT NOT NULL,
                ExecutablePath TEXT NULL,
                CoverPath TEXT NULL,
                Description TEXT NULL,
                Developer TEXT NULL,
                Publisher TEXT NULL,
                DiscoveredAt TEXT NOT NULL,
                MetadataUpdatedAt TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_DiscoveredGames_Source_SourceId ON DiscoveredGames (Source, SourceId);
            """);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var game = modelBuilder.Entity<LibraryGame>();
        game.ToTable("LibraryGames");
        game.HasKey(entry => entry.Id);
        game.Property(entry => entry.Name).HasMaxLength(200).IsRequired();
        game.Property(entry => entry.ExecutablePath).IsRequired();
        game.Property(entry => entry.SunshineApplicationName).HasMaxLength(240).IsRequired();
        game.Property(entry => entry.NspTitleId).HasMaxLength(16).IsRequired();

        var key = modelBuilder.Entity<SecureKeySetting>();
        key.ToTable("SecureKeySettings");
        key.HasKey(entry => entry.Id);
        key.Property(entry => entry.Sha256).IsRequired();
        key.Property(entry => entry.ImportedAt).IsRequired();

        var removed = modelBuilder.Entity<RemovedForwarder>();
        removed.ToTable("RemovedForwarders");
        removed.HasKey(entry => entry.TitleId);
        removed.Property(entry => entry.Name).HasMaxLength(300).IsRequired();

        var discovered = modelBuilder.Entity<DiscoveredGame>();
        discovered.ToTable("DiscoveredGames");
        discovered.HasKey(entry => entry.Id);
        discovered.HasIndex(entry => new { entry.Source, entry.SourceId }).IsUnique();
        discovered.Property(entry => entry.SourceId).HasMaxLength(160).IsRequired();
        discovered.Property(entry => entry.Name).HasMaxLength(300).IsRequired();
        discovered.Property(entry => entry.InstallDirectory).IsRequired();
    }

    private static async Task EnsureNspTitleIdColumnAsync(MfgDbContext database)
    {
        await EnsureLibraryGameColumnAsync(database, "NspTitleId");
    }

    private static async Task EnsureLibraryGameColumnAsync(MfgDbContext database, string columnName)
    {
        await using var connection = database.Database.GetDbConnection();
        await connection.OpenAsync();
        var exists = false;
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(LibraryGames);";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (!exists)
        {
            await connection.CloseAsync();
            var migrationSql = columnName switch
            {
                "NspTitleId" => "ALTER TABLE LibraryGames ADD COLUMN NspTitleId TEXT NULL;",
                "CoverPath" => "ALTER TABLE LibraryGames ADD COLUMN CoverPath TEXT NULL;",
                _ => throw new InvalidOperationException($"Coluna de migração desconhecida: {columnName}")
            };
            await database.Database.ExecuteSqlRawAsync(migrationSql);
        }
    }
}
