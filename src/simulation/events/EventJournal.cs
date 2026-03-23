using System.Collections.Generic;

namespace Stakeout.Simulation.Events;

public class EventJournal
{
    private readonly List<SimulationEvent> _allEvents = new();
    private readonly Dictionary<int, List<SimulationEvent>> _byPerson = new();

    public IReadOnlyList<SimulationEvent> AllEvents => _allEvents;

    public void Append(SimulationEvent evt)
    {
        _allEvents.Add(evt);

        if (!_byPerson.TryGetValue(evt.PersonId, out var personEvents))
        {
            personEvents = new List<SimulationEvent>();
            _byPerson[evt.PersonId] = personEvents;
        }
        personEvents.Add(evt);
    }

    public IReadOnlyList<SimulationEvent> GetEventsForPerson(int personId)
    {
        return _byPerson.TryGetValue(personId, out var events)
            ? events
            : [];
    }
}
