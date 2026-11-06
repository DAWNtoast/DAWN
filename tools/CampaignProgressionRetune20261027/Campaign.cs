using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Content;
using ShatteredSail.Presentation;

internal static partial class Program
{
    static readonly Stopwatch clock=Stopwatch.StartNew();
    static readonly Dictionary<string,object> firstKills=new Dictionary<string,object>();
    static readonly Dictionary<string,object> levelUnlocks=new Dictionary<string,object>();
    static readonly List<object> bossAttempts=new List<object>();
    static readonly List<object> voyageProgress=new List<object>();
    static readonly Dictionary<int,int> routeAttemptDevelopmentVoyages=new Dictionary<int,int>();
    static int effectiveDevelopmentVoyages;
    static object tutorialFirstKill;
    static readonly Dictionary<int,int> routeAttemptTrips=new Dictionary<int,int>(), routeAttemptPower=new Dictionary<int,int>();
    static readonly Dictionary<int,int> routeProbeTrips=new Dictionary<int,int>(), routeProbePower=new Dictionary<int,int>();
    static readonly Dictionary<string,int> routeFailuresByShip=new Dictionary<string,int>();
    static readonly HashSet<int> ownedShips=new HashSet<int>{0};
    static readonly HashSet<string> boughtOffers=new HashSet<string>();
    static readonly Dictionary<string,decimal> incomes=new Dictionary<string,decimal>(),expenses=new Dictionary<string,decimal>();
    static ComponentArtCatalog componentCatalog;
    static OwnedShipComponentInventory equipment;
    static readonly CoreWarehouseCollection collection=new CoreWarehouseCollection();
    static StreamWriter money;
    static decimal lastAccount,lastTravel,lastEscrow,initialCash,totalIncome,totalExpense,totalLoss;
    static DateTime startDate;
    static object primaryCompletion;
    static bool adaptiveCurseDeck;
    static string purchasePolicy="original";
    static bool DirectPurchasePolicy=>purchasePolicy!="original";
    static bool LearningPolicy=>purchasePolicy=="learning";
    static string learningDeck="neutral-sustain";
    static int learningBossLosses;
    static int learningProgressTrip,learningProgressKills,learningProgressShip;
    static readonly string[] learningUpgradeIds={"card_boarding_tide_02","card_boarding_tide_06","card_boarding_blood_06","card_boarding_basic_07"};
    static int ReadinessScore()=>s.MaxHull+s.PirateLevel*10+upgradeIds.Concat(learningUpgradeIds).Distinct().Sum(id=>s.GetCardUpgradeLevel(id))*20;
    static decimal failureLoss,settlementRetentionLoss;
    static decimal Escrow=>s.Phase==CoreDemoPhase.Settlement?s.Settlement?.BankedGold??0:0;
    static decimal Cash=>s.ProfileGold+s.Cargo.Gold+Escrow;
    static readonly string[] upgradeIds={"card_boarding_basic_02","card_boarding_basic_06","card_boarding_basic_09","card_boarding_basic_05","card_boarding_basic_13","card_emergency_repair","card_brace"};
    static int Main(string[] args)
    {
        output=args[0]; maxCycles=args.Length>1?int.Parse(args[1]):200;mode="current-source-no-offline";purchasePolicy=args.Length>3?args[3]:"original";
        Directory.CreateDirectory(output); UnityEngine.Resources.Root=args[2];
        actions=new StreamWriter(Path.Combine(output,"actions.csv"),false,new UTF8Encoding(true));
        battles=new StreamWriter(Path.Combine(output,"battles.csv"),false,new UTF8Encoding(true));
        voyages=new StreamWriter(Path.Combine(output,"voyages.csv"),false,new UTF8Encoding(true));
        money=new StreamWriter(Path.Combine(output,"economy.csv"),false,new UTF8Encoding(true));
        actions.WriteLine("action,trip,date,phase,port,ship,actionType,detail,accountGold,travelGold,water,food,powder,cannonballs,wood,medicine,beer,iron,cloth,gem,ancientMaterial,relic,blackPearl,cargoVolume,capacity,hull,maxHull,morale,ordinaryCrew,specialCrew,pirateLevel,pirateXP,fragments,voyageFragments,clues,voyageHours,distanceNm,x,y,coverage");
        battles.WriteLine("trip,enemyId,enemyName,stage,outcome,turns,cards,powderUsed,ballsUsed,initialHull,minimumHull,finalHull,crewHealth,initialFame,finalFame,fameDelta,netGoldReward");
        voyages.WriteLine("trip,goal,seed,rank,departure,destination,outcome,hours,days,distanceNm,coverage,completedEvents,bankedGold,bankedFragments,bankedXP,treasureCompleted,accountGold,water,food");
        money.WriteLine("action,trip,category,event,detail,accountDelta,travelDelta,settlementEscrowDelta,netDelta,income,expense,failureLoss,internalTransfer,accountBalance,travelBalance,pendingSettlementGold,totalCash,totalIncome,totalExpense,totalLoss,realSeconds,gameDate");
        s=new CoreVoyageSession();s.ConfigureCrewCapabilities(false,false,false,false,false,false);
        s.ConfigureSpecialCardSources(Array.Empty<string>(),Array.Empty<string>());s.ConfigureDepartureCrew(10,0);
        startDate=CoreVoyageSession.NewCampaignStartDate(new DateTime(2026,10,27));
        Need(s.RestoreGameClock((startDate-CoreVoyageSession.GameClockOrigin).TotalHours),"initial front-end game clock");
        componentCatalog=new ComponentArtCatalog();equipment=new OwnedShipComponentInventory(componentCatalog,()=>"audit-part-"+Guid.NewGuid().ToString("N"));equipment.InitializeEmptyLoadout(0);
        File.WriteAllText(Path.Combine(output,"component-catalog.json"),new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(componentCatalog.ExpandedEntries.Select(x=>new{id=x.Id,name=x.Name,rarity=x.Rarity.ToString(),slot=x.Slot.ToString()}).ToArray()),new UTF8Encoding(false));
        RefreshEquipment();s.RecordCollectionPortVisit(s.CurrentPort);
        initialCash=Cash;lastAccount=s.ProfileGold;lastTravel=s.Cargo.Gold;
        int code=1;
        try
        {
            Mark("Fresh campaign; real new-profile balance and cargo; bare starter hull; no offline income");
            Tutorial();
            var tutorialPart=equipment.Grant(componentCatalog.GetById("bow_sawtooth_drift"));
            Need(tutorialPart!=null,"tutorial component catalog");Need(equipment.TryEquip(s.SelectedShipIndex,tutorialPart.InstanceId),"fit earned tutorial component");
            collection.Grant(CoreWarehouseCollection.TutorialTreasureId);collection.TryDisplay(CoreWarehouseCollection.TutorialTreasureId,0);RefreshEquipment();
            Log("TutorialReward","Confirmed tutorial return awards drift bow and compass; player equips/displays earned objects");
            for(int cycle=0;(cycle<maxCycles||LearningPolicy)&&!AllBossesDone();cycle++)
            {
                if(LearningPolicy&&cycle>0&&cycle%maxCycles==0)PauseLearning("Reached "+cycle+" normal voyages; inspect before extending the same continuous campaign");
                if(LearningPolicy)CheckLearningProgress();
                PreparePort();Identify();
                int route=ChooseRoute();s.SelectRoute(route);Need(s.SelectedRouteIndex==route,"route level gate");
                int targetShip=DirectPurchasePolicy&&!ownedShips.Contains(2)?2:s.SelectedShipIndex==0?2:7;
                bool travel=s.SelectedShipIndex!=7&&s.ProfileGold>CoreShipMarketCatalog.PurchaseCost(targetShip)+(DirectPurchasePolicy?400:600)&&s.CurrentPort!="铜钟港";
                bool recovery=LearningPolicy&&s.Cargo.Medicine+Remaining(PortMarketItem.Medicine)<4;
                if(LearningPolicy&&(travel||recovery)){route=0;s.SelectRoute(0);}
                voyageGoal=recovery?"medicine-recovery":travel?"travel-to-shipyard":"boss-progression";
                if(travel)Need(s.SelectDestinationPort("铜钟港"),"select shipyard destination");
                else if(recovery)Need(s.SelectDestinationPort(CoreVoyageSession.DestinationPorts.Where(p=>p!=s.CurrentPort).OrderByDescending(p=>RemainingAt(p,PortMarketItem.Medicine)).First()),"select public stocked harbor");
                else if(s.SelectedDestinationPort==s.CurrentPort)Need(s.SelectDestinationPort(s.CurrentPort=="断锚港"?"白贝港":"断锚港"),"distinct destination");
                Supplies(false);string seed=CoreVoyageSession.CreateChartSeed(route,s.CurrentPort,s.SelectedDestinationPort);
                Need(s.TrySelectChartSeed(seed,out string why),why);trip++;s.StartVoyage();routeProbeTrips[route]=trip;routeProbePower[route]=ReadinessScore();Log("DepartExplore",seed);Navigate(false);
                if(s.Phase==CoreDemoPhase.Settlement)Settle();else throw new InvalidOperationException("Voyage did not settle: "+s.Phase);
            }
            code=AllBossesDone()?0:2;Mark("Campaign stopped with "+firstKills.Count+"/13 target bosses and "+legends.Count+"/6 legendary wins");
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Console.Error.WriteLine(e);Log("Stop",e.Message);}
        finally{Save();actions.Dispose();battles.Dispose();voyages.Dispose();money.Dispose();}
        return code;
    }
    static bool AllBossesDone()=>CoreRouteBossCatalog.All.All(x=>firstKills.ContainsKey(x.Id))&&CoreLegendaryWarships.All.All(x=>firstKills.ContainsKey(x.Id))&&firstKills.ContainsKey(CoreBattleBalance.TreasureBossId);
    static void CheckLearningProgress()
    {
        if(firstKills.Count!=learningProgressKills||s.SelectedShipIndex!=learningProgressShip){learningProgressTrip=trip;learningProgressKills=firstKills.Count;learningProgressShip=s.SelectedShipIndex;}
        if(trip-learningProgressTrip<6)return;
        PauseLearning("Six voyages without new first kill or ship upgrade; inspect development and retry constraints");
    }
    static void PauseLearning(string reason)
    {
        string pause=Path.Combine(output,"live-pause.json"),resume=Path.Combine(output,"resume.signal");
        Mark("Learning watchdog: "+reason+"; live process paused with complete campaign memory; no new voyage until inspected");
        File.WriteAllText(pause,new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=100}.Serialize(new{reason,resume="Create resume.signal only after inspecting this run; continuing preserves exact complete process state",state=CampaignState("live-paused"),processId=Process.GetCurrentProcess().Id}),new UTF8Encoding(false));
        actions.Flush();battles.Flush();voyages.Flush();money.Flush();
        while(!File.Exists(resume))System.Threading.Thread.Sleep(500);
        string explanation=File.ReadAllText(resume);File.Delete(resume);File.Delete(pause);learningProgressTrip=trip;Log("LearningResume",explanation);
    }
    static int ChooseRoute()
    {
        // A separate observed-learning route: try a newly unlocked chart at once;
        // after a failed trial, revisit after a real upgrade and at least two trips.
        // These are player policy decisions, never additional game unlock gates.
        if(purchasePolicy=="unlock-probe"||LearningPolicy)
            for(int r=2;r>=1;r--)
                if(!AvoidRepeatedFailure(r)&&s.CanSelectRoute(r,out _)&&CoreRouteBossCatalog.ForRoute(CoreVoyageSession.Routes[r].Id).Any(x=>!firstKills.ContainsKey(x.Id))&&
                    (!routeProbeTrips.ContainsKey(r)||trip-routeProbeTrips[r]>=2&&ReadinessScore()>routeProbePower[r]))return r;
        for(int r=0;r<3;r++)
            if(!AvoidRepeatedFailure(r)&&s.CanSelectRoute(r,out _)&&CoreRouteBossCatalog.ForRoute(CoreVoyageSession.Routes[r].Id).Any(x=>!firstKills.ContainsKey(x.Id)))
                return r==0||s.SelectedShipIndex==7||r==1&&s.SelectedShipIndex==2?r:0;
        return s.CanSelectRoute(2,out _)&&s.SelectedShipIndex==7&&!AvoidRepeatedFailure(2)?2:0;
    }
    static bool AvoidRepeatedFailure(int route)
    {
        if(!(purchasePolicy=="unlock-probe"||LearningPolicy)||route<1||s.SelectedShipIndex==7||!routeFailuresByShip.TryGetValue(route+"/"+s.SelectedShipIndex,out int failures)||failures<2)return false;
        if(!LearningPolicy)return true;
        return !routeAttemptDevelopmentVoyages.ContainsKey(route)||effectiveDevelopmentVoyages-routeAttemptDevelopmentVoyages[route]<2||ReadinessScore()<=routeAttemptPower[route];
    }
    static bool ReadyForBoss(CoreEncounter e)
    {
        if(LearningPolicy&&s.Hull<s.MaxHull*.60)return false;
        if(LearningPolicy&&voyageGoal=="medicine-recovery"&&s.SelectedRouteIndex!=0)return false;
        if((purchasePolicy=="unlock-probe"||LearningPolicy)&&e.BossKind==CoreBossKind.Route&&s.SelectedRouteIndex>=1)return s.Cargo.Medicine>=4;
        if(e.BossKind==CoreBossKind.Route&&s.SelectedRouteIndex==0)return s.GetCardUpgradeLevel("card_boarding_basic_02")>=1;
        return (s.SelectedShipIndex==7 || s.SelectedShipIndex==2 && (s.SelectedRouteIndex==1 || e.Id=="legendary_pirate_iron_oath" || e.Id=="legendary_pirate_red_widow")) && s.GetCardUpgradeLevel("card_boarding_basic_06")>=2;
    }
    static object CampaignState(string note)=>new{note,action=sequence,trip,boardingStrategy=LearningPolicy?learningDeck:adaptiveCurseDeck&&s.SelectedShipIndex>=2?"neutral-tide-sustain":"neutral-sustain",realElapsedSeconds=clock.Elapsed.TotalSeconds,gameElapsedHours=(s.GameDateTime-startDate).TotalHours,sailedHours=totalHours+(s.Phase==CoreDemoPhase.Port||s.Phase==CoreDemoPhase.Settlement?0:s.TotalHours),sailedNm=totalDistance+(s.Phase==CoreDemoPhase.Port||s.Phase==CoreDemoPhase.Settlement?0:s.TotalDistanceNm),date=s.GameDateTime.ToString("o"),port=s.CurrentPort,phase=s.Phase.ToString(),totalIncome,totalExpense,totalLoss,failureLoss,settlementRetentionLoss,initialCash,accountGold=s.ProfileGold,travelGold=s.Cargo.Gold,pendingSettlementGold=Escrow,totalCash=Cash,incomeCategories=incomes.ToDictionary(x=>x.Key,x=>x.Value),expenseCategories=expenses.ToDictionary(x=>x.Key,x=>x.Value),ship=new{id=s.SelectedShip.Id,name=s.SelectedShip.Name,index=s.SelectedShipIndex,hull=s.Hull,maxHull=s.MaxHull,armor=s.EffectiveArmor,firepower=s.EffectiveFirepower,speed=s.SpeedKnots,capacity=s.CargoCapacityValue},components=equipment.Items.Select(x=>new{x.InstanceId,x.ComponentId,x.ShipIndex}).ToArray(),crew=crewIds.ToArray(),ordinaryCrew=s.SelectedOrdinaryCrew,pirateLevel=s.PirateLevel,pirateXP=s.PirateExperience,deck=Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>().ToDictionary(x=>x.ToString(),x=>s.GetBattleDeck(x).Select(i=>new{id=s.GetBattleCard(i).Id,name=s.GetBattleCard(i).Name,upgrade=s.GetCardUpgradeLevel(s.GetBattleCard(i).Id)}).ToArray()),bonusDeck=Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>().ToDictionary(x=>x.ToString(),x=>s.GetBonusBattleCards(x).Select(i=>new{id=s.GetBattleCard(i).Id,name=s.GetBattleCard(i).Name,upgrade=s.GetCardUpgradeLevel(s.GetBattleCard(i).Id)}).ToArray()),specialCardXP=s.SpecialCardExperience.ToDictionary(x=>x.Key,x=>x.Value),collection=collection.OwnedIds.ToArray(),collectionDisplayed=collection.ExportDisplay(),cargo=new{s.Cargo.Water,s.Cargo.Food,s.Cargo.Powder,s.Cargo.Cannonballs,s.Cargo.Wood,s.Cargo.Medicine},ownedShips=ownedShips.ToArray()};
    static void Ledger(string action,string detail)
    {
        if(money==null||s==null)return;
        decimal account=s.ProfileGold-lastAccount,travel=s.Cargo.Gold-lastTravel,escrow=Escrow-lastEscrow,delta=account+travel+escrow;
        if(delta==0&&account==0&&travel==0)return;
        string category=delta>0?(action=="Sale"?"sale":action=="PlayCard"||action=="EndTurn"||action=="Ram"||action=="Board"?"battle-reward":action=="ResolveChoice"?"encounter-reward":"other-earned"):
            delta<0?(s.Phase==CoreDemoPhase.Settlement?(s.Settlement?.Outcome==VoyageOutcome.Failure?"failure-loss":"settlement-retention-loss"):action=="Purchase"?"supplies":action=="PaidCardUpgrade"?"card-upgrade":action=="Recruit"?"crew-contract":action=="ShipPurchase"?"ship":action=="ComponentPurchase"?"component":action=="ResolveChoice"?"encounter-cost":"other-cost"):"internal-transfer";
        decimal income=Math.Max(0,delta),loss=category=="failure-loss"||category=="settlement-retention-loss"?-delta:0,expense=delta<0&&loss==0?-delta:0;
        totalIncome+=income;totalExpense+=expense;totalLoss+=loss;if(category=="failure-loss")failureLoss+=loss;if(category=="settlement-retention-loss")settlementRetentionLoss+=loss;
        if(income>0){if(!incomes.ContainsKey(category))incomes[category]=0;incomes[category]+=income;}
        if(expense>0){if(!expenses.ContainsKey(category))expenses[category]=0;expenses[category]+=expense;}
        Row(money,sequence+1,trip,category,action,detail,account,travel,escrow,delta,income,expense,loss,delta==0,s.ProfileGold,s.Cargo.Gold,Escrow,Cash,totalIncome,totalExpense,totalLoss,clock.Elapsed.TotalSeconds,s.GameDateTime.ToString("o"));
        lastAccount=s.ProfileGold;lastTravel=s.Cargo.Gold;lastEscrow=Escrow;
        Need(initialCash+totalIncome-totalExpense-totalLoss==Cash,"cash reconciliation");
    }
    static void SaveCampaign()
    {
        if(s==null||equipment==null)return;
        var json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=100};
        File.WriteAllText(Path.Combine(output,"campaign.json"),json.Serialize(new{completed=AllBossesDone(),targetCount=13,purchasePolicy,policyVersion="progression-retune-learning-v1",primaryCompletion,firstKills,tutorialFirstKill,levelUnlocks,bossAttempts,voyageProgress,effectiveDevelopmentVoyages,final=CampaignState("current"),offlineIncome=0,policy="visible map and current hand only; no injected resources, XP, ships, deck order, victory, navigation teleport or future draw access",nonDeterminism="Actual production chart salts, route boss roster and boss loot use Guid; generated seeds and observed actions are recorded, RNG is not overridden."}),new UTF8Encoding(false));
    }
    static void RefreshEquipment()
    {
        if(equipment==null)return;var entries=equipment.Items.Where(x=>x.ShipIndex==s.SelectedShipIndex).Select(x=>componentCatalog.GetById(x.ComponentId)).ToArray();
        s.ConfigureComponentBonuses(entries.Sum(x=>x.HullBonus-componentCatalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).HullBonus),entries.Sum(x=>x.ArmorBonus-componentCatalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).ArmorBonus),entries.Sum(x=>x.FirepowerBonus-componentCatalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).FirepowerBonus),entries.Sum(x=>x.SpeedBonus-componentCatalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).SpeedBonus),entries.Sum(x=>x.VisionBonus-componentCatalog.Get((int)x.Slot*ComponentArtCatalog.EntriesPerSlot).VisionBonus),true);
        float quality(ComponentSlot slot){var e=entries.FirstOrDefault(x=>x.Slot==slot);return e==null?0:1+(int)e.Rarity;}
        s.ConfigureBattleEquipment(quality(ComponentSlot.Bow),quality(ComponentSlot.Plating));s.ConfigureSpecialCardSources(crewIds.ToArray(),entries.Where(x=>x.Rarity>=ComponentRarity.Rare).Select(x=>x.Id).ToArray());
    }
    static void RefreshEnvironment()
    {
        var effects=equipment.Items.Where(x=>x.ShipIndex==s.SelectedShipIndex).Select(x=>componentCatalog.GetById(x.ComponentId).Evaluate(ComponentVoyageContextResolver.FromSession(s))).ToArray();
        s.ConfigureComponentContextEffect(effects.Sum(x=>x.HullBonus),effects.Sum(x=>x.ArmorBonus),effects.Sum(x=>x.FirepowerBonus),effects.Sum(x=>x.SpeedBonus),effects.Sum(x=>x.VisionBonus),effects.Aggregate(1f,(a,x)=>a*Math.Max(.1f,x.RationMultiplier)));
    }
    static void PreparePort()
    {
        Need(s.Phase==CoreDemoPhase.Port,"port preparation");
        while(BoardingComponentDropSelection.TryGrantNext(s,componentCatalog.ExpandedEntries,e=>equipment.Grant(e)!=null,out ComponentArtEntry drop)){s.RecordBoardingComponentDrop(drop.Name);Log("EarnedComponent",drop.Id);}
        foreach(var item in PortMarketCatalog.AllItems){string key=PortMarketCatalog.Key(item);int keep=key=="water"||key=="food"?int.MaxValue:key=="wood"?16:key=="medicine"?12:key=="powder"?20:key=="cannonballs"?20:0;int q=Math.Max(0,s.GetMarketCargoAmount(key)-keep);if(q>0){Need(s.TrySellMarketCargo(key,q,PortMarketCatalog.CalculateSellRevenue(s.CurrentPort,item,s.GameDateTime,q)),"sell "+key);Log("Sale",key+" x"+q);}}
        foreach(var item in CoreWarehouseItemCatalog.RareItems){int q=s.GetWarehouseCargoAmount(item.Key);if(q>0){Need(s.TryDepositPortCargo(item.Key,q),"warehouse deposit");Log("WarehouseDeposit",item.Key+" x"+q);}}
        int desired=s.SelectedShipIndex==7?-1:s.CurrentPort=="铜钟港"?(!ownedShips.Contains(2)?2:7):DirectPurchasePolicy?-1:s.SelectedShipIndex==0&&s.CurrentPort=="灰鸥港"?1:s.SelectedShipIndex==0&&s.CurrentPort=="断锚港"?4:-1;
        if(desired>=0&&!ownedShips.Contains(desired)&&s.ProfileGold>CoreShipMarketCatalog.PurchaseCost(desired)+(DirectPurchasePolicy?400:600)&&s.CanSelectShip(desired)&&CoreShipMarketCatalog.IsSoldAtPort(CoreVoyageSession.DestinationPorts.ToList().IndexOf(s.CurrentPort),desired))
        {Need(s.TrySpendProfileGold(CoreShipMarketCatalog.PurchaseCost(desired),"船坞购船"),"ship purchase");s.RegisterShipAtCurrentPort(desired);ownedShips.Add(desired);equipment.EnsureLoadout(desired);Need(s.SelectShip(desired),"select paid ship");RefreshEquipment();Log("ShipPurchase",s.SelectedShip.Name);}
        if(s.ProfileGold>1000)Hire(4);if(s.ProfileGold>1500)Hire(1);if(s.ProfileGold>1500)Hire(2);if(s.ProfileGold>2000)Hire(5);Crew();
        var ships=new[]{"card_approach","card_approach","card_full_sail_turn","card_full_sail_turn","card_emergency_repair","card_emergency_repair","card_brace","card_brace","card_energy_order","card_energy_order"};
        var boards=new[]{"card_boarding_basic_02","card_boarding_basic_02","card_boarding_basic_05","card_boarding_basic_05","card_boarding_basic_06","card_boarding_basic_06","card_boarding_basic_09","card_boarding_basic_09","card_boarding_basic_13","card_boarding_basic_13"};
        if(adaptiveCurseDeck&&s.SelectedShipIndex>=2){boards=new[]{"card_boarding_basic_02","card_boarding_basic_02","card_boarding_basic_13","card_boarding_basic_13","card_boarding_basic_09","card_boarding_basic_09","card_boarding_tide_02","card_boarding_tide_02","card_boarding_basic_06","card_boarding_basic_06"};Log("StrategyChange","After an observed boss defeat on a larger ship, retain direct damage and replace basic shields with paid-upgrade tide shields; no global threshold-only switch");}
        if(LearningPolicy&&s.PirateLevel>=CoreSchoolUnlockCatalog.Find("tide").RequiredLevel){bool bloodReady=s.PirateLevel>=CoreSchoolUnlockCatalog.Find("blood").RequiredLevel;string damage=bloodReady?"card_boarding_blood_06":"card_boarding_basic_02";boards=new[]{damage,damage,"card_boarding_tide_06","card_boarding_tide_06","card_boarding_basic_09","card_boarding_basic_09","card_boarding_tide_02","card_boarding_tide_02","card_boarding_basic_06","card_boarding_basic_07"};string strategy=bloodReady?"tide-counter-lifesteal-continuous-heal":"tide-counter-continuous-heal";if(strategy!=learningDeck){learningDeck=strategy;Log("StrategyChange","Learning policy: "+strategy+"; legal unlocked cards convert absorbed tide into direct damage; medicine-saving sustain; no threshold-only build");}}
        int[] ix(string[] ids)=>ids.Select(id=>Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(i=>CoreVoyageSession.CardLibrary[i].Id==id)).ToArray();
        Need(s.SetBattleDeck(NavalBattleStage.Preparation,ix(ships)),"legal preparation deck");Need(s.SetBattleDeck(NavalBattleStage.Naval,ix(ships)),"legal naval deck");Need(s.SetBattleDeck(NavalBattleStage.Boarding,ix(boards)),"legal boarding deck");Log("EditDeck","public SetBattleDeck validates current unlocks and stage limits");
        // Spend on the equipped build, keeping an explicit ordinary supply reserve.
        // This policy changes purchases, never prices, requirements or earned money.
        var activeUpgrades=LearningPolicy?boards.Distinct().OrderBy(id=>s.GetCardUpgradeLevel(id)).Concat(new[]{"card_emergency_repair","card_brace"}):upgradeIds.AsEnumerable();
        foreach(string id in activeUpgrades){int index=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(i=>CoreVoyageSession.CardLibrary[i].Id==id);int cost=s.GetCardUpgradeCost(index);if(cost>0&&s.ProfileGold>cost+200){if(s.TryUpgradeBattleCard(index,out string reason))Log("PaidCardUpgrade",id+" cost="+cost);else Log("UpgradeUnavailable",id+" reason="+reason);}else Log("UpgradeStatus",id+" current="+s.GetCardUpgradeLevel(id)+" max="+s.GetCardMaximumUpgradeLevel(index)+" cost="+cost);}
        foreach(ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot))){var best=equipment.Items.Where(x=>x.ShipIndex==-1&&componentCatalog.GetById(x.ComponentId).Slot==slot).OrderByDescending(x=>(int)componentCatalog.GetById(x.ComponentId).Rarity).FirstOrDefault();var old=equipment.Installed(s.SelectedShipIndex,slot);if(best!=null&&(old==null||componentCatalog.GetById(best.ComponentId).Rarity>componentCatalog.GetById(old.ComponentId).Rarity)){Need(equipment.TryEquip(s.SelectedShipIndex,best.InstanceId),"equip owned reward");Log("EquipEarnedComponent",best.ComponentId);}}
        if(LearningPolicy&&s.SelectedShipIndex==7&&learningBossLosses>0)BuyLearningEquipment();
        RefreshEquipment();RefreshCollection();
    }
    static void BuyLearningEquipment()
    {
        // Read the same public monthly inventory as the port screen and pay every
        // contract at its actual price; only replace a strictly lower rarity slot.
        foreach(var offer in PortComponentShopCatalog.BuildExpandedStock(componentCatalog,s.CurrentPort,s.GameDateTime).OrderByDescending(x=>(int)x.Entry.Rarity))
        {
            var old=equipment.Installed(s.SelectedShipIndex,offer.Entry.Slot);
            if(boughtOffers.Contains(offer.Id)||offer.Entry.Rarity<ComponentRarity.Rare||old!=null&&componentCatalog.GetById(old.ComponentId).Rarity>=offer.Entry.Rarity||s.ProfileGold<offer.Price+1500)continue;
            Need(PortComponentShopCatalog.IsCurrentOffer(offer,s.CurrentPort,s.GameDateTime),"current component offer");
            Need(s.TrySpendProfileGold(offer.Price,"船坞组件购买"),"pay component shop price");var owned=equipment.Grant(offer.Entry);Need(owned!=null,"grant paid component");boughtOffers.Add(offer.Id);Log("ComponentPurchase",offer.Entry.Id+" price="+offer.Price+" offer="+offer.Id);Need(equipment.TryEquip(s.SelectedShipIndex,owned.InstanceId),"equip purchased component");Log("EquipPurchasedComponent",offer.Entry.Id);
        }
    }
    static void RefreshCollection()
    {
        s.RecordCollectionPortVisit(s.CurrentPort);var facts=new CoreCollectionFacts{recruitedCrewIds=crewIds.ToArray(),crewCardLevels=crewIds.ToDictionary(id=>id,id=>s.GetCardUpgradeLevel(CoreSpecialCardCatalog.ForCrew(id).Card.Id))};
        bool changed;do{changed=false;var ids=new HashSet<string>(collection.OwnedIds);foreach(var rule in CoreCollectionRewardCatalog.All)if(!ids.Contains(rule.Id)&&rule.IsSatisfied(s.CaptureCollectionProgress(),facts,ids)&&collection.Grant(rule.Id)){changed=true;Log("CollectionEarned",rule.Id);}}while(changed);
    }
    static bool CombatMove()
    {
        if(!s.NavalActionMovementActive||s.Battle.NavalActionPoints<=0)return false;
        VoyagePoint target=s.Battle.EnemyPositionMeters;
        int distance(VoyagePoint p)=>(int)Math.Round((Math.Abs(p.X-target.X)+Math.Abs(p.Y-target.Y))/100f);
        if(distance(s.Battle.PlayerPositionMeters)<=1)return false;
        bool appended=false;int before=distance(s.Battle.PlayerPositionMeters);
        for(int i=0;i<5;i++)
        {
            var steps=s.GetNavalLegalNextSteps().Where(p=>distance(p)>0).OrderBy(distance).ToArray();
            if(steps.Length==0||distance(steps[0])>=before)break;
            Need(s.TryAppendNavalMovementStep(steps[0],out string why),why);appended=true;before=distance(steps[0]);if(before<=1)break;
        }
        if(!appended)return false;
        Need(s.CommitNavalMovement(out string reason),reason);
        if(s.Battle.NavalAwaitingHeadingSelection){var p=s.Battle.PlayerPositionMeters;float angle=Math.Abs(target.X-p.X)>=Math.Abs(target.Y-p.Y)?(target.X>=p.X?0:180):(target.Y>=p.Y?90:270);var headings=s.GetNavalArrivalHeadings();float chosen=headings.OrderBy(h=>Math.Abs(((h-angle+540)%360)-180)).First();Need(s.ConfirmNavalArrivalHeading(chosen,out reason),reason);}
        return true;
    }
}
