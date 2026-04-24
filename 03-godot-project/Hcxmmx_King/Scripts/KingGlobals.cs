using System;
using System.Runtime.CompilerServices;
using Godot;

namespace Hcxmmx.KingMod.Scripts;

public static class KingGlobals
{
    public enum RuntimeProfile { Release, Debug }
    public static RuntimeProfile CurrentProfile = RuntimeProfile.Release;
    public static bool EnableVerboseLogs = false;
    public static bool EnableCombatLog = false;
    public static bool IsInShop = false;
    public static bool IsDead = false;
    public static System.Collections.Generic.HashSet<AnimatedSprite2D> ActiveKingSprites = new();

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

    // 💾 赛博档案馆：读取与保存
    public static void LoadConfig()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) == Error.Ok)
        {
            IsWhiteKingSkin = (bool)config.GetValue("Skin", "IsWhiteKing", false);
            VerboseLog($"💾 赛博档案馆读取成功：当前为 {(IsWhiteKingSkin ? "白王" : "蓝王")}");
        }
    }

    public static void SaveConfig()
    {
        var config = new ConfigFile();
        config.SetValue("Skin", "IsWhiteKing", IsWhiteKingSkin);
        config.Save(ConfigPath);
        VerboseLog("💾 赛博档案馆保存成功！");
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
    public static readonly string[] HitPool = { "stun", "blockDashShield", "blockIceShield", "blockLightningShield" };
    public static readonly string[] DiePool = { "letha" };
    
    // ==========================================
    // ⚔️ 皇家连招兵器库 (二维数组：极其严谨的武器分类)
    // ==========================================
    public static readonly string[][] AttackComboPool = { 
        new[] { "AtkKatanaB2", "AtkKatanaB3", "AtkKatanaB4" },             // 武士刀居合三连
        new[] { "AtkKingScepter" },                                        // 权杖单次重击
        new[] { "AtkVampireKillerA", "AtkVampireKillerB", "AtkVampireKillerC" }, // 吸血鬼杀手鞭
        new[] { "atkBroadSwordA", "atkBroadSwordB", "atkBroadSwordC" },    // 阔剑三连
        new[] { "atkPanA", "atkPanB", "atkPanC", "atkPanD" },              // 平底锅四连
        new[] { "atkRapierA", "atkRapierB", "atkRapierC" },                // 刺剑三连
        new[] { "atkScytheA1", "atkScytheA2", "atkScytheB1", "atkScytheB2" }, // 巨镰四段斩
        new[] { "atkTombstoneA3", "atkTombstoneB3", "atkTombstoneE3" },    // 墓碑三连砸
        new[] { "halberdAtkA", "halberdAtkB", "halberdAtkC" },             // 战戟三连
        new[] { "perfectHalberdAtkA", "perfectHalberdAtkB", "perfectHalberdAtkC", "perfectHalberdAtkD" } // 完美战戟四连
    };

    // --- 连招记忆芯片 ---
    public static string[]? CurrentCombo = null;
    public static int CurrentComboIndex = 0;

    public static void ResetCombo()
    {
        CurrentCombo = null;
        CurrentComboIndex = 0;
    }

    // ==========================================
    // 🎇 物理特效锁孔映射 (VFX Mapping)
    // 根据长官的美术清单，将本体动作极其精准地映射到特效节点！
    // ==========================================
    public static readonly System.Collections.Generic.Dictionary<string, string> AttackVfxMap = new()
    {
        { "AtkKatanaB2", "fxBeheadedKatanaB2" },
        { "AtkKatanaB3", "fxBeheadedKatanaB3" },
        { "AtkKatanaB4", "fxBeheadedKatanaB4" },

        { "AtkVampireKillerA", "fxAtkVampireKillerA" },
        { "AtkVampireKillerB", "fxAtkVampireKillerB" },
        { "AtkVampireKillerC", "fxAtkVampireKillerC" },

        { "atkBroadSwordA", "fxAtkBroadSwordA" },
        { "atkBroadSwordB", "fxAtkBroadSwordB" },
        { "atkBroadSwordC", "fxAtkBroadSwordC" },

        { "atkPanA", "fxAtkPanA" },
        { "atkPanB", "fxAtkPanB" },
        { "atkPanC", "fxAtkPanC" },
        { "atkPanD", "fxAtkPanD" },

        { "atkRapierA", "fxRapierA" },
        { "atkRapierB", "fxRapierB" },
        { "atkRapierC", "fxRapierC" },

        { "atkScytheA1", "fxAtkScytheA1" },
        { "atkScytheA2", "fxAtkScytheA2" },
        { "atkScytheB1", "fxAtkScytheB1" },
        { "atkScytheB2", "fxAtkScytheB2" },

        // 🚨 赛博修复：墓碑的本体和特效命名不一致，这里通过映射强行锁死！
        { "atkTombstoneA3", "fxTombestoneAtkA" },
        { "atkTombstoneB3", "fxTombestoneAtkB" },
        { "atkTombstoneE3", "fxTombestoneAtkC" },

        { "halberdAtkA", "halberdFxAtkA" },
        { "halberdAtkB", "halberdFxAtkB" },
        { "halberdAtkC", "halberdFxAtkC" },

        { "perfectHalberdAtkA", "fxPerfectHalberdAtkA" },
        { "perfectHalberdAtkB", "fxPerfectHalberdAtkB" },
        { "perfectHalberdAtkC", "fxPerfectHalberdAtkC" },
        { "perfectHalberdAtkD", "fxPerfectHalberdAtkD" }
    };

    // --- 音效库保留 ---
    public static readonly string[] IntroVoicePool = { };
    public static readonly string[] AttackVoicePool = { };
    public static readonly string[] HitVoicePool = { };
    public static readonly string[] CastVoicePool = { };

    // ==========================================
    // 🗡️ 夜歌形态监视器与【原汁原味】弹幕台词库
    // ==========================================
    public static bool IsSwordForged = false; 

    // 💤 闲置/无聊/施法吐槽 (原版极度渴望鲜血且鄙视玩家)
    public static readonly string[] SerenadeIdleQuotes = { 
        "希望你给我准备好了食物。",
        "怎么感觉这岛和过去不太一样了啊……嗯，一定是我的错觉。",
        "超～无~聊……",
        "说好的战斗呢？",
        "有什么事再叫醒我。",
        "谢天谢地我没有嗅觉。",
        "至少这里的风景还不错。"
    };

    // ⚔️ 攻击/斩杀宣言 (原版极其嗜血)
    public static readonly string[] SerenadeAttackQuotes = { 
        "嗨，快打点什么东西啊！",
        "继续狩猎吧！",
        "好啊！杀了他们！",
        "刚才这个怪物有点辣辣的……",
        "怪物在哪里呀！！",
        "来！杀啊！"
    };

    // 💥 挨打吐槽 (原版夜歌挨打或掉落时会骂人)
    public static readonly string[] SerenadeHitQuotes = { 
        "闪开啊笨蛋！你想把我也弄断吗！",
        "除了我，谁准你们碰他的？！",
        "为什么挨打的是我们！？快点杀回去！",
        "别用你的肉块身体来挡刀啊！",
        "喂！他们弄脏我了！杀了他们！",
        "你的走位超～无~聊……",
        "保护我啊！你这没用的躯壳！"
    };

    // 🗡️ 铸剑/召唤登场 (极其霸气的现身)
    public static readonly string[] SerenadeForgedQuotes = { 
        "又给我准备了新食物吗？真乖！", 
        "这股力量……让我们继续狩猎吧！",
        "既然把我叫醒了，就多杀几个吧！",
        "我们又要在一起杀戮了吗？太棒了！",
        "变重了……拿稳点，别把我摔了！",
        "更多……我还要更多的力量来撕碎他们！",
        "这才对嘛！现在，快带我去切点什么！"
    };
    private static readonly System.Collections.Generic.Dictionary<string, AudioStream> AudioStreamCache = new(System.StringComparer.Ordinal);
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

    public static AudioStream? GetAudioStreamCached(string? resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;
        if (AudioStreamCache.TryGetValue(resourcePath, out var cached)) return cached;
        var loaded = ResourceLoader.Load<AudioStream>(resourcePath);
        if (loaded != null) AudioStreamCache[resourcePath] = loaded;
        return loaded;
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
    public static void ToggleSkin(Node2D kingCharacterNode)
{
    // 切换状态
    IsWhiteKingSkin = !IsWhiteKingSkin;
    Texture2D targetPalette = IsWhiteKingSkin ? PaletteWhiteKing : PaletteDefault;
    string skinName = IsWhiteKingSkin ? "白王 (无伤者)" : "蓝王 (默认)";

    // 1. 抓取角色身上的 AnimatedSprite2D（也就是套了Shader的那个节点）
    // 请根据你实际的节点名字修改，比如 "Sprite" 或 "BodySprite"
    var sprite = FindFirstNode<AnimatedSprite2D>(kingCharacterNode, n => n.Name == "BodySprite"); 
    
    if (sprite != null && sprite.Material is ShaderMaterial shaderMat)
    {
        // 2. 极其暴力的参数覆写！
        // 把 Shader 里的 "palette_lut" 变量，极其强硬地塞入新的贴图
        shaderMat.SetShaderParameter("palette_lut", targetPalette);
        
        // 3. （可选）如果你想让两套皮肤的流光颜色也不一样，可以极其细腻地在这里一起改！
        if (IsWhiteKingSkin) {
            // 比如白王皮肤，流光变成极其神圣的金色和白色
            shaderMat.SetShaderParameter("glow_color_1", new Color(1.0f, 0.9f, 0.5f)); // 金色
            shaderMat.SetShaderParameter("glow_color_2", new Color(1.0f, 1.0f, 1.0f)); // 纯白
            shaderMat.SetShaderParameter("body_glow_color", new Color(1.0f, 1.0f, 1.0f)); 
        } else {
            // 恢复默认的蓝色/黄色
            shaderMat.SetShaderParameter("glow_color_1", new Color(1.0f, 0.82f, 0.2f)); 
            shaderMat.SetShaderParameter("glow_color_2", new Color(0.2f, 0.8f, 1.0f)); 
            shaderMat.SetShaderParameter("body_glow_color", new Color(0.8f, 0.9f, 1.0f)); 
        }

        CombatLog($"🎨 已极其完美地切换至皮肤：{skinName}");
    }
}

// ==========================================
// 🎨 换肤协议：将当前持久化的皮肤应用到目标节点
// ==========================================
public static void ApplyCurrentSkin(Node2D mechaNode)
{
    // 1. 找到那个叫 Body 的 AnimatedSprite2D 节点
    var sprite = FindFirstNode<AnimatedSprite2D>(mechaNode, n => n.Name == "Body");
    if (sprite != null && sprite.Material is ShaderMaterial shaderMat)
    {
        // 🚨 极其关键：防止资源冲突，进行副本操作
        shaderMat = (ShaderMaterial)shaderMat.Duplicate();
        sprite.Material = shaderMat;

        // 2. 根据全局变量决定用哪张图
        Texture2D targetPalette = IsWhiteKingSkin ? PaletteWhiteKing : PaletteDefault;
        shaderMat.SetShaderParameter("palette_lut", targetPalette);

        // 3. 适配流光颜色
        if (IsWhiteKingSkin) {
            shaderMat.SetShaderParameter("glow_color_1", new Color(1.0f, 0.9f, 0.5f)); // 金白
            shaderMat.SetShaderParameter("glow_color_2", new Color(1.0f, 1.0f, 1.0f)); 
        } else {
            shaderMat.SetShaderParameter("glow_color_1", new Color(1.0f, 0.82f, 0.2f)); // 蓝黄
            shaderMat.SetShaderParameter("glow_color_2", new Color(0.2f, 0.8f, 1.0f)); 
        }
    }
}
}

