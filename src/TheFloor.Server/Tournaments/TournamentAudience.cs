namespace TheFloor.Tournaments;

// Deliberate allowlist. Do not serialize TournamentState or the content deck to audience clients.
public sealed record AudienceTournamentPlayer(Guid Id, string Name, int Color, Guid? CategoryId, int? EliminationOrder,
    int Territories, int MatchupsWon, int MatchupsLost, int DuelsWon, int DuelsLost);
public sealed record AudienceCategory(Guid Id, string Name);
public sealed record AudienceMatchup(Guid ChallengerId, Guid DefenderId, int ChallengerScore, int DefenderScore,
    Guid DefenderCategoryId, Guid ChallengerCategoryId, Guid? DeciderCategoryId, Guid[] DeciderOffer, Guid[] Bans,
    TournamentDuel[] Duels, Guid? WinnerId, Guid? LoserId, int TerritoriesGained, int DuelNumber, Guid StartingPlayerId, Guid? CategoryId);
public sealed record AudienceTournament(Guid Id, long Version, TournamentPhase Phase, int Width, int Height,
    AudienceTournamentPlayer[] Players, Territory[] Floor, AudienceCategory[] Categories, Guid[] EligibleOpponents,
    Guid? ChallengerId, string ChallengerSource, Guid? SelectingPlayerId, int SelectionIndex, Guid[] SelectionOrder,
    Guid[] Offer, AudienceMatchup? Matchup, int CompletedMatchups, int TotalMatchups, Guid? ChampionId, Guid? ActiveDuelId);

public static class TournamentAudience
{
    public static AudienceTournament Project(TournamentState state)
    {
        TournamentRules.Validate(state);
        var matchup = state.CurrentMatchup;
        var standings = TournamentRules.Standings(state).ToDictionary(p => p.Id);
        return new AudienceTournament(state.Id, state.Version, state.Phase, state.Width, state.Height,
            state.Players.Select(p =>
            {
                var stats = standings[p.Id];
                return new AudienceTournamentPlayer(p.Id, p.Name, p.Color, p.CategoryId, p.EliminationOrder,
                    stats.Territories, stats.MatchupsWon, stats.MatchupsLost, stats.DuelsWon, stats.DuelsLost);
            }).ToArray(), state.Floor,
            state.Categories.Select(c => new AudienceCategory(c.Id, c.Name)).ToArray(),
            state.Phase == TournamentPhase.OpponentSelection && state.ChallengerId is { } id
                ? FloorAdjacency.Opponents(state, id) : [],
            state.ChallengerId, state.ChallengerSource, state.SelectingPlayerId, state.SelectionIndex, state.SelectionOrder,
            state.Offer, matchup is null ? null : new AudienceMatchup(matchup.ChallengerId, matchup.DefenderId,
                matchup.Duels.Count(d => d.WinnerId == matchup.ChallengerId), matchup.Duels.Count(d => d.WinnerId == matchup.DefenderId),
                matchup.DefenderCategoryId, matchup.ChallengerCategoryId, matchup.DeciderCategoryId,
                matchup.DeciderOffer, matchup.Bans, matchup.Duels, matchup.WinnerId, matchup.LoserId, matchup.Transferred.Length,
                matchup.Completed is not null ? matchup.Duels.Length : matchup.Duels.Length + 1,
                matchup.Duels.Length == 0 ? matchup.DefenderId : matchup.ChallengerId,
                matchup.Duels.Length == 0 ? matchup.DefenderCategoryId : matchup.Duels.Length == 1 ? matchup.ChallengerCategoryId : matchup.DeciderCategoryId),
            state.Matchups.Count(m => m.Completed is not null), state.Players.Length - 1, state.ChampionId, state.ActiveDuelId);
    }
}
