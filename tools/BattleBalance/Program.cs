using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tests;

internal static class BattleBalanceProgram
{
    private sealed class Build
    {
        public Build(NavalBattleBalanceSimulator.Loadout ship, int level) { Ship = ship; Level = level; }
        public NavalBattleBalanceSimulator.Loadout Ship;
        public int Level;
    }
    private static int Main(string[] args)
    {
        int seeds = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 4;
        if (seeds < 1 || seeds > 256) throw new ArgumentOutOfRangeException(nameof(seeds));
        string output = args.Length > 1 ? args[1] : "UnityClient/Artifacts/BattleBalance";
        Directory.CreateDirectory(output);
        var builds = new[]
        {
            new Build(NavalBattleBalanceSimulator.Loadout.Starter, 0),
            new Build(NavalBattleBalanceSimulator.Loadout.Starter, 1),
            new Build(NavalBattleBalanceSimulator.Loadout.Starter, 3),
            new Build(NavalBattleBalanceSimulator.Loadout.Patrol, 1),
            new Build(NavalBattleBalanceSimulator.Loadout.Patrol, 2),
            new Build(NavalBattleBalanceSimulator.Loadout.Patrol, 3),
            new Build(NavalBattleBalanceSimulator.Loadout.Gunship, 2),
            new Build(NavalBattleBalanceSimulator.Loadout.Gunship, 3),
        };
        var results = new List<NavalBattleBalanceSimulator.Result>();
        using (var csv = new StreamWriter(Path.Combine(output, "runs.csv"), false, new System.Text.UTF8Encoding(true)))
        {
            csv.WriteLine(NavalBattleBalanceSimulator.CsvHeader);
            foreach (var scenario in NavalBattleBalanceSimulator.CalibrationScenarios)
            foreach (var build in builds)
            foreach (bool focused in new[] { false, true })
            foreach (NavalBattleBalanceSimulator.Tactic tactic in Enum.GetValues(typeof(NavalBattleBalanceSimulator.Tactic)))
            {
                // The actual tutorial enforces the built-in deck; upgraded late-game ships are not tutorial samples.
                if (scenario.Tutorial && (build.Ship != NavalBattleBalanceSimulator.Loadout.Starter || build.Level != 0 || focused)) continue;
                var group = new List<NavalBattleBalanceSimulator.Result>();
                for (int seed = 0; seed < seeds; seed++)
                {
                    var result = NavalBattleBalanceSimulator.Run("BALANCE-" + seed.ToString("000"), scenario,
                        build.Level, tactic, focused, build.Ship);
                    group.Add(result);
                    results.Add(result);
                    csv.WriteLine(result.Csv());
                }
                csv.Flush();
                Console.WriteLine(Summary(group));
            }
        }
        using (var report = new StreamWriter(Path.Combine(output, "summary.md"), false, new System.Text.UTF8Encoding(true)))
        {
            report.WriteLine("# 动态费用与冲撞战斗校准\n");
            report.WriteLine("UTC: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + ". 当前 Core 源码离线策略模拟，不能替代 Unity 实际游玩。\n");
            report.WriteLine("覆盖：真实教学首领、三地区普通战斗、三个区域首领、遗迹首领、五传奇。通过 StartVoyage 生成真实节点，仅反射 StartBattle 跳过航海。开战后不改手牌、血量、随机数或数值。教学地图有固定随机种子，其重复行是重复性检查，不是独立地图样本。\n");
            report.WriteLine("Starter：裸轻帆船，14 普通水手、无特殊牌、部件品质1；Patrol/Gunship：巡防/重炮舰，18/26普通水手与2特殊船员，部件品质2、五件真实蓝色来源牌、额外属性0。特殊牌用历史航行经验达到对应阶级。初始20水20粮、20木10药、25火药40炮弹，不包含航程损耗或装备采购过程。\n");
            report.WriteLine("默认与专用十张牌组；固定贪心策略读取当前手牌、动态实际费用和公开敌意图，不预读牌库。Cannon优先炮战且遇冲撞警告尝试拉开；Boarding优先接舷，能够安全抢先冲撞时可抢先。被敌冲撞后两策略均正常白刃战，不强制判败。行动上限仅防止模拟挂死，不能算游戏规则。\n");
            report.WriteLine("总计 " + results.Count + " 战，每组 " + seeds + " 次。胜率含失败/超时；其余均值除特别注明均只取胜局。末船体可能包含船匠胜利修复，累计受伤与最低船体另列。弹药累计逐次合法出牌成本，不用结算库存倒推。纯炮战胜率仅统计在Naval阶段击沉的胜局；转接舷率及原因统计全部样本，不能将被敌撞入接舷后的获胜称作炮战成功。\n");
            report.WriteLine("| 敌舰 | 船型 | 卡组 | 策略 | 升级 | 胜率 | 纯炮战胜率 | 转接舷率(原因) | 回合 | 剩船体 | 剩船员 | 受伤(船/人) | 敌攻击 | 弹/药 | 撞(我/敌) | 超时 | 最大同阶段连打 |\n|---|---|---|---|---:|---:|---:|---|---:|---:|---:|---|---:|---|---|---:|---:|");
            foreach (var group in results.GroupBy(value => value.EnemyId + "/" + value.Ship + "/" + value.Deck + "/" + value.Strategy + "/" + value.Upgrade))
                report.WriteLine(Summary(group.ToList()));
        }
        return 0;
    }
    private static string Summary(List<NavalBattleBalanceSimulator.Result> group)
    {
        var first = group[0];
        var wins = group.Where(value => value.Won).ToArray();
        Func<Func<NavalBattleBalanceSimulator.Result, int>, string> mean = selector => wins.Length == 0 ? "—" : wins.Average(selector).ToString("0.0", CultureInfo.InvariantCulture);
        return "| " + first.EnemyName + " | " + first.Ship + " | " + first.Deck + " | " + first.Strategy + " | " + first.Upgrade +
            " | " + (100d * wins.Length / group.Count).ToString("0.0", CultureInfo.InvariantCulture) + "% | " +
            (100d * group.Count(v => v.Won && v.FinalStage == "Naval") / group.Count).ToString("0.0", CultureInfo.InvariantCulture) + "% | " +
            (100d * group.Count(v => v.BoardingEntry != "none") / group.Count).ToString("0.0", CultureInfo.InvariantCulture) + "% (计划" +
            group.Count(v => v.BoardingEntry == "planned") + ",我撞" + group.Count(v => v.BoardingEntry == "player-ram") +
            ",敌撞" + group.Count(v => v.BoardingEntry == "enemy-ram") + ") | " +
            mean(v => v.Turns) + " | " + mean(v => v.Hull) + " | " + mean(v => v.CrewHealth) + " | " +
            mean(v => v.HullDamageTaken) + "/" + mean(v => v.CrewDamageTaken) + " | " + mean(v => v.EnemyAttackTurns) + " | " +
            mean(v => v.CannonballsUsed) + "/" + mean(v => v.PowderUsed) + " | " + mean(v => v.PlayerRams) + "/" + mean(v => v.EnemyRams) + " | " +
            group.Count(v => v.Outcome.EndsWith("watchdog")) + " | " + group.Max(v => v.MaxCombo) + " |";
    }
}
