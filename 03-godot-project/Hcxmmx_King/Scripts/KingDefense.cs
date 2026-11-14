using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Hcxmmx.KingMod.Scripts;

internal static class KingDefense
{
    internal const string NodeName = "KingShield";
    private static readonly ConditionalWeakTable<DamageResult, object> HandledBlocks = new();
    private static readonly KingWeaponCatalog.Cue HurtCue = new("res://Hcxmmx_King/Presentation/ShieldAssets/hero_dmg1.ogg");
    private static readonly Func<AttackCommand, IReadOnlyList<Creature>> PossibleTargets =
        AccessTools.MethodDelegate<Func<AttackCommand, IReadOnlyList<Creature>>>(
            AccessTools.DeclaredMethod(typeof(AttackCommand), "GetPossibleTargets"));

    internal static void PlayHurt(Node2D actor) => KingWeaponAudio.PlayCue(actor, HurtCue);

    internal static bool ReplacesIncomingHit(AttackCommand command)
    {
        if (command.Attacker?.IsMonster != true || !command.DamageProps.HasFlag(ValueProp.Move) ||
            KingWeaponAudio.LoadCue(HurtCue) == null) return false;
        var targets = PossibleTargets(command);
        return targets.Count > 0 && targets.All(target => FindActor(target) != null);
    }

    internal static bool ObserveBlock(DamageResult result, Creature? dealer, ValueProp props)
    {
        bool blocked = result.WasFullyBlocked;
        if (!ShouldBlock(result, dealer, props)) return blocked;

        var actor = FindActor(result.Receiver);
        if (actor != null && PlayBlock(actor)) HandledBlocks.GetValue(result, _ => new object());
        return blocked;
    }

    internal static bool ShouldBlock(DamageResult result, Creature? dealer, ValueProp props) =>
        result.WasFullyBlocked && result.UnblockedDamage == 0 && result.BlockedDamage > 0 &&
        dealer != null && !ReferenceEquals(dealer, result.Receiver) && props.HasFlag(ValueProp.Move) &&
        !props.HasFlag(ValueProp.Unblockable) && !props.HasFlag(ValueProp.SkipHurtAnim);

    internal static Node2D? FindActor(Creature? target)
    {
        if (NonInteractiveMode.IsActive || KingGlobals.IsInShop || target?.Player == null || target.IsDead ||
            !string.Equals(target.Player.Character.Id.Entry, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase))
            return null;
        var actor = target.GetCreatureNode()?.Visuals?.GetNodeOrNull<Node2D>("KingMecha");
        if (actor == null || !actor.IsVisibleInTree()) return null;
        var state = KingPresentation.For(actor);
        return state.IsDead || state.CombatEnded ? null : actor;
    }

    internal static bool PlayBlock(Node2D actor)
    {
        var shield = actor.GetNodeOrNull<Node2D>(NodeName);
        if (shield == null) return false;
        // Repeated hits keep one shield type while renewing its contact feedback.
        int index = shield.Get("active").AsBool() ? shield.Get("variant_index").AsInt32() : KingGlobals.Rng.Next(3);
        KingPresentation.Interrupt(actor);
        KingPresentation.For(actor).ResetCombo();
        string sound = shield.Call("block", index, KingGlobals.UseOriginalTiming ? 1 : 0).AsString();
        KingWeaponAudio.PlayCue(actor, new KingWeaponCatalog.Cue(sound));
        return true;
    }

    internal static void PlayBlockSound(string sound, float volume, DamageResult result)
    {
        if (!HandledBlocks.TryGetValue(result, out _)) SfxCmd.Play(sound, volume);
    }
}
