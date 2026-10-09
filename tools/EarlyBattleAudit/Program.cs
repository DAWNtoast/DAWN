using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tests;

internal static class EarlyBattleAuditProgram
{
    private static readonly MethodInfo StartBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo BestCard = typeof(NavalBattleBalanceSimulator).GetMethod("BestCard", BindingFlags.NonPublic | BindingFlags.Static);
    private sealed class Sample
    {
        public string Scenario, Seed, Enemy, Strategy, Outcome;
        public int Turns, Hull, Crew, Powder, Balls;
        public bool Won;
        public string Csv() => string.Join(",", new object[] { Scenario, Seed, Enemy, Strategy, Outcome, Turns, Hull, Crew, Powder, Balls }
            .Select(value => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""));
    }

    private static int Main(string[] args)
    {
        int seeds = args.Length > 0 ? int.Parse(args[0]) : 32;
        string output = args.Length > 1 ? args[1] : "D:/Game/UnityClient/Artifacts/EarlyBattleAudit20260909";
        Directory.CreateDirectory(output);
        var all = new List<Sample>();
        for (int route = 0; route <= 1; route++)
        foreach (int ship in route == 0 ? new[] { 0 } : new[] { 1, 2 })
        foreach (int level in route == 0 ? new[] { 0 } : new[] { 0, 1 })
        foreach (string enemy in route == 0 ? new[] { "normal", "random", "boss" } : new[] { "normal", "random" })
        foreach (NavalBattleBalanceSimulator.Tactic tactic in Enum.GetValues(typeof(NavalBattleBalanceSimulator.Tactic)))
        {
            var group = new List<Sample>();
            for (int seed = 0; seed < seeds; seed++)
                group.Add(Run("EARLY-" + seed.ToString("000"), route, ship, level, enemy, tactic));
            all.AddRange(group);
            Console.WriteLine(Summary(group));
        }
        using (var csv = new StreamWriter(Path.Combine(output, "runs.csv"), false, new System.Text.UTF8Encoding(true)))
        {
            csv.WriteLine("scenario,seed,enemy,strategy,outcome,turns,hull,crew,powderUsed,cannonballsUsed");
            foreach (Sample value in all) csv.WriteLine(value.Csv());
        }
        using (var report = new StreamWriter(Path.Combine(output, "summary.md"), false, new System.Text.UTF8Encoding(true)))
        {
            report.WriteLine("# 前两图初始补给战斗采样\n");
            report.WriteLine("每组合 " + seeds + " 个相同种子，共 " + all.Count + " 战。固定策略直接反射复用 NavalBattleBalanceSimulator.BestCard，未修改原评分或第三图矩阵。\n");
            report.WriteLine("第一图：新档默认轻帆船，70船体/4装甲/8火力，默认三套基础牌，未强化；无已招募专长船员、无蓝色以上部件。前端新档招募标记默认0，显式空来源避免旧Core默认六职业能力造成高估。\n");
            report.WriteLine("第二图：商船95船体/5装甲/7火力或巡防舰120船体/8装甲/16火力；假定已招募伊芙琳与巴洛，无蓝色部件与额外属性；基础牌零/一阶，对应来源牌历史经验0/120。第二图配置是明确的成长假设，不是声称新档自动送船员。\n");
            report.WriteLine("所有场次保留Core新建会话的原始补给：8火药、10炮弹；没有采购或额外注入。满生命开战，只跳过到达事件前的航程。普通事件优先取生成地图节点、若该种子未抽中则取同路线原始事件池；随机伏击由原CreateRandomAmbush生成；路线boss取真实地图节点。只反射进入正常StartBattle，之后绝不改手牌、敌HP或随机数。\n");
            report.WriteLine("胜率包含失败与watchdog。平均回合/船体/船员仅统计胜局；弹药平均/最大用量覆盖所有场次，分开报告火药与炮弹。船体包含木匠正常胜利修复2点。该单战样本不包含连续遭遇或远航损耗，不能据此推导整个航线无需补给。\n");
            report.WriteLine("| 场景 | 敌人 | 打法 | 胜率 | 胜局回合 | 胜局船体 | 胜局船员 | 火药均值/最大 | 炮弹均值/最大 | watchdog |\n|---|---|---|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var group in all.GroupBy(value => value.Scenario + "/" + value.Enemy + "/" + value.Strategy))
                report.WriteLine(Summary(group.ToList()));
        }
        return 0;
    }

    private static Sample Run(string seed, int route, int ship, int level, string enemyKey, NavalBattleBalanceSimulator.Tactic tactic)
    {
        var session = new CoreVoyageSession();
        session.SelectShip(ship);
        session.SelectRoute(route);
        session.SetSeed(seed);
        bool hired = route > 0;
        session.ConfigureCrewCapabilities(hired, hired, false, false, false, false);
        session.ConfigureWitchCapability(false);
        session.ConfigureSpecialCardSources(hired ? NavalBattleBalanceSimulator.FixtureCrewIds : Array.Empty<string>(), Array.Empty<string>());
        session.ConfigureCardUpgrades(CoreVoyageSession.CardLibrary.Where(card => !CoreVoyageSession.IsSpecialBattleCard(card.Id)).ToDictionary(card => card.Id, card => level));
        if (level > 0) session.RestoreSpecialCardExperience(session.AcquiredSpecialCardIds.ToDictionary(id => id, id => 120));
        int initialPowder = session.Cargo.Powder;
        int initialBalls = session.Cargo.Cannonballs;
        if (initialPowder != 8 || initialBalls != 10) throw new InvalidOperationException("Initial supply balance changed; update the declared fixture before auditing.");
        session.StartVoyage();
        CoreEncounter encounter;
        bool boss = enemyKey == "boss";
        if (boss) encounter = session.Map.Nodes.Single(node => node.IsBoss).Encounter;
        else if (enemyKey == "random") encounter = (CoreEncounter)typeof(CoreVoyageSession).GetMethod("CreateRandomAmbush", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, new object[] { false });
        else
        {
            string id = route == 0 ? "event_reef_coral_ambush" : "gold_customs";
            encounter = session.Map.Nodes.FirstOrDefault(node => node.Encounter.Id == id)?.Encounter;
            if (encounter == null)
            {
                var pool = (IReadOnlyList<CoreEncounter>)typeof(CoreVoyageSession).GetMethod("CreateEncounterPool", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(session, new object[] { CoreVoyageSession.Routes[route] });
                encounter = pool.Single(value => value.Id == id);
            }
        }
        StartBattle.Invoke(session, new object[] { encounter, boss });
        CoreBattleState battle = session.Battle;
        CoreBattleProfile profile = CoreBattleBalance.Resolve(encounter, route, boss);
        if (battle.EnemyMaxHull != profile.EnemyHull) throw new InvalidOperationException("Enemy profile mismatch.");
        var sample = new Sample { Seed = seed, Scenario = "图" + (route + 1) + " " + CoreVoyageSession.Ships[ship].Name + " +" + level, Enemy = encounter.Name, Strategy = tactic.ToString() };
        bool watchdog = false;
        for (int turn = 0; turn < NavalBattleBalanceSimulator.TurnWatchdog && session.Phase == CoreDemoPhase.Battle; turn++)
        {
            sample.Turns++;
            int actions = 0;
            for (; actions < NavalBattleBalanceSimulator.ActionWatchdog && session.Phase == CoreDemoPhase.Battle; actions++)
            {
                if (tactic == NavalBattleBalanceSimulator.Tactic.Boarding && session.CanBoard(out _) && session.TryBoard()) continue;
                int handIndex = (int)BestCard.Invoke(null, new object[] { session, tactic });
                if (handIndex < 0) break;
                if (!session.PlayCard(handIndex)) throw new InvalidOperationException("Bot selected an illegal action.");
            }
            if (actions >= NavalBattleBalanceSimulator.ActionWatchdog && session.Phase == CoreDemoPhase.Battle) { watchdog = true; break; }
            if (session.Phase == CoreDemoPhase.Battle && !session.EndBattleTurn()) throw new InvalidOperationException("End-turn rejected.");
        }
        sample.Won = session.Phase == CoreDemoPhase.Sailing;
        sample.Outcome = sample.Won ? "win" : watchdog ? "action-watchdog" : session.Phase == CoreDemoPhase.Settlement ? "loss" : "turn-watchdog";
        sample.Hull = session.Hull;
        sample.Crew = battle.PlayerBoardingHealth;
        sample.Powder = initialPowder - session.Cargo.Powder;
        sample.Balls = initialBalls - session.Cargo.Cannonballs;
        return sample;
    }

    private static string Summary(List<Sample> group)
    {
        Sample first = group[0];
        Sample[] wins = group.Where(value => value.Won).ToArray();
        Func<Func<Sample, int>, string> mean = selector => wins.Length == 0 ? "—" : wins.Average(selector).ToString("0.0", CultureInfo.InvariantCulture);
        return "| " + first.Scenario + " | " + first.Enemy + " | " + first.Strategy + " | " +
            (100d * wins.Length / group.Count).ToString("0.0", CultureInfo.InvariantCulture) + "% | " +
            mean(value => value.Turns) + " | " + mean(value => value.Hull) + " | " + mean(value => value.Crew) + " | " +
            group.Average(value => value.Powder).ToString("0.0", CultureInfo.InvariantCulture) + "/" + group.Max(value => value.Powder) + " | " +
            group.Average(value => value.Balls).ToString("0.0", CultureInfo.InvariantCulture) + "/" + group.Max(value => value.Balls) + " | " +
            group.Count(value => value.Outcome.EndsWith("watchdog")) + " |";
    }
}
