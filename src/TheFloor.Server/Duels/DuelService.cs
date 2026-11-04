using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheFloor.Content;

namespace TheFloor.Duels;

public sealed class DuelService(IDbContextFactory<AppDb> database, ContentLibrary library, DataPaths paths, TimeProvider time)
{
    private readonly object gate = new();
    private DuelSession? state;
    private long anchor;
    private bool loaded;

    public DuelSession Prepare(DuelConfiguration configuration)
    {
        lock (gate)
        {
            Load();
            Settle(time.GetTimestamp(), forceCheckpoint: false);
            if (state is { Status: not DuelStatus.Finished })
                throw new ContentConflict("Finish the current duel before preparing another.");
            DuelRules.Validate(configuration);
            var next = library.Exclusive(() =>
            {
                var view = library.View();
                var set = view.Sets.SingleOrDefault(item => item.Definition.Id == view.ActiveSetId)?.Definition;
                var category = view.Categories.SingleOrDefault(item => item.Definition.Id == configuration.CategoryId)?.Definition;
                if (category is null || set is null || !set.CategoryIds.Contains(category.Id))
                    throw new ContentException("Choose a category in the active set.");
                var questions = category.Questions.Where(q => q.Enabled).ToArray();
                if (questions.Length == 0) throw new ContentException("This category has no enabled questions.");
                if (configuration.Order == QuestionOrder.Shuffled) Random.Shared.Shuffle(questions);
                var first = configuration.Starter switch
                {
                    StartingPlayer.A => Player.A, StartingPlayer.B => Player.B,
                    _ => (Player)Random.Shared.Next(2),
                };
                var id = Guid.NewGuid();
                var ticks = TimeSpan.FromSeconds(configuration.Seconds).Ticks;
                return new DuelSession(id, id, set.Id, category.Name, configuration,
                    Snapshot(id, category.Id, questions), first, first, ticks, ticks);
            });
            Persist(next, time.GetTimestamp());
            return next;
        }
    }

    public DuelSession? Command(DuelCommand command)
    {
        lock (gate)
        {
            Load();
            var stamp = time.GetTimestamp();
            var current = Settle(stamp, forceCheckpoint: false);
            if (command.CommandId == Guid.Empty) throw new ContentException("A command ID is required.");
            if (current?.Commands.Contains(command.CommandId) == true) return current;
            if (command.Action == "return")
            {
                if (current?.Id != command.SessionId)
                {
                    using var db = database.CreateDbContext();
                    var archived = db.Duels.Find(command.SessionId);
                    if (archived is not null && JsonSerializer.Deserialize<DuelSession>(archived.Payload)
                        ?.Commands.Contains(command.CommandId) == true) return current;
                }
                if (current?.Id != command.SessionId || command.Version != current.Version || current.Status != DuelStatus.Finished)
                    throw new ContentConflict("Finish the duel before returning to setup.");
                Persist(null, stamp, current with { Commands = [.. current.Commands, command.CommandId] });
                return null;
            }
            if (current is null) throw new ContentConflict("Prepare a duel first.");
            var next = DuelRules.Apply(current, command, time.GetUtcNow(), () => Append(current));
            if (command.Action == "restart" && current.Status == DuelStatus.Finished)
                next = next with { Id = Guid.NewGuid(), Commands = [command.CommandId] };
            Persist(next, stamp);
            return next;
        }
    }

    public DuelSession? Read()
    {
        lock (gate)
        {
            Load();
            return Settle(time.GetTimestamp(), forceCheckpoint: false);
        }
    }

    public DuelSession? Tick()
    {
        lock (gate)
        {
            Load();
            var stamp = time.GetTimestamp();
            return Settle(stamp, forceCheckpoint: time.GetElapsedTime(anchor, stamp) >= TimeSpan.FromMilliseconds(250));
        }
    }

    public void SaveCheckpoint()
    {
        lock (gate)
        {
            Load();
            Settle(time.GetTimestamp(), forceCheckpoint: true);
        }
    }

    public string CurrentMedia(Guid sessionId, Guid questionId)
    {
        lock (gate)
        {
            var current = Read();
            if (current?.Id != sessionId || current.Current is not { Media: { } media } question || question.Id != questionId)
                throw new ContentException("That question media is no longer available.");
            return Media.VerifyFile(SnapshotFolder(current.MediaRootId), media, question.Type);
        }
    }

    private DuelSession? Settle(long stamp, bool forceCheckpoint)
    {
        if (state is not { Status: DuelStatus.Running }) return state;
        var elapsed = time.GetElapsedTime(anchor, stamp);
        var updated = DuelRules.Settle(state, elapsed, time.GetUtcNow());
        if (updated.Status == DuelStatus.Finished || forceCheckpoint)
            Persist(updated, stamp);
        return updated;
    }

    private void Load()
    {
        if (loaded) return;
        using var db = database.CreateDbContext();
        var record = db.Duels.SingleOrDefault(row => row.Current);
        if (record is not null)
        {
            var recovered = JsonSerializer.Deserialize<DuelSession>(record.Payload)
                ?? throw new InvalidOperationException("The current duel record could not be read.");
            var legacy = recovered.Questions.Length != 0;
            if (!legacy)
            {
                var snapshot = db.DuelSnapshots.Find(record.Id)
                    ?? throw new InvalidOperationException("The current duel question snapshot is missing.");
                recovered = recovered with
                {
                    Questions = JsonSerializer.Deserialize<Question[]>(snapshot.Questions)
                        ?? throw new InvalidOperationException("The current duel question snapshot could not be read."),
                };
            }
            if (recovered.Status is DuelStatus.Ready or DuelStatus.Running)
                Persist(recovered with { Status = DuelStatus.Paused, Version = recovered.Version + 1 }, time.GetTimestamp());
            else if (legacy)
                Persist(recovered, time.GetTimestamp());
            else
                state = recovered;
        }
        loaded = true;
    }

    private void Persist(DuelSession? next, long stamp, DuelSession? archived = null)
    {
        using var db = database.CreateDbContext();
        var previous = db.Duels.SingleOrDefault(row => row.Current);
        if (previous is not null && previous.Id != next?.Id)
        {
            previous.Current = false;
            if (archived is not null) previous.Payload = Checkpoint(archived);
        }
        if (next is not null)
        {
            if (state?.Id != next.Id || !ReferenceEquals(state.Questions, next.Questions))
                SaveSnapshot(db, next);
            var payload = Checkpoint(next);
            if (previous is not null && previous.Id == next.Id) previous.Payload = payload;
            else db.Duels.Add(new DuelRecord { Id = next.Id, Current = true, Payload = payload });
        }
        db.SaveChanges();
        state = next;
        anchor = stamp;
    }

    private static string Checkpoint(DuelSession session) =>
        JsonSerializer.Serialize(session with { Questions = [] });

    private static void SaveSnapshot(AppDb db, DuelSession session)
    {
        var existing = db.DuelSnapshots.Find(session.Id);
        var questions = JsonSerializer.Serialize(session.Questions);
        if (existing is null) db.DuelSnapshots.Add(new DuelSnapshot { Id = session.Id, Questions = questions });
        else existing.Questions = questions;
    }

    private Question[] Append(DuelSession current) => library.Exclusive(() =>
    {
        var category = library.View().Categories
            .SingleOrDefault(item => item.Definition.Id == current.Configuration.CategoryId)?.Definition
            ?? throw new ContentException("The original category is unavailable.");
        var used = current.Questions.Select(q => q.Id).ToHashSet();
        var additional = category.Questions.Where(q => q.Enabled && !used.Contains(q.Id)).ToArray();
        if (current.Configuration.Order == QuestionOrder.Shuffled) Random.Shared.Shuffle(additional);
        return Snapshot(current.MediaRootId, category.Id, additional);
    });

    private Question[] Snapshot(Guid snapshotId, Guid categoryId, Question[] questions)
    {
        var sourceFolder = library.CategoryFolder(categoryId);
        var snapshotFolder = SnapshotFolder(snapshotId);
        var copies = new Dictionary<string, string>(StringComparer.Ordinal);
        var files = new List<string>();
        try
        {
            return questions.Select(question =>
            {
                if (question.Media is not { } media) return question;
                if (!copies.TryGetValue(media, out var relative))
                {
                    var source = Media.VerifyFile(sourceFolder, media, question.Type);
                    relative = $"{Guid.NewGuid():N}{Path.GetExtension(media).ToLowerInvariant()}";
                    var destination = DataPaths.SafePath(snapshotFolder, relative);
                    Directory.CreateDirectory(snapshotFolder);
                    files.Add(destination);
                    File.Copy(source, destination);
                    Media.VerifyFile(snapshotFolder, relative, question.Type);
                    copies.Add(media, relative);
                }
                return question with { Media = relative };
            }).ToArray();
        }
        catch
        {
            foreach (var file in files) File.Delete(file);
            throw;
        }
    }

    private string SnapshotFolder(Guid id) => DataPaths.SafePath(paths.State, $"snapshots/{id:D}");
}
