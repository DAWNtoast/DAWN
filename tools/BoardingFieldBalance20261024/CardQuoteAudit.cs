using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;

internal static class CardQuoteAudit
{
    static readonly Dictionary<string,int> Index=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).ToDictionary(i=>CoreVoyageSession.CardLibrary[i].Id);
    static object Invoke(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
    static void Set(object obj,string name,object value)=>obj.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(obj,value);
    static CoreVoyageSession Battle(string id,int rank,bool resources,bool desperate,int guard)
    {
        var s=new CoreVoyageSession();
        s.RestoreTreasureProgress(new CoreTreasureProgress {pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10)});
        s.SetSeed("BOARDING-CARD-QUOTE-AUDIT-20261024");
        if(!s.StartTrainingBattle(CoreTrainingTargets.All[0])||!s.EndBattleTurn())throw new Exception("Failed setup");
        s.Battle.EnemyHull=s.Battle.EnemyMaxHull=10000;
        s.Battle.DistanceMeters=100;
        Set(s.Battle,"EnemyHeadingDegrees",90f);Set(s.Battle,"PlayerHeadingDegrees",90f);
        s.Battle.Alignment=NavalBattleAlignment.Parallel;
        if(!s.TryBoard())throw new Exception("Failed boarding");
        var b=s.Battle;
        b.PlayerMaxBoardingHealth=1000;b.PlayerBoardingHealth=desperate?200:800;
        b.EnemyBoardingHealth=b.EnemyMaxBoardingHealth=10000;
        b.PlayerBlock=0;b.EnemyBlock=guard;b.Energy=100;
        b.Hand.Clear();b.DrawPile.Clear();b.DiscardPile.Clear();
        b.Hand.Add(Index[id]);b.Hand.Add(Index["card_boarding_basic_01"]);
        s.Cargo.Powder=100;s.Cargo.Medicine=100;Set(s,"Morale",0);
        var ranks=(Dictionary<string,int>)typeof(CoreVoyageSession).GetField("cardUpgradeLevels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);
        ranks[CoreBattleCardAliases.CanonicalId(id)]=rank;
        Set(b.Schools,"Mana",resources?12:0);
        Set(b.Schools,"ThunderChain",resources?10:0);
        if(resources){Invoke(s,"CTBGainTide",24);Invoke(s,"CTBAddMarks",8);Invoke(s,"BAddSelfBurn",5);}
        return s;
    }
    static int Main(string[] args)
    {
        string output=args[0];Directory.CreateDirectory(output);
        int checkedCount=0,skipped=0;
        using(var w=new StreamWriter(Path.Combine(output,"actual-versus-quote.csv")))
        {
            w.WriteLine("id,rank,resources,desperate,guard,quote,actual,delayed");
            foreach(var design in CoreBoardingCardCatalog.All.Where(c=>c.Damage>0))
            for(int rank=0;rank<=CoreCardUpgradeDesign.Find(design).MaximumLevel;rank++)
            foreach(bool resources in new[]{false,true})
            foreach(bool desperate in new[]{false,true})
            foreach(int guard in new[]{0,20})
            {
                var s=Battle(design.Id,rank,resources,desperate,guard);var b=s.Battle;
                var card=s.GetBattleCard(b.Hand[0]);
                if(s.GetBoardingChoiceUnavailableReason(0,(CoreBoardingPlayOptions)Invoke(s,"BDefaultOptions",0)).Length>0){skipped++;continue;}
                int quote=s.GetBoardingCardDamageQuote(card),before=b.EnemyBoardingHealth,block=b.EnemyBlock;
                if(!s.PlayCard(0))throw new Exception("Validated play failed: "+design.Id);
                int actual=card.BoardingSpec.IsDelayed?b.Boarding.DelayedDamage.Last().Amount:before-b.EnemyBoardingHealth+block-b.EnemyBlock;
                // A shield-removing rider is not part of direct attack damage.
                if(card.BoardingSpec.Key=="arcane_07")actual-=Math.Min(block,CoreCardUpgradeDesign.ScaleEffect(card,16,card.UpgradeLevel));
                if(card.BoardingSpec.Key=="tide_04")actual-=Math.Min(block,CoreCardUpgradeDesign.ScaleEffect(card,8,card.UpgradeLevel));
                if(card.BoardingSpec.Key=="thunder_04")actual-=Math.Min(block,CoreCardUpgradeDesign.ScaleEffect(card,12,card.UpgradeLevel));
                if(actual!=quote)throw new Exception(design.Id+" rank="+rank+" resources="+resources+" desperate="+desperate+" guard="+guard+" quote="+quote+" actual="+actual);
                w.WriteLine(string.Join(",",design.Id,rank,resources,desperate,guard,quote,actual,card.BoardingSpec.IsDelayed));checkedCount++;
            }
        }
        Console.WriteLine("PASS actual attack equals displayed quote: "+checkedCount+" cases; unavailable combinations skipped="+skipped);
        return 0;
    }
}
