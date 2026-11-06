using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
internal static class VerifyCardChanges
{
 public sealed class Slot { public int Index; public string Id,Number,Stages; }
 public sealed class Rank { public int Level,FullRepair,FallbackRepair,NextUpgradeCost; public string Effect,Cost,Supply,Action,Mechanic,BoardingKey; public string[] Keywords; public Dictionary<string,int> Values; public bool RepairSupply; }
 public sealed class StageRow { public string Stage; public List<Rank> Ranks=new List<Rank>(); }
 public sealed class Card { public int Index; public string Id,Name,Number; public List<StageRow> Stages=new List<StageRow>(); }
 public sealed class Snapshot { public List<Slot> Slots=new List<Slot>(); public List<Card> Cards=new List<Card>(); }
 public sealed class Report { public string Status; public int Cards,Stages,Ranks,Assertions; public List<string> Failures=new List<string>(); public List<string> Changes=new List<string>(); }
 static readonly string[] Retired={"card_navigator_course","card_carpenter_shoring","card_doctor_sutures","card_gunner_precision","card_cook_rations","card_diver_sabotage","card_component_bow","card_component_sail","card_component_cannon","card_component_plating","card_component_instrument"};
 static readonly NavalBattleStage[] Stages={NavalBattleStage.Preparation,NavalBattleStage.Naval,NavalBattleStage.Boarding};
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
 static readonly MethodInfo CostMethod=typeof(CatalogResourceCosts).GetMethod("OtherAtStage",BindingFlags.NonPublic|BindingFlags.Static);
 static string Structure(string t){
  t=t??"";
  // Authored formula upgrades retain the expression and scale its result. Canonicalize only
  // this exact arithmetic wrapper, and the three existing thunder prose equivalents.
  t=Regex.Replace(t,@"\(([^()]+)\)×\d+(?:\.\d+)?（结果四舍五入）","$1");
  t=Regex.Replace(t,@"造成 (\d+) 点伤害，出牌前每有一层连锁，本次伤害提高 (\d+) 点","造成 $1+$2×出牌前连锁层数 点伤害");
  t=Regex.Replace(t,@"造成 (\d+) 点伤害 (\d+) 次，出牌前每有一层连锁，每次伤害提高 (\d+) 点","造成 $1+$3×出牌前连锁层数 点伤害 $2 次");
  t=Regex.Replace(t,@"造成其伤害的 (\d+)% 的伤害","造成该牌伤害比例 $1%");
  t=Regex.Replace(t,@"造成 向下取整的该牌伤害×(\d+)% 点伤害","造成该牌伤害比例 $1%");
  return Regex.Replace(Regex.Replace(t,@"\d+(?:\.\d+)?","#"),@"\s+","");
 }
 static bool Published(CoreBattleCard c)=>c.AllowedStages!=NavalBattleStages.None&&!CoreBattleCardAliases.IsAlias(c.Id)&&!(c.AllowedStages==NavalBattleStages.Boarding&&c.RequiredCrew.Length==0&&c.RequiredComponent.Length==0&&CoreBoardingCardCatalog.IsLegacyAlias(c));
 static CoreBattleCard Resolve(CoreBattleCard c,NavalBattleStage s)=>s==NavalBattleStage.Boarding?CoreBoardingCardCatalog.ForBoarding(c):c;
 static void Save(string p,object v)=>File.WriteAllText(p,Json.Serialize(v),new UTF8Encoding(false));
 static void Check(Report r,bool ok,string message){r.Assertions++;if(!ok)r.Failures.Add(message);}
 static Dictionary<string,int> Values(CoreBattleCard c){
  var v=new Dictionary<string,int>{{"Damage",c.Damage},{"Block",c.Block},{"Repair",c.Repair},{"Draw",c.Draw},{"Energy",c.Energy},{"PowderCost",c.PowderCost},{"CannonballCost",c.CannonballCost},{"BoardingBlock",c.BoardingBlock??c.Block}};
  if(c.SchoolEffect!=null){
   foreach(var p in c.SchoolEffect.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance))if(p.PropertyType==typeof(int)&&p.GetIndexParameters().Length==0)v["School."+p.Name]=(int)p.GetValue(c.SchoolEffect,null);
   foreach(var f in c.SchoolEffect.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))if(f.FieldType==typeof(int))v["School."+f.Name]=(int)f.GetValue(c.SchoolEffect);
  }return v;
 }
 static Snapshot Capture(){
  var result=new Snapshot();var session=new CoreVoyageSession();
  for(int i=0;i<CoreVoyageSession.CardLibrary.Count;i++){
   var original=CoreVoyageSession.CardLibrary[i];result.Slots.Add(new Slot{Index=i,Id=original.Id,Number=CoreBattleCardNumbering.Label(original),Stages=original.AllowedStages.ToString()});
   if(!Published(original))continue;var c=new Card{Index=i,Id=original.Id,Name=original.Name,Number=CoreBattleCardNumbering.Label(original)};
   foreach(var stage in Stages.Where(original.IsAllowedInStage)){
    var sr=new StageRow{Stage=stage.ToString()};var basis=Resolve(session.GetBattleCardAtLevel(i,0),stage);
    for(int level=0;level<=CoreBattleUpgradeBalance.MaximumLevelFor(basis);level++){
     var card=Resolve(session.GetBattleCardAtLevel(i,level),stage);string effect=CoreBattleCardText.RenderTemplate(card.DescriptionTemplate,card,stage);var quote=session.GetBattleCardEffects(card,stage);
     sr.Ranks.Add(new Rank{Level=level,FullRepair=quote.FullRepair,FallbackRepair=quote.FallbackRepair,NextUpgradeCost=level<CoreBattleUpgradeBalance.MaximumLevelFor(basis)?CoreBattleUpgradeBalance.CostForNextLevel(level):0,Effect=effect,Cost=(string)CostMethod.Invoke(null,new object[]{card,stage}),Supply=CardSupplyCostPresentation.ResourceType(card,stage),Action=card.Action.ToString(),Mechanic=card.SchoolEffect?.Mechanic.ToString()??"",BoardingKey=card.BoardingSpec?.Key??"",Keywords=CoreBattleCardKeywordText.Build(card,effect).Select(k=>k.Keyword).ToArray(),Values=Values(card),RepairSupply=quote.FullRepair>quote.FallbackRepair});
    }c.Stages.Add(sr);
   }result.Cards.Add(c);
  }return result;
 }
 static void Structures(Snapshot data,Report r){
  foreach(var c in data.Cards)foreach(var s in c.Stages){var b=s.Ranks[0];foreach(var v in s.Ranks){string key=c.Id+"/"+s.Stage+"/+"+v.Level;
   Check(r,Structure(b.Effect)==Structure(v.Effect),"Effect structure: "+key+" | "+b.Effect+" => "+v.Effect);
   Check(r,Structure(b.Cost)==Structure(v.Cost),"Cost structure: "+key+" | "+b.Cost+" => "+v.Cost);
   Check(r,v.Supply==b.Supply&&v.RepairSupply==b.RepairSupply,"Supply type/gate: "+key);
   Check(r,v.Action==b.Action&&v.Mechanic==b.Mechanic&&v.BoardingKey==b.BoardingKey,"Action/mechanic: "+key);
   Check(r,!v.Keywords.Except(b.Keywords).Any(),"New keyword: "+key);
   foreach(var value in v.Values)if(b.Values.TryGetValue(value.Key,out int baseValue)&&baseValue==0)Check(r,value.Value==0,"New zero-origin effect/cost: "+key+"/"+value.Key);
  }}
 }
 static int Main(string[] args){
  if(args.Length!=2)throw new ArgumentException("baseline|verify output-directory");string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);var current=Capture();
  Save(Path.Combine(output,args[0]=="baseline"?"before-snapshot.json":"after-snapshot.json"),current);
  var report=new Report{Cards=current.Cards.Count,Stages=current.Cards.Sum(c=>c.Stages.Count),Ranks=current.Cards.Sum(c=>c.Stages.Sum(s=>s.Ranks.Count))};Structures(current,report);
  if(args[0]=="baseline"){report.Status="baseline";Save(Path.Combine(output,"before-structure-audit.json"),report);Console.WriteLine("Baseline: "+current.Slots.Count+" slots; "+report.Cards+" cards; "+report.Ranks+" rows; "+report.Failures.Count+" existing structural differences.");return 0;}
  var before=Json.Deserialize<Snapshot>(File.ReadAllText(Path.Combine(output,"before-snapshot.json"),Encoding.UTF8));VerifyApprovedChanges(before,current,report);
  report.Status=report.Failures.Count==0?"passed":"failed";Save(Path.Combine(output,"runtime-validation.json"),report);
  Console.WriteLine(report.Status+": "+report.Cards+" cards; "+report.Stages+" stages; "+report.Ranks+" rows; "+report.Assertions+" assertions; "+report.Failures.Count+" failures.");foreach(var f in report.Failures)Console.WriteLine(f);return report.Failures.Count==0?0:1;
 }
 static void Approved(Report r,string id,string summary,Action check){int count=r.Failures.Count;check();r.Changes.Add((r.Failures.Count==count?"PASS ":"FAIL ")+id+": "+summary);}
 static void Field(Report r,Rank row,string id,string field,int expected){Check(r,row.Values.ContainsKey(field)&&row.Values[field]==expected,id+"/+"+row.Level+" "+field+" expected "+expected);}
 static void FreeHeal(Report r,Rank row,string id,int expected){Field(r,row,id,"Repair",expected);Check(r,row.FullRepair==expected&&row.FallbackRepair==expected&&!row.RepairSupply&&row.Supply=="",id+"/+"+row.Level+" free repair execution and icon expected "+expected);Check(r,!row.Effect.Contains("消耗")&&!row.Cost.Contains("木板")&&!row.Cost.Contains("药"),id+"/+"+row.Level+" adds a supply cost");}
 static readonly string[] FreeCrew={"card_special_crew_bronze_bell_cook_basin","card_special_crew_salt_crown_carpenter_saf","card_special_crew_gray_gull_cook_pepe","card_special_crew_broken_anchor_carpenter_kia"};
 static readonly string[] Plating={"card_special_plating_merchants_oath","card_special_plating_gilded_bulwark","card_special_plating_suncoin_rampart","card_special_plating_redbanner_wall"};
 static readonly string[] Curse={"card_school_curse_b01","card_school_curse_b02"};
 static readonly string[] Split={"card_energy_order","card_tactical_draw"};
 static bool AllowedNumberChange(string id,int level,string field){
  if(FreeCrew.Contains(id)&&field=="Repair")return true;
  if(id=="card_boarding_basic_08"&&level==2&&field=="Draw")return true;
  if(Plating.Contains(id)&&level>0&&(field=="Repair"||id==Plating[3]&&(field=="Block"||field=="BoardingBlock")))return true;
  return Curse.Contains(id)&&field=="School.Power";
 }
 static void VerifyApprovedChanges(Snapshot before,Snapshot after,Report r){
  Check(r,before.Slots.Count==after.Slots.Count,"Saved library slot count changed");
  foreach(var prior in before.Slots){var now=after.Slots.FirstOrDefault(s=>s.Index==prior.Index);Check(r,now!=null&&now.Id==prior.Id,"Saved slot/ID changed: "+prior.Index+"/"+prior.Id);if(now!=null&&!Retired.Contains(prior.Id)&&prior.Id!="card_boarding_basic_08"&&prior.Id!="card_boarding_basic_09")Check(r,now.Number==prior.Number,"Unrelated card number changed: "+prior.Id);}
  Check(r,after.Cards.Count==before.Cards.Count-Retired.Length,"Published count expected baseline minus 11 (boarding counterparts already published)");
  Check(r,after.Cards.Select(c=>c.Id).Distinct().Count()==after.Cards.Count,"Duplicate published card IDs");
  foreach(var prior in before.Cards.Where(c=>!Retired.Contains(c.Id))){
   var now=after.Cards.FirstOrDefault(c=>c.Id==prior.Id);Check(r,now!=null,"Unexpected removed card: "+prior.Id);if(now==null)continue;
   Check(r,now.Name==prior.Name&&now.Number==prior.Number,"Unapproved identity change: "+prior.Id);
   var expectedStages=prior.Stages.Select(s=>s.Stage).Where(s=>!(Split.Contains(prior.Id)&&s=="Boarding"));Check(r,expectedStages.SequenceEqual(now.Stages.Select(s=>s.Stage)),"Unapproved stage change: "+prior.Id);
   foreach(var ps in prior.Stages){var ns=now.Stages.FirstOrDefault(s=>s.Stage==ps.Stage);if(ns==null)continue;Check(r,ns.Ranks.Count==ps.Ranks.Count+(prior.Id=="card_boarding_basic_08"?1:0),"Unapproved max rank change: "+prior.Id+"/"+ps.Stage);
    foreach(var pr in ps.Ranks){var nr=ns.Ranks.FirstOrDefault(v=>v.Level==pr.Level);if(nr==null)continue;string key=prior.Id+"/"+ps.Stage+"/+"+pr.Level;
     Check(r,nr.NextUpgradeCost==(prior.Id=="card_boarding_basic_08"&&pr.Level==2?480:pr.NextUpgradeCost),"Upgrade price changed: "+key);
     Check(r,nr.Action==pr.Action&&nr.Mechanic==pr.Mechanic&&nr.BoardingKey==pr.BoardingKey&&nr.Keywords.SequenceEqual(pr.Keywords),"Unapproved mechanism/keywords change: "+key);
     foreach(var value in pr.Values)if(!AllowedNumberChange(prior.Id,pr.Level,value.Key))Check(r,nr.Values.ContainsKey(value.Key)&&nr.Values[value.Key]==value.Value,"Unapproved numeric change: "+key+"/"+value.Key);
     string expectedEffect=pr.Effect;
     if(FreeCrew.Contains(prior.Id)||Plating.Contains(prior.Id)&&pr.Level>0){
      expectedEffect=Regex.Replace(expectedEffect,@"(修复|恢复) \d+ 点(船体|生命值)(?:；\s*消耗 \d+ (?:木板|药物)额外(?:修复|恢复) \d+ 点(?:船体|生命值))?",m=>m.Groups[1].Value+" "+nr.Values["Repair"]+" 点"+m.Groups[2].Value);
      if(prior.Id==Plating[3])expectedEffect=Regex.Replace(expectedEffect,@"获得 \d+ 点格挡","获得 "+nr.Values["Block"]+" 点格挡");
     }
     if(Curse.Contains(prior.Id))expectedEffect=Regex.Replace(expectedEffect,@"施加 \d+ 层咒印","施加 "+nr.Values["School.Power"]+" 层咒印");
     if(prior.Id=="card_full_sail_turn")expectedEffect=Regex.Replace(expectedEffect,@"获得\s*0\s*点格挡[；。]?\s*","");
     if(prior.Id=="card_boarding_basic_10")expectedEffect="获得 "+new[]{3,4,6}[pr.Level]+" 点能量。";
     if(prior.Id=="card_boarding_basic_08")expectedEffect="抽 "+new[]{3,4,6}[pr.Level]+" 张牌。";
     Check(r,nr.Effect==expectedEffect,"Unapproved effect change: "+key+" | "+expectedEffect+" => "+nr.Effect);
     if(!FreeCrew.Contains(prior.Id)&&!(Plating.Contains(prior.Id)&&pr.Level>0))Check(r,nr.Cost==pr.Cost,"Unapproved resource-cost change: "+key);
     if(pr.Level==0){
      bool free=FreeCrew.Contains(prior.Id),curse=Curse.Contains(prior.Id),sail=prior.Id=="card_full_sail_turn";
      if(!free)Check(r,nr.Cost==pr.Cost&&nr.Supply==pr.Supply&&nr.RepairSupply==pr.RepairSupply,"Unapproved base resource change: "+key);
      if(!free&&!curse&&!sail)Check(r,nr.Effect==pr.Effect&&nr.FullRepair==pr.FullRepair&&nr.FallbackRepair==pr.FallbackRepair,"Unapproved base effect change: "+key);
      if(curse)Check(r,Structure(nr.Effect)==Structure(pr.Effect),"Curse base changed nonnumeric effect: "+key);
     }
    }
   }
  }
  var session=new CoreVoyageSession();session.ConfigureCrewCapabilities(true,true,true,true,true,true);session.ConfigureComponentBattleCards(new[]{"bow","sail","cannon","plating","instrument"});
  for(int ri=0;ri<Retired.Length;ri++){string id=Retired[ri];Approved(r,id,"删除旧通用牌；保留旧槽位和经验迁移",()=>{
   var slot=after.Slots.Single(s=>s.Id==id);Check(r,slot.Stages=="None"&&!after.Cards.Any(c=>c.Id==id),"Retired remains published: "+id);Check(r,session.GetBattleCardAtLevel(slot.Index,0)==null&&session.GetBattleCardAtLevel(slot.Index,3)==null,"Retired definition still playable: "+id);
   foreach(var stage in Stages)Check(r,!session.GetAvailableBattleCards(stage).Contains(slot.Index)&&!session.GetBonusBattleCards(stage).Contains(slot.Index),"Retired remains obtainable: "+id+"/"+stage);
   string legacy=new[]{"navigator","carpenter","doctor","gunner","cook","diver","bow","sail","cannon","plating","instrument"}[ri];bool crew=ri<6;
   var current=CoreSpecialCardCatalog.All.First(d=>d.LegacySourceKey==legacy&&d.SourceKind==(crew?CoreSpecialCardSourceKind.Crew:CoreSpecialCardSourceKind.Component));
   foreach(bool configureFirst in new[]{false,true}){var saved=new CoreVoyageSession();if(configureFirst)saved.ConfigureSpecialCardSources(crew?new[]{current.SourceId}:null,crew?null:new[]{current.SourceId});saved.RestoreSpecialCardExperience(new Dictionary<string,int>{{id,360}});saved.ConfigureCardUpgrades(new Dictionary<string,int>{{id,3}});if(!configureFirst)saved.ConfigureSpecialCardSources(crew?new[]{current.SourceId}:null,crew?null:new[]{current.SourceId});Check(r,saved.GetSpecialCardExperience(id)==840&&saved.GetSpecialCardExperience(current.Card.Id)==840&&!saved.AcquiredSpecialCardIds.Contains(id),"Legacy XP/paid rank migration failed: "+id+"/"+configureFirst);}
  });}
  for(int pi=0;pi<Plating.Length;pi++){string id=Plating[pi];int p=pi;Approved(r,id,"装甲升级按指定格挡/抽牌，免费修复",()=>{var card=after.Cards.Single(c=>c.Id==id);var rows=card.Stages.Single(s=>s.Stage=="Naval").Ranks;for(int level=1;level<rows.Count;level++){var row=rows[level];Field(r,row,id,"Block",new[]{10,18,6,7}[p]);Field(r,row,id,"Draw",p==0||p==3?1:0);FreeHeal(r,row,id,p==0?4:p==3&&level==2?8:5);if(p==2)Check(r,row.Effect.Contains("额外格挡：3"),id+" extra block expected 3");}});}
  for(int fi=0;fi<FreeCrew.Length;fi++){string id=FreeCrew[fi];Approved(r,id,"所有可用阶段免费恢复 4/6/8 或 4/6，无木板/药物",()=>{var c=after.Cards.Single(x=>x.Id==id);int count=id==FreeCrew[0]||id==FreeCrew[3]?3:2;foreach(var s in c.Stages){Check(r,s.Ranks.Count==count,"Free heal upgrade cap: "+id);foreach(var row in s.Ranks)FreeHeal(r,row,id,new[]{4,6,8}[row.Level]);}});}
  foreach(string id in Curse)Approved(r,id,"咒印基础/+1/+2 = 2/5/8，其余效果保持",()=>{var c=after.Cards.Single(x=>x.Id==id);foreach(var s in c.Stages){Check(r,s.Ranks.Count==3,id+" expected +2 cap");foreach(var row in s.Ranks){Field(r,row,id,"School.Power",new[]{2,5,8}[row.Level]);Check(r,row.Effect.Contains("施加 "+new[]{2,5,8}[row.Level]+" 层咒印"),id+" curse text mismatch");}}});
  Approved(r,"card_boarding_basic_10","获得能量基础/+1/+2 = 3/4/6",()=>{var rows=after.Cards.Single(c=>c.Id=="card_boarding_basic_10").Stages.Single().Ranks;Check(r,rows.Count==3,"basic_10 max rank");foreach(var row in rows)Check(r,row.Effect=="获得 "+new[]{3,4,6}[row.Level]+" 点能量。","basic_10 energy curve: +"+row.Level);});
  Approved(r,"card_boarding_basic_08","独立接舷牌可强化+3；抽牌基础/+1/+2/+3 = 3/4/6/7",()=>{var rows=after.Cards.Single(c=>c.Id=="card_boarding_basic_08").Stages.Single().Ranks;Check(r,rows.Count==4,"basic_08 max rank");foreach(var row in rows){Field(r,row,"card_boarding_basic_08","Draw",new[]{3,4,6,7}[row.Level]);Check(r,row.Effect=="抽 "+new[]{3,4,6,7}[row.Level]+" 张牌。","basic_08 draw text: +"+row.Level);}var previous=before.Cards.Single(c=>c.Id=="card_tactical_draw").Stages.Single(s=>s.Stage=="Boarding").Ranks[0];Check(r,Json.Serialize(rows[0])==Json.Serialize(previous),"Split boarding draw base changed");});
  for(int si=0;si<Split.Length;si++){string id=Split[si],boarding=si==0?"card_boarding_basic_09":"card_boarding_basic_08";Approved(r,id,"准备/海战与接舷拆为两张牌、分别强化",()=>{var ship=after.Cards.Single(c=>c.Id==id);var board=after.Cards.Single(c=>c.Id==boarding);Check(r,ship.Stages.All(s=>s.Stage!="Boarding")&&board.Stages.Count==1&&board.Stages[0].Stage=="Boarding","Split stages: "+id);Check(r,ship.Number!=board.Number&&ship.Index!=board.Index,"Split identities: "+id);var independent=new CoreVoyageSession();independent.ConfigureCardUpgrades(new Dictionary<string,int>{{id,0},{boarding,1}});Check(r,independent.GetCardUpgradeLevel(id)==0&&independent.GetCardUpgradeLevel(boarding)==1,"Split boarding upgrade independence: "+id);});}
  Approved(r,"card_full_sail_turn","删除无实际效果的“获得0点格挡”显示",()=>{var c=after.Cards.Single(x=>x.Id=="card_full_sail_turn");foreach(var s in c.Stages)foreach(var row in s.Ranks){Check(r,!Regex.IsMatch(row.Effect,@"获得\s*0\s*点格挡"),"Zero block remains: "+s.Stage+"/+"+row.Level);var old=before.Cards.Single(x=>x.Id==c.Id).Stages.Single(x=>x.Stage==s.Stage).Ranks[row.Level];string expected=Regex.Replace(old.Effect,@"获得\s*0\s*点格挡[；。]?\s*","");Check(r,row.Effect==expected,"Sail turn changed beyond zero-block text");}});
  Check(r,r.Changes.Count==26,"Expected 26 individually audited user suggestions");
 }
}
