using TheFloor.Content;
using TheFloor.Tournaments;
using Xunit;

namespace TheFloor.Tests;

public sealed class StartingFloorTests
{
    [Theory]
    [InlineData(3, 3, 7)]
    [InlineData(4, 4, 7)]
    [InlineData(1, 10, 3)]
    [InlineData(10, 1, 2)]
    [InlineData(4, 4, 16)]
    [InlineData(3, 2, 6)]
    public void EveryExtraCellIsAssignedToADistinctCurrentlyAdjacentOwner(int width, int height, int count)
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var players = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
            var allocation = StartingFloor.Allocate(width, height, players, new Random(seed));
            Assert.Equal(count, allocation.StartingTerritories.Length);
            Assert.Equal(count, allocation.StartingTerritories.Select(t => t.OwnerId).Distinct().Count());
            var owners = allocation.StartingTerritories.ToDictionary(t => (t.X, t.Y), t => t.OwnerId);
            foreach (var extra in allocation.ExtraAssignments)
            {
                Assert.False(owners.ContainsKey((extra.X, extra.Y)));
                var eligible = FloorAdjacency.Neighbors(extra.X, extra.Y, width, height)
                    .Where(owners.ContainsKey).Select(p => owners[p]).Distinct().ToArray();
                Assert.Equal(eligible.Order(), extra.EligibleOwnerIds.Order());
                Assert.Contains(extra.OwnerId, eligible);
                owners.Add((extra.X, extra.Y), extra.OwnerId);
            }
            Assert.Equal(width * height - count, allocation.ExtraAssignments.Length);
            Assert.Equal(width * height, owners.Count);
            Assert.Equal(width * height, allocation.Floor.Select(t => (t.X, t.Y)).Distinct().Count());
            Assert.All(allocation.Floor, t => Assert.Equal(owners[(t.X, t.Y)], t.OwnerId));
        }
    }

    [Fact]
    public void GrowthCanReachCellsNotAdjacentToAnyOriginalStartingTerritory()
    {
        var allocation = StartingFloor.Allocate(12, 12, [Guid.NewGuid(), Guid.NewGuid()], new Random(10));
        var initial = allocation.StartingTerritories.SelectMany(t => FloorAdjacency.Neighbors(t.X, t.Y, 12, 12)).ToHashSet();
        Assert.Contains(allocation.ExtraAssignments, t => !initial.Contains((t.X, t.Y)));
        Assert.Equal(144, allocation.Floor.Length);
    }

    [Theory]
    [InlineData(0, 3, 2)]
    [InlineData(3, -1, 2)]
    [InlineData(2, 2, 5)]
    [InlineData(65, 2, 3)]
    [InlineData(3, 3, 1)]
    public void InvalidDimensionsAndInsufficientCapacityAreRejected(int width, int height, int count) =>
        Assert.Throws<ContentException>(() => StartingFloor.Allocate(width, height,
            Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray(), new Random(1)));

    [Theory]
    [InlineData(2, 7)]
    [InlineData(3, 10)]
    [InlineData(6, 19)]
    [InlineData(7, 22)]
    [InlineData(16, 49)]
    public void CapacityReservesFiveAvailableCategoriesForTheFinalDecidingDraw(int players, int minimum) =>
        Assert.Equal(minimum, TournamentRules.MinimumCategories(players));
}
