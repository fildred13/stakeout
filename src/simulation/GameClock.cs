using System;

namespace Stakeout.Simulation;

public class GameClock
{
    public DateTime CurrentTime { get; private set; }
    public double ElapsedSeconds { get; private set; }
    public float TimeScale { get; set; } = 1.0f;

    public GameClock(DateTime? startTime = null)
    {
        CurrentTime = startTime ?? new DateTime(1980, 1, 1, 0, 0, 0);
        ElapsedSeconds = 0.0;
    }

    public void Tick(double deltaSec)
    {
        ElapsedSeconds += deltaSec;
        CurrentTime = CurrentTime.AddSeconds(deltaSec);
    }
}
