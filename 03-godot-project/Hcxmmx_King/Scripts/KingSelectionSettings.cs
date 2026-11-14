using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Hcxmmx.KingMod.Scripts;

/// <summary>One settings overlay owned by each character selection screen.</summary>
internal partial class KingSelectionSettings : Control
{
    private const string OverlayName = "KingSelectionSettings";
    private Control _view = null!;
    private LocManager? _localization;
    private NHotkeyManager? _blockedHotkeys;

    internal static void Update(Control screen, bool show)
    {
        var overlay = screen.GetNodeOrNull<KingSelectionSettings>(OverlayName);
        if (overlay == null && show)
        {
            overlay = new KingSelectionSettings { Name = OverlayName, MouseFilter = MouseFilterEnum.Ignore };
            screen.AddChild(overlay);
            overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        if (overlay == null) return;
        overlay.Visible = show;
        overlay.Refresh();
        if (!show) overlay._view.Call("set_open", false);
    }

    public override void _Ready()
    {
        var script = ResourceLoader.Load<GDScript>("res://Hcxmmx_King/Presentation/SelectionSettings.gd");
        _view = (Control)script.New().AsGodotObject();
        _view.Name = "PanelView";
        AddChild(_view);
        var definitions = new Godot.Collections.Dictionary
        {
            ["translations"] = new Godot.Collections.Dictionary { ["current"] = Translations() }
        };
        _view.Call("setup", definitions, "current", KingGlobals.IsWhiteKingSkin, KingGlobals.UseOriginalTiming);
        _view.Connect("settings_changed", Callable.From<bool, bool>(SaveSelection));
        _view.Connect("opened_changed", Callable.From<bool>(SetHotkeyBlock));
        _localization = LocManager.Instance;
        _localization?.SubscribeToLocaleChange(Refresh);
    }

    private static Godot.Collections.Dictionary Translations()
    {
        var texts = new Godot.Collections.Dictionary();
        foreach (var key in new[] { "open", "title", "close", "skin", "blue", "white", "rhythm",
                     "fast", "original", "fast_hint", "original_hint", "saved" })
            texts[key] = KingLocalization.GetText("settings." + key);
        return texts;
    }

    private void Refresh() => _view.Call("sync_settings", Translations(),
        KingGlobals.IsWhiteKingSkin, KingGlobals.UseOriginalTiming);

    private static void SaveSelection(bool white, bool original)
    {
        bool skinChanged = KingGlobals.IsWhiteKingSkin != white;
        KingGlobals.IsWhiteKingSkin = white;
        if (KingGlobals.UseOriginalTiming != original)
            KingGlobals.ToggleTiming(); // Saves both settings and refreshes any live presentation.
        else if (skinChanged)
            KingGlobals.SaveConfig();
    }

    private void SetHotkeyBlock(bool opened)
    {
        if (opened && _blockedHotkeys == null)
        {
            _blockedHotkeys = NHotkeyManager.Instance;
            _blockedHotkeys?.AddBlockingScreen(this);
        }
        else if (!opened)
        {
            if (IsInstanceValid(_blockedHotkeys)) _blockedHotkeys!.RemoveBlockingScreen(this);
            _blockedHotkeys = null;
        }
    }

    public override void _ExitTree()
    {
        SetHotkeyBlock(false);
        _localization?.UnsubscribeToLocaleChange(Refresh);
        _localization = null;
    }
}
