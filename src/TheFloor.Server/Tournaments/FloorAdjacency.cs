using TheFloor.Content;

namespace TheFloor.Tournaments;

public static class FloorAdjacency
{
    public static (int X, int Y)[] Neighbors(int x, int y, int width, int height)
    {
        if (width < 1 || height < 1 || x < 0 || x >= width || y < 0 || y >= height)
            throw new ContentException("Invalid board dimensions or territory coordinates.");
        (int X, int Y)[] candidates =
        [((x + 1) % width, y), ((x - 1 + width) % width, y), (x, (y + 1) % height), (x, (y - 1 + height) % height)];
        return candidates.Where(p => p != (x, y)).Distinct().ToArray();
    }

    public static Guid[] Opponents(TournamentState state, Guid playerId)
    {
        var active = state.Players.Where(p => p.EliminationOrder is null && p.Id != playerId).Select(p => p.Id).ToHashSet();
        var neighbors = state.Floor.Where(t => t.OwnerId == playerId)
            .SelectMany(t => Neighbors(t.X, t.Y, state.Width, state.Height)).ToHashSet();
        return state.Floor.Where(t => neighbors.Contains((t.X, t.Y)) && active.Contains(t.OwnerId))
            .Select(t => t.OwnerId).Distinct().ToArray();
    }
}
