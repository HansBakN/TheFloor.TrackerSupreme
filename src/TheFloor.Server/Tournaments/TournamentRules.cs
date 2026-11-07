using TheFloor.Content;
using TheFloor.Duels;

namespace TheFloor.Tournaments;

public static class TournamentRules
{
    public static int MinimumCategories(int players) => checked(3 * players + 1);

    public static TournamentState Create(Guid id, Guid setId, string setName, TournamentSetup setup,
        TournamentCategory[] categories, DateTimeOffset now, Random random)
    {
        var names = setup.Names?.Select(n => n?.Trim() ?? "").ToArray() ?? [];
        if (names.Length is < 2 or > StartingFloor.MaxPlayers || names.Any(n => n.Length is < 1 or > 80) ||
            names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            throw new ContentException($"Enter 2–{StartingFloor.MaxPlayers} distinct participant names, each 1–80 characters.");
        StartingFloor.ValidateDimensions(setup.Width, setup.Height, names.Length);
        DuelRules.Validate(new DuelConfiguration(names[0], names[1], Guid.NewGuid(), setup.Seconds, setup.PassSeconds,
            StartingPlayer.A, setup.Order));
        if (categories.Length < MinimumCategories(names.Length) || categories.Select(c => c.Id).Distinct().Count() != categories.Length ||
            categories.Any(c => c.Id == Guid.Empty || c.Status != CategoryStatus.Available || c.OwnerId is not null))
            throw new ContentException($"Mini Floor with {names.Length} players requires at least {MinimumCategories(names.Length)} distinct eligible categories to guarantee every five-category draw.");
        var players = names.Select((name, i) => new TournamentPlayer(Guid.NewGuid(), name, i)).ToArray();
        var allocation = StartingFloor.Allocate(setup.Width, setup.Height, players.Select(p => p.Id).ToArray(), random);
        var order = players.Select(p => p.Id).ToArray();
        random.Shuffle(order);
        var state = new TournamentState(id, setId, setName, players,
            allocation.Floor, order, categories)
        {
            Width = setup.Width, Height = setup.Height, StartingTerritories = allocation.StartingTerritories, ExtraAssignments = allocation.ExtraAssignments,
            Created = now, Seconds = setup.Seconds, PassSeconds = setup.PassSeconds, Order = setup.Order,
            SelectingPlayerId = order[0], Commands = [setup.CommandId],
        };
        return state with { Offer = Draw(state, random) };
    }

    public static Guid[] Draw(TournamentState state, Random random)
    {
        var available = state.Categories.Where(c => c.Status == CategoryStatus.Available).Select(c => c.Id).ToArray();
        if (available.Distinct().Count() < 5)
            throw new ContentException("Five distinct available categories are required. Restore the category pool or correct the last result; the draw cannot be reduced.");
        random.Shuffle(available);
        return available.Take(5).ToArray();
    }

    public static TournamentState Apply(TournamentState state, TournamentCommand command, Random random)
    {
        Validate(state);
        var ids = command.CategoryIds ?? [];
        var matchup = state.CurrentMatchup;
        switch (command.Action)
        {
            case "pick" when state.Phase is TournamentPhase.InitialSelection or TournamentPhase.WinnerCategory:
                if (ids.Length != 1 || !state.Offer.Contains(ids[0]) ||
                    state.Categories.Single(c => c.Id == ids[0]).Status != CategoryStatus.Available)
                    throw new ContentConflict("Choose one of the five available categories offered to this player.");
                var picked = state with
                {
                    Players = state.Players.Select(p => p.Id == state.SelectingPlayerId ? p with { CategoryId = ids[0] } : p).ToArray(),
                    Categories = state.Categories.Select(c => c.Id == ids[0] ? c with { Status = CategoryStatus.Selected, OwnerId = state.SelectingPlayerId } : c).ToArray(),
                    Offer = [], SelectingPlayerId = null,
                    Selections = [.. state.Selections, new CategorySelection(state.SelectingPlayerId!.Value, state.Offer, ids[0])],
                };
                if (state.Phase == TournamentPhase.WinnerCategory) return picked with { Phase = TournamentPhase.WinnerDecision };
                var index = state.SelectionIndex + 1;
                if (index < state.Players.Length)
                    return picked with { SelectionIndex = index, SelectingPlayerId = state.SelectionOrder[index], Offer = Draw(picked, random) };
                return picked with
                {
                    SelectionIndex = index, Phase = TournamentPhase.OpponentSelection,
                    ChallengerId = state.Players[random.Next(state.Players.Length)].Id, ChallengerSource = "random",
                };
            case "challenge" when state.Phase == TournamentPhase.OpponentSelection:
                if (state.ChallengerId is not { } challenger || command.PlayerId is not { } defender ||
                    !FloorAdjacency.Opponents(state, challenger).Contains(defender))
                    throw new ContentConflict("Choose an active neighboring opponent, including across wrapped edges.");
                var a = state.Players.Single(p => p.Id == challenger);
                var b = state.Players.Single(p => p.Id == defender);
                if (a.CategoryId is not { } ac || b.CategoryId is not { } bc)
                    throw new ContentConflict("Both players must have an active category before a matchup.");
                return state with
                {
                    Phase = TournamentPhase.DuelReady,
                    Matchups = [.. state.Matchups, new Matchup(Guid.NewGuid(), challenger, defender, bc, ac)],
                };
            case "ban" when state.Phase is TournamentPhase.DefenderBan or TournamentPhase.ChallengerBans:
                var count = state.Phase == TournamentPhase.DefenderBan ? 1 : 2;
                if (matchup is null || ids.Length != count || ids.Distinct().Count() != count ||
                    ids.Any(id => !matchup.DeciderOffer.Contains(id) || matchup.Bans.Contains(id)))
                    throw new ContentConflict($"Select exactly {count} distinct, unbanned offered {(count == 1 ? "category" : "categories")}.");
                return ReplaceMatchup(state, matchup with { Bans = [.. matchup.Bans, .. ids] }) with
                { Phase = count == 1 ? TournamentPhase.ChallengerBans : TournamentPhase.DefenderPick };
            case "deciderPick" when state.Phase == TournamentPhase.DefenderPick:
                if (matchup is null || matchup.Bans.Length != 3 || ids.Length != 1 ||
                    !matchup.DeciderOffer.Contains(ids[0]) || matchup.Bans.Contains(ids[0]) ||
                    state.Categories.Single(c => c.Id == ids[0]).Status != CategoryStatus.Available)
                    throw new ContentConflict("The defender must choose one of the two remaining available categories.");
                return ReplaceMatchup(state, matchup with { DeciderCategoryId = ids[0] }) with { Phase = TournamentPhase.DuelReady };
            case "next" when state.Phase == TournamentPhase.MatchupResult:
                return state with { Phase = TournamentPhase.WinnerCategory };
            case "continue" or "return" when state.Phase == TournamentPhase.WinnerDecision:
                var winner = matchup!.WinnerId!.Value;
                var continuing = command.Action == "continue";
                var others = state.Players.Where(p => p.EliminationOrder is null && p.Id != winner).ToArray();
                var next = continuing ? winner : others[random.Next(others.Length)].Id;
                return state with
                {
                    Phase = TournamentPhase.OpponentSelection, ChallengerId = next,
                    ChallengerSource = continuing ? "continued" : "random",
                    Decisions = [.. state.Decisions, new ChallengerDecision(winner, continuing, next)],
                };
            default:
                throw new ContentConflict($"Cannot {command.Action} during {state.Phase}. Review the current required action.");
        }
    }

    public static (Guid CategoryId, Guid StarterId) NextDuel(TournamentState state)
    {
        if (state.Phase != TournamentPhase.DuelReady || state.CurrentMatchup is not { WinnerId: null } matchup)
            throw new ContentConflict("A duel cannot start in this tournament phase.");
        var next = matchup.Duels.Length switch
        {
            0 => (matchup.DefenderCategoryId, matchup.DefenderId),
            1 => (matchup.ChallengerCategoryId, matchup.ChallengerId),
            2 when matchup.Duels.Select(d => d.WinnerId).Distinct().Count() == 2 && matchup.DeciderCategoryId is { } category => (category, matchup.ChallengerId),
            _ => throw new ContentConflict("No further duel is required in this matchup."),
        };
        var entry = state.Categories.SingleOrDefault(c => c.Id == next.Item1);
        if (matchup.Duels.Length < 2)
        {
            if (entry is not { Status: CategoryStatus.Selected } || entry.OwnerId != next.Item2 ||
                state.Players.Single(p => p.Id == next.Item2).CategoryId != entry.Id)
                throw new ContentConflict("The category owner must have a valid unplayed active category.");
        }
        else if (entry is not { Status: CategoryStatus.Available } || matchup.Bans.Length != 3 ||
            matchup.Bans.Contains(entry.Id) || !matchup.DeciderOffer.Contains(entry.Id))
            throw new ContentConflict("Complete the deciding category bans and selection before starting the duel.");
        return next;
    }

    public static TournamentState Launch(TournamentState state, DuelSession duel)
    {
        var (category, starter) = NextDuel(state);
        var matchup = state.CurrentMatchup!;
        if (duel.TournamentId != state.Id || duel.MatchupId != matchup.Id || duel.Configuration.CategoryId != category ||
            duel.First != (starter == matchup.ChallengerId ? Player.A : Player.B))
            throw new ContentConflict("The prepared duel does not match the tournament category, roles or required starter.");
        if (state.Categories.Single(c => c.Id == category).Status == CategoryStatus.Played)
            throw new ContentConflict("This category has already been played.");
        return state with { Phase = TournamentPhase.Playing, ActiveDuelId = duel.Id };
    }

    public static TournamentState ConsumeCategory(TournamentState state, Guid category) => state with
    {
        Categories = state.Categories.Select(c => c.Id == category ? c with { Status = CategoryStatus.Played, OwnerId = null } : c).ToArray(),
        Players = state.Players.Select(p => p.CategoryId == category ? p with { CategoryId = null } : p).ToArray(),
    };

    public static TournamentState RecordResult(TournamentState state, DuelSession duel, Random random, DateTimeOffset now)
    {
        if (state.Phase != TournamentPhase.Playing || state.ActiveDuelId != duel.Id ||
            state.CurrentMatchup is not { WinnerId: null } matchup || duel.Status != DuelStatus.Finished || duel.Winner is null)
            throw new ContentConflict("Only a finished current tournament duel with a winner can be recorded.");
        var expectedCategory = matchup.Duels.Length == 0 ? matchup.DefenderCategoryId :
            matchup.Duels.Length == 1 ? matchup.ChallengerCategoryId : matchup.DeciderCategoryId;
        if (duel.TournamentId != state.Id || duel.MatchupId != matchup.Id || duel.Configuration.CategoryId != expectedCategory ||
            duel.First != (matchup.Duels.Length == 0 ? Player.B : Player.A) || matchup.Duels.Length >= 3)
            throw new ContentConflict("The duel result does not match this matchup's fixed category and starter.");
        var winner = duel.Winner == Player.A ? matchup.ChallengerId : matchup.DefenderId;
        var result = new TournamentDuel(duel.Id, duel.Configuration.CategoryId,
            duel.First == Player.A ? matchup.ChallengerId : matchup.DefenderId, winner, duel.Started, duel.Completed ?? now);
        matchup = matchup with { Duels = [.. matchup.Duels, result] };
        var next = ReplaceMatchup(state, matchup) with { ActiveDuelId = null };
        if (matchup.Duels.Count(d => d.WinnerId == winner) < 2)
        {
            if (matchup.Duels.Length == 1) return next with { Phase = TournamentPhase.DuelReady };
            return ReplaceMatchup(next, matchup with { DeciderOffer = Draw(next, random) }) with { Phase = TournamentPhase.DefenderBan };
        }
        var loser = winner == matchup.ChallengerId ? matchup.DefenderId : matchup.ChallengerId;
        var transferred = state.Floor.Where(t => t.OwnerId == loser).ToArray();
        var elimination = state.Players.Count(p => p.EliminationOrder is not null) + 1;
        next = ReplaceMatchup(next, matchup with { WinnerId = winner, LoserId = loser, Transferred = transferred, Completed = now }) with
        {
            Players = next.Players.Select(p => p.Id == loser ? p with { CategoryId = null, EliminationOrder = elimination } : p).ToArray(),
            Floor = next.Floor.Select(t => t.OwnerId == loser ? t with { OwnerId = winner } : t).ToArray(),
        };
        if (elimination == state.Players.Length - 1)
            return next with { Phase = TournamentPhase.Completed, ChampionId = winner, Completed = now, Offer = [], SelectingPlayerId = null };
        return next with { Phase = TournamentPhase.MatchupResult, SelectingPlayerId = winner, Offer = Draw(next, random) };
    }

    private static TournamentState ReplaceMatchup(TournamentState state, Matchup matchup) =>
        state with { Matchups = [.. state.Matchups.SkipLast(1), matchup] };

    public static PlayerStanding[] Standings(TournamentState state) => state.Players.Select(p =>
    {
        var matchups = state.Matchups.Where(m => m.ChallengerId == p.Id || m.DefenderId == p.Id).ToArray();
        var duels = matchups.SelectMany(m => m.Duels).ToArray();
        return new PlayerStanding(p.Id, p.Name, p.Color, p.EliminationOrder is null, p.CategoryId,
            state.Floor.Count(t => t.OwnerId == p.Id), matchups.Count(m => m.Completed is not null),
            matchups.Count(m => m.WinnerId == p.Id), matchups.Count(m => m.LoserId == p.Id),
            duels.Count(d => d.WinnerId == p.Id), duels.Count(d => d.WinnerId != p.Id), p.EliminationOrder);
    }).OrderByDescending(p => p.Active).ThenByDescending(p => p.Territories).ThenByDescending(p => p.EliminationOrder).ToArray();

    public static void Validate(TournamentState state)
    {
        StartingFloor.ValidateDimensions(state.Width, state.Height, state.Players.Length);
        var count = state.Players.Length;
        var cells = state.Width * state.Height;
        if (
            state.Players.Any(p => p.Id == Guid.Empty) || state.Players.Select(p => p.Id).Distinct().Count() != count ||
            state.Floor.Length != cells || state.Floor.Select(t => (t.X, t.Y)).Distinct().Count() != cells ||
            state.Floor.Any(t => t.X < 0 || t.X >= state.Width || t.Y < 0 || t.Y >= state.Height ||
                !state.Players.Any(p => p.Id == t.OwnerId && p.EliminationOrder is null)))
            throw new ContentException("Invalid tournament floor or participant state. Restore a valid backup.");
        if (!Enum.IsDefined(state.Phase) || state.Categories.Length < MinimumCategories(count) ||
            state.Categories.Any(c => c.Id == Guid.Empty || !Enum.IsDefined(c.Status)) ||
            state.Categories.Select(c => c.Id).Distinct().Count() != state.Categories.Length ||
            state.Players.Any(p => (p.EliminationOrder is null) != state.Floor.Any(t => t.OwnerId == p.Id)) ||
            state.Players.Where(p => p.CategoryId is not null).Select(p => p.CategoryId).Distinct().Count() != state.Players.Count(p => p.CategoryId is not null) ||
            state.Categories.Any(c => c.Status == CategoryStatus.Selected
                ? c.OwnerId is null || !state.Players.Any(p => p.Id == c.OwnerId && p.CategoryId == c.Id && p.EliminationOrder is null)
                : c.OwnerId is not null) ||
            state.Players.Any(p => p.CategoryId is { } id && !state.Categories.Any(c => c.Id == id && c.Status == CategoryStatus.Selected && c.OwnerId == p.Id)))
            throw new ContentException("Invalid tournament category or ownership state. Restore a valid backup.");
        var active = state.Players.Where(p => p.EliminationOrder is null).ToArray();
        var matchup = state.CurrentMatchup;
        var picking = state.Phase is TournamentPhase.InitialSelection or TournamentPhase.WinnerCategory or TournamentPhase.MatchupResult;
        if (state.SelectionOrder.Length != count || state.SelectionOrder.Distinct().Count() != count ||
            state.SelectionOrder.Any(id => !state.Players.Any(p => p.Id == id)) || state.SelectionIndex < 0 || state.SelectionIndex > count ||
            (state.Phase == TournamentPhase.InitialSelection && (state.SelectionIndex >= count || state.SelectingPlayerId != state.SelectionOrder[state.SelectionIndex])) ||
            (picking && (state.Offer.Length != 5 || state.Offer.Distinct().Count() != 5 ||
                !active.Any(p => p.Id == state.SelectingPlayerId && p.CategoryId is null) ||
                state.Offer.Any(id => !state.Categories.Any(c => c.Id == id && c.Status == CategoryStatus.Available)))) ||
            (state.Phase != TournamentPhase.InitialSelection && state.SelectionIndex != count) ||
            (state.Phase == TournamentPhase.OpponentSelection && (!active.Any(p => p.Id == state.ChallengerId) || active.Any(p => p.CategoryId is null))) ||
            (state.Phase == TournamentPhase.Playing && (state.ActiveDuelId is null || matchup is null || matchup.Completed is not null)) ||
            (state.Phase != TournamentPhase.Playing && state.ActiveDuelId is not null) ||
            (state.Phase == TournamentPhase.Completed && (active.Length != 1 || active[0].Id != state.ChampionId || state.Completed is null)) ||
            state.Matchups.Select(m => m.Id).Distinct().Count() != state.Matchups.Length ||
            state.Matchups.SelectMany(m => m.Duels).Select(d => d.CategoryId).Distinct().Count() != state.Matchups.Sum(m => m.Duels.Length) ||
            state.Matchups.Any(m => m.ChallengerId == m.DefenderId || m.Duels.Length > 3 ||
                m.Duels.Any(d => d.WinnerId != m.ChallengerId && d.WinnerId != m.DefenderId) ||
                m.Duels.Where((d, i) => d.StartingPlayerId != (i == 0 ? m.DefenderId : m.ChallengerId) ||
                    d.CategoryId != (i == 0 ? m.DefenderCategoryId : i == 1 ? m.ChallengerCategoryId : m.DeciderCategoryId)).Any() ||
                (m.Completed is not null && (m.WinnerId is null || m.LoserId is null || m.Duels.Count(d => d.WinnerId == m.WinnerId) != 2))) ||
            (state.Phase is TournamentPhase.DefenderBan or TournamentPhase.ChallengerBans or TournamentPhase.DefenderPick &&
                (matchup is null || matchup.Duels.Length != 2 || matchup.Duels.Select(d => d.WinnerId).Distinct().Count() != 2 ||
                 matchup.DeciderOffer.Length != 5 || matchup.DeciderOffer.Distinct().Count() != 5 ||
                 matchup.Bans.Distinct().Count() != matchup.Bans.Length || matchup.Bans.Any(id => !matchup.DeciderOffer.Contains(id)) ||
                 matchup.Bans.Length != (state.Phase == TournamentPhase.DefenderBan ? 0 : state.Phase == TournamentPhase.ChallengerBans ? 1 : 3))))
            throw new ContentException("Invalid tournament phase or matchup history. Restore a valid backup or correct the latest result.");
    }
}
