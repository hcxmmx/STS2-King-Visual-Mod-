using Godot;

namespace Hcxmmx.KingMod.Scripts;

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
            // Keep long English/Japanese lines within a readable width in the scaled scene.
            targetLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            targetLabel.CustomMinimumSize = new Vector2(200f, 0f);
            targetLabel.Text = text;
            targetLabel.ResetSize();
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

