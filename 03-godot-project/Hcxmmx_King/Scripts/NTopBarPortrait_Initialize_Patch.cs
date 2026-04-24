using System;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.sts2.Core.Nodes.TopBar;

// 🟢 极其关键：命名空间必须和 project.txt 里的 Hcxmmx.KingMod.Scripts 完全一致！
namespace Hcxmmx.KingMod.Scripts; 

[HarmonyPatch(typeof(NTopBarPortrait), "Initialize")]
internal static class NTopBarPortrait_Initialize_Patch
{
    private static void Postfix(NTopBarPortrait __instance, Player player)
    {
        // 1. 🎯 调用长官亲写的“赛博雷达”获取角色 Entry ID
        // 这里完美契合 project.txt 里的 GetCharacterEntry 方法
        var entryName = KingGlobals.GetCharacterEntry(player.Character); 

        // 2. 🛡️ 身份校验：使用 project.txt 里的 TargetCharacterId ("REGENT")
        if (!string.Equals(entryName, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) 
        {
            return; 
        }

        KingGlobals.VerboseLog("====== 🎭 顶部栏实验室：侦测到储君降临！执行全套换脸手术！ ======");

        // 3. 🎨 加载素材（请确保路径和之前在项目里定好的一致喵）
        var myIcon = KingGlobals.TopBarIconTexture;
        var myOutline = KingGlobals.TopBarOutlineTexture;
        if (myIcon == null || myOutline == null)
        {
            KingGlobals.ErrorLog("💥 顶部栏图标资源缺失，跳过头像替换。");
            return;
        }

        // 4. 🗡️ 执行递归手术
        PerformSurgery(__instance, myIcon, myOutline);
    }

    private static void PerformSurgery(Node node, Texture2D icon, Texture2D outline)
    {
        if (node is TextureRect tr)
        {
            string path = tr.Texture?.ResourcePath?.ToLower() ?? "";
            
            // 盯准 regent 关键字，实现精准狙击
            if (path.Contains("regent"))
            {
                if (path.EndsWith("_outline.png"))
                {
                    tr.Texture = outline;
                }
                else
                {
                    tr.Texture = icon;
                }
            }
        }

        foreach (Node child in node.GetChildren())
        {
            PerformSurgery(child, icon, outline);
        }
    }
}