using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Godot;
using HarmonyLib;

// Run under the real Godot mono engine; no mod initializer or game/save startup.
public partial class BridgeCheck : Node
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public override void _Ready()
    {
        try { Validate(); GetTree().Quit(); }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Validate()
    {
        var args = OS.GetCmdlineUserArgs();
        Require(args.Length is 2 or 3, "Expected package-directory, game-data-directory and optional --patch-check");
        var context = AssemblyLoadContext.GetLoadContext(typeof(BridgeCheck).Assembly)!;
        context.Resolving += (_, name) =>
        {
            if (name.Name == typeof(GodotObject).Assembly.GetName().Name) return typeof(GodotObject).Assembly;
            var path = Path.Combine(args[1], name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        Require(ProjectSettings.LoadResourcePack(Path.Combine(args[0], "Hcxmmx_King_Skin.pck")), "Pack mount");
        var mod = context.LoadFromAssemblyPath(Path.Combine(args[0], "Hcxmmx_King_Skin.dll"));
        if (args.Length == 3)
        {
            if (args[2] == "--mod-combo-check")
            {
                ModCombinationCheck.Validate(mod, context);
                return;
            }
            Require(args[2] == "--patch-check", "Unknown validation option");
            ValidatePatches(mod);
            AudioCheck.Validate(mod, this, ResourceLoader.Load<PackedScene>("res://Hcxmmx_King/tscn/white_king.tscn"));
            if (args[0].Contains("shield-integration", StringComparison.OrdinalIgnoreCase))
                DefenseCheck.Validate(mod, this, ResourceLoader.Load<PackedScene>("res://Hcxmmx_King/tscn/white_king.tscn"));
            if (args[0].Contains("enemy-impact", StringComparison.OrdinalIgnoreCase))
                EnemyImpactCheck.Validate(mod, this);
            if (args[0].Contains("selection-settings", StringComparison.OrdinalIgnoreCase))
                SelectionSettingsCheck.Validate(mod, this);
            if (args[0].Contains("ranged-game", StringComparison.OrdinalIgnoreCase) ||
                args[0].Contains("bow-only-test", StringComparison.OrdinalIgnoreCase))
                RangedCheck.Validate(mod, this, ResourceLoader.Load<PackedScene>("res://Hcxmmx_King/tscn/white_king.tscn"));
            if (args[0].Contains("bow-only-test", StringComparison.OrdinalIgnoreCase))
            {
                var catalog = mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponCatalog", true)!;
                for (int index = 0; index < 1000; index++)
                {
                    var weapon = catalog.GetMethod("Pick", Static)!.Invoke(null, [new Random(index)])!;
                    Require((string)weapon.GetType().GetProperty("Id")!.GetValue(weapon)! == "fast_bow", "Bow-only test selection");
                }
                GD.Print("BOW_ONLY_PASS: 1000 selections return FastBow");
            }
            else
            {
                var catalog = mod.GetType("Hcxmmx.KingMod.Scripts.KingWeaponCatalog", true)!;
                var available = (Array)catalog.GetField("Weapons", Static)!.GetValue(null)!;
                var selected = new HashSet<string>();
                var random = new Random(113);
                for (int index = 0; index < 1000; index++)
                {
                    var weapon = catalog.GetMethod("Pick", Static)!.Invoke(null, [random])!;
                    selected.Add((string)weapon.GetType().GetProperty("Id")!.GetValue(weapon)!);
                }
                Require(selected.Count == available.Length, "Normal weapon pool restored");
                GD.Print($"RANDOM_POOL_PASS: all {available.Length} weapons sampled in 1000 selections");
            }
            return;
        }
        using var manifest = mod.GetManifestResourceStream("KingMod.Presentation.weapons.json")!;
        using var bytes = new MemoryStream();
        manifest.CopyTo(bytes);
        Require(bytes.ToArray().SequenceEqual(Godot.FileAccess.GetFileAsBytes("res://Hcxmmx_King/Presentation/weapons.json")),
            "DLL/PCK weapon manifest identity");
        var bridge = mod.GetType("Hcxmmx.KingMod.Scripts.KingPresentation", true)!;
        var globals = mod.GetType("Hcxmmx.KingMod.Scripts.KingGlobals", true)!;
        var scene = ResourceLoader.Load<PackedScene>("res://Hcxmmx_King/tscn/white_king.tscn");
        var attach = bridge.GetMethod("Attach", Static)!;
        var actor = scene.Instantiate<Node2D>();
        AddChild(actor);
        var body = actor.GetNode<AnimatedSprite2D>("Body");
        attach.Invoke(null, [actor, body, null]);
        var rig = actor.GetNode<Node2D>("KingScarf");
        Require(rig.Get("chains").AsGodotArray().Count == 5, "Actual C# to GDScript attach");
        attach.Invoke(null, [actor, body, null]);
        Require(actor.GetChildren().Count(n => n.Name == "KingScarf") == 1, "Duplicate attach");
        bridge.GetMethod("WeaponUse", Static)!.Invoke(null, [actor]);
        Require(Math.Abs(rig.Get("impulse_seconds").AsDouble() - 0.4) < 0.00001, "Weapon impulse");
        globals.GetField("IsWhiteKingSkin", Static)!.SetValue(null, true);
        globals.GetMethod("ApplyCurrentSkin", Static)!.Invoke(null, [actor]);
        Require(rig.Get("white_skin").AsBool(), "Skin refresh bridge");
        ValidateActorState(bridge, scene);
        ValidateWind(bridge);
        if (args[0].Contains("audio-integration", StringComparison.OrdinalIgnoreCase) ||
            args[0].Contains("weapon-expansion", StringComparison.OrdinalIgnoreCase) ||
            args[0].Contains("game-integration", StringComparison.OrdinalIgnoreCase))
            AudioCheck.Validate(mod, this, scene);
        actor.Free();
        Require(!GodotObject.IsInstanceValid(rig), "Child cleanup");
        if (args[0].Contains("game-integration", StringComparison.OrdinalIgnoreCase) ||
            args[0].Contains("selection-settings", StringComparison.OrdinalIgnoreCase))
            RuntimeTimelineCheck.Validate(mod, this, scene);
        GD.Print("CSHARP_BRIDGE_PASS: real compiled DLL -> native scarf; duplicate guard, attack push, palette switch, per-actor combos/death, wind lifetime, RNG isolation, cleanup");
    }

    private static void ValidatePatches(Assembly mod)
    {
        var harmony = new Harmony("king_beta113_patch_validation");
        try
        {
            harmony.PatchAll(mod);
            int methods = 0;
            foreach (var type in mod.GetTypes())
            foreach (var method in type.GetMethods(Static | Instance | BindingFlags.DeclaredOnly))
            {
                if (method.ContainsGenericParameters || method.IsAbstract || method.GetMethodBody() == null) continue;
                RuntimeHelpers.PrepareMethod(method.MethodHandle);
                methods++;
            }
            int patchClasses = mod.GetTypes().Count(type => type.IsDefined(typeof(HarmonyPatch), false));
            GD.Print($"PATCH_INSTALL_PASS: {patchClasses} classes; {harmony.GetPatchedMethods().Count()} originals; {methods} methods JIT compiled");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private void ValidateActorState(Type bridge, PackedScene scene)
    {
        var other = scene.Instantiate<Node2D>();
        AddChild(other);
        var first = bridge.GetMethod("For", Static)!.Invoke(null, [other])!;
        var type = first.GetType();
        var second = Activator.CreateInstance(type, nonPublic: true)!;
        var random = new Random(7);
        var next = type.GetMethod("NextAttack", Instance)!;
        next.Invoke(first, [random]);
        next.Invoke(second, [random]);
        Require((int)type.GetProperty("ComboIndex", Instance)!.GetValue(first)! == 1, "First combo");
        type.GetMethod("ResetCombo", Instance)!.Invoke(second, null);
        type.GetProperty("IsDead", Instance)!.SetValue(second, true);
        Require((int)type.GetProperty("ComboIndex", Instance)!.GetValue(first)! == 1 &&
            !(bool)type.GetProperty("IsDead", Instance)!.GetValue(first)!, "Actor state isolation");
        for (int i = 0; i < 100; i++) next.Invoke(first, [random]);
        other.Free();
    }

    private static void ValidateWind(Type bridge)
    {
        var game = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "sts2");
        var runType = game.GetType("MegaCrit.Sts2.Core.Runs.IRunState", true)!;
        var combatType = game.GetType("MegaCrit.Sts2.Core.Combat.ICombatState", true)!;
        var rngType = runType.GetProperty("Rng")!.PropertyType;
        var rng = Activator.CreateInstance(rngType, ["KING-VISUAL-TEST"])!;
        var run = DispatchProxy.Create(runType, typeof(PropertyProxy));
        var runValues = (PropertyProxy)run;
        runValues.Values["Rng"] = rng;
        runValues.Values["CurrentActIndex"] = 0;
        runValues.Values["CurrentRoomCount"] = 1;
        var windFor = bridge.GetMethod("WindFor", Static)!;
        object NewCombat()
        {
            var proxy = DispatchProxy.Create(combatType, typeof(PropertyProxy));
            ((PropertyProxy)proxy).Values["RunState"] = run;
            return proxy;
        }
        var first = NewCombat();
        var wind = windFor.Invoke(null, [first]);
        Require(Equals(wind, windFor.Invoke(null, [first])), "Same battle/rebuilt node");
        Require(Equals(wind, windFor.Invoke(null, [NewCombat()])), "Reloaded equivalent battle");
        runValues.Values["CurrentActIndex"] = 1;
        Require(Equals(wind, windFor.Invoke(null, [first])), "Cached battle ignores later map state changes");
        var choices = new HashSet<float>();
        for (int i = 1; i < 40; i++)
        {
            runValues.Values["CurrentActIndex"] = i;
            choices.Add((float)windFor.Invoke(null, [NewCombat()])!);
        }
        Require(choices.SetEquals([1f, -1f, -2f]), "All three battle presets");
        Require((float)windFor.Invoke(null, [null])! == 0f, "Non-combat wind");
        Require(runValues.Reads.Keys.All(k => k is "Rng" or "CurrentActIndex" or "CurrentMapCoord" or "CurrentRoomCount"), "Read-only visual key");
        // Seed is the only RNG property exposed to production code; no streams/counters are drawn.
        Require(runValues.Reads["Rng"] == 41, "One seed read per new battle, none on cached accesses");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Validation failed: " + message);
    }
}

public class PropertyProxy : DispatchProxy
{
    public Dictionary<string, object?> Values { get; } = new();
    public Dictionary<string, int> Reads { get; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name.StartsWith("get_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Unexpected game state mutation: " + method?.Name);
        var name = method.Name[4..];
        Reads[name] = Reads.GetValueOrDefault(name) + 1;
        return Values.GetValueOrDefault(name);
    }
}
