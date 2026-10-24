using System;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

internal static class AudioCheck
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal static void Validate(Assembly mod, Node host, PackedScene scene)
    {
        // Compile the actual async MoveNext patches against the installed game; remove them after validation.
        var harmony = new Harmony("king_audio_native_validation");
        var patches = mod.GetTypes().Where(type => type.Name.StartsWith("KingAudio_", StringComparison.Ordinal)).ToArray();
        try
        {
            foreach (var patch in patches) harmony.CreateClassProcessor(patch).Patch();
            Require(patches.Length == 3, "Three audio patch classes");
            var audio = mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponAudio", true)!;
            var catalog = mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponCatalog", true)!;
            var weaponType = catalog.GetNestedType("Weapon", BindingFlags.NonPublic)!;
            var stageType = catalog.GetNestedType("Stage", BindingFlags.NonPublic)!;
            var soundType = catalog.GetNestedType("Sound", BindingFlags.NonPublic)!;
            var cueType = catalog.GetNestedType("Cue", BindingFlags.NonPublic)!;
            var weapons = (Array)catalog.GetField("Weapons", Static)!.GetValue(null)!;
            var stages = weapons.Cast<object>().SelectMany(w => ((Array)weaponType.GetProperty("Stages")!.GetValue(w)!).Cast<object>())
                .Where(stage => stageType.GetProperty("Audio")!.GetValue(stage) != null).ToArray();
            Require(stages.Length == 7, "Only two sample weapons / seven stages");
            int sfxBus = AudioServer.GetBusIndex("SFX");
            if (sfxBus < 0)
            {
                AudioServer.AddBus();
                sfxBus = AudioServer.BusCount - 1;
                AudioServer.SetBusName(sfxBus, "SFX");
            }
            AudioServer.SetBusMute(sfxBus, true); // Silent validation process.
            var actor = scene.Instantiate<Node2D>();
            host.AddChild(actor);
            int Voices() => actor.GetChildren().OfType<AudioStreamPlayer>().Count(player => !player.IsQueuedForDeletion());
            foreach (var stage in stages)
            {
                var sound = stageType.GetProperty("Audio")!.GetValue(stage)!;
                foreach (var name in new[] { "Release", "Hit" })
                {
                    var cue = soundType.GetProperty(name)!.GetValue(sound)!;
                    var stream = (AudioStreamOggVorbis)audio.GetMethod("LoadCue", Static)!.Invoke(null, [cue])!;
                    Require(stream != null && stream.GetLength() > 0 && !stream.Loop, "Native Ogg decoding");
                }
            }
            audio.GetMethod("OnAnimation", Static)!.Invoke(null, [actor, stages[0]]);
            Require(Voices() == 1, "One release per animation");
            Require(actor.GetChildren().OfType<AudioStreamPlayer>().All(player => player.Bus == "SFX"), "Game SFX bus");
            var executionType = audio.GetNestedType("Execution", BindingFlags.NonPublic)!;
            var execution = Activator.CreateInstance(executionType, Instance, null, [actor], null)!;
            executionType.GetField("Stage", Instance)!.SetValue(execution, stages[0]);
            executionType.GetField("Started", Instance)!.SetValue(execution, true);
            var command = new AttackCommand(6m);
            var executions = audio.GetField("Executions", Static)!.GetValue(null)!;
            executions.GetType().GetMethod("Add")!.Invoke(executions, [command, execution]);
            var replace = audio.GetMethod("ReplacesHit", Static)!;
            Require((bool)replace.Invoke(null, [command, "blunt_attack.mp3"])!, "Generic hit replaced for bound command");
            Require(!(bool)replace.Invoke(null, [command, "event:/sfx/characters/regent/regent_sovereign_blade"])!, "Unique sovereign sound retained");
            Require(!(bool)replace.Invoke(null, [new AttackCommand(6m), "blunt_attack.mp3"])!, "Other command unaffected");

            var victim = new Creature(null!, 100, 100);
            var otherVictim = new Creature(null!, 100, 100);
            var targets = new[] { victim, otherVictim };
            var groupType = audio.GetNestedType("ImpactGroup", BindingFlags.NonPublic)!;
            var hitting = audio.GetField("Hitting", Static)!.GetValue(null)!;
            void NewGroup() => hitting.GetType().GetProperty("Value")!.SetValue(hitting,
                Activator.CreateInstance(groupType, Instance, null, [execution, targets], null));
            var apply = audio.GetMethod("ApplyHit", Static)!;
            NewGroup();
            apply.Invoke(null, [victim, 1m, ValueProp.Move, new[] { victim }]);
            Require(Voices() == 1, "Nested unrelated damage cannot steal the command impact");
            apply.Invoke(null, [victim, 5m, ValueProp.Move, targets]);
            apply.Invoke(null, [otherVictim, 6m, ValueProp.Move, targets]);
            Require(Voices() == 2, "AOE plays once per hit group");
            Require(victim.CurrentHp == 94 && otherVictim.CurrentHp == 94, "Actual HP-loss result unchanged");
            NewGroup();
            apply.Invoke(null, [victim, 0m, ValueProp.Move, targets]);
            Require(Voices() == 2, "Full block does not play flesh/metal hit");
            NewGroup();
            Require(Voices() == 2, "Empty hit group remains silent");
            apply.Invoke(null, [victim, 2m, ValueProp.Move, targets]);
            Require(Voices() == 3, "Next multi-hit impact plays separately");
            hitting.GetType().GetProperty("Value")!.SetValue(hitting, null);
            apply.Invoke(null, [victim, 4m, ValueProp.Move, targets]);
            Require(Voices() == 3, "Unbound command cannot borrow last actor weapon");
            executionType.GetField("Started", Instance)!.SetValue(execution, false);
            Require(!(bool)replace.Invoke(null, [command, "blunt_attack.mp3"])!, "No actual animation keeps original hit");
            var unsupported = weapons.Cast<object>().SelectMany(w => ((Array)weaponType.GetProperty("Stages")!.GetValue(w)!).Cast<object>())
                .First(stage => stageType.GetProperty("Audio")!.GetValue(stage) == null);
            audio.GetMethod("OnAnimation", Static)!.Invoke(null, [actor, unsupported]);
            Require(Voices() == 3, "Other weapons retain original audio");
            var sound0 = stageType.GetProperty("Audio")!.GetValue(stages[0])!;
            var cue0 = soundType.GetProperty("Hit")!.GetValue(sound0)!;
            for (int i = 0; i < 12; i++) audio.GetMethod("PlayCue", Static)!.Invoke(null, [actor, cue0]);
            Require(Voices() == 8, "Per-actor voice bound");
            actor.Hide();
            audio.GetMethod("PlayCue", Static)!.Invoke(null, [actor, cue0]);
            Require(Voices() == 8, "Hidden skin does not emit new sound");
            var voices = actor.GetChildren().OfType<AudioStreamPlayer>().ToArray();
            actor.Free();
            Require(voices.All(player => !GodotObject.IsInstanceValid(player)), "Actor audio cleanup");
            GD.Print("AUDIO_NATIVE_PASS: .NET " + System.Environment.Version + "; real async patches compile; 7 sample stages; original Ogg decode, release, per-command hit suppression, immediate HP-loss impact, AOE/multi-hit/block/miss, nested damage isolation, unsupported fallback, SFX bus, voice limit, cleanup");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Audio validation failed: " + message);
    }
}
