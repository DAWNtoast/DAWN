using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Finite, public-information heuristics. Never inspects the draw pile order or
// modifies combat pools. Actual effects, legality, shuffles and enemy turns are Core.
public static class NavalBalanceProbe
{
    public sealed class Loadout
    {
        public string Id, ShipId, School; public string[] Components=Array.Empty<string>(); public int Tier, Rank, Hull, Armor, Firepower, Crew;
        public int HullDelta, ArmorDelta, FirepowerDelta; public float SpeedDelta, BowQuality, PlatingQuality;
    }
    public sealed class DeckDefinition
    {
        public string Name, School, Explanation; public string[] Ids;
        public DeckDefinition(string school, string explanation, params string[] ids)
        { Name=School=school; Explanation=explanation; Ids=ids; }
    }
    public static readonly DeckDefinition[] Decks = {
        new DeckDefinition("ordinary","普通炮击、装填，回能抽牌与防御修理；无场地。",
            "card_cannon_barrage","card_cannon_barrage","card_chain_shot","card_chain_shot","card_energy_order","card_energy_order","card_tactical_draw","card_brace","card_brace","card_emergency_repair","card_evasive_maneuver","card_load_shot"),
        new DeckDefinition("fire","火油弹点火，浸油增层，趁火齐射兑现；烟幕提供防御和抽牌。无场地。",
            "card_school_fire_b03","card_school_fire_b03","card_school_fire_b04","card_school_fire_b04","card_school_fire_b05","card_school_fire_n03","card_school_fire_b06","card_energy_order","card_energy_order","card_tactical_draw","card_brace","card_emergency_repair"),
        new DeckDefinition("arcane","余辉回收先产法力，聚束按X兑现；固定费用攻击、屏障与普通回能抽牌。无场地。",
            "card_school_arcane_n04","card_school_arcane_n04","card_school_arcane_b06","card_school_arcane_b06","card_school_arcane_b04","card_school_arcane_b03","card_school_arcane_b05","card_energy_order","card_energy_order","card_tactical_draw","card_brace","card_emergency_repair"),
        new DeckDefinition("curse","两种施印、保印灾兆、清印终结；幽帆防守兼抽牌。无场地。",
            "card_school_curse_b03","card_school_curse_b03","card_school_curse_b04","card_school_curse_b04","card_school_curse_b05","card_school_curse_b05","card_school_curse_n03","card_school_curse_b06","card_energy_order","card_energy_order","card_brace","card_emergency_repair"),
        new DeckDefinition("tide","普通格挡先铺，回潮壁/潮盾转蓄潮，承受实际攻击后由两种X炮兑现；回能抽牌。无场地。",
            "card_school_tide_b04","card_school_tide_b04","card_school_tide_n03","card_school_tide_b03","card_school_tide_b03","card_school_tide_n02","card_energy_order","card_energy_order","card_tactical_draw","card_brace","card_brace","card_emergency_repair"),
        new DeckDefinition("thunder","普通回能在连锁前，预充/接地后接主动攻击、跨弧和终雷；脉冲防御。无场地。",
            "card_school_thunder_n03","card_school_thunder_b03","card_school_thunder_b03","card_school_thunder_n02","card_school_thunder_b04","card_school_thunder_b06","card_school_thunder_n05","card_school_thunder_n04","card_energy_order","card_energy_order","card_brace","card_emergency_repair"),
        new DeckDefinition("blood","付血赤契、偿债吸血、绝境重炮；血铸与禁疗支持，普通回能抽牌。无场地。",
            "card_school_blood_b03","card_school_blood_b03","card_school_blood_b04","card_school_blood_b04","card_school_blood_b06","card_school_blood_n03","card_school_blood_b05","card_school_blood_n02","card_energy_order","card_energy_order","card_tactical_draw","card_brace")
    };
    public static readonly DeckDefinition[] MatureDecks = {
        new DeckDefinition("arcane","真实准备提高法力上限；折射阵自身产法力，充能聚束；实际装备五件自动加入。",
            "card_school_arcane_n01","card_school_arcane_n04","card_school_arcane_n04","card_school_arcane_b06","card_school_arcane_b06","card_school_arcane_b04","card_energy_order","card_energy_order","card_tactical_draw","card_brace"),
        new DeckDefinition("curse","真实准备施印；禁域放敌方并保印，灾兆兑现；实际装备五件自动加入。",
            "card_school_curse_n01","card_school_curse_b03","card_school_curse_b03","card_school_curse_b04","card_school_curse_b05","card_school_curse_b05","card_school_curse_n03","card_energy_order","card_energy_order","card_school_curse_b06"),
        new DeckDefinition("tide","真实准备蓄潮盾；格挡转潮势后消耗，定向水流支持部件。实际装备五件自动加入。",
            "card_school_tide_n01","card_school_tide_b04","card_school_tide_b04","card_school_tide_b03","card_school_tide_b03","card_school_tide_n02","card_school_tide_n03","card_energy_order","card_energy_order","card_brace"),
        new DeckDefinition("thunder","真实准备强化首攻；雷栅保链，合法移动产链，接地回流续航；实际装备五件自动加入。",
            "card_school_thunder_n01","card_school_thunder_b06","card_school_thunder_b06","card_school_thunder_b03","card_school_thunder_n02","card_school_thunder_n03","card_school_thunder_n05","card_energy_order","card_energy_order","card_brace")
    };
    static readonly Dictionary<string,int> CardIds = Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count)
        .ToDictionary(i=>CoreVoyageSession.CardLibrary[i].Id,i=>i);
    static readonly string[] Resources = {"burn","mana","marks","tide","chain"};
    sealed class Snap
    {
        public int HP, EH, Block, EB, Energy, Powder, Balls, Wood, Medicine, Hand,ManaLimit,PlayerTurn; public bool InThunder;
        public int[] R;
        public Snap(CoreVoyageSession s,CoreBattleState b)
        { HP=s.Hull;EH=b.EnemyHull;Block=b.PlayerBlock;EB=b.EnemyBlock;Energy=b.Energy;Powder=s.Cargo.Powder;Balls=s.Cargo.Cannonballs;Wood=s.Cargo.Wood;Medicine=s.Cargo.Medicine;Hand=b.Hand.Count;
          ManaLimit=b.Schools.ManaLimit;PlayerTurn=b.NavalFields.PlayerTurn;InThunder=CoreThunderRules.IsInThunderField(b,true);
          R=new[]{b.EnemyBurnTurns>0?b.EnemyBurnDamage:0,b.Schools.AvailableMana,b.Schools.CurseMarks,b.Schools.TidePower,b.Schools.ThunderChain}; }
    }
    sealed class Result
    {
        public int Tier,Seed,Turns,Plays,EndHP,EnemyHP,MaxHP,EnemyMax,Drawn,PaidLife,Healing,EnemyTurnLoss,Absorbed,Powder,Balls,Wood,Medicine,NetEnemyLoss,PrepPlays,StartMana,StartManaLimit,StartBlock,DeckSize;
        public string School,Policy,Outcome,Trace;
        public int[] Gain=new int[5],Spent=new int[5],Expired=new int[5],Peak=new int[5];
        public int ManaRefill;
        public string Csv()=>Row(new object[]{Tier,School,Policy,Seed,Outcome,Turns,Plays,MaxHP,EndHP,EnemyMax,EnemyHP,NetEnemyLoss,EnemyTurnLoss,Absorbed,PaidLife,Healing,Drawn,Powder,Balls,Wood,Medicine,ManaRefill,PrepPlays,StartMana,StartManaLimit,StartBlock,DeckSize}.Concat(Gain.Cast<object>()).Concat(Spent.Cast<object>()).Concat(Expired.Cast<object>()).Concat(Peak.Cast<object>()).Concat(new object[]{Trace}).ToArray());
    }
    public static IEnumerable<Loadout> Loadouts(string path)
    {
        var lines=File.ReadAllLines(path);string[] header=ParseCsv(lines[0]);
        foreach(string line in lines.Skip(1).Where(l=>l.Length>0))
        {
            string[] cells=ParseCsv(line);var d=header.Select((h,i)=>new{h,i}).ToDictionary(x=>x.h,x=>cells[x.i]);
            int I(string k)=>int.Parse(d[k],CultureInfo.InvariantCulture);float F(string k)=>float.Parse(d[k],CultureInfo.InvariantCulture);
            yield return new Loadout{Id=d["Id"],ShipId=d["ShipId"],School=d.ContainsKey("School")?d["School"]:null,Components=d.ContainsKey("ComponentIds")?d["ComponentIds"].Split(';').Where(x=>x.Length>0).ToArray():Array.Empty<string>(),Tier=I("CalibrationTier"),Rank=I("CalibrationCardRank"),Hull=I("Hull"),Armor=I("Armor"),Firepower=I("Firepower"),Crew=I("Crew"),HullDelta=I("HullDelta"),ArmorDelta=I("ArmorDelta"),FirepowerDelta=I("FirepowerDelta"),SpeedDelta=F("SpeedDelta"),BowQuality=F("BowQuality"),PlatingQuality=F("PlatingQuality")};
        }
    }
    public static CoreVoyageSession CreateSession(Loadout loadout,string school,int seed,bool startVoyage=false,bool mature=false)
    {
        var s=new CoreVoyageSession();
        s.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10)});
        int ship=Enumerable.Range(0,CoreVoyageSession.Ships.Count).First(i=>CoreVoyageSession.Ships[i].Id==loadout.ShipId);
        s.SelectShip(ship);s.SetSeed("NAVAL-BALANCE-20261024-"+seed);
        s.ConfigureCrewCapabilities(false,false,false,false,false,false);s.ConfigureDepartureCrew(loadout.Crew,0);
        s.ConfigureComponentBonuses(loadout.HullDelta,loadout.ArmorDelta,loadout.FirepowerDelta,loadout.SpeedDelta,0);
        s.ConfigureComponentContextEffect(0,0,0,0,0,1);s.ConfigureBattleEquipment(loadout.BowQuality,loadout.PlatingQuality);
        // Baseline isolates ordinary school cards. Equipped-source cards are a separate cohort.
        s.ConfigureSpecialCardSources(Array.Empty<string>(),mature?loadout.Components:Array.Empty<string>());
        DeckDefinition deck=(mature?MatureDecks:Decks).First(x=>x.Name==school);
        if(deck.Ids.Length<10||deck.Ids.Length>12||deck.Ids.GroupBy(x=>x).Any(g=>g.Count()>2))throw new Exception("Invalid authored deck "+school);
        string[] preparation={"card_school_"+school+"_b01","card_school_"+school+"_b01","card_school_"+school+"_b02","card_school_"+school+"_b02","card_energy_order","card_energy_order","card_tactical_draw","card_tactical_draw","card_brace","card_brace"};
        var upgradeIds=deck.Ids.Concat(mature?preparation:Array.Empty<string>()).Concat(mature?loadout.Components.Select(id=>CoreSpecialCardCatalog.ForComponent(id)?.Card.Id).Where(id=>id!=null):Array.Empty<string>()).Distinct();
        s.ConfigureCardUpgrades(upgradeIds.ToDictionary(id=>id,id=>loadout.Rank));
        if(!s.SetBattleDeck(NavalBattleStage.Naval,deck.Ids.Select(id=>CardIds[id]).ToArray()))throw new Exception("Core rejected deck "+school);
        if(mature&&!s.SetBattleDeck(NavalBattleStage.Preparation,preparation.Select(id=>CardIds[id]).ToArray()))throw new Exception("Core rejected preparation "+school);
        if(s.MaxHull!=loadout.Hull||s.Hull!=loadout.Hull||s.EffectiveArmor!=loadout.Armor||s.EffectiveFirepower!=loadout.Firepower)throw new Exception("Loadout totals do not match production "+loadout.Id);
        if(startVoyage){s.StartVoyage();if(s.Phase!=CoreDemoPhase.Sailing)throw new Exception("Voyage initialization failed");}
        s.Cargo.Powder=100;s.Cargo.Cannonballs=100;s.Cargo.Wood=30;s.Cargo.Medicine=30;
        return s;
    }
    public static void Pose(CoreVoyageSession s,bool player,int x,int y)=>Call(s.Battle,"SetTacticalPose",player,new VoyagePoint(x*100,y*100),90f);
    public static bool PlayChosen(CoreVoyageSession s,int slot)
    {var choice=s.GetNavalCardChoice(slot);return choice?.RequiresChoice==true?s.PlayNavalCard(slot,ChooseOptions(s,slot)):s.PlayCard(slot);}
    public static CoreNavalCardPlayOptions ChooseOptions(CoreVoyageSession s,int slot)
    {
        var choice=s.GetNavalCardChoice(slot);if(choice==null)return null;
        if(!choice.IsFieldPlacement)return null;
        var b=s.Battle;int px=(int)Math.Round(b.PlayerPositionMeters.X/100),py=(int)Math.Round(b.PlayerPositionMeters.Y/100);
        int ex=(int)Math.Round(b.EnemyPositionMeters.X/100),ey=(int)Math.Round(b.EnemyPositionMeters.Y/100),size=choice.ShapeWidth;
        CoreNavalCardPlayOptions best=null;double bestValue=double.MinValue;
        for(int x=px-4;x<=px+4;x++)for(int y=py-4;y<=py+4;y++)
        {
            var o=new CoreNavalCardPlayOptions{HasAnchor=true,AnchorX=x,AnchorY=y,DirectionY=1};
            if(s.GetNavalChoiceUnavailableReason(slot,o).Length>0)continue;
            bool self=px>=x&&px<x+size&&py>=y&&py<y+size,enemy=ex>=x&&ex<x+size&&ey>=y&&ey<y+size;
            double value=choice.FieldKind==CoreNavalFieldKind.Curse?(enemy?100:0)-(self?150:0):choice.FieldKind==CoreNavalFieldKind.Arcane?(self?100:0)+(enemy?15:0):(self?100:0)+(enemy?20:0);
            // Keep room to move north under tide, and around the ship inside thunder.
            if(choice.FieldKind==CoreNavalFieldKind.Tide)value-=Math.Abs(x-px)*.1+Math.Abs(y-py);
            else value-=Math.Abs(x-(px-1))*.1+Math.Abs(y-(py-1))*.1;
            if(value>bestValue){bestValue=value;best=o;}
        }
        return bestValue>0?best:null;
    }
    public static int QuoteDamage(CoreVoyageSession s,CoreBattleCard c)
    {
        int value=s.GetBattleCardDamage(c,NavalBattleStage.Naval);var b=s.Battle;
        switch(c.Id)
        {
            case "card_school_fire_b04": if(b.EnemyBurnTurns>0)value+=c.SchoolEffect.Power;break;
            case "card_school_curse_b05":value+=b.Schools.CurseMarks*Math.Max(1,c.SchoolEffect.Power);break;
            case "card_school_curse_n03":value+=6*b.Schools.CurseMarks;break;
            case "card_school_tide_n02":value+=TideStrikeCoefficient(c.UpgradeLevel)*b.Schools.TidePower;break;
            case "card_school_thunder_n02":if(b.Schools.ThunderChain>=2)value+=12;break;
            case "card_school_blood_n03":if(s.GetBattleCardHealthCost(c)>0)value+=12;break;
        }
        return Math.Max(0,value);
    }
    static int TideStrikeCoefficient(int level)
    {
        // Same probe compiles against the immutable before snapshot (no helper)
        // and the final source. Runtime remains the authority for each version.
        var type=typeof(CoreVoyageSession).Assembly.GetType("ShatteredSail.Core.Gameplay.CoreNavalExpansionBalance");
        var method=type?.GetMethod("TideStrikeDamageCoefficient",BindingFlags.Public|BindingFlags.Static);
        return method==null?2:(int)method.Invoke(null,new object[]{level});
    }
    public static int ChooseCard(CoreVoyageSession s,bool survival)
    {
        int best=-1;double value=.01;
        for(int slot=0;slot<s.Battle.Hand.Count;slot++)
        {
            if(!s.CanPlayCard(slot))continue;
            if(s.GetNavalCardChoice(slot)?.RequiresChoice==true&&ChooseOptions(s,slot)==null)continue;
            double score=Score(s,slot,survival);
            if(score>value){best=slot;value=score;}
        }
        return best;
    }
    static double Score(CoreVoyageSession s,int slot,bool survival)
    {
        var b=s.Battle;int index=b.Hand[slot];CoreBattleCard c=s.GetBattleCard(index);
        int cost=s.GetBattleCardEnergyCost(index),energy=b.Energy,chain=b.Schools.ThunderChain,marks=b.Schools.CurseMarks;
        int incoming=CoreBattleIntentPresentation.Build(s).Where(x=>x.IsAttack).Sum(x=>x.Amount??0);
        int remainingIncoming=Math.Max(0,incoming-b.PlayerBlock),lifeCost=s.GetBattleCardHealthCost(c);
        int raw=QuoteDamage(s,c),damage=Math.Max(0,raw-b.EnemyBlock);
        double defWeight=survival?1.55:.35,healWeight=survival?1.6:.65;
        var e=s.GetBattleCardEffects(c,NavalBattleStage.Naval);
        bool inThunder=CoreThunderRules.IsInThunderField(b,true);
        bool protectedThunder=(b.NavalFields.Player.FieldImmunityMask&(1<<(int)CoreNavalFieldKind.Thunder))!=0&&b.NavalFields.Player.FieldImmunityExpires>b.NavalFields.PlayerTurn;
        if(inThunder&&!protectedThunder&&c.SchoolId=="thunder"&&c.Damage>0&&chain+(b.NavalExpansion.Precharge?3:1)>=50)return 0;
        int usefulGuard=Math.Min(remainingIncoming,e.Block),healing=e.ActualRepair;
        if(c.SchoolEffect?.Mechanic==CoreSchoolMechanic.BloodDrain)healing=Math.Min(s.MaxHull-s.Hull,Math.Min(b.EnemyHull,damage));
        bool lethal=damage>=b.EnemyHull&&raw>0;
        if(marks>0&&raw>0&&(long)marks*100>(long)Math.Max(0,b.EnemyHull-damage)*b.Schools.EnemyCurseExecutionThresholdPercent)lethal=true;
        if(lethal)return 100000+damage-cost;
        double score=(raw+usefulGuard*defWeight+healing*healWeight-lifeCost*(survival?1.5:.6))/(cost+.8);
        bool danger=s.Hull<=remainingIncoming+lifeCost;
        if(danger&&(usefulGuard>0||healing>0))score+=150+(usefulGuard+healing)*5;
        int gain=s.GetBattleCardEnergyRecovery(index);
        bool affordableThunder=b.Hand.Select((i,h)=>new{i,h}).Any(x=>s.CanPlayCard(x.h)&&s.GetBattleCard(x.i).SchoolId=="thunder"&&s.GetBattleCard(x.i).Damage>0);
        if(gain>cost)
        {
            if(chain>0&&affordableThunder&&!inThunder)return 0; // ordinary energy would erase a usable chain.
            return 1000+gain-cost;
        }
        if(gain>0)return 0;
        if(c.Draw>0&&b.Hand.Count<8)
            score+=Math.Min(c.Draw,8-b.Hand.Count+1)*(energy>=cost+1?13:3);
        if(c.Id=="card_tactical_draw"&&b.Hand.Count>=6)score*=.3;
        if(c.Id=="card_brace"&&cost==0&&remainingIncoming>0)score+=60;
        if(c.Id=="card_load_shot")
        {
            bool follow=b.Hand.Any(i=>s.GetBattleCard(i).Action==NavalBattleCardAction.Cannon&&s.GetBattleCardEnergyCost(i)<=energy-cost);
            score+=follow?s.GetBattleLoadedShotIncrease(c)/(cost+.8):0;
        }
        if(c.Id=="card_school_arcane_b06")
        {
            bool burst=b.Hand.Any(i=>s.GetBattleCard(i).Id=="card_school_arcane_n04"&&s.GetBattleCardEnergyCost(i)<=energy-cost);
            score+=burst?90:energy>cost?12:0;
        }
        if(c.Id=="card_school_arcane_n04")
        {
            bool charge=b.Hand.Any(i=>s.GetBattleCard(i).Id=="card_school_arcane_b06"&&s.GetBattleCardEnergyCost(i)+cost<=energy);
            if(charge)score*=.2; // actual hand can legally charge before spending all mana.
        }
        if(c.SchoolEffect?.Mechanic==CoreSchoolMechanic.CurseMark)
        {
            int added=c.SchoolEffect.Power;
            if((long)(marks+added)*100>(long)b.EnemyHull*b.Schools.EnemyCurseExecutionThresholdPercent)return 100000;
            score+=(added*6.0)/(cost+.8);
            bool detonate=b.Hand.Any(i=>s.GetBattleCard(i).Id=="card_school_curse_b05"&&s.GetBattleCardEnergyCost(i)<=energy-cost);
            if(detonate)score+=15;
        }
        if(c.Id=="card_school_curse_n03")
        {
            // Do not spend a small permanent mark stock for the first available burst.
            if(marks<12||damage<b.EnemyMaxHull*.30)score*=.12;
        }
        if(c.Id=="card_school_curse_b05"&&marks==0)return 0;
        if(c.SchoolEffect?.Mechanic==CoreSchoolMechanic.FireIgnite||c.SchoolEffect?.Mechanic==CoreSchoolMechanic.FireIgniteOrExtend)
        {
            int added=c.SchoolEffect.Power*(b.NavalExpansion.OilPlayerEndsRemaining>0?2:1);
            score+=added*1.7/(cost+.8);
            if(b.EnemyBurnTurns==0&&b.Hand.Any(i=>s.GetBattleCard(i).Id=="card_school_fire_b04"&&s.GetBattleCardEnergyCost(i)<=energy-cost))score+=30;
        }
        if(c.Id=="card_school_fire_n03")
        {
            bool fire=b.Hand.Any(i=>{var other=s.GetBattleCard(i);return (other.SchoolEffect?.Mechanic==CoreSchoolMechanic.FireIgnite||other.SchoolEffect?.Mechanic==CoreSchoolMechanic.FireIgniteOrExtend)&&s.GetBattleCardEnergyCost(i)<=energy-cost;});
            score=fire&&b.NavalExpansion.OilPlayerEndsRemaining==0?55:0;
        }
        if(c.SchoolEffect?.Mechanic==CoreSchoolMechanic.TideWard||c.Id=="card_school_tide_n03")
        {
            int willAbsorb=Math.Min(incoming,b.PlayerBlock+e.Block);
            int newTide=Math.Max(0,willAbsorb-Math.Min(incoming,b.Schools.TidePaidBlock));
            score+=newTide*(survival?1.3:.9)/(cost+.8);
        }
        if(c.Id=="card_school_thunder_n03")
        {
            bool follow=b.Hand.Any(i=>s.GetBattleCard(i).SchoolId=="thunder"&&s.GetBattleCard(i).Damage>0&&s.GetBattleCardEnergyCost(i)<=energy-cost);
            return follow&&!b.NavalExpansion.Precharge?100:0;
        }
        if(c.Id=="card_school_thunder_n05")
        {
            bool follow=b.Hand.Any(i=>s.GetBattleCard(i).SchoolId=="thunder"&&s.GetBattleCard(i).Damage>0&&s.GetBattleCardEnergyCost(i)<=energy-cost);
            if(!b.NavalExpansion.GroundingUsed&&!b.NavalExpansion.GroundingReady&&follow)score+=80;
        }
        if(c.Id=="card_school_thunder_n04")
        {
            int reduction=Math.Min(remainingIncoming,chain>=2?8:4);
            score+=reduction*defWeight/(cost+.8);
            if(danger&&reduction>0)score+=150+reduction*5;
        }
        if(c.SchoolId=="thunder"&&c.Damage>0&&c.Id!="card_school_thunder_b06")
        {
            score+=8;
            if(b.NavalExpansion.Precharge)score+=20;
            bool finisher=b.Hand.Any(i=>s.GetBattleCard(i).Id=="card_school_thunder_b06"&&s.GetBattleCardEnergyCost(i)<=energy-cost);
            if(finisher)score+=20;
        }
        if(c.SchoolId!="thunder"&&chain>0&&affordableThunder&&!inThunder)score-=90;
        if(c.Id=="card_school_blood_n02")
            score=b.NavalExpansion.EnemyHealingBanTurns>0?0:Math.Min(b.Profile.TrainingTarget?.Repair??0,b.EnemyMaxHull-b.EnemyHull)*2-lifeCost*(survival?1.5:.5);
        var choice=s.GetNavalCardChoice(slot);
        if(choice?.IsFieldPlacement==true)
        {
            var target=choice.FieldKind==CoreNavalFieldKind.Curse?b.EnemyPositionMeters:b.PlayerPositionMeters;
            var existing=s.NavalFieldAt(target);
            score=existing?.Kind==choice.FieldKind&&s.GetNavalFieldRemainingTurns(existing)>1?0:85;
        }
        if(CoreComponentFieldCards.IsFieldCard(c))
        {
            var kind=CoreComponentFieldCards.Kind(c);var effect=c.SchoolEffect;
            var target=kind==CoreNavalFieldKind.Curse||kind==CoreNavalFieldKind.Thunder?b.EnemyPositionMeters:b.PlayerPositionMeters;
            var field=s.NavalFieldAt(target);bool onField=field?.Kind==kind&&field.PlayerOwned;
            if(effect.Mechanic==CoreSchoolMechanic.ComponentFieldExtend&&b.NavalFields.Cells.Any(f=>f.Kind==kind&&f.PlayerOwned))
                score+=onField?(s.GetNavalFieldRemainingTurns(field)<=4?65:20):12;
            if(effect.Mechanic==CoreSchoolMechanic.ComponentFieldPulse&&onField)
                score+=kind==CoreNavalFieldKind.Arcane?55:kind==CoreNavalFieldKind.Curse?effect.Power*6:kind==CoreNavalFieldKind.Thunder?effect.Power*8:effect.Power*1.2;
            if(kind==CoreNavalFieldKind.Thunder&&effect.Mechanic==CoreSchoolMechanic.ComponentFieldPulse&&inThunder&&!protectedThunder&&chain+effect.Power>=50)return 0;
            if(kind==CoreNavalFieldKind.Thunder&&effect.Mechanic==CoreSchoolMechanic.ComponentFieldShelter&&inThunder&&chain>=40&&!protectedThunder)score+=200;
            if(kind==CoreNavalFieldKind.Arcane&&effect.Mechanic==CoreSchoolMechanic.ComponentFieldShelter&&onField&&b.Hand.Any(i=>s.GetBattleCard(i).Id=="card_school_arcane_n04"&&s.GetBattleCardEnergyCost(i)<=energy-cost))score+=55;
            if(effect.Mechanic==CoreSchoolMechanic.ComponentFieldExpand||effect.Mechanic==CoreSchoolMechanic.ComponentFieldMastery)
            {
                // Expanding a curse onto our own ship is not a useful setup.
                int px=(int)Math.Round(b.PlayerPositionMeters.X/100),py=(int)Math.Round(b.PlayerPositionMeters.Y/100);
                if(kind==CoreNavalFieldKind.Curse&&b.NavalFields.Cells.Any(f=>f.Kind==kind&&Math.Abs(f.X-px)+Math.Abs(f.Y-py)<=1))return 0;
                if(kind==CoreNavalFieldKind.Arcane&&b.NavalFields.Cells.Any(f=>f.Kind==kind)&&s.NavalFieldAt(b.EnemyPositionMeters)?.Kind!=kind)score+=18;
            }
        }
        return score;
    }
    public static int ChoosePreparation(CoreVoyageSession s)
    {
        int best=-1;double highest=.01;
        for(int h=0;h<s.Battle.Hand.Count;h++)if(s.CanPlayCard(h))
        {
            int index=s.Battle.Hand[h];var c=s.GetBattleCard(index);int cost=s.GetBattleCardEnergyCost(index);double score=0;
            if(s.GetBattleCardEnergyRecovery(index)>cost)score=1000;
            else if(c.SchoolEffect?.Mechanic==CoreSchoolMechanic.ArcaneCapacity)score=150;
            else if(c.SchoolEffect?.Mechanic==CoreSchoolMechanic.ArcaneCharge||c.SchoolEffect?.Mechanic==CoreSchoolMechanic.ArcaneTemporary)score=100;
            else if(c.Id=="card_tactical_draw")score=s.Battle.Hand.Count<7?70:0;
            else if(c.SchoolEffect!=null)score=60+c.Draw*10;
            else if(c.Block>0)score=cost==0?90:20;
            if(score>highest){highest=score;best=h;}
        }
        return best;
    }
    public static bool TrySupportMove(CoreVoyageSession s)
    {
        var b=s.Battle;if(b==null||b.Stage!=NavalBattleStage.Naval||b.NavalActionPoints<1||b.NavalAwaitingHeadingSelection)return false;
        var current=s.NavalFieldAt(b.PlayerPositionMeters);int chain=b.Schools.ThunderChain;
        var p=b.PlayerPositionMeters;int px=(int)Math.Round(p.X/100),py=(int)Math.Round(p.Y/100);
        var enemy=b.EnemyPositionMeters;int ex=(int)Math.Round(enemy.X/100),ey=(int)Math.Round(enemy.Y/100);
        int distance=Math.Abs(px-ex)+Math.Abs(py-ey);VoyagePoint? best=null;double value=0;
        foreach(var d in new[]{new[]{0,1},new[]{0,-1},new[]{1,0},new[]{-1,0}})
        {
            int x=px+d[0],y=py+d[1],nextDistance=Math.Abs(x-ex)+Math.Abs(y-ey);if(nextDistance<1||(nextDistance>4&&nextDistance>=distance))continue;
            var next=new VoyagePoint(x*100,y*100);if(s.NavalMovementStepUnavailableReason(next).Length>0)continue;
            var f=s.GetNavalFieldAt(x,y);double score=distance>4&&nextDistance<distance?100:0;
            if(f?.Kind==CoreNavalFieldKind.Thunder&&chain<24&&chain+s.GetNavalFieldRemainingTurns(f)<40)score+=50+s.GetNavalFieldRemainingTurns(f);
            if(f?.Kind==CoreNavalFieldKind.Arcane&&current?.Kind!=CoreNavalFieldKind.Arcane)score+=40;
            if(f?.Kind==CoreNavalFieldKind.Tide&&current?.Kind!=CoreNavalFieldKind.Tide)score+=20;
            if(f?.Kind==CoreNavalFieldKind.Curse||f?.Kind==CoreNavalFieldKind.Fire)continue;
            if(score>value){value=score;best=next;}
        }
        if(!best.HasValue)return false;
        if(!s.TryAppendNavalMovementStep(best.Value,out string reason)||!s.CommitNavalMovement(out reason))throw new Exception("Support move rejected "+reason);
        if(s.Battle!=null&&s.Battle.NavalAwaitingHeadingSelection)
        {
            var headings=s.GetNavalArrivalHeadings();float heading=headings.Contains(90f)?90f:headings[0];
            if(!s.ConfirmNavalArrivalHeading(heading,out reason))throw new Exception(reason);
        }
        return true;
    }
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        string output=args[0];int seeds=int.Parse(args[1]),maxTurns=int.Parse(args[2]);bool mature=args.Length>3&&args[3]=="mature";
        Directory.CreateDirectory(Path.Combine(output,"traces"));var rows=new List<Result>();
        var deckRows=new List<string>{Row("school","explanation","slot","id","name","energy","damage","block","draw","maxUpgrade")};
        var decks=mature?MatureDecks:Decks;
        foreach(var d in decks)for(int i=0;i<d.Ids.Length;i++){var c=CoreVoyageSession.CardLibrary[CardIds[d.Ids[i]]];deckRows.Add(Row(d.Name,d.Explanation,i,c.Id,c.Name,c.Energy,c.Damage,c.Block,c.Draw,CoreBattleUpgradeBalance.MaximumLevelFor(c)));}
        File.WriteAllLines(Path.Combine(output,"decks.csv"),deckRows,Encoding.UTF8);
        foreach(var l in Loadouts(Path.Combine(output,"calibration-loadouts.csv")))
        foreach(var d in decks.Where(d=>!mature||d.School==l.School))
        {
            foreach(bool survival in new[]{false,true})for(int seed=0;seed<seeds;seed++)rows.Add(Run(l,d,survival,seed,maxTurns,output,mature));
            Console.WriteLine("tier="+l.Tier+" school="+d.Name+" samples="+(seeds*2)+" complete");
        }
        string header=Row(new[]{"tier","school","policy","seed","outcome","turns","plays","maxHP","endHP","enemyMax","enemyHP","netEnemyHullLoss","enemyTurnHPLoss","absorbed","paidLife","healing","drawnByCards","powder","balls","wood","medicine","manaRefill","preparationPlays","startMana","startManaLimit","startBlock","navalDeckSize"}.Concat(Resources.Select(x=>x+"Gained")).Concat(Resources.Select(x=>x+"Spent")).Concat(Resources.Select(x=>x+"Expired")).Concat(Resources.Select(x=>x+"Peak")).Concat(new[]{"trace"}).Cast<object>().ToArray());
        File.WriteAllLines(Path.Combine(output,"samples.csv"),new[]{header}.Concat(rows.Select(x=>x.Csv())),Encoding.UTF8);
        var summary=new List<string>{Row("tier","school","policy","n","kills","deaths","timeouts","meanTurnsOnKills","meanEndHP","meanEnemyHP","meanNetEnemyHullLoss","meanAbsorbed","meanPaidLife","meanHealing","meanPlays","meanPowder","meanBalls","meanWood","burnPeak","manaPeak","marksPeak","tidePeak","chainPeak")};
        foreach(var g in rows.GroupBy(x=>new{x.Tier,x.School,x.Policy}))
        {var kills=g.Where(x=>x.Outcome=="kill").ToArray();summary.Add(Row(g.Key.Tier,g.Key.School,g.Key.Policy,g.Count(),kills.Length,g.Count(x=>x.Outcome=="death"),g.Count(x=>x.Outcome=="timeout"),kills.Length>0?F(kills.Average(x=>x.Turns)):"",F(g.Average(x=>x.EndHP)),F(g.Average(x=>x.EnemyHP)),F(g.Average(x=>x.NetEnemyLoss)),F(g.Average(x=>x.Absorbed)),F(g.Average(x=>x.PaidLife)),F(g.Average(x=>x.Healing)),F(g.Average(x=>x.Plays)),F(g.Average(x=>x.Powder)),F(g.Average(x=>x.Balls)),F(g.Average(x=>x.Wood)),g.Max(x=>x.Peak[0]),g.Max(x=>x.Peak[1]),g.Max(x=>x.Peak[2]),g.Max(x=>x.Peak[3]),g.Max(x=>x.Peak[4])));}
        File.WriteAllLines(Path.Combine(output,"summary.csv"),summary,Encoding.UTF8);
        File.WriteAllText(Path.Combine(output,"method.txt"),"Five fixed production training targets; calibrated legal slot deltas and ship bases. Level 10 isolates all school access; it is not actual progression. Rank requests 0/1/1/2/3 clamp per card. 12 cards, <=2 copies, no special-source cards or fields in this initial cohort. Real Core shuffle, five-card draws, eight-card hand cap, same-name surcharge, three energy. Preparation ends without playing cards. Two-grid initial range, no movement needed by stationary targets. No bonus HP/block/mana. Supplies: powder/balls100, wood/medicine30, intentionally outside voyage cargo economy; consumption recorded. Two finite visible-information heuristics; no draw-pile inspection. Max turns="+maxTurns+", seeds="+seeds+". Counts are finite-policy outcomes, NOT school win rates. Resource gains/spending are observed action deltas; turn-start mana refill and expiration separate. Chain consumption accounts for the attack's subsequent +1. End-turn HP loss and net enemy hull loss include normal reactions/repair. Pure base-school groups diagnose baseline; equipment identity/field groups require separate evaluation before universal buffs.\n",Encoding.UTF8);
        if(mature)File.WriteAllText(Path.Combine(output,"method.txt"),"Formed cohort, T3 rare calibration gear, T4 school epic set, T5 school legendary set; actual slot deltas, identity-granted cards, per-card upgrade cap. Ten authored naval base cards (<=2 each) plus automatic actual equipped-source bonus cards. Ten preparation base cards: school b01/b02, energy_order/tactical_draw/brace each twice; real five-card preparation shuffle and payment, never injected mana/block. Starting mana/limit/block are measured. One or more field placements from drawn cards; curse targets enemy and avoids self; arcane targets self for mana; thunder/tide prefer both ships. Real cardinal movement, AP and arrival heading; thunder stepping stops below 24 chain and attacks near 50 require immunity. No hidden pile information. Same five fixed targets, level10 mechanism isolation, supplied100 powder/balls,30 wood/medicine. Max12turns unless specified. Counts are finite-policy outcomes, not general win rates. Arcane start refill replaces all unspent mana; field gains counted separately. Before and after use the identical tool source and seed set.\n",Encoding.UTF8);
        Console.WriteLine("Completed "+rows.Count+" finite-policy samples.");return 0;
    }
    static Result Run(Loadout l,DeckDefinition d,bool survival,int seed,int maxTurns,string output,bool mature)
    {
        var s=CreateSession(l,d.Name,seed,false,mature);if(!s.StartTrainingBattle(CoreTrainingTargets.All[l.Tier-1]))throw new Exception("Training start failed");
        var b=s.Battle;Pose(s,true,0,0);Pose(s,false,2,0);
        var r=new Result{Tier=l.Tier,School=d.Name,Policy=survival?"survival":"damage",Seed=seed,MaxHP=s.MaxHull,EnemyMax=b.EnemyMaxHull};
        var trace=new List<string>{Row("turn","event","card","score","hp","enemyHP","block","enemyBlock","energy","burn","mana","marks","tide","chain","hand","enemyIntent","lastAction","playerX","playerY","enemyX","enemyY","manaLimit","playerField","enemyField")};
        if(mature)for(int action=0;action<30;action++)
        {
            int slot=ChoosePreparation(s);if(slot<0)break;var card=s.GetBattleCard(b.Hand[slot]);var before=new Snap(s,b);
            if(!s.PlayCard(slot))throw new Exception("Preparation play rejected "+card.Id);
            var after=new Snap(s,b);Account(r,before,after,card,false);r.PrepPlays++;Trace(trace,s,b,0,"preparation",card.Id,0);
            if(action==29)throw new Exception("Preparation loop");
        }
        if(!s.EndBattleTurn()||s.Battle.Stage!=NavalBattleStage.Naval)throw new Exception("Preparation failed");
        if(!mature&&(s.Hull!=l.Hull||b.PlayerBlock!=0||b.Schools.AvailableMana!=3||b.Energy!=3))throw new Exception("Unrequested starting benefit");
        r.StartMana=b.Schools.AvailableMana;r.StartManaLimit=b.Schools.ManaLimit;r.StartBlock=b.PlayerBlock;r.DeckSize=s.GetBattleDeck(NavalBattleStage.Naval).Count+s.GetBonusBattleCards(NavalBattleStage.Naval).Count;
        r.Peak=new Snap(s,b).R;
        Trace(trace,s,b,0,"initial","",0);
        for(int turn=1;turn<=maxTurns&&Active(s);turn++)
        {
            r.Turns=turn;Trace(trace,s,b,turn,"drawn_hand","",0);
            for(int action=0;action<40&&Active(s);action++)
            {
                if(mature){var beforeMove=new Snap(s,b);if(TrySupportMove(s)){Account(r,beforeMove,new Snap(s,b),null,false);Trace(trace,s,b,turn,"support_move","",0);if(!Active(s))break;}}
                int slot=ChooseCard(s,survival);if(slot<0)break;
                CoreBattleCard card=s.GetBattleCard(b.Hand[slot]);double score=Score(s,slot,survival);
                var before=new Snap(s,b);int paid=s.GetBattleCardHealthCost(card);
                if(!PlayChosen(s,slot))throw new Exception("Selected rejected card "+card.Id+": "+s.CardUnavailableReason(slot));
                var after=new Snap(s,b);r.Plays++;r.PaidLife+=paid;r.Healing+=Math.Max(0,after.HP-before.HP+paid);
                r.Drawn+=Math.Max(0,after.Hand-before.Hand+1);r.NetEnemyLoss+=before.EH-after.EH;
                Account(r,before,after,card,false);Trace(trace,s,b,turn,"play",card.Id,score);
                if(action==39)throw new Exception("Unexpected action loop");
            }
            if(!Active(s))break;
            var preEnd=new Snap(s,b);if(!s.EndBattleTurn())throw new Exception("End turn rejected");
            var postEnd=new Snap(s,b);r.EnemyTurnLoss+=Math.Max(0,preEnd.HP-postEnd.HP);r.NetEnemyLoss+=preEnd.EH-postEnd.EH;
            r.Absorbed+=Math.Max(0,b.LastEnemyGuardAbsorbed);Account(r,preEnd,postEnd,null,true);Trace(trace,s,b,turn,"end_turn","",0);
        }
        r.EndHP=s.Hull;r.EnemyHP=b.EnemyHull;r.Outcome=s.TrainingOutcome==CoreTrainingBattleOutcome.Victory||b.EnemyHull<=0?"kill":s.Hull<=0||s.TrainingOutcome==CoreTrainingBattleOutcome.Defeat?"death":"timeout";
        r.Trace="traces/tier"+l.Tier+"-"+d.Name+"-"+r.Policy+"-seed"+seed.ToString("D3")+".csv";
        File.WriteAllLines(Path.Combine(output,r.Trace),trace,Encoding.UTF8);return r;
    }
    static void Account(Result r,Snap before,Snap after,CoreBattleCard card,bool turnEnd)
    {
        r.Powder+=before.Powder-after.Powder;r.Balls+=before.Balls-after.Balls;r.Wood+=before.Wood-after.Wood;r.Medicine+=before.Medicine-after.Medicine;
        for(int i=0;i<5;i++)
        {
            int delta=after.R[i]-before.R[i];r.Peak[i]=Math.Max(r.Peak[i],Math.Max(before.R[i],after.R[i]));
            if(turnEnd)
            { if(i==1&&after.PlayerTurn>before.PlayerTurn){r.Expired[i]+=before.R[i];r.ManaRefill+=Math.Min(after.R[i],after.ManaLimit);r.Gain[i]+=Math.Max(0,after.R[i]-after.ManaLimit);}else if(delta>0)r.Gain[i]+=delta;else r.Expired[i]-=delta;continue; }
            if(i==4)
            {
                int spent=card?.SchoolEffect?.Mechanic==CoreSchoolMechanic.ThunderFinish&&!before.InThunder?before.R[i]:0;
                r.Spent[i]+=spent;
                if(card?.SchoolId!="thunder"&&delta<0)r.Expired[i]-=delta;else r.Gain[i]+=Math.Max(0,delta+spent);
            }
            else if(delta>=0)r.Gain[i]+=delta;else r.Spent[i]-=delta;
        }
    }
    static bool Active(CoreVoyageSession s)=>s.Phase==CoreDemoPhase.Battle&&s.Battle!=null&&s.Battle.Stage==NavalBattleStage.Naval&&s.Hull>0&&s.Battle.EnemyHull>0;
    static void Trace(List<string> rows,CoreVoyageSession s,CoreBattleState b,int turn,string ev,string card,double score)
    {
        var x=new Snap(s,b);string hand=string.Join(";",b.Hand.Select(i=>s.GetBattleCard(i,NavalBattleStage.Naval).Id));
        var pf=s.NavalFieldAt(b.PlayerPositionMeters);var ef=s.NavalFieldAt(b.EnemyPositionMeters);
        rows.Add(Row(new object[]{turn,ev,card,F(score),x.HP,x.EH,x.Block,x.EB,x.Energy}.Concat(x.R.Cast<object>()).Concat(new object[]{hand,Active(s)?s.GetEnemyIntent():"finished",b.LastAction,b.PlayerPositionMeters.X,b.PlayerPositionMeters.Y,b.EnemyPositionMeters.X,b.EnemyPositionMeters.Y,b.Schools.ManaLimit,pf==null?"":pf.Kind+":"+s.GetNavalFieldRemainingTurns(pf),ef==null?"":ef.Kind+":"+s.GetNavalFieldRemainingTurns(ef)}).ToArray()));
    }
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
    static string F(double value)=>value.ToString("0.00",CultureInfo.InvariantCulture);
    static string Row(params object[] cells)=>string.Join(",",cells.Select(x=>"\""+Convert.ToString(x,CultureInfo.InvariantCulture).Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\""));
    static string[] ParseCsv(string line)
    {
        var cells=new List<string>();var cell=new StringBuilder();bool quoted=false;
        for(int i=0;i<line.Length;i++) {char c=line[i];if(c=='\"'){if(quoted&&i+1<line.Length&&line[i+1]=='\"'){cell.Append(c);i++;}else quoted=!quoted;}else if(c==','&&!quoted){cells.Add(cell.ToString().TrimStart('\uFEFF'));cell.Clear();}else cell.Append(c);}
        cells.Add(cell.ToString());return cells.ToArray();
    }
}
