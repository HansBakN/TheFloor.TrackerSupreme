using Microsoft.EntityFrameworkCore;

namespace TheFloor.Content;

public sealed class ContentLibrary(DataPaths paths, IDbContextFactory<AppDb> database)
{
    private sealed record Stored<T>(string Path, ContentEntry<T> Entry);
    private readonly object gate = new();
    private Dictionary<Guid, Stored<Category>> categories = [];
    private Dictionary<Guid, Stored<CategorySet>> sets = [];
    private string[] errors = [];
    private readonly HashSet<Guid> duplicateCategories = [];
    private readonly HashSet<Guid> duplicateSets = [];

    public LibraryView View(bool refresh = false)
    {
        lock (gate)
        {
            if (refresh) Scan();
            using var db = database.CreateDbContext();
            return new LibraryView(categories.Values.Select(c => c.Entry).OrderBy(c => c.Definition.Name).ToArray(),
                sets.Values.Select(s => s.Entry).OrderBy(s => s.Definition.Name).ToArray(),
                db.Settings.Find(1)?.ActiveSetId, errors, paths.Root);
        }
    }

    public T Exclusive<T>(Func<T> operation)
    {
        lock (gate)
        {
            Scan();
            return operation();
        }
    }

    public string CategoryFolder(Guid id)
    {
        lock (gate)
        {
            var folder = Path.GetDirectoryName(Required(categories, id, "Category").Path)!;
            return DataPaths.SafePath(paths.Categories, Path.GetFileName(folder));
        }
    }

    public ContentEntry<Category> SaveCategory(Category category, string? revision) => Exclusive(() =>
    {
        var current = Existing(categories, duplicateCategories, category.Id, revision);
        var folder = current is null ? NewFolder(paths.Categories, category.Id) : CategoryFolder(category.Id);
        ValidateCategory(category, folder);
        var path = DataPaths.SafePath(folder, "category.yaml");
        WriteDefinition(path, category, revision);
        Scan();
        return Required(categories, category.Id, "Category").Entry;
    });

    public ContentEntry<CategorySet> SaveSet(CategorySet set, string? revision) => Exclusive(() =>
    {
        var current = Existing(sets, duplicateSets, set.Id, revision);
        ValidateSet(set);
        var path = current?.Path ?? DataPaths.SafePath(NewFolder(paths.Sets, set.Id), "set.yaml");
        WriteDefinition(path, set, revision);
        Scan();
        return Required(sets, set.Id, "Set").Entry;
    });

    public void Activate(Guid id) => Exclusive(() =>
    {
        Required(sets, id, "Set");
        using var db = database.CreateDbContext();
        var setting = db.Settings.Find(1);
        if (setting is null) db.Settings.Add(new AppSettings { ActiveSetId = id });
        else setting.ActiveSetId = id;
        db.SaveChanges();
        return true;
    });

    public ContentEntry<Category> DuplicateCategory(Guid id) => Exclusive(() =>
    {
        var copy = CopyCategory(id);
        Scan();
        return Required(categories, copy.Id, "Category").Entry;
    });

    public ContentEntry<CategorySet> DuplicateSet(Guid id, bool copyCategories) => Exclusive(() =>
    {
        var original = Required(sets, id, "Set").Entry.Definition;
        var created = new List<Guid>();
        try
        {
            var references = original.CategoryIds;
            if (copyCategories)
            {
                foreach (var category in references) created.Add(CopyCategory(category).Id);
                references = created.ToArray();
                Scan();
            }
            return SaveSet(original with { Id = Guid.NewGuid(), Name = CopyName(original.Name), CategoryIds = references }, null);
        }
        catch
        {
            foreach (var category in created) Directory.Delete(DataPaths.SafePath(paths.Categories, category.ToString("D")), true);
            Scan();
            throw;
        }
    });

    public void DeleteSet(Guid id, string revision) => Exclusive(() =>
    {
        var stored = Required(sets, id, "Set");
        AtomicFiles.EnsureRevision(stored.Path, revision);
        File.Delete(stored.Path);
        using var db = database.CreateDbContext();
        if (db.Settings.Find(1) is { } setting && setting.ActiveSetId == id)
        {
            setting.ActiveSetId = null;
            db.SaveChanges();
        }
        Scan();
        return true;
    });

    public void DeleteCategory(Guid id, string revision, Guid[] confirmedSets) => Exclusive(() =>
    {
        var stored = Required(categories, id, "Category");
        AtomicFiles.EnsureRevision(stored.Path, revision);
        var referencing = sets.Values.Where(s => s.Entry.Definition.CategoryIds.Contains(id)).ToArray();
        if (!referencing.Select(s => s.Entry.Definition.Id).ToHashSet().SetEquals(confirmedSets))
            throw new ContentConflict("The referencing sets changed. Reload and confirm deletion again.");

        foreach (var set in referencing) AtomicFiles.EnsureRevision(set.Path, set.Entry.Revision);
        var changed = new Stack<(Stored<CategorySet> Set, string Revision)>();
        try
        {
            foreach (var set in referencing)
            {
                var updated = set.Entry.Definition with
                {
                    CategoryIds = set.Entry.Definition.CategoryIds.Where(reference => reference != id).ToArray(),
                };
                changed.Push((set, WriteDefinition(set.Path, updated, set.Entry.Revision)));
            }
            AtomicFiles.EnsureRevision(stored.Path, revision);
            File.Delete(stored.Path);
        }
        catch
        {
            while (changed.TryPop(out var change))
                WriteDefinition(change.Set.Path, change.Set.Entry.Definition, change.Revision);
            throw;
        }
        Scan();
        return true;
    });

    public string Upload(Guid id, string fileName, string mime, byte[] bytes) => Exclusive(() =>
    {
        var folder = CategoryFolder(id);
        Media.Validate(fileName, bytes, bytes.LongLength, mime);
        var mediaFolder = DataPaths.SafePath(folder, "media");
        Directory.CreateDirectory(mediaFolder);
        var relative = $"media/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        AtomicFiles.Write(DataPaths.SafePath(folder, relative), bytes);
        return relative;
    });

    public string MediaFile(Guid id, string relative) => Exclusive(() => Media.VerifyFile(CategoryFolder(id), relative));

    private Category CopyCategory(Guid id)
    {
        var source = Required(categories, id, "Category").Entry.Definition;
        var sourceFolder = CategoryFolder(id);
        var copy = source with
        {
            Id = Guid.NewGuid(),
            Name = CopyName(source.Name),
            Questions = source.Questions.Select(q => q with { Id = Guid.NewGuid() }).ToArray(),
        };
        var folder = NewFolder(paths.Categories, copy.Id);
        try
        {
            foreach (var media in Media.References(copy).DistinctBy(x => x.Path))
            {
                var target = DataPaths.SafePath(folder, media.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Media.VerifyFile(sourceFolder, media.Path, media.Kind), target);
            }
            ValidateCategory(copy, folder);
            WriteDefinition(DataPaths.SafePath(folder, "category.yaml"), copy, null);
            return copy;
        }
        catch
        {
            Directory.Delete(folder, true);
            throw;
        }
    }

    private void Scan()
    {
        var diagnostics = new List<string>();
        categories = ScanDefinitions(paths.Categories, "category.yaml", categories, c => c.Id, ValidateCategory, duplicateCategories, diagnostics);
        sets = ScanDefinitions(paths.Sets, "set.yaml", sets, s => s.Id,
            (set, _) => ContentYaml.Validate(set), duplicateSets, diagnostics);
        foreach (var set in sets.Values)
        foreach (var reference in set.Entry.Definition.CategoryIds)
            if (!categories.ContainsKey(reference))
                diagnostics.Add($"Set '{set.Entry.Definition.Name}' references missing category {reference}.");
        errors = diagnostics.ToArray();
    }

    private static Dictionary<Guid, Stored<T>> ScanDefinitions<T>(string root, string name,
        Dictionary<Guid, Stored<T>> prior, Func<T, Guid> key, Action<T, string> validate, HashSet<Guid> duplicates, List<string> diagnostics)
    {
        duplicates.Clear();
        var candidates = new List<Stored<T>>();
        var previousFiles = prior.Values.ToDictionary(s => s.Path);
        foreach (var directory in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            var path = Path.Combine(directory, name);
            try
            {
                if (Path.GetFileName(directory).StartsWith('.')) continue;
                path = DataPaths.SafePath(root, $"{Path.GetFileName(directory)}/{name}");
                if (!File.Exists(path)) continue;
                var bytes = AtomicFiles.ReadDefinition(path);
                var definition = ContentYaml.Read<T>(bytes);
                validate(definition, directory);
                candidates.Add(new Stored<T>(path, new ContentEntry<T>(definition, AtomicFiles.Revision(bytes))));
            }
            catch (Exception error) when (error is ContentException or IOException or UnauthorizedAccessException)
            {
                diagnostics.Add($"{Path.GetFileName(directory)}/{name}: {error.Message}");
                if (previousFiles.TryGetValue(path, out var previous)) candidates.Add(previous);
            }
        }
        var result = new Dictionary<Guid, Stored<T>>();
        foreach (var group in candidates.GroupBy(s => key(s.Entry.Definition)))
        {
            var matches = group.ToArray();
            if (matches.Length == 1) result.Add(group.Key, matches[0]);
            else
            {
                duplicates.Add(group.Key);
                diagnostics.Add($"Duplicate ID {group.Key} in {string.Join(", ", matches.Select(x => Path.GetFileName(Path.GetDirectoryName(x.Path))))}.");
                if (prior.TryGetValue(group.Key, out var previous) && matches.Any(x => x.Path == previous.Path))
                    result.Add(group.Key, previous);
            }
        }
        return result;
    }

    private static void ValidateCategory(Category category, string folder)
    {
        ContentYaml.Validate(category);
        foreach (var reference in Media.References(category).Distinct())
            Media.VerifyFile(folder, reference.Path, reference.Kind);
    }

    private void ValidateSet(CategorySet set)
    {
        ContentYaml.Validate(set);
        foreach (var id in set.CategoryIds) Required(categories, id, "Category");
    }

    private static Stored<T> Required<T>(Dictionary<Guid, Stored<T>> items, Guid id, string type) =>
        items.TryGetValue(id, out var stored) ? stored : throw new ContentException($"{type} {id} is unavailable.");

    private static Stored<T>? Existing<T>(Dictionary<Guid, Stored<T>> items, HashSet<Guid> duplicates, Guid id, string? revision)
    {
        if (duplicates.Contains(id)) throw new ContentConflict("Resolve the duplicate ID on disk before editing this definition.");
        if (items.TryGetValue(id, out var current))
        {
            if (revision is null || current.Entry.Revision != revision)
                throw new ContentConflict("This definition changed. Reload it and apply your edits again.");
            return current;
        }
        if (revision is not null) throw new ContentConflict("This definition is no longer available.");
        if (id == Guid.Empty) throw new ContentException("A stable UUID is required.");
        return null;
    }

    private static string NewFolder(string root, Guid id)
    {
        var folder = DataPaths.SafePath(root, id.ToString("D"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string WriteDefinition<T>(string path, T definition, string? revision)
    {
        var bytes = ContentYaml.Write(definition);
        if (bytes.Length > ContentYaml.MaxDefinitionBytes) throw new ContentException("Definition exceeds 4 MiB.");
        AtomicFiles.Write(path, bytes, revision);
        return AtomicFiles.Revision(bytes);
    }

    private static string CopyName(string name) => name[..Math.Min(143, name.Length)] + " (copy)";
}
