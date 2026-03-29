using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class AddressView : Control, IContentView
{
    private GameManager _gameManager;
    private SimulationManager _simulationManager;
    private GameShell _gameShell;
    private Label _addressLabel;

    public void SetGameShell(GameShell shell)
    {
        _gameShell = shell;
        ShowDefaultMenu();
    }

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _simulationManager = _gameManager.SimulationManager;

        _addressLabel = GetNode<Label>("AddressLabel");

        var player = _simulationManager.State.Player;
        if (player != null && _simulationManager.State.Addresses.TryGetValue(player.CurrentAddressId, out var address))
        {
            var street = _simulationManager.State.Streets[address.StreetId];
            _addressLabel.Text = $"{address.Number} {street.Name}\n({address.Type})";
        }
    }

    private void ShowDefaultMenu()
    {
        if (_gameShell == null) return;

        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "List People" },
            { "callback", Callable.From(ShowPeopleMenu) }
        });

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "Evidence Board" },
            { "callback", Callable.From(() => _gameShell.OpenEvidenceBoard()) }
        });

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "Graph View" },
            { "callback", Callable.From(ShowGraphView) }
        });

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "Blueprint View" },
            { "callback", Callable.From(ShowBlueprintView) }
        });

        // Airport: show fly-to options for other cities
        var state = _simulationManager.State;
        var player = state.Player;
        if (player != null && state.Addresses.TryGetValue(player.CurrentAddressId, out var addr)
            && addr.Type == Stakeout.Simulation.Entities.AddressType.Airport)
        {
            foreach (var city in state.Cities.Values)
            {
                if (city.Id == player.CurrentCityId) continue;
                if (!city.AirportAddressId.HasValue) continue;

                var destCityId = city.Id;
                var destAirportId = city.AirportAddressId.Value;
                items.Add(new Godot.Collections.Dictionary
                {
                    { "label", $"Fly to {city.Name}" },
                    { "callback", Callable.From(() => OnFlyToCity(destCityId, destAirportId)) }
                });
            }
        }

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "Leave" },
            { "callback", Callable.From(OnLeave) }
        });

        _gameShell.SetMenuItems(items);
    }

    private void ShowPeopleMenu()
    {
        if (_gameShell == null) return;

        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var player = _simulationManager.State.Player;
        var state = _simulationManager.State;

        if (player != null && state.Addresses.TryGetValue(player.CurrentAddressId, out var address))
        {
            var peopleHere = state.People.Values
                .Where(p => p.CurrentAddressId.HasValue && p.CurrentAddressId.Value == address.Id)
                .OrderBy(p => p.FullName)
                .ToList();

            foreach (var person in peopleHere)
            {
                var personId = person.Id;
                items.Add(new Godot.Collections.Dictionary
                {
                    { "label", person.FullName },
                    { "callback", Callable.From(() => { }) },
                    { "personId", personId }
                });
            }

            if (peopleHere.Count == 0)
            {
                items.Add(new Godot.Collections.Dictionary
                {
                    { "label", "(nobody here)" }
                });
            }
        }

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "Go back" },
            { "callback", Callable.From(ShowDefaultMenu) }
        });

        _gameShell.SetMenuItems(items);
    }

    private void ShowGraphView()
    {
        var player = _simulationManager.State.Player;
        if (player == null) return;

        var graphView = GD.Load<PackedScene>("res://scenes/address/GraphView.tscn").Instantiate<GraphView>();
        RemoveSublocationViews();
        AddChild(graphView);
        graphView.Initialize(_simulationManager.State, player.CurrentAddressId);
    }

    private void ShowBlueprintView()
    {
        var player = _simulationManager.State.Player;
        if (player == null) return;

        var blueprintView = GD.Load<PackedScene>("res://scenes/address/BlueprintView.tscn").Instantiate<BlueprintView>();
        RemoveSublocationViews();
        AddChild(blueprintView);
        blueprintView.Initialize(_simulationManager.State, player.CurrentAddressId);
    }

    private void RemoveSublocationViews()
    {
        foreach (var child in GetChildren())
        {
            if (child is GraphView or BlueprintView)
            {
                child.QueueFree();
            }
        }
    }

    private void OnFlyToCity(int destCityId, int destAirportAddressId)
    {
        var state = _simulationManager.State;
        var player = state.Player;
        var destAddress = state.Addresses[destAirportAddressId];

        // Update player city and position to destination airport
        player.CurrentCityId = destCityId;
        player.CurrentAddressId = destAirportAddressId;
        player.CurrentPosition = destAddress.Position;

        // Leave the AddressView back to CityView (which will now show the new city)
        _gameShell.LoadContentView("res://scenes/city/CityView.tscn");
    }

    private void OnLeave()
    {
        _gameShell.LoadContentView("res://scenes/city/CityView.tscn");
    }
}
