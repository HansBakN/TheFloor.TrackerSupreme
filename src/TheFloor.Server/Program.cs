using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TheFloor;
using TheFloor.Content;
using TheFloor.Duels;

var loadDemo = args.Contains("--load-demo", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--load-demo").ToArray());
var paths = DataPaths.FromEnvironment(builder.Environment.WebRootPath ??
    Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
builder.Services.AddSingleton(paths);
builder.Services.AddDbContextFactory<AppDb>(options => AppDb.Configure(options, paths));
builder.Services.AddSingleton<ContentLibrary>();
builder.Services.AddSingleton<ContentPackages>();
builder.Services.AddSingleton<Rehearsal>();
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
    database.Initialize();

if (loadDemo)
{
    try
    {
        var packages = app.Services.GetRequiredService<ContentPackages>();
        var count = DemoContent.Load(Path.Combine(AppContext.BaseDirectory, "demo"), packages);
        Console.WriteLine($"Demo sets available: {count}. Data directory: {paths.Root}");
    }
    catch (Exception error) when (error is ContentException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Could not load demo content. {error.Message}");
        Environment.ExitCode = 1;
    }
    return;
}

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
app.MapRehearsal();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
