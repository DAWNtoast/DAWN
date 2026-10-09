using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tests;

static class CombatAssessmentProgram
{
    static string Q(object value) => "\"" + Convert.ToString(value,CultureInfo.InvariantCulture).Replace("\"","\"\"") + "\"";
    static int Main(string[] args)
    {
        int seeds=int.Parse(args[0]); string output=args[1];
        var all=new List<Tuple<string,int,ShipProgressionSimulator.Result>>();
        var early=ShipProgressionSimulator.CalibrationScenarios.Where(s=>!s.Tutorial && !s.EnemyId.StartsWith("legendary_") && s.EnemyId!=CoreBattleBalance.TreasureBossId).ToArray();
        var late=ShipProgressionSimulator.CalibrationScenarios.Where(s=>s.EnemyId.StartsWith("legendary_")||s.EnemyId.StartsWith("boss_route_")||s.EnemyId==CoreBattleBalance.TreasureBossId).ToArray();
        var tutorial=ShipProgressionSimulator.CalibrationScenarios.Single(s=>s.Tutorial);
        using(var csv=new StreamWriter(Path.Combine(output,"runs.csv"),false,new System.Text.UTF8Encoding(true)))
        {
            csv.WriteLine("phase,medicineSupply,"+ShipProgressionSimulator.CsvHeader);
            Action<string,int,ShipProgressionSimulator.Scenario,int,int,bool> run=(phase,ship,enemy,tier,policy,mature)=>{
                ShipProgressionSimulator.SpecialistUpgrade=mature?tier:0;
                var tactic=policy==0?ShipProgressionSimulator.Tactic.Sustain:ShipProgressionSimulator.Tactic.Cannon;
                string deck=policy==0?"sustain":"focused";
                if(phase=="tutorial") {tactic=policy==0?ShipProgressionSimulator.Tactic.Boarding:ShipProgressionSimulator.Tactic.Cannon;deck="default";}
                foreach(int medicine in new[]{0,10})
                {
                    ShipProgressionSimulator.MedicineSupply=medicine;
                    for(int seed=0;seed<seeds;seed++) {
                        var result=ShipProgressionSimulator.Run("CURRENT-AUDIT-"+seed.ToString("000"),enemy,tier,tactic,deck,ship);
                        all.Add(Tuple.Create(phase,medicine,result));csv.WriteLine(phase+","+medicine+","+result.Csv());
                    }
                }
            };
            foreach(int ship in new[]{0,1,2,7}) {
                ShipProgressionSimulator.SpecialistCount=3;
                foreach(int tier in new[]{0,1}) foreach(var enemy in early) for(int p=0;p<2;p++) run("early",ship,enemy,tier,p,false);
                foreach(int tier in new[]{2,3}) foreach(bool mature in new[]{false,true}) foreach(var enemy in late) for(int p=0;p<2;p++) run("late",ship,enemy,tier,p,mature);
                csv.Flush();Console.WriteLine(CoreVoyageSession.Ships[ship].Id+": "+all.Count);
            }
            foreach(int experts in new[]{0,1,3}) { ShipProgressionSimulator.SpecialistCount=experts; for(int p=0;p<2;p++) run("tutorial",0,tutorial,0,p,false); }
        }
        using(var csv=new StreamWriter(Path.Combine(output,"summary.csv"),false,new System.Text.UTF8Encoding(true))) {
            csv.WriteLine("phase,medicineSupply,ship,shipPrice,tier,cardCost,experts,expertTier,policy,enemyId,count,wins,pureNavalWins,enemyRamEntries,meanMedicineUsed,meanHull,meanCrew,meanCrewDamage,meanCrewHealed,meanTurns,maxTurns,timeouts");
            foreach(var group in all.GroupBy(x=>x.Item1+"/"+x.Item2+"/"+x.Item3.Ship+"/"+x.Item3.Upgrade+"/"+x.Item3.Specialists+"/"+x.Item3.SpecialistLevel+"/"+x.Item3.Strategy+"/"+x.Item3.EnemyId)) {
                var f=group.First();var r=f.Item3;var rows=group.Select(x=>x.Item3).ToArray();
                csv.WriteLine(string.Join(",",new object[]{f.Item1,f.Item2,r.Ship,CoreShipMarketCatalog.PurchaseCost(r.ShipIndex),r.Upgrade,r.UpgradeCost,r.Specialists,r.SpecialistLevel,r.Strategy,r.EnemyId,rows.Length,rows.Count(x=>x.Won),rows.Count(x=>x.Won&&x.FinalStage=="Naval"),rows.Count(x=>x.BoardingEntry=="enemy-ram"),rows.Average(x=>x.MedicineUsed),rows.Average(x=>x.Hull),rows.Average(x=>x.CrewHealth),rows.Average(x=>x.CrewDamageTaken),rows.Average(x=>x.CrewHealed),rows.Average(x=>x.Turns),rows.Max(x=>x.Turns),rows.Count(x=>x.Outcome.EndsWith("watchdog"))}.Select(Q)));
            }
        }
        Console.WriteLine("COMPLETE "+all.Count+" battles; watchdogs="+all.Count(x=>x.Item3.Outcome.EndsWith("watchdog")));
        return 0;
    }
}
