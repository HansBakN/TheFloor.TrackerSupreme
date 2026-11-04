using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TheFloor.Content;

namespace TheFloor.Duels;

public sealed record PublicQuestion(Guid Id, string Type, string? Prompt, string? MediaUrl);
public sealed record AudienceDuel(Guid Id, long Version, DuelStatus Status, string CategoryName,
    string PlayerA, string PlayerB, double AMilliseconds, double BMilliseconds, Player Active,
    Player? Winner, PublicQuestion? Question, long SampledAt);
public sealed record HostDuel(AudienceDuel Duel, Guid CategorySetId, DuelConfiguration Configuration,
    string? Answer, string[] Alternatives, string? Notes, bool CanUndo, bool Exhausted);
public sealed record HostState(HostDuel? Session, int AudienceCount);

public sealed class AudienceConnections
{
    private readonly ConcurrentDictionary<string, byte> audience = new();
    public int Count => audience.Count;
    public void Connect(string id) => audience.TryAdd(id, 0);
    public void Disconnect(string id) => audience.TryRemove(id, out _);
}

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
            connections.Connect(Context.ConnectionId);
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

    public AudienceDuel? Audience() => Project(duels.Read());

    public HostState Host() => HostProjection(duels.Read());

    public async Task Broadcast(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var session = duels.Read();
            await Task.WhenAll(
                hub.Clients.Group("audience").SendAsync("duel", Project(session), cancellationToken),
                hub.Clients.Group("host").SendAsync("hostDuel", HostProjection(session), cancellationToken));
        }
        finally
        {
            gate.Release();
        }
    }

    private HostState HostProjection(DuelSession? session) => new(session is null ? null :
        new HostDuel(Project(session)!, session.CategorySetId, session.Configuration,
            session.Current?.Answer, session.Current?.Alternatives ?? [], session.Current?.Notes,
            session.Undo is not null, session.Exhausted), connections.Count);

    private AudienceDuel? Project(DuelSession? session)
    {
        if (session is null) return null;
        var question = session.Current;
        return new AudienceDuel(session.Id, session.Version, session.Status, session.CategoryName,
            session.Configuration.PlayerA, session.Configuration.PlayerB,
            TimeSpan.FromTicks(session.ATicks).TotalMilliseconds, TimeSpan.FromTicks(session.BTicks).TotalMilliseconds,
            session.Active, session.Winner, question is null ? null :
                new PublicQuestion(question.Id, question.Type, question.Prompt, question.Media is null ? null :
                    $"/api/display/{session.Id:D}/media/{question.Id:D}"),
            time.GetUtcNow().ToUnixTimeMilliseconds());
    }
}

public sealed class DuelTicker(DuelService duels, LiveDuel live, TimeProvider time, ILogger<DuelTicker> logger)
    : BackgroundService
{
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
