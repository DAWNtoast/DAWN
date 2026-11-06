using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Controlled arena-entry experiment. Field cells and adjacency are fixture inputs;
// inheritance, cards, draw RNG, payments, enemy scripts and turn effects are real Core.
public static class BoardingProbe
{
    static readonly Dictionary<string,int> Ids=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).ToDictionary(i=>CoreVoyageSession.CardLibrary[i].Id,i=>i);
    static readonly MethodInfo StartBattle=typeof(CoreVoyageSession).GetMethod("StartBattle",BindingFlags.Instance|BindingFlags.NonPublic);
    static readonly MethodInfo Pose=typeof(CoreBattleState).GetMethod("SetTacticalPose",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
    sealed class Scenario { public string Id;public int Tier;public Scenario(string id,int tier){Id=id;Tier=tier;} }
    static readonly Scenario[] Bosses={
        new Scenario("boss_route_shattered_sail_reef",1),new Scenario("boss_route_shattered_sail_reef_tide",1),
        new Scenario("legendary_pirate_iron_oath",2),new Scenario("legendary_pirate_red_widow",2),
        new Scenario("boss_route_golden_tide_trade",3),new Scenario("boss_route_golden_tide_trade_thunder",3),
        new Scenario("legendary_pirate_thunder_crown",3),new Scenario("legendary_pirate_ember_ark",3),
        new Scenario("boss_route_moon_tide_ruins",4),new Scenario("boss_route_moon_tide_ruins_arcane",4),
        new Scenario(CoreBattleBalance.TreasureBossId,4),new Scenario("legendary_pirate_tide_requiem",5),new Scenario("legendary_pirate_last_horizon",5),
        new Scenario("event_reef_coral_ambush",1),new Scenario("gold_customs",2),new Scenario("gold_customs",3),new Scenario("moon_ghost",4),new Scenario("moon_ghost",5)};
    sealed class Row {
        public string Boss,School,Policy,Field,Owner,Outcome,Inherited,Trace; public int Tier,Rank,Duration,Seed,Turns,Plays,AttackPlays,Energy,HP,MaxHP,EnemyHP,EnemyMax,MinHP,CardWindowDamage,TurnWindowDamage,EnemyBurnHP,PlayerBurnHP,EnemyAttackHP,Absorbed,Medicine,Powder,MaxMana,MaxChain,MaxTide,MaxMarks,MaxEnemyBurn,MaxPlayerBurn;
        public bool Win=>Outcome=="win";
        public string Csv()=>CsvLine(Boss,Tier,Rank,School,Policy,Field,Owner,Duration,Seed,Outcome,Turns,Plays,AttackPlays,Energy,MaxHP,HP,MinHP,EnemyMax,EnemyHP,CardWindowDamage,TurnWindowDamage,EnemyBurnHP,PlayerBurnHP,EnemyAttackHP,Absorbed,Medicine,Powder,MaxMana,MaxChain,MaxTide,MaxMarks,MaxEnemyBurn,MaxPlayerBurn,Inherited,Trace);
    }
    static Dictionary<string,string[]> Decks;
    static int MaxTurns,TraceSeeds;
    public static int Main(string[] a)
    {
        string output=a[0];int seeds=int.Parse(a[1]);MaxTurns=int.Parse(a[2]);TraceSeeds=int.Parse(a[11]);
        string[] fields=a[3].Split(','),owners=a[5].Split(','),policies=a[9].Split(','); int[] durations=a[4].Split(',').Select(int.Parse).ToArray();
        bool fieldOnly=a[10]=="1";Decks=ReadDecks(Path.Combine(output,"decks.csv"));
        foreach(var d in Decks){if(d.Value.Length!=14||d.Value.GroupBy(x=>x).Any(g=>g.Count()>2))throw new Exception("Expected legal 14-card deck: "+d.Key);foreach(string id in d.Value)if(!Ids.ContainsKey("card_boarding_"+id))throw new Exception("Missing card "+id);}
        var loadouts=NavalBalanceProbe.Loadouts(Path.Combine(output,"calibration-loadouts.csv")).ToArray();var rows=new List<Row>();
        using(var runs=Writer(output,"runs.csv"))using(var entries=Writer(output,"entries.csv"))using(var cards=Writer(output,"card-values.csv"))
        {
            runs.WriteLine("boss,tier,rank,school,policy,field,owner,duration,seed,outcome,turns,plays,attackPlays,energySpent,playerMaxHp,playerFinalHp,playerMinimumHp,enemyMaxHp,enemyFinalHp,cardWindowEnemyHpLoss,turnWindowEnemyHpLoss,enemyBurnActualHpLoss,playerBurnActualHpLoss,enemyAttackActualHpLoss,guardAbsorbed,medicineSpent,powderSpent,maxMana,maxChain,maxTide,maxMarks,maxEnemyBurn,maxPlayerBurn,inherited,trace");
            entries.WriteLine("boss,tier,rank,school,field,owner,duration,seed,playerHp,enemyHp,hand,energy,playerMana,enemyMana,playerCell,enemyCell,inherited");
            cards.WriteLine("tier,requestedRank,school,cardId,name,effectiveRank,energy,damage,block,repair,draw,energyRecovery,description");
            foreach(var sc in Bosses.Where(x=>Matches(a[7],x.Id)))
            {
                var l=loadouts.Single(x=>x.Tier==sc.Tier); int[] ranks=a[8]=="stage"?new[]{l.Rank}:a[8]=="both"?new[]{0,l.Rank}.Distinct().ToArray():a[8].Split(',').Select(int.Parse).ToArray();
                foreach(int rank in ranks)foreach(var deck in Decks.Where(d=>d.Key!="field-only"&&(!d.Key.StartsWith("single-")||a[6].Split(',').Contains(d.Key))&&Matches(a[6],d.Key)).Concat(fieldOnly?Decks.Where(d=>d.Key=="field-only"):Enumerable.Empty<KeyValuePair<string,string[]>>()))
                foreach(string fieldSpec in fields)foreach(string field in new[]{fieldSpec.Split('@')[0]})foreach(int duration in field=="none"?new[]{0}:fieldSpec.Contains("@")?new[]{int.Parse(fieldSpec.Split('@')[1])}:durations)foreach(string owner in field=="none"?new[]{"none"}:owners)
                foreach(string policy in deck.Key=="field-only"||deck.Key.StartsWith("single-")?new[]{deck.Key}:policies)for(int seed=0;seed<seeds;seed++)
                {
                    Row r=Run(sc,l,rank,deck.Key,policy,field,owner,duration,seed,output,entries,cards);rows.Add(r);runs.WriteLine(r.Csv());
                }
                runs.Flush();entries.Flush();cards.Flush();Console.WriteLine(sc.Id+": "+rows.Count+" samples; "+rows.Count(x=>x.Win)+" wins");
            }
        }
        using(var f=Writer(output,"summary.csv"))
        {
            f.WriteLine("boss,tier,rank,school,policy,field,owner,duration,samples,wins,losses,timeouts,winTurns,allTurns,finalHpPercent,plays,attackPlays,enemyBurnHpLoss,cardWindowEnemyHpLoss,turnWindowEnemyHpLoss,playerBurnHpLoss,enemyAttackHpLoss,fieldOnlyWins");
            foreach(var g in rows.GroupBy(r=>new{r.Boss,r.Tier,r.Rank,r.School,r.Policy,r.Field,r.Owner,r.Duration}))
                f.WriteLine(CsvLine(g.Key.Boss,g.Key.Tier,g.Key.Rank,g.Key.School,g.Key.Policy,g.Key.Field,g.Key.Owner,g.Key.Duration,g.Count(),g.Count(r=>r.Win),g.Count(r=>r.Outcome=="loss"),g.Count(r=>r.Outcome=="timeout"),g.Any(r=>r.Win)?F(g.Where(r=>r.Win).Average(r=>r.Turns)):"",F(g.Average(r=>r.Turns)),F(g.Average(r=>100d*r.HP/r.MaxHP)),F(g.Average(r=>r.Plays)),F(g.Average(r=>r.AttackPlays)),F(g.Average(r=>r.EnemyBurnHP)),F(g.Average(r=>r.CardWindowDamage)),F(g.Average(r=>r.TurnWindowDamage)),F(g.Average(r=>r.PlayerBurnHP)),F(g.Average(r=>r.EnemyAttackHP)),g.Count(r=>r.Win&&r.Policy=="field-only")));
        }
        File.WriteAllText(Path.Combine(output,"methodology.md"),Methodology,new UTF8Encoding(false));
        File.AppendAllText(Path.Combine(output,"methodology.md"),"\n额外 single-burn/single-pot 策略使用14张合法牌组：2张燃粉投掷/余烬火罐与12张基础防御、治疗、回能、调度牌。最多成功打出1张指定燃烧牌，随后不再主动施加伤害；余烬火罐的一次使用包含即时与下回合两次施加，不冒充一次施加。其余防守评分相同。普通怪仅使用真实三条航线的3种遭遇；gold_customs分别与T2/T3玩家、moon_ghost分别与T4/T5玩家配对，是同一敌人对不同玩家成长阶段，不是五档人工普通敌人。\n",new UTF8Encoding(false));
        Console.WriteLine("DONE "+rows.Count+"; wins="+rows.Count(x=>x.Win)+"; losses="+rows.Count(x=>x.Outcome=="loss")+"; timeouts="+rows.Count(x=>x.Outcome=="timeout"));return 0;
    }
    static Row Run(Scenario sc,NavalBalanceProbe.Loadout l,int rank,string school,string policy,string field,string owner,int duration,int seed,string output,StreamWriter entries,StreamWriter cards)
    {
        var s=new CoreVoyageSession();s.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10)});
        s.SelectShip(Enumerable.Range(0,CoreVoyageSession.Ships.Count).Single(i=>CoreVoyageSession.Ships[i].Id==l.ShipId));
        int route=sc.Id.Contains("shattered")||sc.Id=="event_reef_coral_ambush"?0:sc.Id.Contains("golden")||sc.Id=="gold_customs"?1:2;s.SelectRoute(route);s.SetSeed("BOARDING-FIELD-20261024-"+seed);
        s.ConfigureDepartureCrew(l.Crew,0);s.ConfigureCrewCapabilities(false,false,false,false,false,false);
        s.ConfigureComponentBonuses(l.HullDelta,l.ArmorDelta,l.FirepowerDelta,l.SpeedDelta,0);s.ConfigureComponentContextEffect(0,0,0,0,0,1);s.ConfigureBattleEquipment(l.BowQuality,l.PlatingQuality);
        s.ConfigureSpecialCardSources(Array.Empty<string>(),Array.Empty<string>());
        var deck=Decks[school];s.ConfigureCardUpgrades(deck.Distinct().ToDictionary(id=>"card_boarding_"+id,id=>Math.Min(rank,s.GetCardMaximumUpgradeLevel("card_boarding_"+id))));
        if(!s.SetBattleDeck(NavalBattleStage.Boarding,deck.Select(id=>Ids["card_boarding_"+id]).ToArray()))throw new Exception("Illegal deck: "+school);
        s.Cargo.Food=s.Cargo.Water=20;s.Cargo.Medicine=10;s.Cargo.Powder=30;s.StartVoyage();
        if(s.Phase!=CoreDemoPhase.Sailing)throw new Exception("Departure rejected");
        bool boss=CoreBattleBalance.Classify(sc.Id)!=CoreBossKind.None;
        var encounter=CoreRouteBossCatalog.Find(sc.Id)?.CreateEncounter()??CoreLegendaryWarships.Find(sc.Id)?.CreateEncounter()??(sc.Id==CoreBattleBalance.TreasureBossId?CoreLegendaryWarships.CreateTreasureEncounter():s.Map.Nodes.Select(n=>n.Encounter).FirstOrDefault(e=>e.Id==sc.Id));
        if(encounter==null)throw new Exception("Encounter unavailable "+sc.Id);
        StartBattle.Invoke(s,new object[]{encounter,boss});if(!s.EndBattleTurn())throw new Exception("Preparation rejected");
        var b=s.Battle;Pose.Invoke(b,new object[]{true,new VoyagePoint(0,0),90f});Pose.Invoke(b,new object[]{false,new VoyagePoint(100,0),90f});
        b.NavalFields.Cells.Clear();
        string[] kinds=field=="none"?Array.Empty<string>():field.Split('+');if(kinds.Length>2)throw new Exception("At most two inherited cells/types");
        for(int i=0;i<kinds.Length;i++){
            var kind=(CoreNavalFieldKind)Enum.Parse(typeof(CoreNavalFieldKind),kinds[i],true);bool player=owner=="player"||owner=="mixed"&&i==0;
            b.NavalFields.Cells.Add(new CoreNavalFieldCell{X=i,Y=0,Kind=kind,PlayerOwned=player,InstanceId=i+1,ExpiresAtOwnerTurn=(player?b.NavalFields.CompletedEnemyTurns:b.NavalFields.CompletedPlayerTurns)+duration,DirectionX=1});
        }
        string pcell=Cell(b,0),ecell=Cell(b,1);
        if(!s.TryBoard())throw new Exception("Real boarding entry rejected "+sc.Id);
        string inherited=Fields(b);int expected=kinds.Count(k=>k!="tide");if(b.BoardingFields.Fields.Count!=expected)throw new Exception("Inheritance mismatch "+field);
        if(b.PlayerBoardingHealth!=b.PlayerMaxBoardingHealth||b.EnemyBoardingHealth!=b.EnemyMaxBoardingHealth||b.Hand.Count!=5||b.Energy!=3)throw new Exception("Unexpected boarding start");
        entries.WriteLine(CsvLine(sc.Id,l.Tier,rank,school,field,owner,duration,seed,b.PlayerBoardingHealth,b.EnemyBoardingHealth,b.Hand.Count,b.Energy,b.Schools.AvailableMana,b.BoardingEnemy.Mana,pcell,ecell,inherited));
        if(seed==0&&field=="none"&&policy!="survival")foreach(string id in deck.Distinct()){
            var c=s.GetBattleCard(Ids["card_boarding_"+id]);var e=s.GetBattleCardEffects(c,NavalBattleStage.Boarding);
            cards.WriteLine(CsvLine(l.Tier,rank,school,c.Id,c.Name,c.UpgradeLevel,c.Energy,e.Damage,e.Block,e.Repair,c.Draw,s.GetBattleCardEnergyRecovery(Ids[c.Id]),c.Description));
        }
        var r=new Row{Boss=sc.Id,Tier=l.Tier,Rank=rank,School=school,Policy=policy,Field=field,Owner=owner,Duration=duration,Seed=seed,MaxHP=b.PlayerMaxBoardingHealth,EnemyMax=b.EnemyMaxBoardingHealth,MinHP=b.PlayerBoardingHealth,Inherited=inherited};
        string traceName=sc.Id+"_t"+l.Tier+"_r"+rank+"_"+school+"_"+policy+"_"+field.Replace('+','-')+"_"+owner+"_"+duration+"_"+seed+".csv";
        string traceDir=Path.Combine(output,"traces");Directory.CreateDirectory(traceDir);r.Trace=seed<TraceSeeds?"traces/"+traceName:"";
        using(var trace=seed<TraceSeeds?Writer(traceDir,traceName):null){
            trace?.WriteLine("turn,event,card,playerHp,playerGuard,enemyHp,enemyGuard,energy,playerMana,playerChain,playerTide,enemyMarks,playerBurn,enemyBurn,enemyMana,enemyChain,fields,enemyBurnActualHpLoss,playerBurnActualHpLoss");
            bool burnCast=false;Track(r,b);Trace(trace,b,"entry","");
            for(int turn=1;turn<=MaxTurns&&s.Phase==CoreDemoPhase.Battle;turn++){
                r.Turns=turn;
                for(int action=0;action<64&&s.Phase==CoreDemoPhase.Battle;action++){
                    var preview=s.GetBoardingEnemyPreview();int best=-1;double bestScore=.01;
                    for(int slot=0;slot<b.Hand.Count;slot++){if(!s.CanPlayCard(slot))continue;double val=Score(s,slot,preview,policy,burnCast);if(val>bestScore){best=slot;bestScore=val;}}
                    if(best<0)break;
                    var c=s.GetBattleCard(b.Hand[best]);int enemyBefore=b.EnemyBoardingHealth,medicine=s.Cargo.Medicine,powder=s.Cargo.Powder;
                    r.Energy+=s.GetBoardingHandEnergyCost(best);if(c.BoardingSpec.IsAttack)r.AttackPlays++;
                    if(!s.PlayCard(best))throw new Exception("Legal card failed "+c.Id);if(c.BoardingSpec.Key=="fire_03"||c.BoardingSpec.Key=="fire_04")burnCast=true;r.Plays++;r.Medicine+=medicine-s.Cargo.Medicine;r.Powder+=powder-s.Cargo.Powder;
                    r.CardWindowDamage+=Math.Max(0,enemyBefore-b.EnemyBoardingHealth);Track(r,b);Trace(trace,b,"card",c.Id);
                    if(action==63)throw new Exception("64 card watchdog");
                }
                if(s.Phase!=CoreDemoPhase.Battle)break;
                int eh=b.EnemyBoardingHealth;Trace(trace,b,"before-end","");
                if(!s.EndBattleTurn())throw new Exception("End turn failed");
                r.TurnWindowDamage+=Math.Max(0,eh-b.EnemyBoardingHealth);r.EnemyBurnHP+=b.BoardingEnemy.LastSelfBurnHealthLost;r.PlayerBurnHP+=b.BoardingEnemy.LastBurnHealthLost;
                foreach(var hit in b.BoardingEnemy.LastActions){r.EnemyAttackHP+=hit.HealthLost;r.Absorbed+=hit.AbsorbedDamage;}
                Track(r,b);Trace(trace,b,"after-end","");
            }
        }
        r.HP=b.PlayerBoardingHealth;r.EnemyHP=b.EnemyBoardingHealth;r.Outcome=r.HP<=0?"loss":r.EnemyHP<=0?"win":"timeout";return r;
    }
    static double Score(CoreVoyageSession s,int slot,CoreBoardingEnemyPreview preview,string policy,bool burnCast)
    {
        var b=s.Battle;var c=s.GetBattleCard(b.Hand[slot]);string k=c.BoardingSpec.Key;var e=s.GetBattleCardEffects(c,NavalBattleStage.Boarding);
        int fee=s.GetBoardingHandEnergyCost(slot),raw=s.GetBoardingCardDamageQuote(c),need=preview.Actions.Sum(x=>Math.Max(0,x.HealthLost-x.PiercingDamage))+preview.PlayerBurnDamage;
        int anticipated=preview.PlayerHealthLoss;bool fieldOnly=policy=="field-only",survival=policy!="damage";
        double defense=survival?1.8:.8;if(b.PlayerBoardingHealth<=anticipated)defense=12;else if(b.PlayerBoardingHealth<anticipated*2)defense=survival?5:3;
        double scale=(double)CoreBattleUpgradeBalance.MultiplierForLevel(c.UpgradeLevel);
        if(policy.StartsWith("single-")&&(k=="fire_03"||k=="fire_04"))return burnCast?-1000:1000;
        bool piercing=k=="curse_11"||k=="thunder_14";double damage=Math.Max(0,raw-(piercing?0:b.EnemyBlock));if(c.BoardingSpec.IsDelayed)damage*=.75;
        if(raw>0&&!c.BoardingSpec.IsDelayed&&damage>=b.EnemyBoardingHealth)return 100000/(fee+.65);
        double score=damage*(survival?1.1:1.5)+Math.Min(need,e.Block)*defense;
        int missing=b.PlayerMaxBoardingHealth-b.PlayerBoardingHealth;score+=Math.Min(missing,e.Repair)*(survival?defense:Math.Max(.6,defense));
        if(c.Draw>0&&b.Hand.Count<8)score+=c.Draw*(b.Energy>fee?4:1);
        int recovery=s.GetBattleCardEnergyRecovery(b.Hand[slot]);if(recovery>0)score+=b.Hand.Count>1||c.Draw>0?Math.Max(0,recovery-fee)*15:-50;
        int mana=b.Schools.AvailableMana,marks=b.Boarding.CurseMarks,tide=b.Boarding.TidePower,chain=b.Schools.ThunderChain;
        double burnFuture=Math.Min(3,Math.Max(1,b.EnemyBoardingHealth/30d));
        switch(k){
            case "basic_07":score+=Math.Min(Math.Max(0,missing-e.Repair),e.Repair)*.8;break;
            case "fire_02":score+=4*scale*burnFuture;break;case "fire_03":score+=8*scale*burnFuture;break;case "fire_04":score+=10*scale*burnFuture;break;
            case "fire_11":score-=3*defense;break;case "fire_14":score-=4*defense;break;
            case "fire_15":score+=b.Boarding.SelfBurn.Sum(x=>x.Amount)*(burnFuture+defense);break;
            case "arcane_01":case "arcane_02":score+=4;break;
            case "arcane_03":score+=b.Hand.Any(i=>{var q=s.GetBattleCard(i);return q.SchoolId=="arcane"&&q.BoardingSpec.IsAttack&&q.Energy<=b.Energy-fee;})?17:1;break;
            case "curse_01":case "curse_03":score+=3;break;case "curse_02":score+=9;break;
            case "curse_07":score+=Math.Min(preview.AttackDamage,(3+marks)*scale)*defense*1.5;break;
            case "curse_09":score+=marks*2*scale*preview.Actions.Count(x=>x.AttackDamage>0)*1.5;break;
            case "curse_10":score+=Math.Max(5,b.EnemyBoardingHealth*.10);break;
            case "tide_01":case "tide_02":case "tide_03":score+=Math.Min(need,e.Block)*.9;break;
            case "tide_05":score+=b.Boarding.TideGuardConversionActive?-100:10+b.PlayerBlock*.5;break;
            case "thunder_01":case "thunder_02":case "thunder_03":score+=b.Energy>fee?3:1;break;
            case "thunder_06":score+=b.Hand.Any(i=>{var q=s.GetBattleCard(i);return q.SchoolId=="thunder"&&q.BoardingSpec.IsAttack&&s.GetBattleCardEnergyCost(i)<=b.Energy-fee;})?8*scale:0;break;
            case "blood_01":case "blood_03":case "blood_04":case "blood_05":score-=3*defense;break;case "blood_02":score-=4*defense;break;
            case "blood_06":score+=Math.Min(missing,damage)*defense;break;
            case "blood_08":score+=8*scale*preview.Actions.Count(x=>x.AttackDamage>0)*(survival?1.2:1.5);break;
        }
        if(fieldOnly&&(raw>0||!k.StartsWith("basic_")))return -1000;
        return score/(fee+.65);
    }
    static void Track(Row r,CoreBattleState b){r.MinHP=Math.Min(r.MinHP,b.PlayerBoardingHealth);r.MaxMana=Math.Max(r.MaxMana,b.Schools.AvailableMana);r.MaxChain=Math.Max(r.MaxChain,b.Schools.ThunderChain);r.MaxTide=Math.Max(r.MaxTide,b.Boarding.TidePower);r.MaxMarks=Math.Max(r.MaxMarks,b.Boarding.CurseMarks);r.MaxEnemyBurn=Math.Max(r.MaxEnemyBurn,b.BoardingEnemy.SelfBurnDamage);r.MaxPlayerBurn=Math.Max(r.MaxPlayerBurn,b.BoardingEnemy.PlayerBurnDamage);}
    static string Fields(CoreBattleState b)=>string.Join(";",b.BoardingFields.Fields.Select(f=>f.Kind+":"+(f.PlayerOwned?"player":"enemy")+":"+f.RemainingTurns));
    static string Cell(CoreBattleState b,int x)=>string.Join(";",b.NavalFields.Cells.Where(c=>c.X==x&&c.Y==0).Select(c=>c.Kind+":"+(c.PlayerOwned?"player":"enemy")+":"+c.ExpiresAtOwnerTurn));
    static void Trace(StreamWriter f,CoreBattleState b,string ev,string card){f?.WriteLine(CsvLine(b.PhaseTurn,ev,card,b.PlayerBoardingHealth,b.PlayerBlock,b.EnemyBoardingHealth,b.EnemyBlock,b.Energy,b.Schools.AvailableMana,b.Schools.ThunderChain,b.Boarding.TidePower,b.Boarding.CurseMarks,b.BoardingEnemy.PlayerBurnDamage,b.BoardingEnemy.SelfBurnDamage,b.BoardingEnemy.Mana,b.BoardingEnemy.ThunderChain,Fields(b),b.BoardingEnemy.LastSelfBurnHealthLost,b.BoardingEnemy.LastBurnHealthLost));}
    static Dictionary<string,string[]> ReadDecks(string p)=>File.ReadAllLines(p).Skip(1).Where(x=>x.Trim().Length>0).Select(x=>x.Split(',')).ToDictionary(x=>x[0],x=>x[1].Split(';'));
    static bool Matches(string filter,string value)=>filter=="*"||filter.Split(',').Contains(value);
    static string F(double v)=>v.ToString("0.00",CultureInfo.InvariantCulture);
    static StreamWriter Writer(string d,string n)=>new StreamWriter(Path.Combine(d,n),false,new UTF8Encoding(true));
    static string CsvLine(params object[] v)=>string.Join(",",v.Select(x=>"\""+Convert.ToString(x,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\""));
    const string Methodology="# 接舷场地受控试验\n\n使用五档真实船型、船员、部件属性、卡牌合法强化上限；每系14张，最多2张同名；10药品、30火药，无战中补给或手牌注入。部件来源牌及专家牌不加入，隔离基础/六系卡牌。海战导航及铺场成本未模拟：在真实海战阶段将双方放置正交相邻且平行，向其两格受控注入指定场地/剩余时长/归属，然后调用真实 TryBoard。最多两格两类型，继承、同类选择、衰减及双方效果都由生产Core执行。没有把受控场地称为已验证的合法海战来源。\n\n同一敌人/档位/牌组/种子配对比较无场地与场地；damage、survival策略读取当前手牌、公开伤害报价、逐动作敌方预告，不窥牌库或执行回滚。两策略共享规则，仅防守权重不同。每场满生命入场，海战伤害和既有燃烧未注入，属于满血接舷边界。场地双方生效。field-only为额外14张纯防御治疗调度牌，绝不主动伤害、施咒或反击；其获胜只能来自场地、敌方自损等非主动攻击因素，不能等同火场单独伤害：应同时检查实际燃烧损血与敌方行为。\n\ncardWindowEnemyHpLoss为每张牌结算窗口的敌方净生命下降，包含即时咒印处决；turnWindowEnemyHpLoss为EndBattleTurn窗口净下降，包含燃烧、延迟、反击和咒印，不冒充纯直接攻击伤害。enemyBurnActualHpLoss/playerBurnActualHpLoss读取生产已提交燃烧结果，enemyAttackActualHpLoss和guardAbsorbed读取敌方已提交逐动作结果；净窗口损失可能被同窗口治疗抵消，不能与细项相加。仅记录有限启发式样本，不是最优玩家或总体胜率。记录源码和探针SHA256，任意修改须用新run-tag。\n";
}
