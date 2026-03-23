using System;
using System.Linq;
using Godot;
using Stakeout.Evidence;
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

    public void Populate(EvidenceItem item, SimulationState state)
    {
        if (item.EntityType == EvidenceEntityType.Person && state.People.TryGetValue(item.EntityId, out var person))
        {
            _titleLabel.Text = person.FullName;
            var lines = new System.Collections.Generic.List<string>();

            if (state.Addresses.TryGetValue(person.HomeAddressId, out var home))
            {
                var street = state.Streets[home.StreetId];
                lines.Add($"Home: {home.Number} {street.Name}");
            }
            if (state.Jobs.TryGetValue(person.JobId, out var job) &&
                state.Addresses.TryGetValue(job.WorkAddressId, out var work))
            {
                var workStreet = state.Streets[work.StreetId];
                lines.Add($"Job: {job.Title}");
                lines.Add($"Work: {work.Number} {workStreet.Name}");
                var startTime = DateTime.Today.Add(job.ShiftStart).ToString("h:mm tt");
                var endTime = DateTime.Today.Add(job.ShiftEnd).ToString("h:mm tt");
                lines.Add($"Shift: {startTime} - {endTime}");
            }

            _bodyLabel.Text = string.Join("\n", lines);
        }
        else if (item.EntityType == EvidenceEntityType.Address && state.Addresses.TryGetValue(item.EntityId, out var address))
        {
            var street = state.Streets[address.StreetId];
            _titleLabel.Text = $"{address.Number} {street.Name} — {address.Type}";

            var people = state.People.Values
                .Where(p => p.HomeAddressId == address.Id ||
                            (state.Jobs.TryGetValue(p.JobId, out var j) && j.WorkAddressId == address.Id))
                .ToList();

            if (people.Count > 0)
            {
                var peopleLines = people.Select(p =>
                {
                    var rel = p.HomeAddressId == address.Id ? "lives here" : "works here";
                    return $"{p.FullName} ({rel})";
                });
                _bodyLabel.Text = string.Join("\n", peopleLines);
            }
            else
            {
                _bodyLabel.Text = "No known associates.";
            }
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
