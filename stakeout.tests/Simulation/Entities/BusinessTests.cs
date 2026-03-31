using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class BusinessTests
{
    [Fact]
    public void Business_DefaultState_IsNotResolved()
    {
        var biz = new Business { Id = 1, Name = "Test", Type = BusinessType.Diner };
        Assert.False(biz.IsResolved);
    }

    [Fact]
    public void Business_Positions_StartsEmpty()
    {
        var biz = new Business { Id = 1, Name = "Test", Type = BusinessType.Diner };
        Assert.Empty(biz.Positions);
    }

    [Fact]
    public void Business_Hours_StartsEmpty()
    {
        var biz = new Business { Id = 1, Name = "Test", Type = BusinessType.Diner };
        Assert.Empty(biz.Hours);
    }

    [Fact]
    public void Position_DefaultAssignment_IsNull()
    {
        var pos = new Position { Id = 1, BusinessId = 1, Role = "cook" };
        Assert.Null(pos.AssignedPersonId);
    }

    [Fact]
    public void BusinessHours_NullOpenTime_MeansClosed()
    {
        var hours = new BusinessHours { Day = DayOfWeek.Sunday, OpenTime = null, CloseTime = null };
        Assert.Null(hours.OpenTime);
    }
}
