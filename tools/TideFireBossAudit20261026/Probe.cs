using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;

// Read-only audit of current production Core. Controlled positions/fields are
// fixtures, not claims about draw probability, deck legality or global win rate.
public static class TideFireBossProbe
{
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static string Output;
    static string Csv(params object[] a) => string.Join(",", a.Select(x => "\"" + Convert.ToString(x, CultureInfo.InvariantCulture).Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", " ") + "\""));
    static void Pose(CoreVoyageSession s, bool player, int x, int y, float h) => typeof(CoreBattleState).GetMethod("SetTacticalPose", Hidden).Invoke(s.Battle, new object[]{player, new VoyagePoint(x*100,y*100),h});
    static CoreVoyageSession Start(CoreEncounter e, int distance = 6)
    {
        var s = new CoreVoyageSession();
        s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
        s.SetSeed("TIDE-FIRE-20261026"); s.StartVoyage();
        typeof(CoreVoyageSession).GetMethod("StartBattle", Hidden).Invoke(s,new object[]{e,true});
        s.EndBattleTurn();
        Pose(s,true,0,0,0); Pose(s,false,distance,0,180);
        return s;
    }
    static object Plan(CoreVoyageSession s) => typeof(CoreVoyageSession).GetMethod("BuildEnemyPlan", Hidden).Invoke(s,null);
    static object Get(object o,string n) => o.GetType().GetField(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static bool Field(CoreVoyageSession s, CoreNavalFieldKind k, int x,int y,int dx=0,int dy=0) => s.PlaceNavalField(k,true,x,y,false,dx,dy);
    static string Pos(CoreBattleState b) => (b.EnemyPositionMeters.X/100)+":"+(b.EnemyPositionMeters.Y/100);
    static bool PlayField(CoreVoyageSession s, string id,int x,int y,int dx=0,int dy=0)
    {
        int idx=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(i=>CoreVoyageSession.CardLibrary[i].Id==id);
        s.Battle.Hand.Clear();s.Battle.Hand.Add(idx);
        return s.PlayNavalCard(0,new CoreNavalCardPlayOptions{HasAnchor=true,AnchorX=x,AnchorY=y,DirectionX=dx,DirectionY=dy});
    }
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture; Output=args[0];
        var encounters=CoreRouteBossCatalog.All.Select(x=>x.CreateEncounter()).Concat(CoreLegendaryWarships.All.Select(x=>x.CreateEncounter())).Concat(new[]{CoreLegendaryWarships.CreateTreasureEncounter()}).ToArray();
        var rows=new List<string>{Csv("boss","scenario","round","enemyBefore","enemyAfter","plan","enemyHullBefore","enemyHullAfter","burnBefore","burnAfter","playerLoss","bossActions","tideCells","fireCells","lastAction")};
        foreach(var e in encounters)
        foreach(string scenario in new[]{"none","tide_east","tide_north","tide_refresh","fire_only","tide_then_fire","fire_then_tide","tide_then_fire_refresh","adjacent_fire_tide"})
        {
            var s=Start(e);var b=s.Battle;
            int fixtureHull=s.Hull,fixtureEnemyHull=b.EnemyHull,observedTurns=0;
            if(scenario.StartsWith("tide")) if(!PlayField(s,"card_school_tide_n01",4,0,scenario=="tide_north"?0:1,scenario=="tide_north"?1:0))throw new Exception("Tide card rejected");
            if(scenario.StartsWith("fire")) if(!PlayField(s,"card_school_fire_n01",4,0))throw new Exception("Fire card rejected");
            if(scenario=="adjacent_fire_tide")if(!PlayField(s,"card_school_fire_n01",4,0))throw new Exception("Fire card rejected");
            for(int t=1;t<=22 && s.Phase==CoreDemoPhase.Battle && b.Stage==NavalBattleStage.Naval;t++)
            {
                if(t==2&&scenario.StartsWith("tide_then_fire")) Require(PlayField(s,"card_school_fire_n01",4,0),"followup fire");
                if(t==2&&scenario=="fire_then_tide") Require(PlayField(s,"card_school_tide_n01",4,0,1,0),"followup tide");
                if(t==2&&scenario=="adjacent_fire_tide") Require(PlayField(s,"card_school_tide_n01",1,0,1,0),"adjacent tide");
                if((scenario=="tide_then_fire_refresh"||scenario=="tide_refresh")&&t>2&&t%4==1)Require(PlayField(s,"card_school_tide_n01",4,0,1,0),"refresh tide");
                if(scenario=="tide_then_fire_refresh"&&t>2&&t%4==2)Require(PlayField(s,"card_school_fire_n01",4,0),"refresh fire");
                string before=Pos(b);int eh=b.EnemyHull,ph=s.Hull,burn=b.EnemyBurnTurns;string action=Get(Plan(s),"Action").ToString();
                s.EndBattleTurn();
                observedTurns++;
                if(scenario=="tide_east"&&t<=7)Require(s.Hull==fixtureHull&&b.EnemyNavalBossActionsCompleted==0,"single-current seven-turn suppression");
                rows.Add(Csv(e.Id,scenario,t,before,Pos(b),action,eh,b.EnemyHull,burn,b.EnemyBurnTurns,ph-s.Hull,b.EnemyNavalBossActionsCompleted,b.NavalFields.Cells.Count(c=>c.Kind==CoreNavalFieldKind.Tide),b.NavalFields.Cells.Count(c=>c.Kind==CoreNavalFieldKind.Fire),b.LastAction));
            }
            if(scenario=="tide_refresh"||scenario=="tide_then_fire_refresh")
                Require(observedTurns==22&&s.Hull==fixtureHull&&b.EnemyHull==fixtureEnemyHull&&b.EnemyNavalBossActionsCompleted==0,"renewed-current stalemate");
        }
        File.WriteAllLines(Path.Combine(Output,"boss-scenarios.csv"),rows);
        DurationAndCards(encounters[0]);
        Search(encounters[0]);
        Console.WriteLine("Wrote "+(rows.Count-1)+" boss turn observations.");
        Console.WriteLine("PASS: all 13 bosses suppressed for first 7 turns by one current; both renewal fixtures stayed at zero damage/zero boss actions for 22 turns. Hand supply is injected, not a natural-draw guarantee.");
        return 0;
    }
    static void Require(bool ok,string what){if(!ok)throw new Exception(what+" was rejected");}
    static void DurationAndCards(CoreEncounter e)
    {
        var rows=new List<string>{Csv("duration","round","rawBurnDamage","totalDamage","burnAfter","fieldRemaining")};
        var totals=new List<string>{Csv("duration","firstFiveTurns","firstTenTurns","totalDamage","lastDamageTurn")};
        foreach(int duration in new[]{5,7,10})
        {
            var s=Start(e);var b=s.Battle;b.EnemyHull=b.EnemyMaxHull=100000;b.EnemyBlock=0;
            Require(Field(s,CoreNavalFieldKind.Fire,4,0),"fire fixture");
            if(duration>5)typeof(CoreVoyageSession).GetMethod("ExtendAllPlayerFields",Hidden).Invoke(s,new object[]{CoreNavalFieldKind.Fire,duration-5});
            int t=0,total=0,first5=0,first10=0;
            do
            {
                t++;s.NavalFieldsEndTurn(true);s.NavalFieldsBeginTurn(false);
                int before=b.EnemyHull;
                typeof(CoreVoyageSession).GetMethod("ApplySchoolNaturalBurn",Hidden).Invoke(s,null);
                s.NavalFieldsEndTurn(false);s.NavalFieldsBeginTurn(true);
                int damage=before-b.EnemyHull;total+=damage;if(t<=5)first5+=damage;if(t<=10)first10+=damage;
                rows.Add(Csv(duration,t,damage,total,b.EnemyBurnTurns,s.GetNavalFieldRemainingTurns(s.NavalFieldAt(b.EnemyPositionMeters))));
            }while((b.NavalFields.Cells.Count>0||b.EnemyBurnTurns>0)&&t<200);
            totals.Add(Csv(duration,first5,first10,total,t));
            Require(total==(duration==5?100:duration==7?350:1375),"fire-duration total");
        }
        File.WriteAllLines(Path.Combine(Output,"fire-duration.csv"),rows);
        File.WriteAllLines(Path.Combine(Output,"fire-duration-summary.csv"),totals);
        var cards=new List<string>{Csv("id","name","level","energy","guard","draw","fieldExtension","description")};
        var session=new CoreVoyageSession();
        foreach(string id in new[]{"card_special_sail_starshear_rig","card_special_sail_thunderphoenix","card_special_sail_ghostcurrent_canvas","card_special_sail_tempest_wing"})
        {
            int idx=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(i=>CoreVoyageSession.CardLibrary[i].Id==id);
            for(int level=0;level<=3;level++)
            {
                var c=session.GetBattleCardAtLevel(idx,level);cards.Add(Csv(id,c.Name,c.UpgradeLevel,c.Energy,c.Block,c.Draw,c.SchoolEffect.Power,c.Description));
            }
        }
        File.WriteAllLines(Path.Combine(Output,"extension-cards.csv"),cards);
    }
    static void Search(CoreEncounter e)
    {
        var rows=new List<string>{Csv("tideX","tideY","dx","dy","fireX","fireY","order","enemyDamage","playerDamage","bossActions","endPosition","burn","stage")};
        int tried=0;
        foreach(int tx in Enumerable.Range(1,4))foreach(int ty in Enumerable.Range(-3,7))
        {
            if(Math.Abs(tx)+Math.Abs(ty)>4)continue;
            foreach(var dir in new[]{new[]{1,0},new[]{-1,0},new[]{0,1},new[]{0,-1}})
            foreach(int fx in Enumerable.Range(1,4))foreach(int fy in Enumerable.Range(-3,7))
            {
                if(Math.Abs(fx)+Math.Abs(fy)>4)continue;
                foreach(bool reverse in new[]{false,true})
                {
                    var s=Start(e);var b=s.Battle;int initial=b.EnemyHull,own=s.Hull;
                    if(reverse) {Field(s,CoreNavalFieldKind.Fire,fx,fy);Field(s,CoreNavalFieldKind.Tide,tx,ty,dir[0],dir[1]);}
                    else {Field(s,CoreNavalFieldKind.Tide,tx,ty,dir[0],dir[1]);Field(s,CoreNavalFieldKind.Fire,fx,fy);}
                    int turns=0;
                    while(turns++<5&&s.Phase==CoreDemoPhase.Battle&&b.Stage==NavalBattleStage.Naval)s.EndBattleTurn();
                    tried++;
                    if(own==s.Hull&&initial>b.EnemyHull)
                        rows.Add(Csv(tx,ty,dir[0],dir[1],fx,fy,reverse?"fire-tide":"tide-fire",initial-b.EnemyHull,own-s.Hull,b.EnemyNavalBossActionsCompleted,Pos(b),b.EnemyBurnTurns,b.Stage));
                }
            }
        }
        File.WriteAllLines(Path.Combine(Output,"geometry-search.csv"),rows);
        Console.WriteLine("Controlled legal-anchor search: "+tried+" configurations, "+(rows.Count-1)+" inflicted damage without player hull loss in 5 turns. Simultaneous placement bypasses draw/energy and is geometry-only.");
    }
}
