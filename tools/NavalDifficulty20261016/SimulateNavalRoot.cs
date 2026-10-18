using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;

internal static partial class SimulateNaval
{
    static string Build;
    static StreamWriter Actions;
    static readonly MethodInfo Start = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
    sealed class Result
    {
        public string Enemy, Outcome;
        public int Seed, Turns, Cards, Moves, Shots, StartHull, Hull, EnemyHull, Damage, Repair, Powder, Balls, Wood, MinimumHull;
    }

    static int Main(string[] args)
    {
        string output = args[0]; Build = args[1];
        int count = int.Parse(args[2]), first = int.Parse(args[3]);
        int route = Build == "starter" ? 0 : Build == "mixed" ? 1 : 2;
        string[] enemies = route == 0 ? new[] { "boss_route_shattered_sail_reef", "boss_route_shattered_sail_reef_tide" } :
            route == 1 ? new[] { "boss_route_golden_tide_trade", "boss_route_golden_tide_trade_thunder" } :
            new[] { "boss_route_moon_tide_ruins", "boss_route_moon_tide_ruins_arcane", "legendary_pirate_iron_oath", "legendary_pirate_red_widow", "legendary_pirate_thunder_crown", "legendary_pirate_ember_ark", "legendary_pirate_tide_requiem", "legendary_pirate_last_horizon", CoreBattleBalance.TreasureBossId };
        if (args.Length > 4 && !string.IsNullOrEmpty(args[4])) enemies = args[4].Split(',');
        Directory.CreateDirectory(output);
        Export(output, NavalBuilds.Create(Build, route, first));
        var rows = new List<Result>();
        using (Actions = Writer(output, "actions.csv"))
        using (var runs = Writer(output, "runs.csv"))
        {
            Actions.WriteLine("enemy,seed,turn,stage,action,detail,hullBefore,hullAfter,enemyHullBefore,enemyHullAfter,px,py,ex,ey,heading,gridDistance,ap,energy,powder,balls,wood,intent");
            runs.WriteLine("build,enemy,seed,outcome,turns,cards,moves,shots,startHull,hull,enemyHull,damage,repair,powderUsed,ballsUsed,woodUsed,minHull");
            foreach (string enemy in enemies)
                for (int seed = first; seed < first + count; seed++)
                {
                    var r = Run(route, enemy, seed, seed < first + 3); rows.Add(r);
                    runs.WriteLine(Csv(Build, enemy, seed, r.Outcome, r.Turns, r.Cards, r.Moves, r.Shots, r.StartHull, r.Hull, r.EnemyHull, r.Damage, r.Repair, r.Powder, r.Balls, r.Wood, r.MinimumHull));
                }
        }
        using (var file = Writer(output, "summary.csv"))
        {
            file.WriteLine("build,enemy,runs,wins,winPercent,forcedBoarding,losses,timeouts,averageWinningHullPercent,averageTurns,averageMoves,averageShots,averagePowder,averageBalls,averageWood");
            foreach (var g in rows.GroupBy(r => r.Enemy))
            {
                var wins = g.Where(r => r.Outcome == "win").ToArray();
                string row = Csv(Build, g.Key, g.Count(), wins.Length, F(wins.Length * 100d / g.Count()), g.Count(r => r.Outcome == "forced-boarding"), g.Count(r => r.Outcome == "loss"), g.Count(r => r.Outcome == "timeout"), wins.Length > 0 ? F(wins.Average(r => 100d * r.Hull / r.StartHull)) : "", F(g.Average(r => r.Turns)), F(g.Average(r => r.Moves)), F(g.Average(r => r.Shots)), F(g.Average(r => r.Powder)), F(g.Average(r => r.Balls)), F(g.Average(r => r.Wood)));
                file.WriteLine(row); Console.WriteLine(row);
            }
        }
        return 0;
    }

    static Result Run(int route, string enemy, int seed, bool trace)
    {
        var s = NavalBuilds.Create(Build, route, seed);
        var boss = CoreRouteBossCatalog.Find(enemy); var legendary = CoreLegendaryWarships.Find(enemy);
        var encounter = boss?.CreateEncounter() ?? legendary?.CreateEncounter() ?? (enemy == CoreBattleBalance.TreasureBossId ? CoreLegendaryWarships.CreateTreasureEncounter() : s.Map.Nodes.First(n => n.Encounter.Id == enemy).Encounter);
        Start.Invoke(s, new object[] { encounter, boss != null || legendary != null || enemy == CoreBattleBalance.TreasureBossId });
        var b = s.Battle;
        var r = new Result { Enemy = enemy, Seed = seed, StartHull = s.Hull, MinimumHull = s.Hull };
        while (s.Phase == CoreDemoPhase.Battle && b.Stage != NavalBattleStage.Boarding && r.Turns < 100)
        {
            r.Turns++; int actions = 0;
            for (; actions < 80 && s.Phase == CoreDemoPhase.Battle && b.Stage != NavalBattleStage.Boarding; actions++)
            {
                int best = -1; double value = .01;
                for (int i = 0; i < b.Hand.Count; i++)
                    if (s.CanPlayCard(i)) { double v = Score(s, b.Hand[i]); if (v > value) { value = v; best = i; } }
                if (best >= 0)
                {
                    int index = b.Hand[best]; var card = s.GetBattleCard(index);
                    int hp = s.Hull, ehp = b.EnemyHull;
                    r.Powder += s.GetBattleCardPowderCost(index); r.Balls += card.CannonballCost; int wood = s.Cargo.Wood;
                    if (!s.PlayCard(best)) throw new InvalidOperationException("Rejected card " + card.Id);
                    r.Wood += Math.Max(0, wood - s.Cargo.Wood); r.Cards++;
                    if (card.Action == NavalBattleCardAction.Cannon) r.Shots++;
                    Observe(r, s, hp); if (trace) Trace(s, r, "card", card.Id, hp, ehp); continue;
                }
                int before = s.Hull, enemyBefore = b.EnemyHull;
                if (Move(s)) { r.Moves++; Observe(r, s, before); if (trace) Trace(s, r, "move", "legal-plan-commit-heading", before, enemyBefore); continue; }
                break;
            }
            if (actions >= 80) throw new InvalidOperationException("Action watchdog");
            if (s.Phase != CoreDemoPhase.Battle || b.Stage == NavalBattleStage.Boarding) break;
            int h = s.Hull, e = b.EnemyHull;
            if (!s.EndBattleTurn()) throw new InvalidOperationException("End turn rejected");
            Observe(r, s, h); if (trace) Trace(s, r, "end-turn", "", h, e);
        }
        r.Hull = s.Hull; r.EnemyHull = b.EnemyHull;
        r.Outcome = s.Phase == CoreDemoPhase.Settlement ? "loss" : b.Stage == NavalBattleStage.Boarding ? "forced-boarding" : s.Phase == CoreDemoPhase.Sailing && b.EnemyHull <= 0 ? "win" : "timeout";
        return r;
    }
}
