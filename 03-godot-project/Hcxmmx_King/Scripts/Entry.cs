using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

// 极其规范的专属命名空间
namespace Hcxmmx.KingMod.Scripts;

[ModInitializer("Init")]
public class Entry
{
    public static void Init()
    {
        KingGlobals.ApplyRuntimeProfile();
        KingGlobals.LoadConfig();

        if (KingGlobals.EnableVerboseLogs)
        {
            KingGlobals.VerboseLog("\n====================================");
            KingGlobals.VerboseLog("Hcxmmx King Project: 屑国王与夜歌核心极其华丽地点火！");
            KingGlobals.VerboseLog("====================================\n");
        }

        // 极其唯一的 Harmony ID，防止与天子和咲夜撞车！
        var harmony = new Harmony(KingGlobals.HarmonyId);
        harmony.PatchAll();

        // [Optimized]: 初始化阶段绑定预加载资源，避免战斗中首次加载卡顿。
        KingGlobals.KingScene = KingGlobals.KingScenePreloaded;
        KingGlobals.KingSelectUiScene = KingGlobals.KingSelectUiPreloaded;

        if (KingGlobals.EnableVerboseLogs)
        {
            Log.Debug($"King & Serenade Skin initialized. Profile={KingGlobals.CurrentProfile}");
        }
    }
}