using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheFloor.Content;
using TheFloor.Duels;

namespace TheFloor.Tournaments;

public sealed record ResultCheckpoint(TournamentState State, DuelSession Duel);

public static class TournamentPersistence
{
    public static TournamentState Read(TournamentRecord record) =>
        JsonSerializer.Deserialize<TournamentState>(record.Payload)
            ?? throw new ContentException("The tournament could not be restored. Restore a backup of state/app.db.");

    public static void RecordFinished(AppDb db, DuelSession duel, DateTimeOffset now)
    {
        if (duel.TournamentId is not { } id) return;
        var record = db.Tournaments.Find(id) ?? throw new ContentConflict("The tournament for this duel is missing.");
        var state = Read(record);
        if (state.Phase != TournamentPhase.Playing || state.ActiveDuelId != duel.Id) return;
        if (duel.Started is not null || duel.Status == DuelStatus.Finished && duel.Winner is not null)
        {
            var consumed = TournamentRules.ConsumeCategory(state, duel.Configuration.CategoryId);
            if (state.Categories.Single(c => c.Id == duel.Configuration.CategoryId).Status != CategoryStatus.Played)
                state = consumed with { Version = state.Version + 1 };
        }
        if (duel.Status != DuelStatus.Finished || duel.Winner is null)
        {
            record.Payload = JsonSerializer.Serialize(state);
            return;
        }
        var next = TournamentRules.RecordResult(state, duel, Random.Shared, now) with { Version = state.Version + 1 };
        TournamentRules.Validate(next);
        record.Undo = JsonSerializer.Serialize(new ResultCheckpoint(state, duel));
        record.Payload = JsonSerializer.Serialize(next);
    }

    public static void RequireStandaloneAllowed(IDbContextFactory<AppDb> factory)
    {
        using var db = factory.CreateDbContext();
        if (db.Tournaments.AsNoTracking().AsEnumerable().Any(r => Read(r).Phase != TournamentPhase.Completed))
            throw new ContentConflict("Resume or delete the unfinished Mini Floor tournament before preparing a standalone duel.");
    }

    public static void RequireDuelCommand(IDbContextFactory<AppDb> factory, DuelSession? duel, DuelCommand command)
    {
        if (duel?.TournamentId is not { } id) return;
        using var db = factory.CreateDbContext();
        var record = db.Tournaments.Find(id);
        var tournament = record is null ? null : Read(record);
        if (tournament?.ActiveDuelId != duel.Id || tournament.Phase != TournamentPhase.Playing)
            throw new ContentConflict("This result is recorded. Use Mini Floor's correction control to change it.");
        if (command.Action == "return" || command.Action == "end" && command.Winner is null)
            throw new ContentConflict("Pause this tournament duel to leave it, or declare a winner. Restart/delete the tournament from Mini Floor with confirmation.");
    }
}
