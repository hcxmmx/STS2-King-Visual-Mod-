using System;
using Godot;

namespace Hcxmmx.KingMod.Scripts;

public static class KingGlobals
{
    public enum RuntimeProfile { Release, Debug }
    public static RuntimeProfile CurrentProfile = RuntimeProfile.Release;
    public static bool EnableVerboseLogs = false;
    public static bool EnableCombatLog = false;
    public static bool IsInShop = false;
    public static System.Collections.Generic.HashSet<AnimatedSprite2D> ActiveKingSprites = new();

    // Skin Changer scans this field to identify the runtime skin provider. Keep it explicit.
    public const string TargetCharacterId = "REGENT";
    public const string KingScenePath = "res://Hcxmmx_King/tscn/white_king.tscn";
    public const string SerenadeScenePath = "res://Hcxmmx_King/tscn/SerenadeMecha.tscn";
    public const string KingSelectUiPath = "res://Hcxmmx_King/tscn/KingSelectUI.tscn";
    public const string KingAvatarPath = "res://Hcxmmx_King/Asset/King_Avatar.png";
    public const string TopBarIconPath = "res://Hcxmmx_King/Asset/King_MiniIcon.png";
    public const string TopBarOutlinePath = "res://Hcxmmx_King/Asset/King_MiniIcon_White.png";
    public const string HarmonyId = "sts2.hcxmmx.king.visuals";

    public static readonly Random Rng = new Random();
    public static readonly PackedScene? KingScenePreloaded = ResourceLoader.Load<PackedScene>(KingScenePath);
    public static readonly PackedScene? SerenadeScenePreloaded = ResourceLoader.Load<PackedScene>(SerenadeScenePath);
    public static readonly PackedScene? KingSelectUiPreloaded = ResourceLoader.Load<PackedScene>(KingSelectUiPath);
    public static readonly Texture2D? KingAvatarTexture = ResourceLoader.Load<Texture2D>(KingAvatarPath);
    public static readonly Texture2D? TopBarIconTexture = ResourceLoader.Load<Texture2D>(TopBarIconPath);
    public static readonly Texture2D? TopBarOutlineTexture = ResourceLoader.Load<Texture2D>(TopBarOutlinePath);

    public static PackedScene? KingScene = KingScenePreloaded;
    public static PackedScene? KingSelectUiScene;

    public static Texture2D PaletteDefault = ResourceLoader.Load<Texture2D>("res://Hcxmmx_King/Shaders/king_default_s.png");
    public static Texture2D PaletteWhiteKing = ResourceLoader.Load<Texture2D>("res://Hcxmmx_King/Shaders/king_whiteKing_s.png");
    private const string ConfigPath = "user://king_mod_skin.cfg";

// 极其关键的状态位，记录当前穿的是哪套
    public static bool IsWhiteKingSkin = false;
    public static bool UseOriginalTiming = false;

    // 💾 赛博档案馆：读取与保存
    public static void LoadConfig()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) == Error.Ok)
        {
            IsWhiteKingSkin = (bool)config.GetValue("Skin", "IsWhiteKing", false);
            UseOriginalTiming = (bool)config.GetValue("Presentation", "OriginalTiming", false);
            VerboseLog($"💾 赛博档案馆读取成功：当前为 {(IsWhiteKingSkin ? "白王" : "蓝王")}");
        }
    }

    public static void SaveConfig()
    {
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue("Skin", "IsWhiteKing", IsWhiteKingSkin);
        config.SetValue("Presentation", "OriginalTiming", UseOriginalTiming);
        config.Save(ConfigPath);
        VerboseLog("💾 赛博档案馆保存成功！");
    }

    public static void ToggleTiming()
    {
        UseOriginalTiming = !UseOriginalTiming;
        CleanupActiveKingSprites();
        foreach (var sprite in ActiveKingSprites)
            if (sprite.GetParent() is Node2D actor) KingPresentation.RefreshTiming(actor);
        SaveConfig();
    }

    // ==========================================
    // 🎯 赛博雷达模块：长官亲写的极其完美的深层侦测器
    // ==========================================
    public static string? GetCharacterEntry(object? model)
    {
        if (model == null) return null;

        var modelTraverse = HarmonyLib.Traverse.Create(model);
        var idObj = modelTraverse.Property("Id").GetValue()
            ?? modelTraverse.Field("Id").GetValue();
        if (idObj == null) return null;

        var idTraverse = HarmonyLib.Traverse.Create(idObj);
        return idTraverse.Property("Entry").GetValue<string>()
            ?? idTraverse.Field("Entry").GetValue<string>();
    }

    // ==========================================
    // 👑 皇家常规动作池 (长官的精准分类)
    // ==========================================
    public static readonly string[] IntroPool = { "runB", "show", "walk" };
    public static readonly string[] VictoryPool = { "no", "travolta", "what" };
    public static readonly string[] CastPool = { "fuckOffFast", "moonwalk", "ohYes", "yes", "secretPortal", "spanking" };
    public static readonly string[] DiePool = { "letha" };

    public static bool IsSwordForged = false;

    private static readonly System.Collections.Generic.List<AnimatedSprite2D> InvalidSpriteBuffer = new();

    public static T? FindFirstNode<T>(Node root, Func<T, bool>? predicate = null) where T : Node
    {
        var queue = new System.Collections.Generic.Queue<Node>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is T matched && (predicate == null || predicate(matched))) return matched;
            for (int i = 0; i < current.GetChildCount(); i++) queue.Enqueue(current.GetChild(i));
        }
        return null;
    }

    public static void CleanupActiveKingSprites()
    {
        if (ActiveKingSprites.Count == 0) return;
        InvalidSpriteBuffer.Clear();
        foreach (var sprite in ActiveKingSprites)
        {
            if (!GodotObject.IsInstanceValid(sprite)) InvalidSpriteBuffer.Add(sprite);
        }
        for (int i = 0; i < InvalidSpriteBuffer.Count; i++) ActiveKingSprites.Remove(InvalidSpriteBuffer[i]);
        InvalidSpriteBuffer.Clear();
    }

    public static void VerboseLog(string message)
    {
        if (EnableVerboseLogs) GD.Print(message);
    }

    public static void VerboseLog(Func<string> messageFactory)
    {
        if (EnableVerboseLogs) GD.Print(messageFactory());
    }

    public static void ApplyRuntimeProfile()
    {
        EnableVerboseLogs = CurrentProfile == RuntimeProfile.Debug;
        EnableCombatLog = CurrentProfile == RuntimeProfile.Debug;
    }

    public static void ErrorLog(string message)
    {
        if (EnableVerboseLogs) GD.PushError(message);
    }

    public static void CombatLog(string message)
    {
        if (EnableCombatLog)
        {
            // 使用绿色前缀，让它在茫茫日志里极其好找！
            GD.Print($"[King_Combat_Radar] 📡 {message}");
        }
    }

    public static void CombatLog(Func<string> messageFactory)
    {
        if (EnableCombatLog)
        {
            GD.Print($"[King_Combat_Radar] 📡 {messageFactory()}");
        }
    }
    /// <summary>Apply the saved palette to a private material instance.</summary>
    public static void ApplyCurrentSkin(Node2D mechaNode)
    {
        var sprite = FindFirstNode<AnimatedSprite2D>(mechaNode, n => n.Name == "Body");
        if (sprite?.Material is not ShaderMaterial material) return;

        material = (ShaderMaterial)material.Duplicate();
        sprite.Material = material;
        KingPresentation.RefreshSkin(mechaNode);
        material.SetShaderParameter("palette_lut", IsWhiteKingSkin ? PaletteWhiteKing : PaletteDefault);
        material.SetShaderParameter("glow_color_1", IsWhiteKingSkin
            ? new Color(1.0f, 0.9f, 0.5f) : new Color(1.0f, 0.82f, 0.2f));
        material.SetShaderParameter("glow_color_2", IsWhiteKingSkin
            ? Colors.White : new Color(0.2f, 0.8f, 1.0f));
    }
}
