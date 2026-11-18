using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

internal static class RangedCheck
{
    private const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Dictionary<Creature, NCreature> Nodes = new();
    private static readonly Dictionary<NCreature, Vector2> Points = new();
    private static Control container = null!;

    private static bool CreatureNode(Creature __instance, ref NCreature? __result)
    {
        __result = Nodes.GetValueOrDefault(__instance);
        return false;
    }
    private static bool Container(ref Control __result) { __result = container; return false; }
    private static bool Position(NCreature __instance, ref Vector2 __result) { __result = Points[__instance]; return false; }

    internal static void Validate(Assembly mod, Node host, PackedScene scene)
    {
        var harmony = new Harmony("king_ranged_validation");
        container = new Control { Position = new Vector2(80, 40), Scale = Vector2.One * 1.5f };
        host.AddChild(container);
        var actor = scene.Instantiate<Node2D>();
        host.AddChild(actor);
        try
        {
            harmony.Patch(AccessTools.Method(typeof(Creature), "GetCreatureNode"), prefix: new HarmonyMethod(typeof(RangedCheck), nameof(CreatureNode)));
            harmony.Patch(AccessTools.Method(typeof(Creature), "GetVfxContainer"), prefix: new HarmonyMethod(typeof(RangedCheck), nameof(Container)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(NCreature), "VfxSpawnPosition"), prefix: new HarmonyMethod(typeof(RangedCheck), nameof(Position)));
            var targets = new[] { new Creature(null!, 100, 100), new Creature(null!, 100, 100) };
            for (int index = 0; index < targets.Length; index++)
            {
                var node = new NCreature();
                Nodes.Add(targets[index], node);
                Points.Add(node, new Vector2(650 + index * 240, 300));
            }
            var presentation = mod.GetType("Hcxmmx.KingMod.Scripts.KingPresentation", true)!;
            var ranged = mod.GetType("Hcxmmx.KingMod.Scripts.KingRanged", true)!;
            var globals = mod.GetType("Hcxmmx.KingMod.Scripts.KingGlobals", true)!;
            presentation.GetMethod("Attach", All)!.Invoke(null, [actor, actor.GetNode<AnimatedSprite2D>("Body"), null]);
            var timeline = actor.GetNode("KingCombatTimeline");
            timeline.SetProcess(false);
            var body = actor.GetNode<AnimatedSprite2D>("Body");
            var prepare = ranged.GetMethod("Prepare", All)!;
            var resolve = ranged.GetMethod("Resolve", All)!;
            var release = ranged.GetMethod("Release", All)!;
            foreach (int mode in new[] { 0, 1 })
            {
                timeline.Call("interrupt");
                timeline.Call("set_mode", mode);
                globals.GetField("UseOriginalTiming", All)!.SetValue(null, mode == 1);
                var catalog = mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponCatalog", true)!;
                var bow = ((Array)catalog.GetField("Weapons", All)!.GetValue(null)!).Cast<object>()
                    .Single(weapon => (string)weapon.GetType().GetProperty("Id")!.GetValue(weapon)! == "fast_bow");
                var state = presentation.GetMethod("For", All)!.Invoke(null, [actor])!;
                state.GetType().GetMethod("ResetCombo", All)!.Invoke(state, null);
                state.GetType().GetField("_weapon", All)!.SetValue(state, bow);
                var audio = mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponAudio", true)!;
                var execution = Activator.CreateInstance(audio.GetNestedType("Execution", BindingFlags.NonPublic)!, All, null, [actor], null)!;
                var animating = audio.GetField("Animating", All)!.GetValue(null)!;
                animating.GetType().GetProperty("Value")!.SetValue(animating, execution);
                try { presentation.GetMethod("RequestAttack", All)!.Invoke(null, [actor]); }
                finally { animating.GetType().GetProperty("Value")!.SetValue(animating, null); }
                var shot = execution.GetType().GetField("Shot", All)!.GetValue(execution)!;
                long id = (long)shot.GetType().GetField("Id", All)!.GetValue(shot)!;
                double charge = mode == 1 ? 0.1 : 0.07;
                timeline.Call("advance", charge - 0.0001);
                Require(!shot.GetType().GetField("Released", All)!.GetValue(shot)!.Equals(true), "Arrow waits for release pose");
                resolve.Invoke(null, [shot, new DamageResult(targets[0], ValueProp.Move) { UnblockedDamage = 6 }]);
                Require(!container.HasNode("KingArrowLayer"), "Resolved damage never emits an early arrow");
                timeline.Call("advance", 0.0002);
                Require(body.Frame >= 12, "Source half-cycle release pose");
                var layer = container.GetNode<Node2D>("KingArrowLayer");
                var texture = layer.Get("arrow").As<Texture2D>();
                Require(texture != null && texture.GetWidth() > 0 && texture.GetHeight() > 0, "Decoded arrow texture is nonempty");
                layer.SetProcess(false);
                Godot.Collections.Array Flights() => layer.Get("flights").AsGodotArray();
                Require(Flights().Count == 1, "Timeline release reaches compiled C# and room projectile layer");
                var flight = Flights()[0].AsGodotDictionary();
                Require(layer.ToGlobal(flight["point"].AsVector2()).IsEqualApprox(Points[Nodes[targets[0]]]), "Transformed actual target endpoint");
                var redirected = new DamageResult(targets[1], ValueProp.Move) { UnblockedDamage = 6 };
                var originalTarget = typeof(DamageResult).GetProperty("OriginalTarget");
                if (originalTarget != null)
                {
                    originalTarget.SetValue(redirected, targets[0]);
                    resolve.Invoke(null, [shot, redirected]);
                    Require(Flights().Count == 1 && layer.ToGlobal(Flights()[0].AsGodotDictionary()["point"].AsVector2()).IsEqualApprox(Points[Nodes[targets[1]]]), "Damage redirection retargets existing arrow");
                }
                resolve.Invoke(null, [shot, new DamageResult(targets[1], ValueProp.Move) { WasFullyBlocked = true, BlockedDamage = 6 }]);
                Require(Flights().Count == 2, "Additional AOE receiver includes fully blocked target");
                presentation.GetMethod("Interrupt", All)!.Invoke(null, [actor, false]);
                Require(Flights().Count == 2, "Launched arrows survive hurt interruption");
                layer.Call("advance", 1.0);
                Require(Flights().Count == 0, "Projectiles expire independently");
                // Random targeting may become known after the release frame.
                shot = prepare.Invoke(null, [actor, 0L])!;
                id = (long)shot.GetType().GetField("Id", All)!.GetValue(shot)!;
                release.Invoke(null, [actor, id]);
                Require(Flights().Count == 0, "Unknown random target creates no speculative arrows");
                resolve.Invoke(null, [shot, new DamageResult(targets[0], ValueProp.Move)]);
                Require(Flights().Count == 1, "Late resolved target completes a released shot");
                actor.Hide();
                ranged.GetMethod("Cancel", All)!.Invoke(null, [actor, false]);
                Require(Flights().Count == 0, "Hidden skin clears launched arrows");
                actor.Show();
                var queued = prepare.Invoke(null, [actor, 0L])!;
                id = (long)queued.GetType().GetField("Id", All)!.GetValue(queued)!;
                Require(ReferenceEquals(queued, prepare.Invoke(null, [actor, id])), "Merged pending requests share a shot");
                ranged.GetMethod("Cancel", All)!.Invoke(null, [actor, false]);
                resolve.Invoke(null, [queued, new DamageResult(targets[0], ValueProp.Move)]);
                release.Invoke(null, [actor, id]);
                Require(Flights().Count == 0, "Cancelled wind-up cannot emit a late arrow");
                layer.Free();
            }
            GD.Print("RANGED_GAME_PASS: shipped bow frames, both release timings, compiled timeline bridge, actual/redirected/AOE/blocked/late targets, room transforms, pending merge and interruption cleanup");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            actor.Free();
            container.Free();
            foreach (var node in Nodes.Values) node.Free();
            Nodes.Clear();
            Points.Clear();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Ranged game: " + message);
    }
}
