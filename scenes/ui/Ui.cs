using System;
using Godot;

namespace Stakeout.UI;

/// <summary>Look and feel shared by every screen: fonts, palette, a theme, and small builders for controls.</summary>
public static class Ui
{
    public static readonly Color Bg = new(0.07f, 0.06f, 0.055f);
    public static readonly Color Panel = new(0.12f, 0.105f, 0.095f);
    public static readonly Color PanelLight = new(0.18f, 0.16f, 0.14f);
    public static readonly Color Line = new(0.32f, 0.28f, 0.24f);
    public static readonly Color Text = new(0.91f, 0.88f, 0.82f);
    public static readonly Color Dim = new(0.6f, 0.56f, 0.5f);
    public static readonly Color Amber = new(0.92f, 0.66f, 0.25f);
    public static readonly Color Red = new(0.82f, 0.27f, 0.2f);
    public static readonly Color Blue = new(0.36f, 0.6f, 0.92f);
    public static readonly Color Green = new(0.45f, 0.75f, 0.42f);
    public static readonly Color Paper = new(0.93f, 0.9f, 0.8f);
    public static readonly Color Ink = new(0.15f, 0.17f, 0.3f);

    private static Theme _theme;
    public static Font Pixel { get; private set; }
    public static Font Title { get; private set; }
    public static Font Hand { get; private set; }
    public static Font HandBold { get; private set; }

    public static Theme Theme
    {
        get
        {
            if (_theme != null) return _theme;
            Pixel = GD.Load<Font>("res://fonts/exepixelperfect/EXEPixelPerfect.ttf");
            Title = GD.Load<Font>("res://fonts/karma-future/Karma Future.otf");
            Hand = GD.Load<Font>("res://fonts/Caveat/Caveat-Regular.ttf");
            HandBold = GD.Load<Font>("res://fonts/Caveat/Caveat-Bold.ttf");
            var t = new Theme { DefaultFont = Pixel, DefaultFontSize = 16 };
            t.SetColor("font_color", "Label", Text);
            t.SetColor("font_color", "Button", Text);
            t.SetColor("font_hover_color", "Button", Amber);
            t.SetColor("font_pressed_color", "Button", Amber);
            t.SetColor("font_focus_color", "Button", Text);
            t.SetColor("font_disabled_color", "Button", new Color(Dim, 0.6f));
            t.SetStylebox("normal", "Button", Box(PanelLight, Line));
            t.SetStylebox("hover", "Button", Box(new Color(0.24f, 0.2f, 0.16f), Amber));
            t.SetStylebox("pressed", "Button", Box(new Color(0.3f, 0.22f, 0.12f), Amber));
            t.SetStylebox("disabled", "Button", Box(new Color(0.12f, 0.11f, 0.1f), new Color(Line, 0.5f)));
            t.SetStylebox("focus", "Button", new StyleBoxEmpty());
            t.SetStylebox("panel", "PanelContainer", Box(Panel, Line));
            t.SetStylebox("normal", "LineEdit", Box(new Color(0.05f, 0.045f, 0.04f), Line));
            t.SetStylebox("focus", "LineEdit", Box(new Color(0.05f, 0.045f, 0.04f), Amber));
            t.SetColor("font_color", "LineEdit", Text);
            t.SetStylebox("panel", "ItemList", Box(new Color(0.05f, 0.045f, 0.04f), Line));
            t.SetColor("font_color", "ItemList", Text);
            t.SetColor("font_selected_color", "ItemList", Amber);
            t.SetStylebox("selected", "ItemList", Box(new Color(0.25f, 0.18f, 0.1f), Amber, 0));
            t.SetStylebox("selected_focus", "ItemList", Box(new Color(0.25f, 0.18f, 0.1f), Amber, 0));
            t.SetStylebox("focus", "ItemList", new StyleBoxEmpty());
            t.SetColor("default_color", "RichTextLabel", Text);
            t.SetStylebox("normal", "OptionButton", Box(PanelLight, Line));
            t.SetStylebox("hover", "OptionButton", Box(new Color(0.24f, 0.2f, 0.16f), Amber));
            t.SetStylebox("pressed", "OptionButton", Box(new Color(0.3f, 0.22f, 0.12f), Amber));
            t.SetStylebox("focus", "OptionButton", new StyleBoxEmpty());
            t.SetColor("font_color", "CheckBox", Text);
            t.SetStylebox("panel", "PopupMenu", Box(Panel, Amber));
            t.SetColor("font_color", "PopupMenu", Text);
            t.SetColor("font_hover_color", "PopupMenu", Amber);
            t.SetStylebox("hover", "PopupMenu", Box(new Color(0.25f, 0.18f, 0.1f), Amber, 0));
            t.SetStylebox("panel", "TooltipPanel", Box(new Color(0.05f, 0.045f, 0.04f, 0.95f), Amber));
            t.SetColor("font_color", "TooltipLabel", Text);
            t.SetFont("font", "TooltipLabel", Pixel);
            _theme = t;
            return t;
        }
    }

    public static StyleBoxFlat Box(Color bg, Color border, int borderWidth = 1, int margin = 6)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg, BorderColor = border,
            BorderWidthLeft = borderWidth, BorderWidthRight = borderWidth, BorderWidthTop = borderWidth, BorderWidthBottom = borderWidth,
            ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin * 0.6f, ContentMarginBottom = margin * 0.6f
        };
        return s;
    }

    public static Label Label(string text, int size = 16, Color? color = null, bool wrap = false, Font font = null)
    {
        var l = new Label { Text = text };
        if (size != 16) l.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
        if (font != null) l.AddThemeFontOverride("font", font);
        if (wrap)
        {
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            l.CustomMinimumSize = new Vector2(80, 0);
        }
        return l;
    }

    public static Label Heading(string text, Color? color = null) => Label(text.ToUpperInvariant(), 16, color ?? Amber);

    public static Button Button(string text, Action pressed, bool enabled = true, string tooltip = null)
    {
        var b = new Button { Text = text, Disabled = !enabled, Alignment = HorizontalAlignment.Left, TooltipText = tooltip ?? "" };
        b.ClipText = false;
        if (pressed != null) b.Pressed += () => pressed();
        return b;
    }

    public static VBoxContainer VBox(int separation = 4)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", separation);
        return v;
    }

    public static HBoxContainer HBox(int separation = 4)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", separation);
        return h;
    }

    public static PanelContainer PanelBox(Color? bg = null, Color? border = null, int margin = 8)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Box(bg ?? Panel, border ?? Line, 1, margin));
        return p;
    }

    public static RichTextLabel Rich(string bbcode = "", int size = 16, Font font = null, bool fit = true)
    {
        var r = new RichTextLabel
        {
            BbcodeEnabled = true, Text = bbcode, FitContent = fit, ScrollActive = !fit,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SelectionEnabled = true
        };
        if (font != null)
        {
            r.AddThemeFontOverride("normal_font", font);
            r.AddThemeFontOverride("bold_font", font == Hand ? HandBold : font);
        }
        r.AddThemeFontSizeOverride("normal_font_size", size);
        r.AddThemeFontSizeOverride("bold_font_size", size);
        return r;
    }

    public static Control Spacer(float height = 8) => new Control { CustomMinimumSize = new Vector2(0, height) };

    public static Control Expander()
    {
        var c = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        c.MouseFilter = Control.MouseFilterEnum.Ignore;
        return c;
    }

    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    public static string Escape(string s) => s?.Replace("[", "[lb]") ?? "";

    public static string Hex(Color c) => c.ToHtml(false);
}
