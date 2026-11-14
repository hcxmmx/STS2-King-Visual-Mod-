using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;

internal static class SelectionSettingsCheck
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Validate(Assembly mod, Node root)
    {
        var globals = mod.GetType("Hcxmmx.KingMod.Scripts.KingGlobals", true)!;
        var skin = globals.GetField("IsWhiteKingSkin", Static)!;
        var timing = globals.GetField("UseOriginalTiming", Static)!;
        object oldSkin = skin.GetValue(null)!;
        object oldTiming = timing.GetValue(null)!;
        string configPath = ProjectSettings.GlobalizePath("user://king_mod_skin.cfg");
        byte[]? previousConfig = File.Exists(configPath) ? File.ReadAllBytes(configPath) : null;
        var screen = new Control { Size = new Vector2(1920, 1080) };
        root.AddChild(screen);
        try
        {
            var helper = mod.GetType("Hcxmmx.KingMod.Scripts.KingSelectionSettings", true)!;
            var update = helper.GetMethod("Update", Static)!;
            update.Invoke(null, [screen, false]);
            Require(screen.GetChildCount() == 0, "Other characters do not create settings");
            skin.SetValue(null, true);
            timing.SetValue(null, true);
            update.Invoke(null, [screen, true]);
            update.Invoke(null, [screen, true]);
            Require(screen.GetChildCount() == 1, "Repeated selection keeps one overlay");
            var overlay = screen.GetChild<Control>(0);
            var view = overlay.GetNode<Control>("PanelView");
            Require(view.Size == screen.Size, "Overlay stretches to character screen");
            Require(view.Get("white_king").AsBool() && view.Get("original_timing").AsBool(), "Existing preferences populate controls");
            var gear = view.Get("entry").As<Button>();
            Require(gear.Text == "" && gear.Position.Y >= 100, "Icon entry avoids native act dropdown");
            var rows = view.Get("rows").AsGodotDictionary();
            gear.EmitSignal(Button.SignalName.Pressed);
            Require(view.Get("opened").AsBool(), "Actual gear opens popup");
            rows["blue"].As<Button>().EmitSignal(Button.SignalName.Pressed);
            rows["fast"].As<Button>().EmitSignal(Button.SignalName.Pressed);
            Require(!(bool)skin.GetValue(null)! && !(bool)timing.GetValue(null)!, "Actual signals update both runtime preferences");
            var config = new ConfigFile();
            Require(config.Load("user://king_mod_skin.cfg") == Error.Ok &&
                !config.GetValue("Skin", "IsWhiteKing").AsBool() &&
                !config.GetValue("Presentation", "OriginalTiming").AsBool(), "Real existing config keys are saved");
            rows["white"].As<Button>().EmitSignal(Button.SignalName.Pressed);
            rows["original"].As<Button>().EmitSignal(Button.SignalName.Pressed);
            skin.SetValue(null, false);
            timing.SetValue(null, false);
            globals.GetMethod("LoadConfig", Static)!.Invoke(null, null);
            Require((bool)skin.GetValue(null)! && (bool)timing.GetValue(null)!, "Selections survive config reload");
            var cancel = new InputEventAction { Action = "ui_cancel", Pressed = true };
            view.Call("_input", cancel);
            Require(!view.Get("opened").AsBool(), "Escape closes popup");
            cancel.Pressed = false;
            view.Call("_input", cancel);
            Require(view.Get("cancel_release").AsString() == "", "Escape release is consumed before native back");
            view.Call("set_open", true);
            update.Invoke(null, [screen, false]);
            Require(!overlay.Visible && !view.Get("opened").AsBool(), "Other character hides and closes popup");
            update.Invoke(null, [screen, true]);
            Require(overlay.Visible && !view.Get("opened").AsBool(), "Reselecting King restores collapsed gear");
            view.Call("set_open", true);
            screen.Hide();
            Require(!view.Get("opened").AsBool(), "Leaving character screen closes popup");
            Require(mod.GetType("Hcxmmx.KingMod.Scripts.NCharacterSelectButton_SkinToggle_Patch") == null,
                "Portrait injection is removed");
            Require(screen.GetChildren().Count() == 1, "No extra controls leaked");
            GD.Print("SELECTION_SETTINGS_BRIDGE_PASS: packaged renderer, singleton, saved preferences, real signals/config reload, gear, Escape press/release, character/screen visibility and portrait removal");
        }
        finally
        {
            screen.Free();
            skin.SetValue(null, oldSkin);
            timing.SetValue(null, oldTiming);
            if (previousConfig != null) File.WriteAllBytes(configPath, previousConfig);
            else if (File.Exists(configPath)) File.Delete(configPath);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("King selection settings: " + message);
    }
}
