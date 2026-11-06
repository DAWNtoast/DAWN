using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Read-only profile/intent extraction. Does not run or alter authored combat definitions.
public static class BossProfileAudit
{
    public static int Main(string[] args)
    {
        string dir=args[0];Directory.CreateDirectory(dir);
        var rows=new List<string>{"id,name,kind,archetype,hull,healthMultiplier,cannon,guard,initialGuard,ramChance,legacyRamRange,bowQuality,hullQuality,boardingHealth,melee,boardingGuard,boardingHealthMultiplier"};
        var plans=new List<string>{"id,phaseTurn,action,rawAttack,armor2Damage,armor10Damage,armor16Damage,guard,repair,addedLoad,loadBefore,moveMeters"};
        foreach(var boss in CoreRouteBossCatalog.All) Add(rows,plans,boss.Id,boss.Name,boss.Profile);
        foreach(var boss in CoreLegendaryWarships.All) Add(rows,plans,boss.Id,boss.Name,boss.Profile);
        Add(rows,plans,CoreBattleBalance.TreasureBossId,"守藏舰",CoreBattleBalance.ThirdTreasureBoss);
        Add(rows,plans,"tutorial_profile","教学Profile（实战船体另有80硬编码）",CoreGuidedTutorialContent.BossProfile);
        Add(rows,plans,"tutorial_replay","普通海图重放教学遭遇",CoreBattleBalance.Resolve(new CoreEncounter(CoreGuidedTutorialContent.BossEventId,"教学Boss","战斗","",true,false,0,0),0));
        File.WriteAllLines(Path.Combine(dir,"boss-profiles.csv"),rows,Encoding.UTF8);
        File.WriteAllLines(Path.Combine(dir,"boss-nine-turn-plans.csv"),plans,Encoding.UTF8);
        Console.WriteLine("Exported "+(rows.Count-1)+" profiles and "+(plans.Count-1)+" naval plan samples.");return 0;
    }
    static void Add(List<string> rows,List<string> plans,string id,string name,CoreBattleProfile p)
    {
        rows.Add(string.Join(",",new object[]{id,name,p.BossKind,p.Archetype,p.EnemyHull,p.HealthMultiplier,p.CannonDamage,p.Guard,p.InitialGuard,p.RamChance,p.RamRangeMeters,p.BowQuality,p.HullQuality,p.BoardingHealth,p.MeleeDamage,p.BoardingGuard,p.BoardingHealthMultiplier}));
        var s=new CoreVoyageSession();s.StartTrainingBattle(CoreTrainingTargets.All[0]);
        s.Battle.Profile=p;s.Battle.Stage=NavalBattleStage.Naval;s.Battle.EnemyLoadedShot=4;
        var pose=typeof(CoreBattleState).GetMethod("SetTacticalPose",BindingFlags.NonPublic|BindingFlags.Instance);
        pose.Invoke(s.Battle,new object[]{true,new VoyagePoint(0,0),90f});pose.Invoke(s.Battle,new object[]{false,new VoyagePoint(400,0),90f});
        var get=typeof(CoreVoyageSession).GetMethod("BuildEnemyPlan",BindingFlags.NonPublic|BindingFlags.Instance);
        for(int turn=1;turn<=9;turn++)
        {
            s.Battle.PhaseTurn=turn;object plan=get.Invoke(s,null);string action=Field(plan,"Action").ToString();int attack=(int)Field(plan,"Attack"),load=(int)Field(plan,"Load");
            plans.Add(string.Join(",",new object[]{id,turn,action,attack,CoreNavalStatRules.ReduceIncomingCannonDamage(attack,2),CoreNavalStatRules.ReduceIncomingCannonDamage(attack,10),CoreNavalStatRules.ReduceIncomingCannonDamage(attack,16),Field(plan,"Guard"),Field(plan,"Repair"),load,s.Battle.EnemyLoadedShot,Field(plan,"Move")}));
            s.Battle.EnemyLoadedShot=action=="Cannon"?0:Math.Min(12,s.Battle.EnemyLoadedShot+load);
        }
    }
    static object Field(object x,string n)=>x.GetType().GetField(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(x);
}
