namespace TheFloor.Content;

public sealed record Question
{
    public Guid Id { get; init; }
    public string Type { get; init; } = "text";
    public string? Prompt { get; init; }
    public string? Media { get; init; }
    public string Answer { get; init; } = "";
    public string[] Alternatives { get; init; } = [];
    public string? Notes { get; init; }
    public bool Enabled { get; init; } = true;
}

public sealed record Category
{
    public int SchemaVersion { get; init; }
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? CoverImage { get; init; }
    public Question[] Questions { get; init; } = [];
}

public sealed record CategorySet
{
    public int SchemaVersion { get; init; }
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public Guid[] CategoryIds { get; init; } = [];
}

public sealed record ContentEntry<T>(T Definition, string Revision);
public sealed record LibraryView(ContentEntry<Category>[] Categories, ContentEntry<CategorySet>[] Sets,
    Guid? ActiveSetId, string[] Errors, string DataDirectory);
