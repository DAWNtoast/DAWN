using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tests;

internal static class CrewProgressionProgram
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    private static string Q(object o) => "\""+Convert.ToString(o,CI).Replace("\"","\"\"")+"\"";
    private static string Mean(ShipProgressionSimulator.Result[] rows, Func<ShipProgressionSimulator.Result,int> selector) => rows.Length==0 ? "" : rows.Average(selector).ToString("0.00",CI);
    private static int Main(string[] args)
    {
        int seeds=int.Parse(args[0],CI); string output=args[1], mode=args[2], candidate=args[3];
        if (seeds<1 || seeds>128) throw new ArgumentOutOfRangeException(nameof(seeds));
        bool tutorial=mode=="Tutorial", focus=mode=="CrewFocus", early=mode=="CrewEarly";
        ShipProgressionSimulator.CrewReference=args.Length>4 ? double.Parse(args[4],CI) : 20d;
        ShipProgressionSimulator.UseCrewScale=candidate!="None" && candidate!="StarterOnly";
        var enemies=tutorial ? new[]{new ShipProgressionSimulator.Scenario(CoreGuidedTutorialContent.BossEventId,0,true)} : ShipProgressionSimulator.CalibrationScenarios.Where(s=>s.EnemyId.StartsWith("boss_route_") || s.EnemyId.StartsWith("legendary_") || s.EnemyId==CoreBattleBalance.TreasureBossId).ToArray();
        if (focus) enemies=enemies.Where(s=>s.EnemyId.StartsWith("legendary_")).ToArray();
        var ships=tutorial ? new[]{0} : focus || early ? new[]{0,1,2,7} : Enumerable.Range(0,CoreVoyageSession.Ships.Count).ToArray();
        var levels=tutorial ? new[]{0} : new[]{2,3};
        var specialists=tutorial ? new[]{0,1,3} : focus ? new[]{2,3} : early ? new[]{3} : new[]{2,3,6};
        var rows=new List<ShipProgressionSimulator.Result>();
        using(var csv=new StreamWriter(Path.Combine(output,"runs.csv"),false,new System.Text.UTF8Encoding(true)))
        using(var formations=new StreamWriter(Path.Combine(output,"formations.csv"),false,new System.Text.UTF8Encoding(true)))
        {
            csv.WriteLine(ShipProgressionSimulator.CsvHeader);
            formations.WriteLine("ship,shipCost,hull,armor,firepower,speed,cargo,crewCapacity,specialists,ordinary,boardingHealth,boardingFactor,isShortStaffed,effectiveArmor,effectiveFirepower,effectiveSpeed,specialistCardLevel,prepBonusCards,navalBonusCards,boardingBonusCards");
            foreach(int specialist in specialists)
            foreach(int ship in ships)
            foreach(int level in levels)
            {
                ShipProgressionSimulator.SpecialistCount=specialist;
                ShipProgressionSimulator.SpecialistUpgrade=tutorial || early ? 0 : level;
                var probe=ShipProgressionSimulator.CreateSession("CREW-PROBE",level,tutorial?"default":"sustain",ship,0,tutorial);
                var hull=probe.SelectedShip;
                formations.WriteLine(string.Join(",",new object[]{hull.Id,CoreShipMarketCatalog.PurchaseCost(ship),hull.MaxHull,hull.Armor,hull.Firepower,hull.SpeedKnots,hull.CargoCapacity,hull.CrewCount,specialist,probe.SelectedOrdinaryCrew,
                    CoreBattleBalance.BoardingHealthForOrdinaryCrew(probe.SelectedOrdinaryCrew),Math.Max(.3d,Math.Min(1.6d,probe.SelectedOrdinaryCrew/ShipProgressionSimulator.CrewReference)),probe.IsShortStaffed,probe.EffectiveArmor,probe.EffectiveFirepower,probe.EffectiveSpeedKnots,
                    ShipProgressionSimulator.SpecialistUpgrade,probe.GetBonusBattleCards(NavalBattleStage.Preparation).Count,probe.GetBonusBattleCards(NavalBattleStage.Naval).Count,probe.GetBonusBattleCards(NavalBattleStage.Boarding).Count}.Select(Q)));
                for(int p=0;p<2;p++)
                {
                    string deck=tutorial ? "default" : p==0 ? "sustain" : "focused";
                    var tactic=tutorial ? (p==0?ShipProgressionSimulator.Tactic.Boarding:ShipProgressionSimulator.Tactic.Cannon) : p==0?ShipProgressionSimulator.Tactic.Sustain:ShipProgressionSimulator.Tactic.Cannon;
                    foreach(var enemy in enemies)
                    for(int seed=0;seed<seeds;seed++)
                    {
                        var r=ShipProgressionSimulator.Run("CREW-FLEET-"+seed.ToString("000"),enemy,level,tactic,deck,ship);
                        rows.Add(r); csv.WriteLine(r.Csv());
                    }
                }
                csv.Flush();formations.Flush();
                Console.WriteLine("experts="+specialist+" "+hull.Id+" "+level+": "+rows.Count+" battles");
            }
        }
        using(var summary=new StreamWriter(Path.Combine(output,"group-summary.csv"),false,new System.Text.UTF8Encoding(true)))
        {
            summary.WriteLine("ship,shipCost,upgrade,upgradeCost,specialists,specialistLevel,ordinaryCrew,initialHull,initialCrew,deck,strategy,enemyId,count,wins,pureNavalWins,enemyBoarding,meanHull,meanCrew,meanHullDamage,meanCrewDamage,meanHullHealed,meanCrewHealed,meanGuard,meanTurns,meanEnemyAttacks,maxBlock,timeouts");
            foreach(var group in rows.GroupBy(r=>r.Ship+"/"+r.Upgrade+"/"+r.Specialists+"/"+r.Deck+"/"+r.Strategy+"/"+r.EnemyId))
            {
                var f=group.First();var w=group.Where(r=>r.Won).ToArray();
                summary.WriteLine(string.Join(",",new object[]{f.Ship,CoreShipMarketCatalog.PurchaseCost(f.ShipIndex),f.Upgrade,f.UpgradeCost,f.Specialists,f.SpecialistLevel,f.OrdinaryCrew,f.InitialHull,f.InitialCrewHealth,f.Deck,f.Strategy,f.EnemyId,group.Count(),w.Length,w.Count(r=>r.FinalStage=="Naval"),group.Count(r=>r.BoardingEntry=="enemy-ram"),
                    Mean(w,r=>r.Hull),Mean(w,r=>r.CrewHealth),Mean(w,r=>r.HullDamageTaken),Mean(w,r=>r.CrewDamageTaken),Mean(w,r=>r.HullHealed),Mean(w,r=>r.CrewHealed),Mean(w,r=>r.GuardAbsorbed),Mean(w,r=>r.Turns),Mean(w,r=>r.EnemyAttackTurns),group.Max(r=>r.MaxBlock),group.Count(r=>r.Outcome.EndsWith("watchdog"))}.Select(Q)));
            }
        }
        using(var text=new StreamWriter(Path.Combine(output,"comparison.txt"),false,new System.Text.UTF8Encoding(true)))
        {
            text.WriteLine("EXPERIMENT ONLY. Crew reference="+ShipProgressionSimulator.CrewReference+". Candidate="+candidate+". Source production Assets unchanged; no Unity run. "+rows.Count+" battles, "+seeds+" seeds/group.");
            text.WriteLine("2 experts: Gray Gull cook Pepe + diver Noya. 3 adds navigator Evelyn. 6 adds carpenter Barlow + doctor Moer + gunner Leisa. Cook and doctor have no authored combat-source card. Ordinary sailors=capacity-experts. All real shortage penalties retained.");
            text.WriteLine("Special source XP is 360/840 at II/III (not paid upgrades or earned-play claim). Tutorial: source XP 0 and default decks; 1 expert is Broken Anchor navigator Lorne; 3 adds local cook Tam and diver Udo. 0/1/3 experts give 10/9/7 ordinary sailors on candidate starter. Tutorial seeds repeat the fixed authored map, not independent geography.");
            text.WriteLine("Two fixed policies: sustain boarding, focused cannon (can be forced into boarding). Bare equipment quality 1, no component cards. 74.1 capacity combat load and independent full-health fights; skips navigation, economy and continuous attrition. Finite greedy tactics do not prove player impossibility.");
            text.WriteLine("ship | experts | ordinary | base tier | ship+card cost | policy | boss wins/total | weakest boss | average winning hull/crew | turns");
            foreach(var grouped in rows.Where(r=>tutorial || r.EnemyId.StartsWith("legendary_")).GroupBy(r=>r.Ship+"/"+r.Specialists+"/"+r.Upgrade+"/"+r.Deck+"/"+r.Strategy))
            {
                var f=grouped.First();var w=grouped.Where(r=>r.Won).ToArray();
                text.WriteLine(f.Ship+" | "+f.Specialists+" | "+f.OrdinaryCrew+" | "+f.Upgrade+" | "+(CoreShipMarketCatalog.PurchaseCost(f.ShipIndex)+f.UpgradeCost)+" | "+f.Deck+"/"+f.Strategy+" | "+w.Length+"/"+grouped.Count()+" | "+grouped.GroupBy(r=>r.EnemyId).Min(g=>g.Count(r=>r.Won))+"/"+seeds+" | "+Mean(w,r=>r.Hull)+"/"+Mean(w,r=>r.CrewHealth)+" | "+Mean(w,r=>r.Turns));
            }
        }
        return 0;
    }
}
