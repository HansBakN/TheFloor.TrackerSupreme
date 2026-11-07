using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheFloor.Content;

namespace TheFloor;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    public DbSet<DuelRecord> Duels => Set<DuelRecord>();
    public DbSet<DuelSnapshot> DuelSnapshots => Set<DuelSnapshot>();
    public DbSet<TournamentRecord> Tournaments => Set<TournamentRecord>();
    public DbSet<TournamentDeck> TournamentDecks => Set<TournamentDeck>();

    public void Initialize()
    {
        Database.EnsureCreated();
        // Existing installations were created with EnsureCreated and have no migrations table.
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Tournaments" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "Payload" TEXT NOT NULL,
                "Undo" TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS "TournamentDecks" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "Payload" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "DuelSnapshots" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "Questions" TEXT NOT NULL
            );
            """);
    }

    public static void Configure(DbContextOptionsBuilder options, DataPaths paths) =>
        options.UseSqlite(new SqliteConnectionStringBuilder
        {
            DataSource = DataPaths.SafePath(paths.State, "app.db"),
        }.ToString());
}

public sealed class AppSettings
{
    public int Id { get; init; } = 1;
    public Guid? ActiveSetId { get; set; }
}

public sealed class DuelRecord
{
    public Guid Id { get; init; }
    public bool Current { get; set; }
    public required string Payload { get; set; }
}

public sealed class DuelSnapshot
{
    public Guid Id { get; init; }
    public required string Questions { get; set; }
}

public sealed class TournamentRecord
{
    public Guid Id { get; init; }
    public required string Payload { get; set; }
    public string? Undo { get; set; }
}
public sealed class TournamentDeck
{
    public Guid Id { get; init; }
    public required string Payload { get; set; }
}
