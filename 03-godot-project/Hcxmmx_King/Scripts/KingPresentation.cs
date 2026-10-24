using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Combat;

namespace Hcxmmx.KingMod.Scripts;

// One curated weapon definition is embedded in the DLL and used to generate the PCK/preview data.
internal static class KingWeaponCatalog
{
    internal sealed record Cue(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("volume_db")] float VolumeDb = -6f,
        [property: JsonPropertyName("offset_seconds")] float OffsetSeconds = 0f);
    internal sealed record Sound(
        [property: JsonPropertyName("release")] Cue Release,
        [property: JsonPropertyName("hit")] Cue Hit);
    internal sealed record Stage(
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("vfx")] string? Vfx,
        [property: JsonPropertyName("audio")] Sound? Audio = null);
    internal sealed record Weapon(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("weight")] int Weight,
        [property: JsonPropertyName("stages")] Stage[] Stages);
    private sealed record Manifest([property: JsonPropertyName("weapons")] Weapon[] Weapons);

    internal static readonly Weapon[] Weapons = Load();
    private static Weapon[] Load()
    {
        using var stream = typeof(KingWeaponCatalog).Assembly.GetManifestResourceStream("KingMod.Presentation.weapons.json")
            ?? throw new InvalidOperationException("Missing King weapon definitions");
        var weapons = JsonSerializer.Deserialize<Manifest>(stream)?.Weapons
            ?? throw new InvalidOperationException("Invalid King weapon definitions");
        if (weapons.Length == 0 || weapons.Any(w => w.Weight <= 0 || w.Stages.Length == 0))
            throw new InvalidOperationException("Empty weapon/invalid weight in King definitions");
        return weapons;
    }

    internal static Weapon Pick(Random random)
    {
        int choice = random.Next(Weapons.Sum(w => w.Weight));
        foreach (var weapon in Weapons)
        {
            choice -= weapon.Weight;
            if (choice < 0) return weapon;
        }
        throw new InvalidOperationException("Invalid King weapon weights");
    }
}

internal sealed class KingActorPresentation
{
    private KingWeaponCatalog.Weapon? _weapon;
    internal int ComboIndex { get; private set; }
    internal bool IsDead { get; set; }
    internal KingWeaponCatalog.Stage PeekAttack(Random random)
    {
        if (_weapon == null || ComboIndex >= _weapon.Stages.Length)
        {
            _weapon = KingWeaponCatalog.Pick(random);
            ComboIndex = 0;
        }
        return _weapon.Stages[ComboIndex];
    }
    internal KingWeaponCatalog.Stage NextAttack(Random random)
    {
        var stage = PeekAttack(random);
        ComboIndex++;
        return stage;
    }
    internal void ResetCombo() { _weapon = null; ComboIndex = 0; }
}

internal static class KingPresentation
{
    internal const string RigName = "KingScarf";
    private const string ScriptPath = "res://Hcxmmx_King/Presentation/KingScarf.gd";
    private static readonly ConditionalWeakTable<Node2D, KingActorPresentation> Actors = new();
    private static readonly ConditionalWeakTable<ICombatState, WindChoice> Winds = new();
    private sealed record WindChoice(float Value);

    internal static KingActorPresentation For(Node2D actor) => Actors.GetOrCreateValue(actor);

    // Stable visual randomness: no gameplay RNG draws, no room-ID increment, no savegame changes.
    // All clients/reloads at the same seed/map/encounter get the same independent wind choice.
    internal static float ChooseWind(string battleKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("king-scarf-v1|" + battleKey));
        return new[] { 1f, -1f, -2f }[hash[0] % 3];
    }

    internal static float WindFor(ICombatState? combat)
    {
        if (combat == null) return 0f; // Non-combat hosts do not select battle wind.
        return Winds.GetValue(combat, state =>
        {
            var run = state.RunState;
            string key = FormattableString.Invariant($"{run.Rng.Seed}|{run.CurrentActIndex}|{run.CurrentMapCoord?.col}|{run.CurrentMapCoord?.row}|{run.CurrentRoomCount}|{state.Encounter?.Id}");
            return new WindChoice(ChooseWind(key));
        }).Value;
    }

    internal static void Attach(Node2D actor, AnimatedSprite2D body, ICombatState? combat)
    {
        if (actor.HasNode(RigName)) return;
        // Missing new resources should leave the previously working skin intact.
        try
        {
            var script = ResourceLoader.Load<GDScript>(ScriptPath)
                ?? throw new InvalidOperationException("Missing King scarf resource pack");
            var rig = script.New().AsGodotObject() as Node2D
                ?? throw new InvalidOperationException("Unable to instantiate King scarf");
            rig.Name = RigName;
            actor.AddChild(rig);
            actor.MoveChild(rig, 0);
            rig.Call("bind_body", body, KingGlobals.IsWhiteKingSkin, WindFor(combat));
            actor.TreeExiting += () => { Actors.Remove(actor); KingGlobals.ActiveKingSprites.Remove(body); };
        }
        catch (Exception exception)
        {
            GD.PushWarning("[King] Scarf setup failed: " + exception.Message);
        }
    }

    internal static void WeaponUse(Node2D actor) => actor.GetNodeOrNull<Node2D>(RigName)?.Call("weapon_use");
    internal static void RefreshSkin(Node2D actor) => actor.GetNodeOrNull<Node2D>(RigName)?.Call("set_skin", KingGlobals.IsWhiteKingSkin);
}
