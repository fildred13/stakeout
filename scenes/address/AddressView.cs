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

    private void OnLeave()
    {
        _gameShell.LoadContentView("res://scenes/city/CityView.tscn");
    }
}
