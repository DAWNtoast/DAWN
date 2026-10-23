using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;

public static class ComponentCombatAudit
{
    static int checks;
    static void Require(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
    static Dictionary<string, Dictionary<string,string>> Catalog(string path)
    {
        string[] lines = File.ReadAllLines(path);
        string[] headers = lines[0].TrimStart('\ufeff').Split(',');
        return lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line =>
        {
            string[] cells = line.Split(',');
            return headers.Select((key,index) => new { key, value = cells[index] }).ToDictionary(cell => cell.key, cell => cell.value);
        }).ToDictionary(row => row["component_id"]);
    }
    static int Number(Dictionary<string,string> row, string key) => int.Parse(row[key], CultureInfo.InvariantCulture);
    static float Real(Dictionary<string,string> row, string key) => float.Parse(row[key], CultureInfo.InvariantCulture);
    static CoreVoyageSession Start(int ship, Dictionary<string,string> component, Dictionary<string,string> baseline)
    {
        var s = new CoreVoyageSession();
        Require(s.TryDepositAllPortCargo(), "Deposit cargo");
        Require(s.SelectShip(ship), "Select ship");
        s.ConfigureDepartureCrew(s.SelectedShip.CrewCount, 0);
        s.ConfigureCrewCapabilities(false, false, false, false, false, false);
        s.ConfigureSpecialCardSources(null, null);
        if (component != null) s.ConfigureComponentBonuses(
            Number(component,"max_hull") - Number(baseline,"max_hull"),
            Number(component,"armor") - Number(baseline,"armor"),
            Number(component,"firepower") - Number(baseline,"firepower"),
            Real(component,"speed") - Real(baseline,"speed"),
            Real(component,"vision") - Real(baseline,"vision"));
        s.SetSeed("COMPONENT-COMBAT-AUDIT-20261023");
        s.Cargo.Powder = s.Cargo.Cannonballs = 20;
        s.Cargo.Wood = s.Cargo.Medicine = 0;
        s.StartVoyage();
        typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s,
            new object[] { new CoreEncounter("component_audit", "Component audit", "naval", "Isolated comparison fixture", true, false, 1, 70), false });
        s.Battle.Stage = NavalBattleStage.Naval;
        // Isolate the stat being measured, following the existing EditMode fixtures.
        // PlayCard and EndBattleTurn below remain the unmodified production methods.
        s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced, 1000, 20, 7, 0, 0, 10, 1f, 1f, healthMultiplier: 1);
        s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
        s.Battle.EnemyBlock = s.Battle.PlayerBlock = 0;
        s.Battle.PlayerLoadedShot = s.Battle.EnemyLoadedShot = 0;
        s.Battle.DistanceMeters = 200;
        s.Battle.Alignment = NavalBattleAlignment.Parallel;
        typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s,0);
        s.Battle.Energy = 100;
        return s;
    }
    public static void Main(string[] args)
    {
        var catalog = Catalog(args[0]);
        var rows = new List<string>{"ship,component,firepower,armor,effectiveFirepower,effectiveArmor,cardBaseDamage,quotedDamage,actualDamage,enemyRawDamage,actualReceived,maxHull"};
        foreach(int ship in new[]{0,2})
        foreach(string id in new[]{"baseline","cannon_bronze_longgun","cannon_chain_repeater","plating_gilded_bulwark","plating_moonsilver_skin","plating_living_hull"})
        {
            var component = id == "baseline" ? null : catalog[id];
            var baseline = component == null ? null : catalog[id.StartsWith("cannon_") ? "cannon_deck_swivel" : "plating_braced_ribs"];
            var s = Start(ship,component,baseline);
            int firepower = s.EffectiveFirepower, armor = s.EffectiveArmor;
            int cardIndex = Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_chain_shot");
            var card = s.GetBattleCard(cardIndex);
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(cardIndex);
            int quote = s.GetBattleCardDamage(card,NavalBattleStage.Naval), enemyBefore = s.Battle.EnemyHull;
            Require(s.PlayCard(0), "PlayCard rejected: " + s.CardUnavailableReason(0));
            int damage = enemyBefore - s.Battle.EnemyHull;
            Require(damage == quote, "Quote/runtime mismatch: " + id);
            Require(damage == card.Damage + Math.Max(0,(firepower - 8)/2), "Cannon damage formula mismatch: " + id);
            // Separate fresh battle prevents damage-linked crew loss from influencing the defense comparison.
            var defense = Start(ship,component,baseline);
            int hullBefore = defense.Hull;
            Require(defense.EndBattleTurn(), "EndBattleTurn rejected");
            int received = hullBefore - defense.Hull;
            Require(received == Math.Max(1,20 - Math.Max(0,(armor-4)/2)), "Armor formula mismatch: " + id + ", received=" + received);
            rows.Add(string.Join(",",new object[]{s.SelectedShip.Id,id,s.Firepower,s.Armor,firepower,armor,card.Damage,quote,damage,20,received,s.MaxHull}));
        }
        File.WriteAllLines(Path.Combine(args[1],"results.csv"),rows);
        foreach(string row in rows) Console.WriteLine(row);
        Console.WriteLine("PASS: " + checks + " assertions; 12 loadouts; real production PlayCard/EndBattleTurn; controlled Core fixture, not Unity UI playthrough.");
    }
}
