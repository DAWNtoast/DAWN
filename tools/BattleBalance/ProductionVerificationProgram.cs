using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tests;

internal static class ProductionVerificationProgram
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    private static string Q(object value) => "\"" + Convert.ToString(value, CI).Replace("\"", "\"\"") + "\"";
    private static string Mean(IEnumerable<ShipProgressionSimulator.Result> source, Func<ShipProgressionSimulator.Result, int> selector)
    {
        var rows = source.ToArray();
        return rows.Length == 0 ? "" : rows.Average(selector).ToString("0.00", CI);
    }
    private static int Main(string[] args)
    {
        int seeds = int.Parse(args[0], CI);
        string output = args[1];
        if (seeds < 1 || seeds > 128) throw new ArgumentOutOfRangeException(nameof(seeds));
        var rows = new List<ShipProgressionSimulator.Result>();
        var legends = ShipProgressionSimulator.CalibrationScenarios.Where(s => s.EnemyId.StartsWith("legendary_")).ToArray();
        var tutorial = ShipProgressionSimulator.CalibrationScenarios.Single(s => s.Tutorial);
        using (var csv = new StreamWriter(Path.Combine(output, "runs.csv"), false, new System.Text.UTF8Encoding(true)))
        using (var formations = new StreamWriter(Path.Combine(output, "formations.csv"), false, new System.Text.UTF8Encoding(true)))
        {
            csv.WriteLine(ShipProgressionSimulator.CsvHeader);
            formations.WriteLine("ship,shipCost,hull,armor,firepower,speed,cargo,crewCapacity,specialists,ordinary,boardingHealth,boardingFactor,baseTier,specialistTier,sourceXP,effectiveArmor,effectiveFirepower,effectiveSpeed");
            foreach (int ship in new[] { 0, 1, 2, 7 })
            foreach (int tier in new[] { 2, 3 })
            foreach (bool mature in new[] { false, true })
            {
                ShipProgressionSimulator.SpecialistCount = 3;
                ShipProgressionSimulator.SpecialistUpgrade = mature ? tier : 0;
                var probe = ShipProgressionSimulator.CreateSession("PRODUCTION-PROBE", tier, "sustain", ship, 0);
                var hull = probe.SelectedShip;
                formations.WriteLine(string.Join(",", new object[] { hull.Id, CoreShipMarketCatalog.PurchaseCost(ship), hull.MaxHull, hull.Armor, hull.Firepower, hull.SpeedKnots, hull.CargoCapacity, hull.CrewCount, 3, probe.SelectedOrdinaryCrew,
                    CoreBattleBalance.BoardingHealthForOrdinaryCrew(probe.SelectedOrdinaryCrew), probe.GetBattleCardEffects(14, NavalBattleStage.Boarding).BoardingScale, tier, ShipProgressionSimulator.SpecialistUpgrade, mature ? tier == 2 ? 360 : 840 : 0, probe.EffectiveArmor, probe.EffectiveFirepower, probe.EffectiveSpeedKnots }.Select(Q)));
                for (int policy = 0; policy < 2; policy++)
                foreach (var enemy in legends)
                for (int seed = 0; seed < seeds; seed++)
                {
                    var result = ShipProgressionSimulator.Run("CREW-FLEET-" + seed.ToString("000"), enemy, tier, policy == 0 ? ShipProgressionSimulator.Tactic.Sustain : ShipProgressionSimulator.Tactic.Cannon, policy == 0 ? "sustain" : "focused", ship);
                    rows.Add(result); csv.WriteLine(result.Csv());
                }
                csv.Flush(); formations.Flush();
                Console.WriteLine(hull.Id + " base=" + tier + " source=" + ShipProgressionSimulator.SpecialistUpgrade + " completed=" + rows.Count);
            }
            foreach (int experts in new[] { 0, 1, 3 })
            {
                ShipProgressionSimulator.SpecialistCount = experts;
                ShipProgressionSimulator.SpecialistUpgrade = 0;
                for (int policy = 0; policy < 2; policy++)
                for (int seed = 0; seed < seeds; seed++)
                {
                    var result = ShipProgressionSimulator.Run("CREW-FLEET-" + seed.ToString("000"), tutorial, 0, policy == 0 ? ShipProgressionSimulator.Tactic.Boarding : ShipProgressionSimulator.Tactic.Cannon, "default", 0);
                    rows.Add(result); csv.WriteLine(result.Csv());
                }
                csv.Flush();
                Console.WriteLine("tutorial experts=" + experts + " completed=" + rows.Count);
            }
        }
        using (var summary = new StreamWriter(Path.Combine(output, "group-summary.csv"), false, new System.Text.UTF8Encoding(true)))
        {
            summary.WriteLine("ship,shipCost,upgrade,upgradeCost,specialists,specialistLevel,ordinaryCrew,initialHull,initialCrew,deck,strategy,enemyId,count,wins,pureNavalWins,enemyBoarding,meanHull,meanCrew,meanHullDamage,meanCrewDamage,meanHullHealed,meanCrewHealed,meanGuard,meanTurns,meanEnemyAttacks,maxBlock,timeouts");
            foreach (var group in rows.GroupBy(r => r.Ship + "/" + r.Upgrade + "/" + r.Specialists + "/" + r.SpecialistLevel + "/" + r.Deck + "/" + r.Strategy + "/" + r.EnemyId))
            {
                var f = group.First(); var wins = group.Where(r => r.Won).ToArray();
                summary.WriteLine(string.Join(",", new object[] { f.Ship, CoreShipMarketCatalog.PurchaseCost(f.ShipIndex), f.Upgrade, f.UpgradeCost, f.Specialists, f.SpecialistLevel, f.OrdinaryCrew, f.InitialHull, f.InitialCrewHealth, f.Deck, f.Strategy, f.EnemyId, group.Count(), wins.Length, wins.Count(r => r.FinalStage == "Naval"), group.Count(r => r.BoardingEntry == "enemy-ram"),
                    Mean(wins,r=>r.Hull),Mean(wins,r=>r.CrewHealth),Mean(wins,r=>r.HullDamageTaken),Mean(wins,r=>r.CrewDamageTaken),Mean(wins,r=>r.HullHealed),Mean(wins,r=>r.CrewHealed),Mean(wins,r=>r.GuardAbsorbed),Mean(wins,r=>r.Turns),Mean(wins,r=>r.EnemyAttackTurns),group.Max(r=>r.MaxBlock),group.Count(r=>r.Outcome.EndsWith("watchdog")) }.Select(Q)));
            }
        }
        using (var report = new StreamWriter(Path.Combine(output, "comparison.txt"), false, new System.Text.UTF8Encoding(true)))
        {
            report.WriteLine("PRODUCTION SOURCE VERIFICATION. Unmodified Core source snapshot; Core DLL compiled separately. " + rows.Count + " battles; " + seeds + " seeds/group. No Unity Editor run by this tool.");
            report.WriteLine("Scoring reads session.GetBattleCardEffects; damage, block and healing are resolved exclusively by production PlayCard. No additional crew scale is applied.");
            report.WriteLine("Legendary fixtures: bare ships, 3 Gray Gull specialists (cook Pepe, diver Noya, navigator Evelyn); ordinary=capacity-3. Source XP0 or mature II=360/III=840. Source XP is a fixture input, not an earned-play claim. Paid upgrades use actual APIs and costs.");
            report.WriteLine("Tutorial: unupgraded default decks and XP0; 0/1/3 local experts yields 10/9/7 ordinary. Lorne; plus Tam and Udo. Tutorial authored geometry repeats, so 16 seeds are repeated checks, not independent map samples.");
            report.WriteLine("Finite greedy sustain and cannon captains; public intents, current hand, dynamic fees only. No future-hand knowledge. Independent full health battles and 74.1 cargo provisions; bypass navigation via reflected StartBattle only. No between-turn resource injection. Results are not actual UI play or proofs of optimal player limits.");
            report.WriteLine("ship | base | source | experts | policy | ship+paid-card-cost | wins/total | pure naval wins | average winning hull/crew | average winning hull/crew damage | average winning turns | timeouts");
            foreach (var group in rows.GroupBy(r => r.Ship + "/" + r.Upgrade + "/" + r.Specialists + "/" + r.SpecialistLevel + "/" + r.Deck + "/" + r.Strategy))
            {
                var f = group.First(); var wins = group.Where(r => r.Won).ToArray();
                report.WriteLine(f.Ship + " | " + f.Upgrade + " | " + f.SpecialistLevel + " | " + f.Specialists + " | " + f.Deck + "/" + f.Strategy + " | " + (CoreShipMarketCatalog.PurchaseCost(f.ShipIndex) + f.UpgradeCost) + " | " + wins.Length + "/" + group.Count() + " | " + wins.Count(r => r.FinalStage == "Naval") + " | " + Mean(wins,r=>r.Hull) + "/" + Mean(wins,r=>r.CrewHealth) + " | " + Mean(wins,r=>r.HullDamageTaken) + "/" + Mean(wins,r=>r.CrewDamageTaken) + " | " + Mean(wins,r=>r.Turns) + " | " + group.Count(r=>r.Outcome.EndsWith("watchdog")));
            }
        }
        Console.WriteLine("COMPLETE " + rows.Count + " battles. Timeouts=" + rows.Count(r => r.Outcome.EndsWith("watchdog")));
        return rows.Any(r => r.Outcome.EndsWith("watchdog")) ? 1 : 0;
    }
}
