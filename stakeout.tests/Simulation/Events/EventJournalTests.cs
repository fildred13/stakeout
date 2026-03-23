using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Xunit;

namespace Stakeout.Tests.Simulation.Events;

public class EventJournalTests
{
    [Fact]
    public void Append_AddsToGlobalList()
    {
        var journal = new EventJournal();
        var evt = new SimulationEvent
        {
            Timestamp = new DateTime(1980, 1, 1, 8, 0, 0),
            PersonId = 1,
            EventType = SimulationEventType.WokeUp,
            AddressId = 10
        };

        journal.Append(evt);

        Assert.Single(journal.AllEvents);
        Assert.Same(evt, journal.AllEvents[0]);
    }

    [Fact]
    public void Append_IndexesByPersonId()
    {
        var journal = new EventJournal();
        var evt = new SimulationEvent
        {
            Timestamp = new DateTime(1980, 1, 1, 8, 0, 0),
            PersonId = 5,
            EventType = SimulationEventType.WokeUp
        };

        journal.Append(evt);

        var personEvents = journal.GetEventsForPerson(5);
        Assert.Single(personEvents);
        Assert.Same(evt, personEvents[0]);
    }

    [Fact]
    public void GetEventsForPerson_UnknownPerson_ReturnsEmptyList()
    {
        var journal = new EventJournal();

        var events = journal.GetEventsForPerson(999);

        Assert.Empty(events);
    }

    [Fact]
    public void Append_MultipleEventsForSamePerson_AllIndexed()
    {
        var journal = new EventJournal();
        var evt1 = new SimulationEvent { Timestamp = new DateTime(1980, 1, 1, 8, 0, 0), PersonId = 1, EventType = SimulationEventType.WokeUp };
        var evt2 = new SimulationEvent { Timestamp = new DateTime(1980, 1, 1, 9, 0, 0), PersonId = 1, EventType = SimulationEventType.DepartedAddress };

        journal.Append(evt1);
        journal.Append(evt2);

        Assert.Equal(2, journal.AllEvents.Count);
        Assert.Equal(2, journal.GetEventsForPerson(1).Count);
    }

    [Fact]
    public void Append_DifferentPeople_IndexedSeparately()
    {
        var journal = new EventJournal();
        var evt1 = new SimulationEvent { Timestamp = new DateTime(1980, 1, 1, 8, 0, 0), PersonId = 1, EventType = SimulationEventType.WokeUp };
        var evt2 = new SimulationEvent { Timestamp = new DateTime(1980, 1, 1, 8, 0, 0), PersonId = 2, EventType = SimulationEventType.WokeUp };

        journal.Append(evt1);
        journal.Append(evt2);

        Assert.Equal(2, journal.AllEvents.Count);
        Assert.Single(journal.GetEventsForPerson(1));
        Assert.Single(journal.GetEventsForPerson(2));
    }
}
