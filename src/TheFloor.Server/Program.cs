using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TheFloor;
using TheFloor.Content;
using TheFloor.Duels;

var builder = WebApplication.CreateBuilder(args);
var paths = DataPaths.FromEnvironment();
builder.Services.AddSingleton(paths);
builder.Services.AddDbContextFactory<AppDb>(options => AppDb.Configure(options, paths));
builder.Services.AddSingleton<ContentLibrary>();
builder.Services.AddSingleton<LocalHostAccess>();
builder.Services.AddHostedService<ContentWatcher>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DuelService>();
builder.Services.AddSingleton<LiveDuel>();
builder.Services.AddSingleton<AudienceConnections>();
builder.Services.AddHostedService<DuelTicker>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));

var app = builder.Build();
using var dataLock = new FileStream(DataPaths.SafePath(paths.State, "server.lock"),
    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
using (var database = app.Services.GetRequiredService<IDbContextFactory<AppDb>>().CreateDbContext())
    database.Database.EnsureCreated();

app.UseStaticFiles();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (context.Request.Path.StartsWithSegments("/api/display"))
        context.Response.Headers.CacheControl = "no-store";
    if (context.Request.Path.StartsWithSegments("/api/library") ||
        context.Request.Path.StartsWithSegments("/api/host"))
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.RequestServices.GetRequiredService<LocalHostAccess>().IsAuthorized(context))
        {
            await Results.Json(new { error = "Host access required." }, statusCode: 401).ExecuteAsync(context);
            return;
        }
    }
    try
    {
        await next(context);
    }
    catch (ContentException error)
    {
        await Results.Json(new { error = error.Message }, statusCode: error is ContentConflict ? 409 : 400)
            .ExecuteAsync(context);
    }
    catch (Exception error) when (!context.Response.HasStarted && error is IOException or UnauthorizedAccessException)
    {
        app.Logger.LogError(error, "A data directory operation failed.");
        await Results.Json(new { error = "Cannot access the data directory. Check permissions and retry." }, statusCode: 503)
            .ExecuteAsync(context);
    }
});
app.MapGet("/api/health", () => TypedResults.Ok(new { status = "ready" }));
app.MapPost("/api/local-host", (HttpContext context, LocalHostAccess access) => access.SignIn(context));
app.MapContent();
app.MapDuel();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
