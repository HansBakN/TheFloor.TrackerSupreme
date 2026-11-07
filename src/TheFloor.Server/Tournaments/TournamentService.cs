using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheFloor.Content;
using TheFloor.Duels;

namespace TheFloor.Tournaments;

public sealed class TournamentService(IDbContextFactory<AppDb> database, ContentLibrary library, DuelService duels, TimeProvider time)
{
    public TournamentPool Pool() => library.Exclusive(() =>
    {
        var view = library.View();
        var set = view.Sets.SingleOrDefault(s => s.Definition.Id == view.ActiveSetId)?.Definition;
        var errors = view.Errors.ToList();
        var eligible = new List<TournamentCategory>();
        foreach (var id in set?.CategoryIds.Distinct() ?? [])
        {
            var category = view.Categories.SingleOrDefault(c => c.Definition.Id == id)?.Definition;
            if (category is null || !category.Questions.Any(q => q.Enabled)) continue;
            try
            {
                foreach (var question in category.Questions.Where(q => q.Enabled && q.Media is not null))
                    Media.VerifyFile(library.CategoryFolder(id), question.Media!, question.Type);
                eligible.Add(new TournamentCategory(id, category.Name));
            }
            catch (Exception error) when (error is ContentException or IOException or UnauthorizedAccessException)
            { errors.Add($"{category.Name}: {error.Message}"); }
        }
        return new TournamentPool(set?.Id, set?.Name, eligible.ToArray(), errors.ToArray());
    });

    public TournamentSummary[] List() => duels.Exclusive(() =>
    {
        using var db = database.CreateDbContext();
        return db.Tournaments.AsNoTracking().AsEnumerable().Select(TournamentPersistence.Read)
            .OrderByDescending(t => t.Created).Select(t => new TournamentSummary(t.Id, t.Created, t.Phase,
                t.Players.Select(p => p.Name).ToArray(), t.Players.FirstOrDefault(p => p.Id == t.ChampionId)?.Name)).ToArray();
    });

    public TournamentView Read(Guid id) => duels.Exclusive(() =>
    {
        // Reading the duel also settles any elapsed deadline and atomically records its result.
        duels.Read();
        using var db = database.CreateDbContext();
        return View(Find(db, id));
    });

    public TournamentView Create(TournamentSetup setup) => duels.Exclusive(() => library.Exclusive(() =>
    {
        if (setup.CommandId == Guid.Empty) throw new ContentException("A command ID is required.");
        using var db = database.CreateDbContext();
        var existing = db.Tournaments.AsEnumerable().FirstOrDefault(r => TournamentPersistence.Read(r).Commands.Contains(setup.CommandId));
        if (existing is not null) return View(existing);
        if (db.Tournaments.AsEnumerable().Any(r => TournamentPersistence.Read(r).Phase != TournamentPhase.Completed))
            throw new ContentConflict("Resume, restart or delete the existing unfinished tournament first.");
        if (duels.Read() is { Status: not DuelStatus.Finished })
            throw new ContentConflict("Finish the current standalone duel before creating a tournament.");
        var pool = Pool();
        if (pool.SetId is not { } setId || pool.Errors.Length != 0)
            throw new ContentException("Choose an active set and resolve its library errors before starting a tournament.");
        var id = Guid.NewGuid();
        var state = TournamentRules.Create(id, setId, pool.SetName!, setup, pool.Categories, time.GetUtcNow(), Random.Shared);
        // Freeze playable content and media once. Later YAML edits or category removal cannot strand this tournament.
        var categories = library.View().Categories.Where(c => pool.Categories.Any(p => p.Id == c.Definition.Id))
            .Select(c => duels.SnapshotCategory(id, c.Definition)).ToArray();
        var record = new TournamentRecord { Id = id, Payload = JsonSerializer.Serialize(state) };
        db.Tournaments.Add(record);
        TournamentPersistence.Present(db, id);
        db.TournamentDecks.Add(new TournamentDeck { Id = id, Payload = JsonSerializer.Serialize(categories) });
        db.SaveChanges();
        return View(record);
    }));

    public TournamentView? Command(TournamentCommand command) => duels.Exclusive(() =>
    {
        if (command.CommandId == Guid.Empty) throw new ContentException("A command ID is required.");
        duels.Read();
        using var db = database.CreateDbContext();
        var record = db.Tournaments.Find(command.TournamentId);
        if (record is null && command.Action == "delete" && command.Confirm) return null;
        record ??= Find(db, command.TournamentId);
        var state = TournamentPersistence.Read(record);
        if (state.Commands.Contains(command.CommandId)) return View(record);
        if (state.Version != command.Version) throw new ContentConflict("The tournament changed. Review its current state and try again.");
        TournamentRules.Validate(state);
        if (command.Action == "present")
        {
            if (duels.Read() is { Status: not DuelStatus.Finished } current && current.TournamentId != state.Id)
                throw new ContentConflict("Finish the current duel before presenting another tournament.");
            TournamentPersistence.Present(db, state.Id);
            record.Payload = JsonSerializer.Serialize(state with { Version = state.Version + 1, Commands = [.. state.Commands, command.CommandId] });
            db.SaveChanges();
            return View(record);
        }
        if (command.Action is "undoResult" or "restart" or "delete")
        {
            if (!command.Confirm) throw new ContentConflict("Confirm this destructive tournament action first.");
            if (command.Action != "delete" && db.Tournaments.AsEnumerable().Any(r => r.Id != state.Id &&
                TournamentPersistence.Read(r).Phase != TournamentPhase.Completed))
                throw new ContentConflict("Finish or delete the other unfinished tournament before reopening this one.");
            if (command.Action == "undoResult") return Undo(record, state, command);
            duels.ReplaceTournamentDuel(state.Id, null, (transaction, _) =>
            {
                var target = Find(transaction, state.Id);
                if (command.Action == "delete")
                {
                    transaction.Tournaments.Remove(target);
                    if (transaction.TournamentPresentations.Find(1)?.TournamentId == state.Id)
                        TournamentPersistence.Present(transaction, null);
                    var deck = transaction.TournamentDecks.Find(state.Id);
                    if (deck is not null) transaction.TournamentDecks.Remove(deck);
                }
                else
                {
                    var next = TournamentRules.Create(state.Id, state.SetId, state.SetName,
                        new TournamentSetup(command.CommandId, state.Players.Select(p => p.Name).ToArray(), state.Seconds, state.PassSeconds, state.Order, state.Width, state.Height),
                        state.Categories.Select(c => c with { Status = CategoryStatus.Available, OwnerId = null }).ToArray(), time.GetUtcNow(), Random.Shared)
                        with { Version = state.Version + 1, Commands = [.. state.Commands, command.CommandId] };
                    target.Payload = JsonSerializer.Serialize(next);
                    TournamentPersistence.Present(transaction, state.Id);
                    target.Undo = null;
                }
            });
            return command.Action == "delete" ? null : Read(state.Id);
        }
        if (command.Action == "launch")
        {
            var (categoryId, _) = TournamentRules.NextDuel(state);
            var deck = db.TournamentDecks.Find(state.Id) ?? throw new ContentException("The tournament content snapshot is missing. Restore a backup.");
            var category = JsonSerializer.Deserialize<Category[]>(deck.Payload)!.Single(c => c.Id == categoryId);
            duels.PrepareTournament(state, category, (transaction, duel) =>
            {
                var next = TournamentRules.Launch(state, duel!) with { Version = state.Version + 1, Commands = [.. state.Commands, command.CommandId] };
                TournamentRules.Validate(next);
                Find(transaction, state.Id).Payload = JsonSerializer.Serialize(next);
                TournamentPersistence.Present(transaction, state.Id);
            });
            return Read(state.Id);
        }
        var updated = TournamentRules.Apply(state, command, Random.Shared) with
        { Version = state.Version + 1, Commands = [.. state.Commands, command.CommandId] };
        TournamentRules.Validate(updated);
        record.Payload = JsonSerializer.Serialize(updated);
        TournamentPersistence.Present(db, state.Id);
        db.SaveChanges();
        return View(record);
    });

    public AudienceTournament? Audience() => duels.Exclusive(() =>
    {
        var duel = duels.Read();
        using var db = database.CreateDbContext();
        var presentation = db.TournamentPresentations.Find(1);
        TournamentRecord? record;
        if (presentation is not null)
            record = presentation.TournamentId is { } id ? db.Tournaments.Find(id) : null;
        else if (duel is { TournamentId: null })
            record = null;
        else
            record = db.Tournaments.AsNoTracking().AsEnumerable()
                .Where(r => duel?.TournamentId == r.Id || TournamentPersistence.Read(r).Phase != TournamentPhase.Completed)
                .OrderByDescending(r => TournamentPersistence.Read(r).Created).FirstOrDefault();
        return record is null ? null : TournamentAudience.Project(TournamentPersistence.Read(record));
    });

    private TournamentView Undo(TournamentRecord record, TournamentState state, TournamentCommand command)
    {
        var checkpoint = record.Undo is null ? null : JsonSerializer.Deserialize<ResultCheckpoint>(record.Undo);
        if (checkpoint is null) throw new ContentConflict("There is no recorded duel result to undo.");
        var prior = checkpoint.State;
        // Replay only the corrected duel with its original questions/category/starter; discard all dependent choices.
        var original = checkpoint.Duel;
        var ticks = TimeSpan.FromSeconds(original.Configuration.Seconds).Ticks;
        var duel = original with
        {
            Id = Guid.NewGuid(), Status = DuelStatus.Ready, Version = 1, Index = 0, Active = original.First,
            ATicks = ticks, BTicks = ticks, Winner = null, Started = null, Completed = null,
            Undo = null, PassTicks = 0, Commands = [],
        };
        var restored = prior with
        {
            Version = state.Version + 1, ActiveDuelId = duel.Id,
            Commands = [.. state.Commands, command.CommandId],
        };
        TournamentRules.Validate(restored);
        duels.ReplaceTournamentDuel(state.Id, duel, (transaction, _) =>
        {
            var target = Find(transaction, state.Id);
            target.Payload = JsonSerializer.Serialize(restored);
            TournamentPersistence.Present(transaction, state.Id);
            target.Undo = null;
        });
        return Read(state.Id);
    }

    private static TournamentRecord Find(AppDb db, Guid id) => db.Tournaments.Find(id)
        ?? throw new ContentException("That tournament no longer exists. Return to Mini Floor setup.");

    private static TournamentView View(TournamentRecord record)
    {
        var state = TournamentPersistence.Read(record);
        TournamentRules.Validate(state);
        return new TournamentView(state, state.Phase == TournamentPhase.OpponentSelection && state.ChallengerId is { } id
            ? FloorAdjacency.Opponents(state, id) : [], TournamentRules.Standings(state), record.Undo is not null);
    }
}
