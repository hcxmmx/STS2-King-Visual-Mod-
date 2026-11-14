using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Godot;

internal static class RuntimeTimelineCheck
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Runtime timeline: " + message);
    }

    internal static void Validate(Assembly mod, Node host, PackedScene scene)
    {
        var bridge = mod.GetType("Hcxmmx.KingMod.Scripts.KingPresentation", true)!;
        var globals = mod.GetType("Hcxmmx.KingMod.Scripts.KingGlobals", true)!;
        var audio = mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponAudio", true)!;
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://Hcxmmx_King/Presentation/combat_presentation.json"));
        var weapons = json.RootElement.GetProperty("weapons").EnumerateArray().ToArray();
        Require(weapons.Length == 13 && weapons.Sum(w => w.GetProperty("stages").GetArrayLength()) == 44, "Thirteen weapons / 44 stages");
        var actor = scene.Instantiate<Node2D>();
        host.AddChild(actor);
        bridge.GetMethod("Attach", All)!.Invoke(null, [actor, actor.GetNode<AnimatedSprite2D>("Body"), null]);
        var timeline = actor.GetNode("KingCombatTimeline");
        var body = actor.GetNode<AnimatedSprite2D>("Body");
        var fx = actor.GetNode<AnimatedSprite2D>("VFX_Slash");
        var rig = actor.GetNode<Node2D>("KingScarf");
        var anchorData = rig.Get("data").AsGodotDictionary()["anchors"].AsGodotDictionary();
        timeline.SetProcess(false);
        int Count(string name) => timeline.Get(name).AsInt32();
        void Advance(double seconds) => timeline.Call("advance", seconds);
        void Clear()
        {
            timeline.Call("interrupt");
            foreach (var key in new[] { "requested", "started", "contacts", "merged" }) timeline.Set(key, 0);
        }
        AudioServer.SetBusMute(0, true);
        foreach (int mode in new[] { 0, 1 })
        {
            timeline.Call("set_mode", mode);
            foreach (var weapon in weapons)
            {
                var stages = weapon.GetProperty("stages").EnumerateArray().ToArray();
                Clear();
                int issued = 0;
                for (int tick = 0; tick < 1600; tick++)
                {
                    if (issued < 50 && tick % 21 == 0)
                    {
                        timeline.Call("request_attack", stages[issued % stages.Length].GetProperty("body").GetString()!);
                        issued++;
                    }
                    Advance(1.0 / 120.0);
                }
                Require(Count("requested") == 50 && Count("started") == Count("contacts") && Count("contacts") + Count("merged") == 50, "No lost visual contact: " + weapon.GetProperty("id"));
                Require(timeline.Get("active").AsGodotDictionary().Count == 0 && timeline.Get("pending").AsGodotDictionary().Count == 0 && body.Animation == "Idle", "Drain back to idle");
                GD.Print($"GAME_TIMELINE_PASS mode={mode} weapon={weapon.GetProperty("id").GetString()} contacts={Count("contacts")} merged={Count("merged")}");
                // Every production animation/FX is loaded from the shipped PCK, including extra PNGs.
                foreach (var stage in stages)
                {
                    var name = stage.GetProperty("body").GetString()!;
                    Require(body.SpriteFrames.HasAnimation(name), "Body exists: " + name);
                    for (int frame = 0; frame < body.SpriteFrames.GetFrameCount(name); frame++)
                        Require(anchorData.ContainsKey(body.SpriteFrames.GetFrameTexture(name, frame).ResourcePath), "Shipped scarf anchor exists for every frame: " + name);
                    if (stage.GetProperty("vfx").ValueKind != JsonValueKind.Null)
                        Require(fx.SpriteFrames.HasAnimation(stage.GetProperty("vfx").GetString()!), "FX exists");
                    Clear();
                    timeline.Call("request_attack", name);
                    for (int tick = 0; tick < 300; tick++) Advance(1.0 / 120.0);
                    Require(Count("contacts") == 1 && body.Animation == "Idle", "Complete stage: " + name);
                }
            }
        }
        foreach (int mode in new[] { 0, 1 })
        {
            timeline.Call("set_mode", mode);
            foreach (var (name, hit, lead) in new[] { ("scissorsAtkA", 15, 3), ("scissorsAtkB", 13, 1) })
            {
                Clear();
                timeline.Call("request_attack", name);
                bool sawHit = false;
                for (int tick = 0; tick < 150; tick++)
                {
                    Advance(1.0 / 120.0);
                    if (timeline.Get("active").AsGodotDictionary().Count == 0) break;
                    if (fx.Visible) Require(fx.Frame + lead == body.Frame, "Production scissors preserve source phase");
                    if (body.Frame == hit) sawHit = true;
                }
                Require(sawHit, "Scissors contact rendered");
            }
        }
        // The actual C# request router reserves one pending stage, and binds the
        // coalesced command to that same stage instead of advancing the combo again.
        Clear();
        timeline.Call("set_mode", 1);
        var state = bridge.GetMethod("For", All)!.Invoke(null, [actor])!;
        state.GetType().GetMethod("ResetCombo", All)!.Invoke(state, null);
        var request = bridge.GetMethod("RequestAttack", All)!;
        request.Invoke(null, [actor]);
        var second = request.Invoke(null, [actor])!;
        var merged = request.Invoke(null, [actor])!;
        Require(ReferenceEquals(second, merged) && Count("requested") == 3 && Count("merged") == 1, "C# coalescing keeps the reserved stage");
        var pendingName = timeline.Call("pending_body").AsString();
        var peek = bridge.GetMethod("PeekRequest", All)!.Invoke(null, [actor])!;
        Require((string)peek.GetType().GetProperty("Body")!.GetValue(peek)! == pendingName, "Audio peeks the actual pending stage");
        timeline.Call("set_mode", 0);
        Require(timeline.Get("active").AsGodotDictionary().Count == 0 && timeline.Get("pending").AsGodotDictionary().Count == 0, "Mode switch clears queue");
        timeline.Call("request_attack", "atkPanA");
        actor.Hide();
        timeline.Call("_process", 0.1);
        Require(timeline.Get("active").AsGodotDictionary().Count == 0 && body.Animation == "Idle", "SkinChanger hide cancels to idle");
        actor.Show();
        timeline.Call("request_attack", "scissorsAtkA");
        bridge.GetMethod("Interrupt", All)!.Invoke(null, [actor, false]);
        body.Play("stun");
        Advance(1.0);
        Require(body.Animation == "stun" && !fx.Visible, "Hit pose cannot be overwritten by queued attacks");
        body.Play("letha");
        state.GetType().GetProperty("IsDead", All)!.SetValue(state, true);
        bridge.GetMethod("Interrupt", All)!.Invoke(null, [actor, false]);
        Advance(1.0);
        Require(!actor.GetNode<Node2D>("KingScarf").Visible && body.Animation == "letha", "Death hides scarf and preserves final pose");
        var catalog = mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponCatalog", true)!;
        var probeStage = catalog.GetMethod("StageFor", All)!.Invoke(null, ["atkPanA"]);
        audio.GetMethod("PlayTimelineCue", All)!.Invoke(null, [actor, probeStage, "release"]);
        Require(!actor.GetChildren().OfType<AudioStreamPlayer>().Any(p => !p.IsQueuedForDeletion()), "No new sound after death");
        state.GetType().GetProperty("IsDead", All)!.SetValue(state, false);
        body.Play("Idle");
        bridge.GetMethod("Interrupt", All)!.Invoke(null, [actor, true]);
        audio.GetMethod("PlayTimelineCue", All)!.Invoke(null, [actor, probeStage, "release"]);
        Require(!actor.GetChildren().OfType<AudioStreamPlayer>().Any(p => !p.IsQueuedForDeletion()), "No sound after victory");
        actor.Free();
        SelectionSettingsCheck.Validate(mod, host);
        GD.Print("GAME_RUNTIME_PASS: production PCK, both modes/all stages, real C# coalescing, hide/hit/death/victory cleanup, persisted settings and selection panel");
    }

}
