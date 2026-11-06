using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Finite deterministic policy samples, not an optimal captain or a population win rate.
// Shared deck/session/card policy comes from NavalBalanceProbe; this adapter preserves
// the actual 600 m entry, preparation, movement, enemy decisions and ram transitions.
public static class BossBalanceProbe
{
    static readonly Dictionary<string,int[]> Candidates=new Dictionary<string,int[]>();
    sealed class Scenario
    {
        public string Id; public int Tier;
        public Scenario(string id,int tier) { Id=id;Tier=tier; }
        public CoreEncounter Encounter()
        {
            var route=CoreRouteBossCatalog.Find(Id); if(route!=null)return route.CreateEncounter();
            var legend=CoreLegendaryWarships.Find(Id);if(legend!=null)return legend.CreateEncounter();
            return CoreLegendaryWarships.CreateTreasureEncounter();
        }
    }
    static readonly Scenario[] Scenarios={
        new Scenario("boss_route_shattered_sail_reef",1),new Scenario("boss_route_shattered_sail_reef_tide",1),
        new Scenario("boss_route_golden_tide_trade",3),new Scenario("boss_route_golden_tide_trade_thunder",3),
        new Scenario("boss_route_moon_tide_ruins",4),new Scenario("boss_route_moon_tide_ruins_arcane",4),
        new Scenario("legendary_pirate_iron_oath",2),new Scenario("legendary_pirate_red_widow",2),
        new Scenario("legendary_pirate_thunder_crown",3),new Scenario("legendary_pirate_ember_ark",3),
        new Scenario("legendary_pirate_tide_requiem",5),new Scenario("legendary_pirate_last_horizon",5),
        new Scenario(CoreBattleBalance.TreasureBossId,4)
    };
    static readonly string[] Schools={"ordinary","fire","arcane","curse","tide","thunder","blood"};
    sealed class Result
    {
        public string Boss,Name,School,Outcome,Trace,Loadout,Cohort,Policy;
        public int Tier,Seed,HealthPercent,MaxHull,InitialHull,FinalHull,MinimumHull,EnemyMax,EnemyFinal,
            NavalTurns,EffectiveTurns,PreparationTurns,TravelTurns,Cards,Energy,Damage,OwnWindowHullLoss,
            EnemyWindowHullLoss,Healing,MoveSteps,EnemyRams,Powder,Balls,Wood,Medicine,Drawn,MaxChain,
            StartMana,StartManaLimit,MaxMana,MaxManaLimit,NavalDeckSize;
        public string Csv()=>CsvLine(Boss,Name,Tier,Loadout,School,HealthPercent,Seed,Outcome,NavalTurns,EffectiveTurns,
            PreparationTurns,TravelTurns,Cards,Energy,MaxHull,InitialHull,FinalHull,MinimumHull,EnemyMax,EnemyFinal,
            Damage,OwnWindowHullLoss,EnemyWindowHullLoss,Healing,MoveSteps,EnemyRams,Powder,Balls,Wood,Medicine,Drawn,MaxChain,
            Cohort,Policy,StartMana,StartManaLimit,MaxMana,MaxManaLimit,NavalDeckSize,Trace);
    }
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        string output=args[0];int seeds=int.Parse(args[1]),maxTurns=int.Parse(args[2]);string filter=args.Length>3?args[3]:"*";
        bool mature=args.Length>4&&args[4]=="mature";
        bool survival=args.Length>5&&args[5]=="survival";
        Directory.CreateDirectory(output);BossProfileAudit.Main(new[]{output});
        string candidatePath=Path.Combine(output,"boss-candidates.csv");
        if(File.Exists(candidatePath))foreach(string line in File.ReadAllLines(candidatePath).Skip(1).Where(x=>!string.IsNullOrWhiteSpace(x)))
        {
            string[] cells=line.Split(',').Select(x=>x.Trim().Trim('"')).ToArray();
            Candidates.Add(cells[0],cells.Skip(1).Select(x=>string.IsNullOrEmpty(x)?-1:int.Parse(x)).ToArray());
        }
        var loadouts=NavalBalanceProbe.Loadouts(Path.Combine(output,"calibration-loadouts.csv")).ToArray();
        if(!mature&&loadouts.Length!=5)throw new InvalidOperationException("Expected the five fixed calibration loadouts.");
        var rows=new List<Result>();
        foreach(var scenario in Scenarios.Where(s=>filter=="*"||filter.Split(',').Contains(s.Id)))
        {
            foreach(string school in (mature?new[]{"arcane","curse","tide","thunder"}:Schools))
            {
                var loadout=loadouts.FirstOrDefault(l=>l.Tier==scenario.Tier&&(!mature||(string)l.GetType().GetField("School").GetValue(l)==school));
                if(loadout==null)continue;
                for(int seed=0;seed<seeds;seed++) foreach(int hp in new[]{100,70})
                {
                    MethodInfo factory=typeof(NavalBalanceProbe).GetMethod("CreateSession");
                    object[] factoryArgs=factory.GetParameters().Length>=5?new object[]{loadout,school,seed,true,mature}:new object[]{loadout,school,seed,true};
                    if(mature&&factoryArgs.Length<5)throw new InvalidOperationException("Mature shared policy is unavailable.");
                    var session=(CoreVoyageSession)factory.Invoke(null,factoryArgs);
                    rows.Add(Run(session,scenario,school,hp,seed,maxTurns,mature,survival));
                }
            }
            Console.WriteLine("Completed "+scenario.Id+"; "+rows.Count+" samples.");
        }
        if(rows.Count==0)throw new ArgumentException("BossFilter selected no scenarios.");
        File.WriteAllLines(Path.Combine(output,"boss-samples.csv"),new[]{"boss,name,tier,loadout,school,startingHealthPercent,seed,outcome,navalTurns,effectiveTurns,preparationTurns,travelTurns,cards,energySpent,maxHull,initialHull,finalHull,minimumHull,enemyMaxHull,enemyFinalHull,actualEnemyHullLost,ownWindowHullLoss,enemyWindowHullLoss,healing,moveSteps,enemyRamIntents,powder,balls,wood,medicine,drawn,maxChain,cohort,policy,startMana,startManaLimit,maxMana,maxManaLimit,navalDeckSize,trace"}.Concat(rows.Select(x=>x.Csv())),Encoding.UTF8);
        var summary=new List<string>{"boss,tier,school,startingHealthPercent,n,sunk,boarding,dead,timeout,watchdog,meanNavalTurns,meanEffectiveTurns,meanTurnsWhenSunk,meanEndHull,meanEnemyHullLost,meanCards,meanPowder,meanBalls,meanWood,meanMedicine,meanEnemyRams"};
        foreach(var g in rows.GroupBy(x=>new{x.Boss,x.Tier,x.School,x.HealthPercent}))
            summary.Add(CsvLine(g.Key.Boss,g.Key.Tier,g.Key.School,g.Key.HealthPercent,g.Count(),g.Count(x=>x.Outcome=="sunk"),g.Count(x=>x.Outcome=="boarding"),g.Count(x=>x.Outcome=="dead"),g.Count(x=>x.Outcome=="timeout"),g.Count(x=>x.Outcome=="action-watchdog"),F(g.Average(x=>x.NavalTurns)),F(g.Average(x=>x.EffectiveTurns)),g.Any(x=>x.Outcome=="sunk")?F(g.Where(x=>x.Outcome=="sunk").Average(x=>x.NavalTurns)):"",F(g.Average(x=>x.FinalHull)),F(g.Average(x=>x.Damage)),F(g.Average(x=>x.Cards)),F(g.Average(x=>x.Powder)),F(g.Average(x=>x.Balls)),F(g.Average(x=>x.Wood)),F(g.Average(x=>x.Medicine)),F(g.Average(x=>x.EnemyRams))));
        File.WriteAllLines(Path.Combine(output,"boss-summary.csv"),summary,Encoding.UTF8);
        File.WriteAllText(Path.Combine(output,"probe-notes.md"),
            "# Boss finite-policy probe\n\n"+
            "Real Core battle entry at 600 m, one preparation window, authored enemy movement and ram decisions are preserved. The captain uses the shared NavalBalanceProbe decks, loadouts and damage-oriented card policy, plus legal short-path cannon-range movement. No hands, mana or resources are injected after battle entry. Starting hull is fixed at 100% or 70% before battle. A boarding transition ends the naval observation and is not a death or a win.\n\n"+
            "Full and 70% samples, all seven attack decks, and the exact seed count are shown in CSV. These are limited policy samples, not school win rates or an optimal solver. Card/turn health losses are net deltas within each action window; blood costs and simultaneous healing can offset within one card. Damage means actual enemy hull lost, including end effects, not printed damage.\n\n"+
            "Shared card policy: "+(survival?"survival":"damage")+". Preparation and legal movement policies are unchanged.\n\n"+
            "Naval turns exclude preparation; effective turns had an in-range firing window, and travel turns had none. Finite provisions and shared special-card sources come from the frozen NavalBalanceProbe.cs. Tutorial practice/final battle are not part of the 13 normal Boss runs; their 15-profile snapshot still protects their boarding parameters.\n\n"+
            (mature?"Mature cohort: actual school preparation and field/source-card decisions are taken from the shared policy. At least one full preparation hand is naturally shuffled; starting mana and limit are measured, never injected. School-specific component totals come from the frozen mature loadouts. Support movement prioritizes the real field, and the cannon-range movement is a fallback; actual enemy ram remains possible.\n\n":"Baseline cohort: authored twelve-card attack decks, default preparation deck and no special-source cards; legal equipment numerical bonuses are still active.\n\n")+
            (Candidates.Count>0?"Candidate-only overrides were applied to temporary battle profiles before any player action. boss-candidates.csv lists the explicit final hull/cannon/guard/initial guard values; blank values retain authored numbers. Production sources were not edited.\n":"No candidate profile overrides were applied.\n"),Encoding.UTF8);
        Console.WriteLine("Completed "+rows.Count+" real Core boss samples. Outcomes stop at boarding entry; no boarding battle is simulated.");
        return 0;
    }
    static Result Run(CoreVoyageSession s,Scenario scenario,string school,int hp,int seed,int maxTurns,bool mature,bool survival)
    {
        if(s.Phase!=CoreDemoPhase.Sailing)throw new InvalidOperationException("Shared fixture must initialize the voyage RNG and return before battle.");
        typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s,Math.Max(1,(int)Math.Round(s.MaxHull*hp/100d)));
        var encounter=scenario.Encounter();
        typeof(CoreVoyageSession).GetMethod("StartBattle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{encounter,true});
        CoreBattleState b=s.Battle;
        if(Candidates.TryGetValue(scenario.Id,out int[] candidate))
        {
            CoreBattleProfile p=b.Profile;
            int guard=candidate.Length>2&&candidate[2]>=0?candidate[2]:p.Guard;
            int initial=candidate.Length>3&&candidate[3]>=0?candidate[3]:p.InitialGuard;
            b.Profile=new CoreBattleProfile(p.BossKind,p.Archetype,candidate[0],candidate[1],p.MeleeDamage,guard,initial,
                p.OrdinaryCrewCount,p.BowQuality,p.HullQuality,p.RamChance,p.RamRangeMeters,healthMultiplier:1m,
                boardingHealthMultiplier:p.BoardingHealthMultiplier,boardingGuard:p.BoardingGuard);
            if(b.Profile.BoardingHealth!=p.BoardingHealth)throw new InvalidOperationException("Candidate changed boarding health.");
            b.EnemyHull=b.EnemyMaxHull=b.Profile.EnemyHull;b.EnemyBlock=initial;b.NavalStartEnemyHull=b.EnemyHull;
        }
        if(b.Stage!=NavalBattleStage.Preparation||s.NavalDistanceCells!=6)throw new InvalidOperationException("Real boss entry was not retained.");
        var r=new Result{Boss=scenario.Id,Name=encounter.Name,Tier=scenario.Tier,Loadout=s.SelectedShip.Id,School=school,HealthPercent=hp,Seed=seed,Cohort=mature?"mature":"base",Policy=survival?"survival":"damage",
            MaxHull=s.MaxHull,InitialHull=s.Hull,MinimumHull=s.Hull,EnemyMax=b.EnemyMaxHull};
        r.NavalDeckSize=s.GetBattleDeck(NavalBattleStage.Naval).Count+s.GetBonusBattleCards(NavalBattleStage.Naval).Count;
        var trace=new List<string>();bool watchdog=false;
        for(int turn=0;turn<=maxTurns&&s.Phase==CoreDemoPhase.Battle&&b.Stage!=NavalBattleStage.Boarding;turn++)
        {
            bool preparing=b.Stage==NavalBattleStage.Preparation;
            if(!preparing&&r.NavalTurns>=maxTurns)break;
            if(preparing)r.PreparationTurns++;else r.NavalTurns++;
            trace.Add((preparing?"P":"N"+r.NavalTurns)+"@"+s.NavalDistanceCells);
            if(!preparing&&r.NavalTurns==1){r.StartMana=b.Schools.AvailableMana;r.StartManaLimit=b.Schools.ManaLimit;}
            r.MoveSteps+=Move(s,mature);
            bool effective=!preparing&&s.NavalCannonTargetInRange;
            int action;
            for(action=0;action<40&&s.Phase==CoreDemoPhase.Battle&&b.Stage!=NavalBattleStage.Boarding;action++)
            {
                if(mature&&!preparing)r.MoveSteps+=SupportMove(s);
                if(s.Phase!=CoreDemoPhase.Battle||b.Stage==NavalBattleStage.Boarding)break;
                int slot=mature&&preparing?(int)typeof(NavalBalanceProbe).GetMethod("ChoosePreparation").Invoke(null,new object[]{s}):NavalBalanceProbe.ChooseCard(s,survival);
                if(slot<0)break;
                int index=b.Hand[slot];var card=s.GetBattleCard(index);int energy=s.GetBattleCardEnergyCost(index);
                int before=s.Hull,enemy=b.EnemyHull,hand=b.Hand.Count,wood=s.Cargo.Wood,medicine=s.Cargo.Medicine;
                if(!NavalBalanceProbe.PlayChosen(s,slot))throw new InvalidOperationException("Shared policy selected a rejected card: "+card.Id);
                r.Cards++;r.Energy+=energy;r.Powder+=card.PowderCost;r.Balls+=card.CannonballCost;
                r.Wood+=Math.Max(0,wood-s.Cargo.Wood);r.Medicine+=Math.Max(0,medicine-s.Cargo.Medicine);
                r.Drawn+=Math.Max(0,b.Hand.Count-hand+1);Peaks(r,b);
                r.Damage+=Math.Max(0,enemy-b.EnemyHull);Observe(r,s,before,false);trace.Add(card.Id);
                if(s.Phase==CoreDemoPhase.Battle&&b.Stage!=NavalBattleStage.Boarding&&!s.NavalCannonTargetInRange)r.MoveSteps+=Move(s,mature);
                effective|=!preparing&&s.Phase==CoreDemoPhase.Battle&&s.NavalCannonTargetInRange;
            }
            if(effective)r.EffectiveTurns++;else if(!preparing)r.TravelTurns++;
            if(action>=40){watchdog=true;break;}
            if(s.Phase!=CoreDemoPhase.Battle||b.Stage==NavalBattleStage.Boarding)break;
            r.MoveSteps+=Move(s,mature);
            int turnHull=s.Hull,turnEnemy=b.EnemyHull;bool ram=s.EnemyWillRam;
            if(!s.EndBattleTurn())throw new InvalidOperationException("Legal end turn rejected.");
            if(ram)r.EnemyRams++;
            r.Damage+=Math.Max(0,turnEnemy-b.EnemyHull);Observe(r,s,turnHull,true);Peaks(r,b);
            trace.Add("E:"+b.LastAction);
        }
        r.FinalHull=s.Hull;r.EnemyFinal=b.EnemyHull;
        r.Outcome=s.Hull<=0||s.Phase==CoreDemoPhase.Settlement?"dead":b.EnemyHull<=0?"sunk":b.Stage==NavalBattleStage.Boarding?"boarding":watchdog?"action-watchdog":"timeout";
        r.Trace=string.Join(";",trace);return r;
    }
    static void Observe(Result r,CoreVoyageSession s,int before,bool enemy)
    {
        r.MinimumHull=Math.Min(r.MinimumHull,s.Hull);r.Healing+=Math.Max(0,s.Hull-before);
        if(enemy)r.EnemyWindowHullLoss+=Math.Max(0,before-s.Hull);else r.OwnWindowHullLoss+=Math.Max(0,before-s.Hull);
    }
    static void Peaks(Result r,CoreBattleState b)
    {r.MaxChain=Math.Max(r.MaxChain,b.Schools.ThunderChain);r.MaxMana=Math.Max(r.MaxMana,b.Schools.AvailableMana);r.MaxManaLimit=Math.Max(r.MaxManaLimit,b.Schools.ManaLimit);}
    static int SupportMove(CoreVoyageSession s)
    {
        var b=s.Battle;if(b==null)return 0;var before=b.PlayerPositionMeters;
        bool moved=(bool)typeof(NavalBalanceProbe).GetMethod("TrySupportMove").Invoke(null,new object[]{s});
        return moved?CoreVoyageSession.NavalGridDistance(before,b.PlayerPositionMeters):0;
    }
    static int Move(CoreVoyageSession s,bool mature)
    {
        int steps=mature?SupportMove(s):0;if(!s.NavalActionMovementActive)return steps;
        var field=s.NavalFieldAt(s.Battle.PlayerPositionMeters);
        bool beneficial=field!=null&&(field.Kind==CoreNavalFieldKind.Arcane||field.Kind==CoreNavalFieldKind.Thunder||field.Kind==CoreNavalFieldKind.Tide);
        if(mature&&beneficial&&s.NavalDistanceCells>=2&&s.NavalDistanceCells<=4)return steps;
        return steps+MoveForCannon(s);
    }
    // Legal grid movement only. Keep three/four cells of cannon range and do not
    // deliberately ram; enemy movement/ram remains unchanged. This is a small
    // greedy policy, not look-ahead over hidden future cards or random outcomes.
    static int MoveForCannon(CoreVoyageSession s)
    {
        if(!s.NavalActionMovementActive||s.Battle.NavalActionPoints<=0)return 0;
        var b=s.Battle;s.ClearNavalMovementPath();
        int start=s.NavalDistanceCells;double best=DistanceScore(start);var path=new List<VoyagePoint>();
        SearchPath(s,Math.Min(3,b.NavalActionPoints),ref best,path);
        s.ClearNavalMovementPath();if(path.Count==0)return 0;
        foreach(var point in path)if(!s.TryAppendNavalMovementStep(point,out string why))throw new InvalidOperationException(why);
        if(!s.CommitNavalMovement(out string reason))throw new InvalidOperationException(reason);
        if(s.NavalActionMovementActive&&b.NavalAwaitingHeadingSelection)
        {
            float heading=s.GetNavalArrivalHeadings().OrderBy(h=>HeadingScore(b,h)).First();
            if(!s.ConfirmNavalArrivalHeading(heading,out reason))throw new InvalidOperationException(reason);
        }
        return path.Count;
    }
    static void SearchPath(CoreVoyageSession s,int remaining,ref double best,List<VoyagePoint> chosen)
    {
        if(remaining<=0)return;
        foreach(var p in s.GetNavalLegalNextSteps().ToArray())
        {
            int d=CoreVoyageSession.NavalGridDistance(p,s.Battle.EnemyPositionMeters);if(d==0)continue;
            if(!s.TryAppendNavalMovementStep(p,out _))continue;
            double score=DistanceScore(d)+s.NavalMovementPlannedSteps*.01;
            if(score<best){best=score;chosen.Clear();chosen.AddRange(s.NavalMovementPath.Skip(1).Select(x=>x.PositionMeters));}
            SearchPath(s,remaining-1,ref best,chosen);s.UndoNavalMovementStep();
        }
    }
    static double DistanceScore(int d)=>d<=1?100:Math.Abs(d-4)+(d>4?.2:0);
    static double HeadingScore(CoreBattleState b,float h)
    {
        var next=new VoyagePoint(b.PlayerPositionMeters.X+(float)Math.Round(Math.Cos(h*Math.PI/180))*100,b.PlayerPositionMeters.Y+(float)Math.Round(Math.Sin(h*Math.PI/180))*100);
        int d=CoreVoyageSession.NavalGridDistance(next,b.EnemyPositionMeters);
        // Enemy plans normally approach after firing/guarding. At firing range,
        // finish sideways instead of pointing the next mandatory first step into
        // the closing enemy. At long range, closing to four cells still wins.
        return DistanceScore(d)+(d<CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters)?.5:0);
    }
    static string F(double n)=>n.ToString("0.00",CultureInfo.InvariantCulture);
    static string CsvLine(params object[] cells)=>string.Join(",",cells.Select(x=>"\""+Convert.ToString(x,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\""));
}
