using Godot;

public partial class EvidenceBoard : Control
{
    private Control _corkboardCanvas;
    private Control _polaroidContainer;
    private Button _closeButton;

    private Vector2 _panOffset = Vector2.Zero;
    private float _zoom = 1.0f;
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 2.0f;
    private const float ZoomStep = 0.1f;

    private bool _isPanning;
    private Vector2 _panStartMouse;
    private Vector2 _panStartOffset;

    public override void _Ready()
    {
        _corkboardCanvas = GetNode<Control>("CorkboardCanvas");
        _polaroidContainer = GetNode<Control>("CorkboardCanvas/PolaroidContainer");
        _closeButton = GetNode<Button>("CloseButton");

        _closeButton.Pressed += OnClosePressed;

        // Center the view on the canvas initially
        var viewportSize = GetViewportRect().Size;
        var canvasSize = _corkboardCanvas.GetNode<Control>("CorkboardBackground").Size;
        _panOffset = (viewportSize - canvasSize) / 2;
        ApplyTransform();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            HandleMouseButton(mouseButton);
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isPanning)
        {
            HandlePanMotion(mouseMotion);
        }
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        // Zoom with scroll wheel
        if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
        {
            Zoom(ZoomStep, mouseButton.Position);
        }
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
        {
            Zoom(-ZoomStep, mouseButton.Position);
        }
        // Pan with middle mouse button
        else if (mouseButton.ButtonIndex == MouseButton.Middle)
        {
            if (mouseButton.Pressed)
            {
                _isPanning = true;
                _panStartMouse = mouseButton.Position;
                _panStartOffset = _panOffset;
            }
            else
            {
                _isPanning = false;
            }
        }
        // Left-click on empty space pans
        else if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _isPanning = true;
                _panStartMouse = mouseButton.Position;
                _panStartOffset = _panOffset;
            }
            else
            {
                _isPanning = false;
            }
        }
    }

    private void HandlePanMotion(InputEventMouseMotion mouseMotion)
    {
        _panOffset = _panStartOffset + (mouseMotion.Position - _panStartMouse);
        ApplyTransform();
    }

    private void Zoom(float delta, Vector2 mousePos)
    {
        var oldZoom = _zoom;
        _zoom = Mathf.Clamp(_zoom + delta, MinZoom, MaxZoom);

        // Zoom towards mouse position
        var zoomRatio = _zoom / oldZoom;
        _panOffset = mousePos - (mousePos - _panOffset) * zoomRatio;
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        _corkboardCanvas.Position = _panOffset;
        _corkboardCanvas.Scale = new Vector2(_zoom, _zoom);
    }

    private void OnClosePressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/simulation_debug/SimulationDebug.tscn");
    }
}
