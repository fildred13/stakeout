using System;

namespace Stakeout.Simulation.Entities;

public enum JobType
{
    DinerWaiter,
    OfficeWorker,
    Bartender
}

public class Job
{
    public int Id { get; set; }
    public JobType Type { get; set; }
    public string Title { get; set; }
    public int WorkAddressId { get; set; }
    public TimeSpan ShiftStart { get; set; }
    public TimeSpan ShiftEnd { get; set; }
    public DayOfWeek[] WorkDays { get; set; }
}
