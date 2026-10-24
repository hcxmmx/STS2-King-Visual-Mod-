using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
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
        KingGlobals.VerboseLog(() => $"\n---> King Project: 侦测到 NCreature 试图活化！节点名称 = {__instance.Name} <---");

        var scene = KingGlobals.KingScene ?? KingGlobals.KingScenePreloaded;
        KingGlobals.KingScene = scene;

        if (scene == null || __instance.Entity == null) return;

        var player = __instance.Entity.Player;
        if (player == null || !string.Equals(player.Character?.Id?.Entry, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) return;

        var visuals = __instance.Visuals;
        if (visuals == null) return;

        if (visuals.HasNode("KingMecha")) return;
        KingGlobals.IsInShop = false;
        KingGlobals.IsSwordForged = false;

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

        KingGlobals.ApplyCurrentSkin(kingNode);

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
            KingPresentation.Attach(kingNode, kingSprite, __instance.Entity.CombatState);
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
        if (KingGlobals.IsInShop) return;

        var player = __instance?.Entity?.Player;
        if (player == null || !string.Equals(player.Character?.Id?.Entry, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) return;

        var visuals = __instance?.Visuals;
        var kingMecha = visuals?.GetNodeOrNull<Node2D>("KingMecha");
        if (kingMecha == null) return;
        var presentation = KingPresentation.For(kingMecha);

        var kingSprite = KingCombatNodeCache.GetSprite(kingMecha);
        if (kingSprite == null || presentation.IsDead) return;

        KingGlobals.VerboseLog(() => $"---> King Project: 收到动作指令: {trigger} <---");

        switch (trigger)
        {
            case "Attack":
            case "AttackSingle":
            case "AttackTriple":
            {
                var stage = presentation.NextAttack(KingGlobals.Rng);
                string currentAtkFrame = stage.Body;
                KingPresentation.WeaponUse(kingMecha);

                kingSprite.Stop();
                kingSprite.Play(currentAtkFrame);
                KingWeaponAudio.OnAnimation(kingMecha, stage);

                // ==========================================
                // 🎇 极其致命的刀光引爆逻辑
                // ==========================================
                var vfxSprite = KingCombatNodeCache.GetVfx(kingMecha);
                if (vfxSprite != null) { vfxSprite.Stop(); vfxSprite.Visible = false; }
                if (vfxSprite != null && stage.Vfx is { } mappedVfx)
                {
                    vfxSprite.Visible = true;
                    vfxSprite.Stop();
                    vfxSprite.Play(mappedVfx);
                    KingGlobals.VerboseLog(() => $"🎇 连携引爆: 动作 [{currentAtkFrame}] 映射刀光 -> [{mappedVfx}]");
                }

                kingMecha.Position = Vector2.Zero;

                // ==========================================
                // 💬 夜歌傲娇弹幕：攻击吐槽！(仅在连招第一击触发，防刷屏)
                // ==========================================
                if (presentation.ComboIndex == 1)
                {
                    DanmakuEngine.Fire(KingLocalization.PickQuote(SerenadeQuoteKind.Attack));
                }
                break;
            }
            case "Hit":
            {
                presentation.ResetCombo(); // 🚨 受击打断连击！
                string chosenHit = KingGlobals.HitPool[KingGlobals.Rng.Next(KingGlobals.HitPool.Length)];
                kingSprite.Stop();
                kingSprite.Play(chosenHit);
                kingMecha.Position = Vector2.Zero;

                // ==========================================
                // 💬 夜歌傲娇弹幕：挨打吐槽！
                // ==========================================
                DanmakuEngine.Fire(KingLocalization.PickQuote(SerenadeQuoteKind.Hit));
                break;
            }
            case "Cast":
            {
                presentation.ResetCombo(); // 🚨 施法打断连击！
                string chosenCast = KingGlobals.CastPool[KingGlobals.Rng.Next(KingGlobals.CastPool.Length)];
                kingSprite.Stop();
                kingSprite.Play(chosenCast);
                kingMecha.Position = Vector2.Zero;

                // ==========================================
                // 💬 夜歌傲娇弹幕：施法/无聊吐槽！
                // ==========================================
                DanmakuEngine.Fire(KingLocalization.PickQuote(SerenadeQuoteKind.Idle));
                break;
            }
            case "Die":
            case "Death":
            case "Dead":
                presentation.IsDead = true;
                presentation.ResetCombo();
                string chosenDie = KingGlobals.DiePool[0];
                kingSprite.Stop();
                kingSprite.Play(chosenDie);
                kingMecha.Position = Vector2.Zero;
                break;
            default:
                presentation.ResetCombo();
                kingSprite.Play("Idle");
                kingMecha.Position = Vector2.Zero;
                break;
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

        KingPresentation.For(kingMecha).IsDead = true;
        kingSprite.Stop();
        kingSprite.Play("letha");
        kingMecha.Position = Vector2.Zero;
    }
}

// ==========================================
// 🏆 极其嚣张的胜利结算
// ==========================================
// Normal combat now calls the turn-state overload; the parameterless wrapper is for tests.
[HarmonyPatch]
internal static class CombatManager_EndCombatInternal_Patch
{
    private static MethodBase TargetMethod()
    {
        // CombatTurnState is internal in the game assembly, so resolve its exact type at runtime.
        var turnStateType = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.CombatTurnState", throwOnError: true)!;
        return AccessTools.DeclaredMethod(typeof(CombatManager), "EndCombatInternal", new[] { turnStateType })
            ?? throw new MissingMethodException(typeof(CombatManager).FullName, "EndCombatInternal(CombatTurnState)");
    }

    private static void Prefix()
    {
        KingGlobals.VerboseLog("\n====== 🏆 战斗结束！国王开始极其嚣张地嘲讽！ ======");
        KingGlobals.CleanupActiveKingSprites();
        if (KingGlobals.ActiveKingSprites.Count <= 0) return;

        foreach (var sprite in KingGlobals.ActiveKingSprites)
        {
            if (!sprite.IsInsideTree() || sprite.Animation == "letha") continue;

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
