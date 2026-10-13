using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Tools.CardBalanceAudit
{
    public static partial class DummyDeckBenchmark
    {
        private const int EnduranceRoundLimit = 200;
        private const string StreakHeader = "deck,school,size,coreCopies,rank,tactic,seed,sourceCardId,sourceKind,ordinaryCrew,cleared,attempted,stopReason,stopRound,stopTarget,rightCensored,reachedFive,allFiveWins,stage5Wins,stage5RepeatWins,totalTurns,totalCards,initialHull,maxHull,finalHull,initialCrewHealth,maxCrewHealth,finalCrewHealth,netHullLoss,netCrewLoss,hullRemainingFraction,crewRemainingFraction,initialPowder,initialCannonballs,initialWood,initialMedicine,powderUsed,cannonballsUsed,woodUsed,medicineUsed,totalSuppliesUsed,remainingPowder,remainingCannonballs,remainingWood,remainingMedicine,hullBloodPaid,crewBloodPaid,grossHullLoss,grossCrewLoss,effectiveHullRecovered,effectiveCrewRecovered,boardingCarryover,minHull,minCrewHealth,initialMorale,finalMorale";
        private const string RoundHeader = "streakSeed,roundNumber,moraleBefore,moraleAfter," + ResultHeader + ",remainingPowder,remainingCannonballs,remainingWood,remainingMedicine";
        private sealed class EnduranceRound
        {
            public string StreakSeed; public int Number, MoraleBefore, MoraleAfter; public Result Battle;
            public object[] Values() => new object[] { StreakSeed, Number, MoraleBefore, MoraleAfter }.Concat(Battle.Values()).Concat(new object[] {
                Battle.InitialPowder - Battle.Powder, Battle.InitialBalls - Battle.Balls, Battle.InitialWood - Battle.Wood, Battle.InitialMedicine - Battle.Medicine }).ToArray();
        }
        private sealed class EnduranceStreak
        {
            public Deck Deck; public int Rank; public string Tactic, Seed, StopReason;
            public List<EnduranceRound> Rounds = new List<EnduranceRound>();
            public int Cleared => Rounds.Count(r => r.Battle.Won);
            public object[] Values()
            {
                var f=Rounds.First(); var l=Rounds.Last(); var a=Rounds.Select(r=>r.Battle).ToArray();
                int powder=a.Sum(r=>r.Powder), balls=a.Sum(r=>r.Balls), wood=a.Sum(r=>r.Wood), medicine=a.Sum(r=>r.Medicine);
                return new object[] { Deck.Id, Deck.School, Deck.Size, Deck.Copies, Rank, Tactic, Seed, Deck.Source?.Card.Id ?? "", Deck.Source?.SourceKind.ToString() ?? "none", Deck.OrdinaryCrew,
                    Cleared, Rounds.Count, StopReason, l.Number, l.Battle.Target, StopReason=="round-cap", Rounds.Count>=5, Cleared>=5,
                    Rounds.Count(r=>r.Number>=5&&r.Battle.Won), Rounds.Count(r=>r.Number>5&&r.Battle.Won), a.Sum(r=>r.Turns), a.Sum(r=>r.Cards),
                    f.Battle.InitialHull,f.Battle.MaxHull,l.Battle.Hull,f.Battle.InitialCrew,f.Battle.MaxCrew,l.Battle.Crew,
                    f.Battle.InitialHull-l.Battle.Hull,f.Battle.InitialCrew-l.Battle.Crew,l.Battle.HullRemainingFraction,l.Battle.CrewRemainingFraction,
                    f.Battle.InitialPowder,f.Battle.InitialBalls,f.Battle.InitialWood,f.Battle.InitialMedicine,powder,balls,wood,medicine,powder+balls+wood+medicine,
                    l.Battle.InitialPowder-l.Battle.Powder,l.Battle.InitialBalls-l.Battle.Balls,l.Battle.InitialWood-l.Battle.Wood,l.Battle.InitialMedicine-l.Battle.Medicine,
                    a.Sum(r=>r.HullBloodPaid),a.Sum(r=>r.CrewBloodPaid),a.Sum(r=>r.HullGrossLoss),a.Sum(r=>r.CrewGrossLoss),a.Sum(r=>r.HullEffectiveHealing),a.Sum(r=>r.CrewEffectiveHealing),a.Sum(r=>r.BoardingCarryover),
                    a.Min(r=>r.MinHull),a.Min(r=>r.MinCrew),f.MoraleBefore,l.MoraleAfter };
            }
        }
        private static string EnduranceKey(EnduranceStreak s) => s.Deck.Id+"/"+s.Rank+"/"+s.Tactic+"/"+s.Seed;
        private static string EnduranceRowKey(Dictionary<string,string> r) => r["deck"]+"/"+r["rank"]+"/"+r["tactic"]+"/"+r["seed"];
        private static Target EnduranceTargetForRound(int round)
        {
            if(round<1)throw new ArgumentOutOfRangeException(nameof(round));
            return Targets[Math.Min(round,Targets.Length)-1];
        }
        private static void ValidateEnduranceBoundaries(string output)
        {
            if(EnduranceRoundLimit!=200||EnduranceTargetForRound(1).Id!=Targets[0].Id||EnduranceTargetForRound(5).Id!=Targets[4].Id||EnduranceTargetForRound(6).Id!=Targets[4].Id||EnduranceTargetForRound(200).Id!=Targets[4].Id)
                throw new InvalidOperationException("Endurance target/cap boundary mismatch");
            var synthetic=new EnduranceStreak {Deck=new Deck {Id="tool-boundary-only"},Seed="not-a-trial",StopReason="round-cap"};
            for(int round=1;round<=200;round++)synthetic.Rounds.Add(new EnduranceRound {Number=round,Battle=new Result {Outcome="win",Target=EnduranceTargetForRound(round).Id,MaxHull=1,MaxCrew=1,Hull=1,Crew=1}});
            Func<EnduranceStreak,Dictionary<string,string>> values=s=>StreakHeader.Split(',').Zip(s.Values(),(h,v)=>new {h,v}).ToDictionary(x=>x.h,x=>Convert.ToString(x.v,CultureInfo.InvariantCulture));
            var cap=values(synthetic);
            if(cap["cleared"]!="200"||cap["attempted"]!="200"||cap["rightCensored"]!="True"||cap["stage5Wins"]!="196"||cap["stage5RepeatWins"]!="195"||cap["allFiveWins"]!="True")throw new InvalidOperationException("Endurance cap count mismatch");
            synthetic.Rounds=synthetic.Rounds.Take(5).ToList();synthetic.Rounds[4].Battle.Outcome="loss";synthetic.StopReason="loss";
            var lost=values(synthetic);
            if(lost["cleared"]!="4"||lost["attempted"]!="5"||lost["reachedFive"]!="True"||lost["allFiveWins"]!="False"||lost["stage5RepeatWins"]!="0"||lost["rightCensored"]!="False")throw new InvalidOperationException("Endurance failed fifth count mismatch");
            File.WriteAllText(Path.Combine(output,"tool-boundary-checks.txt"),"Passed: target mapping for rounds1/5/6/200; 200-win right-censor ledger and stage-V repeat counts; failed fifth is4 cleared/5 attempted. Synthetic ledger checks only, never included in persisted challenge or battle data.\n");
        }
        private static EnduranceStreak RunEndurance(Deck deck,int rank,string tactic,string seed,bool replay=false)
        {
            var s=Create(deck,rank,"triple-full",seed);
            var streak=new EnduranceStreak { Deck=deck,Rank=rank,Tactic=tactic,Seed=seed };
            int crew=CoreBattleBalance.BoardingHealthForOrdinaryCrew(deck.OrdinaryCrew);
            for(int round=1;round<=EnduranceRoundLimit;round++)
            {
                var target=EnduranceTargetForRound(round);
                string roundSeed=seed+"::ENDURANCE::ROUND::"+round.ToString("D4");
                int hull=s.Hull,powder=s.Cargo.Powder,balls=s.Cargo.Cannonballs,wood=s.Cargo.Wood,medicine=s.Cargo.Medicine,morale=s.Morale;
                // Public Core entry is passed exactly the previous round's crew
                // health. It does not restore hull, cargo, morale or old wounds.
                var r=Run(deck,rank,tactic,target,"triple-full",roundSeed,replay?new List<object[]>():null,s,crew,roundSeed);
                if(r.InitialHull!=hull||r.InitialCrew!=crew||r.InitialPowder!=powder||r.InitialBalls!=balls||r.InitialWood!=wood||r.InitialMedicine!=medicine)
                    throw new InvalidOperationException("Endurance entry restored health or supplies");
                streak.Rounds.Add(new EnduranceRound {StreakSeed=seed,Number=round,MoraleBefore=morale,MoraleAfter=s.Morale,Battle=r});
                crew=r.Crew;
                if(!r.Won) {streak.StopReason=r.Outcome;break;}
                if(round==EnduranceRoundLimit)streak.StopReason="round-cap";
            }
            var first=streak.Rounds.First().Battle;var last=streak.Rounds.Last().Battle;var rows=streak.Rounds.Select(r=>r.Battle).ToArray();
            if(first.InitialHull-last.Hull!=rows.Sum(r=>r.HullGrossLoss-r.HullEffectiveHealing)||first.InitialCrew-last.Crew!=rows.Sum(r=>r.BoardingCarryover+r.CrewGrossLoss-r.CrewEffectiveHealing))
                throw new InvalidOperationException("Endurance cumulative health conservation mismatch");
            if(first.InitialPowder-s.Cargo.Powder!=rows.Sum(r=>r.Powder)||first.InitialBalls-s.Cargo.Cannonballs!=rows.Sum(r=>r.Balls)||first.InitialWood-s.Cargo.Wood!=rows.Sum(r=>r.Wood)||first.InitialMedicine-s.Cargo.Medicine!=rows.Sum(r=>r.Medicine))
                throw new InvalidOperationException("Endurance cumulative inventory conservation mismatch");
            return streak;
        }
        private static void RunEnduranceMatrix(string output,List<Deck> decks,int seeds,int offset,int shard,int shards,bool sources)
        {
            ValidateEnduranceBoundaries(output);
            var streaks=new List<EnduranceStreak>();int index=0;
            foreach(var deck in decks.Where(d=>d.Valid))
            {
                foreach(int rank in sources?new[]{0,3}:new[]{0,1,2,3})foreach(string tactic in Tactics)for(int seed=0;seed<seeds;seed++)
                {
                    if(index++%shards!=shard)continue;
                    streaks.Add(RunEndurance(deck,rank,tactic,"DECK-SUPPLY-"+(seed+offset).ToString("D4")));
                }
                Console.WriteLine(deck.Id+": "+streaks.Count+" endurance challenges / "+streaks.Sum(s=>s.Rounds.Count)+" battles complete");
            }
            WriteCsv(Path.Combine(output,"streaks.csv"),StreakHeader,streaks.Select(s=>s.Values()));
            WriteCsv(Path.Combine(output,"rounds.csv"),RoundHeader,streaks.SelectMany(s=>s.Rounds).Select(r=>r.Values()));
        }
        private static void MergeEnduranceParts(string output,string partsRoot,int seeds,string mode,string filter)
        {
            ValidateEnduranceBoundaries(output);
            bool sources=mode=="endurancesources";
            var decks=sources?BuildSourceDecks():BuildDecks();if(filter.Length>0)decks=decks.Where(d=>filter.Split(',').Contains(d.School)).ToList();
            var streaks=new List<Dictionary<string,string>>();var rounds=new List<Dictionary<string,string>>();string coreHash=null,toolHash=null;
            foreach(string part in Directory.GetDirectories(partsRoot).OrderBy(p=>p))
            {
                if(!File.Exists(Path.Combine(part,"completed.txt")))throw new InvalidOperationException("Incomplete endurance shard");
                string core=File.ReadAllText(Path.Combine(part,"core-source-sha256.txt")),tool=File.ReadAllText(Path.Combine(part,"benchmark-source-sha256.txt"));
                if(coreHash!=null&&(coreHash!=core||toolHash!=tool))throw new InvalidOperationException("Mixed endurance snapshots");
                coreHash=core;toolHash=tool;streaks.AddRange(ReadCsv(Path.Combine(part,"streaks.csv")));rounds.AddRange(ReadCsv(Path.Combine(part,"rounds.csv")));
            }
            int expected=decks.Count(d=>d.Valid)*(sources?2:4)*Tactics.Length*seeds;
            if(streaks.Count!=expected||streaks.Select(EnduranceRowKey).Distinct().Count()!=expected||streaks.Select(r=>r["seed"]).Distinct().Count()!=seeds)throw new InvalidOperationException("Endurance streak count mismatch");
            var roundGroups=rounds.GroupBy(r=>r["deck"]+"/"+r["rank"]+"/"+r["tactic"]+"/"+r["streakSeed"]).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>int.Parse(r["roundNumber"])).ToArray());
            foreach(var streak in streaks)
            {
                var a=roundGroups[EnduranceRowKey(streak)];int wins=0;
                if(a.Length!=int.Parse(streak["attempted"]))throw new InvalidOperationException("Endurance attempt count mismatch");
                for(int i=0;i<a.Length;i++)
                {
                    var r=a[i];Func<string,int> n=k=>int.Parse(r[k]);
                    if(n("roundNumber")!=i+1||r["target"]!=Targets[Math.Min(i,Targets.Length-1)].Id||r["seed"]!=r["streakSeed"]+"::ENDURANCE::ROUND::"+(i+1).ToString("D4"))throw new InvalidOperationException("Endurance level/seed mismatch");
                    if(r["outcome"]=="win")wins++;else if(i!=a.Length-1)throw new InvalidOperationException("Endurance continued after non-victory");
                    if(n("netHullLoss")!=n("initialHull")-n("hull")||n("netHullLoss")!=n("grossHullLossIncludingBlood")-n("effectiveHullRecovered")||n("netCrewLoss")!=n("initialCrewHealth")-n("crewHealth")||n("netCrewLoss")!=n("boardingCarryover")+n("grossCrewLossExcludingCarryoverIncludingBlood")-n("effectiveCrewRecovered"))throw new InvalidOperationException("Merged round health mismatch");
                    if(i>0)
                    {
                        var p=a[i-1];
                        if(r["initialHull"]!=p["hull"]||r["initialCrewHealth"]!=p["crewHealth"]||r["moraleBefore"]!=p["moraleAfter"]||r["initialPowder"]!=p["remainingPowder"]||r["initialCannonballs"]!=p["remainingCannonballs"]||r["initialWood"]!=p["remainingWood"]||r["initialMedicine"]!=p["remainingMedicine"])throw new InvalidOperationException("Merged round boundary restored state");
                    }
                }
                if(wins!=int.Parse(streak["cleared"])||(streak["stopReason"]=="round-cap"?(a.Length!=EnduranceRoundLimit||wins!=EnduranceRoundLimit):a.Last()["outcome"]!=streak["stopReason"]))throw new InvalidOperationException("Endurance stop accounting mismatch");
            }
            Directory.CreateDirectory(output);WriteDeckManifest(output,decks);
            WriteCsv(Path.Combine(output,"targets.csv"),"target,hull,crewHealth,cannonBaseDamage,meleeBaseDamage,guardPerEnemyTurn,healPerEnemyTurn",Targets.Select(t=>new object[]{t.Id,t.Hull,t.CrewHealth,t.Cannon,t.Melee,t.Guard,t.Heal}));
            WriteCsv(Path.Combine(output,"streaks.csv"),StreakHeader,streaks.OrderBy(EnduranceRowKey).Select(r=>StreakHeader.Split(',').Select(h=>(object)r[h]).ToArray()));
            WriteCsv(Path.Combine(output,"rounds.csv"),RoundHeader,rounds.OrderBy(r=>r["deck"]+"/"+r["rank"]+"/"+r["tactic"]+"/"+r["streakSeed"]).ThenBy(r=>int.Parse(r["roundNumber"])).Select(r=>RoundHeader.Split(',').Select(h=>(object)r[h]).ToArray()));
            var replays=new List<object[]>();
            foreach(var g in streaks.GroupBy(r=>r["school"]+"/"+r["rank"]+"/"+r["tactic"]))
            {
                var original=g.OrderByDescending(r=>int.Parse(r["cleared"])).ThenBy(EnduranceRowKey).First();
                var replay=RunEndurance(decks.Single(d=>d.Id==original["deck"]),int.Parse(original["rank"]),original["tactic"],original["seed"],true);
                var actual=replay.Values().Select(v=>Convert.ToString(v,CultureInfo.InvariantCulture)).ToArray();
                if(!actual.SequenceEqual(StreakHeader.Split(',').Select(h=>original[h])))throw new InvalidOperationException("Endurance replay mismatch "+EnduranceRowKey(original));
                replays.Add(new object[]{original["deck"],original["school"],original["rank"],original["tactic"],original["seed"],replay.Cleared,replay.Rounds.Count,"replay-matched"});
            }
            WriteCsv(Path.Combine(output,"streak-replays.csv"),"deck,school,rank,tactic,seed,cleared,attempted,status",replays);
            File.WriteAllText(Path.Combine(output,"merge-checks.txt"),"Unique challenges: "+streaks.Count+"\nPersisted battles: "+rounds.Count+"\nEvery round and boundary health/inventory/morale verified.\nSampled complete challenge replays: "+replays.Count+"\n");
            Console.WriteLine("Merged and verified "+streaks.Count+" endurance challenges / "+rounds.Count+" persisted battles.");
        }
    }
}
