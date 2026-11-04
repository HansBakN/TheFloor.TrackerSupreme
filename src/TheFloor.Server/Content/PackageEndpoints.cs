using Microsoft.AspNetCore.Http.Features;

namespace TheFloor.Content;

public static class PackageEndpoints
{
    public static void MapPackages(this RouteGroupBuilder group)
    {
        group.MapGet("/sets/{id:guid}/export", (Guid id, ContentPackages packages) =>
            Results.File(packages.ExportSet(id), "application/zip", $"set-{id:D}.zip"));
        group.MapGet("/categories/{id:guid}/export", (Guid id, ContentPackages packages) =>
            Results.File(packages.ExportCategory(id), "application/zip", $"category-{id:D}.zip"));
        group.MapGet("/categories/{id:guid}/export.json", (Guid id, ContentPackages packages) =>
            Results.File(packages.ExportJson(id), "application/json", $"category-{id:D}.json"));
        group.MapPost("/sets/import", async (HttpContext context, ContentPackages packages, bool? copyConflicts,
            CancellationToken cancellationToken) =>
            packages.ImportZip(await Read(context, ContentPackages.MaxPackageBytes, cancellationToken), asSet: true, copyConflicts == true));
        group.MapPost("/categories/import", async (HttpContext context, ContentPackages packages, bool? copyConflicts,
            CancellationToken cancellationToken) =>
            packages.ImportZip(await Read(context, ContentPackages.MaxPackageBytes, cancellationToken), asSet: false, copyConflicts == true));
        group.MapPost("/categories/import.json", async (HttpContext context, ContentPackages packages, bool? copyConflicts,
            CancellationToken cancellationToken) =>
            packages.ImportJson(await Read(context, ContentYaml.MaxDefinitionBytes, cancellationToken), copyConflicts == true));
    }

    private static async Task<byte[]> Read(HttpContext context, int maximum, CancellationToken cancellationToken)
    {
        var request = context.Request;
        if (request.ContentLength > maximum) throw new ContentException("The upload exceeds the package size limit.");
        var size = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (size is { IsReadOnly: false }) size.MaxRequestBodySize = maximum + 1L;
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        try
        {
            int count;
            while ((count = await request.Body.ReadAsync(buffer, cancellationToken)) != 0)
            {
                if (output.Length + count > maximum) throw new ContentException("The upload exceeds the package size limit.");
                output.Write(buffer, 0, count);
            }
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            throw new ContentException("The upload exceeds the package size limit.");
        }
        return output.ToArray();
    }
}
