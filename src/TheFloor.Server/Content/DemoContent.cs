using System.IO.Compression;

namespace TheFloor.Content;

public static class DemoContent
{
    public static int Load(string directory, ContentPackages packages)
    {
        var sets = Directory.GetFiles(Path.Combine(directory, "sets"), "*.yaml");
        if (sets.Length == 0) throw new ContentException("The demo files are missing from this installation.");
        foreach (var path in sets.Order(StringComparer.Ordinal))
        {
            var setBytes = AtomicFiles.ReadDefinition(path);
            var set = ContentYaml.Read<CategorySet>(setBytes);
            ContentYaml.Validate(set);
            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var definition = zip.CreateEntry("set.yaml").Open()) definition.Write(setBytes);
                foreach (var id in set.CategoryIds)
                {
                    var prefix = $"categories/{id:D}";
                    var categoryDirectory = DataPaths.SafePath(directory, prefix);
                    var file = DataPaths.SafePath(categoryDirectory, "category.yaml");
                    var category = ContentYaml.Read<Category>(AtomicFiles.ReadDefinition(file));
                    ContentYaml.Validate(category);
                    zip.CreateEntryFromFile(file, $"{prefix}/category.yaml");
                    foreach (var media in Media.References(category).DistinctBy(reference => reference.Path))
                    {
                        var source = Media.VerifyFile(categoryDirectory, media.Path, media.Kind);
                        zip.CreateEntryFromFile(source, $"{prefix}/{media.Path}");
                    }
                }
            }
            packages.ImportZip(output.ToArray(), asSet: true, copyConflicts: false);
        }
        return sets.Length;
    }
}
