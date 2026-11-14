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
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;
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
        [typeof(PlayerChoiceContext), typeof(IEnumerable<DamageTargetData>), typeof(decimal), typeof(ValueProp),
            typeof(Creature), typeof(CardModel), typeof(CardPlay), typeof(ModifyDamageHookType)])
        ?? throw new MissingMethodException(typeof(CreatureCmd).FullName, "Damage(DamageTargetData, ModifyDamageHookType)");
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
    // Other skins observe the native damage call before King binds its per-hit
    // async audio scope. Harmony reapplies this order when another mod loads.
    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
    {
        var codes = instructions.ToList();
        var command = AccessTools.Field(__originalMethod.DeclaringType, "<>4__this");
        var trigger = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim));
        var sideVfx = AccessTools.Method(typeof(VfxCmd), nameof(VfxCmd.PlayOnSide));
        int sounds = 0, temporary = 0, animations = 0, damages = 0, sideEffects = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            var method = codes[i].operand as MethodInfo;
            bool skinWrapper = method?.DeclaringType?.FullName is
                "Hcxmmx.AKnightSkin.AttackAudioPatch" or "Hcxmmx.SothebySkin.AttackAudioPatch";
            bool sound = codes[i].Calls(KingAudioPatchTargets.Play) || (skinWrapper && method!.Name == "Play");
            bool debug = codes[i].Calls(KingAudioPatchTargets.TmpPlay) || (skinWrapper && method!.Name == "PlayDebug");
            if (sound || debug)
            {
                bool attacker = sound ? sounds++ == 0 : temporary++ == 0;
                var replacement = GuardSound(codes[i], command, generator, attacker, debug);
                codes.RemoveAt(i);
                codes.InsertRange(i, replacement);
                i += replacement.Count - 1;
                continue;
            }
            string? wrapper = null;
            if (codes[i].Calls(trigger))
            {
                animations++;
                wrapper = nameof(KingWeaponAudio.TriggerAttack);
            }
            else if (codes[i].Calls(KingAudioPatchTargets.Damage))
            {
                damages++;
                wrapper = nameof(KingWeaponAudio.DealDamage);
            }
            else if (codes[i].Calls(sideVfx))
            {
                sideEffects++;
                wrapper = nameof(KingWeaponAudio.PlaySideVfx);
            }
            if (wrapper == null) continue;
            var owner = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[i]);
            codes.InsertRange(i, [owner, new CodeInstruction(OpCodes.Ldfld, command)]);
            codes[i + 2].opcode = OpCodes.Call;
            codes[i + 2].operand = KingAudioPatchTargets.Wrapper(wrapper);
            i += 2;
        }
        if (sounds != 2 || temporary != 2 || animations != 1 || damages != 1 || sideEffects != 1)
            throw new InvalidOperationException($"King audio: unexpected Execute call sites ({sounds}, {temporary}, {animations}, {damages}, {sideEffects})");
        return codes;
    }

    private static List<CodeInstruction> GuardSound(CodeInstruction original, FieldInfo command,
        ILGenerator generator, bool attacker, bool debug)
    {
        var method = (MethodInfo)original.operand;
        var types = method.GetParameters().Select(parameter => parameter.ParameterType).ToList();
        if (!method.IsStatic) types.Insert(0, method.DeclaringType!);
        var arguments = types.Select(generator.DeclareLocal).ToArray();
        var codes = new List<CodeInstruction>();
        // Preserve the existing native/skin wrapper as the fall-through call.
        for (int index = arguments.Length - 1; index >= 0; index--)
        {
            var save = new CodeInstruction(OpCodes.Stloc, arguments[index]);
            if (index == arguments.Length - 1) save.MoveLabelsFrom(original).MoveBlocksFrom(original);
            codes.Add(save);
        }
        var callOriginal = generator.DefineLabel();
        var end = generator.DefineLabel();
        codes.Add(new CodeInstruction(OpCodes.Ldarg_0));
        codes.Add(new CodeInstruction(OpCodes.Ldfld, command));
        codes.Add(new CodeInstruction(OpCodes.Ldloc, arguments[method.IsStatic ? 0 : 1]));
        codes.Add(new CodeInstruction(OpCodes.Call, KingAudioPatchTargets.Wrapper(attacker
            ? nameof(KingWeaponAudio.ReplacesAttacker) : nameof(KingWeaponAudio.ReplacesHit))));
        codes.Add(new CodeInstruction(OpCodes.Brfalse, callOriginal));
        if (debug) codes.Add(new CodeInstruction(OpCodes.Ldc_I4_M1));
        codes.Add(new CodeInstruction(OpCodes.Br, end));
        for (int index = 0; index < arguments.Length; index++)
        {
            var load = new CodeInstruction(OpCodes.Ldloc, arguments[index]);
            if (index == 0) load.labels.Add(callOriginal);
            codes.Add(load);
        }
        codes.Add(original);
        var tail = new CodeInstruction(OpCodes.Nop);
        tail.labels.Add(end);
        codes.Add(tail);
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
        var targets = AccessTools.Field(__originalMethod.DeclaringType, "targetData")
            ?? throw new MissingFieldException(__originalMethod.DeclaringType?.FullName, "targetData");
        var dealer = AccessTools.Field(__originalMethod.DeclaringType, "dealer");
        var props = AccessTools.Field(__originalMethod.DeclaringType, "props");
        var blocked = AccessTools.PropertyGetter(typeof(DamageResult), nameof(DamageResult.WasFullyBlocked));
        int resolvedIndex = Enumerable.Range(0, codes.Count - 1).Single(index => codes[index].Calls(blocked) &&
            (codes[index + 1].opcode == OpCodes.Brtrue || codes[index + 1].opcode == OpCodes.Brtrue_S));
        if (resolvedIndex < 2 || codes[resolvedIndex - 1].operand is not FieldInfo resolvedField || resolvedField.FieldType != typeof(DamageResult))
            throw new InvalidOperationException("King impact: missing resolved damage-result load");
        // The closure local survives through native VFX calls. Copy only opcodes
        // and operands, preserving the original branch labels at their sites.
        var loadResult = codes.Skip(resolvedIndex - 2).Take(2)
            .Select(code => new CodeInstruction(code.opcode, code.operand)).ToArray();
        var spark = AccessTools.DeclaredMethod(typeof(NHitSparkVfx), nameof(NHitSparkVfx.Create));
        var cardCenter = AccessTools.DeclaredMethod(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter));
        var cardBase = AccessTools.DeclaredMethod(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreature));
        var result = AccessTools.GetDeclaredFields(__originalMethod.DeclaringType)
            .Single(field => field.Name.StartsWith("<unblockedDamageResult>", StringComparison.Ordinal));
        int changed = 0;
        int blocks = 0, blockSounds = 0, sparks = 0, cardEffects = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            // This is the resolved-result branch that skips hurt animations for
            // full blocks. The other getters belong to VFX/death bookkeeping.
            if (codes[i].Calls(blocked) && i + 1 < codes.Count &&
                (codes[i + 1].opcode == OpCodes.Brtrue || codes[i + 1].opcode == OpCodes.Brtrue_S))
            {
                var first = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[i]);
                codes.InsertRange(i, [first, new CodeInstruction(OpCodes.Ldfld, dealer),
                    new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, props),
                    new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, targets)]);
                codes[i + 6].opcode = OpCodes.Call;
                codes[i + 6].operand = KingAudioPatchTargets.Wrapper(nameof(KingWeaponAudio.ObserveResult));
                blocks++;
                i += 6;
                continue;
            }
            if (codes[i].Calls(spark) || codes[i].Calls(cardCenter) || codes[i].Calls(cardBase))
            {
                bool isSpark = codes[i].Calls(spark);
                string wrapper = isSpark ? nameof(KingEnemyImpact.CreateNativeSpark) :
                    codes[i].Calls(cardCenter) ? nameof(KingEnemyImpact.PlayCardCenter) : nameof(KingEnemyImpact.PlayCardBase);
                var extra = loadResult.Select(code => new CodeInstruction(code.opcode, code.operand)).ToList();
                extra[0].MoveLabelsFrom(codes[i]);
                if (!isSpark) extra.AddRange([new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, targets)]);
                codes.InsertRange(i, extra);
                codes[i + extra.Count].opcode = OpCodes.Call;
                codes[i + extra.Count].operand = AccessTools.Method(typeof(KingEnemyImpact), wrapper);
                if (isSpark) sparks++; else cardEffects++;
                i += extra.Count;
                continue;
            }
            if (codes[i].Calls(KingAudioPatchTargets.Play) && i >= 2 &&
                codes[i - 2].opcode == OpCodes.Ldstr && Equals(codes[i - 2].operand, "event:/sfx/block_hit"))
            {
                var first = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[i]);
                codes.InsertRange(i, [first, new CodeInstruction(OpCodes.Ldfld, result)]);
                codes[i + 2].opcode = OpCodes.Call;
                codes[i + 2].operand = AccessTools.Method(typeof(KingDefense), nameof(KingDefense.PlayBlockSound));
                blockSounds++;
                i += 2;
                continue;
            }
            if (!codes[i].Calls(loseHp)) continue;
            var owner = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[i]);
            codes.InsertRange(i, [owner, new CodeInstruction(OpCodes.Ldfld, targets)]);
            codes[i + 2].opcode = OpCodes.Call;
            codes[i + 2].operand = KingAudioPatchTargets.Wrapper(nameof(KingWeaponAudio.ApplyHit));
            changed++;
            i += 2;
        }
        if (changed != 3) throw new InvalidOperationException($"King audio: expected 3 HP loss sites, found {changed}");
        if (blocks != 1 || blockSounds != 1)
            throw new InvalidOperationException($"King defense: unexpected block sites ({blocks}, {blockSounds})");
        if (sparks != 1 || cardEffects != 2)
            throw new InvalidOperationException($"King impact: unexpected VFX sites ({sparks}, {cardEffects})");
        return codes;
    }
}
