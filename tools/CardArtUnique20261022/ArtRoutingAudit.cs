using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace UnityEngine
{
    public sealed class Texture2D { public string name; }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
    public static class Resources
    {
        public static string Root;
        public static readonly HashSet<string> Hidden = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        public static T Load<T>(string path) where T : class
        {
            if (Hidden.Contains(path) || !File.Exists(Path.Combine(Root, path + ".png"))) return null;
            if (!cache.TryGetValue(path, out Texture2D texture))
                cache[path] = texture = new Texture2D { name = Path.GetFileName(path) };
            return texture as T;
        }
    }
}
namespace ShatteredSail.Presentation
{
    public sealed class LegacyDemoArtCatalog
    {
        public UnityEngine.Texture2D CardSheet = new UnityEngine.Texture2D { name = "legacy-atlas" };
        public UnityEngine.Rect CardUv(string id) => new UnityEngine.Rect(.25f, .5f, .25f, .5f);
    }
}
internal static class ArtRoutingAudit
{
    sealed class Scope
    {
        public string Id, Name, Before, BeforeSha256, InstalledSha256;
        public bool Installed;
    }
    static int checks;
    static readonly List<string> failures = new List<string>();
    static string J(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    static string JsonArray(IEnumerable<string> values) => "[" + string.Join(",", values.Select(J)) + "]";
    static string Hash(string path)
    {
        if (!File.Exists(path)) return null;
        using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    static string ResourceFile(string key) => key == null ? null : Path.Combine(UnityEngine.Resources.Root, key + ".png");
    static bool Visible(CoreBattleCard c) => c.AllowedStages != NavalBattleStages.None && !CoreBattleCardAliases.IsAlias(c.Id) &&
        !(c.AllowedStages == NavalBattleStages.Boarding && c.RequiredCrew.Length == 0 && c.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(c));
    static void Check(bool ok, string message) { checks++; if (!ok) failures.Add(message); }
    static void CacheClear() => ((Dictionary<string, UnityEngine.Texture2D>)typeof(CardSchoolArtCatalog).GetField("Textures", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Clear();
    static void CheckFullUv(CoreBattleCard card)
    {
        UnityEngine.Rect uv = CardSchoolArtCatalog.IllustrationUv(card, null);
        Check(uv.x == 0f && uv.y == 0f && uv.width == 1f && uv.height == 1f, card.Id + ": illustration UV must retain the full image");
    }
    static int Main(string[] args)
    {
        UnityEngine.Resources.Root = args[0];
        string output = args[1];
        var scope = File.ReadAllLines(args[2]).Where(line => line.Length > 0).Select(line =>
        {
            var fields = line.Split('\t');
            return new Scope { Id = fields[0], Name = fields[1], Before = fields[2], BeforeSha256 = fields[3], Installed = fields[4] == "1", InstalledSha256 = fields[5] };
        }).ToArray();
        Check(scope.Length == 38 && scope.Select(s => s.Id).Distinct().Count() == 38, "Scope must contain exactly 38 unique card IDs");
        var cards = CoreVoyageSession.CardLibrary.ToDictionary(card => card.Id, StringComparer.Ordinal);
        var installed = scope.Where(s => s.Installed).ToArray();
        var missing = scope.Where(s => !s.Installed).Select(s => s.Id).ToArray();
        var unlistedFiles = scope.Where(s => !s.Installed && File.Exists(ResourceFile("CardArt/Western/" + s.Id))).Select(s => s.Id).ToArray();
        var rows = new List<string>();
        foreach (Scope row in scope)
        {
            Check(cards.TryGetValue(row.Id, out CoreBattleCard card), row.Id + ": missing current Core definition");
            Check(Hash(ResourceFile(row.Before)) == row.BeforeSha256, row.Id + ": original shared fallback image changed");
            if (card == null) continue;
            string own = "CardArt/Western/" + row.Id;
            string actual = CardSchoolArtCatalog.DedicatedIllustrationPath(card);
            string hiddenResult = null, restored = null;
            string originalArtId = card.ArtId, originalDescription = card.Description;
            if (row.Installed)
            {
                Check(Hash(ResourceFile(own)) == row.InstalledSha256 && row.InstalledSha256.Length == 64, row.Id + ": installed PNG hash does not match installed.json");
                Check(actual == own, row.Id + ": installed card must resolve its own resource, got " + actual);
                Check(CardSchoolArtCatalog.Illustration(card, null) != null, row.Id + ": installed illustration not loadable");
                CheckFullUv(card);
                UnityEngine.Resources.Hidden.Add(own);
                CacheClear();
                hiddenResult = CardSchoolArtCatalog.DedicatedIllustrationPath(card);
                Check(hiddenResult == row.Before, row.Id + ": hidden own-resource fallback expected " + row.Before + ", got " + hiddenResult);
                Check(CardSchoolArtCatalog.Illustration(card, null) != null, row.Id + ": hidden own-resource fallback not loadable");
                UnityEngine.Resources.Hidden.Remove(own);
                CacheClear();
                restored = CardSchoolArtCatalog.DedicatedIllustrationPath(card);
                Check(restored == own && CardSchoolArtCatalog.Illustration(card, null) != null, row.Id + ": failed to restore own resource after clearing cache");
                Check(card.ArtId == originalArtId && card.Description == originalDescription, row.Id + ": presentation altered mechanical ArtId or rules text");
            }
            rows.Add("{\"id\":" + J(row.Id) + ",\"name\":" + J(row.Name) + ",\"installed\":" + (row.Installed ? "true" : "false") +
                ",\"expectedOwn\":" + J(own) + ",\"resourceKey\":" + J(actual) + ",\"expectedFallback\":" + J(row.Before) +
                ",\"hiddenOwnFallback\":" + J(hiddenResult) + ",\"restoredResource\":" + J(restored) + "}");
        }
        int active = 0, visible = 0, aliasChecks = 0;
        var runtime = new List<string>();
        var aliases = new List<string>();
        for (int i = 0; i < CoreVoyageSession.CardLibrary.Count; i++)
        {
            CoreBattleCard c = CoreVoyageSession.CardLibrary[i];
            string key = CardSchoolArtCatalog.DedicatedIllustrationPath(c);
            bool exists = key != null && File.Exists(ResourceFile(key)), shown = Visible(c);
            if (c.AllowedStages != NavalBattleStages.None)
            {
                active++;
                Check(exists && CardSchoolArtCatalog.HasDedicatedArt(c), c.Id + ": active card illustration missing: " + key);
            }
            if (shown) visible++;
            if (CoreBoardingCardCatalog.IsLegacyAlias(c) && !c.Id.StartsWith("card_special_", StringComparison.Ordinal))
            {
                CoreBattleCard boarding = CoreBoardingCardCatalog.ForBoarding(c);
                if (boarding.BoardingSpec != null)
                {
                    CoreBattleCard design = CoreBoardingCardCatalog.FindByKey(boarding.BoardingSpec.Key);
                    string aliasPath = CardSchoolArtCatalog.DedicatedIllustrationPath(boarding);
                    string designPath = CardSchoolArtCatalog.DedicatedIllustrationPath(design);
                    Check(aliasPath == designPath && CardSchoolArtCatalog.HasDedicatedArt(boarding), c.Id + ": legacy boarding alias must resolve current design " + design.Id);
                    aliasChecks++;
                    aliases.Add("{\"legacyId\":" + J(c.Id) + ",\"designId\":" + J(design.Id) + ",\"resourceKey\":" + J(aliasPath) + "}");
                }
            }
            runtime.Add("{\"index\":" + i + ",\"number\":" + CoreBattleCardNumbering.Number(c) + ",\"id\":" + J(c.Id) +
                ",\"name\":" + J(c.Name) + ",\"description\":" + J(c.Description) + ",\"artId\":" + J(c.ArtId) +
                ",\"schoolId\":" + J(c.SchoolId) + ",\"allowedStages\":" + (int)c.AllowedStages + ",\"visible\":" + (shown ? "true" : "false") +
                ",\"resourceKey\":" + J(key) + ",\"path\":" + J(ResourceFile(key)) + ",\"exists\":" + (exists ? "true" : "false") + "}");
        }
        Check(active == 346, "Expected 346 active cards from current audited catalog, got " + active);
        Check(aliasChecks > 0, "Legacy boarding alias verification did not exercise any card");
        var probe = new CoreBattleCard("unshipped_art_probe", "probe", "defense", 1, 0, 3, 0, 0, 0, "", NavalBattleStages.Ship, artId: "card_brace");
        var legacy = new LegacyDemoArtCatalog();
        Check(CardSchoolArtCatalog.Illustration(probe, null) == null, "Unknown missing card must remain missing without atlas");
        Check(CardSchoolArtCatalog.Illustration(probe, legacy) == legacy.CardSheet, "Legacy atlas compatibility fallback lost");
        bool complete = installed.Length == scope.Length && failures.Count == 0;
        string status = failures.Count > 0 ? "FAIL" : complete ? "COMPLETE_PASS" : "PARTIAL_PASS";
        string summary = "{\"status\":" + J(status) + ",\"complete\":" + (complete ? "true" : "false") + ",\"expected\":" + scope.Length +
            ",\"installed\":" + installed.Length + ",\"missing\":" + JsonArray(missing) + ",\"unlistedOwnFiles\":" + JsonArray(unlistedFiles) +
            ",\"checks\":" + checks + ",\"failed\":" + failures.Count + ",\"failures\":" + JsonArray(failures) +
            ",\"slots\":" + CoreVoyageSession.CardLibrary.Count + ",\"activeCards\":" + active + ",\"visibleCards\":" + visible +
            ",\"legacyAliasChecks\":" + aliasChecks + ",\"limitations\":" + J("Current Core and verbatim production illustration methods; Resources stub checks real PNG files and simulates missing own resources. No Unity import, render or visual-quality claim.") + "}";
        File.WriteAllText(Path.Combine(output, "art-routing-audit.json"), summary, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "scope-routing-results.json"), "[" + string.Join(",", rows) + "]", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "legacy-alias-results.json"), "[" + string.Join(",", aliases) + "]", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "runtime-card-art-current.json"), "[" + string.Join(",", runtime) + "]", new UTF8Encoding(false));
        string text = status + "; installed=" + installed.Length + "/" + scope.Length + "; missing=" + missing.Length + "; checks=" + checks + "; failed=" + failures.Count +
            "; active=" + active + "; visible=" + visible + "; aliases=" + aliasChecks;
        File.WriteAllText(Path.Combine(output, "art-routing-audit.txt"), text + "\nMissing: " + string.Join(", ", missing) +
            "\n" + string.Join("\n", failures) + "\nPARTIAL_PASS verifies the installed subset only; it never means all 38 illustrations are complete. No Unity render was run.\n", new UTF8Encoding(false));
        Console.WriteLine(text);
        foreach (string failure in failures) Console.WriteLine("FAIL " + failure);
        return failures.Count == 0 ? 0 : 1;
    }
}
