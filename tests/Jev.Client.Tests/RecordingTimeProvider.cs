namespace Jev.Client.Tests;

/// <summary>
/// A clock fixed at <see cref="Now"/> whose timers fire at once, recording each requested
/// delay, so retry tests never wait for real.
/// </summary>
internal sealed class RecordingTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Every timer delay requested, in order.</summary>
    public List<TimeSpan> Waits { get; } = [];

    public override DateTimeOffset GetUtcNow() => Now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Waits.Add(dueTime);
        ThreadPool.QueueUserWorkItem(_ => callback(state));
        return new NoOpTimer();
    }

    private sealed class NoOpTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
