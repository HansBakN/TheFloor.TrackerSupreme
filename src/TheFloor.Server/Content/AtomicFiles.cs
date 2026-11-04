using System.Security.Cryptography;

namespace TheFloor.Content;

public static class AtomicFiles
{
    public static byte[] ReadDefinition(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > ContentYaml.MaxDefinitionBytes) throw new ContentException("Definition exceeds 4 MiB.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new ContentException("Definition changed while it was being read.");
        return bytes;
    }

    public static string Revision(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static void Write(string path, byte[] bytes, string? expected = null)
    {
        var folder = Path.GetDirectoryName(path)!;
        var temp = Path.Combine(folder, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (expected is not null) EnsureRevision(path, expected);
            File.Move(temp, path, overwrite: expected is not null);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public static void EnsureRevision(string path, string revision)
    {
        if (!File.Exists(path) || Revision(ReadDefinition(path)) != revision)
            throw new ContentConflict("This definition changed on disk. Reload it and apply your edits again.");
    }
}
