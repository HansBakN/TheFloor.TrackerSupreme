using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.SignalR;
using TheFloor.Content;

namespace TheFloor.Duels;

public sealed record PublicQuestion(Guid Id, string Type, string? Prompt, string? MediaUrl);
public sealed record AudienceDuel(Guid Id, long Version, DuelStatus Status, string CategoryName,
    string PlayerA, string PlayerB, double AMilliseconds, double BMilliseconds, Player Active,
    Player? Winner, PublicQuestion? Question, long SampledAt, Guid? AudioDisplayId, double PassMilliseconds);
public sealed record HostDuel(AudienceDuel Duel, Guid CategorySetId, DuelConfiguration Configuration,
    string? Answer, string[] Alternatives, string? Notes, bool CanUndo, bool Exhausted);
public sealed record HostState(HostDuel? Session, int AudienceCount, AudienceStatus Audience);

public sealed class DuelHub(LocalHostAccess access, AudienceConnections connections, LiveDuel live) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var context = Context.GetHttpContext();
        if (context is not null && context.Request.Query["role"].ToString() == "host")
        {
            if (!access.IsAuthorized(context))
            {
                Context.Abort();
                return;
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, "host");
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "audience");
            var display = context?.Request.Query["displayId"].ToString();
            connections.Connect(Context.ConnectionId, Guid.TryParse(display, out var id) ? id : Guid.NewGuid());
        }
        await live.Broadcast();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Disconnect(Context.ConnectionId);
        await live.Broadcast();
        await base.OnDisconnectedAsync(exception);
    }
}

public sealed class LiveDuel(DuelService duels, AudienceConnections connections,
    IHubContext<DuelHub> hub, TimeProvider time)
{
    private readonly SemaphoreSlim gate = new(1);

    public AudienceDuel? Audience() => Project(duels.Read(), connections.Read().AudioDisplayId);

    public HostState Host() => HostProjection(duels.Read(), connections.Read());

    public async Task Broadcast(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var session = duels.Read();
            var audience = connections.Read();
            await Task.WhenAll(
                hub.Clients.Group("audience").SendAsync("duel", Project(session, audience.AudioDisplayId), cancellationToken),
                hub.Clients.Group("host").SendAsync("hostDuel", HostProjection(session, audience), cancellationToken));
        }
        finally
        {
            gate.Release();
        }
    }

    private HostState HostProjection(DuelSession? session, AudienceStatus audience) => new(session is null ? null :
        new HostDuel(Project(session, audience.AudioDisplayId, host: true)!, session.CategorySetId, session.Configuration,
            session.Current?.Answer, session.Current?.Alternatives ?? [], session.Current?.Notes,
            session.Undo is not null, session.Exhausted), audience.Displays.Length, audience);

    private AudienceDuel? Project(DuelSession? session, Guid? audioDisplayId, bool host = false)
    {
        if (session is null) return null;
        var question = (host && session.PassTicks == 0 || session.AudienceQuestionVisible) ? session.Current : null;
        return new AudienceDuel(session.Id, session.Version, session.Status, session.CategoryName,
            session.Configuration.PlayerA, session.Configuration.PlayerB,
            TimeSpan.FromTicks(session.ATicks).TotalMilliseconds, TimeSpan.FromTicks(session.BTicks).TotalMilliseconds,
            session.Active, session.Winner, question is null ? null :
                new PublicQuestion(question.Id, question.Type, question.Prompt, question.Media is null ? null :
                    $"/api/{(host ? "host" : "display")}/{session.Id:D}/media/{question.Id:D}"),
            time.GetUtcNow().ToUnixTimeMilliseconds(), audioDisplayId, TimeSpan.FromTicks(session.PassTicks).TotalMilliseconds);
    }
}

public sealed class DuelTicker(DuelService duels, LiveDuel live, TimeProvider time, ILogger<DuelTicker> logger)
    : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        duels.SaveCheckpoint();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(75), time);
        (Guid?, long?) previous = default;
        var lastSync = time.GetTimestamp();
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var state = duels.Tick();
                var current = (state?.Id, state?.Version);
                if (previous != current || state?.Status == DuelStatus.Running &&
                    time.GetElapsedTime(lastSync) >= TimeSpan.FromSeconds(1))
                {
                    await live.Broadcast(stoppingToken);
                    previous = current;
                    lastSync = time.GetTimestamp();
                }
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(error, "Could not update the duel state.");
                await Task.Delay(TimeSpan.FromSeconds(1), time, stoppingToken);
            }
        }
    }
}

public static class DuelEndpoints
{
    public sealed record AudioSelection(Guid? DisplayId);

    public static void MapDuel(this WebApplication app)
    {
        app.MapHub<DuelHub>("/live");
        app.MapGet("/api/display/state", (LiveDuel live) => live.Audience() is { } state
            ? Results.Json(state)
            : Results.Text("null", "application/json"));
        app.MapGet("/api/display/{id:guid}/media/{question:guid}", (Guid id, Guid question, DuelService service) =>
        {
            var file = service.CurrentMedia(id, question);
            return Results.File(file, Media.ContentType(file), enableRangeProcessing: true);
        });
        app.MapGet("/api/host/{id:guid}/media/{question:guid}", (Guid id, Guid question, DuelService service) =>
        {
            var file = service.CurrentMedia(id, question, host: true);
            return Results.File(file, Media.ContentType(file), enableRangeProcessing: true);
        });
        app.MapGet("/api/host/pairing", (IServer server) => AudiencePairing.Origins(server));
        app.MapPut("/api/host/audio-display", async (AudioSelection selection, AudienceConnections connections, LiveDuel live) =>
        {
            connections.SelectAudio(selection.DisplayId);
            await live.Broadcast();
            return Results.NoContent();
        });
        app.MapGet("/api/host/state", (LiveDuel live) => live.Host());
        app.MapPost("/api/host/duel", async (DuelConfiguration configuration, DuelService service, LiveDuel live) =>
        {
            service.Prepare(configuration);
            await live.Broadcast();
            return live.Host();
        });
        app.MapPost("/api/host/command", async (DuelCommand command, DuelService service, LiveDuel live) =>
        {
            service.Command(command);
            await live.Broadcast();
            return live.Host();
        });
    }
}
