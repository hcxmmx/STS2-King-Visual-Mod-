using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Models; // 🚨 新增：用于拦截卡牌打出

namespace Hcxmmx.KingMod.Scripts;

internal static class SerenadeNodeCache
{
    private static readonly StringName SwordKey = new("SerenadeSwordSpriteRef");
    private static readonly StringName VfxKey = new("SerenadeVfxSpriteRef");

    internal static void Store(Node2D serenade, AnimatedSprite2D? sword, AnimatedSprite2D? vfx)
    {
        if (sword != null) serenade.SetMeta(SwordKey, sword);
        if (vfx != null) serenade.SetMeta(VfxKey, vfx);
    }

    internal static AnimatedSprite2D? GetSword(Node2D serenade)
    {
        var sword = GetCachedNode<AnimatedSprite2D>(serenade, SwordKey);
        if (sword != null) return sword;

        sword = KingGlobals.FindFirstNode<AnimatedSprite2D>(serenade, n => n.Name == "SwordSprite");
        if (sword != null) serenade.SetMeta(SwordKey, sword);
        return sword;
    }

    internal static AnimatedSprite2D? GetVfx(Node2D serenade)
    {
        var vfx = GetCachedNode<AnimatedSprite2D>(serenade, VfxKey);
        if (vfx != null) return vfx;

        vfx = KingGlobals.FindFirstNode<AnimatedSprite2D>(serenade, n => n.Name == "VFX_FlyingSword");
        if (vfx != null) serenade.SetMeta(VfxKey, vfx);
        return vfx;
    }

    private static T? GetCachedNode<T>(Node2D serenade, StringName key) where T : Node
    {
        if (!serenade.HasMeta(key)) return null;
        var cachedObject = serenade.GetMeta(key).AsGodotObject();
        return cachedObject is T node && GodotObject.IsInstanceValid(node) ? node : null;
    }
}

// ==========================================
// 📡 夜歌专属战术中枢：管理所有在场的老婆剑
// ==========================================
internal static class SerenadeManager
{
    public static System.Collections.Generic.List<Node2D> ActiveSerenades = new();
    public static int AttackIndex = 0;

    // 🚨 长官整理的专属武器库与特效库
    public static readonly string[] AtkAnims = { "atkA", "atkB", "atkC" };
    public static readonly string[] VfxAnims = { "FxFlyingSwordSoloAtkA", "FxFlyingSwordSoloAtkB", "FxFlyingSwordSoloAtkC" };

    public static void CleanUp()
    {
        ActiveSerenades.RemoveAll(node => !GodotObject.IsInstanceValid(node));
    }
}

// ==========================================
// 🗡️ 夜歌寄生协议：剥夺肉体，替换为咱们的节点
// ==========================================
[HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx._Ready))]
internal static class NSovereignBladeVfx_Ready_Patch
{
    private static void Postfix(NSovereignBladeVfx __instance)
    {
        var spineSword = __instance.GetNodeOrNull<Node2D>("SpineSword");
        if (spineSword == null) return;

        var swordBone = spineSword.GetNodeOrNull<Node2D>("SwordBone");
        if (swordBone == null) return;

        var scaleContainer = swordBone.GetNodeOrNull<Node2D>("ScaleContainer");
        if (scaleContainer != null) scaleContainer.Visible = false; // 隐藏原版剑体

        // 🚨 请确保这里的路径是长官极其真实的夜歌场景路径！
        var serenadeScene = KingGlobals.SerenadeScenePreloaded;
        if (serenadeScene == null) return;

        var serenadeNode = serenadeScene.Instantiate<Node2D>();
        serenadeNode.Name = "SerenadeMecha";
        swordBone.AddChild(serenadeNode); // 极其霸道地挂载在骨骼上
        
        serenadeNode.Position = Vector2.Zero; 
        serenadeNode.Scale = new Vector2(4.0f, 4.0f); // 缩放可微调
        
        // 抓取本体和特效节点
        var swordSprite = KingGlobals.FindFirstNode<AnimatedSprite2D>(serenadeNode, n => n.Name == "SwordSprite");
        var vfxSprite = KingGlobals.FindFirstNode<AnimatedSprite2D>(serenadeNode, n => n.Name == "VFX_FlyingSword");
        SerenadeNodeCache.Store(serenadeNode, swordSprite, vfxSprite);

        // 特效节点隐形待命协议
        if (vfxSprite != null)
        {
            vfxSprite.Visible = false;
            vfxSprite.AnimationFinished += () => { vfxSprite.Visible = false; vfxSprite.Stop(); };
        }

        // 本体动画打完自动切回待机
        if (swordSprite != null)
        {
            swordSprite.AnimationFinished += () =>
            {
                if (swordSprite.Animation != "idle") swordSprite.Play("idle");
            };
            swordSprite.Play("idle");
        }

        // 极其关键：将新诞生的夜歌编入雷达阵列
        SerenadeManager.CleanUp();
        SerenadeManager.ActiveSerenades.Add(serenadeNode);
        KingGlobals.CombatLog("🗡️ 【夜歌】已全盘接管君王之剑系统！");

        KingGlobals.IsSwordForged = true; // 极其关键：状态切换为完全体！
        // 隐藏国王肩膀上的小图标
        if (KingGlobals.ActiveKingSprites.Count > 0)
        {
            foreach(var s in KingGlobals.ActiveKingSprites)
            {
                var p = s.GetParent()?.GetNodeOrNull<Node2D>("SerenadePet");
                if (p != null) p.Visible = false;
            }
        }
        // 发射进化弹幕！
        DanmakuEngine.Fire(KingGlobals.SerenadeForgedQuotes[KingGlobals.Rng.Next(KingGlobals.SerenadeForgedQuotes.Length)]);
    }
}

// ==========================================
// 🎯 极其致命的监听器：拦截【君王之剑】卡牌打出！
// ==========================================
[HarmonyPatch(typeof(CardModel), "OnPlayWrapper")]
internal static class SovereignBlade_CardPlay_Patch
{
    private static void Prefix(object __instance)
    {
        try
        {
            // 利用反射极其精准地抓出卡牌ID
            var cardTraverse = Traverse.Create(__instance);
            var idObj = cardTraverse.Property("Id").GetValue() ?? cardTraverse.Field("Id").GetValue();
            if (idObj == null) return;

            var idTraverse = Traverse.Create(idObj);
            var entry = idTraverse.Property("Entry").GetValue<string>() ?? idTraverse.Field("Entry").GetValue<string>();

            // 锁定目标卡牌
            if (entry == "SOVEREIGN_BLADE")
            {
                KingGlobals.CombatLog("🎯 侦测到【君王之剑】打出！夜歌开始连携斩击！");
                SerenadeManager.CleanUp();

                if (SerenadeManager.ActiveSerenades.Count == 0) return;

                // 极其优雅的轮盘切招 (A -> B -> C 循环)
                string chosenAtk = SerenadeManager.AtkAnims[SerenadeManager.AttackIndex];
                string chosenVfx = SerenadeManager.VfxAnims[SerenadeManager.AttackIndex];
                SerenadeManager.AttackIndex = (SerenadeManager.AttackIndex + 1) % SerenadeManager.AtkAnims.Length;

                // 呼叫场上所有夜歌同时斩出极其华丽的刀光！
                foreach (var serenade in SerenadeManager.ActiveSerenades)
                {
                    var swordSprite = SerenadeNodeCache.GetSword(serenade);
                    var vfxSprite = SerenadeNodeCache.GetVfx(serenade);

                    if (swordSprite != null)
                    {
                        swordSprite.Stop();
                        swordSprite.Play(chosenAtk);
                    }

                    if (vfxSprite != null)
                    {
                        vfxSprite.Visible = true;
                        vfxSprite.Stop();
                        vfxSprite.Play(chosenVfx);
                    }
                }
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(NSovereignBladeVfx), "Forge")]
internal static class NSovereignBladeVfx_Forge_Patch
{
    private static void Postfix(NSovereignBladeVfx __instance)
    {
        var spineSword = __instance.GetNodeOrNull<Node2D>("SpineSword");
        var swordBone = spineSword?.GetNodeOrNull<Node2D>("SwordBone");
        var serenadeNode = swordBone?.GetNodeOrNull<Node2D>("SerenadeMecha");

        if (serenadeNode == null) return;

        // 1. 极其傲娇的专属弹幕 (不变)
        string[] forgeQuotes = { "这破铁又重了！", "给我血肉来磨砺！", "能量充盈...渴望斩杀！" };
        DanmakuEngine.Fire(forgeQuotes[KingGlobals.Rng.Next(forgeQuotes.Length)]);

        // ==========================================
        // 🚀 终极视觉方案 A：残影爆裂 + 缓动过载
        // ==========================================
        var swordSprite = KingGlobals.FindFirstNode<AnimatedSprite2D>(serenadeNode, n => n.Name == "SwordSprite");
        if (swordSprite != null)
        {
            // ⚔️ 步骤 1：本体极其明显的膨胀与滞空
            var tween = serenadeNode.CreateTween();
            Vector2 baseScale = new Vector2(4.0f, 4.0f); // 基础大小
            Vector2 surgeScale = new Vector2(6.5f, 6.5f); // 极其暴力的放大 (比之前更大)
            
            // 拉长反馈时间：0.15秒炸开 -> 悬停0.1秒展现压迫感 -> 0.5秒极其平滑地缩回 (总耗时0.75秒，绝对看得到！)
            tween.TweenProperty(serenadeNode, "scale", surgeScale, 0.15).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenInterval(0.1); 
            tween.TweenProperty(serenadeNode, "scale", baseScale, 0.5).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);

            // 👻 步骤 2：生成极其炫酷的血红残影
            // 极其硬核的降维克隆法：直接复制当前帧的精灵！
            var ghost = swordSprite.Duplicate() as AnimatedSprite2D;
            if (ghost != null)
            {
                serenadeNode.AddChild(ghost);
                ghost.ZIndex = swordSprite.ZIndex - 1; // 垫在本体下面
                
                // 给残影染上极其浓烈的血红色
                ghost.Modulate = new Color(1f, 0.1f, 0.1f, 0.8f); 

                var ghostTween = ghost.CreateTween();
                ghostTween.SetParallel(true); // 让放大和变透明同时进行

                // 残影极其夸张地向外扩散到 2.5倍，并在 0.8 秒内极其凄美地消散
                ghostTween.TweenProperty(ghost, "scale", new Vector2(2.5f, 2.5f), 0.8).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
                ghostTween.TweenProperty(ghost, "modulate:a", 0f, 0.8).SetEase(Tween.EaseType.In);
                
                // 残影消散后，极其干脆地自我销毁，绝不占用内存！
                ghostTween.Chain().TweenCallback(Callable.From(() => ghost.QueueFree()));
            }

            // 🩸 步骤 3：本体的嗜血潮红闪烁 (加长版)
            var colorTween = swordSprite.CreateTween();
            colorTween.TweenProperty(swordSprite, "modulate", new Color(1f, 0.4f, 0.4f, 1f), 0.1); // 变粉红/亮红
            colorTween.TweenProperty(swordSprite, "modulate", Colors.White, 0.5); // 慢慢褪色
        }
    }
}