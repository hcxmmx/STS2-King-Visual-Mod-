using System;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

internal static class EnemyImpactCheck
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Validate(Assembly mod, Node host)
    {
        var impact = mod.GetType("Hcxmmx.KingMod.Scripts.KingEnemyImpact", true)!;
        var container = new Control { Position = new Vector2(100, 40), Scale = Vector2.One * 1.5f };
        host.AddChild(container);
        var getLayer = impact.GetMethod("GetLayer", Static)!;
        var layer = (Node2D)getLayer.Invoke(null, [container])!;
        layer.SetProcess(false);
        var ordinary = (Node2D)layer.Get("ordinary").AsGodotObject();
        Require(ReferenceEquals(layer, getLayer.Invoke(null, [container])), "One shared room layer");
        Require(layer.Get("textures").AsGodotDictionary().Count == 4, "Original tiles loaded from deployed pack");
        var stages = layer.Get("definitions").AsGodotDictionary()["stages"].AsGodotDictionary();
        Require(stages.Count == 44, "All current stages mapped");
        foreach (var body in stages.Keys)
        {
            Require(layer.Call("can_spawn", body).AsBool(), "Valid stage ready");
            Require(layer.Call("spawn_hit", new Vector2(250, 130), Vector2.Right, 2.5f, body, false).AsBool(), "Native GDScript hit");
        }
        Require(ordinary.Get("hits").AsGodotArray().Count == 32, "Room-wide particle bound");
        layer.Call("clear");
        Require(!layer.Call("spawn_hit", Vector2.Zero, Vector2.Right, 2.5f, "missing", false).AsBool(), "Unsupported stage falls back");
        Require(layer.Call("spawn_hit", Vector2.Zero, Vector2.Right, 2.5f, "missing", true).AsBool(), "Armor style independent of weapon");
        layer.Call("advance", 1.0);
        Require(layer.Get("hits").AsGodotArray().Count == 0, "Effects expire");
        Require(ordinary.Get("hits").AsGodotArray().Count == 0, "Ordinary effects expire");

        var actor = new Node2D { Scale = Vector2.One * 4 };
        var targetBody = new Node2D { Modulate = new Color(0.8f, 0.6f, 0.4f, 0.7f) };
        host.AddChild(actor);
        host.AddChild(targetBody);
        var baseColor = targetBody.Modulate;
        string weaponBody = stages.Keys.First().AsString();
        var present = impact.GetMethod("Present", Static)!;
        bool Present(DamageResult result) => (bool)present.Invoke(null,
            [layer, actor, targetBody, new Vector2(450, 200), new Vector2(450, 400), weaponBody, result])!;
        Godot.Collections.Dictionary Latest() => ordinary.Get("hits").AsGodotArray().Last().AsGodotDictionary();
        var alive = new Creature(null!, 100, 100);
        var dead = new Creature(null!, 0, 100);
        var normal = new DamageResult(alive, ValueProp.Move) { UnblockedDamage = 6 };
        Require(Present(normal), "Compiled C# damage-result entry");
        Require(Latest()["particles"].AsGodotArray().Count == 0, "Ordinary hit has no chunks");
        var visual = (Node2D)Latest()["layer"].AsGodotObject();
        Require(visual.GlobalPosition.IsEqualApprox(new Vector2(450, 200)), "World-space target anchor");
        Require(visual.GlobalScale.IsEqualApprox(Vector2.One * 8), "Accepted 2x scale across room transform");
        Require(targetBody.Modulate.G < baseColor.G && Mathf.IsEqualApprox(targetBody.Modulate.A, baseColor.A), "Body blink preserves alpha");
        Require(Present(normal) && ordinary.Get("hits").AsGodotArray().Count == 1, "One visual per result");
        Require(impact.GetMethod("CreateNativeSpark", Static)!.Invoke(null, [alive, true, normal]) == null, "Handled result suppresses native spark");
        layer.Call("advance", 0.2);
        Require(targetBody.Modulate.IsEqualApprox(baseColor), "Blink restores original color");
        var lethal = new DamageResult(dead, ValueProp.Move) { UnblockedDamage = 6, WasTargetKilled = true };
        Require(Present(lethal) && Latest()["particles"].AsGodotArray().Count == 15, "Actual lethal result generates chunks");
        var revived = new DamageResult(alive, ValueProp.Move) { UnblockedDamage = 6, WasTargetKilled = true };
        Require(Present(revived) && Latest()["particles"].AsGodotArray().Count == 0, "Living receiver does not shatter");
        var repeated = new DamageResult(dead, ValueProp.Move) { UnblockedDamage = 6 };
        Require(Present(repeated) && Latest()["particles"].AsGodotArray().Count == 0, "Dead receiver alone cannot retrigger chunks");
        Require(!Present(new DamageResult(alive, ValueProp.Move)), "Zero damage does not emit");
        layer.Call("clear");
        Require(targetBody.Modulate.IsEqualApprox(baseColor), "Clear restores blink");
        var blocked = new DamageResult(alive, ValueProp.Move) { WasFullyBlocked = true, BlockedDamage = 6 };
        Require(Present(blocked) && ordinary.Get("hits").AsGodotArray().Count == 0 &&
            layer.Get("hits").AsGodotArray().Count == 1, "Full block selects armor");
        Require(targetBody.Modulate.IsEqualApprox(baseColor), "Full block keeps body color");
        var next = new DamageResult(alive, ValueProp.Move) { UnblockedDamage = 3 };
        Present(next);
        targetBody.Modulate = Colors.Blue;
        layer.Call("advance", 0.05);
        layer.Call("clear");
        Require(targetBody.Modulate == Colors.Blue, "External tint change retains ownership");
        targetBody.Modulate = baseColor;
        Present(new DamageResult(dead, ValueProp.Move) { UnblockedDamage = 3, WasTargetKilled = true });
        actor.Free();
        targetBody.Free();
        layer.Call("advance", 0.25);
        Require(ordinary.Get("hits").AsGodotArray().Count == 1, "Lethal particles survive freed creature nodes");
        var generic = impact.GetMethod("Generic", Static)!;
        Require((bool)generic.Invoke(null, ["vfx/vfx_attack_slash"])!, "Generic slash replacement");
        Require(!(bool)generic.Invoke(null, ["vfx/vfx_starry_impact"])!, "Card-specific VFX preserved");
        container.Free();
        Require(!GodotObject.IsInstanceValid(layer), "Room owns cleanup");
        GD.Print("ENEMY_IMPACT_NATIVE_PASS: DLL/PCK; 44 stages; real result routing, 2x world scale, per-result dedupe, normal/lethal/revived/block/zero, flash restore and ownership, fatal-node lifetime, room cleanup");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("King enemy impact: " + message);
    }
}
