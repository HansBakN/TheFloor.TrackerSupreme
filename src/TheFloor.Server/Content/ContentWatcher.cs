using System.Threading.Channels;

namespace TheFloor.Content;

public sealed class ContentWatcher(DataPaths paths, ContentLibrary library, ILogger<ContentWatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
        });
        using var categories = Watch(paths.Categories);
        using var sets = Watch(paths.Sets);
        library.View(refresh: true);
        await foreach (var change in changes.Reader.ReadAllAsync(stoppingToken))
        {
            await Task.Delay(200, stoppingToken);
            changes.Reader.TryRead(out _);
            try
            {
                library.View(refresh: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ContentException)
            {
                logger.LogWarning(error, "Could not refresh the content library. Check the data directory and retry.");
            }
        }

        FileSystemWatcher Watch(string path)
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                    NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            watcher.Changed += Queue;
            watcher.Created += Queue;
            watcher.Deleted += Queue;
            watcher.Renamed += Queue;
            watcher.Error += (_, _) => changes.Writer.TryWrite(true);
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        void Queue(object sender, FileSystemEventArgs args) => changes.Writer.TryWrite(true);
    }
}
