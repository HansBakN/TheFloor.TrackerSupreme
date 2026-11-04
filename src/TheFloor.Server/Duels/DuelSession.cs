using System.Text.Json.Serialization;
using TheFloor.Content;

namespace TheFloor.Duels;

public enum DuelStatus { Ready, Running, Paused, Finished }
public enum Player { A, B }
public enum StartingPlayer { Random, A, B }
public enum QuestionOrder { Shuffled, Ordered }

public sealed record DuelConfiguration(string PlayerA, string PlayerB, Guid CategoryId,
    int Seconds = 45, int PassSeconds = 3, StartingPlayer Starter = StartingPlayer.Random,
    QuestionOrder Order = QuestionOrder.Shuffled);

public sealed record DuelCommand(Guid SessionId, Guid CommandId, long Version, string Action, Player? Winner);
public sealed record DuelUndo(int Index, Player Active, long ATicks, long BTicks);
public sealed record DuelSession(Guid Id, Guid MediaRootId, Guid CategorySetId, string CategoryName, DuelConfiguration Configuration,
    Question[] Questions, Player First, Player Active, long ATicks, long BTicks)
{
    public long Version { get; init; } = 1;
    public DuelStatus Status { get; init; } = DuelStatus.Ready;
    public int Index { get; init; }
    public DuelUndo? Undo { get; init; }
    public Player? Winner { get; init; }
    public DateTimeOffset? Started { get; init; }
    public DateTimeOffset? Completed { get; init; }
    public Guid[] Commands { get; init; } = [];
    [JsonIgnore]
    public bool Exhausted => Index >= Questions.Length;
    [JsonIgnore]
    public Question? Current => Exhausted ? null : Questions[Index];
}
