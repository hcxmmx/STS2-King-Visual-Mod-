using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

// ✅ 命名空间已重构为 KingMod
namespace Hcxmmx.KingMod.Scripts;

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen), "SelectCharacter")]
internal static class NCharacterSelectScreen_SelectCharacter_Patch
{
    private static bool IsKing(CharacterModel character)
    {
        return string.Equals(character.Id.Entry, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var sound = AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]);
        var character = AccessTools.PropertyGetter(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Character));
        var guard = AccessTools.Method(typeof(NCharacterSelectScreen_SelectCharacter_Patch), nameof(IsKing));
        int changed = 0;
        foreach (var instruction in instructions)
        {
            var method = instruction.operand as MethodInfo;
            bool skinWrapper = method?.DeclaringType?.FullName == "Hcxmmx.SothebySkin.CharacterSelectPresentation" &&
                method.Name == "PlaySelectionSound";
            if (!instruction.Calls(sound) && !skinWrapper)
            {
                yield return instruction;
                continue;
            }

            // Guard the existing call so other skins can apply their own guards in either order.
            var playOriginal = generator.DefineLabel();
            var end = generator.DefineLabel();
            yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return new CodeInstruction(OpCodes.Callvirt, character);
            yield return new CodeInstruction(OpCodes.Call, guard);
            yield return new CodeInstruction(OpCodes.Brfalse, playOriginal);
            int arguments = method!.GetParameters().Length + (method.IsStatic ? 0 : 1);
            for (int index = 0; index < arguments; index++) yield return new CodeInstruction(OpCodes.Pop);
            yield return new CodeInstruction(OpCodes.Br, end);
            instruction.labels.Add(playOriginal);
            yield return instruction;
            var tail = new CodeInstruction(OpCodes.Nop);
            tail.labels.Add(end);
            yield return tail;
            changed++;
        }
        if (changed != 1) throw new InvalidOperationException($"King selection: expected 1 sound call, found {changed}");
    }

    private static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton)
    {
        KingSelectionSettings.Update(__instance, IsKing(charSelectButton.Character) && !charSelectButton.IsLocked);
        if (!IsKing(charSelectButton.Character))
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
            nameTraverse.Method("SetTextAutoSize", new object[] { KingLocalization.GetText("character.name") }).GetValue();
        }

        if (descLabel != null)
        {
            // 🎯 修改为极其霸气的专属介绍
            descLabel.Text = KingLocalization.GetText("character.description");
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

// 选人头像替换。

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
// 🌐 多人读档界面 (主机端)：替换背景大立绘
// ==========================================
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NMultiplayerLoadGameScreen), "InitializeAsHost")]
internal static class NMultiplayerLoadGameScreen_InitializeAsHost_Patch
{
    private static void Postfix(Godot.Node __instance, object run)
    {
        KingGlobals.VerboseLog("\n====== 🌐 多人读档雷达 (Host)：侦测到界面加载！ ======");

        var bgContainer = __instance.GetNodeOrNull<Godot.Control>("%BgContainer")
                       ?? __instance.GetNodeOrNull<Godot.Control>("BgContainer")
                       ?? __instance.GetNodeOrNull<Godot.Control>("%Bg");

        var targetContainer = bgContainer ?? (__instance as Godot.Control);

        string charId = "";
        try {
            var playersList = Traverse.Create(run).Property("Players").GetValue<System.Collections.IList>()
                           ?? Traverse.Create(run).Field("Players").GetValue<System.Collections.IList>();

            if (playersList != null && playersList.Count > 0)
            {
                var hostPlayer = playersList[0];
                var modelIdObj = Traverse.Create(hostPlayer).Property("CharacterId").GetValue()
                              ?? Traverse.Create(hostPlayer).Field("CharacterId").GetValue();

                if (modelIdObj != null) charId = modelIdObj.ToString() ?? "";
            }
        } catch (Exception ex) {
            KingGlobals.ErrorLog($"[Error] Host提取角色ID异常: {ex.Message}");
        }
        KingGlobals.VerboseLog($"Host 最终提取的 ID: '{charId}'");

        if (string.IsNullOrEmpty(charId) || (!charId.Contains("Regent", StringComparison.OrdinalIgnoreCase) && !charId.Contains(KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        KingSelectBackground.Apply(targetContainer, bgContainer != null);
    }
}

// ==========================================
// 🌐 多人读档界面 (客机端)：替换背景大立绘
// ==========================================
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NMultiplayerLoadGameScreen), "InitializeAsClient")]
internal static class NMultiplayerLoadGameScreen_InitializeAsClient_Patch
{
    private static void Postfix(Godot.Node __instance, object message)
    {
        KingGlobals.VerboseLog("\n====== 🌐 多人读档雷达 (Client)：侦测到界面加载！ ======");

        var bgContainer = __instance.GetNodeOrNull<Godot.Control>("%BgContainer")
                       ?? __instance.GetNodeOrNull<Godot.Control>("BgContainer")
                       ?? __instance.GetNodeOrNull<Godot.Control>("%Bg");
        var targetContainer = bgContainer ?? (__instance as Godot.Control);

        string charId = "";
        try {
            var runObj = Traverse.Create(message).Property("Run").GetValue() ?? Traverse.Create(message).Field("Run").GetValue();
            if (runObj != null)
            {
                var playersList = Traverse.Create(runObj).Property("Players").GetValue<System.Collections.IList>()
                               ?? Traverse.Create(runObj).Field("Players").GetValue<System.Collections.IList>();

                if (playersList != null && playersList.Count > 0)
                {
                    var targetPlayer = playersList[0];
                    var modelIdObj = Traverse.Create(targetPlayer).Property("CharacterId").GetValue()
                                  ?? Traverse.Create(targetPlayer).Field("CharacterId").GetValue();

                    if (modelIdObj != null) charId = modelIdObj.ToString() ?? "";
                }
            }
        } catch (Exception ex) {
            KingGlobals.ErrorLog($"[Error] Client提取角色ID异常: {ex.Message}");
        }
        KingGlobals.VerboseLog($"Client 最终提取的 ID: '{charId}'");

        if (string.IsNullOrEmpty(charId) || (!charId.Contains("Regent", StringComparison.OrdinalIgnoreCase) && !charId.Contains(KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        KingSelectBackground.Apply(targetContainer, bgContainer != null);
    }
}
