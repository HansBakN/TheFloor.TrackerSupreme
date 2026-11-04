using System.Text;

namespace TheFloor.Content;

public static class Media
{
    public const int MaxBytes = 24 * 1024 * 1024;
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
        [".webp"] = "image/webp", [".gif"] = "image/gif",
        [".mp3"] = "audio/mpeg", [".wav"] = "audio/wav", [".ogg"] = "audio/ogg",
    };

    public static string ContentType(string fileName) =>
        Types.TryGetValue(Path.GetExtension(fileName), out var type) ? type
            : throw new ContentException("Unsupported media format.");

    public static void Validate(string fileName, ReadOnlySpan<byte> header, long length, string? expectedType = null)
    {
        if (length <= 0 || length > MaxBytes) throw new ContentException("Media must be between 1 byte and 24 MiB.");
        var type = ContentType(fileName);
        if (expectedType is not null && type != expectedType && !type.StartsWith(expectedType + "/", StringComparison.Ordinal))
            throw new ContentException("Media format does not match the question or upload MIME type.");
        var valid = type switch
        {
            "image/jpeg" => header.StartsWith(new byte[] { 255, 216, 255 }),
            "image/png" => header.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => Riff(header, "WEBP"),
            "image/gif" => header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8),
            "audio/mpeg" => header.StartsWith("ID3"u8) ||
                (header.Length >= 2 && header[0] == 0xff && (header[1] & 0xe0) == 0xe0),
            "audio/wav" => Riff(header, "WAVE"),
            "audio/ogg" => header.StartsWith("OggS"u8),
            _ => false,
        };
        if (!valid) throw new ContentException("Media signature does not match its extension.");
    }

    public static string VerifyFile(string directory, string relative, string? expectedType = null)
    {
        var path = DataPaths.SafePath(directory, relative);
        if (!File.Exists(path)) throw new ContentException($"Missing media '{relative}'.");
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[16];
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        Validate(relative, header[..read], stream.Length, expectedType);
        return path;
    }

    public static IEnumerable<(string Path, string Kind)> References(Category category)
    {
        if (category.CoverImage is { } cover) yield return (cover, "image");
        foreach (var question in category.Questions)
            if (question.Media is { } media) yield return (media, question.Type);
    }

    private static bool Riff(ReadOnlySpan<byte> header, string kind) =>
        header.Length >= 12 && header.StartsWith("RIFF"u8) && header[8..12].SequenceEqual(Encoding.ASCII.GetBytes(kind));
}
