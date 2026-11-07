using System.Text.Json;

namespace TheFloor.Content;

internal enum ContentStepKind { ReplaceSet, DeleteCategory, InstallCategory, InstallSet, ArchiveCategory }

internal sealed record ContentStep(ContentStepKind Kind, string Target, string? Source,
    string? Before, string? After);

internal sealed class ContentJournal(DataPaths paths)
{
    private const string Manifest = "plan.json";
    private const string Prefix = "content-op-";

    public string CreateStage() =>
        Directory.CreateDirectory(DataPaths.SafePath(paths.State, $"{Prefix}{Guid.NewGuid():N}")).FullName;

    public void WritePlan(string stage, IReadOnlyList<ContentStep> steps) =>
        AtomicFiles.Write(DataPaths.SafePath(stage, Manifest), JsonSerializer.SerializeToUtf8Bytes(steps));

    public void Recover()
    {
        foreach (var stage in Directory.EnumerateDirectories(paths.State, $"{Prefix}*").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(stage);
            if (!Guid.TryParseExact(name[Prefix.Length..], "N", out _)) continue;
            var safe = DataPaths.SafePath(paths.State, name);
            if (EntryPresent(DataPaths.SafePath(safe, Manifest))) Apply(safe);
            else DiscardUncommitted(safe);
        }
    }

    public void Apply(string stage)
    {
        var manifest = DataPaths.SafePath(stage, Manifest);
        string? target = null;
        try
        {
            var steps = JsonSerializer.Deserialize<ContentStep[]>(AtomicFiles.ReadDefinition(manifest))
                ?? throw new ContentException("The recovery plan is empty.");
            foreach (var validateOnly in new[] { true, false })
            foreach (var step in steps)
            {
                target = step.Target;
                ApplyStep(stage, step, validateOnly);
            }
            target = null;
            File.Delete(manifest);
            Directory.Delete(stage, recursive: true);
        }
        catch (Exception error) when (error is ContentException or IOException or UnauthorizedAccessException or JsonException)
        {
            var affected = target is null ? "" : $" Affected path: '{target}'.";
            throw new ContentConflict($"Content recovery is pending in '{Path.GetFileName(stage)}'.{affected} " +
                $"Resolve the conflict, then reload. {error.Message}");
        }
    }

    public static void DiscardUncommitted(string stage)
    {
        if (Directory.Exists(stage) && !EntryPresent(Path.Combine(stage, Manifest)))
            Directory.Delete(stage, recursive: true);
    }

    private void ApplyStep(string stage, ContentStep step, bool validateOnly)
    {
        var target = DataPaths.SafePath(paths.Root, step.Target);
        var segments = step.Target.Split('/');
        switch (step.Kind)
        {
            case ContentStepKind.ReplaceSet when segments is ["sets", _, "set.yaml"]:
            {
                var bytes = AtomicFiles.ReadDefinition(DataPaths.SafePath(stage, step.Source!));
                if (AtomicFiles.Revision(bytes) != step.After) throw new ContentConflict("A staged set changed.");
                if (Matches(target, step.After)) return;
                var before = step.Before ?? throw new ContentConflict("A set revision is missing.");
                if (validateOnly) AtomicFiles.EnsureRevision(target, before);
                else AtomicFiles.Write(target, bytes, before);
                return;
            }
            case ContentStepKind.DeleteCategory when segments is ["library", "categories", _, "category.yaml"]:
                if (!EntryPresent(target)) return;
                AtomicFiles.EnsureRevision(target, step.Before ?? throw new ContentConflict("A category revision is missing."));
                if (!validateOnly) File.Delete(target);
                return;
            case ContentStepKind.ArchiveCategory when segments is ["library", "categories", var folder]:
            {
                var destination = step.Source ?? throw new ContentException("A backup destination is required.");
                if (destination.Split('/') is not ["backups", var backup, "categories", var archivedFolder] ||
                    !backup.StartsWith("category-reset-", StringComparison.Ordinal) || archivedFolder != folder)
                    throw new ContentException("Invalid category backup destination.");
                var archived = DataPaths.SafePath(paths.Root, destination);
                var before = step.Before ?? throw new ContentConflict("A category revision is missing.");
                if (!Directory.Exists(target))
                {
                    if (Matches(DataPaths.SafePath(archived, "category.yaml"), before)) return;
                    throw new ContentConflict("The category and its backup are missing or changed.");
                }
                AtomicFiles.EnsureRevision(DataPaths.SafePath(target, "category.yaml"), before);
                if (Path.Exists(archived)) throw new ContentConflict("The category backup destination already exists.");
                if (!validateOnly)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(archived)!);
                    Directory.Move(target, archived);
                }
                return;
            }
            case ContentStepKind.InstallCategory when segments is ["library", "categories", var categoryId] &&
                                                       Guid.TryParseExact(categoryId, "D", out _):
                Install(stage, step, target, "category.yaml", validateOnly);
                return;
            case ContentStepKind.InstallSet when segments is ["sets", var setId] && Guid.TryParseExact(setId, "D", out _):
                Install(stage, step, target, "set.yaml", validateOnly);
                return;
            default:
                throw new ContentException("A recovery plan contains an invalid operation.");
        }
    }

    private static void Install(string stage, ContentStep step, string target, string definition, bool validateOnly)
    {
        var source = DataPaths.SafePath(stage, step.Source!);
        if (!Directory.Exists(source))
        {
            if (Matches(DataPaths.SafePath(target, definition), step.After)) return;
            throw new ContentConflict($"The installed definition at '{step.Target}' changed or is missing.");
        }

        if (Path.Exists(target)) throw new ContentConflict($"The import destination '{step.Target}' already exists.");
        if (!Matches(DataPaths.SafePath(source, definition), step.After))
            throw new ContentConflict($"The staged definition for '{step.Target}' changed or is missing.");
        if (!validateOnly) Directory.Move(source, target);
    }

    private static bool EntryPresent(string path)
    {
        try
        {
            File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static bool Matches(string path, string? revision) =>
        revision is not null && File.Exists(path) && AtomicFiles.Revision(AtomicFiles.ReadDefinition(path)) == revision;
}
