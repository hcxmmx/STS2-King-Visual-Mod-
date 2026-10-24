using System;
using System.Collections.Generic;
using System.Text.Json;
using MegaCrit.Sts2.Core.Localization;

namespace Hcxmmx.KingMod.Scripts;

internal enum SerenadeQuoteKind { Idle, Attack, Hit, Summon, Forge }

/// <summary>Mod-owned text, embedded in the DLL so translations do not require a new PCK.</summary>
internal static class KingLocalization
{
    private sealed class Catalog
    {
        public Dictionary<string, string> Text { get; set; } = new();
        public Dictionary<string, string[]> Quotes { get; set; } = new();
    }

    private static readonly Dictionary<string, Catalog> Catalogs = new(StringComparer.OrdinalIgnoreCase);

    // Use the game's language codes. Unsupported languages fall back to English.
    private static string CurrentLanguage => LocManager.Instance?.Language ?? "zhs";

    internal static string GetText(string key, string? language = null)
    {
        foreach (var code in FallbackLanguages(language ?? CurrentLanguage))
        {
            if (Load(code).Text.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text))
                return text;
        }
        return key;
    }

    internal static string PickQuote(SerenadeQuoteKind kind)
    {
        var quotes = GetQuotes(kind);
        return quotes.Length == 0 ? "" : quotes[KingGlobals.Rng.Next(quotes.Length)];
    }

    internal static string[] GetQuotes(SerenadeQuoteKind kind, string? language = null)
    {
        foreach (var code in FallbackLanguages(language ?? CurrentLanguage))
        {
            if (Load(code).Quotes.TryGetValue(kind.ToString(), out var quotes) && quotes.Length > 0)
                return quotes;
        }
        return Array.Empty<string>();
    }

    private static IEnumerable<string> FallbackLanguages(string language)
    {
        // Only resolve bundled languages, never use external paths from locale settings.
        if (language is "zhs" or "eng" or "jpn") yield return language;
        if (language != "eng") yield return "eng";
        if (language != "zhs") yield return "zhs";
    }

    private static Catalog Load(string language)
    {
        if (Catalogs.TryGetValue(language, out var catalog)) return catalog;
        using var stream = typeof(KingLocalization).Assembly.GetManifestResourceStream($"KingMod.Localization.{language}.json");
        if (stream == null) throw new InvalidOperationException($"Missing King localization: {language}");
        catalog = JsonSerializer.Deserialize<Catalog>(stream)
            ?? throw new InvalidOperationException($"Invalid King localization: {language}");
        Catalogs[language] = catalog;
        return catalog;
    }
}
