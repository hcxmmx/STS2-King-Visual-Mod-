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
// 🎨 选人界面：右键换肤雷达与动态弹幕引擎
// ==========================================
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectButton), "Init")]
internal static class NCharacterSelectButton_SkinToggle_Patch
{
    private static readonly StringName SkinToggleBoundKey = new("KingSkinToggleBound");
    private static readonly StringName TouchStartTimeKey = new("KingTouchStartMs");
    private static readonly StringName TouchingKey = new("KingIsTouching");
    private static readonly StringName TouchIndexKey = new("KingTouchIndex");
    private static readonly StringName TouchStartPosKey = new("KingTouchStartPos");

    private const ulong LongPressThresholdMs = 650;
    private const float LongPressMoveTolerancePx = 24f;

    private static void Postfix(Control __instance, object character)
    {
        var entryName = KingGlobals.GetCharacterEntry(character);
        if (!string.Equals(entryName, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) return;

        if (__instance.HasMeta(SkinToggleBoundKey)) return;
        __instance.SetMeta(SkinToggleBoundKey, true);

        // 极其暴力的底层拦截：监听这个 Control 节点的所有输入
        __instance.GuiInput += (InputEvent @event) =>
        {
            // 🎯 通道 1：鼠标右键按下瞬间切换
            if (@event is InputEventMouseButton rightClick && rightClick.ButtonIndex == MouseButton.Right)
            {
                if (rightClick.Pressed)
                {
                    ExecuteSkinToggle(__instance);
                }
                return;
            }

            // 🎯 通道 2：触屏/左键长按（按住>=阈值，且松开位置仍在按钮区域内）
            bool isPressEvent = false;
            bool isReleaseEvent = false;
            int pointerIndex = -1;
            Vector2 eventPos = Vector2.Zero;

            if (@event is InputEventScreenTouch touch)
            {
                isPressEvent = touch.Pressed;
                isReleaseEvent = !touch.Pressed;
                pointerIndex = touch.Index;
                eventPos = touch.Position;
            }
            else if (@event is InputEventMouseButton leftClick && leftClick.ButtonIndex == MouseButton.Left)
            {
                isPressEvent = leftClick.Pressed;
                isReleaseEvent = !leftClick.Pressed;
                pointerIndex = -1;
                eventPos = leftClick.GlobalPosition;
            }
            else
            {
                return;
            }

            if (isPressEvent)
            {
                __instance.SetMeta(TouchStartTimeKey, Time.GetTicksMsec());
                __instance.SetMeta(TouchingKey, true);
                __instance.SetMeta(TouchIndexKey, pointerIndex);
                __instance.SetMeta(TouchStartPosKey, eventPos);
                return;
            }

            if (!isReleaseEvent) return;
            if (!__instance.HasMeta(TouchingKey) || !__instance.GetMeta(TouchingKey).AsBool()) return;

            int trackedIndex = __instance.HasMeta(TouchIndexKey) ? (int)__instance.GetMeta(TouchIndexKey).AsInt64() : -2;
            if (trackedIndex != pointerIndex)
            {
                return;
            }

            __instance.SetMeta(TouchingKey, false);

            ulong startMs = __instance.HasMeta(TouchStartTimeKey) ? (ulong)__instance.GetMeta(TouchStartTimeKey).AsInt64() : 0;
            ulong duration = Time.GetTicksMsec() - startMs;

            Vector2 startPos = __instance.HasMeta(TouchStartPosKey) ? __instance.GetMeta(TouchStartPosKey).AsVector2() : eventPos;
            float movedDistance = startPos.DistanceTo(eventPos);

            bool insideOnRelease = __instance.GetGlobalRect().HasPoint(eventPos);
            bool isLongPress = duration >= LongPressThresholdMs;
            bool withinMoveTolerance = movedDistance <= LongPressMoveTolerancePx;

            if (isLongPress && withinMoveTolerance && insideOnRelease)
            {
                ExecuteSkinToggle(__instance);
            }
        };
    }

    private static void ExecuteSkinToggle(Control parent)
    {
        // 1. 切换状态并存档
        KingGlobals.IsWhiteKingSkin = !KingGlobals.IsWhiteKingSkin;
        KingGlobals.SaveConfig();

        // 2. 准备弹幕
        string quote = KingGlobals.IsWhiteKingSkin ? "✨ 无伤的证明，白王降临！" : "🔵 熟悉的味道，重返经典。";

        // 3. 发射动态弹幕
        FireDynamicDanmaku(parent, quote, KingGlobals.IsWhiteKingSkin);

        // 4. 标记事件已处理，防止事件穿透
        parent.AcceptEvent();
    }

    private static void FireDynamicDanmaku(Control parent, string text, bool isWhiteKing)
    {
        // 代码级虚空造物！不需要任何预设节点！
        var label = new Label();
        label.Text = text;

        // 极其细腻的美术微调
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", isWhiteKing ? new Color(1f, 0.9f, 0.5f) : new Color(0.5f, 0.8f, 1f));
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);

        parent.AddChild(label);

        // 初始位置设在按钮的正上方
        label.Position = new Vector2(parent.Size.X / 2f - 100f, -30f);

        // 极其丝滑的升空与消散动画
        var tween = parent.CreateTween();
        tween.TweenProperty(label, "position:y", label.Position.Y - 60f, 1.2f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "modulate:a", 0f, 1.2f).SetTrans(Tween.TransitionType.Cubic);

        // 极其干净的内存回收
        tween.TweenCallback(Callable.From(() => label.QueueFree()));
    }
}
