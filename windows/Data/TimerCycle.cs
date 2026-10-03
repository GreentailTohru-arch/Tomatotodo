namespace Tomatotodo_Windows.Data;

public static class TimerCycle
{
    public static bool ShouldContinue(bool automatic, bool countup, bool nextIsBreak,
        bool hadTask, TodoTask? task, Guid? activeTaskId)
    {
        if (!automatic || countup) return false;
        if (nextIsBreak) return true;
        if (!hadTask) return activeTaskId is null;
        return task is { IsComplete: false } && task.Id == activeTaskId &&
            (task.EstimatedPomodoros is not > 0 || task.CompletedPomodoros < task.EstimatedPomodoros);
    }
}
