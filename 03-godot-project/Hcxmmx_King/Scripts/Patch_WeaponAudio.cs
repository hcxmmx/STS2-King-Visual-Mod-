using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Hcxmmx.KingMod.Scripts;

internal static class KingAudioPatchTargets
{
    internal static MethodInfo MoveNext(Type type, string name)
    {
        var method = AccessTools.DeclaredMethod(type, name);
        return MoveNext(method ?? throw new MissingMethodException(type.FullName, name));
    }
    internal static MethodInfo MoveNext(MethodInfo method)
    {
        var state = method?.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMethodException("Missing audio target async state machine");
        return AccessTools.DeclaredMethod(state, "MoveNext")
            ?? throw new MissingMethodException(state.FullName, "MoveNext");
    }
    internal static readonly MethodInfo Play = AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]);
    internal static readonly MethodInfo TmpPlay = AccessTools.Method(typeof(NDebugAudioManager), nameof(NDebugAudioManager.Play));
    internal static readonly MethodInfo Damage = AccessTools.DeclaredMethod(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
        [typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay)]);
    internal static MethodInfo Wrapper(string name) => AccessTools.Method(typeof(KingWeaponAudio), name);
}

[HarmonyPatch]
internal static class KingAudio_CharacterAttack_Patch
{
    private static MethodBase TargetMethod() => KingAudioPatchTargets.MoveNext(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim));
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var codes = instructions.ToList();
        var getter = AccessTools.PropertyGetter(typeof(CharacterModel), nameof(CharacterModel.AttackSfx));
        var creature = AccessTools.Field(__originalMethod.DeclaringType, "creature");
        int changed = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            if (!codes[i].Calls(getter)) continue;
            int play = codes.FindIndex(i + 1, instruction => instruction.Calls(KingAudioPatchTargets.Play));
            if (play < 0 || play - i > 5) throw new InvalidOperationException("King audio: character attack SFX call site changed");
            var owner = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[play]);
            codes.InsertRange(play, [owner, new CodeInstruction(OpCodes.Ldfld, creature)]);
            codes[play + 2].opcode = OpCodes.Call;
            codes[play + 2].operand = KingAudioPatchTargets.Wrapper(nameof(KingWeaponAudio.PlayCharacterAttack));
            changed++;
            i = play + 2;
        }
        if (changed != 1) throw new InvalidOperationException($"King audio: expected 1 character attack SFX site, found {changed}");
        return codes;
    }
}

[HarmonyPatch]
internal static class KingAudio_AttackExecution_Patch
{
    private static MethodBase TargetMethod() => KingAudioPatchTargets.MoveNext(typeof(AttackCommand), nameof(AttackCommand.Execute));
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var codes = instructions.ToList();
        var command = AccessTools.Field(__originalMethod.DeclaringType, "<>4__this");
        var trigger = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim));
        int sounds = 0, temporary = 0, animations = 0, damages = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            string? wrapper = null;
            if (codes[i].Calls(KingAudioPatchTargets.Play))
                wrapper = sounds++ == 0 ? nameof(KingWeaponAudio.PlayAttacker) : nameof(KingWeaponAudio.PlayHit);
            else if (codes[i].Calls(KingAudioPatchTargets.TmpPlay))
                wrapper = temporary++ == 0 ? nameof(KingWeaponAudio.PlayTmpAttacker) : nameof(KingWeaponAudio.PlayTmpHit);
            else if (codes[i].Calls(trigger))
            {
                animations++;
                wrapper = nameof(KingWeaponAudio.TriggerAttack);
            }
            else if (codes[i].Calls(KingAudioPatchTargets.Damage))
            {
                damages++;
                wrapper = nameof(KingWeaponAudio.DealDamage);
            }
            if (wrapper == null) continue;
            var owner = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[i]);
            codes.InsertRange(i, [owner, new CodeInstruction(OpCodes.Ldfld, command)]);
            codes[i + 2].opcode = OpCodes.Call;
            codes[i + 2].operand = KingAudioPatchTargets.Wrapper(wrapper);
            i += 2;
        }
        if (sounds != 2 || temporary != 2 || animations != 1 || damages != 1)
            throw new InvalidOperationException($"King audio: unexpected Execute call sites ({sounds}, {temporary}, {animations}, {damages})");
        return codes;
    }
}

[HarmonyPatch]
internal static class KingAudio_DamageImpact_Patch
{
    private static MethodBase TargetMethod() => KingAudioPatchTargets.MoveNext(KingAudioPatchTargets.Damage);
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var codes = instructions.ToList();
        var loseHp = AccessTools.DeclaredMethod(typeof(Creature), nameof(Creature.LoseHpInternal));
        var targets = AccessTools.Field(__originalMethod.DeclaringType, "targets");
        int changed = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            if (!codes[i].Calls(loseHp)) continue;
            var owner = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[i]);
            codes.InsertRange(i, [owner, new CodeInstruction(OpCodes.Ldfld, targets)]);
            codes[i + 2].opcode = OpCodes.Call;
            codes[i + 2].operand = KingAudioPatchTargets.Wrapper(nameof(KingWeaponAudio.ApplyHit));
            changed++;
            i += 2;
        }
        if (changed != 2) throw new InvalidOperationException($"King audio: expected 2 HP loss sites, found {changed}");
        return codes;
    }
}
