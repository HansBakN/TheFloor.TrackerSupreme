using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheFloor.Content;

namespace TheFloor;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    public DbSet<DuelRecord> Duels => Set<DuelRecord>();

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
