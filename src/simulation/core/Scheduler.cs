using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Core;

public sealed class EventHandle
{
    public DateTime Time { get; internal set; }
    public bool Cancelled { get; private set; }
    internal Action Action;
    public void Cancel() => Cancelled = true;
}

/// <summary>
/// Discrete-event clock. The world only changes inside scheduled events, so the same seed gives the
/// same history whether Godot advances it in one-second steps or one-day jumps.
/// Ties are broken by scheduling order.
/// </summary>
public class Scheduler
{
    private readonly PriorityQueue<EventHandle, (long Ticks, long Seq)> _queue = new();
    private long _seq;

    public DateTime Now { get; private set; }
    public long EventsProcessed { get; private set; }
    public int Pending => _queue.Count;

    public Scheduler(DateTime start)
    {
        Now = start;
    }

    /// <summary>Schedules an action. Times in the past run at the current time.</summary>
    public EventHandle At(DateTime time, Action action)
    {
        if (time < Now) time = Now;
        var handle = new EventHandle { Time = time, Action = action };
        _queue.Enqueue(handle, (time.Ticks, _seq++));
        return handle;
    }

    public EventHandle After(TimeSpan delay, Action action) => At(Now + delay, action);

    /// <summary>Runs every event due at or before <paramref name="time"/>, then sets the clock to it.</summary>
    public void RunUntil(DateTime time)
    {
        while (_queue.TryPeek(out var handle, out var priority) && priority.Ticks <= time.Ticks)
        {
            _queue.Dequeue();
            if (handle.Cancelled) continue;
            Now = handle.Time;
            EventsProcessed++;
            handle.Action();
        }
        if (time > Now) Now = time;
    }

    /// <summary>Time of the next pending event, or null.</summary>
    public DateTime? NextEventTime
    {
        get
        {
            while (_queue.TryPeek(out var handle, out _) && handle.Cancelled) _queue.Dequeue();
            return _queue.TryPeek(out var next, out _) ? next.Time : null;
        }
    }
}
