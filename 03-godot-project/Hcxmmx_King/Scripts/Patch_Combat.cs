using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Hcxmmx.KingMod.Scripts;

// ==========================================
// 🛡️ 节点缓存系统 (新增了特效节点的双轨缓存)
// ==========================================
internal static class KingCombatNodeCache
{
    private static readonly StringName SpriteKey = new("KingSpriteRef");
    private static readonly StringName VfxKey = new("KingVfxRef");

    internal static void Store(Node2D mecha, AnimatedSprite2D? sprite, AnimatedSprite2D? vfx)
    {
        if (sprite != null) mecha.SetMeta(SpriteKey, sprite);
        if (vfx != null) mecha.SetMeta(VfxKey, vfx);
    }

    internal static AnimatedSprite2D? GetSprite(Node2D mecha)
    {
        var sprite = GetCachedNode<AnimatedSprite2D>(mecha, SpriteKey);
        if (sprite != null) return sprite;

        sprite = KingGlobals.FindFirstNode<AnimatedSprite2D>(mecha);
        if (sprite != null) mecha.SetMeta(SpriteKey, sprite);
        return sprite;
    }

    internal static AnimatedSprite2D? GetVfx(Node2D mecha)
    {
        var vfx = GetCachedNode<AnimatedSprite2D>(mecha, VfxKey);
        if (vfx != null) return vfx;

        // 🚨 极其关键：通过你提供的名字寻找武器特效节点！
        vfx = KingGlobals.FindFirstNode<AnimatedSprite2D>(mecha, n => n.Name == "VFX_Slash");
        if (vfx != null) mecha.SetMeta(VfxKey, vfx);
        return vfx;
    }

    private static T? GetCachedNode<T>(Node2D mecha, StringName key) where T : Node
    {
        if (!mecha.HasMeta(key)) return null;
        var cachedObject = mecha.GetMeta(key).AsGodotObject();
        return cachedObject is T node && GodotObject.IsInstanceValid(node) ? node : null;
    }
}

// ==========================================
// 👑 活化阶段：强行挂载国王机甲与幼年期夜歌
// ==========================================
[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class NCreature_Ready_Patch
{
    private static void Postfix(NCreature __instance)
    {
        KingGlobals.IsDead = false;
        KingGlobals.IsInShop = false;
        KingGlobals.ResetCombo(); // 进场清空连击缓存
        KingGlobals.IsSwordForged = false; // 🚨 极其关键：进场必定是幼年期图标形态！
        
        KingGlobals.VerboseLog(() => $"\n---> King Project: 侦测到 NCreature 试图活化！节点名称 = {__instance.Name} <---");

        var scene = KingGlobals.KingScene ?? KingGlobals.KingScenePreloaded;
        KingGlobals.KingScene = scene;

        if (scene == null || __instance.Entity == null) return;

        var player = __instance.Entity.Player;
        if (player == null || !string.Equals(player.Character?.Id?.Entry, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) return;

        var visuals = __instance.Visuals;
        if (visuals == null) return;

        KingGlobals.VerboseLog("====== 突破所有防线！强行夺舍储君 (REGENT)！ ======");
        
        var originalBody = visuals.GetNodeOrNull<Node2D>("%Visuals");
        originalBody?.Hide();

        // 🚨 第一步：先将国王的肉体极其华丽地召唤出来！
        var kingNode = scene.Instantiate<Node2D>();
        if (kingNode == null) return;

        kingNode.Name = "KingMecha";
        visuals.AddChild(kingNode);
        kingNode.Position = Vector2.Zero; 
        kingNode.Scale = new Vector2(4.0f, 4.0f); // 保持长官极其霸气的4倍巨大化
        kingNode.Visible = true;

        // 🚨 换肤协议：寻找 Body 节点并强行注入 LUT 贴图！
        var kingBodyNode = KingGlobals.FindFirstNode<AnimatedSprite2D>(kingNode, n => n.Name == "Body");
        if (kingBodyNode != null && kingBodyNode.Material is ShaderMaterial shaderMat)
        {
            // 极其安全的资源局部化，防止误伤无辜
            shaderMat = (ShaderMaterial)shaderMat.Duplicate();
            kingBodyNode.Material = shaderMat;

            Texture2D targetPalette = KingGlobals.IsWhiteKingSkin ? KingGlobals.PaletteWhiteKing : KingGlobals.PaletteDefault;
            shaderMat.SetShaderParameter("palette_lut", targetPalette);

            // 极其细腻的流光适配
            if (KingGlobals.IsWhiteKingSkin)
            {
                shaderMat.SetShaderParameter("glow_color_1", new Color(1.0f, 0.9f, 0.5f)); // 金色流光
                shaderMat.SetShaderParameter("glow_color_2", new Color(1.0f, 1.0f, 1.0f)); // 纯白
            }
            else
            {
                shaderMat.SetShaderParameter("glow_color_1", new Color(1.0f, 0.82f, 0.2f)); // 原版黄
                shaderMat.SetShaderParameter("glow_color_2", new Color(0.2f, 0.8f, 1.0f));  // 原版蓝
            }
            KingGlobals.VerboseLog(() => $"🎨 战术皮肤已实装：{(KingGlobals.IsWhiteKingSkin ? "白王" : "蓝王")}");
        }

        var kingSprite = KingGlobals.FindFirstNode<AnimatedSprite2D>(kingNode);
        var kingVfx = KingGlobals.FindFirstNode<AnimatedSprite2D>(kingNode, n => n.Name == "VFX_Slash");
        
        KingCombatNodeCache.Store(kingNode, kingSprite, kingVfx);

        // 特效节点隐形待命协议
        if (kingVfx != null)
        {
            kingVfx.Visible = false;
            kingVfx.AnimationFinished += () => { kingVfx.Visible = false; kingVfx.Stop(); };
        }

        if (kingSprite != null)
        {
            KingGlobals.CleanupActiveKingSprites();
            KingGlobals.ActiveKingSprites.Add(kingSprite);

            // 动画播放完毕自动归位待机
            kingSprite.AnimationFinished += () =>
            {
                if (kingSprite.Animation != "Idle" && kingSprite.Animation != "letha" && kingSprite.Animation != "travolta" && kingSprite.Animation != "what" && kingSprite.Animation != "no")
                {
                    kingSprite.Play("Idle"); 
                    kingNode.Position = Vector2.Zero;
                }
            };

            // 极其嚣张的入场
            string chosenIntro = KingGlobals.IntroPool[KingGlobals.Rng.Next(KingGlobals.IntroPool.Length)];
            kingSprite.Play(chosenIntro);
        }

        // ==========================================
        // 🚨 第二步：等肉体组装完毕，给幼年夜歌注入上下浮动的灵魂！
        // ==========================================
        var pet = kingNode.GetNodeOrNull<Node2D>("SerenadePet");
        if (pet != null)
        {
            pet.Visible = true;
            var floatTween = pet.CreateTween().SetLoops();
            float baseY = pet.Position.Y;
            floatTween.TweenProperty(pet, "position:y", baseY - 15f, 1.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            floatTween.TweenProperty(pet, "position:y", baseY, 1.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }

        // 物理朝向矫正雷达
        var syncTimer = new Godot.Timer();
        syncTimer.Name = "KingDirectionRadar";
        syncTimer.WaitTime = 0.05f;
        syncTimer.Autostart = true;
        kingNode.AddChild(syncTimer);

        Node2D? bodyRef = visuals.GetNodeOrNull<Node2D>("%Visuals");
        Node2D? kingRef = kingNode;

        syncTimer.Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(bodyRef) || !GodotObject.IsInstanceValid(kingRef))
            {
                syncTimer.Stop();
                syncTimer.QueueFree();
                return;
            }
            float targetSign = Mathf.Sign(bodyRef.Scale.X);
            float currentSign = Mathf.Sign(kingRef.Scale.X);
            if (targetSign != currentSign && targetSign != 0)
            {
                float absX = Mathf.Abs(kingRef.Scale.X);
                kingRef.Scale = new Vector2(absX * targetSign, kingRef.Scale.Y);
            }
        };
    }
}

// ==========================================
// ⚔️ 战斗指令劫持：动态连段与特效引爆器！
// ==========================================
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class NCreature_SetAnimationTrigger_Patch
{
    // 🚨 极其关键的“侦察兵”：在逻辑执行前截获指令
    private static void Prefix(NCreature __instance, string trigger)
    {
        // 只检查咱们夺舍的角色
        var player = __instance?.Entity?.Player;
        if (player == null || !string.Equals(player.Character?.Id?.Entry, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) return;

        // 记录原始指令！这就是长官要的“查找功能”
        // 当你在游戏里打出“飞刀”或者“攻击”时，这里会极其精准地打印出对应的字符串
        KingGlobals.CombatLog(() => $"监测到动作信号波：>>> {trigger} <<<");
    }
    private static void Postfix(NCreature __instance, string trigger)
    {
        if (KingGlobals.IsInShop || KingGlobals.IsDead) return;

        var player = __instance?.Entity?.Player;
        if (player == null || !string.Equals(player.Character?.Id?.Entry, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) return;

        var visuals = __instance?.Visuals;
        var kingMecha = visuals?.GetNodeOrNull<Node2D>("KingMecha");
        if (kingMecha == null) return;

        var kingSprite = KingCombatNodeCache.GetSprite(kingMecha);
        if (kingSprite == null) return;

        KingGlobals.VerboseLog(() => $"---> King Project: 收到动作指令: {trigger} <---");

        switch (trigger)
        {
            case "Attack":
            case "AttackSingle":
            case "AttackTriple":
            {
                // 1. 如果没存货，或者上套连招打完，随机拔出一把新武器！
                if (KingGlobals.CurrentCombo == null || KingGlobals.CurrentComboIndex >= KingGlobals.CurrentCombo.Length)
                {
                    KingGlobals.CurrentCombo = KingGlobals.AttackComboPool[KingGlobals.Rng.Next(KingGlobals.AttackComboPool.Length)];
                    KingGlobals.CurrentComboIndex = 0;
                }

                // 2. 拔出当前段数的物理攻击动作
                string currentAtkFrame = KingGlobals.CurrentCombo[KingGlobals.CurrentComboIndex];
                
                kingSprite.Stop();
                kingSprite.Play(currentAtkFrame);
                
                // ==========================================
                // 🎇 极其致命的刀光引爆逻辑
                // ==========================================
                var vfxSprite = KingCombatNodeCache.GetVfx(kingMecha);
                if (vfxSprite != null && KingGlobals.AttackVfxMap.TryGetValue(currentAtkFrame, out var mappedVfx))
                {
                    vfxSprite.Visible = true;
                    vfxSprite.Stop();
                    vfxSprite.Play(mappedVfx);
                    KingGlobals.VerboseLog(() => $"🎇 连携引爆: 动作 [{currentAtkFrame}] 映射刀光 -> [{mappedVfx}]");
                }

                KingGlobals.VerboseLog(() => $"💥 连段斩击: {currentAtkFrame} (第 {KingGlobals.CurrentComboIndex + 1}/{KingGlobals.CurrentCombo.Length} 段)");
                KingGlobals.CurrentComboIndex++;
                
                kingMecha.Position = Vector2.Zero;

                // ==========================================
                // 💬 夜歌傲娇弹幕：攻击吐槽！(仅在连招第一击触发，防刷屏)
                // ==========================================
                if (KingGlobals.CurrentComboIndex == 1) 
                {
                    DanmakuEngine.Fire(KingGlobals.SerenadeAttackQuotes[KingGlobals.Rng.Next(KingGlobals.SerenadeAttackQuotes.Length)]);
                }
                break;
            }
            case "Hit":
            {
                KingGlobals.ResetCombo(); // 🚨 受击打断连击！
                string chosenHit = KingGlobals.HitPool[KingGlobals.Rng.Next(KingGlobals.HitPool.Length)];
                kingSprite.Stop();
                kingSprite.Play(chosenHit);
                kingMecha.Position = Vector2.Zero;

                // ==========================================
                // 💬 夜歌傲娇弹幕：挨打吐槽！
                // ==========================================
                DanmakuEngine.Fire(KingGlobals.SerenadeHitQuotes[KingGlobals.Rng.Next(KingGlobals.SerenadeHitQuotes.Length)]);
                break;
            }
            case "Cast":
            {
                KingGlobals.ResetCombo(); // 🚨 施法打断连击！
                string chosenCast = KingGlobals.CastPool[KingGlobals.Rng.Next(KingGlobals.CastPool.Length)];
                kingSprite.Stop();
                kingSprite.Play(chosenCast);
                kingMecha.Position = Vector2.Zero;

                // ==========================================
                // 💬 夜歌傲娇弹幕：施法/无聊吐槽！
                // ==========================================
                DanmakuEngine.Fire(KingGlobals.SerenadeIdleQuotes[KingGlobals.Rng.Next(KingGlobals.SerenadeIdleQuotes.Length)]);
                break;
            }
            case "Die":
            case "Death":
            case "Dead":
                KingGlobals.IsDead = true;
                KingGlobals.ResetCombo();
                string chosenDie = KingGlobals.DiePool[0]; 
                kingSprite.Stop();
                kingSprite.Play(chosenDie);
                kingMecha.Position = Vector2.Zero;
                break;
            default:
                KingGlobals.ResetCombo();
                kingSprite.Play("Idle"); 
                kingMecha.Position = Vector2.Zero;
                break;
        }
    }
}

// ==========================================
// 💬 夜歌赛博弹幕通讯引擎 (抗崩坏安全版)
// ==========================================
internal static class DanmakuEngine
{
    public static void Fire(string text)
    {
        // 🚨 极其关键的安全协议：开火前全盘清理所有已死亡的丧尸节点！
        KingGlobals.CleanupActiveKingSprites();
        SerenadeManager.CleanUp();

        Label? targetLabel = null;
        Node2D? targetParent = null;

        // 1. 智能寻的：现在是幼年期还是完全体？
        if (KingGlobals.IsSwordForged && SerenadeManager.ActiveSerenades.Count > 0)
        {
            // 完全体：找那把飞剑
            targetParent = SerenadeManager.ActiveSerenades[0];
            if (GodotObject.IsInstanceValid(targetParent))
            {
                targetLabel = targetParent.GetNodeOrNull<Label>("ChatLabel");
            }
        }
        else if (!KingGlobals.IsSwordForged && KingGlobals.ActiveKingSprites.Count > 0)
        {
            // 幼年期：找国王肩膀上的图标
            foreach (var sprite in KingGlobals.ActiveKingSprites)
            {
                // 🛡️ 双重保险：碰它之前先摸摸脉搏，死了就跳过！
                if (!GodotObject.IsInstanceValid(sprite)) continue; 

                var king = sprite.GetParent() as Node2D;
                if (king != null && GodotObject.IsInstanceValid(king))
                {
                    targetParent = king.GetNodeOrNull<Node2D>("SerenadePet");
                    if (targetParent != null && GodotObject.IsInstanceValid(targetParent))
                    {
                        targetLabel = targetParent.GetNodeOrNull<Label>("ChatLabel");
                        break;
                    }
                }
            }
        }

        // 2. 引爆弹幕！(发射前最后一次存活确认)
        if (targetLabel != null && GodotObject.IsInstanceValid(targetLabel) && 
            targetParent != null && GodotObject.IsInstanceValid(targetParent))
        {
            targetLabel.Text = text;
            targetLabel.Visible = true;
            targetLabel.Modulate = new Color(1, 1, 1, 1);
            targetLabel.Position = new Vector2(-targetLabel.Size.X / 2, -50f);

            var tween = targetParent.CreateTween();
            tween.TweenProperty(targetLabel, "position:y", targetLabel.Position.Y - 40f, 1.5f)
                 .SetTrans(Tween.TransitionType.Quad)
                 .SetEase(Tween.EaseType.Out);
                 
            tween.Parallel().TweenProperty(targetLabel, "modulate:a", 0f, 1.5f);
            
            // 动画播完后也要做生命检测，防止播的过程中节点被销毁导致崩溃
            tween.TweenCallback(Callable.From(() => {
                if (GodotObject.IsInstanceValid(targetLabel)) 
                {
                    targetLabel.Visible = false;
                }
            }));
        }
    }
}

// ==========================================
// 💀 死亡双重保险
// ==========================================
[HarmonyPatch(typeof(NCreature), "AnimDie")]
internal static class NCreature_AnimDie_Patch
{
    private static void Prefix(NCreature __instance)
    {
        var player = __instance.Entity?.Player;
        if (player == null || !string.Equals(player.Character?.Id?.Entry, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) return;

        var visuals = __instance.Visuals;
        var kingMecha = visuals?.GetNodeOrNull<Node2D>("KingMecha");
        if (kingMecha == null) return;

        var kingSprite = KingCombatNodeCache.GetSprite(kingMecha);
        if (kingSprite == null) return;

        KingGlobals.IsDead = true;
        kingSprite.Stop();
        kingSprite.Play("letha");
        kingMecha.Position = Vector2.Zero;
    }
}

// ==========================================
// 🏆 极其嚣张的胜利结算
// ==========================================
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Combat.CombatManager), "EndCombatInternal")]
internal static class CombatManager_EndCombatInternal_Patch
{
    private static void Prefix(object __instance) 
    {
        KingGlobals.VerboseLog("\n====== 🏆 战斗结束！国王开始极其嚣张地嘲讽！ ======");
        KingGlobals.CleanupActiveKingSprites();
        if (KingGlobals.ActiveKingSprites.Count <= 0) return;

        foreach (var sprite in KingGlobals.ActiveKingSprites)
        {
            // 防重复嘲讽
            bool isAlreadyTaunting = (sprite.Animation == "travolta" || sprite.Animation == "no" || sprite.Animation == "what");
            if (!isAlreadyTaunting)
            {
                string chosenVic = KingGlobals.VictoryPool[KingGlobals.Rng.Next(KingGlobals.VictoryPool.Length)];
                sprite.Stop();
                sprite.Play(chosenVic);
            }
        }
    }
}