using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using HarmonyLib;

// Offline verification only: never run a mod initializer or install Harmony patches.
if (args.Length != 4)
    throw new ArgumentException("Expected: game-directory SkinChanger-directory old-package-directory new-package-directory");

var gameDir = Path.GetFullPath(args[0]);
var switcherDir = Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    foreach (var folder in new[] { Path.Combine(gameDir, "data_sts2_windows_x86_64"), switcherDir })
    {
        var path = Path.Combine(folder, name.Name + ".dll");
        if (File.Exists(path)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    }
    return null;
};
Validate(gameDir, switcherDir, args[2], args[3]);

static void Validate(string gameDir, string switcherDir, string oldRoot, string newRoot)
{
    const string modId = "Hcxmmx_King_Skin";
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    var switcher = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(switcherDir, "Gurio.SkinChanger.dll"));
    Console.WriteLine("SkinChanger " + switcher.GetName().Version);
    var scanner = switcher.GetType("STS2SkinChanger.Catalog.DirectCharacterRuntimeTargetScanner", true)!;
    var catalogType = switcher.GetType("STS2SkinChanger.Catalog.SkinCatalog", true)!;
    var descriptorType = switcher.GetType("STS2SkinChanger.Catalog.SkinModDescriptor", true)!;
    var groups = new[] { "ironclad", "silent", "defect", "necrobinder", "regent" };

    foreach (var root in new[] { oldRoot, newRoot })
    {
        var package = Path.GetFullPath(root);
        var dll = Path.Combine(package, modId + ".dll");
        var targets = (IReadOnlySet<string>)scanner.GetMethod("ScanAssembly", Static)!.Invoke(null, [dll, groups])!;
        var hasPatch = (bool)catalogType.GetMethod("HasDirectVisualHarmonyPatch", Static)!.Invoke(null, [dll])!;
        if (!targets.SetEquals(["regent"]) || !hasPatch)
            throw new Exception("SkinChanger did not recognize the Regent runtime skin: " + dll);
        Console.WriteLine($"SCANNER PASS: {dll}; targets=[regent], direct_visual_patch=true");

        var descriptors = Array.CreateInstance(descriptorType, 1);
        descriptors.SetValue(Activator.CreateInstance(descriptorType,
            modId, "King compatibility probe", Path.Combine(package, modId + ".pck"), false, package, true, null), 0);
        using var catalog = (IDisposable)catalogType.GetMethod("Build", Static)!.Invoke(null,
            [Path.Combine(gameDir, "SlayTheSpire2.pck"), descriptors])!;
        if (!(bool)catalogType.GetMethod("ProviderUsesDirectCharacterRuntime")!.Invoke(catalog, [modId])!)
            throw new Exception("Catalog did not classify King as a runtime provider");
        foreach (var selection in new[] { modId, "__base__", modId })
        {
            var providers = (IReadOnlySet<string>)catalogType.GetMethod("GetSelectedDirectCharacterRuntimeProviders")!
                .Invoke(catalog, [new Dictionary<string, string> { ["regent"] = selection }])!;
            if (!providers.SetEquals(selection == modId ? new[] { modId } : Array.Empty<string>()))
                throw new Exception("Incorrect runtime provider routing");
            Console.WriteLine($"ROUTING PASS: regent/{selection} -> [{string.Join(",", providers)}]");
        }
    }

    var mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(Path.GetFullPath(newRoot), modId + ".dll"));
    int patchCount = 0;
    foreach (var type in mod.GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), false)))
    {
        var targetFactory = type.GetMethod("TargetMethod", Static);
        MethodBase? original;
        if (targetFactory != null)
            original = (MethodBase?)targetFactory.Invoke(null, null);
        else
        {
            var info = type.GetCustomAttributes<HarmonyPatch>().Single().info;
            original = AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
        }
        if (original == null) throw new Exception("Missing patch target: " + type.Name);
        var parameters = original.GetParameters().Select(p => p.Name).ToHashSet();
        foreach (var patch in type.GetMethods(Static).Where(m => m.Name is "Prefix" or "Postfix"))
        {
            foreach (var arg in patch.GetParameters())
                if (!arg.Name!.StartsWith("__", StringComparison.Ordinal) && !parameters.Contains(arg.Name))
                    throw new Exception($"Unbound patch parameter: {type.Name}.{patch.Name}({arg.Name})");
        }
        if (type.Name == "CombatManager_EndCombatInternal_Patch")
        {
            var signature = original.GetParameters();
            if (signature.Length != 1 || signature[0].ParameterType.FullName != "MegaCrit.Sts2.Core.Combat.CombatTurnState")
                throw new Exception("Victory patch resolved the wrong overload");
            Console.WriteLine("VICTORY TARGET PASS: " + original);
        }
        patchCount++;
    }
    Console.WriteLine($"PATCH TARGETS PASS: {patchCount} classes, targets and argument bindings resolved against installed game");

    var localization = mod.GetType("Hcxmmx.KingMod.Scripts.KingLocalization", true)!;
    var quoteKind = mod.GetType("Hcxmmx.KingMod.Scripts.SerenadeQuoteKind", true)!;
    var getText = localization.GetMethod("GetText", Static)!;
    var getQuotes = localization.GetMethod("GetQuotes", Static)!;
    Dictionary<string, JsonElement>? baseline = null;
    foreach (var language in new[] { "zhs", "eng", "jpn" })
    {
        using var stream = mod.GetManifestResourceStream($"KingMod.Localization.{language}.json")!;
        using var document = JsonDocument.Parse(stream);
        var text = document.RootElement.GetProperty("Text").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        if (baseline != null && !text.Keys.ToHashSet().SetEquals(baseline.Keys))
            throw new Exception("Mismatched localization keys: " + language);
        baseline ??= text;
        foreach (var (key, value) in text)
        {
            var actual = (string)getText.Invoke(null, [key, language])!;
            if (string.IsNullOrWhiteSpace(actual) || actual.Contains('\ufffd') || actual != value.GetString())
                throw new Exception($"Invalid localized text: {language}/{key}");
        }
        foreach (var (kind, count) in new[] { ("Idle", 7), ("Attack", 6), ("Hit", 7), ("Summon", 7), ("Forge", 3) })
        {
            var quotes = (string[])getQuotes.Invoke(null, [Enum.Parse(quoteKind, kind), language])!;
            if (quotes.Length != count || quotes.Any(q => string.IsNullOrWhiteSpace(q) || q.Contains('\ufffd')))
                throw new Exception($"Invalid quote pool: {language}/{kind}");
        }
        Console.WriteLine($"LOCALIZATION PASS: {language}, {text.Count} UI strings + 30 quotes");
    }
    var english = getText.Invoke(null, ["character.name", "eng"]);
    if (!Equals(english, getText.Invoke(null, ["character.name", "fra"])))
        throw new Exception("Unsupported language did not fall back to English");
    foreach (var kind in Enum.GetValues(quoteKind))
    {
        var expected = (string[])getQuotes.Invoke(null, [kind, "eng"])!;
        var actual = (string[])getQuotes.Invoke(null, [kind, "fra"])!;
        if (!expected.SequenceEqual(actual)) throw new Exception("Quote fallback failed");
    }
    Console.WriteLine("FALLBACK PASS; no game startup, mod initialization, Harmony activation or settings writes");
}
