using System;
using Godot;
using HarmonyLib;

namespace Hcxmmx.KingMod.Scripts;

// ==========================================
// 🛍️ 商店雷达：鸠占鹊巢与思考动画接力
// ==========================================
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom), nameof(MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom._Ready))]
internal static class NMerchantRoom_Ready_Patch
{
    private static void Postfix(MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom __instance)
    {
        KingGlobals.VerboseLog("\n====== 🛍️ 侦测到进入商店！屑国王开始思考怎么花钱！ ======");
        KingGlobals.IsInShop = true;

        var players = Traverse.Create(__instance).Field("_players").GetValue<System.Collections.IList>();
        var playerVisuals = Traverse.Create(__instance).Field("_playerVisuals").GetValue<System.Collections.IList>();
        
        if (players == null || playerVisuals == null || players.Count != playerVisuals.Count) return;

        var characterContainer = __instance.GetNodeOrNull<Control>("%CharacterContainer");
        if (characterContainer == null) return;

        var scene = KingGlobals.KingScene ?? KingGlobals.KingScenePreloaded;
        KingGlobals.KingScene = scene;
        if (scene == null) return;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var playerTraverse = Traverse.Create(player);
            var character = playerTraverse.Property("Character").GetValue() ?? playerTraverse.Field("Character").GetValue();
            string? entryName = KingGlobals.GetCharacterEntry(character);

            // 不是储君？极其冷酷地跳过
            if (!string.Equals(entryName, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) continue;

            KingGlobals.VerboseLog($"🎯 商店雷达锁定：玩家 {i} 替换为国王！");
            
            var targetChild = playerVisuals[i] as Node2D;
            if (targetChild == null) continue;

            // 抹杀原版储君肉体
            targetChild.Hide();

            // 注入国王机甲
            var kingShopMecha = scene.Instantiate<Node2D>();
            kingShopMecha.Name = $"KingShopMecha_{i}";
            characterContainer.AddChild(kingShopMecha);

            // 极其精准地继承原位置（长官可根据商店里的视觉效果微调此处的偏移量）
            kingShopMecha.Position = targetChild.Position + new Vector2(0, 0);
            kingShopMecha.Scale = new Vector2(7.5f, 7.5f); // 保持霸气的7.5倍巨大化
            KingGlobals.ApplyCurrentSkin(kingShopMecha);

            // 🚨 抓取身体动画节点，隐蔽无关战斗节点
            var kingSprite = KingGlobals.FindFirstNode<AnimatedSprite2D>(kingShopMecha, s => s.Name != "VFX_Slash");
            var vfxSprite = kingShopMecha.GetNodeOrNull<AnimatedSprite2D>("VFX_Slash");
            var petNode = kingShopMecha.GetNodeOrNull<Node2D>("SerenadePet");

            if (vfxSprite != null) vfxSprite.Visible = false;
            if (petNode != null) petNode.Visible = false; // 逛街时收起小剑宠

            if (kingSprite != null)
            {
                // 🎇 极其优雅的动画链式接力：播完 idleThink 自动锁死在 think！
                kingSprite.AnimationFinished += () =>
                {
                    // 注意：这里严格使用了长官提取的大写I的拼写 "idleThink"
                    if (kingSprite.Animation == "idleThink")
                    {
                        kingSprite.Play("think");
                    }
                };
                
                // 进场先播摸下巴
                kingSprite.Play("idleThink");
            }
        }
    }
}

// 离开商店解锁
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom), "HideScreen")]
internal static class NMerchantRoom_HideScreen_Patch
{
    private static void Prefix() { KingGlobals.IsInShop = false; }
}


// ==========================================
// 🍷 篝火雷达：二郎腿红酒极乐时刻
// ==========================================
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NRestSiteRoom), nameof(MegaCrit.Sts2.Core.Nodes.Rooms.NRestSiteRoom._Ready))]
internal static class NRestSiteRoom_Ready_Patch
{
    private static void Postfix(MegaCrit.Sts2.Core.Nodes.Rooms.NRestSiteRoom __instance)
    {
        KingGlobals.VerboseLog("\n====== 🍷 篝火雷达：侦测到进入篝火！屑国王开始品酒！ ======");
        KingGlobals.IsInShop = true;

        var runState = Traverse.Create(__instance).Field("_runState").GetValue();
        if (runState == null) return;

        var players = Traverse.Create(runState).Property("Players").GetValue<System.Collections.IList>() ?? Traverse.Create(runState).Field("Players").GetValue<System.Collections.IList>();
        if (players == null) return;

        var scene = KingGlobals.KingScene ?? KingGlobals.KingScenePreloaded;
        KingGlobals.KingScene = scene;
        if (scene == null) return;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var playerTraverse = Traverse.Create(player);
            var character = playerTraverse.Property("Character").GetValue() ?? playerTraverse.Field("Character").GetValue();
            string? entryName = KingGlobals.GetCharacterEntry(character);

            if (!string.Equals(entryName, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) continue;

            string containerPath = $"BgContainer/Character_{i + 1}";
            var container = __instance.GetNodeOrNull<Control>(containerPath);
            if (container == null) continue;

            // 启动光学迷彩：把官方坑位的图像透明度降为 0
            for (int j = 0; j < container.GetChildCount(); j++)
            {
                if (container.GetChild(j) is CanvasItem canvasItem)
                {
                    canvasItem.Modulate = new Color(1f, 1f, 1f, 0f);
                }
            }

            var kingCampMecha = scene.Instantiate<Node2D>();
            kingCampMecha.Name = $"KingCampMecha_{i}";
            container.AddChild(kingCampMecha);

            kingCampMecha.Scale = new Vector2(8.0f, 8.0f);
            kingCampMecha.Position = new Vector2(0, 200f); // 根据座位高低可微调
            KingGlobals.ApplyCurrentSkin(kingCampMecha);

            // 联机模式下的翻转支持 (如果是2号位则翻转方向)
            bool needsFlip = (i % 2 == 1);

            var kingSprite = KingGlobals.FindFirstNode<AnimatedSprite2D>(kingCampMecha, s => s.Name != "VFX_Slash");
            var vfxSprite = kingCampMecha.GetNodeOrNull<AnimatedSprite2D>("VFX_Slash");
            var petNode = kingCampMecha.GetNodeOrNull<Node2D>("SerenadePet");

            if (vfxSprite != null) vfxSprite.Visible = false;
            if (petNode != null) petNode.Visible = false; 

            if (kingSprite != null)
            {
                kingSprite.Play("wineDrink"); // 🍷 极其优雅地端起红酒
                
                if (needsFlip)
                {
                    kingSprite.FlipH = !kingSprite.FlipH;
                }
            }
        }
    }
}

// 离开篝火解锁
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NRestSiteRoom), "OnProceedButtonReleased")]
internal static class NRestSiteRoom_Exit_Patch
{
    private static void Prefix() { KingGlobals.IsInShop = false; }
}

// ==========================================
// 🎭 假商人雷达：极其优雅的伪装识破
// ==========================================

// 👉 改动点 1：替换 typeof 里的类名路径
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Events.Custom.NFakeMerchant), nameof(MegaCrit.Sts2.Core.Nodes.Events.Custom.NFakeMerchant._Ready))]
internal static class NFakeMerchant_Ready_Patch
{
    // 👉 改动点 2：替换 __instance 的参数类型
    private static void Postfix(MegaCrit.Sts2.Core.Nodes.Events.Custom.NFakeMerchant __instance)
    {
        KingGlobals.VerboseLog("\n====== 🎭 侦测到进入假商人房间！开始替换模型！ ======");
        KingGlobals.IsInShop = true; 

        var players = Traverse.Create(__instance).Field("_players").GetValue<System.Collections.IList>();
        var playerVisuals = Traverse.Create(__instance).Field("_playerVisuals").GetValue<System.Collections.IList>();
        if (players == null || playerVisuals == null || players.Count != playerVisuals.Count) return;

        var characterContainer = __instance.GetNodeOrNull<Control>("%CharacterContainer");
        if (characterContainer == null) return;

        var scene = KingGlobals.KingScene ?? KingGlobals.KingScenePreloaded;
        KingGlobals.KingScene = scene;
        if (scene == null) return;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var playerTraverse = Traverse.Create(player);
            var character = playerTraverse.Property("Character").GetValue() ?? playerTraverse.Field("Character").GetValue();
            string? entryName = KingGlobals.GetCharacterEntry(character);

            if (!string.Equals(entryName, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) continue;
            
            var targetChild = playerVisuals[i] as Node2D;
            if (targetChild == null) continue;

            targetChild.Hide(); // 抹杀原版模型

            var kingShopMecha = scene.Instantiate<Node2D>();
            kingShopMecha.Name = $"KingShopMecha_Fake_{i}";
            characterContainer.AddChild(kingShopMecha);

            kingShopMecha.Position = targetChild.Position + new Vector2(0, 0);
            kingShopMecha.Scale = new Vector2(7.5f, 7.5f);
            KingGlobals.ApplyCurrentSkin(kingShopMecha);

            var kingSprite = KingGlobals.FindFirstNode<AnimatedSprite2D>(kingShopMecha, s => s.Name != "VFX_Slash");
            var vfxSprite = kingShopMecha.GetNodeOrNull<AnimatedSprite2D>("VFX_Slash");
            var petNode = kingShopMecha.GetNodeOrNull<Node2D>("SerenadePet");

            if (vfxSprite != null) vfxSprite.Visible = false;
            if (petNode != null) petNode.Visible = false;

            if (kingSprite != null)
            {
                kingSprite.AnimationFinished += () =>
                {
                    if (kingSprite.Animation == "idleThink")
                    {
                        kingSprite.Play("think");
                    }
                };
                kingSprite.Play("idleThink");
            }
        }
    }
}

// 离开假商人房间解锁
// 👉 改动点 3：替换 typeof 里的类名路径
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Events.Custom.NFakeMerchant), "HideScreen")]
internal static class NFakeMerchant_HideScreen_Patch
{
    private static void Prefix() { KingGlobals.IsInShop = false; }
}