using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using ShatteredSail.Core.Gameplay;

// Observes natural policy samples without replacing hands, state or decisions.
public static class BossBehaviorTelemetry
{
    static readonly string FileName=Path.Combine(Environment.GetEnvironmentVariable("BOSS_BEHAVIOR_OUTPUT"),"turn-observations.csv");
    static object Get(object value,string name) {
        if(value==null)return "";
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var field=value.GetType().GetField(name,flags);if(field!=null)return field.GetValue(value);
        var property=value.GetType().GetProperty(name,flags);return property==null?"":property.GetValue(value);
    }
    static string Csv(params object[] a)=>string.Join(",",a.Select(x=>"\""+Convert.ToString(x,CultureInfo.InvariantCulture).Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\""));
    public static void Observe(CoreVoyageSession s,string boss,string school,int hp,int seed,int turn,string window,string policy,CoreBattleState retainedBattle)
    {
        var b=s.Battle??retainedBattle;if(b==null)return;
        if(!File.Exists(FileName))File.WriteAllText(FileName,Csv("boss","school","startingHealthPercent","seed","turn","window","policy","phase","stage","playerHull","enemyHull","playerBlock","enemyBlock","enemyPosition","distanceCells","actionsCompleted","playerTurn","enemyTurn","completedPlayerTurns","enemyAllImmune","enemyImmunityMask","enemyImmunityExpires","enemyOwnSchoolImmune","fields","lastAction","phaseTurn","plannedMove","plannedStage","plannedStep","plannedLength","plannedAction","bossImmunityMask")+Environment.NewLine);
        string fields=string.Join(";",b.NavalFields.Cells.Select(c=>c.Kind+":"+c.X+":"+c.Y+":"+c.PlayerOwned+":"+Math.Max(0,c.ExpiresAtOwnerTurn-(c.PlayerOwned?b.NavalFields.CompletedEnemyTurns:b.NavalFields.CompletedPlayerTurns))));
        object plan=null,move=null;
        if(window=="before_enemy"&&s.Phase==CoreDemoPhase.Battle&&b.Stage==NavalBattleStage.Naval){plan=typeof(CoreVoyageSession).GetMethod("BuildEnemyPlan",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);move=Get(plan,"BossMove");}
        File.AppendAllText(FileName,Csv(boss,school,hp,seed,turn,window,policy,s.Phase,b.Stage,s.Hull,b.EnemyHull,b.PlayerBlock,b.EnemyBlock,b.EnemyPositionMeters.X+":"+b.EnemyPositionMeters.Y,CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters),b.EnemyNavalBossActionsCompleted,b.NavalFields.PlayerTurn,b.NavalFields.EnemyTurn,b.NavalFields.CompletedPlayerTurns,Get(b.NavalFields.Enemy,"AllFieldEffectsImmune"),Get(b.NavalFields.Enemy,"FieldImmunityMask"),Get(b.NavalFields.Enemy,"FieldImmunityExpires"),Get(b.NavalFields.Enemy,"NegativeFieldImmunityMask"),fields,b.LastAction,b.PhaseTurn,Get(move,"Name"),Get(move,"StageName"),Get(move,"CycleStep"),Get(move,"CycleLength"),Get(plan,"Action"),Get(b.NavalFields.Enemy,"BossFieldImmunityMask"))+Environment.NewLine);
    }
}

public static class BossCalibrationMain
{
    public static int Main(string[] args)
    {
        var rows=new System.Collections.Generic.List<string>{"id,name,tier,school,hull,cannon,guard,initialGuard,boardingHealth,boardingMelee,boardingGuard"};
        foreach(var scenario in BossBalanceProbe.Scenarios)
        {
            var route=CoreRouteBossCatalog.Find(scenario.Id);var legend=CoreLegendaryWarships.Find(scenario.Id);
            var profile=route!=null?route.Profile:legend!=null?legend.Profile:CoreBattleBalance.ThirdTreasureBoss;
            string school=route!=null?route.SchoolId:legend!=null?legend.SchoolId:"treasure";
            rows.Add(string.Join(",",new object[]{scenario.Id,scenario.Encounter().Name,scenario.Tier,school,profile.EnemyHull,profile.CannonDamage,profile.Guard,profile.InitialGuard,profile.BoardingHealth,profile.MeleeDamage,profile.BoardingGuard}));
        }
        File.WriteAllLines(Path.Combine(args[0],"boss-profiles.csv"),rows);
        return SchoolPowerProbe.Main(args);
    }
}
