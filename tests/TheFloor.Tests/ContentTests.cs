using Microsoft.EntityFrameworkCore;
using TheFloor.Content;
using Xunit;

namespace TheFloor.Tests;

public sealed class ContentTests : IDisposable
{
    private readonly DataPaths paths = new(Path.Combine(Path.GetTempPath(), "thefloor-tests", Guid.NewGuid().ToString("N")));
    private readonly ContentLibrary library;

    public ContentTests()
    {
        var options = new DbContextOptionsBuilder<AppDb>();
        AppDb.Configure(options, paths);
        var factory = new TestFactory(options.Options);
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
        library = new ContentLibrary(paths, factory);
    }

    [Fact]
    public void SharedCategoriesAreReferencedAndCopiesAreIndependent()
    {
        var category = library.SaveCategory(Category(), null);
        var first = library.SaveSet(Set(category.Definition.Id), null);
        var second = library.SaveSet(Set(category.Definition.Id), null);
        library.Activate(second.Definition.Id);

        var updated = library.SaveCategory(category.Definition with { Name = "Renamed" }, category.Revision);
        var copy = library.DuplicateCategory(updated.Definition.Id);
        var independent = library.DuplicateSet(second.Definition.Id, copyCategories: true);
        var shared = library.DuplicateSet(second.Definition.Id, copyCategories: false);
        library.SaveSet(first.Definition with { CategoryIds = [] }, first.Revision);

        var state = library.View();
        Assert.Equal(second.Definition.Id, state.ActiveSetId);
        Assert.Equal("Renamed", state.Categories.Single(c => c.Definition.Id == category.Definition.Id).Definition.Name);
        Assert.NotEqual(category.Definition.Id, copy.Definition.Id);
        Assert.NotEqual(category.Definition.Questions[0].Id, copy.Definition.Questions[0].Id);
        Assert.NotEqual(category.Definition.Id, Assert.Single(independent.Definition.CategoryIds));
        Assert.Equal(category.Definition.Id, Assert.Single(shared.Definition.CategoryIds));
        Assert.Empty(state.Sets.Single(s => s.Definition.Id == first.Definition.Id).Definition.CategoryIds);
        Assert.Equal(category.Definition.Id, Assert.Single(state.Sets.Single(s => s.Definition.Id == second.Definition.Id).Definition.CategoryIds));
    }

    [Fact]
    public void InvalidExternalYamlRetainsLastValidContentAndPreventsStaleWrites()
    {
        var original = library.SaveCategory(Category(), null);
        var definition = Path.Combine(library.CategoryFolder(original.Definition.Id), "category.yaml");
        File.WriteAllText(definition, "name: [bad");
        var invalid = library.View(refresh: true);
        Assert.NotEmpty(invalid.Errors);
        Assert.Equal(original.Revision, Assert.Single(invalid.Categories).Revision);
        Assert.Throws<ContentConflict>(() => library.SaveCategory(original.Definition with { Name = "UI edit" }, original.Revision));
        Assert.Equal("name: [bad", File.ReadAllText(definition));

        File.WriteAllBytes(definition, ContentYaml.Write(original.Definition with { Name = "External edit" }));
        var corrected = library.View(refresh: true);
        Assert.Empty(corrected.Errors);
        Assert.Equal("External edit", Assert.Single(corrected.Categories).Definition.Name);
        Assert.Throws<ContentConflict>(() => library.SaveCategory(original.Definition, original.Revision));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(definition)!, "*.tmp"));
    }

    [Fact]
    public void FolderRenamesKeepStableIdsAndSetReferences()
    {
        var category = library.SaveCategory(Category(), null);
        var set = library.SaveSet(Set(category.Definition.Id), null);
        Directory.Move(library.CategoryFolder(category.Definition.Id), Path.Combine(paths.Categories, "new-folder"));
        var state = library.View(refresh: true);
        Assert.Empty(state.Errors);
        Assert.Equal(category.Definition.Id, Assert.Single(state.Categories).Definition.Id);
        Assert.Equal(category.Definition.Id, Assert.Single(set.Definition.CategoryIds));
    }

    [Fact]
    public void DeletingSharedCategoryRequiresConfirmationOfCurrentReferences()
    {
        var category = library.SaveCategory(Category(), null);
        var set = library.SaveSet(Set(category.Definition.Id), null);
        Assert.Throws<ContentConflict>(() => library.DeleteCategory(category.Definition.Id, category.Revision, []));
        library.DeleteCategory(category.Definition.Id, category.Revision, [set.Definition.Id]);
        var state = library.View();
        Assert.Empty(state.Categories);
        Assert.Empty(Assert.Single(state.Sets).Definition.CategoryIds);
    }

    [Fact]
    public void DetectsMissingMediaAndDanglingReferences()
    {
        var category = library.SaveCategory(Category(), null);
        var bad = category.Definition with
        {
            Questions = [category.Definition.Questions[0] with { Type = "image", Media = "media/missing.png" }],
        };
        Assert.Throws<ContentException>(() => library.SaveCategory(bad, category.Revision));
        var set = library.SaveSet(Set(category.Definition.Id), null);
        File.Delete(Path.Combine(library.CategoryFolder(category.Definition.Id), "category.yaml"));
        var state = library.View(refresh: true);
        Assert.Empty(state.Categories);
        Assert.Equal(set.Definition.Id, Assert.Single(state.Sets).Definition.Id);
        Assert.Contains("missing category", Assert.Single(state.Errors));
    }

    [Fact]
    public void UploadAndDuplicateUseIndependentValidatedMedia()
    {
        var original = library.SaveCategory(Category(), null);
        var image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jAZkAAAAASUVORK5CYII=");
        var relative = library.Upload(original.Definition.Id, "photo.png", "image/png", image);
        library.SaveCategory(original.Definition with { CoverImage = relative }, original.Revision);
        var copy = library.DuplicateCategory(original.Definition.Id);
        File.Delete(library.MediaFile(original.Definition.Id, relative));
        Assert.Equal(image, File.ReadAllBytes(library.MediaFile(copy.Definition.Id, relative)));
        Assert.Throws<ContentException>(() => library.Upload(copy.Definition.Id, "bad.png", "image/png", [1, 2, 3]));
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("/absolute.png")]
    [InlineData("a\\..\\escape.png")]
    [InlineData("file:stream.png")]
    public void UnsafePathsAreRejected(string relative) =>
        Assert.Throws<ContentException>(() => DataPaths.SafePath(paths.Root, relative));

    [Theory]
    [InlineData("schemaVersion: 1\nschemaVersion: 1")]
    [InlineData("name: !arbitrary unsafe")]
    [InlineData("name: &name x\ndescription: *name")]
    public void UnsafeYamlIsRejected(string yaml) =>
        Assert.Throws<ContentException>(() => ContentYaml.Read<Category>(System.Text.Encoding.UTF8.GetBytes(yaml)));

    private static Category Category() => new()
    {
        SchemaVersion = 1, Id = Guid.NewGuid(), Name = "Geography",
        Questions = [new Question { Id = Guid.NewGuid(), Prompt = "Capital of Denmark?", Answer = "Copenhagen" }],
    };

    private static CategorySet Set(Guid id) => new()
    {
        SchemaVersion = 1, Id = Guid.NewGuid(), Name = "Birthday", CategoryIds = [id],
    };

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(paths.Root, recursive: true);
    }

    private sealed class TestFactory(DbContextOptions<AppDb> options) : IDbContextFactory<AppDb>
    {
        public AppDb CreateDbContext() => new(options);
    }
}
