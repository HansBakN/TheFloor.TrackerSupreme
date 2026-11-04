namespace TheFloor.Content;

public static class ContentEndpoints
{
    public static void MapContent(this WebApplication app)
    {
        var group = app.MapGroup("/api/library");
        group.MapGet("/", (ContentLibrary library) => library.View());
        group.MapPost("/reload", (ContentLibrary library) => library.View(refresh: true));
        group.MapPut("/categories/{id:guid}", (Guid id, Save<Category> request, ContentLibrary library) =>
            library.SaveCategory(request.RequiredDefinition with { Id = id }, request.Revision));
        group.MapPost("/categories/{id:guid}/duplicate", (Guid id, ContentLibrary library) =>
            library.DuplicateCategory(id));
        group.MapPost("/categories/{id:guid}/delete", (Guid id, DeleteCategory request, ContentLibrary library) =>
        {
            library.DeleteCategory(id, request.Revision, request.ConfirmedSets ?? throw new ContentException("Confirm the referencing sets."));
            return Results.NoContent();
        });
        group.MapPut("/sets/{id:guid}", (Guid id, Save<CategorySet> request, ContentLibrary library) =>
            library.SaveSet(request.RequiredDefinition with { Id = id }, request.Revision));
        group.MapPost("/sets/{id:guid}/duplicate", (Guid id, DuplicateSet request, ContentLibrary library) =>
            library.DuplicateSet(id, request.CopyCategories));
        group.MapPost("/sets/{id:guid}/delete", (Guid id, DeleteSet request, ContentLibrary library) =>
        {
            library.DeleteSet(id, request.Revision);
            return Results.NoContent();
        });
        group.MapPost("/sets/{id:guid}/activate", (Guid id, ContentLibrary library) =>
        {
            library.Activate(id);
            return Results.NoContent();
        });
        group.MapGet("/categories/{id:guid}/media/{**relative}",
            (Guid id, string relative, ContentLibrary library) =>
                Results.File(library.MediaFile(id, relative), Media.ContentType(relative), enableRangeProcessing: true));
        group.MapPost("/categories/{id:guid}/media", async (
            Guid id, HttpRequest request, ContentLibrary library, CancellationToken cancellationToken) =>
        {
            var form = await request.ReadFormAsync(cancellationToken);
            if (form.Files.Count != 1 || form.Files[0].Length > Media.MaxBytes)
                throw new ContentException("Choose one media file no larger than 24 MiB.");
            var file = form.Files[0];
            using var bytes = new MemoryStream();
            await file.CopyToAsync(bytes, cancellationToken);
            return Results.Ok(new { path = library.Upload(id, file.FileName, file.ContentType, bytes.ToArray()) });
        });
    }

    public sealed record Save<T>(T? Definition, string? Revision) where T : class
    {
        public T RequiredDefinition => Definition ?? throw new ContentException("A definition is required.");
    }
    public sealed record DeleteCategory(string Revision, Guid[]? ConfirmedSets);
    public sealed record DeleteSet(string Revision);
    public sealed record DuplicateSet(bool CopyCategories);
}
