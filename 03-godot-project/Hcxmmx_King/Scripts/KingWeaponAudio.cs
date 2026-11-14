using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Hcxmmx.KingMod.Scripts;

internal static class KingWeaponAudio
{
    private sealed class Execution(Node2D actor)
    {
        internal readonly Node2D Actor = actor;
        internal KingWeaponCatalog.Stage? Stage;
        internal bool Started;
        internal KingRanged.Shot? Shot;
    }
    private sealed class ImpactGroup(Execution execution, object? targets)
    {
        internal readonly Execution Execution = execution;
        internal readonly KingWeaponCatalog.Stage Stage = execution.Stage!;
        internal readonly object? Targets = targets;
        internal bool Played;
    }

    private static readonly ConditionalWeakTable<AttackCommand, Execution> Executions = new();
    private static readonly AsyncLocal<Execution?> Animating = new();
    private static readonly AsyncLocal<ImpactGroup?> Hitting = new();
    private static readonly Dictionary<string, AudioStreamOggVorbis?> Streams = new(StringComparer.Ordinal);
    private static readonly StringName VoiceMarker = new("KingWeaponSoundVoice");
    private static readonly HashSet<string> GenericSounds = new(StringComparer.OrdinalIgnoreCase)
    {
        "slash_attack.mp3", "blunt_attack.mp3", "heavy_attack.mp3",
        "event:/sfx/characters/regent/regent_attack"
    };
    private static readonly AccessTools.FieldRef<AttackCommand, string?> Animation =
        AccessTools.FieldRefAccess<AttackCommand, string?>("_attackerAnimName");
    private static readonly AccessTools.FieldRef<AttackCommand, bool> ShouldAnimate =
        AccessTools.FieldRefAccess<AttackCommand, bool>("_shouldPlayAnimation");
    private static readonly AccessTools.FieldRef<AttackCommand, Creature?> VisualAttacker =
        AccessTools.FieldRefAccess<AttackCommand, Creature?>("_visualAttacker");

    private static bool IsAttack(string? trigger) => trigger is "Attack" or "AttackSingle" or "AttackTriple";

    private static Node2D? FindActor(Creature? creature)
    {
        if (NonInteractiveMode.IsActive || KingGlobals.IsInShop || creature?.Player == null || creature.IsDead ||
            !string.Equals(creature.Player.Character.Id.Entry, KingGlobals.TargetCharacterId, StringComparison.OrdinalIgnoreCase)) return null;
        var actor = creature.GetCreatureNode()?.Visuals?.GetNodeOrNull<Node2D>("KingMecha");
        if (actor == null || !actor.IsVisibleInTree() || KingPresentation.For(actor).IsDead || KingPresentation.For(actor).CombatEnded) return null;
        return actor;
    }

    internal static AudioStreamOggVorbis? LoadCue(KingWeaponCatalog.Cue cue)
    {
        if (Streams.TryGetValue(cue.Path, out var cached)) return cached;
        AudioStreamOggVorbis? stream = null;
        if (Godot.FileAccess.FileExists(cue.Path))
        {
            stream = AudioStreamOggVorbis.LoadFromBuffer(Godot.FileAccess.GetFileAsBytes(cue.Path));
            if (stream != null) stream.Loop = false;
        }
        Streams[cue.Path] = stream;
        if (stream == null) GD.PushWarning("[King] Missing/invalid weapon audio; retaining original sound: " + cue.Path);
        return stream;
    }

    private static bool Ready(KingWeaponCatalog.Stage? stage) => stage?.Audio is { } sound &&
        LoadCue(sound.Release) != null && LoadCue(sound.Hit) != null;

    internal static void PlayCue(Node2D actor, KingWeaponCatalog.Cue cue) => PlayVoice(actor, cue, "");

    private static void PlayVoice(Node2D actor, KingWeaponCatalog.Cue cue, string kind)
    {
        if (!GodotObject.IsInstanceValid(actor) || !actor.IsInsideTree() || !actor.IsVisibleInTree() || NonInteractiveMode.IsActive) return;
        var stream = LoadCue(cue);
        if (stream == null) return;
        // Use the game's existing SFX bus: volume sliders and background mute apply normally.
        var voices = actor.GetChildren().OfType<AudioStreamPlayer>()
            .Where(player => player.HasMeta(VoiceMarker) && !player.IsQueuedForDeletion()).ToArray();
        if (voices.Length >= 8) { voices[0].Stop(); voices[0].QueueFree(); }
        var player = new AudioStreamPlayer
        {
            Name = "KingWeaponSound", Stream = stream, VolumeDb = cue.VolumeDb,
            Bus = AudioServer.GetBusIndex("SFX") >= 0 ? "SFX" : "Master"
        };
        player.SetMeta(VoiceMarker, true);
        player.SetMeta("KingCueKind", kind);
        actor.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play(cue.OffsetSeconds);
    }

    internal static void StopOwnedVoices(Node2D actor, bool chargeOnly = false)
    {
        foreach (var player in actor.GetChildren().OfType<AudioStreamPlayer>())
        {
            if (!player.HasMeta(VoiceMarker) || (chargeOnly && player.GetMeta("KingCueKind", "").AsString() != "charge")) continue;
            player.Stop();
            player.QueueFree();
        }
    }

    internal static void PlayTimelineCue(Node2D actor, KingWeaponCatalog.Stage stage, string kind)
    {
        if (KingPresentation.For(actor).IsDead || KingPresentation.For(actor).CombatEnded || !Ready(stage)) return;
        var cue = kind == "charge" ? stage.Audio!.Charge : kind == "release" ? stage.Audio!.Release : null;
        if (cue != null) PlayVoice(actor, cue, kind);
    }

    // Only this call site in CreatureCmd.TriggerAnim's Attack branch is replaced.
    internal static void PlayCharacterAttack(string sound, float volume, Creature creature)
    {
        var actor = FindActor(creature);
        if (actor != null && Ready(KingPresentation.PeekRequest(actor))) return;
        SfxCmd.Play(sound, volume);
    }

    private static Execution? Prepare(AttackCommand command)
    {
        if (!ShouldAnimate(command) || !IsAttack(Animation(command)) ||
            command.ModelSource?.Id.Entry == "SOVEREIGN_BLADE" ||
            (VisualAttacker(command) != null && !ReferenceEquals(VisualAttacker(command), command.Attacker))) return null;
        var actor = FindActor(command.Attacker);
        if (actor == null) return null;
        if (Executions.TryGetValue(command, out var old) && !ReferenceEquals(old.Actor, actor))
            Executions.Remove(command);
        var execution = Executions.GetValue(command, _ => new Execution(actor));
        if (!execution.Started) execution.Stage = KingPresentation.PeekRequest(actor);
        return execution;
    }

    // Scope exists inside this async wrapper; each command carries its own animation/weapon association.
    internal static async Task TriggerAttack(Creature creature, string trigger, float delay, AttackCommand command)
    {
        var previous = Animating.Value;
        Animating.Value = IsAttack(trigger) ? Prepare(command) : null;
        try { await CreatureCmd.TriggerAnim(creature, trigger, delay); }
        finally { Animating.Value = previous; }
    }

    internal static long BindAttack(Node2D actor, KingWeaponCatalog.Stage stage)
    {
        var execution = Animating.Value;
        if (execution != null && ReferenceEquals(execution.Actor, actor))
        {
            execution.Stage = stage;
            execution.Started = true;
            if (stage.Body == "FastBow")
            {
                long pending = actor.GetNodeOrNull<Node>(KingPresentation.TimelineName)?.Call("pending_shot").AsInt64() ?? 0;
                execution.Shot = KingRanged.Prepare(actor, pending);
                return execution.Shot.Id;
            }
        }
        return 0;
    }

    internal static void OnAnimation(Node2D actor, KingWeaponCatalog.Stage stage)
    {
        BindAttack(actor, stage);
        if (Ready(stage)) PlayCue(actor, stage.Audio!.Release);
    }

    internal static bool ReplacesAttacker(AttackCommand command, string sound) => GenericSounds.Contains(sound) && Ready(Prepare(command)?.Stage);
    internal static bool ReplacesHit(AttackCommand command, string sound) => GenericSounds.Contains(sound) &&
        ((Executions.TryGetValue(command, out var execution) && execution.Started &&
        GodotObject.IsInstanceValid(execution.Actor) && execution.Actor.IsVisibleInTree() && Ready(execution.Stage)) ||
        KingDefense.ReplacesIncomingHit(command));

    internal static async Task<IEnumerable<DamageResult>> DealDamage(PlayerChoiceContext choiceContext,
        IEnumerable<DamageTargetData>? targets, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay, ModifyDamageHookType modifyDamageHookType, AttackCommand command)
    {
        var previous = Hitting.Value;
        Hitting.Value = Executions.TryGetValue(command, out var execution) && execution.Started && Ready(execution.Stage)
            ? new ImpactGroup(execution, targets) : null;
        try { return await CreatureCmd.Damage(choiceContext, targets, amount, props, dealer, cardSource, cardPlay, modifyDamageHookType); }
        finally { Hitting.Value = previous; }
    }

    internal static DamageResult ApplyHit(Creature target, decimal amount, ValueProp props, object? targets)
    {
        var result = target.LoseHpInternal(amount, props);
        var group = Hitting.Value;
        // Match this exact Damage invocation, so nested status/reflection damage cannot borrow its sound.
        // Play before Damage's post-hit waits/death hooks, once per AOE group, only on actual HP loss.
        if (group != null && !group.Played && ReferenceEquals(group.Targets, targets) && result.UnblockedDamage > 0 &&
            !KingPresentation.For(group.Execution.Actor).IsDead && !KingPresentation.For(group.Execution.Actor).CombatEnded)
        {
            group.Played = true;
            PlayVoice(group.Execution.Actor, group.Stage.Audio!.Hit, "hit");
        }
        return result;
    }

    internal static bool EnsureImpact(DamageResult result, object? targets)
    {
        var group = Hitting.Value;
        if (group == null || !ReferenceEquals(group.Targets, targets)) return false;
        var actor = group.Execution.Actor;
        if (!GodotObject.IsInstanceValid(actor) || !actor.IsInsideTree() || !actor.IsVisibleInTree()) return false;
        var state = KingPresentation.For(actor);
        if (!state.IsDead && !state.CombatEnded) KingRanged.Resolve(group.Execution.Shot, result);
        return !state.IsDead && !state.CombatEnded && KingEnemyImpact.Emit(actor, group.Stage.Body, result);
    }

    internal static bool ObserveResult(DamageResult result, Creature? dealer, ValueProp props, object? targets)
    {
        EnsureImpact(result, targets);
        return KingDefense.ObserveBlock(result, dealer, props);
    }

    internal static void PlaySideVfx(CombatSide side, string path, ICombatState combat, AttackCommand command)
    {
        if (KingEnemyImpact.Generic(path) && Executions.TryGetValue(command, out var execution) && execution.Started &&
            GodotObject.IsInstanceValid(execution.Actor) && execution.Actor.IsVisibleInTree() &&
            !KingPresentation.For(execution.Actor).IsDead && !KingPresentation.For(execution.Actor).CombatEnded &&
            KingEnemyImpact.CanReplaceSide(combat, side, execution.Stage!.Body)) return;
        VfxCmd.PlayOnSide(side, path, combat);
    }
}
