using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using TheFloor.Content;
using Xunit;

namespace TheFloor.Tests;

public sealed class PackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "thefloor-packages", Guid.NewGuid().ToString("N"));

    [Fact]
    public void BundledDemoSetsAreValidIdempotentAndShareCategories()
    {
        var (library, packages) = Create("demo");
        var directory = Path.Combine(AppContext.BaseDirectory, "demo");
        Assert.Equal(2, DemoContent.Load(directory, packages));
        Assert.Equal(2, DemoContent.Load(directory, packages));
        var view = library.View();
        Assert.Empty(view.Errors);
        Assert.Equal(4, view.Categories.Length);
        Assert.Equal(2, view.Sets.Length);
        Assert.Equal(2, view.Sets[0].Definition.CategoryIds.Intersect(view.Sets[1].Definition.CategoryIds).Count());
        Assert.Contains(view.Categories, c => c.Definition.Name == "Demo Image Recognition");
    }

    [Fact]
    public void SetZipIsPortableAndIdenticalCategoriesAreReused()
    {
        var (source, export) = Create("source");
        var (destination, import) = Create("destination");
        var category = WithMedia(source);
        var set = source.SaveSet(NewSet(category.Id), null).Definition;

        var package = export.ExportSet(set.Id);
        var first = import.ImportZip(package, asSet: true, copyConflicts: false);
        Assert.Equal(set.Id, first.SetId);
        var imported = Assert.Single(destination.View().Categories).Definition;
        Assert.Equal(category.Id, imported.Id);
        Assert.Equal(category.Questions[0].Answer, imported.Questions[0].Answer);
        Assert.Equal("GIF89atest"u8.ToArray(), File.ReadAllBytes(destination.MediaFile(category.Id, category.Questions[0].Media!)));

        var second = import.ImportZip(package, asSet: true, copyConflicts: false);
        Assert.Equal(1, second.ReusedCategories);
        Assert.Single(destination.View().Categories);
        Assert.Single(destination.View().Sets);
    }

    [Fact]
    public void MediaCollisionRequiresConsentAndRemapsSetReferences()
    {
        var (source, export) = Create("source");
        var (destination, import) = Create("destination");
        var category = WithMedia(source);
        var unrelated = destination.SaveCategory(NewCategory("Other category"), null).Definition;
        var set = source.SaveSet(NewSet(category.Id), null).Definition;
        var package = export.ExportSet(set.Id);
        import.ImportZip(package, asSet: true, copyConflicts: false);

        var media = destination.MediaFile(category.Id, category.Questions[0].Media!);
        File.WriteAllBytes(media, "GIF89achanged"u8.ToArray());
        Assert.Throws<ContentConflict>(() => import.ImportZip(package, asSet: true, copyConflicts: false));
        Assert.Equal(2, destination.View().Categories.Length);
        Assert.Single(destination.View().Sets);

        var accepted = import.ImportZip(package, asSet: true, copyConflicts: true);
        Assert.Equal(1, accepted.CopiedCategories);
        Assert.NotEqual(set.Id, accepted.SetId);
        var state = destination.View();
        var copy = Assert.Single(state.Sets, entry => entry.Definition.Id == accepted.SetId).Definition;
        var copiedId = Assert.Single(copy.CategoryIds);
        Assert.NotEqual(category.Id, copiedId);
        Assert.Contains(state.Categories, entry => entry.Definition.Id == unrelated.Id);
        Assert.Equal("GIF89achanged"u8.ToArray(), File.ReadAllBytes(media));
        Assert.Equal("GIF89atest"u8.ToArray(), File.ReadAllBytes(destination.MediaFile(copiedId, category.Questions[0].Media!)));
    }

    [Fact]
    public void AmbiguousIdsOnDiskCannotBeSilentlyReusedOrAdded()
    {
        var (source, export) = Create("source");
        var (destination, import) = Create("destination");
        var category = source.SaveCategory(NewCategory("Shared"), null).Definition;
        var set = source.SaveSet(NewSet(category.Id), null).Definition;
        foreach (var name in new[] { "first", "second" })
        {
            var categoryFolder = Directory.CreateDirectory(Path.Combine(root, "destination", "library", "categories", name));
            var setFolder = Directory.CreateDirectory(Path.Combine(root, "destination", "sets", name));
            File.WriteAllBytes(Path.Combine(categoryFolder.FullName, "category.yaml"), ContentYaml.Write(category));
            File.WriteAllBytes(Path.Combine(setFolder.FullName, "set.yaml"), ContentYaml.Write(set));
        }

        var package = export.ExportSet(set.Id);
        Assert.Throws<ContentConflict>(() => import.ImportZip(package, asSet: true, copyConflicts: false));
        Assert.Equal(2, destination.View().Errors.Length);
        var copy = import.ImportZip(package, asSet: true, copyConflicts: true);
        Assert.NotEqual(set.Id, copy.SetId);
        Assert.NotEqual(category.Id, Assert.Single(destination.View().Categories).Definition.Id);
        Assert.Equal(1, copy.CopiedCategories);
    }

    [Fact]
    public void CachedSetCannotBeReusedWhenTheDefinitionOnDiskIsInvalid()
    {
        var (library, packages) = Create("library");
        var category = library.SaveCategory(NewCategory("Shared"), null).Definition;
        var set = library.SaveSet(NewSet(category.Id), null).Definition;
        var package = packages.ExportSet(set.Id);
        var setFile = Path.Combine(root, "library", "sets", set.Id.ToString("D"), "set.yaml");
        File.WriteAllText(setFile, "name: [broken");

        Assert.Throws<ContentConflict>(() => packages.ImportZip(package, asSet: true, copyConflicts: false));
        var copy = packages.ImportZip(package, asSet: true, copyConflicts: true);
        Assert.NotEqual(set.Id, copy.SetId);
        Assert.Equal(1, copy.ReusedCategories);
        Assert.Equal("name: [broken", File.ReadAllText(setFile));
    }

    [Fact]
    public void CompactJsonIsRejectedWhenItsStoredYamlWouldExceedTheLimit()
    {
        var (destination, import) = Create("destination");
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, id = Guid.NewGuid(), name = "Large category",
            questions = Enumerable.Range(0, 1900).Select(_ => new
            {
                id = Guid.NewGuid(), type = "text", prompt = new string('q', 100), answer = new string('a', 2000),
            }),
        });
        Assert.True(json.Length < ContentYaml.MaxDefinitionBytes);
        Assert.Throws<ContentException>(() => import.ImportJson(json, copyConflicts: false));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(root, "destination", "library", "categories")));
        Assert.Empty(destination.View(refresh: true).Categories);
    }

    [Fact]
    public void StandaloneZipAndTextJsonPreserveQuestionIdentity()
    {
        var (source, export) = Create("source");
        var (destination, import) = Create("destination");
        var image = WithMedia(source);
        var text = source.SaveCategory(NewCategory("Text"), null).Definition;

        Assert.Equal(image.Id, import.ImportZip(export.ExportCategory(image.Id), asSet: false, copyConflicts: false).CategoryId);
        Assert.Equal(text.Id, import.ImportJson(export.ExportJson(text.Id), copyConflicts: false).CategoryId);
        var imported = Assert.Single(destination.View().Categories, entry => entry.Definition.Id == text.Id).Definition;
        Assert.Equal(text.Questions[0].Id, imported.Questions[0].Id);
        Assert.Throws<ContentException>(() => export.ExportJson(image.Id));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("C:/outside.png")]
    [InlineData("/outside.png")]
    [InlineData("a/../../outside.png")]
    [InlineData("a\\..\\outside.png")]
    public void ZipTraversalIsRejectedWithoutInstallingContent(string unsafeName)
    {
        var (library, import) = Create("destination");
        var package = Zip(zip => zip.CreateEntry(unsafeName));
        Assert.Throws<ContentException>(() => import.ImportZip(package, asSet: false, copyConflicts: false));
        Assert.Empty(library.View().Categories);
        Assert.Empty(library.View().Sets);
    }

    [Fact]
    public void ZipRejectsDuplicateSymlinkAndOversizedEntries()
    {
        var (library, import) = Create("destination");
        foreach (var package in new[]
        {
            Zip(zip => { zip.CreateEntry("category.yaml"); zip.CreateEntry("Category.yaml"); }),
            Zip(zip => zip.CreateEntry("media/link.png").ExternalAttributes = unchecked((int)0xA0000000)),
            Zip(zip =>
            {
                using var entry = zip.CreateEntry("media/large.png").Open();
                var buffer = new byte[1024 * 1024];
                for (var i = 0; i < 25; i++) entry.Write(buffer);
            }),
        })
            Assert.Throws<ContentException>(() => import.ImportZip(package, asSet: false, copyConflicts: false));
        Assert.Empty(library.View().Categories);
    }

    private (ContentLibrary Library, ContentPackages Packages) Create(string folder)
    {
        var paths = new DataPaths(Path.Combine(root, folder));
        var options = new DbContextOptionsBuilder<AppDb>();
        AppDb.Configure(options, paths);
        var factory = new TestFactory(options.Options);
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
        var library = new ContentLibrary(paths, factory);
        return (library, new ContentPackages(library, paths));
    }

    private static Category WithMedia(ContentLibrary source)
    {
        var category = source.SaveCategory(NewCategory("Colors"), null);
        var path = source.Upload(category.Definition.Id, "color.gif", "image/gif", "GIF89atest"u8.ToArray());
        var question = category.Definition.Questions[0] with { Type = "image", Media = path };
        return source.SaveCategory(category.Definition with { Questions = [question] }, category.Revision).Definition;
    }

    private static Category NewCategory(string name) => new()
    {
        SchemaVersion = 1, Id = Guid.NewGuid(), Name = name,
        Questions = [new Question { Id = Guid.NewGuid(), Prompt = "What color?", Answer = "Teal" }],
    };

    private static CategorySet NewSet(Guid id) =>
        new() { SchemaVersion = 1, Id = Guid.NewGuid(), Name = "Event", CategoryIds = [id] };

    private static byte[] Zip(Action<ZipArchive> build)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true)) build(archive);
        return output.ToArray();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }

    private sealed class TestFactory(DbContextOptions<AppDb> options) : IDbContextFactory<AppDb>
    {
        public AppDb CreateDbContext() => new(options);
    }
}
