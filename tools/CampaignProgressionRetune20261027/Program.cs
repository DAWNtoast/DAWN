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

internal static partial class Program
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
        Ledger(action,detail);
        sequence++;
        minWater=Math.Min(minWater,s.Cargo.Water);minFood=Math.Min(minFood,s.Cargo.Food);
        minPowder=Math.Min(minPowder,s.Cargo.Powder);minBalls=Math.Min(minBalls,s.Cargo.Cannonballs);minGold=Math.Min(minGold,s.ProfileGold);
        Row(actions,sequence,trip,CoreVoyageSession.FormatGameDate(s.GameDateTime,true),s.Phase,s.CurrentPort,s.SelectedShip.Id,action,detail,s.ProfileGold,s.Cargo.Gold,s.Cargo.Water,s.Cargo.Food,s.Cargo.Powder,s.Cargo.Cannonballs,s.Cargo.Wood,s.Cargo.Medicine,s.Cargo.Beer,s.Cargo.Iron,s.Cargo.Cloth,s.Cargo.Gem,s.Cargo.AncientMaterial,s.Cargo.Relic,s.Cargo.BlackPearl,s.Cargo.UsedVolume,s.CargoCapacityValue,s.Hull,s.MaxHull,s.Morale,s.SelectedOrdinaryCrew,s.SelectedSpecialCrewCount,s.PirateLevel,s.PirateExperience,s.TreasureFragments,s.VoyageTreasureFragments,s.TreasureClues,s.TotalHours,s.TotalDistanceNm,s.Position.X,s.Position.Y,s.ExplorationCoverage());
        foreach(int level in Enumerable.Range(2,9))if(s.PirateLevel>=level&&!levelUnlocks.ContainsKey(level.ToString()))levelUnlocks[level.ToString()]=CampaignState("level-"+level+"-unlocked");
    }
    static void Mark(string note){Log("Milestone",note);milestones.Add(State(note));Console.WriteLine("TRIP "+trip+" "+note+" | gold="+s.ProfileGold+" XP="+s.PirateExperience+" fragments="+s.TreasureFragments);Save();}
    static void Shortage(string note){Log("ResourceLimited",note);shortages.Add(State(note));}
    static void Need(bool ok,string label){if(!ok)throw new InvalidOperationException(label);}
    static void Save(){SaveCampaign();}
    static void Crew()
    {
        bool[] flags=new bool[6];foreach(string id in crewIds)flags[CrewNarrativeCatalog.FindById(id).ProfessionIndex]=true;
        s.ConfigureCrewCapabilities(flags[0],flags[1],flags[2],flags[3],flags[4],flags[5]);
        RefreshEquipment();
        s.ConfigureDepartureCrew(s.CrewCountValue-crewIds.Count,crewIds.Count);Log("CrewFormation",string.Join(";",crewIds));
    }
    static void Hire(int profession)
    {
        if(crewIds.Any(id=>CrewNarrativeCatalog.FindById(id).ProfessionIndex==profession))return;
        var crew=CrewNarrativeCatalog.ForPortAndProfession(s.CurrentPort,profession);
        if(!s.TrySpendProfileGold(crew.ContractCost,"酒馆船员契约")){Shortage("Cannot recruit "+crew.DisplayName+" cost="+crew.ContractCost);return;}
        crewIds.Add(crew.Id);Log("Recruit",crew.DisplayName+" cost="+crew.ContractCost);Crew();Mark("Paid recruitment: "+crew.DisplayName+" / "+crew.ContractCost);
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
        Hire(0);trip++;voyageGoal="tutorial-boss";Need(s.ConfigureGuidedTutorialVoyage(2),"tutorial leg2");s.StartVoyage();Log("DepartTutorial","combined navigator/boss lesson");
        for(int i=0;i<60&&!s.GuidedTutorialBossDefeated;i++)
        {
            if(s.Phase==CoreDemoPhase.Sailing){s.SetHeading(SailingDirection.East);s.SetDistance(120);Need(s.Sail(),"tutorial2 sail");Log("Sail","tutorial east 120");}
            if(s.Phase==CoreDemoPhase.Encounter){encounters.Add(s.ActiveEncounter.Id);string id=s.ActiveEncounter.Id;Need(s.ResolveChoice(0),"tutorial2 choice");Log("ResolveChoice",id+" /0");}
            if(s.Phase==CoreDemoPhase.Battle)Fight();
        }
        Need(s.GuidedTutorialBossDefeated,"tutorial boss finished");Need(s.SafeReturn(),"tutorial boss return");Settle();Need(s.ConfigureGuidedTutorialVoyage(0),"end tutorial");
        Mark("Tutorial boss won; one real fragment earned; current adapter handles earned bow/compass immediately after confirmed return");
    }
    static int Remaining(PortMarketItem item)
        =>RemainingAt(s.CurrentPort,item);
    static int RemainingAt(string port,PortMarketItem item)
    {
        string key=port+"/"+PortMarketCatalog.MonthKey(s.GameDateTime)+"/"+PortMarketCatalog.Key(item);
        marketBought.TryGetValue(key,out int bought);
        return Math.Max(0,Math.Min(PortMarketCatalog.DayState(port,item,s.GameDateTime,0).Stock,PortMarketCatalog.MaxPurchaseQuota(item,0))-bought);
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
    static void Supplies(bool treasure)
    {
        Buy(PortMarketItem.Wood,Math.Max(0,8-s.Cargo.Wood));Buy(PortMarketItem.Medicine,Math.Max(0,(s.SelectedShipIndex==7?24:s.SelectedShipIndex==2?16:8)-s.Cargo.Medicine));
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
        if(LearningPolicy&&s.BossDefeated&&(voyageGoal=="medicine-recovery"||voyageGoal=="travel-to-shipyard"))return new Target{key="destination",point=s.Map.Destination,priority=100000};
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
                if(!ReadyForBoss(n.Encounter)||firstKills.ContainsKey(id))continue;
                targets.Add(new Target{key="node:"+n.Id,point=n.Position,priority=20000});continue;
            }
            if(n.IsBoss)
            {if(!ReadyForBoss(n.Encounter))continue;targets.Add(new Target{key="node:"+n.Id,point=n.Position,priority=LearningPolicy&&!firstKills.ContainsKey(id)?100000:20000});continue;}
            double p=treasure?(clueNames.Contains(n.Encounter.Name)?4000:300):200;
            if(treasure&&!encounters.Contains(id))p+=500;
            targets.Add(new Target{key="node:"+n.Id,point=n.Position,priority=p});
        }
        if(s.BossDefeated&&(s.SelectedRouteIndex<2||AllBossesDone()))return new Target{key="destination",point=s.Map.Destination,priority=20000};
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
            RefreshEnvironment();
            if(Math.Min(s.WaterSupplyHours,s.FoodSupplyHours)<30||s.Hull<(LearningPolicy?s.MaxHull*.45:Math.Max(10,s.MaxHull/6))||s.SelectedRouteIndex>=1&&s.Cargo.Medicine<4)
            {Shortage("Return threshold: supply hours="+Math.Min(s.WaterSupplyHours,s.FoodSupplyHours)+" hull="+s.Hull);Return();continue;}
            if(!treasure&&s.ExplorationCoverage()>95&&s.CompletedEventCount()>10){Return();continue;}
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
        // The actual encounter screen rebuilds the sailing dashboard at the arrival
        // position/time before exposing choices, including fitting condition effects.
        RefreshEnvironment();
        Log("EnvironmentRefresh","Encounter arrival context before choosing an action");
        var encounter=s.ActiveEncounter;encounters.Add(encounter.Id);int best=-1;double score=double.NegativeInfinity;
        bool challenge=encounter.BossKind!=CoreBossKind.None ? ReadyForBoss(encounter) : s.Hull>s.MaxHull*.6&&s.Cargo.Medicine>=3;
        if(LearningPolicy&&voyageGoal=="medicine-recovery"&&encounter.BossKind!=CoreBossKind.Route)challenge=false;
        for(int i=0;i<encounter.Choices.Count;i++)
        {
            var e=s.EffectiveChoiceEffect(i);if(e.MapFragments>0)fragmentNames.Add(encounter.Name);if(e.TreasureClues>0)clueNames.Add(encounter.Name);
            if(!s.IsChoiceAvailable(i)){blockedChoices++;Log("UnavailableChoice",encounter.Id+" /"+i+" "+s.ChoiceUnavailableReason(i));continue;}
            var c=encounter.Choices[i];double p=e.MapFragments*3+e.TreasureClues*5+e.Reveal*2+e.Gold*2+Math.Min(s.MaxHull-s.Hull,Math.Max(0,e.Hull))*2+e.Water*.04+e.Food*.12+e.Iron*5+e.Wood*3-c.Hours*.4-(e.ForceReturn?100000:0)+(c.StartsBattle?-2000:0);
            if(encounter.Id==CoreTreasureHuntCatalog.CoreEncounterId)p=i==2?1000000:i==0?900000:i==1?800000:-1000000;
            if(LearningPolicy){double hullWeight=s.Hull<s.MaxHull*.7?12:4;p+=Math.Min(s.MaxHull-s.Hull,Math.Max(0,e.Hull))*(hullWeight-2)+Math.Min(0,e.Hull)*hullWeight;}
            if(challenge&&c.StartsBattle)p+=1000000;
            if(p>score){score=p;best=i;}
        }
        if(!encounter.Mandatory&&(best<0||score<0)&&!encounter.Id.StartsWith("treasure_")){Need(s.SkipEncounter(),"skip");Log("SkipEncounter",encounter.Id);return;}
        if(best>=0){string detail=encounter.Id+" /"+best+" "+encounter.Choices[best].Label;
            if(encounter.BossKind!=CoreBossKind.None&&encounter.Choices[best].StartsBattle&&s.CanCaptureVoyageCheckpoint){string checkpointPath=Path.Combine(output,"Checkpoints");Directory.CreateDirectory(checkpointPath);File.WriteAllText(Path.Combine(checkpointPath,"before-trip-"+trip+"-"+encounter.Id+".json"),new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=100}.Serialize(new{checkpoint=s.CaptureVoyageCheckpoint(),state=CampaignState("before-boss-choice"),choice=best}),new UTF8Encoding(false));}
            Need(s.ResolveChoice(best),"choice "+detail);resolved.Add(encounter.Id);if(!choiceHistory.ContainsKey(encounter.Id))choiceHistory[encounter.Id]=new List<int>();choiceHistory[encounter.Id].Add(best);Log("ResolveChoice",detail);return;}
        Shortage("Mandatory encounter has no affordable choice: "+encounter.Id);Return();
    }
    static void Fight()
    {
        battleCount++;string id=s.ActiveEncounter.Id,name=s.ActiveEncounter.Name;decimal battleStartingCash=Cash;int battleStartingFame=s.Cargo.Fame;var battleStartingSnapshot=CampaignState("battle-start");int initial=s.Hull,min=initial,cards=0,powder=0,balls=0,turns=0;
        if(CoreRouteBossCatalog.Find(id)!=null){routeAttemptTrips[s.SelectedRouteIndex]=trip;routeAttemptPower[s.SelectedRouteIndex]=ReadinessScore();routeAttemptDevelopmentVoyages[s.SelectedRouteIndex]=effectiveDevelopmentVoyages+1;}
        var stageTurns=new Dictionary<string,int>{{"Preparation",0},{"Naval",0},{"Boarding",0}};int boardingEntryAfterNavalTurns=-1;
        int startingMedicine=s.Cargo.Medicine;
        var battle=s.Battle;Mark("Battle begins: "+name+" / hull "+s.Hull+" ammunition "+s.Cargo.Powder+"/"+s.Cargo.Cannonballs);
        for(int action=0;action<1800&&s.Phase==CoreDemoPhase.Battle;action++)
        {
            battle=s.Battle;int beforeTurns=turns;var turnStage=battle.Stage;
            if(s.IsGuidedBossBattle && s.GuidedBossExpectedAction!=GuidedBossActionKind.FreePlay)
            {
                var kind=s.GuidedBossExpectedAction;int hand=battle.Hand.FindIndex(i=>s.GetBattleCard(i).Id==s.GuidedBossExpectedCardId);
                if(kind==GuidedBossActionKind.BlockedCard){Need(s.TryObserveGuidedBossBlockedCard(hand,out string r),r);Log("TutorialBlockedCard",s.GuidedBossExpectedCardId);}
                else if(kind==GuidedBossActionKind.Card){var c=s.GetBattleCard(battle.Hand[hand]);powder+=c.PowderCost;balls+=c.CannonballCost;Need(s.PlayCard(hand),"tutorial card");cards++;Log("PlayCard",c.Name);}
                else if(kind==GuidedBossActionKind.Ram){Need(s.TryRam(),"tutorial ram");Log("Ram","authored tutorial");}
                else if(kind==GuidedBossActionKind.EndTurn){Need(s.EndBattleTurn(),"tutorial end");turns++;Log("EndTurn",s.GetEnemyIntent());}
                else if(kind==GuidedBossActionKind.Inspect){Need(s.TryInspectGuidedBoss(s.GuidedBossInspectionTopic,out string why),why);Log("TutorialInspect",s.GuidedBossInspectionTopic);}
                else if(kind==GuidedBossActionKind.Continue){Need(!s.GuidedBossExerciseFailed,"guided exercise failed");Need(s.TryContinueGuidedBossLesson(out string why),why);Log("TutorialContinue",s.GuidedBossLesson.ToString());}
                else if(kind==GuidedBossActionKind.Movement){Need(s.TryAppendNavalMovementStep(s.GetNavalLegalNextSteps()[0],out string why),why);Need(s.CommitNavalMovement(out why),why);Need(s.ConfirmNavalArrivalHeading(s.GetNavalArrivalHeadings()[0],out why),why);Log("TutorialMovement",s.GuidedBossStep.ToString());}
                else throw new InvalidOperationException("Missing guided action: "+kind);
            }
            else
            {
                if(s.CanRam(out _) && (!s.IsGuidedBossBattle||s.GuidedBossLesson!=GuidedBossLesson.BoardingContact)){Need(s.TryRam(),"legal ram");Log("Ram","current public contact geometry");if(boardingEntryAfterNavalTurns<0)boardingEntryAfterNavalTurns=stageTurns["Naval"];continue;}
                if(s.CanBoard(out string boardReason)){Need(s.TryBoard(),boardReason);Log("Board","legal public boarding");if(boardingEntryAfterNavalTurns<0)boardingEntryAfterNavalTurns=stageTurns["Naval"];continue;}
                int best=-1;double bestScore=.01;
                for(int h=0;h<battle.Hand.Count;h++){if(!s.CanPlayCard(h))continue;double p=CardScore(battle.Hand[h]);if(p>bestScore){best=h;bestScore=p;}}
                if(best>=0){var c=s.GetBattleCard(battle.Hand[best]);powder+=c.PowderCost;balls+=c.CannonballCost;Need(s.PlayCard(best),"card "+c.Name);cards++;Log("PlayCard",c.Name+" / "+battle.Stage);}
                else if(CombatMove()){Log("CombatMovement","planned legal steps; public path forecast");}
                else{Need(s.EndBattleTurn(),"end turn");turns++;Log("EndTurn",s.GetEnemyIntent());}
            }
            if(turns>beforeTurns)stageTurns[turnStage.ToString()]++;if(boardingEntryAfterNavalTurns<0&&s.Battle?.Stage==NavalBattleStage.Boarding)boardingEntryAfterNavalTurns=stageTurns["Naval"];
            min=Math.Min(min,s.Hull);
        }
        bool won=s.Phase==CoreDemoPhase.Sailing;if(!won&&(id=="legendary_pirate_last_horizon"||id=="boss_route_moon_tide_ruins_arcane")&&s.SelectedShipIndex==7)adaptiveCurseDeck=true;if(!won)losses++;if(won&&CoreLegendaryWarships.Find(id)!=null)legends.Add(id);
        if(LearningPolicy&&!won&&CoreBattleBalance.Classify(id)!=CoreBossKind.None){learningBossLosses++;Log("LearningObservation","Observed boss defeat "+id+"; enemy HP="+battle.EnemyBoardingHealth+" medicine="+s.Cargo.Medicine+"; next port considers real upgrades and component offers");}
        if(!won&&CoreRouteBossCatalog.Find(id)!=null){string failureKey=s.SelectedRouteIndex+"/"+s.SelectedShipIndex;routeFailuresByShip.TryGetValue(failureKey,out int oldFailures);routeFailuresByShip[failureKey]=oldFailures+1;if(purchasePolicy=="unlock-probe"&&s.SelectedShipIndex>=2)adaptiveCurseDeck=true;}
        Row(battles,trip,id,name,battle.Stage,won?"win":s.Phase==CoreDemoPhase.Settlement?"loss":"watchdog",turns,cards,powder,balls,initial,min,s.Hull,battle.PlayerBoardingHealth,battleStartingFame,s.Cargo.Fame,s.Cargo.Fame-battleStartingFame,Cash-battleStartingCash);
        Mark("Battle "+(won?"won":"lost")+": "+name+"; powder "+powder+", balls "+balls+", minimum hull "+min);
        if(won&&id==CoreGuidedTutorialContent.BossEventId&&tutorialFirstKill==null)tutorialFirstKill=new{id,name,battleRewardGold=Cash-battleStartingCash,battleRewardFame=s.Cargo.Fame-battleStartingFame,initialLoadoutSnapshot=battleStartingSnapshot,firstKill=CampaignState("tutorial-first-kill"),effectiveDeckNote="Guided lessons replace ordinary saved decks with authored fixed lesson piles; initialLoadoutSnapshot records saved loadout, not those transient lesson piles."};
        if(CoreBattleBalance.Classify(id)!=CoreBossKind.None)bossAttempts.Add(new{id,name,won,outcome=won?"win":s.Phase==CoreDemoPhase.Settlement?"loss":"watchdog",trip,turns,cards,powder,balls,stageTurns,boardingEntryAfterNavalTurns,startingMedicine,endingMedicine=s.Cargo.Medicine,enemyHull=battle.EnemyHull,enemyMaxHull=battle.EnemyMaxHull,enemyBoardingHealth=battle.EnemyBoardingHealth,enemyMaxBoardingHealth=battle.EnemyMaxBoardingHealth,playerBoardingHealth=battle.PlayerBoardingHealth,playerMaxBoardingHealth=battle.PlayerMaxBoardingHealth,battleStart=battleStartingSnapshot,battleEnd=CampaignState("battle-end")});
        if(won && (CoreLegendaryWarships.Find(id)!=null || CoreRouteBossCatalog.Find(id)!=null || id==CoreBattleBalance.TreasureBossId) && !firstKills.ContainsKey(id)){firstKills[id]=new {id,name,battleRewardGold=Cash-battleStartingCash,stageTurns,boardingEntryAfterNavalTurns,startingMedicine,endingMedicine=s.Cargo.Medicine,battleStart=battleStartingSnapshot,firstKill=CampaignState("first-kill")};if(legends.Count==6&&primaryCompletion==null)primaryCompletion=CampaignState("all-six-legends-first-completion");SaveCampaign();}
        if(won&&firstKills.ContainsKey(id)&&s.CanCaptureVoyageCheckpoint){string checkpointPath=Path.Combine(output,"Checkpoints");Directory.CreateDirectory(checkpointPath);File.WriteAllText(Path.Combine(checkpointPath,"trip-"+trip+"-"+id+".json"),new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=100}.Serialize(new{checkpoint=s.CaptureVoyageCheckpoint(),state=CampaignState("after-boss-victory")}),new UTF8Encoding(false));}
        if(s.Phase==CoreDemoPhase.Battle)throw new InvalidOperationException("Combat watchdog");
    }
    static double CardScore(int index)
    {
        var c=s.GetBattleCard(index);var b=s.Battle;var e=s.GetBattleCardEffects(c,b.Stage);bool boarding=b.Stage==NavalBattleStage.Boarding,preparing=b.Stage==NavalBattleStage.Preparation;
        if(c.BoardingSpec?.Key=="curse_10")return 1000;
        if(c.Action==NavalBattleCardAction.Energize || c.BoardingSpec?.Key=="basic_09")return 100;
        int enemy=boarding?b.EnemyBoardingHealth:b.EnemyHull,health=boarding?b.PlayerBoardingHealth:s.Hull,max=boarding?b.PlayerMaxBoardingHealth:s.MaxHull;
        int attack=CoreBattleIntentPresentation.Build(s).Where(x=>x.IsAttack).Sum(x=>x.Amount??0);
        int repair=Math.Min(max-health,e.Repair),block=Math.Min(Math.Max(0,(preparing?14:attack)-b.PlayerBlock),e.Block+(c.Block>0&&!boarding?b.PlayerDeckBuff:0));
        double score=repair*(health<max*.55?2.5:.65)+block*(health<=attack*2?2:.9);
        if(boarding)score=repair*(health<=attack*2?3.5:1.2)+block*(health<=attack*2?3.5:2.1);
        int damage=e.Damage+(c.Action==NavalBattleCardAction.Cannon?Math.Max(0,(s.EffectiveFirepower-8)/2)+b.PlayerLoadedShot:0);
        if(LearningPolicy&&boarding&&c.BoardingSpec?.Key=="blood_06")score+=Math.Min(max-health,Math.Max(0,damage-b.EnemyBlock))*(health<=attack*2?3.5:1.8);
        if(LearningPolicy&&boarding&&c.BoardingSpec?.Key=="basic_07"&&s.Cargo.Medicine>0)score+=Math.Min(max-health+attack*2,50)*(health<max*.75?1.5:.8);
        if(damage>=enemy+b.EnemyBlock)return 10000/(s.GetBattleCardEnergyCost(index)+.65);
        if(boarding)score+=damage*1.7; else if(!preparing)score+=damage*.5;
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
        var x=s.Settlement;string seed=s.Map.Seed,routeId=s.Map.Route.Id,rank=s.ActiveTreasureMap?.RankId??"";totalHours+=x.Hours;totalDistance+=x.DistanceNm;
        if(x.TreasureCompleted&&x.Outcome!=VoyageOutcome.Failure)ranks.Add(rank);
        Row(voyages,trip,voyageGoal,seed,rank,s.Map.DepartureName,x.Destination,x.Outcome,x.Hours,x.Hours/24,x.DistanceNm,x.Coverage,x.CompletedEvents,x.BankedGold,x.BankedFragments,x.BankedPirateExperience,x.TreasureCompleted,s.ProfileGold,s.Cargo.Water,s.Cargo.Food);
        Mark("Settlement: "+x.Outcome+" / "+x.Hours.ToString("F1")+" h / "+x.CompletedEvents+" events / "+x.BankedFragments+" fragments / "+x.BankedPirateExperience+" XP");
        s.ReturnToPortAfterSettlement();Log("ConfirmSettlement",x.Outcome+" rank="+rank);
        bool effective=trip>2&&x.Hours>0&&x.DistanceNm>0&&(x.CompletedEvents>0||x.BankedPirateExperience>0||x.BankedGold>0);
        if(effective)effectiveDevelopmentVoyages++;
        voyageProgress.Add(new{trip,goal=voyageGoal,routeId,outcome=x.Outcome.ToString(),hours=x.Hours,distanceNm=x.DistanceNm,completedEvents=x.CompletedEvents,bankedGold=x.BankedGold,bankedXP=x.BankedPirateExperience,effectiveDevelopmentVoyage=effective,effectiveDevelopmentVoyages,afterReturn=CampaignState("voyage-completed")});
        Save();
    }
}
