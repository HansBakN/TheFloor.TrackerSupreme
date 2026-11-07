using System.Text;

namespace TheFloor.Content;

public static class Media
{
    public const int MaxBytes = 24 * 1024 * 1024;
    public const int MaxVideoBytes = 8 * 1024 * 1024;
    public const double MaxVideoSeconds = 5;
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
        [".webp"] = "image/webp", [".gif"] = "image/gif",
        [".mp3"] = "audio/mpeg", [".wav"] = "audio/wav", [".ogg"] = "audio/ogg",
        [".mp4"] = "video/mp4", [".webm"] = "video/webm",
    };

    public static string ContentType(string fileName) =>
        Types.TryGetValue(Path.GetExtension(fileName), out var type) ? type
            : throw new ContentException("Unsupported media format.");

    public static void Validate(string fileName, ReadOnlySpan<byte> header, long length, string? expectedType = null)
    {
        if (length <= 0 || length > MaxBytes) throw new ContentException("Media must be between 1 byte and 24 MiB.");
        var type = ContentType(fileName);
        var isVideo = type.StartsWith("video/", StringComparison.Ordinal);
        if (isVideo && length > MaxVideoBytes) throw new ContentException("Video clips must be at most 8 MiB.");
        expectedType = expectedType?.ToLowerInvariant() switch
        {
            "audio/x-wav" or "audio/wave" => "audio/wav",
            "audio/mp3" => "audio/mpeg",
            "application/ogg" => "audio/ogg",
            var mime => mime,
        };
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
            "video/mp4" => header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8),
            "video/webm" => header.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }),
            _ => false,
        };
        if (!valid) throw new ContentException("Media signature does not match its extension.");
        // The duration lives inside the container, so it is only checkable when the whole file is supplied.
        if (isVideo && header.Length == length)
        {
            var seconds = type == "video/mp4" ? VideoDuration.Mp4(header) : VideoDuration.WebM(header);
            if (seconds is not { } known || known <= 0)
                throw new ContentException("The video duration could not be read. Re-encode it as a standard MP4 or WebM clip.");
            if (known > MaxVideoSeconds + 0.1)
                throw new ContentException($"Video clips must be at most {MaxVideoSeconds:0} seconds long (this one is {known:0.0}).");
        }
    }

    public static string VerifyFile(string directory, string relative, string? expectedType = null)
    {
        var path = DataPaths.SafePath(directory, relative);
        if (!File.Exists(path)) throw new ContentException($"Missing media '{relative}'.");
        if (Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".webm")
        {
            if (new FileInfo(path).Length > MaxVideoBytes) throw new ContentException("Video clips must be at most 8 MiB.");
            var bytes = File.ReadAllBytes(path);
            Validate(relative, bytes, bytes.Length, expectedType);
            return path;
        }
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
