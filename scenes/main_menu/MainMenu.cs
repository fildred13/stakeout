using Godot;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        GetNode<Button>("VBoxContainer/NewCareerButton").Pressed += _OnNewCareerPressed;
        GetNode<Button>("VBoxContainer/LoadCareerButton").Pressed += _OnLoadCareerPressed;
        GetNode<Button>("VBoxContainer/GodModeButton").Pressed += _OnGodModePressed;
        GetNode<Button>("VBoxContainer/OptionsButton").Pressed += _OnOptionsPressed;
    }

    private void _OnNewCareerPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/game_shell/GameShell.tscn");
    }
    private void _OnLoadCareerPressed() { }
    private void _OnGodModePressed() { }
    private void _OnOptionsPressed() { }
}
