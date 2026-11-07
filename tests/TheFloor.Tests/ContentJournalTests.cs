using TheFloor.Content;
using Xunit;

namespace TheFloor.Tests;

public sealed class ContentJournalTests : IDisposable
{
    private readonly DataPaths paths = new(Path.Combine(Path.GetTempPath(), "thefloor-journal", Guid.NewGuid().ToString("N")));

    [Fact]
    public void RecoveryCompletesAnImportWithAnAlreadyMovedCategory()
    {
        var journal = new ContentJournal(paths);
        var stage = journal.CreateStage();
        var category = Guid.NewGuid();
        var set = Guid.NewGuid();
        var categoryStage = Directory.CreateDirectory(Path.Combine(stage, "category")).FullName;
        var setStage = Directory.CreateDirectory(Path.Combine(stage, "set")).FullName;
        var categoryBytes = ContentYaml.Write(new Category { SchemaVersion = 1, Id = category, Name = "Geography" });
        var setBytes = ContentYaml.Write(new CategorySet { SchemaVersion = 1, Id = set, Name = "Party", CategoryIds = [category] });
        AtomicFiles.Write(Path.Combine(categoryStage, "category.yaml"), categoryBytes);
        AtomicFiles.Write(Path.Combine(setStage, "set.yaml"), setBytes);
        journal.WritePlan(stage, [
            new(ContentStepKind.InstallCategory, $"library/categories/{category:D}", "category", null, AtomicFiles.Revision(categoryBytes)),
            new(ContentStepKind.InstallSet, $"sets/{set:D}", "set", null, AtomicFiles.Revision(setBytes)),
        ]);
        Directory.Move(categoryStage, Path.Combine(paths.Categories, category.ToString("D")));

        new ContentJournal(paths).Recover();
        new ContentJournal(paths).Recover();

        Assert.Equal(categoryBytes, File.ReadAllBytes(Path.Combine(paths.Categories, category.ToString("D"), "category.yaml")));
        Assert.Equal(setBytes, File.ReadAllBytes(Path.Combine(paths.Sets, set.ToString("D"), "set.yaml")));
        Assert.False(Directory.Exists(stage));
    }

    [Fact]
    public void DeletionRecoveryPreservesAnExternalEditAndResumesAfterResolution()
    {
        var journal = new ContentJournal(paths);
        var stage = journal.CreateStage();
        var category = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var beforeCategory = ContentYaml.Write(new Category { SchemaVersion = 1, Id = category, Name = "Geography" });
        var categoryPath = Path.Combine(Directory.CreateDirectory(Path.Combine(paths.Categories, category.ToString("D"))).FullName, "category.yaml");
        AtomicFiles.Write(categoryPath, beforeCategory);
        var steps = new List<ContentStep>();
        var setPaths = new List<string>();
        var setDefinitions = new List<byte[]>();
        foreach (var id in new[] { first, second })
        {
            var definition = new CategorySet { SchemaVersion = 1, Id = id, Name = "Party", CategoryIds = [category] };
            var before = ContentYaml.Write(definition);
            var after = ContentYaml.Write(definition with { CategoryIds = [] });
            var folder = Directory.CreateDirectory(Path.Combine(paths.Sets, id.ToString("D"))).FullName;
            var path = Path.Combine(folder, "set.yaml");
            var source = $"{id:D}.yaml";
            AtomicFiles.Write(path, before);
            AtomicFiles.Write(Path.Combine(stage, source), after);
            steps.Add(new(ContentStepKind.ReplaceSet, $"sets/{id:D}/set.yaml", source,
                AtomicFiles.Revision(before), AtomicFiles.Revision(after)));
            setPaths.Add(path);
            setDefinitions.Add(before);
        }
        steps.Add(new(ContentStepKind.DeleteCategory, $"library/categories/{category:D}/category.yaml", null,
            AtomicFiles.Revision(beforeCategory), null));
        journal.WritePlan(stage, steps);
        File.Copy(Path.Combine(stage, $"{first:D}.yaml"), setPaths[0], overwrite: true);
        File.WriteAllText(setPaths[1], "External edit");

        Assert.Throws<ContentConflict>(() => new ContentJournal(paths).Recover());
        Assert.Equal("External edit", File.ReadAllText(setPaths[1]));
        Assert.True(File.Exists(categoryPath));
        Assert.True(Directory.Exists(stage));

        File.WriteAllBytes(setPaths[1], setDefinitions[1]);
        new ContentJournal(paths).Recover();
        Assert.False(File.Exists(categoryPath));
        Assert.All(setPaths, path => Assert.Empty(ContentYaml.Read<CategorySet>(File.ReadAllBytes(path)).CategoryIds));
        Assert.False(Directory.Exists(stage));
    }

    [Fact]
    public void MalformedManifestDoesNotDiscardStagedData()
    {
        var journal = new ContentJournal(paths);
        var stage = journal.CreateStage();
        Directory.CreateDirectory(Path.Combine(stage, "plan.json"));
        var stagedFile = Path.Combine(stage, "staged.yaml");
        File.WriteAllText(stagedFile, "Keep pending content");

        Assert.Throws<ContentConflict>(() => journal.Recover());
        Assert.Equal("Keep pending content", File.ReadAllText(stagedFile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetRecoveryRetainsMediaAndResumesAlreadyArchivedCategories(bool externalEdit)
    {
        var journal = new ContentJournal(paths);
        var stage = journal.CreateStage();
        var backup = "backups/category-reset-" + Guid.NewGuid().ToString("N");
        var steps = new List<ContentStep>();
        var folders = new List<string>();
        var originals = new List<byte[]>();
        for (var i = 0; i < 2; i++)
        {
            var name = Guid.NewGuid().ToString("D");
            var folder = Directory.CreateDirectory(Path.Combine(paths.Categories, name)).FullName;
            var bytes = ContentYaml.Write(new Category { SchemaVersion = 1, Id = Guid.Parse(name), Name = "Geography" });
            File.WriteAllBytes(Path.Combine(folder, "category.yaml"), bytes);
            File.WriteAllText(Path.Combine(folder, "media.gif"), "GIF89atest");
            folders.Add(folder);
            originals.Add(bytes);
            steps.Add(new(ContentStepKind.ArchiveCategory, $"library/categories/{name}",
                $"{backup}/categories/{name}", AtomicFiles.Revision(bytes), null));
        }
        journal.WritePlan(stage, steps);
        var firstBackup = DataPaths.SafePath(paths.Root, steps[0].Source!);
        Directory.CreateDirectory(Path.GetDirectoryName(firstBackup)!);
        Directory.Move(folders[0], firstBackup);
        if (externalEdit)
        {
            File.WriteAllText(Path.Combine(folders[1], "category.yaml"), "External edit");
            Assert.Throws<ContentConflict>(() => new ContentJournal(paths).Recover());
            Assert.Equal("External edit", File.ReadAllText(Path.Combine(folders[1], "category.yaml")));
            File.WriteAllBytes(Path.Combine(folders[1], "category.yaml"), originals[1]);
        }
        new ContentJournal(paths).Recover();
        new ContentJournal(paths).Recover();
        Assert.False(Directory.Exists(stage));
        Assert.Empty(Directory.EnumerateDirectories(paths.Categories));
        Assert.All(steps, step => Assert.Equal("GIF89atest",
            File.ReadAllText(Path.Combine(DataPaths.SafePath(paths.Root, step.Source!), "media.gif"))));
    }

    public void Dispose() => Directory.Delete(paths.Root, recursive: true);
}
