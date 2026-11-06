using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;
using ShatteredSail.Content;
using ShatteredSail.Presentation;
// Explicit constructed battle fixtures, not new-game acquisition or progression.
public static partial class BattleModes
{
 static string output;static int maxTurns;static ComponentArtCatalog catalog;
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=100};
 static readonly Dictionary<string,int> Cards=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).ToDictionary(i=>CoreVoyageSession.CardLibrary[i].Id,i=>i);
 static readonly List<object> loadouts=new List<object>();static readonly List<Result> results=new List<Result>();
 static string[] Pair(params string[] ids)=>ids.SelectMany(x=>new[]{x,x}).ToArray();
 static readonly string[] Contact=Pair("card_approach","card_full_sail_turn","card_emergency_repair","card_brace","card_energy_order");
 static readonly string[] Neutral=Pair("card_boarding_basic_02","card_boarding_basic_05","card_boarding_basic_06","card_boarding_basic_09","card_boarding_basic_13");
 static readonly string[] Curse=Pair("card_boarding_curse_10","card_boarding_basic_09","card_boarding_basic_05","card_boarding_tide_02","card_boarding_basic_06");
 static readonly string[] CannonPrep=Pair("card_load_shot","card_deck_polish","card_energy_order","card_tactical_draw","card_brace");
 sealed class Cohort{public string Id,Ship;public int Level,Rank,Budget,SpecialXP;public string[] Parts,Crew;}
 static readonly Cohort[] Cohorts={
 new Cohort{Id="starter",Ship="sloop",Level=1,Rank=0,Budget=0,SpecialXP=0,Parts=new string[0],Crew=new string[0]},
 new Cohort{Id="patrol",Ship="patrol",Level=5,Rank=1,Budget=720,SpecialXP=120,Parts=new[]{"bow_sunwake_crest","sail_sunthread_spinnaker","cannon_anchorbreaker","plating_coralsteel_hide","instrument_tradeledger_oracle"},Crew=new[]{"crew_broken_anchor_navigator_lorne","crew_white_shell_doctor_mirei"}},
 new Cohort{Id="gunship",Ship="gunship",Level=10,Rank=3,Budget=2400,SpecialXP=840,Parts=new[]{"bow_freewake_lance","sail_tempest_wing","cannon_worlds_end_volley","plating_worldtree_bark","instrument_worldcompass"},Crew=new[]{"crew_broken_anchor_navigator_lorne","crew_white_shell_doctor_mirei","crew_white_shell_cook_flora"}}};
 public sealed class Result{
 public string cohort,mode,boss,name,outcome,trace;public int seed,budget,spent,maxHull,initialHull,minimumHull,finalHull,playerMaxCrew,minimumCrew,finalCrew,enemyMaxHull,enemyFinalHull,enemyMaxCrew,enemyFinalCrew,prepTurns,navalTurns,boardingTurns,cards,movementSteps,powder,balls,wood,medicine,boardingEntryNavalTurn=-1,boardingEntryHull,boardingEntryEnemyHull,boardingEntryCrew,boardingEntryEnemyCrew,carryover,startPlayerHull,startEnemyHull,ownWindowHullLoss,enemyWindowHullLoss,ownWindowCrewLoss,enemyWindowCrewLoss,movementFieldDamage,boardingEntryPowder,boardingEntryBalls,boardingEntryWood,boardingEntryMedicine;public bool supplyExhausted,won,achievedMode;public decimal replacementCost;public object initial;}
 public static int Main(string[] args){
 CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;output=args[0];int seeds=int.Parse(args[1]);maxTurns=int.Parse(args[2]);UnityEngine.Resources.Root=args[3];catalog=new ComponentArtCatalog();if(args[5]=="diagnostic")return Diagnostic();if(args[5]=="sensitivity"){foreach(string id in new[]{"boss_route_shattered_sail_reef","boss_route_shattered_sail_reef_tide"})for(int seed=0;seed<8;seed++)results.Add(Run(Cohorts[0],"cannon-starter10",CoreRouteBossCatalog.Find(id).CreateEncounter(),seed));Save();return 0;}if(args[5]=="smoke"){results.Add(Run(Cohorts[0],"cannon",CoreRouteBossCatalog.Find("boss_route_shattered_sail_reef").CreateEncounter(),1));results.Add(Run(Cohorts[2],"cannon",CoreRouteBossCatalog.Find("boss_route_moon_tide_ruins").CreateEncounter(),6));Save();return 0;}
 var scenarios=CoreRouteBossCatalog.All.Select(x=>x.CreateEncounter()).Concat(CoreLegendaryWarships.All.Select(x=>x.CreateEncounter())).Concat(new[]{CoreLegendaryWarships.CreateTreasureEncounter()}).ToArray();
 foreach(var cohort in Cohorts.Where(x=>args[4].Split(',').Contains(x.Id)))foreach(var mode in args[5].Split(',')){
 if((mode=="boarding-curse"||mode=="cannon-tide")&&cohort.Level<5)continue;
 foreach(var enemy in scenarios.Concat(new[]{Ordinary(cohort)})){for(int seed=0;seed<seeds;seed++)results.Add(Run(cohort,mode,enemy,seed));Console.WriteLine(cohort.Id+" "+mode+" "+enemy.Id+" "+results.Count);Save();}}
 Save();return 0;}
 static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
 static string Cell(object x)=>"\""+Convert.ToString(x,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";
 static void Save(){File.WriteAllText(Path.Combine(output,"samples.json"),Json.Serialize(results),new UTF8Encoding(false));File.WriteAllText(Path.Combine(output,"loadouts.json"),Json.Serialize(loadouts),new UTF8Encoding(false));var f=typeof(Result).GetFields().Where(x=>x.Name!="initial").ToArray();File.WriteAllLines(Path.Combine(output,"samples.csv"),new[]{string.Join(",",f.Select(x=>Cell(x.Name)))}.Concat(results.Select(r=>string.Join(",",f.Select(x=>Cell(x.GetValue(r)))))),new UTF8Encoding(true));}
 static CoreVoyageSession Create(Cohort c,string mode,int seed,int route,out object initial,out int spent){
 var s=new CoreVoyageSession();s.RestoreProfile(100000,"断锚港");s.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=CoreVoyageSession.PirateExperienceForLevel(c.Level)});
 int ship=Enumerable.Range(0,CoreVoyageSession.Ships.Count).Single(i=>CoreVoyageSession.Ships[i].Id==c.Ship);Need(s.SelectShip(ship),"fixture ship");
 bool[] professions=new bool[6];foreach(string id in c.Crew)professions[CrewNarrativeCatalog.FindById(id).ProfessionIndex]=true;s.ConfigureCrewCapabilities(professions[0],professions[1],professions[2],professions[3],professions[4],professions[5]);s.ConfigureDepartureCrew(s.CrewCountValue-c.Crew.Length,c.Crew.Length);
 var parts=c.Parts.Select(catalog.GetById).ToArray();Need(parts.All(x=>x!=null),"parts");s.ConfigureComponentBonuses(parts.Sum(x=>x.HullBonus-catalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).HullBonus),parts.Sum(x=>x.ArmorBonus-catalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).ArmorBonus),parts.Sum(x=>x.FirepowerBonus-catalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).FirepowerBonus),parts.Sum(x=>x.SpeedBonus-catalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).SpeedBonus),0);
 float quality(ComponentSlot slot){var p=parts.FirstOrDefault(x=>x.Slot==slot);return p==null?0:1+(int)p.Rarity;}s.ConfigureBattleEquipment(quality(ComponentSlot.Bow),quality(ComponentSlot.Plating));s.ConfigureComponentContextEffect(0,0,0,0,0,1);
 s.ConfigureSpecialCardSources(c.Crew,parts.Where(x=>x.Rarity>=ComponentRarity.Rare).Select(x=>x.Id).ToArray());var sourceIds=c.Crew.Select(x=>CoreSpecialCardCatalog.ForCrew(x)?.Card.Id).Concat(parts.Select(x=>CoreSpecialCardCatalog.ForComponent(x.Id)?.Card.Id)).Where(x=>x!=null).Distinct();s.RestoreSpecialCardExperience(sourceIds.ToDictionary(x=>x,x=>c.SpecialXP));
 bool cannon=mode.StartsWith("cannon");string[] naval=mode=="cannon-starter10"?new[]{"card_cannon_barrage","card_cannon_barrage","card_chain_shot","card_chain_shot","card_energy_order","card_energy_order","card_brace","card_emergency_repair","card_load_shot","card_tactical_draw"}:mode=="cannon-tide"?NavalBalanceProbe.MatureDecks.Single(x=>x.Name=="tide").Ids:cannon?NavalBalanceProbe.Decks.Single(x=>x.Name=="ordinary").Ids:Contact;
 string[] prep=mode=="cannon-tide"?Pair("card_school_tide_b01","card_school_tide_b02","card_energy_order","card_tactical_draw","card_brace"):cannon?CannonPrep:Contact;string[] boarding=mode=="boarding-curse"?Curse:Neutral;
 Need(s.SetBattleDeck(NavalBattleStage.Preparation,prep.Select(x=>Cards[x]).ToArray()),"preparation rejected "+mode);Need(s.SetBattleDeck(NavalBattleStage.Naval,naval.Select(x=>Cards[x]).ToArray()),"naval rejected "+mode);Need(s.SetBattleDeck(NavalBattleStage.Boarding,boarding.Select(x=>Cards[x]).ToArray()),"boarding rejected "+mode);
 var upgradeOrder=(cannon?naval.Concat(prep):boarding.Concat(naval)).Distinct().ToArray();spent=Upgrade(s,upgradeOrder,c);
 if(route>=0){s.SelectRoute(route);Need(s.SelectedRouteIndex==route,"ordinary route eligibility");}s.SetSeed("BATTLE-MODE-PAIRED-"+seed);s.StartVoyage();int ammo=c.Id=="starter"?24:c.Id=="patrol"?48:80,medical=c.Id=="starter"?8:c.Id=="patrol"?16:24;s.Cargo.Powder=ammo;s.Cargo.Cannonballs=ammo;s.Cargo.Wood=medical;s.Cargo.Medicine=medical;Need(s.Cargo.UsedVolume<=s.CargoCapacityValue,"fixture cargo capacity");typeof(CoreVoyageSession).GetField("voyageRandom",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new Mulberry32Random(SeedHash.Compute("BATTLE-MODE-PAIRED-"+seed)));
 initial=new {cohort=c.Id,mode,seed,ship=c.Ship,level=c.Level,budget=c.Budget,spent,hull=s.Hull,maxHull=s.MaxHull,armor=s.EffectiveArmor,firepower=s.EffectiveFirepower,ordinaryCrew=s.SelectedOrdinaryCrew,crew=c.Crew,components=parts.Select(x=>new{id=x.Id,name=x.Name,rarity=x.Rarity.ToString()}).ToArray(),environment="neutral-context-disabled",noRetreatForVictory=true,route,specialXP=c.SpecialXP,preparation=prep,naval,boarding,upgrades=upgradeOrder.ToDictionary(x=>x,x=>s.GetCardUpgradeLevel(x)),powder=ammo,balls=ammo,wood=medical,medicine=medical,cargoVolume=s.Cargo.UsedVolume,capacity=s.CargoCapacityValue};if(seed==0)loadouts.Add(initial);return s;}
}
public static partial class BattleModes
{
 static Result Run(Cohort c,string mode,CoreEncounter enemy,int seed){
 bool isBoss=enemy.BossKind!=CoreBossKind.None;int route=isBoss?-1:c.Id=="starter"?0:c.Id=="patrol"?1:2;var s=Create(c,mode,seed,route,out object initial,out int spent);typeof(CoreVoyageSession).GetMethod("StartBattle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{enemy,isBoss});var b=s.Battle;Need(b.Stage==NavalBattleStage.Preparation&&s.NavalDistanceCells==6,"normal 600m preparation entry");
 var r=new Result{cohort=c.Id,mode=mode,boss=enemy.Id,name=enemy.Name,seed=seed,budget=c.Budget,spent=spent,initial=initial,maxHull=s.MaxHull,initialHull=s.Hull,minimumHull=s.Hull,playerMaxCrew=b.PlayerMaxBoardingHealth,minimumCrew=b.PlayerBoardingHealth,enemyMaxHull=b.EnemyMaxHull,enemyMaxCrew=b.EnemyMaxBoardingHealth,startPlayerHull=b.NavalStartPlayerHull,startEnemyHull=b.NavalStartEnemyHull};
 bool cannon=mode.StartsWith("cannon");var trace=new List<string>();
 for(int action=0;action<3000&&s.Phase==CoreDemoPhase.Battle;action++){
 if(r.navalTurns+r.boardingTurns>=maxTurns){r.outcome="timeout";break;}
 if(cannon&&b.Stage==NavalBattleStage.Boarding){if(r.boardingEntryNavalTurn<0)Entry(r,s,b);r.outcome="forced_boarding";break;}
 int hp=s.Hull,ch=b.PlayerBoardingHealth;bool ended=false;string decision="";
 if(!cannon&&b.Stage==NavalBattleStage.Naval&&(s.CanRam(out _)||s.CanBoard(out _))){if(s.CanBoard(out _)){Need(s.TryBoard(),"board");decision="board";}else{Need(s.TryRam(),"ram");decision="ram";}}
 else{
 int slot=b.Stage==NavalBattleStage.Boarding?ChooseContact(s):cannon?(b.Stage==NavalBattleStage.Preparation?NavalBalanceProbe.ChoosePreparation(s):ChooseCannon(s)):ChooseContact(s);
 if(slot>=0){var card=s.GetBattleCard(b.Hand[slot]);decision=card.Id;int p=s.Cargo.Powder,a=s.Cargo.Cannonballs,w=s.Cargo.Wood,m=s.Cargo.Medicine;Need(NavalBalanceProbe.PlayChosen(s,slot),"card "+card.Id);r.powder+=Math.Max(0,p-s.Cargo.Powder);r.balls+=Math.Max(0,a-s.Cargo.Cannonballs);r.wood+=Math.Max(0,w-s.Cargo.Wood);r.medicine+=Math.Max(0,m-s.Cargo.Medicine);r.cards++;}
 else if(b.Stage!=NavalBattleStage.Boarding&&Move(s,cannon,r))decision="move";
 else {if(diagnostic&&b.Stage==NavalBattleStage.Naval&&r.navalTurns==5)BeforeDiagnosticEnd(s);var stage=b.Stage;int remaining=b.Energy,ap=b.NavalActionPoints,available=Enumerable.Range(0,b.Hand.Count).Count(i=>s.CanPlayCard(i)),intent=CoreBattleIntentPresentation.Build(s).Where(x=>x.IsAttack).Sum(x=>x.Amount??0);Need(s.EndBattleTurn(),"end");ended=true;decision="end energy="+remaining+" ap="+ap+" legalCards="+available+" intent="+intent;if(stage==NavalBattleStage.Preparation)r.prepTurns++;else if(stage==NavalBattleStage.Naval)r.navalTurns++;else r.boardingTurns++;}}
 int hl=Math.Max(0,hp-s.Hull),cl=Math.Max(0,ch-b.PlayerBoardingHealth);if(ended){r.enemyWindowHullLoss+=hl;r.enemyWindowCrewLoss+=cl;}else{r.ownWindowHullLoss+=hl;r.ownWindowCrewLoss+=cl;}
 r.minimumHull=Math.Min(r.minimumHull,s.Hull);r.minimumCrew=Math.Min(r.minimumCrew,b.PlayerBoardingHealth);
 if(trace.Count<400)trace.Add(b.Stage+":"+decision+":"+s.Hull+"/"+b.PlayerBoardingHealth+":"+b.EnemyHull+"/"+b.EnemyBoardingHealth);
 if(b.Stage==NavalBattleStage.Boarding&&r.boardingEntryNavalTurn<0)Entry(r,s,b);if(diagnostic&&ended&&r.navalTurns==6){r.outcome=b.Stage==NavalBattleStage.Naval&&s.Hull>0?"diagnostic_survived_naval":b.Stage==NavalBattleStage.Boarding?"forced_boarding":"dead";break;}}
 r.finalHull=s.Hull;r.finalCrew=b.PlayerBoardingHealth;r.enemyFinalHull=b.EnemyHull;r.enemyFinalCrew=b.EnemyBoardingHealth;r.supplyExhausted=s.Cargo.Powder==0||s.Cargo.Cannonballs==0||s.Cargo.Medicine==0;
 if(r.outcome==null)r.outcome=s.Hull<=0||b.PlayerBoardingHealth<=0?"dead":b.EnemyHull<=0?"sunk":b.Stage==NavalBattleStage.Boarding&&b.EnemyBoardingHealth<=0?"captured":s.Phase!=CoreDemoPhase.Battle?"ended":"action_watchdog";
 r.won=r.outcome=="sunk"||r.outcome=="captured";r.achievedMode=cannon?r.outcome=="sunk":r.outcome=="captured";r.replacementCost=Price(PortMarketItem.Powder,r.powder)+Price(PortMarketItem.Cannonballs,r.balls)+Price(PortMarketItem.Wood,r.wood)+Price(PortMarketItem.Medicine,r.medicine);r.trace=string.Join(";",trace);return r;}
 static decimal Price(PortMarketItem item,int quantity)=>PortMarketCatalog.CalculateBuyCost("断锚港",item,CoreVoyageSession.NewCampaignStartDate(new DateTime(2026,10,27)),quantity,0);
 static void Entry(Result r,CoreVoyageSession s,CoreBattleState b){r.boardingEntryNavalTurn=r.navalTurns;r.boardingEntryHull=s.Hull;r.boardingEntryEnemyHull=b.EnemyHull;r.boardingEntryCrew=b.PlayerBoardingHealth;r.boardingEntryEnemyCrew=b.EnemyBoardingHealth;r.carryover=b.TrainingBoardingCarryover;r.boardingEntryPowder=s.Cargo.Powder;r.boardingEntryBalls=s.Cargo.Cannonballs;r.boardingEntryWood=s.Cargo.Wood;r.boardingEntryMedicine=s.Cargo.Medicine;}
 static int ChooseContact(CoreVoyageSession s){int best=-1;double value=.01;for(int h=0;h<s.Battle.Hand.Count;h++)if(s.CanPlayCard(h)){if(s.GetNavalCardChoice(h)?.RequiresChoice==true&&NavalBalanceProbe.ChooseOptions(s,h)==null)continue;double v=ContactScore(s,s.Battle.Hand[h]);if(v>value){value=v;best=h;}}return best;}
 static double ContactScore(CoreVoyageSession s,int ix){
 var c=s.GetBattleCard(ix);var b=s.Battle;var e=s.GetBattleCardEffects(c,b.Stage);bool board=b.Stage==NavalBattleStage.Boarding,prep=b.Stage==NavalBattleStage.Preparation;
 if(c.BoardingSpec?.Key=="curse_10")return 1000;if(c.Action==NavalBattleCardAction.Energize||c.BoardingSpec?.Key=="basic_09")return 100;
 int enemy=board?b.EnemyBoardingHealth:b.EnemyHull,hp=board?b.PlayerBoardingHealth:s.Hull,max=board?b.PlayerMaxBoardingHealth:s.MaxHull;int attack=CoreBattleIntentPresentation.Build(s).Where(x=>x.IsAttack).Sum(x=>x.Amount??0),repair=Math.Min(max-hp,e.ActualRepair),block=Math.Min(Math.Max(0,(prep?14:attack)-b.PlayerBlock),e.Block);
 double score=repair*(hp<=attack*2?3.5:1.2)+block*(hp<=attack*2?3.5:2.1);int damage=s.GetBattleCardDamage(c,b.Stage);if(damage>=enemy+b.EnemyBlock)return 10000/(s.GetBattleCardEnergyCost(ix)+.65);score+=damage*(board?1.7:.5);if(c.Draw>0&&b.Hand.Count<7)score+=c.Draw*(b.Hand.Count<4?6:2);
 if(c.Action==NavalBattleCardAction.Approach||c.Action==NavalBattleCardAction.Circle||c.Action==NavalBattleCardAction.Turn){if(b.DistanceMeters>60)score+=40;}if(c.Action==NavalBattleCardAction.Navigator||c.Action==NavalBattleCardAction.Parallel)score+=b.DistanceMeters>60?44:b.Alignment!=NavalBattleAlignment.Parallel?65:0;return score/(s.GetBattleCardEnergyCost(ix)+.65);}
 static bool Move(CoreVoyageSession s,bool cannon,Result r){
 if(!s.NavalActionMovementActive||s.Battle.NavalActionPoints<=0||s.Battle.NavalAwaitingHeadingSelection)return false;var b=s.Battle;s.ClearNavalMovementPath();int distance=CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters);moveRamThreat=CoreBattleIntentPresentation.Build(s).Any(x=>x.Kind==CoreBattleIntentKind.Ram);double best=DistanceScore(distance,cannon)+FieldRisk(s,b.PlayerPositionMeters)+(cannon?RamRisk(s,b.PlayerPositionMeters):0);var path=new List<VoyagePoint>();Search(s,Math.Min(6,b.NavalActionPoints),cannon,ref best,path);s.ClearNavalMovementPath();if(path.Count==0)return false;
 foreach(var p in path)Need(s.TryAppendNavalMovementStep(p,out _),"append legal path");int activeSteps=b.Stage==NavalBattleStage.Preparation?s.NavalMovementPlannedSteps:s.NavalMovementFieldPreview.ActiveSteps;r.movementFieldDamage+=s.NavalMovementFieldPreview.HullDamage;Need(s.CommitNavalMovement(out _),"commit movement");r.movementSteps+=activeSteps;
 if(b.NavalAwaitingHeadingSelection){if(!cannon&&s.CanNavalArrivalContact(true,out _,out _)){Need(s.TryNavalArrivalContact(true,out _),"arrival boarding");return true;}float heading=s.GetNavalArrivalHeadings().OrderBy(h=>HeadingScore(s,h,cannon)).First();Need(s.ConfirmNavalArrivalHeading(heading,out _),"heading");}return true;}
 static void Search(CoreVoyageSession s,int remaining,bool cannon,ref double best,List<VoyagePoint> chosen){if(remaining==0)return;foreach(var p in s.GetNavalLegalNextSteps().ToArray()){int d=CoreVoyageSession.NavalGridDistance(p,s.Battle.EnemyPositionMeters);if(d==0||!s.TryAppendNavalMovementStep(p,out _))continue;var f=s.NavalMovementFieldPreview;var final=s.Battle.Stage==NavalBattleStage.Preparation?p:f.FinalPositionMeters;double score=DistanceScore(CoreVoyageSession.NavalGridDistance(final,s.Battle.EnemyPositionMeters),cannon)+f.HullDamage*5+FieldRisk(s,final)+(cannon?RamRisk(s,final):0)+s.NavalMovementPlannedSteps*.02;if(!cannon&&d==1&&s.Battle.Stage==NavalBattleStage.Naval){bool horizontal=Math.Abs(Math.Sin(s.Battle.EnemyHeadingDegrees*Math.PI/180))<.5;bool side=horizontal?Math.Abs(f.FinalPositionMeters.Y-s.Battle.EnemyPositionMeters.Y)>50:Math.Abs(f.FinalPositionMeters.X-s.Battle.EnemyPositionMeters.X)>50;score+=side&&s.Battle.NavalActionPoints-f.ActiveSteps>=1?-30:10;}if(f.Sinks||cannon&&f.EntersBoarding)score+=10000;if(score<best){best=score;chosen.Clear();chosen.AddRange(s.NavalMovementPath.Skip(1).Select(x=>x.PositionMeters));}Search(s,remaining-1,cannon,ref best,chosen);s.UndoNavalMovementStep();}}
 static double DistanceScore(int d,bool cannon)=>cannon?(d<=1?200:d<=4?0:(d-4)*12):d<=1?0:(d-1)*12;
 static double FieldRisk(CoreVoyageSession s,VoyagePoint p){var f=s.NavalFieldAt(p);if(f==null)return 0;var st=s.Battle.NavalFields.Player;if((st.FieldImmunityMask&(1<<(int)f.Kind))!=0&&st.FieldImmunityExpires>s.Battle.NavalFields.PlayerTurn)return 0;switch(f.Kind){case CoreNavalFieldKind.Fire:return 24;case CoreNavalFieldKind.Curse:return 30;case CoreNavalFieldKind.Blood:return s.MaxHull*.06;case CoreNavalFieldKind.Thunder:return s.Battle.Schools.ThunderChain>=40?100:0;case CoreNavalFieldKind.Arcane:return 6;default:return 0;}}
 static double HeadingScore(CoreVoyageSession s,float h,bool cannon){var b=s.Battle;var next=new VoyagePoint(b.PlayerPositionMeters.X+(float)Math.Round(Math.Cos(h*Math.PI/180))*100,b.PlayerPositionMeters.Y+(float)Math.Round(Math.Sin(h*Math.PI/180))*100);int current=CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters),future=CoreVoyageSession.NavalGridDistance(next,b.EnemyPositionMeters);double facePenalty=cannon&&current<=4&&future<current?80:0;return DistanceScore(future,cannon)+FieldRisk(s,next)+facePenalty+(cannon?RamRisk(s,next):0);}
}

public static partial class BattleModes {
 static int Upgrade(CoreVoyageSession s,string[] ids,Cohort c){
 var dp=new Dictionary<int,int[]>{{0,new int[ids.Length]}};
 int cost(int rank)=>rank==0?0:rank==1?120:rank==2?360:840;
 double merit(int[] levels)=>levels.Select((v,i)=>v*v*(ids.Length-i)).Sum();
 for(int i=0;i<ids.Length;i++){var next=new Dictionary<int,int[]>();foreach(var pair in dp)for(int rank=0;rank<=Math.Min(c.Rank,s.GetCardMaximumUpgradeLevel(ids[i]));rank++){int used=pair.Key+cost(rank);if(used>c.Budget)continue;var levels=(int[])pair.Value.Clone();levels[i]=rank;if(!next.ContainsKey(used)||merit(levels)>merit(next[used]))next[used]=levels;}dp=next;}
 Need(dp.ContainsKey(c.Budget),"unspendable equal budget "+c.Id+" limits="+string.Join(";",ids.Select(id=>id+":"+s.GetCardMaximumUpgradeLevel(id)))+" reachable="+string.Join(",",dp.Keys.OrderBy(x=>x)));int actual=0;
 for(int i=0;i<ids.Length;i++)for(int rank=0;rank<dp[c.Budget][i];rank++){int quote=s.GetCardUpgradeCost(Cards[ids[i]]);Need(s.TryUpgradeBattleCard(Cards[ids[i]],out string why),"upgrade "+ids[i]+" "+why);actual+=quote;}
 Need(actual==c.Budget,"paid upgrade total");return actual;
 }
}


public static partial class BattleModes {
 static CoreEncounter Ordinary(Cohort c){int route=c.Id=="starter"?0:c.Id=="patrol"?1:2;string id=route==0?"event_reef_coral_ambush":route==1?"gold_customs":"moon_ghost";
 var session=new CoreVoyageSession();var pool=(IReadOnlyList<CoreEncounter>)typeof(CoreVoyageSession).GetMethod("CreateEncounterPool",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new object[]{CoreVoyageSession.Routes[route]});
 return pool.Concat(CoreVoyageEventCatalog.CreateFixedEncounters(CoreVoyageSession.Routes[route].Id)).First(x=>x.Id==id);
 }
}

public static partial class BattleModes {
 static int ChooseCannon(CoreVoyageSession s){int best=NavalBalanceProbe.ChooseCard(s,true);if(best>=0)return best;bool needs=s.NavalDistanceCells>4||FieldRisk(s,s.Battle.PlayerPositionMeters)>0||s.NavalDistanceCells<=1;if(!needs)return -1;
 return Enumerable.Range(0,s.Battle.Hand.Count).Where(h=>s.CanPlayCard(h)&&s.GetBattleCard(s.Battle.Hand[h]).NavalActionPointGain>0).OrderByDescending(h=>s.GetBattleCard(s.Battle.Hand[h]).NavalActionPointGain).DefaultIfEmpty(-1).First();}
}

public static partial class BattleModes {
 static bool diagnostic;static int diagnosticBranch=-1;static readonly List<VoyagePoint[]> diagnosticPaths=new List<VoyagePoint[]>();static readonly List<object> diagnosticResults=new List<object>();
 static int Diagnostic(){diagnostic=true;var c=Cohorts.Single(x=>x.Id=="gunship");var enemy=CoreRouteBossCatalog.Find("boss_route_moon_tide_ruins").CreateEncounter();
 var baseline=Run(c,"cannon",enemy,6);diagnosticResults.Add(new {branch=-1,result=baseline});
 for(int i=0;i<diagnosticPaths.Count;i++){diagnosticBranch=i;var result=Run(c,"cannon",enemy,6);diagnosticResults.Add(new {branch=i,path=diagnosticPaths[i].Select(p=>new{x=p.X,y=p.Y}).ToArray(),result});}
 File.WriteAllText(Path.Combine(output,"ram-avoidance-branches.json"),Json.Serialize(diagnosticResults),new UTF8Encoding(false));Console.WriteLine("Diagnostic branches "+diagnosticPaths.Count);return 0;}
 static void BeforeDiagnosticEnd(CoreVoyageSession s){
 if(diagnosticBranch<0){var candidates=new List<object>();foreach(var p in s.GetNavalReachableCells()){
 s.ClearNavalMovementPath();if(!s.TryAppendNavalMovementSegment(p,out _))continue;var path=s.NavalMovementPath.Skip(1).Select(n=>n.PositionMeters).ToArray();var f=s.NavalMovementFieldPreview;diagnosticPaths.Add(path);var cell=s.NavalFieldAt(f.FinalPositionMeters);candidates.Add(new {branch=diagnosticPaths.Count-1,path=path.Select(n=>new{x=n.X,y=n.Y}).ToArray(),steps=f.ActiveSteps,hullDamage=f.HullDamage,sinks=f.Sinks,stopsAtThunder=f.StopsAtThunder,field=cell==null?"none":cell.Kind.ToString(),endX=f.FinalPositionMeters.X,endY=f.FinalPositionMeters.Y});}
 s.ClearNavalMovementPath();var b=s.Battle;var field=s.NavalFieldAt(b.PlayerPositionMeters);
 File.WriteAllText(Path.Combine(output,"ram-avoidance-state.json"),Json.Serialize(new{turn=b.Turn,ap=b.NavalActionPoints,energy=b.Energy,hull=s.Hull,playerX=b.PlayerPositionMeters.X,playerY=b.PlayerPositionMeters.Y,playerHeading=b.PlayerHeadingDegrees,enemyX=b.EnemyPositionMeters.X,enemyY=b.EnemyPositionMeters.Y,enemyHeading=b.EnemyHeadingDegrees,field=field==null?null:new{kind=field.Kind.ToString(),field.DirectionX,field.DirectionY},intents=CoreBattleIntentPresentation.Build(s),candidates}),new UTF8Encoding(false));return;}
 s.ClearNavalMovementPath();foreach(var p in diagnosticPaths[diagnosticBranch])Need(s.TryAppendNavalMovementStep(p,out _),"diagnostic legal move");Need(s.CommitNavalMovement(out _),"diagnostic commit");if(s.Battle!=null&&s.Battle.NavalAwaitingHeadingSelection){float h=s.GetNavalArrivalHeadings().OrderBy(x=>HeadingScore(s,x,true)).First();Need(s.ConfirmNavalArrivalHeading(h,out _),"diagnostic heading");}
 }
}

public static partial class BattleModes {
 static bool moveRamThreat;
 static double RamRisk(CoreVoyageSession s,VoyagePoint p){var b=s.Battle;var front=new VoyagePoint(b.EnemyPositionMeters.X+(float)Math.Round(Math.Cos(b.EnemyHeadingDegrees*Math.PI/180))*100,b.EnemyPositionMeters.Y+(float)Math.Round(Math.Sin(b.EnemyHeadingDegrees*Math.PI/180))*100);return CoreVoyageSession.NavalGridDistance(p,front)==0?(moveRamThreat?500:60):0;}
}
