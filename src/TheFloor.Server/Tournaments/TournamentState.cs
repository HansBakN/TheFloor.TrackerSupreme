using System.Text.Json.Serialization;
using TheFloor.Duels;

namespace TheFloor.Tournaments;

public enum TournamentPhase
{
    InitialSelection, OpponentSelection, DuelReady, Playing, DefenderBan, ChallengerBans,
    DefenderPick, MatchupResult, WinnerCategory, WinnerDecision, Completed,
}
public enum CategoryStatus { Available, Selected, Played }
public sealed record TournamentPlayer(Guid Id, string Name, int Color, Guid? CategoryId = null, int? EliminationOrder = null);
public sealed record Territory(int X, int Y, Guid OwnerId);
public sealed record TournamentCategory(Guid Id, string Name, CategoryStatus Status = CategoryStatus.Available, Guid? OwnerId = null);
public sealed record TournamentDuel(Guid Id, Guid CategoryId, Guid StartingPlayerId, Guid WinnerId, DateTimeOffset? Started, DateTimeOffset Completed);
public sealed record Matchup(Guid Id, Guid ChallengerId, Guid DefenderId, Guid DefenderCategoryId, Guid ChallengerCategoryId)
{
    public TournamentDuel[] Duels { get; init; } = [];
    public Guid[] DeciderOffer { get; init; } = [];
    public Guid[] Bans { get; init; } = [];
    public Guid? DeciderCategoryId { get; init; }
    public Guid? WinnerId { get; init; }
    public Guid? LoserId { get; init; }
    public Territory[] Transferred { get; init; } = [];
    public DateTimeOffset? Completed { get; init; }
}
public sealed record ChallengerDecision(Guid PlayerId, bool Continue, Guid NextChallengerId);
public sealed record TournamentState(Guid Id, Guid SetId, string SetName, TournamentPlayer[] Players,
    Territory[] Floor, Guid[] SelectionOrder, TournamentCategory[] Categories)
{
    public int Width { get; init; } = 3;
    public int Height { get; init; } = 2;
    public long Version { get; init; } = 1;
    public TournamentPhase Phase { get; init; } = TournamentPhase.InitialSelection;
    public int SelectionIndex { get; init; }
    public Guid[] Offer { get; init; } = [];
    public Guid? SelectingPlayerId { get; init; }
    public Guid? ChallengerId { get; init; }
    public string ChallengerSource { get; init; } = "random";
    public Matchup[] Matchups { get; init; } = [];
    public Guid? ActiveDuelId { get; init; }
    public Guid? ChampionId { get; init; }
    public ChallengerDecision[] Decisions { get; init; } = [];
    public Guid[] Commands { get; init; } = [];
    public int Seconds { get; init; } = 45;
    public int PassSeconds { get; init; } = 3;
    public QuestionOrder Order { get; init; } = QuestionOrder.Shuffled;
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? Completed { get; init; }
    [JsonIgnore] public Matchup? CurrentMatchup => Matchups.LastOrDefault();
}
public sealed record TournamentSetup(Guid CommandId, string[] Names, int Seconds = 45, int PassSeconds = 3,
    QuestionOrder Order = QuestionOrder.Shuffled);
public sealed record TournamentCommand(Guid TournamentId, Guid CommandId, long Version, string Action,
    Guid? PlayerId = null, Guid[]? CategoryIds = null, bool Confirm = false);
public sealed record PlayerStanding(Guid Id, string Name, int Color, bool Active, Guid? CategoryId, int Territories,
    int MatchupsPlayed, int MatchupsWon, int MatchupsLost, int DuelsWon, int DuelsLost, int? EliminationOrder);
public sealed record TournamentView(TournamentState State, Guid[] EligibleOpponents, PlayerStanding[] Standings, bool CanUndo);
public sealed record TournamentSummary(Guid Id, DateTimeOffset Created, TournamentPhase Phase, string[] Players, string? Champion);
public sealed record TournamentPool(Guid? SetId, string? SetName, TournamentCategory[] Categories, string[] Errors);
