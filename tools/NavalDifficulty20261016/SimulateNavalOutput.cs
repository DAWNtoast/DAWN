using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;
internal static partial class SimulateNaval
{
 static void Observe(Result r,CoreVoyageSession s,int before){r.Damage+=Math.Max(0,before-s.Hull);r.Repair+=Math.Max(0,s.Hull-before);r.MinimumHull=Math.Min(r.MinimumHull,s.Hull);}
 static void Trace(CoreVoyageSession s,Result r,string action,string detail,int hp,int enemyHp){var b=s.Battle;if(b==null){Actions.WriteLine(Csv(r.Enemy,r.Seed,r.Turns,s.Phase,action,detail,hp,s.Hull,enemyHp,"terminal","","","","","","","","",s.Cargo.Powder,s.Cargo.Cannonballs,s.Cargo.Wood,""));return;}Actions.WriteLine(Csv(r.Enemy,r.Seed,r.Turns,b.Stage,action,detail,hp,s.Hull,enemyHp,b.EnemyHull,b.PlayerPositionMeters.X,b.PlayerPositionMeters.Y,b.EnemyPositionMeters.X,b.EnemyPositionMeters.Y,b.PlayerHeadingDegrees,CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters),b.NavalActionPoints,b.Energy,s.Cargo.Powder,s.Cargo.Cannonballs,s.Cargo.Wood,s.GetEnemyIntent()));}
 static void Export(string output,CoreVoyageSession s)
 {
  using(var f=Writer(output,"deck.csv")){f.WriteLine("stage,id,name,copies,upgrade,maxUpgrade,source,energy,damage,block,repair,actionPoints");foreach(var stage in new[]{NavalBattleStage.Preparation,NavalBattleStage.Naval})foreach(var g in s.GetBattleDeck(stage).Concat(s.GetBonusBattleCards(stage)).GroupBy(i=>i)){var c=s.GetBattleCard(g.Key);var fx=s.GetBattleCardEffects(c,stage);f.WriteLine(Csv(stage,c.Id,c.Name,g.Count(),c.UpgradeLevel,s.GetCardMaximumUpgradeLevel(c.Id),CoreVoyageSession.IsSpecialBattleCard(c.Id),c.Energy,fx.Damage,fx.Block,fx.Repair,c.NavalActionPointGain));}}
  File.WriteAllText(Path.Combine(output,"fixture.txt"),"build="+Build+"\nship="+s.SelectedShip.Id+"\nhull="+s.Hull+"\nordinary="+s.SelectedOrdinaryCrew+"\nspecial="+s.SelectedSpecialCrewCount+"\npowder="+s.Cargo.Powder+"\nballs="+s.Cargo.Cannonballs+"\nwood="+s.Cargo.Wood+"\ncargoVolume="+s.Cargo.UsedVolume+"\ncargoCapacity="+s.CargoCapacityValue+"\nEquipment qualities 1; no components. StartBattle only skips navigation. Real preparation, geometry, resources and attacks. Boarding transition is not a naval win.\n");
 }
 static StreamWriter Writer(string output,string name)=>new StreamWriter(Path.Combine(output,name),false,new UTF8Encoding(false));
 static string F(double v)=>v.ToString("0.00",CultureInfo.InvariantCulture);
 static string Csv(params object[] cells)=>string.Join(",",cells.Select(c=>"\""+Convert.ToString(c,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\""));
}
