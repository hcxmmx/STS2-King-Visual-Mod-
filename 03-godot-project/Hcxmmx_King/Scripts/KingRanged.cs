using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;

namespace Hcxmmx.KingMod.Scripts;

internal static class KingRanged
{
    internal sealed class Aim(Control container, Vector2 point, int index)
    {
        internal Control Container = container;
        internal Vector2 Point = point;
        internal readonly int Index = index;
    }

    internal sealed class Shot(long id)
    {
        internal readonly long Id = id;
        internal readonly Dictionary<Creature, Aim> Targets = new();
        internal bool Released;
        internal bool Cancelled;
        internal int Mode;
        internal Node2D? Actor;
    }

    private sealed class State
    {
        internal long NextId;
        internal readonly Dictionary<long, Shot> Waiting = new();
        internal readonly HashSet<ulong> Layers = new();
    }

    private static readonly ConditionalWeakTable<Node2D, State> Actors = new();
    private static bool warned;
    private static readonly Func<DamageResult, Creature>? OriginalTarget =
        typeof(DamageResult).GetProperty("OriginalTarget")?.GetMethod?.CreateDelegate<Func<DamageResult, Creature>>();

    internal static Shot Prepare(Node2D actor, long pending)
    {
        var state = Actors.GetOrCreateValue(actor);
        if (!state.Waiting.TryGetValue(pending, out var shot))
        {
            shot = new Shot(++state.NextId) { Actor = actor, Mode = KingGlobals.UseOriginalTiming ? 1 : 0 };
            state.Waiting.Add(shot.Id, shot);
        }
        return shot;
    }

    internal static void Release(Node2D actor, long id)
    {
        if (!Actors.TryGetValue(actor, out var state) || !state.Waiting.Remove(id, out var shot)) return;
        shot.Released = true;
        foreach (var aim in shot.Targets.Values) Fire(shot, aim);
    }

    private static Node2D GetLayer(Control container)
    {
        var layer = container.GetNodeOrNull<Node2D>("KingArrowLayer");
        if (layer != null) return layer;
        var script = ResourceLoader.Load<GDScript>("res://Hcxmmx_King/Presentation/KingArrows.gd");
        layer = (Node2D)script.New().AsGodotObject();
        layer.Name = "KingArrowLayer";
        container.AddChild(layer);
        return layer;
    }

    internal static void Fire(Shot shot, Aim aim)
    {
        var actor = shot.Actor;
        if (!GodotObject.IsInstanceValid(actor) || !actor!.IsVisibleInTree() ||
            !GodotObject.IsInstanceValid(aim.Container) || !aim.Container.IsInsideTree()) return;
        try
        {
            var layer = GetLayer(aim.Container);
            Actors.GetOrCreateValue(actor).Layers.Add(layer.GetInstanceId());
            var origin = layer.ToLocal(actor.ToGlobal(new Vector2(20, -45)));
            float scale = (layer.ToLocal(actor.ToGlobal(Vector2.Right)) - layer.ToLocal(actor.GlobalPosition)).Length();
            layer.Call("shoot", actor.GetInstanceId(), shot.Id, aim.Index, origin, layer.ToLocal(aim.Point), scale, shot.Mode);
        }
        catch (Exception error)
        {
            if (!warned) { GD.PushWarning("[King] Arrow presentation unavailable: " + error.Message); warned = true; }
        }
    }

    internal static void Resolve(Shot? shot, DamageResult result)
    {
        if (shot == null || shot.Cancelled) return;
        var target = result.Receiver.GetCreatureNode();
        var container = result.Receiver.GetVfxContainer();
        if (target == null || container == null) return;
        var original = OriginalTarget?.Invoke(result) ?? result.Receiver;
        if (!shot.Targets.TryGetValue(original, out var aim))
        {
            if (shot.Targets.Count >= 32) return;
            aim = new Aim(container, target.VfxSpawnPosition, shot.Targets.Count);
            shot.Targets.Add(original, aim);
            if (shot.Released) Fire(shot, aim);
            return;
        }
        // An already flying arrow follows the resolved receiver, including damage redirection.
        if (shot.Released && GodotObject.IsInstanceValid(aim.Container))
        {
            var layer = aim.Container.GetNodeOrNull<Node2D>("KingArrowLayer");
            if (layer != null && GodotObject.IsInstanceValid(shot.Actor))
                layer.Call("retarget", shot.Actor!.GetInstanceId(), shot.Id, aim.Index, layer.ToLocal(target.VfxSpawnPosition));
        }
        aim.Point = target.VfxSpawnPosition;
        aim.Container = container;
    }

    internal static void Cancel(Node2D actor, bool clearFlights = false)
    {
        if (!Actors.TryGetValue(actor, out var state)) return;
        foreach (var shot in state.Waiting.Values) shot.Cancelled = true;
        state.Waiting.Clear();
        var presentation = KingPresentation.For(actor);
        if (!clearFlights && actor.IsVisibleInTree() && !presentation.IsDead && !presentation.CombatEnded) return;
        foreach (var id in state.Layers)
            if (GodotObject.InstanceFromId(id) is Node2D layer) layer.Call("clear_owner", actor.GetInstanceId());
        state.Layers.Clear();
    }
}
