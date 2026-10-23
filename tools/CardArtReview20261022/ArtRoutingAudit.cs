using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace UnityEngine
{
    public sealed class Texture2D { public string name; }
    public struct Rect { public Rect(float x, float y, float w, float h) {} }
    public static class Resources
    {
        public static string Root;
        public static readonly HashSet<string> Hidden = new HashSet<string>();
        public static readonly HashSet<string> Injected = new HashSet<string>();
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        public static T Load<T>(string path) where T : class
        {
            if (Hidden.Contains(path) || !Injected.Contains(path) && !File.Exists(Path.Combine(Root, path + ".png"))) return null;
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
        public UnityEngine.Rect CardUv(string id) => new UnityEngine.Rect(0, 0, 1, 1);
    }
}
internal static class ArtRoutingAudit
{
    static int checks;
    static string J(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    static bool Visible(CoreBattleCard c) => c.AllowedStages != NavalBattleStages.None && !CoreBattleCardAliases.IsAlias(c.Id) &&
        !(c.AllowedStages == NavalBattleStages.Boarding && c.RequiredCrew.Length == 0 && c.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(c));
    static CoreBattleCard Card(string id) => CoreVoyageSession.CardLibrary.Single(c => c.Id == id);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void CacheClear() => ((Dictionary<string, UnityEngine.Texture2D>)typeof(CardSchoolArtCatalog).GetField("Textures", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Clear();
    static void Expect(string id, string path)
    {
        CoreBattleCard card = Card(id);
        string art = card.ArtId, description = card.Description;
        Check(CardSchoolArtCatalog.DedicatedIllustrationPath(card) == path, id + " expected " + path + " got " + CardSchoolArtCatalog.DedicatedIllustrationPath(card));
        Check(CardSchoolArtCatalog.Illustration(card, null) != null, id + " missing illustration");
        Check(card.ArtId == art && card.Description == description, id + " rules changed");
    }
    static string Before(CoreBattleCard c)
    {
        bool school = CardSchoolArtCatalog.HasSchoolIllustration(c);
        string id = c.BoardingSpec != null && c.Id.StartsWith("card_boarding_") && !school && !string.IsNullOrEmpty(c.ArtId)
            ? c.ArtId : c.Id == "card_energy_order" ? "card_brace" : c.Id == "card_tactical_draw" ? "card_full_sail_turn" : c.Id;
        // Review starts before this session's new per-card files were installed.
        string western = "CardArt/Western/" + id;
        bool preExisting = !newIds.Contains(id);
        return preExisting && File.Exists(Path.Combine(UnityEngine.Resources.Root, western + ".png")) ? western : school ? "CardSchools/Art/" + c.ArtId : western;
    }
    static HashSet<string> newIds;
    static int Main(string[] args)
    {
        UnityEngine.Resources.Root = args[0];
        newIds = new HashSet<string>(File.ReadAllLines(args[2]).Where(s => s.Length > 0));
        Expect("card_energy_order", "CardArt/Western/card_energy_order");
        Expect("card_tactical_draw", "CardArt/Western/card_tactical_draw");
        Expect("card_boarding_basic_13", "CardArt/Western/card_boarding_basic_13");
        Expect("card_boarding_basic_06", "CardArt/Western/card_crew_bandage");
        Expect("card_boarding_basic_08", "CardArt/Western/card_tactical_draw");
        Expect("card_boarding_fire_06", "CardSchools/Art/card_school_fire_b08");
        Expect("card_boarding_fire_04", "CardArt/Western/card_boarding_fire_04");
        Expect("card_school_fire_b10", "CardArt/Western/card_boarding_fire_04");
        Expect("card_crew_pistol", "CardArt/Western/card_cutlass_raid");
        Expect("card_special_plating_moonsilver_skin", "CardSchools/Art/card_school_arcane_b05");
        Expect("card_special_plating_worldtree_bark", "CardArt/Western/card_special_plating_worldtree_bark");
        Expect("card_chain_shot", "CardArt/Western/card_chain_shot");
        UnityEngine.Resources.Injected.Add("CardArt/Western/card_boarding_basic_06"); CacheClear();
        Expect("card_boarding_basic_06", "CardArt/Western/card_boarding_basic_06");
        UnityEngine.Resources.Injected.Clear(); CacheClear();
        Expect("card_boarding_basic_06", "CardArt/Western/card_crew_bandage");
        UnityEngine.Resources.Hidden.Add("CardArt/Western/card_energy_order"); CacheClear();
        Expect("card_energy_order", "CardArt/Western/card_brace");
        UnityEngine.Resources.Hidden.Clear();
        UnityEngine.Resources.Hidden.Add("CardArt/Western/card_boarding_basic_13"); CacheClear();
        Expect("card_boarding_basic_13", "CardArt/Western/card_cutlass_raid");
        UnityEngine.Resources.Hidden.Clear();
        UnityEngine.Resources.Hidden.Add("CardArt/Western/card_crew_bandage"); CacheClear();
        Expect("card_boarding_basic_06", "CardArt/Western/card_emergency_repair");
        UnityEngine.Resources.Hidden.Clear(); CacheClear();
        UnityEngine.Resources.Hidden.Add("CardArt/Western/card_boarding_fire_04"); CacheClear();
        Expect("card_boarding_fire_04", "CardSchools/Art/card_school_fire_b10");
        UnityEngine.Resources.Hidden.Clear(); CacheClear();
        var probe = new CoreBattleCard("unshipped_art_probe", "probe", "defense", 1, 0, 3, 0, 0, 0, "", NavalBattleStages.Ship, artId: "card_brace");
        var legacy = new LegacyDemoArtCatalog();
        Check(CardSchoolArtCatalog.Illustration(probe, null) == null, "Missing unknown card must not silently reuse art");
        Check(CardSchoolArtCatalog.Illustration(probe, legacy) == legacy.CardSheet, "Legacy atlas fallback lost");
        int active = 0, visible = 0, changedActive = 0, changedVisible = 0;
        var result = new StringBuilder("[");
        for (int i = 0; i < CoreVoyageSession.CardLibrary.Count; i++)
        {
            var c = CoreVoyageSession.CardLibrary[i];
            string path = CardSchoolArtCatalog.DedicatedIllustrationPath(c), before = Before(c);
            bool shown = Visible(c), changed = path != before;
            bool exists = path != null && File.Exists(Path.Combine(args[0], path + ".png"));
            if (c.AllowedStages != NavalBattleStages.None)
            {
                active++; if (changed) changedActive++;
                Check(exists && CardSchoolArtCatalog.HasDedicatedArt(c), c.Id + " missing current resource: " + path);
            }
            if (shown) { visible++; if (changed) changedVisible++; }
            if (i > 0) result.Append(',');
            result.Append("{\"index\":").Append(i).Append(",\"number\":").Append(CoreBattleCardNumbering.Number(c))
                .Append(",\"id\":").Append(J(c.Id)).Append(",\"name\":").Append(J(c.Name))
                .Append(",\"description\":").Append(J(c.Description)).Append(",\"schoolId\":").Append(J(c.SchoolId))
                .Append(",\"artId\":").Append(J(c.ArtId)).Append(",\"allowedStages\":").Append((int)c.AllowedStages)
                .Append(",\"visible\":").Append(shown ? "true" : "false").Append(",\"changed\":").Append(changed ? "true" : "false")
                .Append(",\"beforeResourceKey\":").Append(J(before)).Append(",\"resourceKey\":").Append(J(path))
                .Append(",\"path\":").Append(J(path == null ? null : Path.Combine(args[0], path + ".png")))
                .Append(",\"exists\":").Append(exists ? "true" : "false").Append('}');
        }
        result.Append(']');
        File.WriteAllText(Path.Combine(args[1], "runtime-card-art-current.json"), result.ToString(), new UTF8Encoding(false));
        string summary = "PASS checks=" + checks + "; slots=" + CoreVoyageSession.CardLibrary.Count + "; active=" + active + "; visible=" + visible + "; changedActive=" + changedActive + "; changedVisible=" + changedVisible;
        File.WriteAllText(Path.Combine(args[1], "art-routing-audit.txt"), summary + "\nCompiled current Core and verbatim production illustration methods. Resources stub checks real PNG files; missing-resource cases are simulated. No Unity render is claimed.\n", new UTF8Encoding(false));
        Console.WriteLine(summary);
        return 0;
    }
}
