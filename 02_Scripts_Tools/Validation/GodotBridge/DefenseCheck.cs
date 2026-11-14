using System;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

internal static class DefenseCheck
{
    private const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Validate(Assembly mod, Node host, PackedScene scene)
    {
        var defense = mod.GetType("Hcxmmx.KingMod.Scripts.KingDefense", true)!;
        var presentation = mod.GetType("Hcxmmx.KingMod.Scripts.KingPresentation", true)!;
        var shouldBlock = defense.GetMethod("ShouldBlock", All)!;
        var target = new Creature(null!, 100, 100);
        var dealer = new Creature(null!, 100, 100);
        var result = new DamageResult(target, ValueProp.Move) { WasFullyBlocked = true, BlockedDamage = 8 };
        bool Classify(Creature? source, ValueProp props) => (bool)shouldBlock.Invoke(null, [result, source, props])!;
        Require(Classify(dealer, ValueProp.Move), "Full block classified");
        result = new DamageResult(target, ValueProp.Move) { WasFullyBlocked = false, BlockedDamage = 8, UnblockedDamage = 2 };
        Require(!Classify(dealer, ValueProp.Move), "Partial block with HP loss uses hurt");
        result = new DamageResult(target, ValueProp.Move) { WasFullyBlocked = true, BlockedDamage = 0 };
        Require(!Classify(dealer, ValueProp.Move), "Zero damage does not raise shield");
        result.BlockedDamage = 8;
        Require(!Classify(target, ValueProp.Move) && !Classify(null, ValueProp.Move), "Self/environment excluded");
        Require(!Classify(dealer, ValueProp.Unpowered) && !Classify(dealer, ValueProp.Move | ValueProp.Unblockable) &&
            !Classify(dealer, ValueProp.Move | ValueProp.SkipHurtAnim), "Status and skipped hurt excluded");

        var actor = scene.Instantiate<Node2D>();
        host.AddChild(actor);
        var body = actor.GetNode<AnimatedSprite2D>("Body");
        presentation.GetMethod("Attach", All)!.Invoke(null, [actor, body, null]);
        var shield = actor.GetNode("KingShield");
        var timeline = actor.GetNode("KingCombatTimeline");
        shield.SetProcess(false);
        timeline.SetProcess(false);
        var data = shield.Get("data").AsGodotDictionary();
        var variants = data["variants"].AsGodotArray();
        defense.GetMethod("PlayHurt", All)!.Invoke(null, [actor]);
        Require(actor.GetChildren().OfType<AudioStreamPlayer>().Any(player => player.Stream.GetLength() > 0), "Original hero damage sound decodes and plays");
        Require(variants.Count == 3, "Three shield variants");
        foreach (int mode in new[] { 0, 1 })
        for (int index = 0; index < 3; index++)
        {
            shield.Call("cancel", true);
            string sound = shield.Call("block", index, mode).AsString();
            Require(shield.Get("active").AsBool(), "Block starts immediately");
            var variant = variants[index].AsGodotDictionary();
            Require(body.Animation == variant["hold"].AsString() && body.Frame == 0, "Source hold frame");
            Require(shield.Get("particles").AsGodotArray().Count == 43, "Original charge and impact arc counts");
            Require(body.SpriteFrames.GetFrameCount(variant["hold"].AsString()) == 1 &&
                body.SpriteFrames.GetFrameCount(variant["end"].AsString()) == 9, "Original one-plus-nine shield frames");
            var audio = mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponAudio", true)!;
            var cue = Activator.CreateInstance(mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponCatalog+Cue", true)!, All, null,
                [sound, 0f, 0f], null)!;
            Require(audio.GetMethod("LoadCue", All)!.Invoke(null, [cue]) != null, "Original shield Ogg decodes");
            double hold = mode == 1 ? 0.2 : 0.12;
            shield.Call("advance", hold - 0.001);
            Require(body.Animation == variant["hold"].AsString(), "Finite hold duration");
            shield.Call("advance", 0.002);
            Require(body.Animation == variant["end"].AsString() && body.Frame == 0, "Release begins at source frame zero");
            shield.Call("advance", 0.4);
            Require(!shield.Get("active").AsBool() && body.Animation == "Idle", "Release returns to idle");
        }
        defense.GetMethod("PlayBlock", All)!.Invoke(null, [actor]);
        Require(shield.Get("active").AsBool(), "Actual C# defense entry starts shield");
        presentation.GetMethod("Interrupt", All)!.Invoke(null, [actor, false]);
        body.Play("stun");
        shield.Call("advance", 1.0);
        Require(body.Animation == "stun" && !shield.Get("active").AsBool(), "Hurt interrupts shield without a late idle");
        defense.GetMethod("PlayBlock", All)!.Invoke(null, [actor]);
        presentation.GetMethod("RequestAttack", All)!.Invoke(null, [actor]);
        Require(!shield.Get("active").AsBool(), "Attack interrupts shield immediately");
        presentation.GetMethod("Interrupt", All)!.Invoke(null, [actor, false]);
        defense.GetMethod("PlayBlock", All)!.Invoke(null, [actor]);
        actor.Hide();
        shield.Call("advance", 0.01);
        Require(!shield.Get("active").AsBool() && shield.Get("particles").AsGodotArray().Count == 0, "Hidden skin cleans shield");
        actor.Free();
        GD.Print("DEFENSE_PASS: full/partial/zero/self/status routing; three shields in both rhythms; original frames/particles/Ogg; hurt/attack/hidden interruption");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("King defense: " + message);
    }
}
