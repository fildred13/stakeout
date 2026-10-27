using System;
using System.Linq;
using Godot;
using Stakeout.Evidence;
using Stakeout.Investigation;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class DossierWindow : Panel
{
    private Label _titleLabel;
    private Label _bodyLabel;
    private Button _closeButton;

    private bool _isDragging;
    private Vector2 _dragOffset;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBox/Title");
        _bodyLabel = GetNode<Label>("VBox/Body");
        _closeButton = GetNode<Button>("CloseButton");

        _closeButton.Pressed += () => QueueFree();

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.95f, 0.92f, 0.85f),
            BorderColor = new Color(0.4f, 0.35f, 0.3f),
            BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderWidthTop = 2, BorderWidthBottom = 2
        };
        AddThemeStyleboxOverride("panel", style);
    }

    public void Populate(EvidenceItem item, Detective detective)
    {
        var state = detective.State;
        if (item.EntityType == EvidenceEntityType.Person && state.People.TryGetValue(item.EntityId, out var person))
        {
            _titleLabel.Text = person.FullName;
            var lines = new System.Collections.Generic.List<string>();
            detective.People.TryGetValue(person.Id, out var known);
            if (known?.AddressKnown == true) lines.Add($"Home: {state.HomeLine(person)}");
            if (known?.EmployerKnown == true && person.BusinessId.HasValue) lines.Add($"Work: {state.Businesses[person.BusinessId.Value].Name}");
            if (known?.VehicleKnown == true && person.VehicleId.HasValue) lines.Add($"Car: {state.Vehicles[person.VehicleId.Value].Description}");
            if (known?.Met == true) lines.Add(detective.Looks(person.Id));
            _bodyLabel.Text = lines.Count > 0 ? string.Join('\n', lines) : "Not much known yet.";
        }
        else if (item.EntityType == EvidenceEntityType.Address && state.Addresses.TryGetValue(item.EntityId, out var address))
        {
            _titleLabel.Text = state.PlaceName(address.Id);
            var people = detective.People.Values
                .Where(k => k.AddressKnown && state.People[k.PersonId].HomeAddressId == address.Id)
                .Select(k => $"{state.People[k.PersonId].FullName} lives here").ToList();
            _bodyLabel.Text = people.Count > 0 ? string.Join('\n', people) : "No known associates.";
        }
        else if (item.EntityType == EvidenceEntityType.Clue)
        {
            var clue = detective.Clues.FirstOrDefault(c => c.Id == item.EntityId);
            _titleLabel.Text = clue?.Title ?? "?";
            _bodyLabel.Text = clue == null ? "" : clue.Text + $"  ({clue.Source}, {clue.FoundAt:ddd HH:mm})";
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                var localY = mb.Position.Y;
                if (localY <= 30f)
                {
                    _isDragging = true;
                    _dragOffset = Position - mb.GlobalPosition;
                }
                AcceptEvent();
            }
            else
            {
                _isDragging = false;
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion mm && _isDragging)
        {
            Position = mm.GlobalPosition + _dragOffset;
            AcceptEvent();
        }
    }
}
