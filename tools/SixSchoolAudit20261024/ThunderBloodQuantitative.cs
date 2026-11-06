using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Reproducible three-turn policy sample, not an optimal solver or a global win-rate claim.
// Uses the real rules and random dealt ten-card decks. Navigation alone is skipped.
public static class ThunderBloodQuantitative
{
    static CoreBattleState ActiveBattle;
    static readonly Dictionary<string, int> Ids = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
        .ToDictionary(i => CoreVoyageSession.CardLibrary[i].Id, i => i);
    static readonly string[][] Boarding = {
        new[] {"basic_09","basic_10","basic_03","basic_02","basic_08","basic_11","basic_04","basic_05","basic_06","basic_09"},
        new[] {"basic_09","basic_10","thunder_01","thunder_03","thunder_09","thunder_11","thunder_02","thunder_13","thunder_12","thunder_07"},
        new[] {"basic_09","basic_10","blood_05","blood_04","blood_02","blood_01","blood_06","blood_07","blood_03","blood_09"}
    };
    static readonly string[][] Naval = {
        new[] {"card_cannon_barrage","card_cannon_barrage","card_chain_shot","card_chain_shot","card_energy_order","card_tactical_draw","card_brace","card_evasive_maneuver","card_emergency_repair","card_load_shot"},
        new[] {"card_school_thunder_n03","card_school_thunder_n03","card_school_thunder_b03","card_school_thunder_n02","card_school_thunder_b04","card_school_thunder_b06","card_school_thunder_n05","card_school_thunder_n04","card_energy_order","card_brace"},
        new[] {"card_school_blood_b03","card_school_blood_b03","card_school_blood_b04","card_school_blood_b04","card_school_blood_b06","card_school_blood_n03","card_school_blood_b05","card_energy_order","card_brace","card_tactical_draw"}
    };
    sealed class Result
    {
        public string School, Stage, Enemy, Policy, Outcome, Trace;
        public int Seed, PlayerMax, EnemyMax, EnemyGuard, EnemyAttack, Turns, Plays, Damage, EnemyTurnDamage,
            PaidLife, Healing, EndHealth, MaxChain, Powder, Balls, Drawn;
        public string Csv() => string.Join(",",new object[]{School,Stage,Enemy,Policy,Seed,Outcome,PlayerMax,EnemyMax,EnemyGuard,EnemyAttack,Turns,Plays,Damage,EnemyTurnDamage,PaidLife,Healing,EndHealth,MaxChain,Powder,Balls,Drawn,"\""+Trace+"\""});
    }
    public static int Main(string[] args)
    {
        string output = args[0]; Directory.CreateDirectory(output); var rows = new List<Result>();
        foreach (var stage in new[] { NavalBattleStage.Naval, NavalBattleStage.Boarding })
            for (int school = 0; school < 3; school++) for (int enemy = 0; enemy < 3; enemy++)
                for (int policy = 0; policy < 2; policy++) for (int seed = 0; seed < 32; seed++)
                    rows.Add(Run(stage, school, enemy, policy, seed));
        File.WriteAllLines(Path.Combine(output,"three-turn-samples.csv"), new[]{"school,stage,enemy,policy,seed,outcome,playerMax,enemyMax,guardBase,attackBase,turns,plays,damage,enemyTurnLifeLoss,paidLife,healing,endHealth,maxChain,powder,balls,drawn,trace"}.Concat(rows.Select(x=>x.Csv())), Encoding.UTF8);
        var summary = new List<string>{"school,stage,enemy,policy,n,wins,deaths,meanDamage,meanEnemyTurnLoss,meanPaidLife,meanHealing,meanEndHealth,meanPlays,meanDrawn,maxChain,meanPowder,meanBalls"};
        foreach(var g in rows.GroupBy(x=>new{x.School,x.Stage,x.Enemy,x.Policy}))
            summary.Add(string.Join(",", new object[]{g.Key.School,g.Key.Stage,g.Key.Enemy,g.Key.Policy,g.Count(),g.Count(x=>x.Outcome=="win"),g.Count(x=>x.Outcome=="loss"),
                F(g.Average(x=>x.Damage)),F(g.Average(x=>x.EnemyTurnDamage)),F(g.Average(x=>x.PaidLife)),F(g.Average(x=>x.Healing)),F(g.Average(x=>x.EndHealth)),F(g.Average(x=>x.Plays)),F(g.Average(x=>x.Drawn)),g.Max(x=>x.MaxChain),F(g.Average(x=>x.Powder)),F(g.Average(x=>x.Balls))}));
        File.WriteAllLines(Path.Combine(output,"three-turn-summary.csv"), summary, Encoding.UTF8);
        Console.WriteLine("Completed "+rows.Count+" real-rule samples; each has at most three player/enemy turns."); return 0;
    }
    static string F(double x)=>x.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
    static int Health(CoreVoyageSession s, NavalBattleStage stage)=> stage==NavalBattleStage.Boarding?(s.Battle??ActiveBattle).PlayerBoardingHealth:s.Hull;
    static int EnemyHealth(CoreVoyageSession s, NavalBattleStage stage)=> stage==NavalBattleStage.Boarding?(s.Battle??ActiveBattle).EnemyBoardingHealth:(s.Battle??ActiveBattle).EnemyHull;
    static Result Run(NavalBattleStage stage,int school,int enemy,int policy,int seed)
    {
        var s=new CoreVoyageSession();
        s.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10)});
        s.SelectShip(0); s.SelectRoute(enemy==0?0:1); s.SetSeed("THUNDER-BLOOD-QUANT-"+seed);
        s.ConfigureCrewCapabilities(false,false,false,false,false,false);
        s.ConfigureDepartureCrew(s.CrewCountValue,0); s.ConfigureComponentBonuses(0,0,0,0,0);
        s.ConfigureSpecialCardSources(Array.Empty<string>(),Array.Empty<string>());
        var deck=(stage==NavalBattleStage.Boarding?Boarding[school].Select(x=>"card_boarding_"+x):Naval[school]).Select(x=>Ids[x]).ToArray();
        if(!s.SetBattleDeck(stage,deck))throw new Exception("Illegal deck");
        s.Cargo.Powder=10;s.Cargo.Cannonballs=10;s.Cargo.Medicine=0;s.Cargo.Wood=0;
        s.StartVoyage(); if(s.Phase!=CoreDemoPhase.Sailing) throw new Exception("Voyage setup rejected; seed not initialized.");
        var e=enemy==2?CoreRouteBossCatalog.Find("boss_route_golden_tide_trade").CreateEncounter():new CoreEncounter(enemy==0?"event_reef_coral_ambush":"gold_customs","审计目标","战斗","",true,false,2,enemy==0?95:115);
        Call(s,"StartBattle",e,enemy==2); Call(s,"EnterBattleStage",stage);
        ActiveBattle=s.Battle;
        Pose(s,true,0,0);Pose(s,false,2,0); Set(s,"Morale",0);
        var r=new Result{School=new[]{"ordinary","thunder","blood"}[school],Stage=stage.ToString(),Enemy=e.Id,Policy=policy==0?"damage":"survival",Seed=seed,
            PlayerMax=Health(s,stage),EnemyMax=EnemyHealth(s,stage),EnemyGuard=stage==NavalBattleStage.Boarding?s.Battle.Profile.BoardingGuard:s.Battle.Profile.Guard,
            EnemyAttack=stage==NavalBattleStage.Boarding?s.Battle.Profile.MeleeDamage:s.Battle.Profile.CannonDamage};
        var trace=new List<string>();
        for(int turn=1;turn<=3&&s.Phase==CoreDemoPhase.Battle&&s.Battle.Stage==stage;turn++)
        {
            r.Turns++;trace.Add("T"+turn);
            for(int action=0;action<30&&s.Phase==CoreDemoPhase.Battle;action++)
            {
                int slot=Best(s,stage,policy);if(slot<0)break;
                CoreBattleCard card=s.GetBattleCard(s.Battle.Hand[slot]);int h=Health(s,stage),eh=EnemyHealth(s,stage), hand=s.Battle.Hand.Count;
                int powder=s.Cargo.Powder,balls=s.Cargo.Cannonballs;
                string key=card.BoardingSpec?.Key;
                int paid=stage==NavalBattleStage.Boarding?(key=="blood_02"?4:key=="blood_01"||key=="blood_03"||key=="blood_04"||key=="blood_05"?3:0):s.GetBattleCardHealthCost(card);
                if(!s.PlayCard(slot))throw new Exception("Selected unplayable "+card.Id);
                r.Plays++;r.PaidLife+=paid;r.Damage+=Math.Max(0,eh-EnemyHealth(s,stage));r.Healing+=Math.Max(0,Health(s,stage)-(h-paid));
                r.Drawn+=Math.Max(0,ActiveBattle.Hand.Count-hand+1);r.Powder+=powder-s.Cargo.Powder;r.Balls+=balls-s.Cargo.Cannonballs;
                r.MaxChain=Math.Max(r.MaxChain,ActiveBattle.Schools.ThunderChain);trace.Add(card.Id.Replace("card_boarding_","").Replace("card_school_",""));
            }
            if(s.Phase!=CoreDemoPhase.Battle)break;
            int before=Health(s,stage);s.EndBattleTurn();r.EnemyTurnDamage+=Math.Max(0,before-Health(s,stage));
        }
        r.EndHealth=Health(s,stage);r.Outcome=r.EndHealth<=0?"loss":EnemyHealth(s,stage)<=0?"win":ActiveBattle.Stage!=stage?"transition":"ongoing";
        r.Trace=string.Join(";",trace);return r;
    }
    static int Best(CoreVoyageSession s,NavalBattleStage stage,int policy)
    {
        int best=-1;double value=0;int incoming=stage==NavalBattleStage.Boarding?s.EnemyBoardingAttackDamage:s.Battle.Profile.CannonDamage;
        for(int slot=0;slot<s.Battle.Hand.Count;slot++)
        {
            if(!s.CanPlayCard(slot))continue;int index=s.Battle.Hand[slot];CoreBattleCard c=s.GetBattleCard(index);
            string k=c.BoardingSpec?.Key;int cost=stage==NavalBattleStage.Boarding?s.GetBoardingHandEnergyCost(slot):s.GetBattleCardEnergyCost(index);
            double score=0;int gain=CoreBattleUpgradeBalance.EnergyRecovery(c);
            if(k=="thunder_09"&&s.Battle.Schools.ThunderChain<2)gain=0;
            if(gain>cost)score=100+gain-cost;
            else if(gain>0)continue;
            int damage=s.GetBattleCardDamage(c,stage);
            var effect=s.GetBattleCardEffects(c,stage);
            int health=Health(s,stage),maximum=stage==NavalBattleStage.Boarding?s.Battle.PlayerMaxBoardingHealth:s.MaxHull;
            int healing=effect.ActualRepair;
            if(k=="blood_06"||c.Id=="card_school_blood_b04")healing=Math.Min(maximum-health,Math.Max(0,damage-s.Battle.EnemyBlock));
            int usefulGuard=Math.Min(Math.Max(0,incoming-s.Battle.PlayerBlock),effect.Block);
            score=Math.Max(score,(damage+healing*(policy==0?.6:1.5)+usefulGuard*(policy==0?.2:1.5))/(cost+.7));
            if(damage>0&&c.SchoolId=="thunder")
            {
                if(cost==0)score+=80;
                else if(k!="thunder_13"&&k!="thunder_14"&&k!="thunder_15"&&c.Id!="card_school_thunder_b06")score+=6;
            }
            if(c.Draw>0&&s.Battle.Hand.Count<=4&&s.Battle.Energy>=cost+1)score+=40;
            if(k=="thunder_07"&&s.Battle.Schools.ThunderChain>=2&&s.Battle.Hand.Count<8)score+=45;
            if(k=="thunder_07"&&s.Battle.Schools.ThunderChain<2)score=0;
            if(c.Id=="card_school_thunder_n03"&&!s.Battle.NavalExpansion.Precharge&&s.Battle.Hand.Any(i=>s.GetBattleCard(i).SchoolId=="thunder"&&s.GetBattleCard(i).Damage>0))score=85;
            if(c.Id=="card_school_thunder_n05"&&!s.Battle.NavalExpansion.GroundingUsed&&!s.Battle.NavalExpansion.GroundingReady&&s.Battle.Energy>=2)score=65;
            if(c.Id=="card_school_thunder_n04")score=Math.Max(score,(s.Battle.Schools.ThunderChain>=2?8:4)*(policy==0?.2:1.5)/(cost+.7));
            if(c.Id=="card_load_shot"&&s.Battle.Hand.Any(i=>s.GetBattleCard(i).Action==NavalBattleCardAction.Cannon)&&s.Battle.Energy>=2)score=Math.Max(score,12);
            if(c.SchoolId!="thunder"&&s.Battle.Schools.ThunderChain>0&&gain>0)score-=90;
            if(score>value){best=slot;value=score;}
        }
        return best;
    }
    static object Call(object x,string n,params object[] a)=>x.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(x,a);
    static void Set(object x,string n,object v)=>x.GetType().GetProperty(n).SetValue(x,v);
    static void Pose(CoreVoyageSession s,bool p,int x,int y)=>Call(s.Battle,"SetTacticalPose",p,new VoyagePoint(x*100,y*100),90f);
}
