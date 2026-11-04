using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheFloor.Content;

public sealed record ImportResult(Guid? SetId, Guid? CategoryId, int ReusedCategories, int CopiedCategories);

public sealed class ContentPackages(ContentLibrary library, DataPaths paths)
{
    public const int MaxPackageBytes = 128 * 1024 * 1024;
    private const int MaxEntries = 10_000;
    private static readonly JsonSerializerOptions TextJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private sealed record BundledCategory(Category Definition, Dictionary<string, byte[]> MediaFiles);

    public byte[] ExportCategory(Guid id) => library.Exclusive(() =>
        WriteZip(budget => WriteCategory(budget, FindCategory(id), "")));

    public byte[] ExportSet(Guid id) => library.Exclusive(() =>
    {
        var set = library.View().Sets.FirstOrDefault(entry => entry.Definition.Id == id)?.Definition
            ?? throw new ContentException("This set is unavailable.");
        return WriteZip(budget =>
        {
            budget.Add("set.yaml", ContentYaml.Write(set));
            foreach (var categoryId in set.CategoryIds)
                WriteCategory(budget, FindCategory(categoryId), $"categories/{categoryId:D}/");
        });
    });

    public byte[] ExportJson(Guid id) => library.Exclusive(() =>
    {
        var category = FindCategory(id);
        ValidateText(category);
        return JsonSerializer.SerializeToUtf8Bytes(category, TextJson);
    });

    public ImportResult ImportZip(byte[] bytes, bool asSet, bool copyConflicts)
    {
        if (bytes.Length > MaxPackageBytes) throw new ContentException("The ZIP exceeds 128 MiB.");
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
            var files = ReadEntries(zip);
            var set = asSet ? ContentYaml.Read<CategorySet>(Take(files, "set.yaml")) : null;
            if (set is not null) ContentYaml.Validate(set);
            var categories = set is null ? [ReadCategory(files, "")] : set.CategoryIds.Select(id =>
            {
                var category = ReadCategory(files, $"categories/{id:D}/");
                if (category.Definition.Id != id) throw new ContentException("The category UUID does not match its ZIP directory.");
                return category;
            }).ToArray();
            if (files.Count > 0) throw new ContentException($"Unexpected ZIP entry '{files.Keys.First()}'.");
            return Integrate(categories, set, copyConflicts);
        }
        catch (InvalidDataException error)
        {
            throw new ContentException($"Invalid ZIP. {error.Message}");
        }
    }

    public ImportResult ImportJson(byte[] bytes, bool copyConflicts)
    {
        if (bytes.Length > ContentYaml.MaxDefinitionBytes) throw new ContentException("JSON exceeds 4 MiB.");
        Category category;
        try
        {
            category = JsonSerializer.Deserialize<Category>(bytes, TextJson)
                ?? throw new ContentException("JSON must contain a category.");
        }
        catch (JsonException error)
        {
            throw new ContentException($"Invalid JSON. {error.Message}");
        }
        ContentYaml.Validate(category);
        ValidateText(category);
        return Integrate([new BundledCategory(category, [])], null, copyConflicts);
    }

    private ImportResult Integrate(BundledCategory[] categories, CategorySet? set, bool copyConflicts) =>
        library.Exclusive(() =>
        {
            var view = library.View();
            var knownCategories = view.Categories.ToDictionary(entry => entry.Definition.Id);
            var knownSets = view.Sets.ToDictionary(entry => entry.Definition.Id);
            var remapped = new Dictionary<Guid, Guid>();
            var additions = new List<BundledCategory>();
            var reused = 0;
            var copied = 0;
            foreach (var package in categories)
            {
                var original = package.Definition;
                var id = original.Id;
                var ambiguous = library.HasDuplicateCategoryId(id);
                if (knownCategories.TryGetValue(id, out var existing) && !ambiguous && Matches(existing, package))
                {
                    reused++;
                }
                else if (ambiguous || existing is not null || Directory.Exists(CategoryDestination(id)))
                {
                    if (!copyConflicts) throw new ContentConflict($"Category '{original.Name}' conflicts with the existing UUID {id}. Import independent copies to keep both.");
                    id = Guid.NewGuid();
                    additions.Add(package with { Definition = original with
                    {
                        Id = id,
                        Questions = original.Questions.Select(question => question with { Id = Guid.NewGuid() }).ToArray(),
                    } });
                    copied++;
                }
                else
                {
                    additions.Add(package);
                }
                remapped.Add(original.Id, id);
            }

            var newSet = set;
            if (set is not null)
            {
                newSet = set with { CategoryIds = set.CategoryIds.Select(id => remapped[id]).ToArray() };
                var ambiguous = library.HasDuplicateSetId(set.Id);
                if (knownSets.TryGetValue(set.Id, out var existing) && !ambiguous &&
                    SameDefinition(newSet, existing.Definition) && library.HasCurrentSetRevision(set.Id, existing.Revision))
                    newSet = null;
                else if (ambiguous || existing is not null || Directory.Exists(SetDestination(set.Id)))
                {
                    if (!copyConflicts) throw new ContentConflict($"Set '{set.Name}' conflicts with the existing UUID {set.Id}. Import an independent copy to keep both.");
                    newSet = newSet with { Id = Guid.NewGuid() };
                }
            }

            Install(additions, newSet);
            return new ImportResult(newSet?.Id ?? set?.Id, set is null ? remapped[categories[0].Definition.Id] : null, reused, copied);
        });

    private void Install(List<BundledCategory> categories, CategorySet? set)
    {
        if (categories.Count == 0 && set is null) return;
        var staging = DataPaths.SafePath(paths.State, $"import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        var plan = new List<(string Source, string Destination)>();
        var installed = new Stack<(string Source, string Destination)>();
        try
        {
            foreach (var item in categories)
            {
                var folder = DataPaths.SafePath(staging, $"category-{item.Definition.Id:D}");
                Directory.CreateDirectory(folder);
                foreach (var media in item.MediaFiles)
                {
                    var target = DataPaths.SafePath(folder, media.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllBytes(target, media.Value);
                }
                AtomicFiles.Write(DataPaths.SafePath(folder, "category.yaml"), ContentYaml.WriteBounded(item.Definition));
                plan.Add((folder, CategoryDestination(item.Definition.Id)));
            }
            if (set is not null)
            {
                var folder = DataPaths.SafePath(staging, "set");
                Directory.CreateDirectory(folder);
                AtomicFiles.Write(DataPaths.SafePath(folder, "set.yaml"), ContentYaml.WriteBounded(set));
                plan.Add((folder, SetDestination(set.Id)));
            }
            foreach (var item in plan)
            {
                Directory.Move(item.Source, item.Destination);
                installed.Push(item);
            }
            library.View(refresh: true);
        }
        catch
        {
            while (installed.TryPop(out var item)) Directory.Move(item.Destination, item.Source);
            library.View(refresh: true);
            throw;
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private string CategoryDestination(Guid id) => DataPaths.SafePath(paths.Categories, id.ToString("D"));
    private string SetDestination(Guid id) => DataPaths.SafePath(paths.Sets, id.ToString("D"));

    private Category FindCategory(Guid id) =>
        library.View().Categories.FirstOrDefault(entry => entry.Definition.Id == id)?.Definition
            ?? throw new ContentException("This category is unavailable.");

    private bool Matches(ContentEntry<Category> existing, BundledCategory package)
    {
        var folder = library.CategoryFolder(existing.Definition.Id);
        try
        {
            AtomicFiles.EnsureRevision(DataPaths.SafePath(folder, "category.yaml"), existing.Revision);
            if (!SameDefinition(existing.Definition, package.Definition)) return false;
            return package.MediaFiles.All(media =>
            {
                var file = Media.VerifyFile(folder, media.Key);
                using var stream = File.OpenRead(file);
                return stream.Length == media.Value.Length && System.Security.Cryptography.SHA256.HashData(stream)
                    .AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(media.Value));
            });
        }
        catch (ContentException)
        {
            return false;
        }
    }

    private static bool SameDefinition<T>(T left, T right) =>
        ContentYaml.Write(left).AsSpan().SequenceEqual(ContentYaml.Write(right));

    private void WriteCategory(ZipBudget budget, Category category, string prefix)
    {
        var folder = library.CategoryFolder(category.Id);
        budget.Add(prefix + "category.yaml", ContentYaml.Write(category));
        foreach (var reference in Media.References(category).DistinctBy(x => x.Path))
        {
            var source = Media.VerifyFile(folder, reference.Path, reference.Kind);
            using var stream = File.OpenRead(source);
            budget.Add(prefix + reference.Path, stream);
        }
    }

    private static byte[] WriteZip(Action<ZipBudget> write)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            write(new ZipBudget(zip));
        if (output.Length > MaxPackageBytes) throw new ContentException("The ZIP exceeds 128 MiB.");
        return output.ToArray();
    }

    private sealed class ZipBudget(ZipArchive zip)
    {
        private long bytes;
        private int count;

        public void Add(string name, byte[] content)
        {
            using var stream = new MemoryStream(content, writable: false);
            Add(name, stream);
        }

        public void Add(string name, Stream content)
        {
            var length = content.Length;
            bytes += length;
            if (++count > MaxEntries || bytes > MaxPackageBytes)
                throw new ContentException("The ZIP exceeds its size or entry limit.");
            using var output = zip.CreateEntry(name, CompressionLevel.Fastest).Open();
            output.Write(ReadExact(content, checked((int)length)));
        }
    }

    private Dictionary<string, byte[]> ReadEntries(ZipArchive zip)
    {
        if (zip.Entries.Count > MaxEntries) throw new ContentException("The ZIP has too many entries.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            var directory = entry.FullName.EndsWith('/');
            var name = directory ? entry.FullName[..^1] : entry.FullName;
            DataPaths.SafePath(paths.State, name);
            if (!names.Add(name) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new ContentException($"Duplicate or symbolic-link ZIP entry '{entry.FullName}'.");
            if (directory)
            {
                if (entry.Length != 0) throw new ContentException("A ZIP directory entry contains data.");
                continue;
            }
            var max = name.EndsWith(".yaml", StringComparison.Ordinal) ? ContentYaml.MaxDefinitionBytes : Media.MaxBytes;
            if (entry.Length < 0 || entry.Length > max || (total += entry.Length) > MaxPackageBytes)
                throw new ContentException("The ZIP exceeds its expanded size limit.");
            using var stream = entry.Open();
            files.Add(name, ReadExact(stream, (int)entry.Length));
        }
        return files;
    }

    private static byte[] ReadExact(Stream stream, int length)
    {
        var bytes = new byte[length];
        try { stream.ReadExactly(bytes); }
        catch (EndOfStreamException) { throw new ContentException("An entry ended before its declared size."); }
        if (stream.ReadByte() != -1) throw new ContentException("An entry exceeds its declared size.");
        return bytes;
    }

    private static BundledCategory ReadCategory(Dictionary<string, byte[]> files, string prefix)
    {
        var definition = ContentYaml.Read<Category>(Take(files, prefix + "category.yaml"));
        ContentYaml.Validate(definition);
        var media = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var reference in Media.References(definition))
        {
            if (!media.TryGetValue(reference.Path, out var bytes))
            {
                bytes = Take(files, prefix + reference.Path);
                media.Add(reference.Path, bytes);
            }
            Media.Validate(reference.Path, bytes, bytes.Length, reference.Kind);
        }
        return new BundledCategory(definition, media);
    }

    private static byte[] Take(Dictionary<string, byte[]> files, string name) =>
        files.Remove(name, out var value) ? value : throw new ContentException($"Missing ZIP entry '{name}'.");

    private static void ValidateText(Category category)
    {
        if (category.CoverImage is not null || category.Questions.Any(question => question.Type != "text" || question.Media is not null))
            throw new ContentException("JSON supports only text questions without media or a cover image. Use ZIP for this category.");
    }
}
