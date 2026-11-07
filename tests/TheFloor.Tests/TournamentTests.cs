using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheFloor.Content;
using TheFloor.Duels;
using TheFloor.Tournaments;
using Xunit;

namespace TheFloor.Tests;

public sealed class FloorAdjacencyTests
{
    [Theory]
    [InlineData(3, 2, 3)]
    [InlineData(4, 4, 4)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 2, 1)]
    public void EveryCellHasDistinctSymmetricOrthogonalNeighbors(int width, int height, int count)
    {
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var neighbors = FloorAdjacency.Neighbors(x, y, width, height);
            Assert.Equal(count, neighbors.Length);
            Assert.Equal(count, neighbors.Distinct().Count());
            Assert.DoesNotContain((x, y), neighbors);
            Assert.All(neighbors, n =>
            {
                Assert.True(n.X == x || n.Y == y);
                Assert.Contains((x, y), FloorAdjacency.Neighbors(n.X, n.Y, width, height));
            });
        }
    }

    [Theory]
    [InlineData(4, 4, 0, 0, "1,0;3,0;0,1;0,3")]
    [InlineData(4, 4, 3, 3, "2,3;0,3;3,2;3,0")]
    [InlineData(4, 4, 1, 1, "0,1;2,1;1,0;1,2")]
    [InlineData(3, 2, 0, 0, "1,0;2,0;0,1")]
    [InlineData(3, 2, 2, 1, "1,1;0,1;2,0")]
    public void ExactNeighborSetsMatchTheSpecification(int w, int h, int x, int y, string expected) =>
        Assert.Equal(expected.Split(';').Order(), FloorAdjacency.Neighbors(x, y, w, h).Select(n => $"{n.X},{n.Y}").Order());

    [Theory]
    [InlineData(-1, 0, 3, 2)]
    [InlineData(3, 0, 3, 2)]
    [InlineData(0, 2, 3, 2)]
    [InlineData(0, 0, 0, 2)]
    public void InvalidCoordinatesAreRejected(int x, int y, int w, int h) =>
        Assert.Throws<ContentException>(() => FloorAdjacency.Neighbors(x, y, w, h));
}

public sealed class TournamentTests : IDisposable
{
    private readonly DataPaths paths = new(Path.Combine(Path.GetTempPath(), "thefloor-mini", Guid.NewGuid().ToString("N")));
    private readonly Factory factory;
    private readonly TestClock clock = new();
    private readonly ContentLibrary library;
    private readonly DuelService duels;
    private readonly TournamentService tournaments;
    private readonly Guid setId;

    public TournamentTests()
    {
        var options = new DbContextOptionsBuilder<AppDb>();
        AppDb.Configure(options, paths);
        factory = new Factory(options.Options);
        using (var db = factory.CreateDbContext()) db.Initialize();
        library = new ContentLibrary(paths, factory);
        var categories = Enumerable.Range(1, 19).Select(i => new Category
        {
            SchemaVersion = 1, Id = Guid.NewGuid(), Name = $"Tournament category {i}",
            Questions = Enumerable.Range(1, 4).Select(q => new Question { Id = Guid.NewGuid(), Prompt = $"Question {i}.{q}", Answer = "Private answer" }).ToArray(),
        }).ToArray();
        foreach (var category in categories) library.SaveCategory(category, null);
        setId = Guid.NewGuid();
        library.SaveSet(new CategorySet { SchemaVersion = 1, Id = setId, Name = "Mini fixture", CategoryIds = categories.Select(c => c.Id).ToArray() }, null);
        library.Activate(setId);
        duels = new DuelService(factory, library, paths, clock);
        tournaments = new TournamentService(factory, library, duels, clock);
    }

    private static TournamentSetup Setup() => new(Guid.NewGuid(), ["Anders", "Brian", "Christian", "Ditte", "Emil", "Freja"]);
    private TournamentState Create() => tournaments.Create(Setup()).State;
    private TournamentState Act(TournamentState state, string action, Guid? player = null, Guid[]? categories = null, bool confirm = false) =>
        tournaments.Command(new TournamentCommand(state.Id, Guid.NewGuid(), state.Version, action, player, categories, confirm))!.State;
    private TournamentState Initial(TournamentState? initial = null)
    {
        var state = initial ?? Create();
        while (state.Phase == TournamentPhase.InitialSelection) state = Act(state, "pick", categories: [state.Offer[0]]);
        return state;
    }
    private TournamentState Challenge(TournamentState state, bool wrapped = false)
    {
        var eligible = FloorAdjacency.Opponents(state, state.ChallengerId!.Value);
        var cell = state.Floor.Single(t => t.OwnerId == state.ChallengerId);
        var target = wrapped ? state.Floor.Single(t => t.Y == cell.Y && t.X == (cell.X + 2) % 3).OwnerId : eligible[0];
        return Act(state, "challenge", target);
    }
    private TournamentState Finish(TournamentState state, Player winner)
    {
        if (state.Phase == TournamentPhase.DuelReady) state = Act(state, "launch");
        var duel = duels.Read()!;
        duels.Command(new DuelCommand(duel.Id, Guid.NewGuid(), duel.Version, "end", winner));
        return tournaments.Read(state.Id).State;
    }
    private TournamentState Decider(TournamentState state)
    {
        var offer = state.CurrentMatchup!.DeciderOffer;
        state = Act(state, "ban", categories: [offer[0]]);
        state = Act(state, "ban", categories: [offer[1], offer[2]]);
        return Act(state, "deciderPick", categories: [offer[3]]);
    }
    private TournamentState Victory(TournamentState state)
    {
        state = Finish(state, Player.A);
        return Finish(state, Player.A);
    }
    private void AssertDurable(TournamentState state)
    {
        var recoveredDuels = new DuelService(factory, library, paths, clock);
        var recovered = new TournamentService(factory, library, recoveredDuels, clock).Read(state.Id).State;
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(recovered));
    }

    [Fact]
    public void SetupValidatesSixDistinctNamesSettingsAndNineteenEligibleCategories()
    {
        foreach (var names in new[] { new[] { "Only one" }, new[] { "A", "a", "C", "D", "E", "F" }, new[] { "A", "B", "C", "D", "E", " " } })
            Assert.Throws<ContentException>(() => tournaments.Create(Setup() with { Names = names }));
        Assert.Throws<ContentException>(() => tournaments.Create(Setup() with { Seconds = 0 }));
        Assert.Throws<ContentException>(() => tournaments.Create(Setup() with { CommandId = Guid.Empty }));
        var set = library.View().Sets.Single();
        library.SaveSet(set.Definition with { CategoryIds = set.Definition.CategoryIds.Take(18).ToArray() }, set.Revision);
        Assert.Equal(18, tournaments.Pool().Categories.Length);
        Assert.Throws<ContentException>(() => Create());
    }

    [Fact]
    public void SetupFiltersDisabledEmptyAndMissingMediaAndRejectsInvalidLibrary()
    {
        var category = library.View().Categories[0];
        library.SaveCategory(category.Definition with { Questions = category.Definition.Questions.Select(q => q with { Enabled = false }).ToArray() }, category.Revision);
        Assert.Equal(18, tournaments.Pool().Categories.Length);
        Assert.Throws<ContentException>(() => Create());
        category = library.View().Categories[1];
        // External removal after an otherwise valid upload exercises eligible-media filtering.
        var media = library.Upload(category.Definition.Id, "test.gif", "image/gif", "GIF89atest"u8.ToArray());
        library.SaveCategory(category.Definition with { Questions = [category.Definition.Questions[0] with { Type = "image", Media = media }] }, category.Revision);
        File.Delete(library.MediaFile(category.Definition.Id, media));
        Assert.NotEmpty(tournaments.Pool().Errors);
        Assert.Throws<ContentException>(() => Create());
    }

    [Fact]
    public void SetupAndEveryPickHaveStableDrawsAndOnlySelectedCategoriesAreReserved()
    {
        var setup = Setup();
        var state = tournaments.Create(setup).State;
        Assert.Equal(state.Id, tournaments.Create(setup).State.Id);
        Assert.Equal(6, state.SelectionOrder.Distinct().Count());
        Assert.Equal(6, state.Floor.Select(t => t.OwnerId).Distinct().Count());
        Assert.All(state.Players, p => Assert.Equal(3, FloorAdjacency.Opponents(state, p.Id).Length));
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(5, state.Offer.Distinct().Count());
            Assert.Equal(state.SelectionOrder[i], state.SelectingPlayerId);
            Assert.All(state.Offer, id => Assert.Equal(CategoryStatus.Available, state.Categories.Single(c => c.Id == id).Status));
            AssertDurable(state);
            var rejected = state.Offer.Skip(1).ToArray();
            var originalOffer = state.Offer;
            state = Act(state, "pick", categories: [state.Offer[0]]);
            Assert.Equal(i + 1, state.Selections.Length);
            Assert.Equal(originalOffer, state.Selections[^1].Offer);
            Assert.Equal(originalOffer[0], state.Selections[^1].SelectedId);
            Assert.All(rejected, id => Assert.Equal(CategoryStatus.Available, state.Categories.Single(c => c.Id == id).Status));
            Assert.Equal(i + 1, state.Categories.Count(c => c.Status == CategoryStatus.Selected));
        }
        Assert.Equal(TournamentPhase.OpponentSelection, state.Phase);
        AssertDurable(state);
        Assert.All(state.Players, p => Assert.NotNull(p.CategoryId));
    }

    [Fact]
    public void RejectedOffersCanBeDrawnAgainAndInsufficientDrawsAreNeverReduced()
    {
        var state = Create();
        var rejected = state.Offer[1];
        var chosen = state.Offer[0];
        state = Act(state, "pick", categories: [chosen]);
        var available = state.Categories.Where(c => c.Status == CategoryStatus.Available).Select(c => c.Id).Take(4).Append(rejected).Distinct().ToList();
        foreach (var id in state.Categories.Where(c => c.Status == CategoryStatus.Available).Select(c => c.Id))
            if (available.Count < 5 && !available.Contains(id)) available.Add(id);
        var reduced = state with { Categories = state.Categories.Where(c => available.Contains(c.Id) || c.Id == chosen).ToArray() };
        Assert.Contains(rejected, TournamentRules.Draw(reduced, new Random(1)));
        Assert.DoesNotContain(chosen, TournamentRules.Draw(reduced, new Random(1)));
        Assert.Throws<ContentException>(() => TournamentRules.Draw(reduced with { Categories = reduced.Categories.Take(4).ToArray() }, new Random(1)));
    }

    [Fact]
    public void ChallengeValidatesSelfDiagonalsEliminatedAndRequiredPicksAndRolesNeverReverse()
    {
        var state = Create();
        Assert.Throws<ContentConflict>(() => Act(state, "challenge", state.Players[0].Id));
        state = Initial(state);
        var challenger = state.ChallengerId!.Value;
        Assert.Throws<ContentConflict>(() => Act(state, "challenge", challenger));
        var invalid = state.Players.First(p => p.Id != challenger && !FloorAdjacency.Opponents(state, challenger).Contains(p.Id)).Id;
        Assert.Throws<ContentConflict>(() => Act(state, "challenge", invalid));
        state = Challenge(state, wrapped: true);
        var matchup = state.CurrentMatchup!;
        Assert.Equal(challenger, matchup.ChallengerId);
        Assert.Equal((matchup.DefenderCategoryId, matchup.DefenderId), TournamentRules.NextDuel(state));
        state = Act(state, "launch");
        var duel = duels.Read()!;
        Assert.Equal(Player.B, duel.First);
        Assert.Equal(matchup.DefenderCategoryId, duel.Configuration.CategoryId);
        Assert.Equal(matchup.Id, duel.MatchupId);
        Assert.Equal(state.Id, duel.TournamentId);
        Assert.Throws<ContentConflict>(() => duels.Command(new DuelCommand(duel.Id, Guid.NewGuid(), duel.Version, "return", null)));
        Assert.Throws<ContentConflict>(() => duels.Command(new DuelCommand(duel.Id, Guid.NewGuid(), duel.Version, "end", null)));
        state = Finish(state, Player.A);
        Assert.Equal((matchup.ChallengerCategoryId, matchup.ChallengerId), TournamentRules.NextDuel(state));
        state = Act(state, "launch");
        Assert.Equal(Player.A, duels.Read()!.First);
        Assert.Equal(matchup.ChallengerId, state.CurrentMatchup!.ChallengerId);
        Assert.Equal(matchup.DefenderId, state.CurrentMatchup.DefenderId);
    }

    [Fact]
    public void BansEnforceFixedRolesCountsOrderAndUnchosenCategoriesStayAvailable()
    {
        var state = Challenge(Initial());
        state = Finish(state, Player.A);
        state = Finish(state, Player.B);
        Assert.Equal(TournamentPhase.DefenderBan, state.Phase);
        var offer = state.CurrentMatchup!.DeciderOffer;
        Assert.Equal(5, offer.Distinct().Count());
        Assert.Throws<ContentConflict>(() => Act(state, "deciderPick", categories: [offer[0]]));
        Assert.Throws<ContentConflict>(() => Act(state, "launch"));
        Assert.Throws<ContentConflict>(() => Act(state, "ban", categories: [offer[0], offer[1]]));
        AssertDurable(state);
        state = Act(state, "ban", categories: [offer[0]]);
        Assert.Equal(TournamentPhase.ChallengerBans, state.Phase);
        AssertDurable(state);
        Assert.Throws<ContentConflict>(() => Act(state, "ban", categories: [offer[1]]));
        Assert.Throws<ContentConflict>(() => Act(state, "ban", categories: [offer[1], offer[1]]));
        Assert.Throws<ContentConflict>(() => Act(state, "ban", categories: [offer[0], offer[1]]));
        state = Act(state, "ban", categories: [offer[1], offer[2]]);
        AssertDurable(state);
        Assert.Throws<ContentConflict>(() => Act(state, "deciderPick", categories: [offer[0]]));
        state = Act(state, "deciderPick", categories: [offer[3]]);
        AssertDurable(state);
        Assert.All(offer, id => Assert.Equal(CategoryStatus.Available, state.Categories.Single(c => c.Id == id).Status));
        state = Act(state, "launch");
        Assert.Equal(Player.A, duels.Read()!.First);
        Assert.Equal(offer[3], duels.Read()!.Configuration.CategoryId);
        Assert.Equal(CategoryStatus.Available, state.Categories.Single(c => c.Id == offer[3]).Status);
        var prepared = duels.Read()!;
        duels.Command(new DuelCommand(prepared.Id, Guid.NewGuid(), prepared.Version, "start", null));
        state = tournaments.Read(state.Id).State;
        Assert.Equal(CategoryStatus.Played, state.Categories.Single(c => c.Id == offer[3]).Status);
        Assert.All(offer.Where(id => id != offer[3]), id => Assert.Equal(CategoryStatus.Available, state.Categories.Single(c => c.Id == id).Status));
        state = Finish(state, Player.B);
        Assert.Equal(3, state.CurrentMatchup!.Duels.Length);
        Assert.Equal(state.CurrentMatchup.DefenderId, state.CurrentMatchup.WinnerId);
        var reusable = offer.Where(id => id != offer[3]).ToArray();
        var fifth = state.Categories.First(c => c.Status == CategoryStatus.Available && !offer.Contains(c.Id)).Id;
        var drawnAgain = TournamentRules.Draw(state with { Categories = state.Categories.Where(c => reusable.Contains(c.Id) || c.Id == fifth).ToArray() }, new Random(1));
        Assert.All(reusable, id => Assert.Contains(id, drawnAgain));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompleteSixPlayerTournamentWithSweepsOrEveryMatchupDecided(bool deciding)
    {
        var state = Initial();
        var played = new HashSet<Guid>();
        var wrappedChallenge = false;
        for (var m = 0; m < 5; m++)
        {
            var challenger = state.ChallengerId!.Value;
            var opponents = FloorAdjacency.Opponents(state, challenger);
            Assert.Equal(opponents.Distinct().Count(), opponents.Length);
            if (m == 0)
            {
                // Force an actual opposite-edge challenge; x=0 vs x=2.
                var edge = state.Floor.First(t => t.X == 0);
                state = state with { ChallengerId = edge.OwnerId };
                using var db = factory.CreateDbContext();
                db.Tournaments.Find(state.Id)!.Payload = JsonSerializer.Serialize(state);
                db.SaveChanges();
                state = Act(state, "challenge", state.Floor.Single(t => t.Y == edge.Y && t.X == 2).OwnerId);
                wrappedChallenge = true;
            }
            else state = Act(state, "challenge", opponents[0]);
            Assert.Contains(state.CurrentMatchup!.DefenderId, FloorAdjacency.Opponents(state, state.CurrentMatchup.ChallengerId));
            var floorBefore = state.Floor;
            for (var d = 0; d < (deciding ? 3 : 2); d++)
            {
                if (d == 2) state = Decider(state);
                var spec = TournamentRules.NextDuel(state);
                Assert.True(played.Add(spec.CategoryId));
                state = Act(state, "launch");
                var session = duels.Read()!;
                Assert.Equal(d == 0 ? Player.B : Player.A, session.First);
                // Exercise real engine clocks and deadline completion in addition to manual results.
                if (m == 4 && d == (deciding ? 2 : 1))
                {
                    var running = duels.Command(new DuelCommand(session.Id, Guid.NewGuid(), session.Version, "start", null))!;
                    duels.Command(new DuelCommand(running.Id, Guid.NewGuid(), running.Version, "correct", null));
                    clock.Advance(46_000);
                    duels.Tick();
                    state = tournaments.Read(state.Id).State;
                }
                else state = Finish(state, deciding && d == 1 ? Player.B : Player.A);
                AssertDurable(state);
            }
            var result = state.CurrentMatchup!;
            Assert.Equal(floorBefore.Where(t => t.OwnerId == result.LoserId).ToArray(), result.Transferred);
            Assert.Equal(floorBefore.Count(t => t.OwnerId == result.LoserId || t.OwnerId == result.WinnerId), state.Floor.Count(t => t.OwnerId == result.WinnerId));
            Assert.Null(state.Players.Single(p => p.Id == result.LoserId).CategoryId);
            Assert.Equal(m + 1, state.Players.Count(p => p.EliminationOrder is not null));
            Assert.Equal(6, state.Floor.Length);
            Assert.Equal(6, state.Floor.Select(t => (t.X, t.Y)).Distinct().Count());
            Assert.Equal(deciding ? 3 : 2, state.CurrentMatchup!.Duels.Length);
            TournamentRules.Validate(state);
            if (m == 4) break;
            Assert.Equal(TournamentPhase.MatchupResult, state.Phase);
            AssertDurable(state);
            Assert.Throws<ContentConflict>(() => Act(state, "challenge", opponents[0]));
            state = Act(state, "next");
            AssertDurable(state);
            var winner = state.CurrentMatchup!.WinnerId!.Value;
            state = Act(state, "pick", categories: [state.Offer[0]]);
            AssertDurable(state);
            Assert.Equal(TournamentPhase.WinnerDecision, state.Phase);
            state = Act(state, m % 2 == 0 ? "return" : "continue");
            if (m % 2 == 0) Assert.NotEqual(winner, state.ChallengerId);
            else Assert.Equal(winner, state.ChallengerId);
            AssertDurable(state);
        }
        Assert.True(wrappedChallenge);
        Assert.Equal(TournamentPhase.Completed, state.Phase);
        Assert.Equal(5, state.Matchups.Length);
        Assert.Single(state.Players, p => p.EliminationOrder is null);
        Assert.All(state.Floor, t => Assert.Equal(state.ChampionId, t.OwnerId));
        Assert.Equal(deciding ? 15 : 10, played.Count);
        Assert.Equal(played.Count, state.Categories.Count(c => c.Status == CategoryStatus.Played));
        Assert.Equal(Enumerable.Range(1, 5), state.Players.Where(p => p.EliminationOrder is not null).Select(p => p.EliminationOrder!.Value).Order());
        var standings = tournaments.Read(state.Id).Standings;
        Assert.Equal(5, standings.Sum(p => p.MatchupsWon));
        Assert.Equal(5, standings.Sum(p => p.MatchupsLost));
        Assert.Equal(played.Count, standings.Sum(p => p.DuelsWon));
        Assert.Equal(played.Count, standings.Sum(p => p.DuelsLost));
        Assert.Throws<ContentConflict>(() => Act(state, "launch"));
        Assert.Throws<ContentConflict>(() => Act(state, "challenge", state.Players[0].Id));
        var last = duels.Read()!;
        duels.Command(new DuelCommand(last.Id, Guid.NewGuid(), last.Version, "return", null));
        Assert.Null(duels.Read());
        Assert.Equal(state.ChampionId, tournaments.Read(state.Id).State.ChampionId);
        var standalone = duels.Prepare(new DuelConfiguration("Standalone A", "Standalone B", state.Categories[0].Id));
        Assert.Null(standalone.TournamentId);
        duels.Command(new DuelCommand(standalone.Id, Guid.NewGuid(), standalone.Version, "return", null));
        Assert.All(Create().Categories, c => Assert.Equal(CategoryStatus.Available, c.Status));
    }

    [Fact]
    public void MergedOwnershipDeduplicatesOpponentsAndUsesNewTerritoriesIncludingWraps()
    {
        var state = Initial();
        var owner = state.Floor.Single(t => t.X == 0 && t.Y == 0).OwnerId;
        var loser = state.Floor.Single(t => t.X == 1 && t.Y == 0).OwnerId;
        var newNeighbor = state.Floor.Single(t => t.X == 1 && t.Y == 1).OwnerId;
        Assert.DoesNotContain(newNeighbor, FloorAdjacency.Opponents(state, owner));
        state = state with
        {
            Floor = state.Floor.Select(t => t.OwnerId == loser ? t with { OwnerId = owner } : t).ToArray(),
            Players = state.Players.Select(p => p.Id == loser ? p with { EliminationOrder = 1 } : p).ToArray(),
        };
        var targets = FloorAdjacency.Opponents(state, owner);
        Assert.Contains(newNeighbor, targets);
        Assert.DoesNotContain(loser, targets);
        Assert.DoesNotContain(owner, targets);
        Assert.Equal(targets.Length, targets.Distinct().Count());
        Assert.Contains(state.Floor.Single(t => t.X == 2 && t.Y == 0).OwnerId, targets);
    }

    [Fact]
    public async Task RepeatedAndConcurrentSubmissionsCannotAdvanceTwoPicksOrRecordTwice()
    {
        var state = Create();
        var command = new TournamentCommand(state.Id, Guid.NewGuid(), state.Version, "pick", CategoryIds: [state.Offer[0]]);
        var first = tournaments.Command(command)!.State;
        Assert.Equal(first.Version, tournaments.Command(command)!.State.Version);
        Assert.Throws<ContentConflict>(() => tournaments.Command(command with { CommandId = Guid.NewGuid() }));
        state = first;
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            try { Act(state, "pick", categories: [state.Offer[0]]); return true; }
            catch (ContentConflict) { return false; }
        })));
        Assert.Single(results, success => success);
        state = Challenge(Initial(tournaments.Read(state.Id).State));
        state = Act(state, "launch");
        var duel = duels.Read()!;
        var finish = new DuelCommand(duel.Id, Guid.NewGuid(), duel.Version, "end", Player.A);
        duels.Command(finish);
        duels.Command(finish);
        Assert.Single(tournaments.Read(state.Id).State.CurrentMatchup!.Duels);
        var done = duels.Read()!;
        Assert.Throws<ContentConflict>(() => duels.Command(new DuelCommand(done.Id, Guid.NewGuid(), done.Version, "restart", null)));
    }

    [Fact]
    public void RecoveryKeepsInProgressDuelSnapshotAndFreezesOfflineClockEvenAfterLibraryReset()
    {
        var state = Act(Challenge(Initial()), "launch");
        var ready = duels.Read()!;
        duels.Command(new DuelCommand(ready.Id, Guid.NewGuid(), ready.Version, "start", null));
        clock.Advance(1_250);
        duels.SaveCheckpoint();
        var libraryView = library.View();
        library.ResetCategories(libraryView.Categories.ToDictionary(c => c.Definition.Id, c => c.Revision),
            libraryView.Sets.ToDictionary(s => s.Definition.Id, s => s.Revision));
        clock.Advance(90_000);
        var recoveredDuels = new DuelService(factory, library, paths, clock);
        var recovered = recoveredDuels.Read()!;
        Assert.Equal(ready.Id, recovered.Id);
        Assert.Equal(ready.First, recovered.First);
        Assert.Equal(ready.Current!.Id, recovered.Current!.Id);
        Assert.Equal(DuelStatus.Paused, recovered.Status);
        Assert.Equal(43_750, TimeSpan.FromTicks(recovered.BTicks).TotalMilliseconds);
        var recoveredTournaments = new TournamentService(factory, library, recoveredDuels, clock);
        Assert.Empty(recoveredTournaments.Read(state.Id).State.CurrentMatchup!.Duels);
        recoveredDuels.Command(new DuelCommand(recovered.Id, Guid.NewGuid(), recovered.Version, "end", Player.A));
        state = recoveredTournaments.Read(state.Id).State;
        Assert.Equal(TournamentPhase.DuelReady, state.Phase);
        recoveredTournaments.Command(new TournamentCommand(state.Id, Guid.NewGuid(), state.Version, "launch"));
        Assert.Equal(Player.A, recoveredDuels.Read()!.First);
    }

    [Fact]
    public void LatestResultUndoRestoresEliminationFloorCategoriesAndRollsBackDownstreamDuelAndChoices()
    {
        var state = Challenge(Initial());
        state = Finish(state, Player.A);
        state = Act(state, "launch");
        var before = TournamentRules.ConsumeCategory(state, duels.Read()!.Configuration.CategoryId);
        state = Finish(state, Player.A);
        var completedMatchup = state.CurrentMatchup!;
        var afterResult = state;
        state = Act(state, "next");
        state = Act(state, "pick", categories: [state.Offer[0]]);
        state = Act(state, "return");
        state = Act(state, "challenge", tournaments.Read(state.Id).EligibleOpponents[0]);
        state = Act(state, "launch");
        var downstreamId = duels.Read()!.Id;
        Assert.Throws<ContentConflict>(() => Act(state, "undoResult"));
        state = Act(state, "undoResult", confirm: true);
        Assert.Equal(TournamentPhase.Playing, state.Phase);
        Assert.Equal(before.Players, state.Players);
        Assert.Equal(before.Floor, state.Floor);
        Assert.Single(state.Matchups);
        Assert.Single(state.CurrentMatchup!.Duels);
        Assert.Null(state.CurrentMatchup.WinnerId);
        Assert.Equal(before.Decisions, state.Decisions);
        Assert.All(state.Players, p => Assert.Null(p.EliminationOrder));
        Assert.NotEqual(downstreamId, state.ActiveDuelId);
        Assert.Equal(DuelStatus.Ready, duels.Read()!.Status);
        Assert.Equal(Player.A, duels.Read()!.First);
        Assert.Equal(completedMatchup.ChallengerCategoryId, duels.Read()!.Configuration.CategoryId);
        Assert.False(tournaments.Read(state.Id).CanUndo);
        state = Finish(state, Player.B);
        Assert.Equal(TournamentPhase.DefenderBan, state.Phase);
        Assert.All(state.Players, p => Assert.Null(p.EliminationOrder));
        Assert.Equal(afterResult.Categories.Count(c => c.Status == CategoryStatus.Played), state.Categories.Count(c => c.Status == CategoryStatus.Played));
        AssertDurable(state);
    }

    [Fact]
    public void RestartAndDeleteRequireConfirmationAndKeepStandaloneCompatibilityAndPoolReusable()
    {
        var state = Initial();
        Assert.Throws<ContentConflict>(() => Create());
        Assert.Throws<ContentConflict>(() => duels.Prepare(new DuelConfiguration("A", "B", state.Categories[0].Id)));
        Assert.Throws<ContentConflict>(() => Act(state, "restart"));
        var restart = new TournamentCommand(state.Id, Guid.NewGuid(), state.Version, "restart", Confirm: true);
        state = tournaments.Command(restart)!.State;
        Assert.Equal(state.Version, tournaments.Command(restart)!.State.Version);
        Assert.Equal(TournamentPhase.InitialSelection, state.Phase);
        Assert.All(state.Categories, c => Assert.Equal(CategoryStatus.Available, c.Status));
        AssertDurable(state);
        Assert.Throws<ContentConflict>(() => Act(state, "delete"));
        Assert.Null(tournaments.Command(new TournamentCommand(state.Id, Guid.NewGuid(), state.Version, "delete", Confirm: true)));
        Assert.Empty(tournaments.List());
        var standalone = duels.Prepare(new DuelConfiguration("Standalone A", "Standalone B", state.Categories[0].Id));
        Assert.Null(standalone.TournamentId);
        duels.Command(new DuelCommand(standalone.Id, Guid.NewGuid(), standalone.Version, "return", null));
        Assert.Equal(19, Create().Categories.Length);
    }

    [Fact]
    public void InvalidOwnershipAndMalformedCategoriesAreRejected()
    {
        var state = Create();
        Assert.Throws<ContentException>(() => TournamentRules.Validate(state with { Floor = state.Floor.Skip(1).ToArray() }));
        Assert.Throws<ContentException>(() => TournamentRules.Validate(state with { Floor = [state.Floor[0], .. state.Floor.Skip(1).Select(t => t with { X = 0, Y = 0 })] }));
        Assert.Throws<ContentException>(() => TournamentRules.Validate(state with { Floor = state.Floor.Select(t => t with { OwnerId = Guid.Empty }).ToArray() }));
        Assert.Throws<ContentException>(() => TournamentRules.Validate(state with { Categories = [state.Categories[0], .. state.Categories.Skip(1).Select(c => c with { Id = state.Categories[0].Id })] }));
        Assert.Throws<ContentConflict>(() => Act(state, "pick", categories: [Guid.NewGuid()]));
        var selected = state.Offer[0];
        state = Act(state, "pick", categories: [selected]);
        Assert.Throws<ContentConflict>(() => Act(state, "pick", categories: [selected]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SQLiteFailureCannotPartiallyCommitDuelLaunchOrResult(bool finishing)
    {
        var state = Challenge(Initial());
        if (finishing) state = Act(state, "launch");
        var beforeDuel = duels.Read();
        using (var db = factory.CreateDbContext())
            db.Database.ExecuteSqlRaw("CREATE TRIGGER reject_tournament BEFORE UPDATE ON Tournaments BEGIN SELECT RAISE(ABORT, 'simulated storage failure'); END;");
        if (finishing)
        {
            var command = new DuelCommand(beforeDuel!.Id, Guid.NewGuid(), beforeDuel.Version, "end", Player.A);
            Assert.Throws<DbUpdateException>(() => duels.Command(command));
        }
        else Assert.Throws<DbUpdateException>(() => Act(state, "launch"));
        using (var db = factory.CreateDbContext())
        {
            Assert.Equal(JsonSerializer.Serialize(state), db.Tournaments.Find(state.Id)!.Payload);
            Assert.Null(db.Tournaments.Find(state.Id)!.Undo);
            Assert.Equal(finishing ? 1 : 0, db.Duels.Count());
            db.Database.ExecuteSqlRaw("DROP TRIGGER reject_tournament");
        }
        Assert.Equal(beforeDuel?.Id, duels.Read()?.Id);
        Assert.Equal(beforeDuel?.Status, duels.Read()?.Status);
        if (finishing)
        {
            state = Finish(state, Player.A);
            Assert.Single(state.CurrentMatchup!.Duels);
        }
        else Assert.Equal(TournamentPhase.Playing, Act(state, "launch").Phase);
    }

    [Fact]
    public void PlayedCategoriesCannotBeLaunchedAgainAndDuplicateDeleteIsSafe()
    {
        var state = Challenge(Initial());
        var next = TournamentRules.NextDuel(state);
        var invalid = state with { Categories = state.Categories.Select(c => c.Id == next.CategoryId ? c with { Status = CategoryStatus.Played, OwnerId = null } : c).ToArray() };
        // Launch rejects replay independently of frontend and command versions.
        var template = new DuelSession(Guid.NewGuid(), state.Id, state.SetId, "test", new DuelConfiguration("A", "B", next.CategoryId), [], Player.B, Player.B, 1, 1);
        Assert.Throws<ContentConflict>(() => TournamentRules.Launch(invalid, template));
        var command = new TournamentCommand(state.Id, Guid.NewGuid(), state.Version, "delete", Confirm: true);
        Assert.Null(tournaments.Command(command));
        Assert.Null(tournaments.Command(command));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(paths.Root, true);
    }
    private sealed class Factory(DbContextOptions<AppDb> options) : IDbContextFactory<AppDb>
    { public AppDb CreateDbContext() => new(options); }
    private sealed class TestClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(ticks);
        public void Advance(int milliseconds) => ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
    }
}
