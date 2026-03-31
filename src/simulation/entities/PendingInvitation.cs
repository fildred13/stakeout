using System;
namespace Stakeout.Simulation.Entities;

public enum InvitationType { DateInvitation, MeetingRequest }

public class PendingInvitation
{
    public int Id { get; set; }
    public int FromPersonId { get; set; }
    public int ToPersonId { get; set; }
    public InvitationType Type { get; set; }
    public int ProposedGroupId { get; set; }
    public DateTime CreatedAt { get; set; }
}
