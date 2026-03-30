using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Businesses;

public class BusinessTemplateTests
{
    private static SimulationState CreateState() => new SimulationState();

    [Fact]
    public void DinerTemplate_Hours_Open24_7()
    {
        var template = new DinerBusinessTemplate();
        var hours = template.GenerateHours();
        Assert.Equal(7, hours.Count);
        Assert.All(hours, h => Assert.NotNull(h.OpenTime));
    }

    [Fact]
    public void DinerTemplate_Positions_HasCooksAndWaiters()
    {
        var template = new DinerBusinessTemplate();
        var state = CreateState();
        var positions = template.GeneratePositions(state, new Random(42));
        Assert.Contains(positions, p => p.Role == "cook");
        Assert.Contains(positions, p => p.Role == "waiter");
        Assert.True(positions.Count(p => p.Role == "cook") >= 2);
    }

    [Fact]
    public void DinerTemplate_GenerateName_ReturnsNonEmpty()
    {
        var template = new DinerBusinessTemplate();
        Assert.False(string.IsNullOrWhiteSpace(template.GenerateName(new Random(42))));
    }

    [Fact]
    public void DiveBarTemplate_Hours_ClosedSunday()
    {
        var template = new DiveBarBusinessTemplate();
        var hours = template.GenerateHours();
        var sunday = hours.First(h => h.Day == DayOfWeek.Sunday);
        Assert.Null(sunday.OpenTime);
    }

    [Fact]
    public void DiveBarTemplate_Hours_FridayClosesAt4am()
    {
        var template = new DiveBarBusinessTemplate();
        var hours = template.GenerateHours();
        var friday = hours.First(h => h.Day == DayOfWeek.Friday);
        Assert.Equal(new TimeSpan(4, 0, 0), friday.CloseTime);
    }

    [Fact]
    public void DiveBarTemplate_Positions_HasBartenders()
    {
        var template = new DiveBarBusinessTemplate();
        var state = CreateState();
        var positions = template.GeneratePositions(state, new Random(42));
        Assert.Contains(positions, p => p.Role == "bartender");
    }

    [Fact]
    public void OfficeTemplate_Hours_ClosedWeekends()
    {
        var template = new OfficeBusinessTemplate();
        var hours = template.GenerateHours();
        var saturday = hours.First(h => h.Day == DayOfWeek.Saturday);
        var sunday = hours.First(h => h.Day == DayOfWeek.Sunday);
        Assert.Null(saturday.OpenTime);
        Assert.Null(sunday.OpenTime);
    }

    [Fact]
    public void OfficeTemplate_Hours_OpenWeekdays7to7()
    {
        var template = new OfficeBusinessTemplate();
        var hours = template.GenerateHours();
        var monday = hours.First(h => h.Day == DayOfWeek.Monday);
        Assert.Equal(new TimeSpan(7, 0, 0), monday.OpenTime);
        Assert.Equal(new TimeSpan(19, 0, 0), monday.CloseTime);
    }

    [Fact]
    public void OfficeTemplate_Positions_HasCEO()
    {
        var template = new OfficeBusinessTemplate();
        var state = CreateState();
        var positions = template.GeneratePositions(state, new Random(42));
        Assert.Single(positions, p => p.Role == "ceo");
    }

    [Fact]
    public void OfficeTemplate_Positions_AllWeekdaysOnly()
    {
        var template = new OfficeBusinessTemplate();
        var state = CreateState();
        var positions = template.GeneratePositions(state, new Random(42));
        foreach (var p in positions)
        {
            Assert.DoesNotContain(DayOfWeek.Saturday, p.WorkDays);
            Assert.DoesNotContain(DayOfWeek.Sunday, p.WorkDays);
        }
    }

    [Theory]
    [InlineData(BusinessType.Diner)]
    [InlineData(BusinessType.DiveBar)]
    [InlineData(BusinessType.Office)]
    public void Registry_Get_ReturnsTemplateForAllTypes(BusinessType type)
    {
        BusinessTemplateRegistry.RegisterAll();
        Assert.NotNull(BusinessTemplateRegistry.Get(type));
    }
}
