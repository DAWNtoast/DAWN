using System;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
public static class GameplayAuditProbe {
 static readonly BindingFlags Private = BindingFlags.Instance|BindingFlags.NonPublic;
 static void ClearAndLoad(CoreVoyageSession s) {foreach(var p in typeof(VoyageCargo).GetProperties().Where(p=>p.PropertyType==typeof(int))) p.SetValue(s.Cargo,0);s.Cargo.Water=100;s.Cargo.Food=100;s.Cargo.Iron=12;s.Cargo.Wood=6;s.Cargo.Lantern=2;}
 static CoreVoyageSession Ready(string target) {
  var s=new CoreVoyageSession();s.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10)});
  for(int i=0;i<150;i++){string seed=CoreVoyageSession.CreateChartSeed(2,"断锚港","白贝港");if(!s.TrySelectChartSeed(seed,out var reason)) throw new Exception(reason);if(s.PreviewSelectedSeaChart().Nodes.Any(n=>n.Encounter.Id==target)){Console.WriteLine("chart="+seed);return s;}}
  throw new Exception("target not sampled: "+target);
 }
 static void Enter(CoreVoyageSession s,string id){var n=s.Map.Nodes.Single(n=>n.Encounter.Id==id);typeof(CoreVoyageSession).GetField("activeNode",Private).SetValue(s,n);typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(s,n.Encounter);typeof(CoreVoyageSession).GetProperty("Phase").SetValue(s,CoreDemoPhase.Encounter);}
 static void Revisit(string id,int choice) {
  var s=Ready(id);
  for(int visit=0;visit<2;visit++) {
   ClearAndLoad(s);s.StartVoyage();Enter(s,id);var e=s.EffectiveChoiceEffect(choice);
   int iron=s.Cargo.Iron,wood=s.Cargo.Wood,lantern=s.Cargo.Lantern,relic=s.Cargo.Relic,compass=s.Cargo.AncientCompass,curse=s.Cargo.Curse,water=s.Cargo.Water,food=s.Cargo.Food;
   Console.WriteLine("case="+id+" visit="+(visit+1)+" claim="+s.ActiveEncounterClaimCount+" multiplier="+s.ActiveEncounterRewardMultiplier+" available="+s.IsChoiceAvailable(choice)+" quotedCompass="+e.AncientCompass+" quotedRelic="+e.Relic);
   bool ok=s.ResolveChoice(choice);
   Console.WriteLine("resolved="+ok+" ironDelta="+(s.Cargo.Iron-iron)+" woodDelta="+(s.Cargo.Wood-wood)+" lanternDelta="+(s.Cargo.Lantern-lantern)+" relicDelta="+(s.Cargo.Relic-relic)+" compassDelta="+(s.Cargo.AncientCompass-compass)+" curseDelta="+(s.Cargo.Curse-curse)+" waterDelta="+(s.Cargo.Water-water)+" foodDelta="+(s.Cargo.Food-food));
   if(!ok)throw new Exception("Resolution failed");s.SafeReturn();s.ReturnToPortAfterSettlement();
  }
 }
 static void OneUnitSettlement(){foreach(var outcome in new[]{VoyageOutcome.SafeReturn,VoyageOutcome.ArrivedAtPort}){var s=new CoreVoyageSession();ClearAndLoad(s);s.Cargo.AncientMaterial=0;s.Cargo.Wood=0;s.StartVoyage();s.Cargo.AncientMaterial=1;s.Cargo.Wood=1;typeof(CoreVoyageSession).GetMethod("FinishVoyage",Private).Invoke(s,new object[]{outcome});Console.WriteLine("settlement="+outcome+" earnedAncient=1 retainedAncient="+s.Cargo.AncientMaterial+" earnedWood=1 retainedWood="+s.Cargo.Wood);}}
 public static void Main(){Console.WriteLine("Current production Core probe; isolated fixture uses reflection only to enter sampled events, no player save access.");Revisit("event_moon_relic_diver",1);Revisit("event_moon_v2_ossuary_buoy",0);OneUnitSettlement();}
}
