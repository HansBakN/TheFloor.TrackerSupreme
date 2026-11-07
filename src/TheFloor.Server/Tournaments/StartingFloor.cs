using TheFloor.Content;

namespace TheFloor.Tournaments;

public sealed record ExtraTerritory(int X, int Y, Guid OwnerId, Guid[] EligibleOwnerIds);
public sealed record FloorAllocation(Territory[] Floor, Territory[] StartingTerritories, ExtraTerritory[] ExtraAssignments);

public static class StartingFloor
{
    public const int MaxPlayers = 256;
    public const int MaxDimension = 64;

    public static void ValidateDimensions(int width, int height, int players)
    {
        if (players is < 2 or > MaxPlayers || width is < 1 or > MaxDimension || height is < 1 or > MaxDimension ||
            width * height < players)
            throw new ContentException($"Choose 2–{MaxPlayers} players and 1–{MaxDimension} rows/columns, with at least one cell per player.");
    }

    public static FloorAllocation Allocate(int width, int height, Guid[] players, Random random)
    {
        ValidateDimensions(width, height, players.Length);
        if (players.Any(id => id == Guid.Empty) || players.Distinct().Count() != players.Length)
            throw new ContentException("Starting territories require distinct participant IDs.");
        var cells = Enumerable.Range(0, width * height).Select(i => (X: i % width, Y: i / width)).ToArray();
        random.Shuffle(cells);
        var owners = new Dictionary<(int X, int Y), Guid>();
        var starts = players.Select((id, i) => new Territory(cells[i].X, cells[i].Y, id)).ToArray();
        foreach (var start in starts) owners.Add((start.X, start.Y), start.OwnerId);
        var frontier = starts.SelectMany(t => FloorAdjacency.Neighbors(t.X, t.Y, width, height))
            .Where(p => !owners.ContainsKey(p)).ToHashSet();
        var extras = new List<ExtraTerritory>();
        while (frontier.Count > 0)
        {
            var cell = frontier.ElementAt(random.Next(frontier.Count));
            var eligible = FloorAdjacency.Neighbors(cell.X, cell.Y, width, height)
                .Where(owners.ContainsKey).Select(p => owners[p]).Distinct().ToArray();
            var owner = eligible[random.Next(eligible.Length)];
            owners.Add(cell, owner);
            extras.Add(new ExtraTerritory(cell.X, cell.Y, owner, eligible));
            frontier.Remove(cell);
            foreach (var neighbor in FloorAdjacency.Neighbors(cell.X, cell.Y, width, height))
                if (!owners.ContainsKey(neighbor)) frontier.Add(neighbor);
        }
        var floor = owners.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X)
            .Select(p => new Territory(p.Key.X, p.Key.Y, p.Value)).ToArray();
        if (floor.Length != width * height) throw new ContentException("The starting floor could not be filled.");
        return new FloorAllocation(floor, starts, extras.ToArray());
    }
}
