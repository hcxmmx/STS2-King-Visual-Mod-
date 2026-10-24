using Godot;

namespace Hcxmmx.KingMod.Scripts;

/// <summary>Shared background mounting for multiplayer load screens.</summary>
internal static class KingSelectBackground
{
    internal static void Apply(Control? container, bool hasBackgroundContainer)
    {
        if (container == null) return;

        if (hasBackgroundContainer)
        {
            foreach (Node child in container.GetChildren())
            {
                if (child is CanvasItem canvasItem) canvasItem.Hide();
            }
        }
        else
        {
            container.GetNodeOrNull<CanvasItem>("StaticBg")?.Hide();
            container.GetNodeOrNull<CanvasItem>("AnimatedBg")?.Hide();
        }

        var existingScreen = container.GetNodeOrNull<Control>("KingSelectUI_MP");
        if (existingScreen != null)
        {
            existingScreen.Show();
            return;
        }

        KingGlobals.KingSelectUiScene ??= KingGlobals.KingSelectUiPreloaded;
        KingGlobals.KingSelectUiScene ??= (PackedScene)ResourceLoader.LoadThreadedGet(KingGlobals.KingSelectUiPath);
        if (KingGlobals.KingSelectUiScene == null) return;

        var screen = KingGlobals.KingSelectUiScene.Instantiate<Control>();
        screen.Name = "KingSelectUI_MP";
        screen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        container.AddChild(screen);
        if (!hasBackgroundContainer) container.MoveChild(screen, 0);
        screen.GetNodeOrNull<AudioStreamPlayer>("KingVoicePlayer")?.Play();
    }
}
