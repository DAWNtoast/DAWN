using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

public static class FieldAudit
{
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,Hidden).Invoke(target,args);
    static void Save(string dir,string file,List<string> rows) => File.WriteAllLines(Path.Combine(dir,file),rows,Utf8);
    static CoreVoyageSession Setup(CoreNavalFieldKind? field,int turns,bool owner=true,CoreEncounter encounter=null)
    {
        var s = new CoreVoyageSession(); s.SetSeed("BOARDING-FIELD-AUDIT");
        s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10) });
        if(encounter==null) s.StartTrainingBattle(CoreTrainingTargets.All[0]);
        else { s.StartVoyage();Call(s,"StartBattle",encounter,true); }
        s.EndBattleTurn();
        Call(s.Battle,"SetTacticalPose",true,new VoyagePoint(0,0),90f);
        Call(s.Battle,"SetTacticalPose",false,new VoyagePoint(100,0),90f);
        s.Battle.Alignment=NavalBattleAlignment.Parallel;
        s.Battle.NavalActionPoints=10;
        if(field.HasValue)
        {
            if(!s.PlaceNavalField(field.Value,owner,0,0,false,1,0)) throw new Exception("Placement failed");
            int initial=field.Value==CoreNavalFieldKind.Arcane?3:5;
            if(turns>initial) Call(s,"ExtendAllPlayerFields",field.Value,turns-initial);
            else foreach(var cell in s.Battle.NavalFields.Cells)cell.ExpiresAtOwnerTurn-=initial-turns;
        }
        if(!s.TryBoard()) throw new Exception("Boarding failed");
        if(field.HasValue && field!=CoreNavalFieldKind.Tide && s.Battle.BoardingFields.Remaining(field.Value)!=turns)
            throw new Exception("Inherited duration mismatch");
        s.Battle.PlayerBlock=s.Battle.EnemyBlock=0;
        return s;
    }
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        var totals=new List<string>{"scenario,duration,totalEnemyLoss,peakEnemyBurn,lastDamageRound,enemyBurnAfter"};
        var ticks=new List<string>{"scenario,duration,round,remainingBefore,enemyBurnBefore,enemyLoss,enemyBurnAfter,remainingAfter"};
        foreach(int duration in new[]{1,3,5,7,8,10,15})
        {
            var s=Setup(CoreNavalFieldKind.Fire,duration);s.Battle.PlayerBoardingHealth=s.Battle.PlayerMaxBoardingHealth=1000000;
            s.Battle.EnemyBoardingHealth=s.Battle.EnemyMaxBoardingHealth=1000000;
            int peak=0,last=0;
            for(int round=1;round<=500;round++)
            {
                int remaining=s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire),burn=s.Battle.EnemyBurnDamage,health=s.Battle.EnemyBoardingHealth;
                Call(s,"BPlayerEnd");Call(s,"BEnemyEnd");
                int lost=health-s.Battle.EnemyBoardingHealth;peak=Math.Max(peak,s.Battle.EnemyBurnDamage);if(lost>0)last=round;
                ticks.Add($"inherited_fire,{duration},{round},{remaining},{burn},{lost},{s.Battle.EnemyBurnDamage},{s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire)}");
                if(!s.Battle.BoardingFields.Contains(CoreNavalFieldKind.Fire)&&s.Battle.EnemyBurnDamage==0)break;
            }
            totals.Add($"inherited_fire,{duration},{1000000-s.Battle.EnemyBoardingHealth},{peak},{last},{s.Battle.EnemyBurnDamage}");
        }
        foreach(int burn in new[]{4,8,14,20})
        {
            var s=Setup(null,0);s.Battle.PlayerBoardingHealth=s.Battle.EnemyBoardingHealth=1000000;
            Call(s,"BAddEnemyBurn",burn,2);
            for(int turn=0;turn<burn;turn++)Call(s,"BEnemyEnd");
            totals.Add($"single_burn,{burn},{1000000-s.Battle.EnemyBoardingHealth},{burn},{burn},{s.Battle.EnemyBurnDamage}");
        }
        Save(args[0],"field-only-totals.csv",totals);Save(args[0],"field-only-ticks.csv",ticks);
        var bosses=new List<CoreEncounter>();bosses.AddRange(CoreRouteBossCatalog.All.Select(x=>x.CreateEncounter()));
        bosses.AddRange(CoreLegendaryWarships.All.Select(x=>x.CreateEncounter()));bosses.Add(CoreLegendaryWarships.CreateTreasureEncounter());
        var pressure=new List<string>{"bossId,fieldDuration,initialHealth,roundsUntilDeathOrLimit,endHealth,outcome"};
        foreach(var boss in bosses)foreach(int turns in new[]{0,5,7,10,15})
        {
            var s=Setup(turns==0?(CoreNavalFieldKind?)null:CoreNavalFieldKind.Fire,turns,true,boss);var battle=s.Battle;
            battle.PlayerBoardingHealth=battle.PlayerMaxBoardingHealth=1000000;
            int initial=battle.EnemyBoardingHealth,round=0;
            while(s.Battle!=null&&s.Phase==CoreDemoPhase.Battle&&round<40)
            {
                battle.PlayerBlock=1000000;round++;s.EndBattleTurn();
            }
            pressure.Add($"{boss.Id},{turns},{initial},{round},{battle.EnemyBoardingHealth},{(battle.EnemyBoardingHealth==0?"enemy_dies":"survives_40")}");
        }
        Save(args[0],"boss-no-player-offense-diagnostic.csv",pressure);
        var fields=new List<string>{"kind,duration,enemyTotalApplied,playerTotalApplied,enemyPeakResource,playerPeakResource"};
        foreach(var kind in new[]{CoreNavalFieldKind.Curse,CoreNavalFieldKind.Arcane})foreach(int duration in new[]{3,5,10})
        {
            var s=Setup(kind,duration);s.Battle.PlayerBoardingHealth=s.Battle.PlayerMaxBoardingHealth=1000000;
            s.Battle.EnemyBoardingHealth=s.Battle.EnemyMaxBoardingHealth=1000000;
            int enemy=0,player=0,enemyPeak=0,playerPeak=0;
            for(int round=1;round<=duration+1;round++)
            {
                if(kind==CoreNavalFieldKind.Arcane)
                {
                    s.Battle.Schools.Mana=s.Battle.Schools.ManaLimit;
                    s.Battle.BoardingEnemy.Mana=CoreBoardingEnemyState.ManaLimit;
                    Call(s,"BBoardingFieldsBeginTurn",true);Call(s,"BBoardingFieldsBeginTurn",false);
                    player+=s.Battle.Schools.Mana-s.Battle.Schools.ManaLimit;
                    enemy+=s.Battle.BoardingEnemy.Mana-CoreBoardingEnemyState.ManaLimit;
                    playerPeak=Math.Max(playerPeak,s.Battle.Schools.Mana);enemyPeak=Math.Max(enemyPeak,s.Battle.BoardingEnemy.Mana);
                }
                Call(s,"BPlayerEnd");Call(s,"BEnemyEnd");
            }
            if(kind==CoreNavalFieldKind.Curse){enemy=s.Battle.Boarding.CurseMarks;player=s.Battle.BoardingEnemy.CurseMarks;enemyPeak=enemy;playerPeak=player;}
            fields.Add($"{kind},{duration},{enemy},{player},{enemyPeak},{playerPeak}");
        }
        Save(args[0],"other-field-resources.csv",fields);
        var extension=new List<string>{"cardId,name,level,energy,extraTurns,resultingFireDuration"};
        var reader=new CoreVoyageSession();
        foreach(string id in new[]{"card_special_sail_starshear_rig","card_special_sail_thunderphoenix"})
        {
            int index=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(i=>CoreVoyageSession.CardLibrary[i].Id==id);
            for(int level=0;level<=reader.GetCardMaximumUpgradeLevel(index);level++)
            {
                var card=reader.GetBattleCardAtLevel(index,level);
                if(card.SchoolEffect.Mechanic!=CoreSchoolMechanic.ComponentFieldExtend)throw new Exception("Unexpected extension definition");
                var s=new CoreVoyageSession();s.StartTrainingBattle(CoreTrainingTargets.All[0]);s.EndBattleTurn();
                Call(s.Battle,"SetTacticalPose",true,new VoyagePoint(0,0),90f);
                s.PlaceNavalField(CoreNavalFieldKind.Fire,true,0,0,false,1,0);
                Call(s,"ApplyComponentFieldCardEffect",card);
                extension.Add($"{id},{card.Name},{level},{card.Energy},{card.SchoolEffect.Power},{s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0,0))}");
            }
        }
        Save(args[0],"actual-extension-card-values.csv",extension);
        var reach=new List<string>{"step,energy,powder,actionPoints,fireRemaining,stage,playerBurn,enemyBurn"};
        var reachable=new CoreVoyageSession();
        reachable.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10)});
        reachable.ConfigureSpecialCardSources(null,new[]{"sail_thunderphoenix"});
        reachable.SetSeed("FIELD-ACTUAL-CARD-REACH");reachable.StartTrainingBattle(CoreTrainingTargets.All[0]);reachable.EndBattleTurn();
        Call(reachable.Battle,"SetTacticalPose",true,new VoyagePoint(0,0),90f);
        Call(reachable.Battle,"SetTacticalPose",false,new VoyagePoint(100,0),90f);
        reachable.Battle.Alignment=NavalBattleAlignment.Parallel;
        reachable.Battle.Energy=3;reachable.Battle.NavalActionPoints=1;reachable.Cargo.Powder=1;
        reachable.Battle.Hand.Clear();
        foreach(string id in new[]{"card_school_fire_n01","card_special_sail_thunderphoenix"})
            reachable.Battle.Hand.Add(Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(i=>CoreVoyageSession.CardLibrary[i].Id==id));
        Action<string> record=step=>reach.Add($"{step},{reachable.Battle.Energy},{reachable.Cargo.Powder},{reachable.Battle.NavalActionPoints},{(reachable.Battle.Stage==NavalBattleStage.Boarding?reachable.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire):reachable.GetNavalFieldRemainingTurns(reachable.GetNavalFieldAt(0,0)))},{reachable.Battle.Stage},{reachable.Battle.PlayerBurnStacks},{reachable.Battle.EnemyBurnDamage}");
        record("initial_controlled_legal_hand");
        if(!reachable.PlayNavalCard(0,new CoreNavalCardPlayOptions{HasAnchor=true,AnchorX=0,AnchorY=0}))throw new Exception("Actual fire card failed: "+reachable.CardUnavailableReason(0));
        record("played_fire_field");
        int extendHand=reachable.Battle.Hand.FindIndex(i=>CoreVoyageSession.CardLibrary[i].Id=="card_special_sail_thunderphoenix");
        if(!reachable.PlayCard(extendHand))throw new Exception("Actual extension card failed: "+reachable.CardUnavailableReason(extendHand));
        record("played_extend");
        if(!reachable.TryBoard())throw new Exception("Actual board failed");
        record("actual_boarded");
        if(reachable.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire)!=10)throw new Exception("Reachable 10-turn fire failed");
        Save(args[0],"actual-card-sequence.csv",reach);
        Console.WriteLine("Field audit complete: actual Core inheritance and end effects; isolated field burden and 65 diagnostic Boss cases. No deck win-rate claims.");
        return 0;
    }
}
