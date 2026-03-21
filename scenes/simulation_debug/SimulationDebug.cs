using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class SimulationDebug : Control
{
    private SimulationManager _simulationManager;
    private Label _clockLabel;
    private ItemList _peopleList;

    public override void _Ready()
    {
        _clockLabel = GetNode<Label>("MarginContainer/VBoxContainer/ClockLabel");
        _peopleList = GetNode<ItemList>("MarginContainer/VBoxContainer/PeopleList");

        _simulationManager = new SimulationManager();
        AddChild(_simulationManager);

        _simulationManager.PersonAdded += OnPersonAdded;
    }

    public override void _Process(double delta)
    {
        var time = _simulationManager.State.Clock.CurrentTime;
        _clockLabel.Text = time.ToString("HH:mm:ss");
    }

    private void OnPersonAdded(Person person)
    {
        _peopleList.AddItem($"{person.Id}. {person.FullName}");
    }
}
