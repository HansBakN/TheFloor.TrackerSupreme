namespace TheFloor.Content;

public sealed class DataPaths
{
    public string Root { get; }
    public string Categories => SafePath(Root, "library/categories");
    public string Sets => SafePath(Root, "sets");
    public string State => SafePath(Root, "state");

    public DataPaths(string root, string? webRoot = null)
    {
        Root = CanonicalDirectory(Path.GetFullPath(root));
        if (webRoot is not null)
        {
            var publicRoot = CanonicalDirectory(Path.GetFullPath(webRoot));
            if (Contains(publicRoot, Root) || Contains(Root, publicRoot))
                throw new InvalidOperationException("The data directory must be separate from the public web root.");
        }
        for (var parent = new DirectoryInfo(Root); parent is not null; parent = parent.Parent)
        {
            var marker = Path.Combine(parent.FullName, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
                throw new InvalidOperationException("The data directory must be outside the Git checkout.");
        }

        try
        {
            Directory.CreateDirectory(Root);
            foreach (var relative in new[] { "library/categories", "sets", "state" })
                Directory.CreateDirectory(SafePath(Root, relative));
            var probe = Path.Combine(State, $".write-check-{Guid.NewGuid():N}");
            using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Cannot initialize data directory '{Root}'. Check its availability and permissions.", error);
        }
    }

    public static DataPaths FromEnvironment(string webRoot)
    {
        var configured = Environment.GetEnvironmentVariable("THEFLOOR_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
            return new DataPaths(configured, webRoot);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            throw new InvalidOperationException("Set THEFLOOR_DATA_DIR to a writable external directory.");
        var basePath = OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Application Support")
            : OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg &&
                    Path.IsPathFullyQualified(xdg) ? xdg : Path.Combine(home, ".local", "share");
        return new DataPaths(Path.Combine(basePath, "TheFloor.TrackerSupreme"), webRoot);
    }

    private static bool Contains(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string CanonicalDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        if (info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
            return CanonicalDirectory(target.FullName);
        return info.Parent is { } parent
            ? Path.Combine(CanonicalDirectory(parent.FullName), info.Name)
            : info.FullName;
    }

    public static string SafePath(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Length > 512 || relative.Contains('\\'))
            throw new ContentException("Use a safe relative path with forward slashes.");

        var current = directory;
        if (new DirectoryInfo(current).LinkTarget is not null)
            throw new ContentException("Symbolic links are not supported in content directories.");
        foreach (var part in relative.Split('/'))
        {
            if (part is "" or "." or ".." || part.Length > 128 || part.EndsWith('.') ||
                !part.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') || IsDeviceName(part))
                throw new ContentException($"Unsafe relative path '{relative}'.");
            current = Path.Combine(current, part);
            if (new FileInfo(current).LinkTarget is not null)
                throw new ContentException("Symbolic links are not supported in content directories.");
        }
        return current;
    }

    private static bool IsDeviceName(string segment)
    {
        var stem = segment.Split('.', 2)[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" ||
            stem.Length == 4 && char.IsAsciiDigit(stem[3]) && (stem.StartsWith("COM", StringComparison.Ordinal) ||
                stem.StartsWith("LPT", StringComparison.Ordinal));
    }
}

public class ContentException(string message) : Exception(message);
public sealed class ContentConflict(string message) : ContentException(message);
