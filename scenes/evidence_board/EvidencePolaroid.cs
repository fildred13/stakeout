using Godot;
using Stakeout.Evidence;

public partial class EvidencePolaroid : Control
{
    [Signal]
    public delegate void PolaroidClickedEventHandler(int itemId);

    [Signal]
    public delegate void PolaroidRemovedEventHandler(int itemId);

    [Signal]
    public delegate void ThumbtackDragStartedEventHandler(int itemId, Vector2 globalPos);

    [Signal]
    public delegate void ThumbtackRightClickedEventHandler(int itemId, Vector2 globalPos);

    public int ItemId { get; set; }

    private Label _initialsLabel;
    private Label _captionLabel;
    private Panel _thumbtack;
    private bool _isDragging;
    private Vector2 _dragOffset;
    private bool _dragStartedOnThumbtrack;
    private const float DragThreshold = 4f;
    private Vector2 _mouseDownPos;
    private bool _mouseDown;

    public override void _Ready()
    {
        _initialsLabel = GetNode<Label>("Body/ImageArea/Initials");
        _captionLabel = GetNode<Label>("Body/Caption");
        _thumbtack = GetNode<Panel>("Thumbtack");
    }

    public void SetContent(string initials, string caption)
    {
        _initialsLabel.Text = initials;
        _captionLabel.Text = caption;
    }

    public Vector2 GetThumbTackGlobalCenter()
    {
        return _thumbtack.GlobalPosition + _thumbtack.Size / 2;
    }

    public void SetThumbTackGlow(bool glowing)
    {
        var style = new StyleBoxFlat
        {
            BgColor = glowing ? new Color(1f, 0.8f, 0.2f) : new Color(0.8f, 0.1f, 0.1f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        };
        if (glowing)
        {
            style.ShadowColor = new Color(1f, 0.9f, 0.3f, 0.6f);
            style.ShadowSize = 4;
        }
        _thumbtack.AddThemeStyleboxOverride("panel", style);
    }

    /// <summary>
    /// Converts a global/viewport position to canvas-local coordinates,
    /// accounting for the parent canvas's pan and zoom transform.
    /// </summary>
    private Vector2 ToCanvasLocal(Vector2 globalPos)
    {
        var canvas = GetParent<Control>();
        return (globalPos - canvas.GlobalPosition) / canvas.Scale;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _mouseDown = true;
                    _mouseDownPos = mb.GlobalPosition;

                    // Check if click is on the thumbtack
                    var thumbtackRect = new Rect2(_thumbtack.GlobalPosition, _thumbtack.Size);
                    _dragStartedOnThumbtrack = thumbtackRect.HasPoint(mb.GlobalPosition);

                    if (!_dragStartedOnThumbtrack)
                    {
                        _dragOffset = Position - ToCanvasLocal(mb.GlobalPosition);
                    }
                    AcceptEvent();
                }
                else
                {
                    if (_isDragging)
                    {
                        _isDragging = false;
                    }
                    else if (_mouseDown && !_dragStartedOnThumbtrack)
                    {
                        // Single click — open dossier
                        EmitSignal(SignalName.PolaroidClicked, ItemId);
                    }
                    _mouseDown = false;
                    _dragStartedOnThumbtrack = false;
                    AcceptEvent();
                }
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                var thumbtackRect = new Rect2(_thumbtack.GlobalPosition, _thumbtack.Size);
                if (thumbtackRect.HasPoint(mb.GlobalPosition))
                {
                    EmitSignal(SignalName.ThumbtackRightClicked, ItemId, mb.GlobalPosition);
                }
                else
                {
                    // Show context menu with "Remove from Board"
                    var menu = new PopupMenu();
                    menu.AddItem("Remove from Board", 0);
                    menu.IdPressed += (id) =>
                    {
                        if (id == 0) EmitSignal(SignalName.PolaroidRemoved, ItemId);
                        menu.QueueFree();
                    };
                    menu.PopupHide += () => menu.QueueFree();
                    AddChild(menu);
                    menu.Position = new Vector2I((int)mb.GlobalPosition.X, (int)mb.GlobalPosition.Y);
                    menu.Popup();
                }
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion mm && _mouseDown)
        {
            var distance = mm.GlobalPosition.DistanceTo(_mouseDownPos);
            if (distance > DragThreshold)
            {
                if (_dragStartedOnThumbtrack)
                {
                    EmitSignal(SignalName.ThumbtackDragStarted, ItemId, mm.GlobalPosition);
                    _mouseDown = false; // Hand off to board for string creation
                }
                else if (!_isDragging)
                {
                    _isDragging = true;
                    // Bring to front
                    GetParent()?.MoveChild(this, -1);
                }
            }

            if (_isDragging)
            {
                Position = ToCanvasLocal(mm.GlobalPosition) + _dragOffset;
                AcceptEvent();
            }
        }
    }
}
