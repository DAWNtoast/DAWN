using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Same-loadout paired comparison. No post-entry resource/hand injection.
public static class SchoolPowerProbe
{
    static readonly string[] Schools={"ordinary","fire","arcane","curse","tide","thunder","blood"};
    static readonly Dictionary<string,int> Ids=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).ToDictionary(i=>CoreVoyageSession.CardLibrary[i].Id,i=>i);
    static readonly Dictionary<string,string[]> Overrides=new Dictionary<string,string[]>();
    static readonly List<string> Manifest=new List<string>{Csv("cohort","loadout","school","stage","slot","id","name","requestedRank","actualRank")};
    static string Cohort;static bool Sources;
    static string[] Preparation(string school)=>school=="ordinary"?new[]{"card_energy_order","card_energy_order","card_tactical_draw","card_tactical_draw","card_brace","card_brace","card_load_shot","card_load_shot","card_deck_polish","card_deck_polish"}:new[]{"card_school_"+school+"_b01","card_school_"+school+"_b01","card_school_"+school+"_b02","card_school_"+school+"_b02","card_energy_order","card_energy_order","card_tactical_draw","card_tactical_draw","card_brace","card_brace"};
    static string[] Deck(string school,string stage)
    {
        if(Cohort=="grid-risk"&&Overrides.TryGetValue("candidate|"+school+"|"+stage,out string[] riskyIds))return riskyIds;
        if(Overrides.TryGetValue(Cohort+"|"+school+"|"+stage,out string[] ids))return ids;
        return stage=="preparation"?Preparation(school):NavalBalanceProbe.Decks.First(d=>d.School==school).Ids;
    }
    static CoreVoyageSession Create(NavalBalanceProbe.Loadout l,string school,int seed,bool voyage,bool ignored)
    {
        var s=new CoreVoyageSession();s.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10)});
        s.SelectShip(Enumerable.Range(0,CoreVoyageSession.Ships.Count).First(i=>CoreVoyageSession.Ships[i].Id==l.ShipId));
        s.SetSeed("SCHOOL-POWER-20261024-"+seed);s.ConfigureCrewCapabilities(false,false,false,false,false,false);s.ConfigureDepartureCrew(l.Crew,0);
        s.ConfigureComponentBonuses(l.HullDelta,l.ArmorDelta,l.FirepowerDelta,l.SpeedDelta,0);s.ConfigureComponentContextEffect(0,0,0,0,0,1);s.ConfigureBattleEquipment(l.BowQuality,l.PlatingQuality);
        s.ConfigureSpecialCardSources(Array.Empty<string>(),Sources?l.Components:Array.Empty<string>());
        string[] naval=Deck(school,"naval"),prep=Deck(school,"preparation");
        if(naval.Length!=12||prep.Length!=10||naval.GroupBy(x=>x).Any(g=>g.Count()>2)||prep.GroupBy(x=>x).Any(g=>g.Count()>2))throw new Exception("Invalid paired deck size/copies "+Cohort+" "+school);
        var sourceIds=Sources?l.Components.Select(id=>CoreSpecialCardCatalog.ForComponent(id)?.Card.Id).Where(x=>x!=null):Enumerable.Empty<string>();
        foreach(string id in naval.Concat(prep).Concat(sourceIds))if(!Ids.ContainsKey(id))throw new Exception("Unknown card "+id);
        s.ConfigureCardUpgrades(naval.Concat(prep).Concat(sourceIds).Distinct().ToDictionary(id=>id,id=>l.Rank));
        if(!s.SetBattleDeck(NavalBattleStage.Naval,naval.Select(id=>Ids[id]).ToArray())||!s.SetBattleDeck(NavalBattleStage.Preparation,prep.Select(id=>Ids[id]).ToArray()))throw new Exception("Core rejected legal deck");
        if(s.MaxHull!=l.Hull||s.Hull!=l.Hull||s.EffectiveArmor!=l.Armor||s.EffectiveFirepower!=l.Firepower)throw new Exception("Physical loadout mismatch "+l.Id);
        if(voyage){s.StartVoyage();if(s.Phase!=CoreDemoPhase.Sailing)throw new Exception("Voyage failed");}
        s.Cargo.Powder=100;s.Cargo.Cannonballs=100;s.Cargo.Wood=30;s.Cargo.Medicine=30;return s;
    }
    sealed class Sample
    {
        public string Cohort,Loadout,School,Target,Policy,Outcome,Trace;public int Tier,Seed,InitialHP,MaxHP,EndHP,MinimumHP,EnemyMax,EnemyHP,Turns,Plays,Energy,Absorbed,PaidLife,Healing,EnemyWindowLoss,WindowDamage,Powder,Balls,Wood,Medicine,DeckSize,StartMana,StartManaLimit,MaxMana,MaxChain,MaxTide,MaxMarks;
        public string Row()=>Csv(Cohort,Loadout,Tier,School,Target,Policy,Seed,Outcome,Turns,MaxHP,InitialHP,EndHP,MinimumHP,EnemyMax,EnemyHP,WindowDamage,EnemyWindowLoss,Absorbed,PaidLife,Healing,Plays,Energy,Powder,Balls,Wood,Medicine,DeckSize,StartMana,StartManaLimit,MaxMana,MaxChain,MaxTide,MaxMarks,Trace);
    }
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;string output=args[0],mode=args[3];int seeds=int.Parse(args[1]),turns=int.Parse(args[2]);Sources=args[4]=="sources";
        string filter=args.Length>5?args[5]:"*";string schools=args.Length>6?args[6]:"*";string cohorts=args.Length>7?args[7]:"base,candidate";
        ReadOverrides(Path.Combine(output,"decks.csv"));NavalBalanceProbe.SessionFactory=Create;var samples=new List<Sample>();
        foreach(string cohort in cohorts.Split(','))
        {
            Cohort=cohort;
            NavalBalanceProbe.AllowThunderOverload=cohort=="grid-risk";
            foreach(var l in NavalBalanceProbe.Loadouts(Path.Combine(output,"calibration-loadouts.csv")))
            foreach(string school in Schools.Where(x=>(schools=="*"||schools.Split(',').Contains(x))&&(!(cohort=="flow"||cohort=="grid-risk")||x=="ordinary"||x=="thunder")&&(string.IsNullOrEmpty(l.School)||x=="ordinary"||x==l.School)))
            {
                if(Deck(school,"naval").Concat(Deck(school,"preparation")).Any(id=>!Ids.ContainsKey(id)))throw new Exception("Manifest has new cards absent from snapshot: "+cohort+"/"+school+". Use -Cohorts base for before.");
                LogDeck(l,school,"naval");LogDeck(l,school,"preparation");
                string group=Safe(cohort+"-"+l.Id+"-"+school);string groupPath=Path.Combine(output,group);Directory.CreateDirectory(Path.Combine(groupPath,"traces"));
                foreach(bool survival in new[]{false,true})for(int seed=0;seed<seeds;seed++)
                {
                    if(mode=="boss")
                    {
                        foreach(var boss in BossBalanceProbe.Scenarios.Where(x=>x.Tier==l.Tier&&(filter=="*"||filter.Split(',').Contains(x.Id))))
                        foreach(int hp in new[]{100,70})
                        {
                            var r=BossBalanceProbe.Run(Create(l,school,seed,true,true),boss,school,hp,seed,turns,true,survival);
                            string trace=Path.Combine(group,"traces",Safe(boss.Id+"-"+(survival?"survival":"damage")+"-"+hp+"-"+seed)+".txt");File.WriteAllText(Path.Combine(output,trace),r.Trace,Encoding.UTF8);
                            samples.Add(new Sample{Cohort=cohort,Loadout=l.Id,Tier=l.Tier,School=school,Target=boss.Id+"-hp"+hp,Policy=r.Policy,Seed=seed,Outcome=r.Outcome,Turns=r.NavalTurns,MaxHP=r.MaxHull,InitialHP=r.InitialHull,EndHP=r.FinalHull,MinimumHP=r.MinimumHull,EnemyMax=r.EnemyMax,EnemyHP=r.EnemyFinal,WindowDamage=r.Damage,EnemyWindowLoss=r.EnemyWindowHullLoss,Absorbed=r.Absorbed,PaidLife=r.PaidLife,Healing=r.Healing,Plays=r.Cards,Energy=r.Energy,Powder=r.Powder,Balls=r.Balls,Wood=r.Wood,Medicine=r.Medicine,DeckSize=r.NavalDeckSize,StartMana=r.StartMana,StartManaLimit=r.StartManaLimit,MaxMana=r.MaxMana,MaxChain=r.MaxChain,MaxTide=r.MaxTide,MaxMarks=r.MaxMarks,Trace=trace});
                        }
                    }
                    else
                    {
                        NavalBalanceProbe.TargetMode=mode;
                        var r=NavalBalanceProbe.Run(l,new NavalBalanceProbe.DeckDefinition(school,"paired",Deck(school,"naval")),survival,seed,turns,groupPath,true);
                        samples.Add(new Sample{Cohort=cohort,Loadout=l.Id,Tier=l.Tier,School=school,Target=mode+"-"+l.Tier,Policy=r.Policy,Seed=seed,Outcome=r.Outcome,Turns=r.Turns,MaxHP=r.MaxHP,InitialHP=r.MaxHP,EndHP=r.EndHP,MinimumHP=r.MinimumHP,EnemyMax=r.EnemyMax,EnemyHP=r.EnemyHP,WindowDamage=r.WindowEnemyHullLoss,EnemyWindowLoss=r.EnemyTurnLoss,Absorbed=r.Absorbed,PaidLife=r.PaidLife,Healing=r.Healing,Plays=r.Plays+r.PrepPlays,Energy=r.EnergySpent,Powder=r.Powder,Balls=r.Balls,Wood=r.Wood,Medicine=r.Medicine,DeckSize=r.DeckSize,StartMana=r.StartMana,StartManaLimit=r.StartManaLimit,MaxMana=r.Peak[1],MaxMarks=r.Peak[2],MaxTide=r.Peak[3],MaxChain=r.Peak[4],Trace=Path.Combine(group,r.Trace)});
                    }
                }
                Console.WriteLine(cohort+" "+l.Id+" "+school+": "+samples.Count+" samples");
            }
        }
        File.WriteAllLines(Path.Combine(output,"used-decks.csv"),Manifest.Distinct(),Encoding.UTF8);
        File.WriteAllLines(Path.Combine(output,"samples.csv"),new[]{Csv("cohort","loadout","tier","school","target","policy","seed","outcome","turns","maxHP","initialHP","endHP","minimumHP","enemyMax","enemyHP","enemyHullLossAcrossWindows","enemyTurnHullLoss","absorbed","paidLife","healingNetOfCost","playsIncludingPreparation","energySpentIncludingPreparation","powder","balls","wood","medicine","navalDeckSize","startMana","startManaLimit","maxMana","maxChain","maxTide","maxMarks","trace")}.Concat(samples.Select(x=>x.Row())),Encoding.UTF8);
        Summarize(output,samples);return 0;
    }
    static bool Kill(Sample s)=>s.Outcome=="kill"||s.Outcome=="sunk";
    static void Summarize(string output,List<Sample> rows)
    {
        var summary=new List<string>{Csv("cohort","loadout","tier","target","school","policy","n","kills","deaths","other","meanTurnsOnKills","meanEndHP","meanMinimumHP","meanNetHullLoss","meanEnemyTurnHullLoss","meanAbsorbed","meanPaidLife","meanHealing","meanEnergy","meanPlays","meanPowder","meanBalls","meanWood","meanMedicine","meanWindowDamage","ordinarySpeedRatioIfAllKilled","ordinaryHullLossReduction","damagePerEnergyRatioToOrdinary","maxMana","maxChain","maxTide","maxMarks")};
        foreach(var g in rows.GroupBy(x=>new{x.Cohort,x.Loadout,x.Tier,x.Target,x.School,x.Policy}))
        {
            var b=rows.Where(x=>x.Cohort==g.Key.Cohort&&x.Loadout==g.Key.Loadout&&x.Target==g.Key.Target&&x.Policy==g.Key.Policy&&x.School=="ordinary").ToArray();
            double loss=g.Average(x=>x.InitialHP-x.EndHP),baseLoss=b.Length>0?b.Average(x=>x.InitialHP-x.EndHP):0;
            string speed=b.Length>0&&b.All(Kill)&&g.All(Kill)?F(b.Average(x=>x.Turns)/g.Average(x=>x.Turns)):"";
            string efficiency=b.Length>0&&b.Sum(x=>x.WindowDamage)>0&&g.Sum(x=>x.Energy)>0?F(((double)g.Sum(x=>x.WindowDamage)/g.Sum(x=>x.Energy))/((double)b.Sum(x=>x.WindowDamage)/Math.Max(1,b.Sum(x=>x.Energy)))):"";
            summary.Add(Csv(g.Key.Cohort,g.Key.Loadout,g.Key.Tier,g.Key.Target,g.Key.School,g.Key.Policy,g.Count(),g.Count(Kill),g.Count(x=>x.Outcome=="death"||x.Outcome=="dead"),g.Count(x=>!Kill(x)&&x.Outcome!="death"&&x.Outcome!="dead"),g.Any(Kill)?F(g.Where(Kill).Average(x=>x.Turns)):"",F(g.Average(x=>x.EndHP)),F(g.Average(x=>x.MinimumHP)),F(loss),F(g.Average(x=>x.EnemyWindowLoss)),F(g.Average(x=>x.Absorbed)),F(g.Average(x=>x.PaidLife)),F(g.Average(x=>x.Healing)),F(g.Average(x=>x.Energy)),F(g.Average(x=>x.Plays)),F(g.Average(x=>x.Powder)),F(g.Average(x=>x.Balls)),F(g.Average(x=>x.Wood)),F(g.Average(x=>x.Medicine)),F(g.Average(x=>x.WindowDamage)),speed,baseLoss>0?F(1-loss/baseLoss):"",efficiency,g.Max(x=>x.MaxMana),g.Max(x=>x.MaxChain),g.Max(x=>x.MaxTide),g.Max(x=>x.MaxMarks)));
        }
        File.WriteAllLines(Path.Combine(output,"summary.csv"),summary,Encoding.UTF8);
    }
    static void LogDeck(NavalBalanceProbe.Loadout l,string school,string stage)
    {
        var deck=Deck(school,stage);for(int i=0;i<deck.Length;i++){var c=CoreVoyageSession.CardLibrary[Ids[deck[i]]];Manifest.Add(Csv(Cohort,l.Id,school,stage,i,c.Id,c.Name,l.Rank,Math.Min(l.Rank,CoreBattleUpgradeBalance.MaximumLevelFor(c))));}
    }
    static void ReadOverrides(string path)
    {
        if(!File.Exists(path))return;var rows=File.ReadAllLines(path).Skip(1).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(Parse).ToArray();
        foreach(var g in rows.GroupBy(x=>x[0]+"|"+x[1]+"|"+x[2]))Overrides[g.Key]=g.OrderBy(x=>int.Parse(x[3])).Select(x=>x[4]).ToArray();
        foreach(var k in Overrides.Keys.Where(k=>k.Contains("|ordinary|"))){string stage=k.Split('|')[2];var standard=stage=="naval"?NavalBalanceProbe.Decks[0].Ids:Preparation("ordinary");if(!Overrides[k].SequenceEqual(standard))throw new Exception("Ordinary reference must remain fixed");}
    }
    static string Safe(string s)=>new string(s.Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_').ToArray());
    static string F(double n)=>n.ToString("0.000",CultureInfo.InvariantCulture);
    static string Csv(params object[] cells)=>string.Join(",",cells.Select(x=>"\""+Convert.ToString(x,CultureInfo.InvariantCulture).Replace("\"","\"\"").Replace("\n"," ")+"\""));
    static string[] Parse(string line){var list=new List<string>();var b=new StringBuilder();bool quote=false;for(int i=0;i<line.Length;i++){char c=line[i];if(c=='\"'){if(quote&&i+1<line.Length&&line[i+1]=='\"'){b.Append(c);i++;}else quote=!quote;}else if(c==','&&!quote){list.Add(b.ToString().TrimStart('\uFEFF'));b.Clear();}else b.Append(c);}list.Add(b.ToString());return list.ToArray();}
}
