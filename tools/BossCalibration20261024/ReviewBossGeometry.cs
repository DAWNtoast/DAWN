using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;

// Independent controlled behavior review. Does not modify production definitions.
public static class ReviewBossGeometry
{
    static readonly BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    public static int Main(string[] args)
    {
        foreach (var xy in new[] { new[] {400,0},new[] {-400,0},new[] {0,400},new[] {0,-400},new[] {600,0} })
        {
            var s=Start("boss_route_golden_tide_trade_thunder");
            Pose(s.Battle,false,xy[0],xy[1],xy[0]>0?180:xy[0]<0?0:xy[1]>0?270:90);
            for(int i=0;i<4 && s.Battle.Stage==NavalBattleStage.Naval;i++)
            {
                string intent=s.GetEnemyIntent().Replace('\n',' ');
                int turn=s.Battle.PhaseTurn;
                s.EndBattleTurn();
                var b=s.Battle;
                Console.WriteLine("thunder start=("+xy[0]+","+xy[1]+") turn="+turn+" pos=("+b.EnemyPositionMeters.X+","+b.EnemyPositionMeters.Y+") chain="+b.NavalFields.Enemy.ThunderChain+" fields="+b.NavalFields.Cells.Count+" stage="+b.Stage+" | "+intent);
            }
        }
        var blood=Start("boss_route_golden_tide_trade");
        Pose(blood.Battle,false,400,0,180);
        blood.Battle.EnemyHull=100;
        blood.Battle.Schools.CurseMarks=49;
        typeof(CoreVoyageSession).GetProperty("Hull").SetValue(blood,blood.MaxHull);
        string quoted=blood.GetEnemyIntent().Replace('\n',' ');
        int before=blood.Hull;
        var bloodState=blood.Battle;
        blood.EndBattleTurn();
        Console.WriteLine("blood preHP=100,marks=49; quoted="+quoted+"; actualPlayerLoss="+(before-blood.Hull)+"; enemyHP="+bloodState.EnemyHull+"; executed="+bloodState.EnemyNavalExecutedByCurse);
        return 0;
    }
    static CoreVoyageSession Start(string id)
    {
        var s=new CoreVoyageSession();s.SetSeed("review-geometry-21");s.StartVoyage();
        typeof(CoreVoyageSession).GetMethod("StartBattle",P).Invoke(s,new object[]{CoreRouteBossCatalog.Find(id).CreateEncounter(),true});
        typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s,10000);
        s.Battle.Stage=NavalBattleStage.Naval;s.Battle.PhaseTurn=1;s.Battle.Turn=1;
        s.Battle.EnemyHull=s.Battle.EnemyMaxHull=10000;
        Pose(s.Battle,true,0,0,90);
        return s;
    }
    static void Pose(CoreBattleState b,bool player,int x,int y,float heading) =>
        typeof(CoreBattleState).GetMethod("SetTacticalPose",P).Invoke(b,new object[]{player,new VoyagePoint(x,y),heading});
}
