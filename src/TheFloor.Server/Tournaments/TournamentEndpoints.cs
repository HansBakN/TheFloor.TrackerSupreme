using TheFloor.Duels;

namespace TheFloor.Tournaments;

public static class TournamentEndpoints
{
    public static void MapTournaments(this WebApplication app)
    {
        app.MapGet("/api/host/tournaments/pool", (TournamentService tournaments) => tournaments.Pool());
        app.MapGet("/api/host/tournaments", (TournamentService tournaments) => tournaments.List());
        app.MapGet("/api/host/tournaments/{id:guid}", (Guid id, TournamentService tournaments) => tournaments.Read(id));
        app.MapPost("/api/host/tournaments", (TournamentSetup setup, TournamentService tournaments) => tournaments.Create(setup));
        app.MapPost("/api/host/tournaments/command", async (TournamentCommand command, TournamentService tournaments, LiveDuel live) =>
        {
            var state = tournaments.Command(command);
            await live.Broadcast();
            return state is null ? Results.Text("null", "application/json") : Results.Json(state);
        });
    }
}
