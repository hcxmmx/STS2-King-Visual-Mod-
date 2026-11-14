using System;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace Hcxmmx.KingMod.Scripts;

internal static class KingEnemyImpact
{
    private const string LayerName = "KingEnemyImpactLayer";
    private static readonly ConditionalWeakTable<DamageResult, object> Handled = new();
    private static bool warned;

    internal static bool Emit(Node2D actor, string body, DamageResult result)
    {
        if (Handled.TryGetValue(result, out _)) return true;
        var target = result.Receiver;
        if (NonInteractiveMode.IsActive || !target.IsMonster || !HasImpact(result)) return false;
        var creature = target.GetCreatureNode();
        var container = target.GetVfxContainer();
        if (creature == null || container == null || !creature.IsInsideTree() || !creature.IsVisibleInTree()) return false;
        try
        {
            var layer = GetLayer(container);
            if (layer == null) return false;
            var hitbox = creature.Hitbox;
            var floor = hitbox.GetGlobalTransform() * new Vector2(hitbox.Size.X * 0.5f, hitbox.Size.Y);
            return Present(layer, actor, creature.Body, creature.VfxSpawnPosition, floor, body, result);
        }
        catch (Exception error)
        {
            if (!warned) { GD.PushWarning("[King] Enemy impact unavailable: " + error.Message); warned = true; }
            return false;
        }
    }

    private static bool HasImpact(DamageResult result) => result.UnblockedDamage > 0 ||
        (result.WasFullyBlocked && result.BlockedDamage > 0);

    internal static bool Present(Node2D layer, Node2D actor, CanvasItem targetBody, Vector2 worldCenter,
        Vector2 worldFloor, string body, DamageResult result)
    {
        if (Handled.TryGetValue(result, out _)) return true;
        if (!HasImpact(result)) return false;
        var center = layer.ToLocal(worldCenter);
        var origin = layer.ToLocal(actor.ToGlobal(new Vector2(0, -32)));
        // Keep the accepted source-pixel scale relative to the King, including
        // transforms on the room's VFX container. The shared script applies 2x.
        float scale = (layer.ToLocal(actor.ToGlobal(Vector2.Right)) - layer.ToLocal(actor.GlobalPosition)).Length();
        bool killed = result.WasTargetKilled && result.Receiver.IsDead && result.UnblockedDamage > 0;
        bool played = layer.Call("spawn_hit", center, (center - origin).Normalized(), scale, body,
            result.WasFullyBlocked, killed, layer.ToLocal(worldFloor).Y).AsBool();
        if (!played) return false;
        Handled.Add(result, new object());
        if (!result.WasFullyBlocked) layer.Call("flash_target", targetBody);
        return true;
    }

    internal static NHitSparkVfx? CreateNativeSpark(Creature target, bool requireInteractable, DamageResult result) =>
        Handled.TryGetValue(result, out _) ? null : NHitSparkVfx.Create(target, requireInteractable);

    private static Node2D? GetLayer(Control container)
    {
        var layer = container.GetNodeOrNull<Node2D>(LayerName);
        if (layer != null) return layer;
        var script = ResourceLoader.Load<GDScript>("res://Hcxmmx_King/Presentation/ImpactPresentation.gd");
        layer = script?.New().AsGodotObject() as Node2D;
        if (layer == null) return null;
        layer.Name = LayerName;
        container.AddChild(layer);
        return layer;
    }

    internal static bool CanReplaceSide(ICombatState combat, CombatSide side, string body)
    {
        bool any = false;
        try
        {
            foreach (var target in combat.GetCreaturesOnSide(side))
            {
                if (!target.IsHittable) continue;
                var container = target.GetVfxContainer();
                if (!target.IsMonster || container == null) return false;
                var layer = GetLayer(container);
                if (layer == null || !layer.Call("can_spawn", body).AsBool()) return false;
                any = true;
            }
        }
        catch (Exception error)
        {
            if (!warned) { GD.PushWarning("[King] Enemy impact unavailable: " + error.Message); warned = true; }
            return false;
        }
        return any;
    }

    internal static bool Generic(string path) => path is VfxCmd.slashPath or VfxCmd.bluntPath or VfxCmd.heavyBluntPath;

    internal static void PlayCardCenter(Creature target, string path, bool forceIfDead, DamageResult result, object? targets)
    {
        if (Generic(path) && KingWeaponAudio.EnsureImpact(result, targets)) return;
        VfxCmd.PlayOnCreatureCenter(target, path, forceIfDead);
    }

    internal static void PlayCardBase(Creature target, string path, bool forceIfDead, DamageResult result, object? targets)
    {
        if (Generic(path) && KingWeaponAudio.EnsureImpact(result, targets)) return;
        VfxCmd.PlayOnCreature(target, path, forceIfDead);
    }
}
