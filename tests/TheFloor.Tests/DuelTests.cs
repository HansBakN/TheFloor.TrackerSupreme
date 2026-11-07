using Microsoft.EntityFrameworkCore;
using TheFloor.Content;
using TheFloor.Duels;
using Xunit;

namespace TheFloor.Tests;

public sealed class DuelTests : IDisposable
{
    private readonly DataPaths paths = new(Path.Combine(Path.GetTempPath(), "thefloor-tests", Guid.NewGuid().ToString("N")));
    private readonly TestClock clock = new();
    private readonly ContentLibrary library;
    private readonly TestFactory factory;
    private readonly DuelService duels;
    private readonly Guid categoryId = Guid.NewGuid();

    public DuelTests()
    {
        var options = new DbContextOptionsBuilder<AppDb>();
        AppDb.Configure(options, paths);
        factory = new TestFactory(options.Options);
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
        library = new ContentLibrary(paths, factory);
        var set = library.SaveSet(new CategorySet { SchemaVersion = 1, Id = Guid.NewGuid(), Name = "Duel set" }, null);
        library.SaveCategory(new Category
        {
            SchemaVersion = 1, Id = categoryId, Name = "Geography",
            Questions = Enumerable.Range(1, 4).Select(number => Question(number)).ToArray(),
        }, null);
        library.SaveSet(set.Definition with { CategoryIds = [categoryId] }, set.Revision);
        library.Activate(set.Definition.Id);
        duels = new DuelService(factory, library, paths, clock);
    }

    [Fact]
    public void SetupDoesNotConsumeTimeAndCorrectSwitchesWithoutResettingClocks()
    {
        var ready = Prepare();
        clock.Advance(10_000);
        Assert.Equal(DuelStatus.Ready, duels.Read()!.Status);
        Assert.Equal(45_000, Milliseconds(duels.Read()!.ATicks));
        var running = Apply(ready, "start");
        clock.Advance(1_200);
        var b = Apply(running, "correct");
        Assert.Equal(Player.B, b.Active);
        Assert.Equal(43_800, Milliseconds(b.ATicks));
        Assert.Equal(45_000, Milliseconds(b.BTicks));
        Assert.Equal("Question 2", b.Current!.Prompt);
        clock.Advance(2_001);
        var a = Apply(b, "correct");
        Assert.Equal(Player.A, a.Active);
        Assert.Equal(43_800, Milliseconds(a.ATicks));
        Assert.Equal(42_999, Milliseconds(a.BTicks));
        Assert.Equal("Question 3", a.Current!.Prompt);
    }

    [Theory]
    [InlineData("correct")]
    [InlineData("pass")]
    public void UndoRestoresTheQuestionAndClocksAndRequiresResume(string action)
    {
        var running = Apply(Prepare(), "start");
        clock.Advance(1_000);
        var advanced = Apply(running, action);
        clock.Advance(1_000);
        var undone = Apply(advanced, "undo");
        Assert.Equal(DuelStatus.Paused, undone.Status);
        Assert.Equal(Player.A, undone.Active);
        Assert.Equal("Question 1", undone.Current!.Prompt);
        Assert.Equal(44_000, Milliseconds(undone.ATicks));
        Assert.Equal(45_000, Milliseconds(undone.BTicks));
        clock.Advance(5_000);
        Assert.Equal(44_000, Milliseconds(duels.Read()!.ATicks));
        Assert.Equal(DuelStatus.Running, Apply(undone, "resume").Status);
    }

    [Fact]
    public void PassPenaltyAppliesOnceForRetriedCommand()
    {
        var running = Apply(Prepare(), "start");
        clock.Advance(800);
        var command = Command(running, "pass");
        var passed = duels.Command(command)!;
        Assert.Equal(Player.A, passed.Active);
        Assert.Equal(44_200, Milliseconds(passed.ATicks));
        Assert.Equal("Question 2", passed.Current!.Prompt);
        Assert.Equal(passed.Version, duels.Command(command)!.Version);
        Assert.Equal(44_200, Milliseconds(duels.Read()!.ATicks));
        clock.Advance(2_999);
        var locked = duels.Read()!;
        Assert.Equal(1, Milliseconds(locked.PassTicks));
        Assert.False(locked.AudienceQuestionVisible);
        Assert.Throws<ContentConflict>(() => Apply(locked, "correct"));
        Assert.Throws<ContentConflict>(() => Apply(locked, "pass"));
        clock.Advance(1);
        var revealed = duels.Tick()!;
        Assert.Equal(0, revealed.PassTicks);
        Assert.True(revealed.AudienceQuestionVisible);
        Assert.Equal(41_200, Milliseconds(revealed.ATicks));
        Assert.Equal(45_000, Milliseconds(revealed.BTicks));
        Assert.Equal(revealed.Version, duels.Command(command)!.Version);
        Assert.Equal("Question 3", Apply(revealed, "correct").Current!.Prompt);
    }

    [Fact]
    public void APassCannotMakeTimeNegative()
    {
        Apply(Apply(Prepare(2), "start"), "pass");
        clock.Advance(2_000);
        var finished = duels.Tick()!;
        Assert.Equal(DuelStatus.Finished, finished.Status);
        Assert.Equal(Player.B, finished.Winner);
        Assert.Equal(0, finished.ATicks);
        Assert.Null(finished.Undo);
    }

    [Fact]
    public void ExactDeadlinePrecedesCommandsAndPersistsTheWinner()
    {
        var running = Apply(Prepare(1), "start");
        clock.Advance(1_000);
        Assert.Throws<ContentConflict>(() => Apply(running, "correct"));
        var finished = duels.Tick()!;
        Assert.Equal(DuelStatus.Finished, finished.Status);
        Assert.Equal(Player.B, finished.Winner);
        Assert.Equal(0, finished.ATicks);
        Assert.Equal(0, finished.Index);
    }

    [Fact]
    public void TickExpiresWithoutConnectedClients()
    {
        Apply(Prepare(1), "start");
        clock.Advance(1_001);
        var finished = duels.Tick()!;
        Assert.Equal(DuelStatus.Finished, finished.Status);
        Assert.Equal(Player.B, finished.Winner);
    }

    [Fact]
    public async Task OnlyOneCommandWithTheSameExpectedVersionCanAdvanceTheQuestion()
    {
        var running = Apply(Prepare(), "start");
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            try { Apply(running, "correct"); return true; }
            catch (ContentConflict) { return false; }
        })));
        Assert.Single(results, succeeded => succeeded);
        Assert.Equal(1, duels.Read()!.Index);
    }

    [Fact]
    public void ExhaustionPausesAndAppendRequiresNewEnabledQuestionsAndExplicitResume()
    {
        var current = Apply(Prepare(), "start");
        var visited = new HashSet<Guid>();
        while (!current.Exhausted)
        {
            Assert.True(visited.Add(current.Current!.Id));
            current = Apply(current, "correct");
        }
        Assert.Equal(4, visited.Count);
        Assert.Equal(DuelStatus.Paused, current.Status);
        Assert.Throws<ContentConflict>(() => Apply(current, "resume"));
        Assert.Throws<ContentException>(() => Apply(current, "append"));

        var category = Assert.Single(library.View().Categories);
        var added = Question(5);
        library.SaveCategory(category.Definition with { Questions = [.. category.Definition.Questions, added] }, category.Revision);
        var appended = Apply(current, "append");
        Assert.Equal(added.Id, appended.Current!.Id);
        Assert.Equal(DuelStatus.Paused, appended.Status);
        Assert.Equal(DuelStatus.Running, Apply(appended, "resume").Status);
    }

    [Fact]
    public void RestartRestoresSnapshotAndMediaPausedWithoutConsumingOfflineTime()
    {
        var category = Assert.Single(library.View().Categories);
        var uploaded = library.Upload(categoryId, "original.gif", "image/gif", "GIF89atest"u8.ToArray());
        var question = category.Definition.Questions[0] with { Type = "image", Media = uploaded };
        library.SaveCategory(category.Definition with { Questions = [question] }, category.Revision);

        var running = Apply(Prepare(), "start");
        clock.Advance(2_000);
        duels.Tick();
        var snapshot = duels.CurrentMedia(running.Id, question.Id);
        var source = library.MediaFile(categoryId, uploaded);
        File.WriteAllBytes(source, "GIF89achanged"u8.ToArray());
        Assert.Equal("GIF89atest"u8.ToArray(), File.ReadAllBytes(snapshot));

        var latest = Assert.Single(library.View().Categories);
        library.SaveCategory(latest.Definition with { Name = "Changed", Questions = [] }, latest.Revision);
        clock.Advance(60_000);
        var restarted = new DuelService(factory, library, paths, clock);
        var recovered = restarted.Read()!;
        Assert.Equal(DuelStatus.Paused, recovered.Status);
        Assert.Equal("Geography", recovered.CategoryName);
        Assert.Equal(question.Id, recovered.Current!.Id);
        Assert.Equal(43_000, Milliseconds(recovered.ATicks));
        Assert.Equal(snapshot, restarted.CurrentMedia(recovered.Id, question.Id));
    }

    [Fact]
    public void InvalidTransitionsAreRejectedAndCompletedDuelsAreRetained()
    {
        var ready = Prepare();
        Assert.Throws<ContentConflict>(() => Apply(ready, "pass"));
        var finished = Apply(ready, "end");
        var restarted = Apply(finished, "restart");
        Assert.NotEqual(finished.Id, restarted.Id);
        Assert.Equal(DuelStatus.Ready, restarted.Status);
        using var db = factory.CreateDbContext();
        Assert.Equal(2, db.Duels.Count());
        Assert.False(db.Duels.Single(row => row.Id == finished.Id).Current);
    }

    [Fact]
    public void DisabledQuestionsCannotPrepareAnEmptyDuel()
    {
        var category = Assert.Single(library.View().Categories);
        library.SaveCategory(category.Definition with
        {
            Questions = category.Definition.Questions.Select(question => question with { Enabled = false }).ToArray(),
        }, category.Revision);
        Assert.Throws<ContentException>(() => Prepare());
        Assert.Null(duels.Read());
    }

    [Fact]
    public void GracefulStopPersistsTimeBetweenPeriodicCheckpoints()
    {
        Apply(Prepare(), "start");
        clock.Advance(137);
        duels.SaveCheckpoint();
        clock.Advance(20_000);
        var restored = new DuelService(factory, library, paths, clock).Read()!;
        Assert.Equal(DuelStatus.Paused, restored.Status);
        Assert.Equal(44_863, Milliseconds(restored.ATicks));
        Assert.Equal(45_000, Milliseconds(restored.BTicks));
    }

    [Fact]
    public void LegacyDatabaseUpgradesToSmallCheckpointsWithoutLosingTheDeck()
    {
        var ready = Prepare();
        using (var db = factory.CreateDbContext())
        {
            db.Duels.Single().Payload = System.Text.Json.JsonSerializer.Serialize(ready);
            db.SaveChanges();
            db.Database.ExecuteSqlRaw("DROP TABLE DuelSnapshots");
            db.Initialize();
        }

        var service = new DuelService(factory, library, paths, clock);
        var recovered = service.Read()!;
        Assert.Equal(DuelStatus.Paused, recovered.Status);
        Assert.Equal(ready.Questions.Select(question => question.Id), recovered.Questions.Select(question => question.Id));
        string questions;
        using (var db = factory.CreateDbContext())
        {
            questions = db.DuelSnapshots.Single().Questions;
            Assert.DoesNotContain(ready.Current!.Prompt!, db.Duels.Single().Payload);
        }

        service.Command(new DuelCommand(recovered.Id, Guid.NewGuid(), recovered.Version, "resume", null));
        clock.Advance(300);
        service.Tick();
        using var persisted = factory.CreateDbContext();
        Assert.Equal(questions, persisted.DuelSnapshots.Single().Questions);
        Assert.DoesNotContain(ready.Current!.Prompt!, persisted.Duels.Single().Payload);
    }

    [Fact]
    public void AudienceSeesNoPreparedQuestionIncludingAfterRecoveryWhileHostCanPreviewMedia()
    {
        var category = Assert.Single(library.View().Categories);
        var media = library.Upload(categoryId, "preview.gif", "image/gif", "GIF89atest"u8.ToArray());
        var question = category.Definition.Questions[0] with { Type = "image", Media = media };
        library.SaveCategory(category.Definition with { Questions = [question] }, category.Revision);
        var ready = Prepare();
        var live = new LiveDuel(duels, new AudienceConnections(), null!, clock);
        Assert.Null(live.Audience()!.Question);
        Assert.Equal(question.Id, live.Host().Session!.Duel.Question!.Id);
        Assert.Throws<ContentException>(() => duels.CurrentMedia(ready.Id, question.Id));
        Assert.True(File.Exists(duels.CurrentMedia(ready.Id, question.Id, host: true)));
        clock.Advance(10_000);
        var recoveredService = new DuelService(factory, library, paths, clock);
        var recoveredLive = new LiveDuel(recoveredService, new AudienceConnections(), null!, clock);
        var recovered = recoveredService.Read()!;
        Assert.Equal(DuelStatus.Paused, recovered.Status);
        Assert.Null(recoveredLive.Audience()!.Question);
        Assert.Throws<ContentException>(() => recoveredService.CurrentMedia(ready.Id, question.Id));
        recoveredService.Command(Command(recovered, "resume"));
        Assert.Equal(question.Id, recoveredLive.Audience()!.Question!.Id);
        Assert.True(File.Exists(recoveredService.CurrentMedia(ready.Id, question.Id)));
    }

    [Fact]
    public void LockoutSurvivesPauseRecoveryAndResumeAndUndoRestoresThePassedQuestion()
    {
        var passed = Apply(Apply(Prepare(), "start"), "pass");
        clock.Advance(750);
        var paused = Apply(passed, "pause");
        Assert.Equal(2_250, Milliseconds(paused.PassTicks));
        clock.Advance(20_000);
        var restored = new DuelService(factory, library, paths, clock);
        var recovered = restored.Read()!;
        Assert.Equal(2_250, Milliseconds(recovered.PassTicks));
        Assert.Equal(44_250, Milliseconds(recovered.ATicks));
        Assert.False(recovered.AudienceQuestionVisible);
        var resumed = restored.Command(Command(recovered, "resume"))!;
        clock.Advance(2_249);
        Assert.False(restored.Read()!.AudienceQuestionVisible);
        clock.Advance(1);
        var revealed = restored.Tick()!;
        Assert.True(revealed.AudienceQuestionVisible);
        Assert.Equal(42_000, Milliseconds(revealed.ATicks));
        var undone = restored.Command(Command(revealed, "undo"))!;
        Assert.Equal(0, undone.Index);
        Assert.Equal(0, undone.PassTicks);
        Assert.Equal(DuelStatus.Paused, undone.Status);
        Assert.Equal(45_000, Milliseconds(undone.ATicks));
        Assert.True(undone.AudienceQuestionVisible);
        Assert.Equal(resumed.Id, undone.Id);
    }

    [Fact]
    public void RunningLockoutRecoversPausedWithoutOfflineTimeAndRestartClearsIt()
    {
        Apply(Apply(Prepare(), "start"), "pass");
        clock.Advance(500);
        duels.SaveCheckpoint();
        clock.Advance(30_000);
        var restored = new DuelService(factory, library, paths, clock);
        var recovered = restored.Read()!;
        Assert.Equal(DuelStatus.Paused, recovered.Status);
        Assert.Equal(2_500, Milliseconds(recovered.PassTicks));
        Assert.Equal(44_500, Milliseconds(recovered.ATicks));
        Assert.False(recovered.AudienceQuestionVisible);
        var ready = restored.Command(Command(recovered, "restart"))!;
        Assert.Equal(DuelStatus.Ready, ready.Status);
        Assert.Equal(0, ready.PassTicks);
        Assert.False(ready.AudienceQuestionVisible);
    }

    [Fact]
    public void ZeroPenaltyAdvancesImmediatelyAndLastPassPausesOnlyAfterLockout()
    {
        var ready = duels.Prepare(new DuelConfiguration("A", "B", categoryId, 45, 0, StartingPlayer.A, QuestionOrder.Ordered));
        var passed = Apply(Apply(ready, "start"), "pass");
        Assert.Equal(0, passed.PassTicks);
        Assert.True(passed.AudienceQuestionVisible);
        Assert.Equal(45_000, Milliseconds(passed.ATicks));
        Apply(passed, "return");
        var current = Apply(Prepare(), "start");
        for (var i = 0; i < 3; i++) current = Apply(current, "correct");
        current = Apply(current, "pass");
        Assert.True(current.Exhausted);
        Assert.Equal(DuelStatus.Running, current.Status);
        clock.Advance(1_000);
        var paused = Apply(current, "pause");
        var resumed = Apply(paused, "resume");
        clock.Advance(5_000);
        var exhausted = duels.Tick()!;
        Assert.Equal(DuelStatus.Paused, exhausted.Status);
        Assert.Equal(0, exhausted.PassTicks);
        Assert.Equal(42_000, Milliseconds(exhausted.BTicks));
        Assert.Equal(resumed.Id, exhausted.Id);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("running")]
    [InlineData("paused")]
    [InlineData("pass")]
    public void ReturnEndsAnUnfinishedDuelWithoutWinnerAndIsDurableAndIdempotent(string phase)
    {
        var current = Prepare();
        if (phase != "ready") current = Apply(current, "start");
        clock.Advance(500);
        if (phase == "paused") current = Apply(current, "pause");
        if (phase == "pass") current = Apply(current, "pass");
        var command = Command(current, "return");
        Assert.Throws<ContentConflict>(() => duels.Command(command with { Version = current.Version - 1 }));
        Assert.NotNull(duels.Read());
        Assert.Null(duels.Command(command));
        Assert.Null(duels.Command(command));
        clock.Advance(60_000);
        Assert.Null(new DuelService(factory, library, paths, clock).Read());
        using var db = factory.CreateDbContext();
        var record = db.Duels.Single();
        Assert.False(record.Current);
        var archived = System.Text.Json.JsonSerializer.Deserialize<DuelSession>(record.Payload)!;
        Assert.Equal(DuelStatus.Finished, archived.Status);
        Assert.Null(archived.Winner);
        Assert.NotNull(archived.Completed);
        Assert.Equal(0, archived.PassTicks);
        Assert.Equal(phase == "ready" ? 45_000 : 44_500, Milliseconds(archived.ATicks));
        var next = Prepare();
        duels.Command(command);
        Assert.Equal(next.Id, duels.Read()!.Id);
    }

    [Fact]
    public void CategoryResetPreservesLiveAndRecoverableDuelSnapshotsAndMedia()
    {
        var category = Assert.Single(library.View().Categories);
        var uploaded = library.Upload(categoryId, "original.gif", "image/gif", "GIF89atest"u8.ToArray());
        var question = category.Definition.Questions[0] with { Type = "image", Media = uploaded };
        library.SaveCategory(category.Definition with { Questions = [question, .. category.Definition.Questions.Skip(1)] }, category.Revision);
        var running = Apply(Prepare(), "start");
        var before = library.View();
        library.ResetCategories(before.Categories.ToDictionary(c => c.Definition.Id, c => c.Revision),
            before.Sets.ToDictionary(s => s.Definition.Id, s => s.Revision));
        Assert.Equal(running.Version, duels.Read()!.Version);
        Assert.Equal(question.Id, duels.Read()!.Current!.Id);
        Assert.Equal("GIF89atest"u8.ToArray(), File.ReadAllBytes(duels.CurrentMedia(running.Id, question.Id)));
        var recovered = new DuelService(factory, library, paths, clock);
        Assert.Equal(DuelStatus.Paused, recovered.Read()!.Status);
        Assert.Equal(question.Id, recovered.Read()!.Current!.Id);
        Assert.Equal("GIF89atest"u8.ToArray(), File.ReadAllBytes(recovered.CurrentMedia(running.Id, question.Id)));
        Assert.Equal("Question 2", Apply(running, "correct").Current!.Prompt);
    }

    private DuelSession Prepare(int seconds = 45) => duels.Prepare(new DuelConfiguration("Martin", "Thomas",
        categoryId, seconds, 3, StartingPlayer.A, QuestionOrder.Ordered));

    private DuelSession Apply(DuelSession state, string action) => duels.Command(Command(state, action))!;

    private static DuelCommand Command(DuelSession state, string action) =>
        new(state.Id, Guid.NewGuid(), state.Version, action, null);

    private static double Milliseconds(long ticks) => TimeSpan.FromTicks(ticks).TotalMilliseconds;

    private static Question Question(int number) =>
        new() { Id = Guid.NewGuid(), Prompt = $"Question {number}", Answer = $"Answer {number}" };

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(paths.Root, recursive: true);
    }

    private sealed class TestFactory(DbContextOptions<AppDb> options) : IDbContextFactory<AppDb>
    {
        public AppDb CreateDbContext() => new(options);
    }

    private sealed class TestClock : TimeProvider
    {
        private long stamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => stamp;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(stamp);
        public void Advance(int milliseconds) => stamp += TimeSpan.FromMilliseconds(milliseconds).Ticks;
    }
}
