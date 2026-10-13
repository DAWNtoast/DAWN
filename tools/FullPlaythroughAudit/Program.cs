using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Content;
using ShatteredSail.Presentation;

internal static class Program
{
    static CoreVoyageSession s;
    static StreamWriter actions, battles, voyages;
    static string output, mode;
    static int sequence, trip, maxCycles, battleCount, losses, navigationSteps, blockedChoices;
    static double totalHours,totalDistance;
    static readonly List<object> milestones=new List<object>();
    static readonly HashSet<string> ranks=new HashSet<string>();
    static readonly HashSet<string> encounters=new HashSet<string>();
    static readonly HashSet<string> resolved=new HashSet<string>();
    static readonly HashSet<string> legends=new HashSet<string>();
    static readonly Dictionary<string,List<int>> choiceHistory=new Dictionary<string,List<int>>();
    static readonly HashSet<string> fragmentNames=new HashSet<string>();
    static readonly HashSet<string> clueNames=new HashSet<string>();
    static readonly List<string> crewIds=new List<string>();
    static readonly Dictionary<string,int> marketBought=new Dictionary<string,int>();
    static readonly List<object> shortages=new List<object>();
    static string voyageGoal;
    static int minWater=int.MaxValue,minFood=int.MaxValue,minPowder=int.MaxValue,minBalls=int.MaxValue;
    static decimal minGold=decimal.MaxValue;
    static bool coverageObjective;
    static string Q(object o)=>"\""+Convert.ToString(o,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";
    static void Row(StreamWriter writer,params object[] values){writer.WriteLine(string.Join(",",values.Select(Q)));writer.Flush();}
    static object State(string note)=>new { action=sequence,trip,note,phase=s.Phase.ToString(),date=CoreVoyageSession.FormatGameDate(s.GameDateTime,true),port=s.CurrentPort,ship=s.SelectedShip.Id,gold=s.ProfileGold,water=s.Cargo.Water,food=s.Cargo.Food,powder=s.Cargo.Powder,balls=s.Cargo.Cannonballs,volume=s.Cargo.UsedVolume,capacity=s.CargoCapacityValue,hull=s.Hull,x=s.Position.X,y=s.Position.Y,xp=s.PirateExperience,level=s.PirateLevel,fragments=s.TreasureFragments,voyageFragments=s.VoyageTreasureFragments,clues=s.TreasureClues };
    static void Log(string action,string detail="")
    {
        sequence++;
        minWater=Math.Min(minWater,s.Cargo.Water);minFood=Math.Min(minFood,s.Cargo.Food);
        minPowder=Math.Min(minPowder,s.Cargo.Powder);minBalls=Math.Min(minBalls,s.Cargo.Cannonballs);minGold=Math.Min(minGold,s.ProfileGold);
        Row(actions,sequence,trip,CoreVoyageSession.FormatGameDate(s.GameDateTime,true),s.Phase,s.CurrentPort,s.SelectedShip.Id,action,detail,s.ProfileGold,s.Cargo.Gold,s.Cargo.Water,s.Cargo.Food,s.Cargo.Powder,s.Cargo.Cannonballs,s.Cargo.Wood,s.Cargo.Medicine,s.Cargo.Beer,s.Cargo.Iron,s.Cargo.Cloth,s.Cargo.Gem,s.Cargo.AncientMaterial,s.Cargo.Relic,s.Cargo.BlackPearl,s.Cargo.UsedVolume,s.CargoCapacityValue,s.Hull,s.MaxHull,s.Morale,s.SelectedOrdinaryCrew,s.SelectedSpecialCrewCount,s.PirateLevel,s.PirateExperience,s.TreasureFragments,s.VoyageTreasureFragments,s.TreasureClues,s.TotalHours,s.TotalDistanceNm,s.Position.X,s.Position.Y,s.ExplorationCoverage());
    }
    static void Mark(string note){Log("Milestone",note);milestones.Add(State(note));Console.WriteLine("TRIP "+trip+" "+note+" | gold="+s.ProfileGold+" XP="+s.PirateExperience+" fragments="+s.TreasureFragments);Save();}
    static void Shortage(string note){Log("ResourceLimited",note);shortages.Add(State(note));}
    static void Need(bool ok,string label){if(!ok)throw new InvalidOperationException(label);}
    static int Main(string[] args)
    {
        output=args[0];maxCycles=int.Parse(args[1]);mode=args[2];Directory.CreateDirectory(output);
        actions=new StreamWriter(Path.Combine(output,"actions.csv"),false,new UTF8Encoding(true));
        battles=new StreamWriter(Path.Combine(output,"battles.csv"),false,new UTF8Encoding(true));
        voyages=new StreamWriter(Path.Combine(output,"voyages.csv"),false,new UTF8Encoding(true));
        actions.WriteLine("action,trip,date,phase,port,ship,actionType,detail,accountGold,travelGold,water,food,powder,cannonballs,wood,medicine,beer,iron,cloth,gem,ancientMaterial,relic,blackPearl,cargoVolume,capacity,hull,maxHull,morale,ordinaryCrew,specialCrew,pirateLevel,pirateXP,fragments,voyageFragments,clues,voyageHours,distanceNm,x,y,coverage");
        battles.WriteLine("trip,enemyId,enemyName,stage,outcome,turns,cards,powderUsed,ballsUsed,initialHull,minimumHull,finalHull,crewHealth");
        voyages.WriteLine("trip,goal,seed,rank,departure,destination,outcome,hours,days,distanceNm,coverage,completedEvents,bankedGold,bankedFragments,bankedXP,treasureCompleted,accountGold,water,food");
        s=new CoreVoyageSession();
        // Match an empty front-end profile, rather than the standalone Core default professions.
        s.ConfigureCrewCapabilities(false,false,false,false,false,false);s.ConfigureSpecialCardSources(new string[0],new string[0]);s.ConfigureDepartureCrew(10,0);
        DateTime date=CoreVoyageSession.NewCampaignStartDate(new DateTime(2026,10,11));
        Need(s.RestoreGameClock((date-CoreVoyageSession.GameClockOrigin).TotalHours),"new campaign date initialization");
        Mark("New campaign: fresh front-end-equivalent resources, no recruited specialists");
        int result=1;
        try
        {
            Tutorial();
            for(int cycle=0;cycle<maxCycles;cycle++)
            {
                PreparePort();Identify();
                if(ranks.Count==3&&TreasureEventCoverage()==18&&legends.Count==5){coverageObjective=true;break;}
                string rank=ReadyRank();
                if(rank!=null)
                {
                    string seed="PLAY-20261011-"+mode+"-"+rank+"-"+trip;
                    Need(s.TryCraftTreasureMapForRank(rank,seed,out string reason),reason);
                    Mark("Crafted "+rank+" map using four actually identified fragments");
                    Need(s.TrySelectTreasureSeed(seed,out reason),reason);
                    Supplies(true);voyageGoal="treasure";trip++;Need(s.TryStartTreasureVoyage(out reason),reason);Log("DepartTreasure",seed);
                    Navigate(true);
                }
                else
                {
                    Supplies(false);trip++;
                    bool needTravel=s.SelectedShipIndex==1&&s.ProfileGold>2400&&s.CurrentPort!="铜钟港";
                    voyageGoal=needTravel?"travel-to-shipyard":s.SelectedShipIndex==2&&legends.Count<5?"legendary":"explore";
                    int route=voyageGoal=="legendary"?2:0;
                    s.SelectRoute(route);
                    if(needTravel)s.SelectDestinationPort("铜钟港");
                    string seed=CoreVoyageSession.CreateChartSeed(route,s.CurrentPort,s.SelectedDestinationPort);
                    Need(s.TrySelectChartSeed(seed,out string reason),reason);s.StartVoyage();Log("DepartExplore",seed);Navigate(false);
                }
                if(s.Phase==CoreDemoPhase.Settlement)Settle();
                else throw new InvalidOperationException("Navigation stopped without settlement: "+s.Phase);
            }
            Mark("Run ended: completed ranks="+string.Join("/",ranks)+"; treasure event templates resolved="+TreasureEventCoverage()+"/18; legends="+legends.Count+"/5");
            result=ranks.Count==3&&TreasureEventCoverage()==18&&legends.Count==5?0:1;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Console.Error.WriteLine(e);Log("Stop",e.Message);}
        finally{Save();actions.Dispose();battles.Dispose();voyages.Dispose();}
        return result;
    }
    static int TreasureEventCoverage()=>resolved.Count(id=>id.StartsWith("treasure_")&&CoreTreasureHuntCatalog.CreateEncounters(CoreTreasureHuntCatalog.Ranks[0]).Any(e=>e.Id==id));
    static void Save()
    {
        var summary=new { method="Actual released Core DLL; all progression uses normal legal gameplay APIs; port pricing/stock uses released Presentation catalog. No GM, state restore, cargo injection or enemy/deck/draw mutation.",mode,completeThreeRanks=ranks.Count==3,completeTemplateCoverage=TreasureEventCoverage()==18,completeLegends=legends.Count==5,trip,actions=sequence,battles=battleCount,losses,totalHours,totalDays=totalHours/24,totalDistance,completedRanks=ranks.OrderBy(x=>x).ToArray(),defeatedLegends=legends.OrderBy(x=>x).ToArray(),treasureEvents=encounters.Where(x=>x.StartsWith("treasure_")).OrderBy(x=>x).ToArray(),resolvedEvents=resolved.OrderBy(x=>x).ToArray(),choices=choiceHistory,allEncounters=encounters.OrderBy(x=>x).ToArray(),minimum=new { water=minWater,food=minFood,powder=minPowder,balls=minBalls,accountGold=minGold },shortages,milestones,final=State("Final"),treasureProgress=s.CaptureTreasureProgress(),collectionProgress=s.CaptureCollectionProgress(),warehouse=s.PortWarehouse.Serialize(),marketLedger=marketBought,crew=crewIds };
        File.WriteAllText(Path.Combine(output,"summary.json"),new JavaScriptSerializer { MaxJsonLength=int.MaxValue }.Serialize(summary),new UTF8Encoding(false));
    }
    static void Crew()
    {
        bool[] flags=new bool[6];foreach(string id in crewIds)flags[CrewNarrativeCatalog.FindById(id).ProfessionIndex]=true;
        s.ConfigureCrewCapabilities(flags[0],flags[1],flags[2],flags[3],flags[4],flags[5]);
        s.ConfigureSpecialCardSources(crewIds.ToArray(),new string[0]);
        s.ConfigureDepartureCrew(s.CrewCountValue-crewIds.Count,crewIds.Count);Log("CrewFormation",string.Join(";",crewIds));
    }
    static void Hire(int profession)
    {
        if(crewIds.Any(id=>CrewNarrativeCatalog.FindById(id).ProfessionIndex==profession))return;
        var crew=CrewNarrativeCatalog.ForPortAndProfession(s.CurrentPort,profession);
        if(!s.TrySpendProfileGold(crew.ContractCost,"酒馆船员契约")){Shortage("Cannot recruit "+crew.DisplayName+" cost="+crew.ContractCost);return;}
        crewIds.Add(crew.Id);Crew();Mark("Paid recruitment: "+crew.DisplayName+" / "+crew.ContractCost);
    }
    static void Tutorial()
    {
        trip++;voyageGoal="tutorial-first";Need(s.ConfigureGuidedTutorialVoyage(1),"tutorial leg1");s.StartVoyage();Log("DepartTutorial","1");
        for(int i=0;i<30&&s.GuidedTutorialCompletedEncounters<3;i++)
        {
            if(s.Phase==CoreDemoPhase.Sailing){s.SetHeading(SailingDirection.East);s.SetDistance(120);Need(s.Sail(),"tutorial sail");Log("Sail","tutorial east 120");}
            if(s.Phase==CoreDemoPhase.Encounter){encounters.Add(s.ActiveEncounter.Id);if(s.ActiveEncounter.Id==CoreGuidedTutorialContent.OptionalEventId){Need(s.SkipEncounter(),"optional skip");Log("SkipEncounter","tutorial optional");}else{int c=s.ActiveEncounter.Id==CoreGuidedTutorialContent.CrewRequirementEventId?1:0;Need(s.ResolveChoice(c),"tutorial choice");Log("ResolveChoice",c.ToString());}}
        }
        Need(s.GuidedTutorialCompletedEncounters==3,"tutorial first finished");Need(s.SafeReturn(),"tutorial return");Settle();Mark("First lesson: mandatory event, optional skip, missing-navigator gate, paid return");
        Hire(0);BuyPaired(120);trip++;voyageGoal="tutorial-boss";Need(s.ConfigureGuidedTutorialVoyage(2),"tutorial leg2");s.StartVoyage();Log("DepartTutorial","combined navigator/boss lesson");
        for(int i=0;i<60&&!s.GuidedTutorialBossDefeated;i++)
        {
            if(s.Phase==CoreDemoPhase.Sailing){s.SetHeading(SailingDirection.East);s.SetDistance(120);Need(s.Sail(),"tutorial2 sail");Log("Sail","tutorial east 120");}
            if(s.Phase==CoreDemoPhase.Encounter){encounters.Add(s.ActiveEncounter.Id);string id=s.ActiveEncounter.Id;Need(s.ResolveChoice(0),"tutorial2 choice");Log("ResolveChoice",id+" /0");}
            if(s.Phase==CoreDemoPhase.Battle)Fight();
        }
        Need(s.GuidedTutorialBossDefeated,"tutorial boss finished");Need(s.SafeReturn(),"tutorial boss return");Settle();Need(s.ConfigureGuidedTutorialVoyage(0),"end tutorial");
        Mark("Tutorial boss won; one real fragment earned. Authored white bow/warehouse compass omitted from combat stats and inventory (conservative port-layer limitation)");
    }
    static int Remaining(PortMarketItem item)
    {
        string key=s.CurrentPort+"/"+PortMarketCatalog.MonthKey(s.GameDateTime)+"/"+PortMarketCatalog.Key(item);
        marketBought.TryGetValue(key,out int bought);
        return Math.Max(0,Math.Min(PortMarketCatalog.DayState(s.CurrentPort,item,s.GameDateTime,0).Stock,PortMarketCatalog.MaxPurchaseQuota(item,0))-bought);
    }
    static void Buy(PortMarketItem item,int amount)
    {
        if(amount<=0)return;
        string key=PortMarketCatalog.Key(item);
        decimal price=PortMarketCatalog.BuyUnitPrice(s.CurrentPort,item,s.GameDateTime,s.Cargo.Fame);
        int fit=(int)Math.Floor((s.CargoCapacityValue-s.Cargo.UsedVolume+.001f)/PortMarketCatalog.CargoVolume(item));
        int legal=Math.Min(amount,Math.Min(Remaining(item),Math.Min(Math.Max(0,fit),(int)Math.Min(int.MaxValue,decimal.Floor(s.ProfileGold/price)))));
        if(legal<amount)Shortage("Purchase "+key+" requested="+amount+" legal="+legal+" remainingMonthly="+Remaining(item)+" freeVolume="+(s.CargoCapacityValue-s.Cargo.UsedVolume));
        if(legal<=0)return;
        decimal cost=PortMarketCatalog.CalculateBuyCost(s.CurrentPort,item,s.GameDateTime,legal,s.Cargo.Fame);
        Need(s.TryPurchaseMarketCargo(key,legal,cost,PortMarketCatalog.CargoVolume(item)),"market purchase "+key);
        string ledger=s.CurrentPort+"/"+PortMarketCatalog.MonthKey(s.GameDateTime)+"/"+key;marketBought.TryGetValue(ledger,out int old);marketBought[ledger]=old+legal;
        Log("Purchase",key+" x"+legal+" cost="+cost+" stockAfter="+Remaining(item));
    }
    static void BuyPaired(int target)
    {
        int w=0,f=0;double volume=s.Cargo.UsedVolume;decimal cost=0;
        decimal wp=PortMarketCatalog.BuyUnitPrice(s.CurrentPort,PortMarketItem.Water,s.GameDateTime,s.Cargo.Fame),fp=PortMarketCatalog.BuyUnitPrice(s.CurrentPort,PortMarketItem.Food,s.GameDateTime,s.Cargo.Fame);
        while(s.Cargo.Water+w<target||s.Cargo.Food+f<target)
        {
            bool water=s.Cargo.Water+w<=s.Cargo.Food+f;if(s.Cargo.Water+w>=target)water=false;if(s.Cargo.Food+f>=target)water=true;
            if((water?w:f)>=(water?Remaining(PortMarketItem.Water):Remaining(PortMarketItem.Food)))break;
            double v=water?.07:.09;decimal p=water?wp:fp;
            // Retain room for observed rewards rather than loading the hold to 100%.
            if(volume+v>s.CargoCapacityValue-8||cost+p>s.ProfileGold)break;
            if(water)w++;else f++;volume+=v;cost+=p;
        }
        Buy(PortMarketItem.Water,w);Buy(PortMarketItem.Food,f);
        if(Math.Min(s.Cargo.Water,s.Cargo.Food)<target)Shortage("Paired ration target "+target+" limited by hold/funds/monthly stock; loaded="+Math.Min(s.Cargo.Water,s.Cargo.Food));
    }
    static void PreparePort()
    {
        Need(s.Phase==CoreDemoPhase.Port,"prepare at port");
        foreach(var item in PortMarketCatalog.AllItems)
        {
            string key=PortMarketCatalog.Key(item);int keep=key=="water"||key=="food"?int.MaxValue:key=="wood"?8:key=="medicine"?3:key=="beer"?4:key=="powder"?12:key=="cannonballs"?12:0;
            int qty=Math.Max(0,s.GetMarketCargoAmount(key)-keep);
            if(qty>0){decimal revenue=PortMarketCatalog.CalculateSellRevenue(s.CurrentPort,item,s.GameDateTime,qty);Need(s.TrySellMarketCargo(key,qty,revenue),"sell "+key);Log("Sale",key+" x"+qty+" revenue="+revenue);}
        }
        foreach(var item in CoreWarehouseItemCatalog.RareItems)
        {
            int qty=s.GetWarehouseCargoAmount(item.Key);if(qty>0){Need(s.TryDepositPortCargo(item.Key,qty),"deposit "+item.Key);Log("WarehouseDeposit",item.Key+" x"+qty);}
        }
        if(s.SelectedShipIndex==0&&s.ProfileGold>1000)
        {
            int port=CoreVoyageSession.DestinationPorts.ToList().IndexOf(s.CurrentPort);
            if(CoreShipMarketCatalog.IsSoldAtPort(port,1)&&s.TrySpendProfileGold(CoreShipMarketCatalog.PurchaseCost(1),"皇家船坞购买船只")){Need(s.SelectShip(1),"merchant ship select");Mark("Paid merchant hull purchase: 800 gold, cargo 80→150");}
        }
        if(s.SelectedShipIndex==1&&s.CurrentPort=="铜钟港"&&s.ProfileGold>2100)
        {Need(s.TrySpendProfileGold(CoreShipMarketCatalog.PurchaseCost(2),"皇家船坞购买船只"),"pay patrol");Need(s.SelectShip(2),"patrol select");Mark("Paid patrol hull purchase: 1800 gold, 155 hull / 150 cargo / 22 berths");}
        if(s.ProfileGold>300)Hire(4);if(s.ProfileGold>350)Hire(1);if(s.ProfileGold>450&&s.SelectedShipIndex!=0)Hire(5);
        Crew();
        foreach(string id in new[]{"card_crew_thrust","card_crew_guard","card_crew_rally","card_crew_bandage","card_full_sail_turn","card_parallel_course","card_brace"})
        {
            int index=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).FirstOrDefault(i=>CoreVoyageSession.CardLibrary[i].Id==id);
            if(CoreVoyageSession.CardLibrary[index].Id!=id)continue;
            int cost=s.GetCardUpgradeCost(index);
            if(cost>0&&s.GetCardUpgradeLevel(id)<(s.SelectedShipIndex==0?1:2)&&s.ProfileGold>cost+300&&s.TryUpgradeBattleCard(index,out string reason))Log("PaidCardUpgrade",id+" cost="+cost);
        }
        // Edit the actual legal 8-card decks in port; sources and progression remain unchanged.
        var shipIds=new[]{"card_full_sail_turn","card_full_sail_turn","card_full_sail_turn","card_parallel_course","card_parallel_course","card_parallel_course","card_brace","card_brace"};
        var boardIds=new[]{"card_crew_thrust","card_crew_thrust","card_crew_guard","card_crew_guard","card_crew_rally","card_crew_rally","card_crew_bandage","card_crew_bandage"};
        int[] Indexes(string[] ids)=>ids.Select(id=>Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(i=>CoreVoyageSession.CardLibrary[i].Id==id)).ToArray();
        Need(s.SetBattleDeck(NavalBattleStage.Preparation,Indexes(shipIds)),"preparation deck");Need(s.SetBattleDeck(NavalBattleStage.Naval,Indexes(shipIds)),"naval deck");Need(s.SetBattleDeck(NavalBattleStage.Boarding,Indexes(boardIds)),"boarding deck");Log("EditDeck","legal sustain boarding deck, 8 cards per stage");
    }
    static void Supplies(bool treasure)
    {
        Buy(PortMarketItem.Wood,Math.Max(0,8-s.Cargo.Wood));Buy(PortMarketItem.Medicine,Math.Max(0,3-s.Cargo.Medicine));
        Buy(PortMarketItem.Powder,Math.Max(0,12-s.Cargo.Powder));Buy(PortMarketItem.Cannonballs,Math.Max(0,12-s.Cargo.Cannonballs));
        int days=treasure||s.SelectedShipIndex==2?35:24;int target=(int)Math.Ceiling(s.EstimateDailyRationConsumption(s.EffectiveCrewCount,s.HasCook)*days);
        Log("RecommendedSupplies", "actual recommendation target water="+(s.Cargo.Water+s.CreateRecommendedSupplyOrder().Water)+" food="+(s.Cargo.Food+s.CreateRecommendedSupplyOrder().Food)+" powder="+(s.Cargo.Powder+s.CreateRecommendedSupplyOrder().Powder)+" balls="+(s.Cargo.Cannonballs+s.CreateRecommendedSupplyOrder().Cannonballs));
        BuyPaired(target);Mark("Departure reserve: "+days+" days requested; available="+(Math.Min(s.Cargo.Water,s.Cargo.Food)/s.EstimateDailyRationConsumption(s.EffectiveCrewCount,s.HasCook)).ToString("F2")+" days");
    }
    static void Identify(){while(s.CanIdentifyTreasureFragment(out string reason)){Need(s.TryIdentifyTreasureFragment(out reason),reason);Log("IdentifyFragment",reason);}}
    static string ReadyRank()
    {
        foreach(var rank in CoreTreasureHuntCatalog.Ranks.OrderBy(r=>ranks.Contains(r.Id)?1:0).ThenByDescending(r=>r.UnlockLevel))if(s.CanCraftTreasureMap(rank.Id,out string reason))return rank.Id;
        return null;
    }
    static float Distance(VoyagePoint a,VoyagePoint b){float x=a.X-b.X,y=a.Y-b.Y;return (float)Math.Sqrt(x*x+y*y);}
    sealed class Target{public string key;public VoyagePoint point;public double priority;}
    static Target ChooseTarget(bool treasure,HashSet<string> rejected)
    {
        var targets=new List<Target>();
        foreach(var n in s.Map.Nodes)
        {
            if(!n.IsChartVisible)continue; // Position and identity are never read before visibility.
            if(n.Visited||rejected.Contains("node:"+n.Id))continue;
            string id=n.Encounter.Id;
            if(id==CoreTreasureHuntCatalog.CoreEncounterId){if(s.TreasureClues<s.TreasureRequiredClues-(s.HasDiver?1:0))continue;targets.Add(new Target{key="node:"+n.Id,point=n.Position,priority=10000});continue;}
            if(n.Skipped)continue;
            if(n.Encounter.BossKind==CoreBossKind.Legendary)
            {
                if(s.SelectedShipIndex!=2||legends.Contains(id)||s.GetCardUpgradeLevel("card_crew_bandage")<2)continue;
                targets.Add(new Target{key="node:"+n.Id,point=n.Position,priority=20000});continue;
            }
            if(n.IsBoss)
            {if(voyageGoal!="travel-to-shipyard")continue;targets.Add(new Target{key="node:"+n.Id,point=n.Position,priority=20000});continue;}
            double p=treasure?(clueNames.Contains(n.Encounter.Name)?4000:300):(fragmentNames.Contains(n.Encounter.Name)?5000:200);
            if(treasure&&!encounters.Contains(id))p+=500;
            targets.Add(new Target{key="node:"+n.Id,point=n.Position,priority=p});
        }
        if(voyageGoal=="travel-to-shipyard"&&s.BossDefeated)return new Target{key="destination",point=s.Map.Destination,priority=20000};
        var t=targets.OrderByDescending(x=>x.priority/(1+Distance(s.Position,x.point))).FirstOrDefault();if(t!=null)return t;
        int columns=CoreVoyageMap.GridColumns,rows=CoreVoyageMap.GridRows;
        int Unknown(int i,int radius){int count=0,c=i%columns,r=i/columns;for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++){int a=c+x,b=r+y;if(a>1&&a<columns-2&&b>1&&b<rows-2&&!s.Map.ExploredCells[b*columns+a])count++;}return count;}
        for(int i=0;i<s.Map.CellCount;i++)
        {
            if(!s.Map.ExploredCells[i]||rejected.Contains("cell:"+i)||!s.Map.IsPassable(i%columns,i/columns)||Unknown(i,1)==0)continue;
            var point=s.Map.CellCenter(i%columns,i/columns);if(point.X<6||point.X>94||point.Y<6||point.Y>58||Distance(s.Position,point)<2)continue;
            targets.Add(new Target{key="cell:"+i,point=point,priority=Unknown(i,3)});
        }
        return targets.OrderByDescending(x=>x.priority/(2+Distance(s.Position,x.point))).FirstOrDefault();
    }
    static void Navigate(bool treasure)
    {
        var rejected=new HashSet<string>();
        for(int step=0;step<1800&&s.Phase!=CoreDemoPhase.Settlement;step++)
        {
            if(s.Phase==CoreDemoPhase.Battle){Fight();continue;}
            if(s.Phase==CoreDemoPhase.Encounter){Resolve(treasure);continue;}
            if(s.Phase!=CoreDemoPhase.Sailing)break;
            if(Math.Min(s.WaterSupplyHours,s.FoodSupplyHours)<30||s.Hull<Math.Max(10,s.MaxHull/6))
            {Shortage("Return threshold: supply hours="+Math.Min(s.WaterSupplyHours,s.FoodSupplyHours)+" hull="+s.Hull);Return();continue;}
            if(!treasure&&s.ExplorationCoverage()>80&&s.CompletedEventCount()>5){Return();continue;}
            var target=ChooseTarget(treasure,rejected);if(target==null){Log("NavigationEnd","No visible unvisited node or passable explored frontier");Return();continue;}
            s.SetCourseTo(target.point);
            if(s.Phase!=CoreDemoPhase.Sailing)continue;
            var forecast=s.GetForecast();
            if(forecast.Blocked||forecast.ActualDistanceNm<1){rejected.Add(target.key);Log("BlockedForecast",target.key);continue;}
            if(forecast.WaterCost>s.Cargo.Water||forecast.FoodCost>s.Cargo.Food){Shortage("Cannot afford route preview "+target.key);Return();continue;}
            VoyagePoint before=s.Position;Need(s.Sail(),"legal sail");navigationSteps++;Log("Sail",target.key+" public forecast="+forecast.ActualDistanceNm.ToString("F1")+"nm /"+forecast.Hours.ToString("F1")+"h");
            if(Distance(before,s.Position)<.01&&s.Phase==CoreDemoPhase.Sailing)rejected.Add(target.key);
            if(step%50==0)Save();
        }
        if(s.Phase==CoreDemoPhase.Sailing){Log("NavigationBudget","1800 action watchdog; real return");Return();}
    }
    static void Return(){if(s.SafeReturn()){Log("SafeReturn","normal protected settlement");return;}if(s.CanRequestEmergencyTow()){Need(s.RequestEmergencyTow(),"tow");Log("EmergencyTow","normal failure settlement");return;}throw new InvalidOperationException("No legal return");}
    static void Resolve(bool treasure)
    {
        var encounter=s.ActiveEncounter;encounters.Add(encounter.Id);int best=-1;double score=double.NegativeInfinity;
        bool challenge=encounter.BossKind==CoreBossKind.Legendary&&s.SelectedShipIndex==2&&s.GetCardUpgradeLevel("card_crew_bandage")>=2&&!legends.Contains(encounter.Id)||encounter.BossKind==CoreBossKind.Route&&voyageGoal=="travel-to-shipyard";
        for(int i=0;i<encounter.Choices.Count;i++)
        {
            var e=s.EffectiveChoiceEffect(i);if(e.MapFragments>0)fragmentNames.Add(encounter.Name);if(e.TreasureClues>0)clueNames.Add(encounter.Name);
            if(!s.IsChoiceAvailable(i)){blockedChoices++;Log("UnavailableChoice",encounter.Id+" /"+i+" "+s.ChoiceUnavailableReason(i));continue;}
            var c=encounter.Choices[i];double p=e.MapFragments*10000+e.TreasureClues*5000+e.Reveal*180+e.Gold*.25+e.Hull*4+e.Water+e.Food-c.Hours*3-(e.ForceReturn?100000:0)+(c.StartsBattle?-2000:0);
            if(encounter.Id==CoreTreasureHuntCatalog.CoreEncounterId)p=i==2?1000000:i==0?900000:i==1?800000:-1000000;
            if(challenge&&c.StartsBattle)p+=1000000;
            if(p>score){score=p;best=i;}
        }
        if(!encounter.Mandatory&&(best<0||score<0)&&!encounter.Id.StartsWith("treasure_")){Need(s.SkipEncounter(),"skip");Log("SkipEncounter",encounter.Id);return;}
        if(best>=0){string detail=encounter.Id+" /"+best+" "+encounter.Choices[best].Label;Need(s.ResolveChoice(best),"choice "+detail);resolved.Add(encounter.Id);if(!choiceHistory.ContainsKey(encounter.Id))choiceHistory[encounter.Id]=new List<int>();choiceHistory[encounter.Id].Add(best);Log("ResolveChoice",detail);return;}
        Shortage("Mandatory encounter has no affordable choice: "+encounter.Id);Return();
    }
    static void Fight()
    {
        battleCount++;string id=s.ActiveEncounter.Id,name=s.ActiveEncounter.Name;int initial=s.Hull,min=initial,cards=0,powder=0,balls=0,turns=0;
        var battle=s.Battle;Mark("Battle begins: "+name+" / hull "+s.Hull+" ammunition "+s.Cargo.Powder+"/"+s.Cargo.Cannonballs);
        for(int action=0;action<600&&s.Phase==CoreDemoPhase.Battle;action++)
        {
            if(s.IsGuidedBossBattle)
            {
                var kind=s.GuidedBossExpectedAction;int hand=battle.Hand.FindIndex(i=>s.GetBattleCard(i).Id==s.GuidedBossExpectedCardId);
                if(kind==GuidedBossActionKind.BlockedCard){Need(s.TryObserveGuidedBossBlockedCard(hand,out string r),r);Log("TutorialBlockedCard",s.GuidedBossExpectedCardId);}
                else if(kind==GuidedBossActionKind.Card){var c=s.GetBattleCard(battle.Hand[hand]);powder+=c.PowderCost;balls+=c.CannonballCost;Need(s.PlayCard(hand),"tutorial card");cards++;Log("PlayCard",c.Name);}
                else if(kind==GuidedBossActionKind.Ram){Need(s.TryRam(),"tutorial ram");Log("Ram","authored tutorial");}
                else if(kind==GuidedBossActionKind.EndTurn){Need(s.EndBattleTurn(),"tutorial end");turns++;Log("EndTurn",s.GetEnemyIntent());}
                else throw new InvalidOperationException("Missing guided action");
            }
            else
            {
                if(s.CanBoard(out string boardReason)){Need(s.TryBoard(),boardReason);Log("Board","legal public boarding");continue;}
                int best=-1;double bestScore=.01;
                for(int h=0;h<battle.Hand.Count;h++){if(!s.CanPlayCard(h))continue;double p=CardScore(battle.Hand[h]);if(p>bestScore){best=h;bestScore=p;}}
                if(best>=0){var c=s.GetBattleCard(battle.Hand[best]);powder+=c.PowderCost;balls+=c.CannonballCost;Need(s.PlayCard(best),"card "+c.Name);cards++;Log("PlayCard",c.Name+" / "+battle.Stage);}
                else{Need(s.EndBattleTurn(),"end turn");turns++;Log("EndTurn",s.GetEnemyIntent());}
            }
            min=Math.Min(min,s.Hull);
        }
        bool won=s.Phase==CoreDemoPhase.Sailing;if(!won)losses++;if(won&&CoreLegendaryWarships.Find(id)!=null)legends.Add(id);
        Row(battles,trip,id,name,battle.Stage,won?"win":s.Phase==CoreDemoPhase.Settlement?"loss":"watchdog",turns,cards,powder,balls,initial,min,s.Hull,battle.PlayerBoardingHealth);
        Mark("Battle "+(won?"won":"lost")+": "+name+"; powder "+powder+", balls "+balls+", minimum hull "+min);
        if(s.Phase==CoreDemoPhase.Battle)throw new InvalidOperationException("Combat watchdog");
    }
    static double CardScore(int index)
    {
        var c=s.GetBattleCard(index);var b=s.Battle;var e=s.GetBattleCardEffects(c,b.Stage);bool boarding=b.Stage==NavalBattleStage.Boarding,preparing=b.Stage==NavalBattleStage.Preparation;
        int enemy=boarding?b.EnemyBoardingHealth:b.EnemyHull,health=boarding?b.PlayerBoardingHealth:s.Hull,max=boarding?b.PlayerMaxBoardingHealth:s.MaxHull;
        Match m=Regex.Match(s.GetEnemyIntent()??"","(?:炮击：伤害 |白刃突击：船员伤害 )(\\d+)");int attack=m.Success?int.Parse(m.Groups[1].Value):0;
        int repair=Math.Min(max-health,e.Repair),block=Math.Min(Math.Max(0,(preparing?14:attack)-b.PlayerBlock),e.Block+(c.Block>0&&!boarding?b.PlayerDeckBuff:0));
        double score=repair*(health<max*.55?2.5:.65)+block*(health<=attack*2?2:.9);
        if(boarding)score=repair*(health<=attack*2?3.5:1.2)+block*(health<=attack*2?3.5:2.1);
        int damage=e.Damage+(c.Action==NavalBattleCardAction.Cannon?Math.Max(0,(s.EffectiveFirepower-8)/2)+b.PlayerLoadedShot:0);
        if(damage>=enemy+b.EnemyBlock)return 10000/(s.GetBattleCardEnergyCost(index)+.65);
        if(boarding)score+=damage*1.7;
        if(c.Draw>0&&b.Hand.Count<7)score+=c.Draw*(b.Hand.Count<4?6:2);
        switch(c.Action)
        {
            case NavalBattleCardAction.Polish:if(b.PlayerDeckBuff<c.StrategyLimit)score+=preparing?35:9;break;
            case NavalBattleCardAction.Approach:case NavalBattleCardAction.Circle:case NavalBattleCardAction.Turn:if(b.DistanceMeters>60)score+=40;else if(b.Alignment==NavalBattleAlignment.HeadOn&&b.DistanceMeters<=100&&c.Action!=NavalBattleCardAction.Approach)score+=14;break;
            case NavalBattleCardAction.Navigator:case NavalBattleCardAction.Parallel:if(b.DistanceMeters>60)score+=44;else if(b.Alignment!=NavalBattleAlignment.Parallel)score+=65;break;
            case NavalBattleCardAction.Retreat:return 0;
        }
        return score/(s.GetBattleCardEnergyCost(index)+.65);
    }
    static void Settle()
    {
        var x=s.Settlement;string seed=s.Map.Seed,rank=s.ActiveTreasureMap?.RankId??"";totalHours+=x.Hours;totalDistance+=x.DistanceNm;
        if(x.TreasureCompleted&&x.Outcome!=VoyageOutcome.Failure)ranks.Add(rank);
        Row(voyages,trip,voyageGoal,seed,rank,s.Map.DepartureName,x.Destination,x.Outcome,x.Hours,x.Hours/24,x.DistanceNm,x.Coverage,x.CompletedEvents,x.BankedGold,x.BankedFragments,x.BankedPirateExperience,x.TreasureCompleted,s.ProfileGold,s.Cargo.Water,s.Cargo.Food);
        Mark("Settlement: "+x.Outcome+" / "+x.Hours.ToString("F1")+" h / "+x.CompletedEvents+" events / "+x.BankedFragments+" fragments / "+x.BankedPirateExperience+" XP");
        s.ReturnToPortAfterSettlement();Log("ConfirmSettlement",x.Outcome+" rank="+rank);
        Save();
    }
}
