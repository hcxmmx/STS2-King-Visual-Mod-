using System;
using Godot;
using HarmonyLib;

// ✅ 命名空间已重构为 KingMod
namespace Hcxmmx.KingMod.Scripts; 

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen), "SelectCharacter")]
internal static class NCharacterSelectScreen_SelectCharacter_Patch
{
    private static void Prefix(object characterModel, ref string __state)
    {
        var entryName = KingGlobals.GetCharacterEntry(characterModel);
        if (!string.Equals(entryName, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var characterTraverse = Traverse.Create(characterModel);
        __state = characterTraverse.Property("CharacterSelectSfx").GetValue<string>()
            ?? characterTraverse.Field("CharacterSelectSfx").GetValue<string>();

        try { characterTraverse.Property("CharacterSelectSfx").SetValue(""); } catch { }
        try { characterTraverse.Field("CharacterSelectSfx").SetValue(""); } catch { }
        try { characterTraverse.Field("<CharacterSelectSfx>k__BackingField").SetValue(""); } catch { }
    }

    private static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen __instance, Node charSelectButton, object characterModel, ref string __state)
    {
        var entryName = KingGlobals.GetCharacterEntry(characterModel);
        if (!string.Equals(entryName, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        KingGlobals.VerboseLog("\n====== 🎯 选人界面雷达：侦测到枭首者登场！启动视觉劫持！ ======");

        var instanceTraverse = Traverse.Create(__instance);
        var bgContainer = instanceTraverse.Field("_bgContainer").GetValue<Control>();
        var nameLabel = instanceTraverse.Field("_name").GetValue();
        var descLabel = instanceTraverse.Field("_description").GetValue<RichTextLabel>();

        if (bgContainer != null)
        {
            foreach (Node child in bgContainer.GetChildren())
            {
                if (child is CanvasItem canvasItem)
                {
                    canvasItem.Hide();
                }
            }

            // 🚨 极其优雅的内存提取：不再读硬盘，直接从后台线程拿货！
            if (KingGlobals.KingSelectUiScene == null)
            {
                KingGlobals.KingSelectUiScene = KingGlobals.KingSelectUiPreloaded;
            }

            if (KingGlobals.KingSelectUiScene == null)
            {
                // 如果这里报空，说明玩家手速太快，预加载还没搞完。LoadThreadedGet 会极其智能地等它加载完再返回。
                KingGlobals.KingSelectUiScene = (PackedScene)ResourceLoader.LoadThreadedGet(KingGlobals.KingSelectUiPath);
            }
            
            var kingScreenScene = KingGlobals.KingSelectUiScene;
            
            if (kingScreenScene != null)
            {
                var kingScreen = kingScreenScene.Instantiate<Control>();
                kingScreen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                bgContainer.AddChild(kingScreen);
                KingGlobals.VerboseLog("✅ 狂风骤雨王座背景铺设完毕！");
                
                // 把 kingScreen 加到 bgContainer 之后...
                var voicePlayer = kingScreen.GetNodeOrNull<AudioStreamPlayer>("KingVoicePlayer");
                if (voicePlayer != null)
                {
                     voicePlayer.Play();
                     KingGlobals.VerboseLog("📢 选人语音播报触发！(通过场景锚点极其优雅地播放)");
                }
            }
            else
            {
                KingGlobals.ErrorLog("💥 场景提取失败！请检查 KingSelectUiPath 路径是否拼写正确！");
            }
        }

        if (nameLabel != null)
        {
            // 🎯 修改为细胞人的名字
            var nameTraverse = Traverse.Create(nameLabel);
            nameTraverse.Method("SetTextAutoSize", new object[] { "枭首者" }).GetValue();
        }

        if (descLabel != null)
        {
            // 🎯 修改为极其霸气的专属介绍
            descLabel.Text = "贪婪而傲慢的国王，前来尖塔寻找新的乐子与杀戮。\n带着一把魔剑夜歌";
        }

        if (__state != null)
        {
            var characterTraverse = Traverse.Create(characterModel);
            try { characterTraverse.Property("CharacterSelectSfx").SetValue(__state); } catch { }
            try { characterTraverse.Field("CharacterSelectSfx").SetValue(__state); } catch { }
            try { characterTraverse.Field("<CharacterSelectSfx>k__BackingField").SetValue(__state); } catch { }
        }

        KingGlobals.VerboseLog("🎉 UI 篡改与防崩溃战术静音协议极其完美地执行完毕！");
    }
}

// 🚨 极其关键的预加载钩子：游戏刚进入选人界面时，立刻开后台线程偷跑加载 UI！
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectButton), "Init")]
internal static class NCharacterSelectButton_Init_Patch
{
    private static void Postfix(object __instance, object character)
    {
        var entryName = KingGlobals.GetCharacterEntry(character);
        if (string.IsNullOrEmpty(entryName) || !entryName.Contains(KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 告诉 Godot：趁着玩家还没点，立刻在后台新开一个线程偷偷加载 UI 路径！
        KingGlobals.VerboseLog("\n====== ⏳ 预加载雷达：悄悄把王座UI塞进后台线程... ======");
        if (KingGlobals.KingSelectUiScene == null && KingGlobals.KingSelectUiPreloaded == null)
        {
            ResourceLoader.LoadThreadedRequest(KingGlobals.KingSelectUiPath);
        }
    }
}

// 🔇 头像替换模块（暂未制作头像，已全段注释休眠）

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectButton), "Init")]
internal static class NCharacterSelectButton_Init_Patch_Avatar
{
    private static void Postfix(object __instance, object character)
    {
        var entryName = KingGlobals.GetCharacterEntry(character);
        if (!string.Equals(entryName, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        KingGlobals.VerboseLog("\n====== 🎯 头像雷达：锁定枭首者选人按钮！启动物理换脸！ ======");

        var customAvatar = KingGlobals.KingAvatarTexture;
        if (customAvatar == null)
        {
            KingGlobals.ErrorLog("💥 找不到枭首者的头像图片！");
            return;
        }

        var buttonTraverse = Traverse.Create(__instance);
        var iconNode = buttonTraverse.Field("_icon").GetValue();
        if (iconNode == null)
        {
            return;
        }

        var iconTraverse = Traverse.Create(iconNode);
        iconTraverse.Property("Texture").SetValue(customAvatar);
        KingGlobals.VerboseLog("✅ 枭首者头像极其完美地贴上去了！");
    }
}

// ==========================================
// 🎨 选人界面：终极原生 UI 按钮换肤与动态弹幕
// ==========================================
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectButton), "Init")]
internal static class NCharacterSelectButton_SkinToggle_Patch
{
    private static readonly StringName SkinBtnInjectedKey = new("KingSkinBtnInjected");
    private const string BtnNodeName = "KingSkinToggleBtn";

    private static void Postfix(Control __instance, object character)
    {
        var entryName = KingGlobals.GetCharacterEntry(character);
        if (!string.Equals(entryName, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) return;

        // 双重防重：Meta 与节点名都做检查
        if (__instance.HasMeta(SkinBtnInjectedKey) || __instance.GetNodeOrNull<Button>(BtnNodeName) != null) return;
        __instance.SetMeta(SkinBtnInjectedKey, true);

        var toggleBtn = new Button();
        toggleBtn.Name = BtnNodeName;
        toggleBtn.Flat = true;

        Action updateBtnVisuals = () =>
        {
            toggleBtn.Text = KingGlobals.IsWhiteKingSkin ? "👑切换：白王" : "👑切换：蓝王";
            toggleBtn.AddThemeColorOverride("font_color", KingGlobals.IsWhiteKingSkin ? new Color(1f, 0.9f, 0.5f) : new Color(0.5f, 0.8f, 1f));
        };

        updateBtnVisuals();
        toggleBtn.AddThemeFontSizeOverride("font_size", 16);
        toggleBtn.AddThemeConstantOverride("outline_size", 4);
        toggleBtn.AddThemeColorOverride("font_outline_color", Colors.Black);
        toggleBtn.MouseFilter = Control.MouseFilterEnum.Stop;
        toggleBtn.FocusMode = Control.FocusModeEnum.All;
        toggleBtn.CustomMinimumSize = new Vector2(90f, 30f);

        __instance.AddChild(toggleBtn);

        void RepositionButton()
        {
            toggleBtn.Position = new Vector2(Mathf.Max(6f, __instance.Size.X - toggleBtn.Size.X - 10f), 10f);
        }

        RepositionButton();
        __instance.Resized += RepositionButton;
        toggleBtn.Resized += RepositionButton;

        toggleBtn.Pressed += () =>
        {
            KingGlobals.IsWhiteKingSkin = !KingGlobals.IsWhiteKingSkin;
            KingGlobals.SaveConfig();
            updateBtnVisuals();

            string quote = KingGlobals.IsWhiteKingSkin ? "✨无伤的证明，白王降临！" : "🔵熟悉的味道，重返经典。";
            FireDynamicDanmaku(__instance, quote, KingGlobals.IsWhiteKingSkin);
            toggleBtn.AcceptEvent();
        };
    }

    private static void FireDynamicDanmaku(Control parent, string text, bool isWhiteKing)
    {
        var label = new Label();
        label.Text = text;
        label.ZIndex = 100;

        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", isWhiteKing ? new Color(1f, 0.9f, 0.5f) : new Color(0.5f, 0.8f, 1f));
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);

        parent.AddChild(label);

        float estimatedTextWidth = Mathf.Max(140f, text.Length * 18f);
        label.Position = new Vector2(parent.Size.X / 2f - estimatedTextWidth / 2f, -20f);

        var tween = parent.CreateTween();
        tween.TweenProperty(label, "position:y", label.Position.Y - 50f, 1.2f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "modulate:a", 0f, 1.2f).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenCallback(Callable.From(() => label.QueueFree()));
    }
}
