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
 static string output;static int maxTurns;static string boardingDeck="neutral";static double diagnosticCrewScale=1;static double diagnosticAttackScale=1;static ComponentArtCatalog catalog;
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=100};
 static readonly Dictionary<string,int> Cards=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).ToDictionary(i=>CoreVoyageSession.CardLibrary[i].Id,i=>i);
 static readonly HashSet<string> loadoutKeys=new HashSet<string>();static readonly List<object> loadouts=new List<object>();static readonly List<Result> results=new List<Result>();
 static string[] Pair(params string[] ids)=>ids.SelectMany(x=>new[]{x,x}).ToArray();
 static readonly string[] Contact=Pair("card_approach","card_full_sail_turn","card_emergency_repair","card_brace","card_energy_order");
 static readonly string[] Neutral=Pair("card_boarding_basic_02","card_boarding_basic_05","card_boarding_basic_06","card_boarding_basic_09","card_boarding_basic_13");
 static readonly string[] Curse=Pair("card_boarding_curse_10","card_boarding_basic_09","card_boarding_basic_05","card_boarding_tide_02","card_boarding_basic_06");
 static readonly string[] CannonPrep=Pair("card_load_shot","card_deck_polish","card_energy_order","card_tactical_draw","card_brace");
 sealed class Cohort{public string Id,Ship;public int Level,Rank,Budget,SpecialXP,OldNavalRank;public string[] Parts,Crew;}
 static readonly Cohort[] Cohorts={
 new Cohort{Id="starter",Ship="sloop",Level=1,Rank=3,Budget=720,SpecialXP=0,OldNavalRank=0,Parts=new string[0],Crew=new string[0]},
 new Cohort{Id="patrol",Ship="patrol",Level=5,Rank=3,Budget=1200,SpecialXP=120,OldNavalRank=1,Parts=new[]{"bow_gilded_mermaid","sail_guild_silk","cannon_chain_repeater","plating_gilded_bulwark","instrument_guild_engine"},Crew=new string[0]},
 new Cohort{Id="gunship",Ship="gunship",Level=10,Rank=3,Budget=1200,SpecialXP=360,OldNavalRank=2,Parts=new[]{"bow_abyssal_fang","sail_moonweave","cannon_starfire_battery","plating_moonsilver_skin","instrument_lunar_astrolabe"},Crew=new string[0]}};
 public sealed class Result{
 public string cohort,mode,boss,name,outcome,trace,failureStage,contactType,entryFieldKinds;public double softenTarget,boardingEntryEnemyRatio=-1,boardingEntryPlayerCrewRatio=-1;public bool softenTargetReached,boardingEntryInTargetBand,skippedNaval;public double diagnosticEnemyCrewFactor=1,diagnosticEnemyMaxScale=1,diagnosticEnemyAttackScale=1;public int safeReentryCandidatesRejected;public int authoredEnemyMaxCrew;public object budgetLedger;public int seed,budget,spent,maxHull,initialHull,minimumHull,finalHull,playerMaxCrew,minimumCrew,finalCrew,enemyMaxHull,enemyFinalHull,enemyMaxCrew,enemyFinalCrew,prepTurns,navalTurns,boardingTurns,cards,movementSteps,powder,balls,wood,medicine,boardingEntryNavalTurn=-1,boardingEntryHull,boardingEntryEnemyHull,boardingEntryCrew,boardingEntryEnemyCrew,carryover,startPlayerHull,startEnemyHull,ownWindowHullLoss,enemyWindowHullLoss,ownWindowCrewLoss,enemyWindowCrewLoss,movementFieldDamage,boardingEntryPowder,boardingEntryBalls,boardingEntryWood,boardingEntryMedicine;public bool supplyExhausted,won,achievedMode;public decimal replacementCost;public object initial;}
 public static int Main(string[] args){
 CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;output=args[0];int seeds=int.Parse(args[1]);maxTurns=int.Parse(args[2]);UnityEngine.Resources.Root=args[3];catalog=new ComponentArtCatalog();int seedStart=int.Parse(args[6]);string filter=args[7];boardingDeck=args[8];
 var scenarios=CoreRouteBossCatalog.All.Select(x=>x.CreateEncounter()).Concat(CoreLegendaryWarships.All.Select(x=>x.CreateEncounter())).Concat(new[]{CoreLegendaryWarships.CreateTreasureEncounter()}).Where(x=>filter=="*"||filter.Split(',').Contains(x.Id)).ToArray();
 foreach(double attackScale in (args.Length>10?args[10]:"1").Split(',').Select(x=>double.Parse(x,CultureInfo.InvariantCulture)))foreach(double scale in (args.Length>9?args[9]:"1").Split(',').Select(x=>double.Parse(x,CultureInfo.InvariantCulture)))foreach(var cohort in Cohorts.Where(x=>args[4].Split(',').Contains(x.Id)))foreach(var mode in args[5].Split(','))foreach(var enemy in scenarios.Where(x=>Fits(cohort,x.Id))){diagnosticCrewScale=scale;diagnosticAttackScale=attackScale;for(int seed=seedStart;seed<seedStart+seeds;seed++)results.Add(Run(cohort,mode,enemy,seed));Console.WriteLine(cohort.Id+" "+mode+" "+enemy.Id+" "+results.Count);Save();}
 Need(results.Count>0,"no samples: check cohort/mode/boss arguments");Save();return 0;}
 static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
 static string Cell(object x)=>"\""+Convert.ToString(x,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";
 static void Save(){File.WriteAllText(Path.Combine(output,"samples.json"),Json.Serialize(results),new UTF8Encoding(false));File.WriteAllText(Path.Combine(output,"loadouts.json"),Json.Serialize(loadouts),new UTF8Encoding(false));var f=typeof(Result).GetFields().Where(x=>x.Name!="initial"&&x.Name!="budgetLedger").ToArray();File.WriteAllLines(Path.Combine(output,"samples.csv"),new[]{string.Join(",",f.Select(x=>Cell(x.Name)))}.Concat(results.Select(r=>string.Join(",",f.Select(x=>Cell(x.GetValue(r)))))),new UTF8Encoding(true));}
 static CoreVoyageSession Create(Cohort c,string mode,int seed,int route,out object initial,out int spent){
 var s=new CoreVoyageSession();s.RestoreProfile(c.Budget,"断锚港");s.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=CoreVoyageSession.PirateExperienceForLevel(c.Level)});
 int ship=Enumerable.Range(0,CoreVoyageSession.Ships.Count).Single(i=>CoreVoyageSession.Ships[i].Id==c.Ship);Need(s.SelectShip(ship),"fixture ship");
 bool[] professions=new bool[6];foreach(string id in c.Crew)professions[CrewNarrativeCatalog.FindById(id).ProfessionIndex]=true;s.ConfigureCrewCapabilities(professions[0],professions[1],professions[2],professions[3],professions[4],professions[5]);s.ConfigureDepartureCrew(s.CrewCountValue-c.Crew.Length,c.Crew.Length);
 var parts=c.Parts.Length==0?Enumerable.Range(0,5).Select(i=>catalog.Get(i*ComponentArtCatalog.EntriesPerSlot)).ToArray():c.Parts.Select(catalog.GetById).ToArray();Need(parts.All(x=>x!=null),"parts");s.ConfigureComponentBonuses(parts.Sum(x=>x.HullBonus-catalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).HullBonus),parts.Sum(x=>x.ArmorBonus-catalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).ArmorBonus),parts.Sum(x=>x.FirepowerBonus-catalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).FirepowerBonus),parts.Sum(x=>x.SpeedBonus-catalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).SpeedBonus),0);
 float quality(ComponentSlot slot){var p=parts.FirstOrDefault(x=>x.Slot==slot);return p==null?0:1+(int)p.Rarity;}s.ConfigureBattleEquipment(quality(ComponentSlot.Bow),quality(ComponentSlot.Plating));s.ConfigureComponentContextEffect(0,0,0,0,0,1);
 s.ConfigureSpecialCardSources(c.Crew,parts.Where(x=>x.Rarity>=ComponentRarity.Rare).Select(x=>x.Id).ToArray());var sourceIds=c.Crew.Select(x=>CoreSpecialCardCatalog.ForCrew(x)?.Card.Id).Concat(parts.Select(x=>CoreSpecialCardCatalog.ForComponent(x.Id)?.Card.Id)).Where(x=>x!=null).Distinct();s.RestoreSpecialCardExperience(sourceIds.ToDictionary(x=>x,x=>c.SpecialXP));var commonRanks=new[]{"card_cannon_barrage","card_chain_shot","card_brace","card_emergency_repair"}.ToDictionary(x=>x,x=>Math.Min(c.OldNavalRank,s.GetCardMaximumUpgradeLevel(x)));s.ConfigureCardUpgrades(commonRanks);
 bool cannon=mode.StartsWith("cannon");bool soft=mode.StartsWith("soften");string[] naval=cannon?NavalBalanceProbe.Decks.Single(x=>x.Name=="ordinary").Ids:soft?new[]{"card_cannon_barrage","card_cannon_barrage","card_chain_shot","card_chain_shot","card_approach","card_approach","card_energy_order","card_energy_order","card_emergency_repair","card_brace"}:Contact;
 if(mode=="cannon-repair")naval=naval.Select(id=>id=="card_load_shot"?"card_emergency_repair":id).ToArray();
 bool defensive=mode.EndsWith("-defensive"),repairDeck=mode.EndsWith("-repair");if(soft&&(defensive||repairDeck))naval=naval.Concat(new[]{"card_emergency_repair","card_brace"}).ToArray();string[] prep=soft&&defensive?Contact:cannon||soft?CannonPrep:Contact;string[] boarding=boardingDeck=="curse"?Pair("card_boarding_curse_01","card_boarding_curse_02","card_boarding_curse_06","card_boarding_basic_09","card_boarding_basic_05","card_boarding_curse_12"):Neutral;
 Need(s.SetBattleDeck(NavalBattleStage.Preparation,prep.Select(x=>Cards[x]).ToArray()),"preparation rejected "+mode);Need(s.SetBattleDeck(NavalBattleStage.Naval,naval.Select(x=>Cards[x]).ToArray()),"naval rejected "+mode);Need(s.SetBattleDeck(NavalBattleStage.Boarding,boarding.Select(x=>Cards[x]).ToArray()),"boarding rejected "+mode+" unavailable="+string.Join(",",boarding.Distinct().Where(id=>!s.GetAvailableBattleCards(NavalBattleStage.Boarding).Contains(Cards[id]))));
 currentUpgradeLedger=new List<object>();var upgradeOrder=(cannon?naval.Concat(prep):boarding).Distinct().ToArray();spent=Upgrade(s,upgradeOrder,c);lastBudgetLedger=new {budget=c.Budget,actualSpent=spent,unused=s.ProfileGold,newShipCost=0,newComponentCost=0,installationWoodCost=0,cardUpgradeCost=spent,upgrades=currentUpgradeLedger.ToArray(),commonAssets=new {ship=c.Ship,shipReferenceCatalogValue=ship==0?0:CoreShipMarketCatalog.PurchaseCost(ship),components=parts.Select(x=>new{id=x.Id,referenceCatalogPrice=x.Price,commonExistingAsset=true}).ToArray(),crew=c.Crew,ordinary=s.SelectedOrdinaryCrew,sourceXP=c.SpecialXP,existingCardRanks=commonRanks,commonAccountGold=0,commonTravelGold=60,chargedToIncrementalBudget=false},scope="Same already-owned stage assets (including historically acquired starter gray fittings, not a free new-game grant); only listed new purchases count. This baseline has no new ship/component purchases."};
 if(route>=0){s.SelectRoute(route);Need(s.SelectedRouteIndex==route,"ordinary route eligibility");}s.SetSeed("BATTLE-MODE-PAIRED-"+seed);s.StartVoyage();int ammo=c.Id=="starter"?24:c.Id=="patrol"?48:80,medical=c.Id=="starter"?8:c.Id=="patrol"?16:24;s.Cargo.Powder=ammo;s.Cargo.Cannonballs=ammo;s.Cargo.Wood=medical;s.Cargo.Medicine=medical;Need(s.Cargo.UsedVolume<=s.CargoCapacityValue,"fixture cargo capacity");typeof(CoreVoyageSession).GetField("voyageRandom",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new Mulberry32Random(SeedHash.Compute("BATTLE-MODE-PAIRED-"+seed)));
 initial=new {cohort=c.Id,mode,seed,softeningDeckPreset=soft?(defensive?"defensive-12-contact-prep":repairDeck?"defensive-12-cannon-prep":"hybrid-10-cannon-prep"):"not-applicable",ship=c.Ship,level=c.Level,budget=c.Budget,spent,hull=s.Hull,maxHull=s.MaxHull,armor=s.EffectiveArmor,firepower=s.EffectiveFirepower,ordinaryCrew=s.SelectedOrdinaryCrew,crew=c.Crew,components=parts.Select(x=>new{id=x.Id,name=x.Name,rarity=x.Rarity.ToString()}).ToArray(),environment="neutral-context-disabled",noRetreatForVictory=true,commonExistingUpgrades=commonRanks,allPaidCardUpgrades=s.CardUpgradeLevels,route,specialXP=c.SpecialXP,preparation=prep,naval,boarding,upgrades=upgradeOrder.ToDictionary(x=>x,x=>s.GetCardUpgradeLevel(x)),powder=ammo,balls=ammo,wood=medical,medicine=medical,cargoVolume=s.Cargo.UsedVolume,capacity=s.CargoCapacityValue};if(loadoutKeys.Add(c.Id+"|"+mode+"|"+boardingDeck))loadouts.Add(initial);return s;}
}
public static partial class BattleModes
{
 static Result Run(Cohort c,string mode,CoreEncounter enemy,int seed){
 bool isBoss=enemy.BossKind!=CoreBossKind.None;int route=isBoss?-1:c.Id=="starter"?0:c.Id=="patrol"?1:2;var s=Create(c,mode,seed,route,out object initial,out int spent);
 typeof(CoreVoyageSession).GetMethod("StartBattle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{enemy,isBoss});var b=s.Battle;Need(b.Stage==NavalBattleStage.Preparation&&s.NavalDistanceCells==6,"600m and preparation entry");
 bool stageDiagnostic=mode.StartsWith("diagnostic"),pure=mode.StartsWith("cannon"),soft=mode.StartsWith("soften");double target=soft?int.Parse(mode.Substring(6).Split('-')[0])/100d:1;
 pureCannonRun=pure;safeReentryRejects=0;var r=new Result{cohort=c.Id,mode=mode,boss=enemy.Id,name=enemy.Name,seed=seed,budget=c.Budget,spent=spent,initial=initial,maxHull=s.MaxHull,initialHull=s.Hull,minimumHull=s.Hull,playerMaxCrew=b.PlayerMaxBoardingHealth,minimumCrew=b.PlayerBoardingHealth,enemyMaxHull=b.EnemyMaxHull,enemyMaxCrew=b.EnemyMaxBoardingHealth,startPlayerHull=b.NavalStartPlayerHull,startEnemyHull=b.NavalStartEnemyHull,softenTarget=target,budgetLedger=lastBudgetLedger};var trace=new List<string>();if(stageDiagnostic){r.skippedNaval=true;r.authoredEnemyMaxCrew=b.EnemyMaxBoardingHealth;r.diagnosticEnemyMaxScale=diagnosticCrewScale;r.diagnosticEnemyAttackScale=diagnosticAttackScale;r.diagnosticEnemyCrewFactor=int.Parse(mode.Substring(10))/100d;typeof(CoreVoyageSession).GetMethod("EnterBattleStage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{NavalBattleStage.Boarding});ApplyDiagnosticPressure(b);b.EnemyMaxBoardingHealth=(int)Math.Ceiling(b.EnemyMaxBoardingHealth*diagnosticCrewScale);r.enemyMaxCrew=b.EnemyMaxBoardingHealth;b.EnemyBoardingHealth=(int)Math.Ceiling(b.EnemyMaxBoardingHealth*r.diagnosticEnemyCrewFactor);Entry(r,s,b);r.contactType="diagnostic-stage-entry-no-naval-path";}
 for(int action=0;action<4000&&s.Phase==CoreDemoPhase.Battle;action++){
 if(r.navalTurns+r.boardingTurns>=maxTurns){r.outcome="timeout";break;}
 bool atTarget=!soft||b.EnemyHull<=b.EnemyMaxHull*target||(b.Stage==NavalBattleStage.Naval&&b.EnemyHull<=b.EnemyMaxHull*.6&&!HasSafeSofteningAttack(s,target));atTarget=atTarget||(soft&&b.Stage==NavalBattleStage.Naval&&SafeSoftRam(s));bool firing=pure||soft&&!atTarget;softContactActive=soft&&atTarget;softBoardAllowed=!soft||b.EnemyHull<=b.EnemyMaxHull*.6;
 if(pure&&b.Stage==NavalBattleStage.Boarding){if(r.boardingEntryNavalTurn<0)Entry(r,s,b);r.outcome="forced_boarding";break;}
 int hp=s.Hull,ch=b.PlayerBoardingHealth;bool ended=false;string decision="";int preBoardRepair=!firing&&b.Stage==NavalBattleStage.Naval?ChoosePreBoardRepair(s):-1;
 if(!firing&&preBoardRepair<0&&b.Stage==NavalBattleStage.Naval&&((softBoardAllowed&&s.CanBoard(out _))||(s.CanRam(out _)&&(!soft||SafeSoftRam(s))))){if(softBoardAllowed&&s.CanBoard(out _)){Need(s.TryBoard(),"board");decision="board";}else{Need(s.TryRam(),"ram");decision="ram";}}
 else{
 int slot=preBoardRepair>=0?preBoardRepair:b.Stage==NavalBattleStage.Boarding?ChooseContact(s):b.Stage==NavalBattleStage.Preparation?(mode=="direct"?ChooseContact(s):NavalBalanceProbe.ChoosePreparation(s)):firing?(pure?ChooseCannon(s):ChooseSofteningCard(s,target)):ChooseApproach(s);
 if(slot>=0){var card=s.GetBattleCard(b.Hand[slot]);decision=card.Id;int p=s.Cargo.Powder,a=s.Cargo.Cannonballs,w=s.Cargo.Wood,m=s.Cargo.Medicine;Need(NavalBalanceProbe.PlayChosen(s,slot),"card "+card.Id);r.powder+=Math.Max(0,p-s.Cargo.Powder);r.balls+=Math.Max(0,a-s.Cargo.Cannonballs);r.wood+=Math.Max(0,w-s.Cargo.Wood);r.medicine+=Math.Max(0,m-s.Cargo.Medicine);r.cards++;}
 else if(b.Stage!=NavalBattleStage.Boarding&&Move(s,firing,r))decision="move";
 else{var stage=b.Stage;int remaining=b.Energy,ap=b.NavalActionPoints,available=Enumerable.Range(0,b.Hand.Count).Count(i=>s.CanPlayCard(i));var preview=stage==NavalBattleStage.Boarding?s.GetBoardingEnemyPreview():null;int intent=preview?.PlayerHealthLoss??CoreBattleIntentPresentation.Build(s).Where(x=>x.IsAttack).Sum(x=>x.Amount??0);Need(s.EndBattleTurn(),"end");ended=true;decision="end energy="+remaining+" ap="+ap+" legalCards="+available+" totalThreat="+intent;if(stage==NavalBattleStage.Preparation)r.prepTurns++;else if(stage==NavalBattleStage.Naval)r.navalTurns++;else r.boardingTurns++;}}
 int hl=Math.Max(0,hp-s.Hull),cl=Math.Max(0,ch-b.PlayerBoardingHealth);if(ended){r.enemyWindowHullLoss+=hl;r.enemyWindowCrewLoss+=cl;}else{r.ownWindowHullLoss+=hl;r.ownWindowCrewLoss+=cl;}
 r.minimumHull=Math.Min(r.minimumHull,s.Hull);r.minimumCrew=Math.Min(r.minimumCrew,b.PlayerBoardingHealth);
 if(trace.Count<(stageDiagnostic?(seed<2?700:0):700))trace.Add(b.Stage+":"+decision+":"+s.Hull+"/"+b.PlayerBoardingHealth+":"+b.EnemyHull+"/"+b.EnemyBoardingHealth+PolicyPose(s));
 if(b.Stage==NavalBattleStage.Boarding&&r.boardingEntryNavalTurn<0){Entry(r,s,b);r.contactType=b.BoardingInitiatedByEnemy?"enemy-forced-contact":decision=="board"?"direct-board":decision=="ram"?"player-ram":decision=="move"?(b.BoardingConnection==NavalBoardingConnection.Planned?"arrival-board":"arrival-ram"):"enemy-forced-contact";}}
 r.safeReentryCandidatesRejected=safeReentryRejects;r.finalHull=s.Hull;r.finalCrew=b.PlayerBoardingHealth;r.enemyFinalHull=b.EnemyHull;r.enemyFinalCrew=b.EnemyBoardingHealth;r.supplyExhausted=s.Cargo.Powder==0||s.Cargo.Cannonballs==0||s.Cargo.Medicine==0;
 if(r.outcome==null)r.outcome=s.Hull<=0||b.PlayerBoardingHealth<=0?"dead":b.EnemyHull<=0?"sunk":b.Stage==NavalBattleStage.Boarding&&b.EnemyBoardingHealth<=0?"captured":s.Phase!=CoreDemoPhase.Battle?"ended":"action_watchdog";
 r.failureStage=r.outcome=="dead"||r.outcome=="timeout"||r.outcome=="action_watchdog"?b.Stage.ToString():"none";
 r.won=r.outcome=="sunk"||r.outcome=="captured";r.achievedMode=pure?r.outcome=="sunk":r.outcome=="captured"&&(!soft||r.boardingEntryInTargetBand);
 r.replacementCost=Price(PortMarketItem.Powder,r.powder)+Price(PortMarketItem.Cannonballs,r.balls)+Price(PortMarketItem.Wood,r.wood)+Price(PortMarketItem.Medicine,r.medicine);r.trace=string.Join(";",trace);return r;}
 static decimal Price(PortMarketItem item,int quantity)=>PortMarketCatalog.CalculateBuyCost("断锚港",item,CoreVoyageSession.NewCampaignStartDate(new DateTime(2026,10,27)),quantity,0);
 static void Entry(Result r,CoreVoyageSession s,CoreBattleState b){r.boardingEntryNavalTurn=r.navalTurns;r.boardingEntryHull=s.Hull;r.boardingEntryEnemyHull=b.EnemyHull;r.boardingEntryCrew=b.PlayerBoardingHealth;r.boardingEntryEnemyCrew=b.EnemyBoardingHealth;r.boardingEntryEnemyRatio=b.EnemyHull/(double)b.EnemyMaxHull;r.boardingEntryPlayerCrewRatio=b.PlayerBoardingHealth/(double)b.PlayerMaxBoardingHealth;r.softenTargetReached=r.boardingEntryEnemyRatio<=r.softenTarget;r.boardingEntryInTargetBand=r.boardingEntryEnemyRatio>=.5&&r.boardingEntryEnemyRatio<=.6;r.entryFieldKinds=string.Join(";",b.BoardingFields.Fields.Select(x=>x.Kind+":"+x.RemainingTurns+":"+(x.PlayerOwned?"player":"enemy")));r.carryover=b.TrainingBoardingCarryover;r.boardingEntryPowder=s.Cargo.Powder;r.boardingEntryBalls=s.Cargo.Cannonballs;r.boardingEntryWood=s.Cargo.Wood;r.boardingEntryMedicine=s.Cargo.Medicine;}
 static int ChooseContact(CoreVoyageSession s){int best=-1;double value=.01;for(int h=0;h<s.Battle.Hand.Count;h++)if(s.CanPlayCard(h)){if(s.GetNavalCardChoice(h)?.RequiresChoice==true&&NavalBalanceProbe.ChooseOptions(s,h)==null)continue;double v=ContactScore(s,s.Battle.Hand[h]);if(v>value){value=v;best=h;}}return best;}
 static double ContactScore(CoreVoyageSession s,int ix){
 var c=s.GetBattleCard(ix);var b=s.Battle;var e=s.GetBattleCardEffects(c,b.Stage);bool board=b.Stage==NavalBattleStage.Boarding,prep=b.Stage==NavalBattleStage.Preparation;
 if(c.BoardingSpec?.Key=="curse_10"){int marks=b.Boarding.CurseMarks,threshold=b.Schools.EnemyCurseExecutionThresholdPercent,after=threshold-CoreCurseRules.EntangleThresholdReductionPercent;if(CoreCurseRules.ReachesExecutionThreshold(marks,b.EnemyBoardingHealth,after))return 100000;return marks>0?Math.Min(45,marks*500d/Math.Max(5,threshold)):(s.GetBattleCardEnergyCost(ix)==0?4:0);}if(c.Action==NavalBattleCardAction.Energize||c.BoardingSpec?.Key=="basic_09")return 100;
 int enemy=board?b.EnemyBoardingHealth:b.EnemyHull,hp=board?b.PlayerBoardingHealth:s.Hull,max=board?b.PlayerMaxBoardingHealth:s.MaxHull;var preview=board?s.GetBoardingEnemyPreview():null;int attack=preview?.PlayerHealthLoss??CoreBattleIntentPresentation.Build(s).Where(x=>x.IsAttack).Sum(x=>x.Amount??0);int repair=Math.Min(max-hp,e.ActualRepair),block=Math.Min(board?Math.Max(0,(preview.Actions.Sum(x=>Math.Max(0,x.AttackDamage-x.PiercingDamage-x.AbsorbedDamage))+preview.PlayerBurnDamage)):Math.Max(0,(prep?14:attack)-b.PlayerBlock),e.Block);
 double score=repair*(hp<=attack*2?3.5:1.2)+block*(hp<=attack*2?3.5:2.1);int damage=s.GetBattleCardDamage(c,b.Stage);if(board&&(c.BoardingSpec?.Key=="curse_01"||c.BoardingSpec?.Key=="curse_02")){double markValue=CurseMarkValue(s,c,damage);if(markValue>=100000)return markValue;score+=markValue;}if(damage>=enemy+b.EnemyBlock)return 10000/(s.GetBattleCardEnergyCost(ix)+.65);score+=damage*(board?1.7:.5);if(c.Draw>0&&b.Hand.Count<7)score+=c.Draw*(b.Hand.Count<4?6:2);
 if(c.Action==NavalBattleCardAction.Approach||c.Action==NavalBattleCardAction.Circle||c.Action==NavalBattleCardAction.Turn){if(b.DistanceMeters>60)score+=40;}if(c.Action==NavalBattleCardAction.Navigator||c.Action==NavalBattleCardAction.Parallel)score+=b.DistanceMeters>60?44:b.Alignment!=NavalBattleAlignment.Parallel?65:0;return score/(s.GetBattleCardEnergyCost(ix)+.65);}
 static bool Move(CoreVoyageSession s,bool cannon,Result r){
 if(!s.NavalActionMovementActive||s.Battle.NavalActionPoints<=0||s.Battle.NavalAwaitingHeadingSelection)return false;var b=s.Battle;s.ClearNavalMovementPath();int distance=CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters);safeCannonOrigin=pureCannonRun&&b.Stage==NavalBattleStage.Naval&&distance>4;var intents=CoreBattleIntentPresentation.Build(s);moveRamThreat=intents.Any(x=>x.Kind==CoreBattleIntentKind.Ram);moveCannonThreat=Math.Max(0,intents.Where(x=>x.Kind==CoreBattleIntentKind.Cannon).Sum(x=>x.Amount??0)-b.PlayerBlock);moveEnemyPoint=b.Stage==NavalBattleStage.Naval?s.GetEnemyNavalFieldPreview(intents.Where(x=>x.Kind==CoreBattleIntentKind.Move).Sum(x=>x.Amount??0)*100).FinalPositionMeters:b.EnemyPositionMeters;double best=SoftManeuverSetupRisk(s)+(s.AuditNextTurnTideBlocksHeading(b.PlayerHeadingDegrees)?200:0)+DistanceScore(distance,cannon)+SoftContactGeometryRisk(s,b.PlayerPositionMeters)+FieldRisk(s,b.PlayerPositionMeters)+(cannon?RamRisk(s,b.PlayerPositionMeters)+CannonPositionRisk(s,b.PlayerPositionMeters):0);var path=new List<VoyagePoint>();Search(s,Math.Min(6,b.NavalActionPoints),cannon,ref best,path);s.ClearNavalMovementPath();if(path.Count==0)return false;
 foreach(var p in path)Need(s.TryAppendNavalMovementStep(p,out _),"append legal path");int activeSteps=b.Stage==NavalBattleStage.Preparation?s.NavalMovementPlannedSteps:s.NavalMovementFieldPreview.ActiveSteps;r.movementFieldDamage+=s.NavalMovementFieldPreview.HullDamage;Need(s.CommitNavalMovement(out _),"commit movement");r.movementSteps+=activeSteps;
 if(b.NavalAwaitingHeadingSelection){if(!cannon&&softBoardAllowed&&s.CanNavalArrivalContact(true,out _,out _)){Need(s.TryNavalArrivalContact(true,out _),"arrival boarding");return true;}if(!cannon&&softContactActive&&SafeSoftRam(s)&&s.CanNavalArrivalContact(false,out _,out _)){Need(s.TryNavalArrivalContact(false,out _),"arrival ram within window");return true;}float heading=s.GetNavalArrivalHeadings().OrderBy(h=>HeadingScore(s,h,cannon)).First();Need(s.ConfirmNavalArrivalHeading(heading,out _),"heading");}return true;}
 static void Search(CoreVoyageSession s,int remaining,bool cannon,ref double best,List<VoyagePoint> chosen){if(remaining==0)return;foreach(var p in s.GetNavalLegalNextSteps().ToArray()){int d=CoreVoyageSession.NavalGridDistance(p,s.Battle.EnemyPositionMeters);if(d==0||!s.TryAppendNavalMovementStep(p,out _))continue;var f=s.NavalMovementFieldPreview;var final=s.Battle.Stage==NavalBattleStage.Preparation?p:f.FinalPositionMeters;double score=DistanceScore(CoreVoyageSession.NavalGridDistance(final,s.Battle.EnemyPositionMeters),cannon)+SoftContactGeometryRisk(s,final)+f.HullDamage*5+FieldRisk(s,final)+(cannon?RamRisk(s,final)+CannonPositionRisk(s,final):0)+s.NavalMovementPlannedSteps*.02;if(!cannon&&s.Battle.Stage==NavalBattleStage.Naval){bool board=softBoardAllowed&&s.AuditPlannedArrivalContact(true,out _,out _);bool ram=softContactActive&&s.AuditPlannedArrivalContact(false,out int hit,out _)&&s.Battle.EnemyHull-hit>=s.Battle.EnemyMaxHull*.5&&s.Battle.EnemyHull-hit<=s.Battle.EnemyMaxHull*.6;if(board||ram)score-=300;else if(d==1)score+=120;}if(f.Sinks||cannon&&f.EntersBoarding)score+=10000;bool unsafeReentry=safeCannonOrigin&&s.AuditCannonEntryHasThreatButNoAttack(final);if(unsafeReentry)safeReentryRejects++;if(!unsafeReentry&&score<best){best=score;chosen.Clear();chosen.AddRange(s.NavalMovementPath.Skip(1).Select(x=>x.PositionMeters));}Search(s,remaining-1,cannon,ref best,chosen);s.UndoNavalMovementStep();}}
 static double DistanceScore(int d,bool cannon)=>cannon?(d<=1?200:d<=4?(4-d)*.2:(d-4)*12):d<=1?0:(d-1)*12;
 static double FieldRisk(CoreVoyageSession s,VoyagePoint p){var f=s.NavalFieldAt(p);if(f==null)return 0;var st=s.Battle.NavalFields.Player;if((st.FieldImmunityMask&(1<<(int)f.Kind))!=0&&st.FieldImmunityExpires>s.Battle.NavalFields.PlayerTurn)return 0;switch(f.Kind){case CoreNavalFieldKind.Fire:return 24;case CoreNavalFieldKind.Curse:return 30;case CoreNavalFieldKind.Blood:return s.MaxHull*.06;case CoreNavalFieldKind.Thunder:return s.Battle.Schools.ThunderChain>=40?100:0;case CoreNavalFieldKind.Arcane:return 6;default:return 0;}}
 static double HeadingScore(CoreVoyageSession s,float h,bool cannon){var b=s.Battle;var next=new VoyagePoint(b.PlayerPositionMeters.X+(float)Math.Round(Math.Cos(h*Math.PI/180))*100,b.PlayerPositionMeters.Y+(float)Math.Round(Math.Sin(h*Math.PI/180))*100);int current=CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters),future=CoreVoyageSession.NavalGridDistance(next,b.EnemyPositionMeters);double facePenalty=(cannon&&current<=4&&future<current)||(softContactActive&&current<=1&&future<current)?80:0;return DistanceScore(future,cannon)+(softContactActive?SoftTwoStepDistance(s,next,h)*24:0)+FieldRisk(s,next)+facePenalty+(s.AuditTideBlocksStep(b.PlayerPositionMeters,next)||s.AuditNextTurnTideBlocksHeading(h)?10000:0)+(cannon?RamRisk(s,next)+CannonPositionRisk(s,next):0);}
}

public static partial class BattleModes {
 static int Upgrade(CoreVoyageSession s,string[] ids,Cohort c){
 if(boardingDeck=="curse"&&ids.Any(x=>x.StartsWith("card_boarding_curse_")))return UpgradeCurseSensitivity(s,ids,c);
 var old=ids.Select(s.GetCardUpgradeLevel).ToArray();var dp=new Dictionary<int,int[]>{{0,new int[ids.Length]}};
 int cost(int start,int add)=>Enumerable.Range(start,add).Sum(CoreBattleUpgradeBalance.CostForNextLevel);
 double merit(int[] adds)=>adds.Select((v,i)=>(v*v+2*v*old[i])*(ids.Length-i)).Sum();
 for(int i=0;i<ids.Length;i++){var next=new Dictionary<int,int[]>();int max=Math.Max(0,Math.Min(c.Rank,s.GetCardMaximumUpgradeLevel(ids[i]))-old[i]);foreach(var pair in dp)for(int add=0;add<=max;add++){int used=pair.Key+cost(old[i],add);if(used>c.Budget)continue;var levels=(int[])pair.Value.Clone();levels[i]=add;if(!next.ContainsKey(used)||merit(levels)>merit(next[used]))next[used]=levels;}dp=next;}
 Need(dp.ContainsKey(c.Budget),"unspendable incremental budget "+c.Id+" reachable="+string.Join(",",dp.Keys.OrderBy(x=>x)));int actual=0;
 for(int i=0;i<ids.Length;i++)for(int n=0;n<dp[c.Budget][i];n++){int from=s.GetCardUpgradeLevel(ids[i]),quote=s.GetCardUpgradeCost(Cards[ids[i]]);Need(s.TryUpgradeBattleCard(Cards[ids[i]],out string why),"upgrade "+ids[i]+" "+why);actual+=quote;currentUpgradeLedger.Add(new {id=ids[i],fromRank=from,toRank=s.GetCardUpgradeLevel(ids[i]),cost=quote,balanceAfter=s.ProfileGold});}
 Need(actual==c.Budget,"incremental paid upgrade total");return actual;
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

public static partial class BattleModes {
 static object lastBudgetLedger;static List<object> currentUpgradeLedger;
 static int ChooseSofteningCard(CoreVoyageSession s,double target){int best=-1;double score=.01;for(int h=0;h<s.Battle.Hand.Count;h++){if(!s.CanPlayCard(h))continue;var c=s.GetBattleCard(s.Battle.Hand[h]);int damage=Math.Max(0,NavalBalanceProbe.QuoteDamage(s,c)-s.Battle.EnemyBlock);double after=(s.Battle.EnemyHull-damage)/(double)s.Battle.EnemyMaxHull;if(damage>0&&after<.5)continue;if(s.GetNavalCardChoice(h)?.RequiresChoice==true&&NavalBalanceProbe.ChooseOptions(s,h)==null)continue;double v=NavalBalanceProbe.Score(s,h,true);if(damage>0&&after>=.5&&after<=.6)v+=1000-Math.Abs(after-target)*1000;if(v>score){score=v;best=h;}}return best>=0?best:ChooseManeuver(s);}
 static bool HasSafeSofteningAttack(CoreVoyageSession s,double target){for(int h=0;h<s.Battle.Hand.Count;h++){if(!s.CanPlayCard(h))continue;int damage=Math.Max(0,NavalBalanceProbe.QuoteDamage(s,s.GetBattleCard(s.Battle.Hand[h]))-s.Battle.EnemyBlock);double after=(s.Battle.EnemyHull-damage)/(double)s.Battle.EnemyMaxHull;if(damage>0&&after>=.5)return true;}return false;}

 static int ChooseApproach(CoreVoyageSession s){int best=-1;double value=.01;for(int h=0;h<s.Battle.Hand.Count;h++)if(s.CanPlayCard(h)){var c=s.GetBattleCard(s.Battle.Hand[h]);if(s.GetBattleCardDamage(c,s.Battle.Stage)>0)continue;double v=ContactScore(s,s.Battle.Hand[h]);if(v>value){value=v;best=h;}}return best;}
}

public static partial class BattleModes {
 static int ChooseManeuver(CoreVoyageSession s){bool needs=s.NavalDistanceCells>4||FieldRisk(s,s.Battle.PlayerPositionMeters)>0||s.NavalDistanceCells<=1;if(!needs)return -1;return Enumerable.Range(0,s.Battle.Hand.Count).Where(h=>s.CanPlayCard(h)&&s.GetBattleCard(s.Battle.Hand[h]).NavalActionPointGain>0).OrderByDescending(h=>s.GetBattleCard(s.Battle.Hand[h]).NavalActionPointGain).DefaultIfEmpty(-1).First();}
}

public static partial class BattleModes {
 static bool Fits(Cohort c,string boss){string stage=boss.Contains("shattered_sail_reef")?"starter":boss.Contains("golden_tide_trade")||new[]{"legendary_pirate_iron_oath","legendary_pirate_red_widow","legendary_pirate_thunder_crown","legendary_pirate_ember_ark"}.Contains(boss)?"patrol":"gunship";return c.Id==stage;}
}

public static partial class BattleModes {
 static bool QuotedSoftRamInBand(CoreVoyageSession s){int after=s.Battle.EnemyHull-s.AuditPlayerRamDamageQuote();return after>=s.Battle.EnemyMaxHull*.5&&after<=s.Battle.EnemyMaxHull*.6;}
 static bool SafeSoftRam(CoreVoyageSession s)=>QuotedSoftRamInBand(s)&&s.Hull>s.AuditPlayerRamSelfDamageQuote();
}

public static partial class BattleModes {
 static bool softContactActive,softBoardAllowed;
 static double SoftContactGeometryRisk(CoreVoyageSession s,VoyagePoint p) {
  if(!softContactActive||s.Battle.Stage!=NavalBattleStage.Naval||CoreVoyageSession.NavalGridDistance(p,s.Battle.EnemyPositionMeters)!=1)return 0;
  bool horizontal=System.Math.Abs(System.Math.Sin(s.Battle.EnemyHeadingDegrees*System.Math.PI/180))<.5;
  bool side=horizontal?System.Math.Abs(p.Y-s.Battle.EnemyPositionMeters.Y)>50:System.Math.Abs(p.X-s.Battle.EnemyPositionMeters.X)>50;
  if(s.NavalMovementPlannedSteps==0&&System.Math.Abs(p.X-s.Battle.PlayerPositionMeters.X)<.01&&System.Math.Abs(p.Y-s.Battle.PlayerPositionMeters.Y)<.01&&(!softBoardAllowed||!s.CanBoard(out _)))return 120;
  return side?0:120;
 }
}

public static partial class BattleModes {
 static int moveCannonThreat;static VoyagePoint moveEnemyPoint;
 static double CannonPositionRisk(CoreVoyageSession s,VoyagePoint p)=>s.Battle.Stage==NavalBattleStage.Naval&&moveCannonThreat>s.MaxHull*.12&&CoreVoyageSession.NavalGridDistance(p,moveEnemyPoint)<=CoreVoyageSession.NavalCannonRangeCells?moveCannonThreat*2:0;
 static string PolicyPose(CoreVoyageSession s) {var b=s.Battle;if(b==null||s.Phase!=CoreDemoPhase.Battle||b.Stage!=NavalBattleStage.Naval)return "";return " [p="+b.PlayerPositionMeters.X+","+b.PlayerPositionMeters.Y+" h="+b.PlayerHeadingDegrees+" e="+b.EnemyPositionMeters.X+","+b.EnemyPositionMeters.Y+" eh="+b.EnemyHeadingDegrees+" board="+s.CanBoard(out _)+" ram="+s.CanRam(out _)+" ramDamage="+s.AuditPlayerRamDamageQuote()+" next="+string.Join("|",s.GetNavalLegalNextSteps().Select(p=>p.X+","+p.Y))+"]";}
}

public static partial class BattleModes {
 static int ChoosePreBoardRepair(CoreVoyageSession s)=>System.Linq.Enumerable.Range(0,s.Battle.Hand.Count).Where(h=>s.CanPlayCard(h)&&s.GetBattleCardEffects(s.GetBattleCard(s.Battle.Hand[h]),NavalBattleStage.Naval).ActualRepair>0&&(s.GetNavalCardChoice(h)?.RequiresChoice!=true||NavalBalanceProbe.ChooseOptions(s,h)!=null)).OrderByDescending(h=>s.GetBattleCardEffects(s.GetBattleCard(s.Battle.Hand[h]),NavalBattleStage.Naval).ActualRepair/(s.GetBattleCardEnergyCost(s.Battle.Hand[h])+.65)).DefaultIfEmpty(-1).First();
}

public static partial class BattleModes {
 static int UpgradeCurseSensitivity(CoreVoyageSession s,string[] ids,Cohort c) {
  Need(c.Budget==720||c.Budget==1200,"fixed curse sensitivity budget");int total=0;
  foreach(string id in ids) {int target=c.Budget==720?1:(id=="card_boarding_curse_02"||id=="card_boarding_curse_06"?2:1);Need(s.GetCardUpgradeLevel(id)==0&&target<=s.GetCardMaximumUpgradeLevel(id),"curse common rank/cap "+id);
   while(s.GetCardUpgradeLevel(id)<target) {int from=s.GetCardUpgradeLevel(id),cost=s.GetCardUpgradeCost(Cards[id]);Need(s.TryUpgradeBattleCard(Cards[id],out string why),"curse paid upgrade "+id+" "+why);total+=cost;currentUpgradeLedger.Add(new{id,fromRank=from,toRank=s.GetCardUpgradeLevel(id),cost,balanceAfter=s.ProfileGold});}}
  Need(total==c.Budget,"curse exact incremental spend");return total;
 }
 static double CurseMarkValue(CoreVoyageSession s,CoreBattleCard card,int rawDamage) {
  var b=s.Battle;int marks=b.Boarding.CurseMarks,added=CoreBoardingCardBalance.Marks(card.BoardingSpec.Key,card.UpgradeLevel),threshold=b.Schools.EnemyCurseExecutionThresholdPercent;int afterHealth=Math.Max(0,b.EnemyBoardingHealth-Math.Max(0,rawDamage-b.EnemyBlock));
  if(CoreCurseRules.ReachesExecutionThreshold(marks+added,afterHealth,threshold))return 100000;
  double value=added*100d/Math.Max(5,threshold);int remaining=b.Energy-s.GetBattleCardEnergyCost(Cards[card.Id]);
  foreach(int ix in b.Hand){var next=s.GetBattleCard(ix);if(s.GetBattleCardEnergyCost(ix)>remaining)continue;
   if(next.BoardingSpec?.Key=="curse_06"){value+=CoreCardUpgradeDesign.ScaleEffect(next,12+marks+added,next.UpgradeLevel)-CoreCardUpgradeDesign.ScaleEffect(next,12+marks,next.UpgradeLevel);remaining-=s.GetBattleCardEnergyCost(ix);}
   if(next.BoardingSpec?.Key=="curse_12"&&marks<4&&marks+added>=4&&s.Cargo.Medicine>0){value+=Math.Max(0,Math.Min(b.PlayerMaxBoardingHealth-b.PlayerBoardingHealth,CoreCardUpgradeDesign.ScaleEffect(next,CoreBoardingCardBalance.Linear(13),next.UpgradeLevel))-2);remaining-=s.GetBattleCardEnergyCost(ix);}
  }return value;
 }
}

public static partial class BattleModes {
 static double SoftManeuverSetupRisk(CoreVoyageSession s){if(!softContactActive)return 0;var b=s.Battle;int d=CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters);if(d<=1)return 0;var front=new VoyagePoint(b.PlayerPositionMeters.X+(float)Math.Round(Math.Cos(b.PlayerHeadingDegrees*Math.PI/180))*100,b.PlayerPositionMeters.Y+(float)Math.Round(Math.Sin(b.PlayerHeadingDegrees*Math.PI/180))*100);return CoreVoyageSession.NavalGridDistance(front,b.EnemyPositionMeters)>=d?60:0;}
 static int SoftTwoStepDistance(CoreVoyageSession s,VoyagePoint first,float heading){int best=int.MaxValue;foreach(float turn in new[]{0f,90f,-90f}){double h=(heading+turn)*Math.PI/180;var next=new VoyagePoint(first.X+(float)Math.Round(Math.Cos(h))*100,first.Y+(float)Math.Round(Math.Sin(h))*100);if(s.AuditTideBlocksStep(first,next)||CoreVoyageSession.NavalGridDistance(next,s.Battle.EnemyPositionMeters)==0)continue;best=Math.Min(best,CoreVoyageSession.NavalGridDistance(next,s.Battle.EnemyPositionMeters));}return best==int.MaxValue?100:best;}
}

public static partial class BattleModes {static bool pureCannonRun,safeCannonOrigin;static int safeReentryRejects;}
