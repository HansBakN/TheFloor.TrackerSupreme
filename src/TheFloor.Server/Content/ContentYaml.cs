using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TheFloor.Content;

public static class ContentYaml
{
    public const int MaxDefinitionBytes = 4 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .Build();
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .DisableAliases()
        .Build();

    public static T Read<T>(byte[] bytes)
    {
        if (bytes.Length > MaxDefinitionBytes) throw new ContentException("Definition exceeds 4 MiB.");
        try
        {
            var text = Utf8.GetString(bytes).TrimStart('\uFEFF');
            var parser = new Parser(new StringReader(text));
            var depth = 0;
            while (parser.MoveNext())
            {
                if (parser.Current is AnchorAlias || parser.Current is NodeEvent node &&
                    (!node.Tag.IsEmpty || !node.Anchor.IsEmpty))
                    throw new ContentException("YAML tags, anchors and aliases are unsupported.");
                if (parser.Current is SequenceStart or MappingStart && ++depth > 12)
                    throw new ContentException("YAML nesting is too deep.");
                if (parser.Current is SequenceEnd or MappingEnd) depth--;
            }
            return Deserializer.Deserialize<T>(text) ?? throw new ContentException("Definition is empty.");
        }
        catch (Exception error) when (error is YamlException or DecoderFallbackException)
        {
            throw new ContentException($"Invalid YAML. {error.Message}");
        }
    }

    public static byte[] Write<T>(T definition) => Utf8.GetBytes(Serializer.Serialize(definition));

    public static void Validate(Category category)
    {
        ValidateIdentity(category.SchemaVersion, category.Id, category.Name, category.Description);
        if (category.Questions is null || category.Questions.Length > 2_000)
            throw new ContentException("A category must contain a question list with at most 2,000 entries.");
        var ids = new HashSet<Guid>();
        foreach (var question in category.Questions)
        {
            if (question is null || question.Id == Guid.Empty || !ids.Add(question.Id))
                throw new ContentException("Each question needs a unique UUID.");
            if (question.Type is not ("text" or "image" or "audio"))
                throw new ContentException("Question type must be text, image, or audio.");
            if (string.IsNullOrWhiteSpace(question.Answer) || question.Answer.Length > 2_000)
                throw new ContentException("Each question needs an answer of up to 2,000 characters.");
            if (question.Alternatives is null || question.Alternatives.Length > 50 ||
                question.Alternatives.Any(answer => string.IsNullOrWhiteSpace(answer) || answer.Length > 2_000))
                throw new ContentException("Use at most 50 nonempty alternative answers.");
            if (question.Prompt?.Length > 5_000 || question.Notes?.Length > 5_000)
                throw new ContentException("Prompts and notes are limited to 5,000 characters.");
            if (question.Type == "text")
            {
                if (string.IsNullOrWhiteSpace(question.Prompt) || question.Media is not null)
                    throw new ContentException("Text questions need a prompt and cannot reference media.");
            }
            else if (string.IsNullOrWhiteSpace(question.Media))
                throw new ContentException($"{question.Type} questions need a media path.");
        }
    }

    public static void Validate(CategorySet set)
    {
        ValidateIdentity(set.SchemaVersion, set.Id, set.Name, set.Description);
        if (set.CategoryIds is null || set.CategoryIds.Length > 1_000 ||
            set.CategoryIds.Contains(Guid.Empty) || set.CategoryIds.Distinct().Count() != set.CategoryIds.Length)
            throw new ContentException("A set needs a list of up to 1,000 unique category UUIDs.");
    }

    private static void ValidateIdentity(int version, Guid id, string name, string? description)
    {
        if (version != 1) throw new ContentException("Only schemaVersion 1 is supported.");
        if (id == Guid.Empty) throw new ContentException("A stable UUID is required.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || description?.Length > 5_000)
            throw new ContentException("Use a name of up to 150 characters and a description of up to 5,000 characters.");
    }
}
