using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tests;

internal static class ShipProgressionProgram
{
    private sealed class Policy
    {
        public string Deck; public ShipProgressionSimulator.Tactic Tactic;
        public Policy(string deck, ShipProgressionSimulator.Tactic tactic) { Deck=deck; Tactic=tactic; }
    }
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    private static string N(double number) => number.ToString("0.00",CI);
    private static string Q(object value) => "\"" + Convert.ToString(value,CI).Replace("\"","\"\"") + "\"";
    private static int Main(string[] args)
    {
        int seeds = args.Length > 0 ? int.Parse(args[0],CI) : 4;
        if (seeds < 1 || seeds > 128) throw new ArgumentOutOfRangeException(nameof(seeds));
        string output = args.Length > 1 ? args[1] : "UnityClient/Artifacts/ShipProgression20261003/Calibration-baseline";
        Directory.CreateDirectory(output);
        bool validation = args.Length > 2 && args[2] == "Validation";
        string candidate = args.Length > 3 ? args[3] : "None";
        ShipProgressionSimulator.CrewReference = args.Length > 4 ? double.Parse(args[4],CI) : 20d;
        ShipProgressionSimulator.UseCrewScale = candidate == "CrewScale" || candidate == "StarterAndCrewScale";
        var policies = new[] {
            new Policy("default",ShipProgressionSimulator.Tactic.Cannon),
            new Policy("default",ShipProgressionSimulator.Tactic.Boarding),
            new Policy("focused",ShipProgressionSimulator.Tactic.Cannon),
            new Policy("focused",ShipProgressionSimulator.Tactic.Boarding),
            new Policy("sustain",ShipProgressionSimulator.Tactic.Sustain),
        };
        if (validation) policies = policies.Where(p=>p.Deck=="sustain" || (p.Deck=="default" && p.Tactic==ShipProgressionSimulator.Tactic.Boarding) || (p.Deck=="focused" && p.Tactic==ShipProgressionSimulator.Tactic.Cannon)).ToArray();
        var enemies = ShipProgressionSimulator.CalibrationScenarios.Where(s =>
            s.EnemyId.StartsWith("boss_route_") || s.EnemyId.StartsWith("legendary_") || s.EnemyId == CoreBattleBalance.TreasureBossId).ToArray();
        if (validation) enemies = enemies.Where(s=>s.EnemyId.StartsWith("legendary_")).ToArray();
        var ships = validation ? new[] {0,5,7} : Enumerable.Range(0,CoreVoyageSession.Ships.Count).ToArray();
        var rows = new List<ShipProgressionSimulator.Result>();
        using (var csv = new StreamWriter(Path.Combine(output,"runs.csv"),false,new System.Text.UTF8Encoding(true)))
        {
            csv.WriteLine(ShipProgressionSimulator.CsvHeader);
            foreach (int ship in ships)
            for (int level=validation ? 2 : 0;level<=3;level++)
            {
                foreach (var policy in policies)
                foreach (var enemy in enemies)
                for (int seed=0;seed<seeds;seed++)
                {
                    var r = ShipProgressionSimulator.Run((validation ? "SHIP-VALIDATION-" : "PROGRESSION-")+seed.ToString("000"),enemy,level,policy.Tactic,policy.Deck,ship);
                    rows.Add(r); csv.WriteLine(r.Csv());
                }
                csv.Flush();
                Console.WriteLine(CoreVoyageSession.Ships[ship].Id+" tier "+level+": "+rows.Count+" completed");
            }
        }
        using (var csv = new StreamWriter(Path.Combine(output,"group-summary.csv"),false,new System.Text.UTF8Encoding(true)))
        {
            csv.WriteLine("ship,shipIndex,shipCost,upgrade,upgradeCost,deck,strategy,enemyId,count,wins,pureNavalWins,meanHull,meanCrew,meanHullDamage,meanCrewDamage,meanHullHealed,meanCrewHealed,meanGuardAbsorbed,meanTurns,meanEnemyAttacks,maxBlock,timeouts");
            foreach (var g in rows.GroupBy(r=>r.Ship+"/"+r.Upgrade+"/"+r.Deck+"/"+r.Strategy+"/"+r.EnemyId))
            {
                var f=g.First(); var w=g.Where(r=>r.Won).ToArray();
                Func<Func<ShipProgressionSimulator.Result,int>,double> avg=select=>w.Length==0?0:w.Average(select);
                csv.WriteLine(string.Join(",",new object[]{f.Ship,f.ShipIndex,CoreShipMarketCatalog.PurchaseCost(f.ShipIndex),f.Upgrade,f.UpgradeCost,f.Deck,f.Strategy,f.EnemyId,g.Count(),w.Length,
                    w.Count(r=>r.FinalStage=="Naval"),N(avg(r=>r.Hull)),N(avg(r=>r.CrewHealth)),N(avg(r=>r.HullDamageTaken)),N(avg(r=>r.CrewDamageTaken)),N(avg(r=>r.HullHealed)),N(avg(r=>r.CrewHealed)),
                    N(avg(r=>r.GuardAbsorbed)),N(avg(r=>r.Turns)),N(avg(r=>r.EnemyAttackTurns)),g.Max(r=>r.MaxBlock),g.Count(r=>r.Outcome.EndsWith("watchdog"))}.Select(Q)));
            }
        }
        using (var report=new StreamWriter(Path.Combine(output,"summary.md"),false,new System.Text.UTF8Encoding(true)))
        {
            report.WriteLine("# 船只成长"+(validation ? "独立32种子复核" : "12船基线")+"：真实 Core 离线模拟\n");
            report.WriteLine("候选标签："+candidate+"。None为未改Core；其余仅在本目录CoreSnapshot应用实验，不是正式产品规则。\n");
            report.WriteLine("UTC "+DateTime.UtcNow.ToString("s",CI)+"；"+rows.Count+" 战；每构筑/敌人 "+seeds+" 种子。CoreSnapshot 为实际编译来源。未启动 Unity，未模拟航海、赚钱、购船路径或连续无补给挑战。\n");
            report.WriteLine("入选船满编普通水手、裸装品质1、无特殊船员/部件牌/额外属性。升级仅对当前三阶段牌组涉及的基础牌购买，实际 TryUpgradeBattleCard 逐级扣款；0/I/II/III 分别模拟不同投入。装备和船员没有额外成长时，小船能否靠卡牌满级抹掉差异是本轮重点。所有数值来自未修改的真实Core。\n");
            report.WriteLine("基线五固定策略（扩大复核只取默认接舷、专用炮战、8张续航接舷）：默认炮战/默认接舷、专用炮战/专用接舷、8张防御治疗接舷。读取当前手牌与公开意图，不预读洗牌。防御治疗策略看当前生命和预告伤害调整权重，未进行穷举或按敌人调参。炮战会尝试避撞，被迫接舷仍继续打；纯炮战击杀另记。胜率是这些有限策略的结果，失败不能证明玩家没有更好策略；有限种子不能证明全随机稳定。\n");
            report.WriteLine("20水20粮18木10药25火药40炮弹，验证真实容量足够；战斗开始后不增资源，不改手牌/RNG/生命。包扎/修理按真实规则执行，包括当前不耗药/木的卡牌行为。每场重新满血满资源，不是连续5战。100回合/64动作防挂死上限，不是玩法规则。\n");
            report.WriteLine("## 各船各投入的同一固定策略五传奇总表现\n");
            report.WriteLine("每行从5个预先固定的策略中选总胜场最多者，各传奇仍使用同一套牌和策略；这是挑选效果较好的有限策略，不是预知随机。若总胜场并列，选强化成本较低者。最差传奇胜场揭示仅总胜率高但无法通吃的情况。生命/受伤/治疗/格挡均只统计胜局。\n");
            report.WriteLine("| 船 | 船价 | 等级 | 牌价 | 固定构筑 | 五传奇胜场 | 最差传奇胜场 | 剩船/人 | 承伤船/人 | 治疗船/人 | 吸收格挡 | 回合 |\n|---|---:|---:|---:|---|---:|---:|---|---|---|---:|---:|");
            foreach(var byShip in rows.Where(r=>r.EnemyId.StartsWith("legendary_")).GroupBy(r=>r.ShipIndex+"/"+r.Upgrade))
            {
                var best=byShip.GroupBy(r=>r.Deck+"/"+r.Strategy).OrderByDescending(g=>g.Count(r=>r.Won)).ThenBy(g=>g.First().UpgradeCost).First();
                var f=best.First();var w=best.Where(r=>r.Won).ToArray();
                Func<Func<ShipProgressionSimulator.Result,int>,string> mean=selector=>w.Length==0?"—":N(w.Average(selector));
                report.WriteLine("| "+f.Ship+" | "+CoreShipMarketCatalog.PurchaseCost(f.ShipIndex)+" | "+f.Upgrade+" | "+f.UpgradeCost+" | "+f.Deck+"/"+f.Strategy+" | "+w.Length+"/"+best.Count()+" | "+best.GroupBy(r=>r.EnemyId).Min(g=>g.Count(r=>r.Won))+"/"+seeds+" | "+mean(r=>r.Hull)+"/"+mean(r=>r.CrewHealth)+" | "+mean(r=>r.HullDamageTaken)+"/"+mean(r=>r.CrewDamageTaken)+" | "+mean(r=>r.HullHealed)+"/"+mean(r=>r.CrewHealed)+" | "+mean(r=>r.GuardAbsorbed)+" | "+mean(r=>r.Turns)+" |");
            }
            report.WriteLine("\n完整分敌人结果见 group-summary.csv；逐场伤害/治疗/耗弹/接舷方式与超时见 runs.csv。允许攒钱跨级，一次直接购买3200重炮舰可通关本身不是问题；应检查起始轻帆是否在合理卡牌投入后与高级船同样稳定。机制建议需结合路径/经济和真实UI验证。 ");
        }
        return 0;
    }
}
