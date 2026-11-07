using TheFloor.Duels;

namespace TheFloor.Tournaments;

public static class TournamentEndpoints
{
    public static void MapTournaments(this WebApplication app)
    {
        app.MapGet("/api/host/tournaments/pool", (TournamentService tournaments) => tournaments.Pool());
        app.MapGet("/api/host/tournaments", (TournamentService tournaments) => tournaments.List());
        app.MapGet("/api/host/tournaments/{id:guid}", (Guid id, TournamentService tournaments) => tournaments.Read(id));
        app.MapGet("/api/display/tournament", (TournamentService tournaments) => tournaments.Audience() is { } state
            ? Results.Json(state) : Results.Text("null", "application/json"));
        app.MapPost("/api/host/tournaments", async (TournamentSetup setup, TournamentService tournaments, LiveDuel live) =>
        {
            var state = tournaments.Create(setup);
            await live.Broadcast();
            return state;
        });
        app.MapPost("/api/host/tournaments/command", async (TournamentCommand command, TournamentService tournaments, LiveDuel live) =>
        {
            var state = tournaments.Command(command);
            await live.Broadcast();
            return state is null ? Results.Text("null", "application/json") : Results.Json(state);
        });
    }
}
