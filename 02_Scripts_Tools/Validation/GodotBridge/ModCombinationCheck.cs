using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;

internal static class ModCombinationCheck
{
    internal static void Validate(Assembly king, AssemblyLoadContext context)
    {
        const string knightRoot = @"D:\youxi\steamapps\workshop\content\2868840\3825224241";
        const string sothebyRoot = @"D:\youxi\steamapps\common\Slay the Spire 2\mods\SothebySkin";
        foreach (var (root, name) in new[] { (knightRoot, "AKnightSkin"), (sothebyRoot, "SothebySkin") })
            if (!ProjectSettings.LoadResourcePack(Path.Combine(root, name + ".pck")))
                throw new InvalidOperationException("Cannot mount combination pack " + name);
        var knight = context.LoadFromAssemblyPath(Path.Combine(knightRoot, "AKnightSkin.dll"));
        var sotheby = context.LoadFromAssemblyPath(Path.Combine(sothebyRoot, "SothebySkin.dll"));
        var mods = new[] { king, knight, sotheby };
        var owners = new[] { new Harmony("sts2.hcxmmx.king.visuals"), new Harmony("hcxmmx.aknight.visuals"), new Harmony("hcxmmx.sotheby.visuals") };
        var execute = AccessTools.DeclaredMethod(typeof(AttackCommand), nameof(AttackCommand.Execute));
        var state = execute.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        var moveNext = AccessTools.DeclaredMethod(state, "MoveNext");
        foreach (var order in new[] { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 0, 1, 2 }, new[] { 2, 1, 0 } })
        {
            try
            {
                foreach (int index in order) owners[index].PatchAll(mods[index]);
                var codes = PatchProcessor.GetCurrentInstructions(moveNext);
                int kingDamage = codes.Count(code => code.operand is MethodInfo method &&
                    method.DeclaringType?.FullName == "Hcxmmx.KingMod.Scripts.KingWeaponAudio" && method.Name == "DealDamage");
                int knightHit = codes.Count(code => code.operand is MethodInfo method &&
                    method.DeclaringType?.FullName == "Hcxmmx.AKnightSkin.ActualAttackTargetsPatch" && method.Name == "Observe");
                int soundGuards = codes.Count(code => code.operand is MethodInfo method &&
                    method.DeclaringType?.FullName == "Hcxmmx.KingMod.Scripts.KingWeaponAudio" &&
                    method.Name is "ReplacesAttacker" or "ReplacesHit");
                if (kingDamage != 1 || knightHit != 1 || soundGuards != 4)
                    throw new InvalidOperationException($"Combination coverage changed: King damage={kingDamage}, Knight hit={knightHit}, sound guards={soundGuards}");
                for (int index = 1; index < codes.Count; index++)
                {
                    if (codes[index].operand is not MethodInfo guard ||
                        guard.DeclaringType?.FullName != "Hcxmmx.KingMod.Scripts.KingWeaponAudio" ||
                        guard.Name is not ("ReplacesAttacker" or "ReplacesHit")) continue;
                    if (codes[index - 1].operand is not System.Reflection.Emit.LocalBuilder local || local.LocalType != typeof(string))
                        throw new InvalidOperationException("Audio guard must receive the string sound argument");
                }
                RuntimeHelpers.PrepareMethod(moveNext.MethodHandle);
                GD.Print("MOD_COMBINATION_PASS: " + string.Join(" -> ", order.Select(index => mods[index].GetName().Name)) +
                    "; full PatchAll, native observer, King hit scope, four sound guards");
            }
            finally
            {
                // Remove King first, letting each remaining observer see native IL.
                foreach (var owner in owners) owner.UnpatchAll(owner.Id);
            }
        }
    }
}
