using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tests;

// Exact recorded 4560-gold build, with no full-deck upgrades or specialist cards.
internal static class OldLoopProgram
{
    private static readonly Dictionary<int, int> Levels = new Dictionary<int, int>
    { { 1, 2 }, { 3, 3 }, { 16, 3 }, { 13, 3 }, { 14, 3 }, { 6, 3 } };
    private static readonly int[] ShipDeck = { 1, 1, 1, 13, 13, 13, 7, 7 };
    private static readonly int[] BoardingDeck = { 3, 3, 3, 16, 16, 16, 14, 14 };
    private static readonly MethodInfo StartBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo BestCard = typeof(NavalBattleBalanceSimulator).GetMethod("BestCard", BindingFlags.Static | BindingFlags.NonPublic);
    private sealed class Row
    {
        public string Seed, Enemy, Name, Ship, Outcome, Connection = "none";
        public int ShipCost, InitialHull, InitialCrew, Turns, Cards, ZeroCost, EnergySpent, Hull, Crew, MinHull,
            HullDamage, CrewDamage, EnemyAttacks, PlayerRams, EnemyRams, MaxCombo, MaxBlock, EnemyHull, EnemyCrew;
        public bool Won => Outcome == "win";
        public string Csv() => string.Join(",", new object[]
        {
            Seed, Enemy, Name, Ship, ShipCost, 4560, InitialHull, InitialCrew, Outcome, Connection, Turns, Cards,
            ZeroCost, EnergySpent, Hull, Crew, MinHull, HullDamage, CrewDamage, EnemyAttacks, PlayerRams, EnemyRams,
            MaxCombo, MaxBlock, EnemyHull, EnemyCrew,
        }.Select(v => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""));
    }
    private static int Main(string[] args)
    {
        int seeds = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 16;
        string output = args.Length > 1 ? args[1] : "UnityClient/Artifacts/BattleRules20261002/OldLoop-v2";
        if (seeds < 1 || seeds > 256) throw new ArgumentOutOfRangeException(nameof(seeds));
        Directory.CreateDirectory(output);
        var all = new List<Row>();
        using (var csv = new StreamWriter(Path.Combine(output, "old-loop-runs.csv"), false, new System.Text.UTF8Encoding(true)))
        {
            csv.WriteLine("seed,enemyId,enemyName,ship,shipCost,upgradeCost,initialHull,initialCrew,outcome,boardingEntry,turns,cards,zeroCostCards,energySpent,hull,crewHealth,minimumHull,hullDamageTaken,crewDamageTaken,enemyAttackTurns,playerRams,enemyRams,maxCombo,maxBlock,enemyHull,enemyCrew");
            foreach (int ship in new[] { 0, 7 })
            foreach (CoreLegendaryWarship enemy in CoreLegendaryWarships.All)
            {
                var group = new List<Row>();
                for (int seed = 0; seed < seeds; seed++)
                {
                    Row row = Run("OLD-LOOP-" + seed.ToString("000"), ship, enemy.Id);
                    all.Add(row); group.Add(row); csv.WriteLine(row.Csv());
                }
                csv.Flush();
                Console.WriteLine(Summary(group));
            }
        }
        using (var report = new StreamWriter(Path.Combine(output, "old-loop-summary.md"), false, new System.Text.UTF8Encoding(true)))
        {
            report.WriteLine("# 原实玩循环构筑复核\n");
            report.WriteLine("UTC: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "。离线真实 Core 模拟，不代表 Unity UI 实玩。\n");
            report.WriteLine("精确升级：1 满帆转向 II；3 钩索绊敌、16 集结队伍、13 并排行船、14 水手刺击、6 炮火齐射 III；其他0。通过正常 TryUpgradeBattleCard 购买并验证总成本4560。第6号牌已购买强化但没有编入原循环牌组。\n");
            report.WriteLine("准备/炮战各为 {1,1,1,13,13,13,7,7}；接舷为 {3,3,3,16,16,16,14,14}。没有盾阵、包扎、维修、撤离、特殊船员或部件牌。双方船型都裸装品质1；轻帆14普通水手(76生命)，重炮28普通水手(132生命)。船价0/3200另列，未模拟赚钱或采购旅程。\n");
            report.WriteLine("16个种子按两船共用；只跳过航行进入真实地图传奇节点，开战后不改手牌、生命、敌配置或随机数。使用有限策略读取当前手牌与实际动态费用，不预读牌库；优先合法接舷，可安全先撞时允许先撞，不结束无限循环强行判赢。\n");
            report.WriteLine("每组 " + seeds + " 场，总 " + all.Count + " 场。胜率包含失败/超时；剩余生命、回合、受伤/敌攻击均值仅胜局，范围覆盖全部样本。无卡组治疗，累计受伤不会被包扎隐藏。\n");
            report.WriteLine("| 船型 | 传奇 | 胜场 | 胜局回合 | 胜局船体 | 胜局船员 | 船员范围(全局) | 胜局受伤(船/人) | 敌攻击 | 接舷原因(计划/我撞/敌撞) | 最大连打 | 最大格挡 | 超时 |\n|---|---|---:|---:|---:|---:|---|---|---:|---|---:|---:|---:|");
            foreach (var group in all.GroupBy(r => r.Ship + "/" + r.Enemy)) report.WriteLine(Summary(group.ToList()));
        }
        return 0;
    }
    private static CoreVoyageSession Create(string seed, int ship)
    {
        var session = new CoreVoyageSession();
        session.SelectShip(ship); session.SelectRoute(2); session.SetSeed(seed);
        session.ConfigureCrewCapabilities(false, false, false, false, false, false);
        session.ConfigureDepartureCrew(session.CrewCountValue, 0);
        session.ConfigureSpecialCardSources(Array.Empty<string>(), Array.Empty<string>());
        session.ConfigureBattleEquipment(1f, 1f);
        session.ConfigureComponentBonuses(0, 0, 0, 0, 0);
        // Fixture bankroll only verifies the cost of the build, not an earned-currency claim.
        session.RestoreProfile(50000, session.CurrentPort);
        decimal before = session.ProfileGold;
        foreach (var pair in Levels)
            for (int level = 0; level < pair.Value; level++)
                if (!session.TryUpgradeBattleCard(pair.Key, out string reason)) throw new InvalidOperationException(reason);
        if (before - session.ProfileGold != 4560) throw new InvalidOperationException("Recorded build cost changed.");
        for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            if (session.GetCardUpgradeLevel(CoreVoyageSession.CardLibrary[index].Id) != (Levels.TryGetValue(index, out int level) ? level : 0))
                throw new InvalidOperationException("Unexpected upgrade outside the recorded build.");
        if (!session.SetBattleDeck(NavalBattleStage.Preparation, ShipDeck) || !session.SetBattleDeck(NavalBattleStage.Naval, ShipDeck) ||
            !session.SetBattleDeck(NavalBattleStage.Boarding, BoardingDeck)) throw new InvalidOperationException("Recorded deck is no longer legal.");
        foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            if (session.GetBonusBattleCards(stage).Count != 0) throw new InvalidOperationException("Bare-ship audit acquired extra cards.");
        session.Cargo.Water = session.Cargo.Food = 20;
        session.Cargo.Wood = 20; session.Cargo.Medicine = 10;
        session.Cargo.Beer = session.Cargo.Iron = session.Cargo.Cloth = 0;
        session.Cargo.Powder = 25; session.Cargo.Cannonballs = 40;
        session.StartVoyage();
        return session;
    }
    private static Row Run(string seed, int ship, string enemyId)
    {
        var session = Create(seed, ship);
        var node = session.Map.Nodes.Single(n => n.Encounter.Id == enemyId);
        StartBattle.Invoke(session, new object[] { node.Encounter, node.IsBoss });
        var battle = session.Battle;
        var row = new Row { Seed = seed, Enemy = enemyId, Name = node.Encounter.Name, Ship = session.SelectedShip.Id,
            ShipCost = CoreShipMarketCatalog.PurchaseCost(ship), InitialHull = session.Hull, InitialCrew = battle.PlayerMaxBoardingHealth,
            MinHull = session.Hull };
        bool watchdog = false;
        for (int turn = 0; turn < 100 && session.Phase == CoreDemoPhase.Battle; turn++)
        {
            row.Turns++;
            int actions = 0;
            for (; actions < 64 && session.Phase == CoreDemoPhase.Battle; actions++)
            {
                if (session.CanBoard(out _) && session.TryBoard()) { row.Connection = "planned"; continue; }
                int oldHull = session.Hull, oldCrew = battle.PlayerBoardingHealth;
                if (session.Hull > 28 && session.CanRam(out _))
                {
                    if (!session.TryRam()) throw new InvalidOperationException("Legal ram rejected.");
                    row.PlayerRams++; row.EnergySpent++;
                    if (battle.Stage == NavalBattleStage.Boarding) row.Connection = "player-ram";
                    Observe(row, session, battle, oldHull, oldCrew); continue;
                }
                int handIndex = (int)BestCard.Invoke(null, new object[] { session, NavalBattleBalanceSimulator.Tactic.Boarding });
                if (handIndex < 0) break;
                int cost = session.GetBattleCardEnergyCost(battle.Hand[handIndex]);
                if (!session.PlayCard(handIndex)) throw new InvalidOperationException("Scored legal card was rejected.");
                row.Cards++; row.EnergySpent += cost;
                if (cost == 0) row.ZeroCost++;
                row.MaxCombo = Math.Max(row.MaxCombo, battle.CardsPlayedThisTurn);
                row.MaxBlock = Math.Max(row.MaxBlock, battle.PlayerBlock);
                Observe(row, session, battle, oldHull, oldCrew);
            }
            if (actions == 64 && session.Phase == CoreDemoPhase.Battle) { watchdog = true; break; }
            if (session.Phase != CoreDemoPhase.Battle) break;
            bool ram = session.EnemyWillRam;
            bool attack = ram || Regex.IsMatch(session.GetEnemyIntent(), "(?:炮击：伤害 |白刃突击：船员伤害 )\\d+");
            int h = session.Hull, c = battle.PlayerBoardingHealth;
            if (!session.EndBattleTurn()) throw new InvalidOperationException("End turn rejected.");
            if (attack) row.EnemyAttacks++;
            if (ram) row.EnemyRams++;
            if (ram && battle.Stage == NavalBattleStage.Boarding) row.Connection = "enemy-ram";
            Observe(row, session, battle, h, c);
        }
        row.Outcome = session.Phase == CoreDemoPhase.Sailing ? "win" : watchdog ? "action-watchdog" :
            session.Phase == CoreDemoPhase.Settlement ? "loss" : "turn-watchdog";
        row.Hull = session.Hull; row.Crew = battle.PlayerBoardingHealth;
        row.EnemyHull = battle.EnemyHull; row.EnemyCrew = battle.EnemyBoardingHealth;
        if (session.Cargo.Powder != 25 || session.Cargo.Cannonballs != 40)
            throw new InvalidOperationException("The recorded movement/melee deck unexpectedly used ammunition.");
        return row;
    }
    private static void Observe(Row row, CoreVoyageSession session, CoreBattleState battle, int hull, int crew)
    {
        row.MinHull = Math.Min(row.MinHull, session.Hull);
        row.HullDamage += Math.Max(0, hull - session.Hull);
        row.CrewDamage += Math.Max(0, crew - battle.PlayerBoardingHealth);
    }
    private static string Summary(List<Row> group)
    {
        Row first = group[0]; Row[] wins = group.Where(r => r.Won).ToArray();
        Func<Func<Row, int>, string> mean = selector => wins.Length == 0 ? "—" : wins.Average(selector).ToString("0.0", CultureInfo.InvariantCulture);
        return "| " + first.Ship + " | " + first.Name + " | " + wins.Length + "/" + group.Count + " | " +
            mean(r => r.Turns) + " | " + mean(r => r.Hull) + " | " + mean(r => r.Crew) + " | " +
            group.Min(r => r.Crew) + "–" + group.Max(r => r.Crew) + " | " + mean(r => r.HullDamage) + "/" + mean(r => r.CrewDamage) + " | " +
            mean(r => r.EnemyAttacks) + " | " + group.Count(r => r.Connection == "planned") + "/" + group.Count(r => r.Connection == "player-ram") +
            "/" + group.Count(r => r.Connection == "enemy-ram") + " | " + group.Max(r => r.MaxCombo) + " | " + group.Max(r => r.MaxBlock) +
            " | " + group.Count(r => r.Outcome.EndsWith("watchdog")) + " |";
    }
}
