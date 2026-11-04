using TheFloor.Duels;

namespace TheFloor.Content;

public sealed record RehearsalPreview(Guid Id, string CategoryName, PublicQuestion Question);
public sealed record RehearsalSelection(Guid CategoryId, Guid QuestionId);

public sealed class Rehearsal(ContentLibrary library)
{
    private readonly object gate = new();
    private RehearsalPreview? preview;
    private Guid revision;
    private byte[]? media;
    private string? contentType;

    public RehearsalPreview Show(RehearsalSelection selection) => library.Exclusive(() =>
    {
        var view = library.View();
        var set = view.Sets.FirstOrDefault(item => item.Definition.Id == view.ActiveSetId)?.Definition;
        if (set?.CategoryIds.Contains(selection.CategoryId) != true)
            throw new ContentException("Choose a question in the active set.");
        var category = view.Categories.FirstOrDefault(item => item.Definition.Id == selection.CategoryId)?.Definition
            ?? throw new ContentException("The selected category is unavailable.");
        var question = category.Questions.FirstOrDefault(item => item.Id == selection.QuestionId)
            ?? throw new ContentException("The selected question is unavailable.");

        byte[]? bytes = null;
        string? type = null;
        if (question.Media is { } relative)
        {
            var file = Media.VerifyFile(library.CategoryFolder(category.Id), relative, question.Type);
            using var stream = File.OpenRead(file);
            if (stream.Length > Media.MaxBytes) throw new ContentException("The rehearsal media exceeds the size limit.");
            bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            Media.Validate(relative, bytes, bytes.Length, question.Type);
            type = Media.ContentType(file);
        }

        lock (gate)
        {
            var id = preview?.Id ?? Guid.NewGuid();
            revision = Guid.NewGuid();
            media = bytes;
            contentType = type;
            return preview = new RehearsalPreview(id, category.Name, new PublicQuestion(
                question.Id, question.Type, question.Prompt,
                bytes is null ? null : $"/api/display/rehearsal/{id:D}/media/{revision:D}"));
        }
    });

    public RehearsalPreview? Read(Guid? id = null)
    {
        lock (gate) return id is null || preview?.Id == id ? preview : null;
    }

    public IResult MediaResult(Guid id, Guid requestedRevision)
    {
        lock (gate)
            return preview?.Id == id && revision == requestedRevision && media is not null
                ? Results.File(media, contentType, enableRangeProcessing: true)
                : Results.NotFound();
    }

    public void End()
    {
        lock (gate)
        {
            preview = null;
            media = null;
            contentType = null;
        }
    }
}

public static class RehearsalEndpoints
{
    public static void MapRehearsal(this WebApplication app)
    {
        app.MapGet("/api/host/rehearsal", (Rehearsal rehearsal) => Json(rehearsal.Read()));
        app.MapPut("/api/host/rehearsal", (RehearsalSelection selection, Rehearsal rehearsal) => rehearsal.Show(selection));
        app.MapPost("/api/host/rehearsal/end", (Rehearsal rehearsal) =>
        {
            rehearsal.End();
            return Results.NoContent();
        });
        app.MapGet("/api/display/rehearsal/{id:guid}", (Guid id, Rehearsal rehearsal) => Json(rehearsal.Read(id)));
        app.MapGet("/api/display/rehearsal/{id:guid}/media/{revision:guid}",
            (Guid id, Guid revision, Rehearsal rehearsal) => rehearsal.MediaResult(id, revision));
    }

    private static IResult Json(RehearsalPreview? value) => value is null
        ? Results.Text("null", "application/json")
        : Results.Json(value);
}
