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
        Assert.Equal(41_200, Milliseconds(passed.ATicks));
        Assert.Equal("Question 2", passed.Current!.Prompt);
        Assert.Equal(passed.Version, duels.Command(command)!.Version);
        Assert.Equal(41_200, Milliseconds(duels.Read()!.ATicks));
    }

    [Fact]
    public void APassCannotMakeTimeNegative()
    {
        var finished = Apply(Apply(Prepare(2), "start"), "pass");
        Assert.Equal(DuelStatus.Finished, finished.Status);
        Assert.Equal(Player.B, finished.Winner);
        Assert.Equal(0, finished.ATicks);
        Assert.Equal(DuelStatus.Paused, Apply(finished, "undo").Status);
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
