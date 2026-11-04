using TheFloor.Content;

namespace TheFloor.Duels;

public static class DuelRules
{
    public static void Validate(DuelConfiguration setup)
    {
        if (string.IsNullOrWhiteSpace(setup.PlayerA) || string.IsNullOrWhiteSpace(setup.PlayerB) ||
            setup.PlayerA.Length > 80 || setup.PlayerB.Length > 80)
            throw new ContentException("Enter both contestant names, each up to 80 characters.");
        if (setup.Seconds is < 1 or > 3600 || setup.PassSeconds is < 0 or > 3600 ||
            !Enum.IsDefined(setup.Starter) || !Enum.IsDefined(setup.Order))
            throw new ContentException("Check the time, pass penalty, starting player, and question order.");
    }

    public static DuelSession Settle(DuelSession state, TimeSpan elapsed, DateTimeOffset now)
    {
        if (state.Status != DuelStatus.Running) return state;
        var ticks = Math.Max(0, Remaining(state) - Math.Max(0, elapsed.Ticks));
        var settled = SetRemaining(state, ticks);
        return ticks == 0
            ? Finish(settled with { Version = state.Version + 1, Undo = null }, Other(state.Active), now)
            : settled;
    }

    public static DuelSession Apply(DuelSession state, DuelCommand command, DateTimeOffset now,
        Func<Question[]> append)
    {
        if (command.CommandId == Guid.Empty) throw new ContentException("A command ID is required.");
        if (state.Commands.Contains(command.CommandId)) return state;
        if (state.Id != command.SessionId || state.Version != command.Version)
            throw new ContentConflict("The duel has changed. Check the current state and try again.");

        var next = command.Action switch
        {
            "start" when state.Status == DuelStatus.Ready => state with { Status = DuelStatus.Running, Started = now },
            "pause" when state.Status == DuelStatus.Running => state with { Status = DuelStatus.Paused },
            "resume" when state.Status == DuelStatus.Paused && !state.Exhausted =>
                state with { Status = DuelStatus.Running, Started = state.Started ?? now },
            "correct" when state.Status == DuelStatus.Running => Advance(state, correct: true, now),
            "pass" when state.Status == DuelStatus.Running => Advance(state, correct: false, now),
            "undo" when state.Undo is { } undo => state with
            {
                Index = undo.Index, Active = undo.Active, ATicks = undo.ATicks, BTicks = undo.BTicks,
                Status = DuelStatus.Paused, Undo = null, Winner = null, Completed = null,
            },
            "append" when state.Status == DuelStatus.Paused && state.Exhausted => Append(state, append()),
            "end" when state.Status != DuelStatus.Finished && (command.Winner is null || Enum.IsDefined(command.Winner.Value)) =>
                Finish(state with { Undo = null }, command.Winner, now),
            "restart" => Restart(state),
            _ => throw new ContentConflict($"Cannot {command.Action} while the duel is {state.Status.ToString().ToLowerInvariant()}."),
        };
        return next with { Version = state.Version + 1, Commands = [.. state.Commands, command.CommandId] };
    }

    private static DuelSession Advance(DuelSession state, bool correct, DateTimeOffset now)
    {
        var next = state with
        {
            Undo = new DuelUndo(state.Index, state.Active, state.ATicks, state.BTicks),
            Index = state.Index + 1,
        };
        if (correct) next = next with { Active = Other(state.Active) };
        else
        {
            next = SetRemaining(next, Math.Max(0, Remaining(next) - TimeSpan.FromSeconds(state.Configuration.PassSeconds).Ticks));
            if (Remaining(next) == 0) return Finish(next, Other(next.Active), now);
        }
        return next.Exhausted ? next with { Status = DuelStatus.Paused } : next;
    }

    private static DuelSession Append(DuelSession state, Question[] questions)
    {
        if (questions.Length == 0) throw new ContentException("Add and enable new questions in this category first.");
        return state with { Questions = [.. state.Questions, .. questions] };
    }

    private static DuelSession Restart(DuelSession state)
    {
        var ticks = TimeSpan.FromSeconds(state.Configuration.Seconds).Ticks;
        return state with
        {
            Status = DuelStatus.Ready, Active = state.First, ATicks = ticks, BTicks = ticks, Index = 0,
            Undo = null, Winner = null, Started = null, Completed = null,
        };
    }

    private static DuelSession Finish(DuelSession state, Player? winner, DateTimeOffset now) =>
        state with { Status = DuelStatus.Finished, Winner = winner, Completed = now };

    private static long Remaining(DuelSession state) => state.Active == Player.A ? state.ATicks : state.BTicks;

    private static DuelSession SetRemaining(DuelSession state, long ticks) =>
        state.Active == Player.A ? state with { ATicks = ticks } : state with { BTicks = ticks };

    private static Player Other(Player player) => player == Player.A ? Player.B : Player.A;
}
