using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Offline calibration, not a claim about optimal play or campaign progression.
// Only navigation and approach distance are bypassed. All cards, draws, fees,
// status timers, enemy turns and victory rules execute through production Core.
internal static class SimulateBoarding
{
    private static readonly MethodInfo StartBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly Dictionary<string, int> CardIndexes = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
        .ToDictionary(i => CoreVoyageSession.CardLibrary[i].Id, i => i);
    private static readonly Dictionary<string, string> EncounterRows = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> UpgradeRows = new Dictionary<string, string>();
    private static bool Observe;
    private static StreamWriter TurnTrace, ActionTrace;
    private sealed class Scenario
    {
        public string Id, Group; public int Route;
        public Scenario(string id, string group, int route) { Id = id; Group = group; Route = route; }
    }
    private sealed class Build
    {
        public int Ship, Level;
        public Build(int ship, int level) { Ship = ship; Level = level; }
    }
    private sealed class Result
    {
        public string Seed, Enemy, Group, Ship, Deck, Outcome;
        public int Level, Turns, Cards, Hp, MaxHp, EnemyHp, EnemyMaxHp, Energy, Medicine, Powder, Incoming, Damage, Guard;
        public int CycleLength, EnemyTurnsStarted, EnemyTurnsCompleted, EnemyActions, EnergyRecovered, MedicineLeft, PowderLeft;
        public int EnemyAttackHpLoss, HostileBurnHpLoss, EnemySelfBurnHpLoss, EnemyHealing, EnemySelfDamage, EnemyGuardGained;
        public int MaxEnemyBurn, MaxEnemyMana, MaxEnemyThunder, MaxEnemyCurse, MaxEnemyTide, MaxEnemyRetainedGuard, MaxEnemyBlood;
        public bool Won => Outcome == "win";
        public int CompleteCycles => EnemyTurnsCompleted / CycleLength;
        public bool QuickVictory => Won && CompleteCycles == 0;
    }
    private static readonly Dictionary<string, string[]> Decks = new Dictionary<string, string[]>
    {
        { "basic", new[] { "basic_01", "basic_01", "basic_02", "basic_02", "basic_03", "basic_04", "basic_04", "basic_05", "basic_06", "basic_06", "basic_09", "basic_10", "basic_11", "basic_13" } },
        { "fire", School("fire_01", "fire_01", "fire_02", "fire_02", "fire_03", "fire_04", "fire_05", "fire_11", "fire_13", "fire_14") },
        { "arcane", School("arcane_01", "arcane_01", "arcane_02", "arcane_02", "arcane_03", "arcane_04", "arcane_05", "arcane_06", "arcane_10", "arcane_11") },
        { "curse", School("curse_01", "curse_01", "curse_02", "curse_02", "curse_03", "curse_06", "curse_07", "curse_09", "curse_11", "curse_14") },
        { "tide", School("tide_01", "tide_01", "tide_02", "tide_03", "tide_06", "tide_07", "tide_08", "tide_10", "tide_11", "tide_14") },
        { "thunder", School("thunder_01", "thunder_01", "thunder_02", "thunder_03", "thunder_05", "thunder_06", "thunder_09", "thunder_11", "thunder_12", "thunder_13") },
        { "blood", School("blood_01", "blood_02", "blood_03", "blood_04", "blood_05", "blood_06", "blood_06", "blood_08", "blood_09", "blood_11") },
    };
    private static readonly Dictionary<string, string[]> ObservationDeck = new Dictionary<string, string[]> {
        { "defensive-probe", new[] { "basic_01", "basic_01", "basic_04", "basic_04", "basic_05", "basic_05", "basic_06", "basic_06", "basic_07", "basic_07", "basic_08", "basic_08", "basic_09", "basic_09" } }
    };
    private static string[] School(params string[] cards) => new[] { "basic_04", "basic_05", "basic_06", "basic_09" }.Concat(cards).ToArray();

    private static int Main(string[] args)
    {
        int seeds = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 16;
        if (seeds < 1 || seeds > 256) throw new ArgumentOutOfRangeException(nameof(seeds));
        string output = args.Length > 1 ? args[1] : "UnityClient/Artifacts/BoardingEnemyBalance20261015/Simulation";
        string scope = args.Length > 2 ? args[2] : "Legacy";
        string mode = args.Length > 3 ? args[3] : "Combat";
        int traceSeeds = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 1;
        if (!new[] { "Legacy", "Full", "Progression" }.Contains(scope)) throw new ArgumentException("Unknown scope");
        if (!new[] { "Combat", "Observe" }.Contains(mode)) throw new ArgumentException("Unknown mode");
        if (traceSeeds < 0 || traceSeeds > seeds) traceSeeds = Math.Min(seeds, Math.Max(0, traceSeeds));
        Observe = mode == "Observe";
        var decks = Observe ? ObservationDeck : Decks;
        Directory.CreateDirectory(output);
        var scenarios = new List<Scenario> {
            new Scenario("event_reef_coral_ambush", "ordinary1", 0), new Scenario("gold_customs", "ordinary2", 1),
            new Scenario("moon_ghost", "ordinary3", 2) };
        foreach (var boss in CoreRouteBossCatalog.All)
            scenarios.Add(new Scenario(boss.Id, "route" + (Enumerable.Range(0, CoreVoyageSession.Routes.Count).Single(i => CoreVoyageSession.Routes[i].Id == boss.RouteId) + 1),
                Enumerable.Range(0, CoreVoyageSession.Routes.Count).Single(i => CoreVoyageSession.Routes[i].Id == boss.RouteId)));
        scenarios.Add(new Scenario(CoreBattleBalance.TreasureBossId, "treasure", 2));
        foreach (var boss in CoreLegendaryWarships.All) scenarios.Add(new Scenario(boss.Id, "legendary", 2));
        var builds = scope == "Full" ? new[] { 0, 2, 7 }.SelectMany(ship => Enumerable.Range(0, 4).Select(rank => new Build(ship, rank))).ToArray()
            : new[] { new Build(0, 0), new Build(0, 1), new Build(2, 0), new Build(2, 1), new Build(2, 2), new Build(2, 3), new Build(7, 2), new Build(7, 3) };
        var results = new List<Result>();
        using (TurnTrace = Writer(output, "turns.csv"))
        using (ActionTrace = Writer(output, "actions.csv"))
        using (var file = Writer(output, "runs.csv"))
        {
            TurnTrace.WriteLine("seed,enemy,ship,requestedUpgradeRank,deck,phaseTurn,step,playerHp,playerGuard,enemyHp,enemyGuard,energy,handCount,medicine,powder,playerMana,playerThunder,playerCurse,playerTide,playerSelfBurn,enemySelfBurn,enemySelfBurnTurns,enemyMana,enemyThunder,enemyCurse,enemyTide,enemyRetainedGuard,enemyBlood,hostileBurn,hostileBurnTurns,previewActions,previewAttack,previewPiercing,previewAttackHpLoss,previewHostileBurnHpLoss,previewEnemySelfBurnHpLoss,executedActions,completeEnemyTurns,completeCycles");
            ActionTrace.WriteLine("seed,enemy,ship,requestedUpgradeRank,deck,phaseTurn,actor,actionIndex,cardId,name,energyCost,energyRecovered,medicineUsed,powderUsed,rawAttack,attack,piercing,absorbed,healthLost,guardGained,healed,selfDamage,playerHpBefore,playerHpAfter,playerGuardBefore,playerGuardAfter,enemyHpBefore,enemyHpAfter,enemyGuardBefore,enemyGuardAfter,enemySelfBurn,enemySelfBurnTurns,enemyMana,enemyThunder,enemyCurse,enemyTide,enemyRetainedGuard,enemyBlood,hostileBurn,hostileBurnTurns");
            file.WriteLine("seed,enemy,group,ship,requestedUpgradeRank,deck,outcome,turns,cards,remainingHp,maximumHp,enemyRemainingHp,enemyMaximumHp,energySpent,medicineUsed,powderUsed,incomingAttackTotal,actualHpLost,guardAbsorbed,enemyCycleLength,enemyTurnsStarted,enemyTurnsCompleted,enemyActions,completeEnemyCycles,wonBeforeFullCycle,energyRecovered,medicineLeft,powderLeft,enemyAttackHpLoss,hostileBurnHpLoss,enemySelfBurnHpLoss,enemyHealing,enemySelfDamage,enemyGuardGained,maxEnemySelfBurn,maxEnemyMana,maxEnemyThunder,maxEnemyCurse,maxEnemyTide,maxEnemyRetainedGuard,maxEnemyBlood");
            foreach (var scenario in scenarios)
            {
                foreach (var build in builds.Where(b => scope != "Progression" || IsProgression(scenario, b)))
                foreach (var deck in decks)
                for (int seed = 0; seed < seeds; seed++)
                {
                    Result row = Run("BOARDING-ENEMY-20261015-" + seed.ToString("000"), scenario, build, deck.Key, deck.Value, seed < traceSeeds);
                    results.Add(row);
                    file.WriteLine(Csv(row.Seed, row.Enemy, row.Group, row.Ship, row.Level, row.Deck, row.Outcome, row.Turns, row.Cards,
                        row.Hp, row.MaxHp, row.EnemyHp, row.EnemyMaxHp, row.Energy, row.Medicine, row.Powder, row.Incoming, row.Damage, row.Guard,
                        row.CycleLength, row.EnemyTurnsStarted, row.EnemyTurnsCompleted, row.EnemyActions, row.CompleteCycles, row.QuickVictory,
                        row.EnergyRecovered, row.MedicineLeft, row.PowderLeft, row.EnemyAttackHpLoss, row.HostileBurnHpLoss, row.EnemySelfBurnHpLoss,
                        row.EnemyHealing, row.EnemySelfDamage, row.EnemyGuardGained, row.MaxEnemyBurn, row.MaxEnemyMana, row.MaxEnemyThunder,
                        row.MaxEnemyCurse, row.MaxEnemyTide, row.MaxEnemyRetainedGuard, row.MaxEnemyBlood));
                }
                file.Flush();
                Console.WriteLine(scenario.Id + ": " + results.Count + " battles complete");
            }
        }
        WriteSummary(output, "summary.csv", results, "enemy");
        WriteSummary(output, "tiers.csv", results, "tier");
        WriteSummary(output, "progression.csv", results.Where(r => IsProgression(r.Group, r.Ship, r.Level)).ToList(), "progression");
        WriteSummary(output, "progression-schools.csv", results.Where(r => IsProgression(r.Group, r.Ship, r.Level)).ToList(), "tier");
        using (var file = Writer(output, "encounters.csv"))
        {
            file.WriteLine("id,name,group,school,boardingHp,meleeDamage,boardingGuard,cycleLength,script");
            foreach (Scenario scenario in scenarios) file.WriteLine(EncounterRows[scenario.Id]);
        }
        using (var file = Writer(output, "decks.csv"))
        {
            file.WriteLine("deck,position,cardId,name,energy");
            foreach (var deck in decks)
                for (int i = 0; i < deck.Value.Length; i++)
                {
                    var card = CoreVoyageSession.CardLibrary[CardIndexes["card_boarding_" + deck.Value[i]]];
                    file.WriteLine(Csv(deck.Key, i + 1, card.Id, card.Name, card.Energy));
                }
        }
        var buildDescriptions = new List<string>();
        using (var file = Writer(output, "builds.csv"))
        {
            file.WriteLine("ship,name,ordinaryCrew,maximumBoardingHp,requestedUpgradeRank");
            foreach (Build build in builds)
            {
                var session = new CoreVoyageSession();
                if (!session.SelectShip(build.Ship)) throw new InvalidOperationException("Metadata ship selection rejected");
                Result observedBuild = results.FirstOrDefault(r => r.Ship == session.SelectedShip.Id && r.Level == build.Level);
                if (observedBuild == null) continue;
                int health = observedBuild.MaxHp;
                file.WriteLine(Csv(session.SelectedShip.Id, session.SelectedShip.Name, session.CrewCountValue, health, build.Level));
                buildDescriptions.Add(session.SelectedShip.Name + " " + session.CrewCountValue + "普通船员/" + health + "接舷生命/请求强化" + build.Level);
            }
        }
        using (var file = Writer(output, "upgrades.csv"))
        {
            file.WriteLine("requestedUpgradeRank,cardId,name,printedEnergy,effectiveUpgradeRank,maximumUpgradeRank,utilityMaximumRank,draw,energyRecovery,damage,block,repair");
            foreach (var row in UpgradeRows.OrderBy(p => p.Key)) file.WriteLine(row.Value);
        }
        using (var file = Writer(output, "methodology.txt"))
        {
            file.WriteLine("接舷敌方难度校准 / " + DateTime.UtcNow.ToString("O") + " UTC");
            file.WriteLine("样本 " + results.Count + " 战；" + scenarios.Count + "种真实遭遇；" + decks.Count + "种14张牌组；scope=" + scope + ", mode=" + mode + "；每组 " + seeds + " 个独立确定性种子。");
            file.WriteLine("Legacy保留原8档，Full覆盖三船型各+0/+1/+2/+3共12档；Progression只跑普通1灰鸥0、普通2巡防1、普通3巡防2、路1灰鸥0/1、路2巡防1、路3巡防2、宝藏巡防2、传奇重炮2/3。builds.csv记录实际出现档位。");
            file.WriteLine("舰船（真实Core与本次入场记录）：" + string.Join("；", buildDescriptions) + "。无专家、部件、额外加成。requestedUpgradeRank是目标档位，设置时逐卡按GetCardMaximumUpgradeLevel封顶，且验证有效等级；抽牌/回能再按各自费用上限封顶，复合牌其他数值继续增长。upgrades.csv导出实际所用卡值。");
            file.WriteLine("港口恢复海盗等级10只用于解锁全部系别并合法设置牌组，不模拟获取成本。初始药品10、火药30；均有限且按实付消耗，战中不补充。每场重新开战，不是连续航程。");
            file.WriteLine("正常StartVoyage后反射真实StartBattle跳过航海；正常结束准备回合，再将海战距离设60且平行并TryBoard。双方零海战伤害/满血接舷，是接舷战生存的上限场景；未覆盖敌撞先手、航程消耗或弱补给。");
            file.WriteLine("初始手牌与每回合抽牌由Core完成；无修改手牌、牌库、血量、费用或随机数。3能量、抽5张、重复加价、暴击、六系资源与回合结算均走生产代码。士气保持真实开局值。");
            file.WriteLine("策略读取真实费用、GetBoardingCardDamageQuote、GetBattleCardEffects及GetBoardingEnemyPreview公开逐动作预告。威胁采用预告实际攻击损血、敌方点燃和己方自燃；格挡收益用尚可格挡的预告损血估值。贪心分值考虑治疗、资源、真实回能、延迟/持续伤害和生命支付；致命威胁提升防守权重。不会读取牌库，也不预测随机暴击。");
            file.WriteLine("所有选择均基于当前手牌和公开状态；不使用临时执行回滚来窥探下一张牌。六系牌组为代表性手工组合，未自动搜索最强牌组。胜率反映此有限策略，不能证明失败局不可胜或某系绝对优劣。");
            file.WriteLine("每战最多60接舷回合/每回合64张出牌，超限列为timeout并计入非胜局；不是游戏规则。summary.csv细分敌人/舰船/强化/牌组，tiers.csv按敌方档位聚合。剩余血和轮数同时提供全样本与仅胜局均值，超时独列。");
            file.WriteLine("只评估新敌人接舷循环。炮战数值与端到端航程难度须另行验证。CoreSnapshot与source-hashes.csv确定本次真实编译版本。");
            file.WriteLine("actualHpLost是逐次出牌和回合结算前后观测的生命下降合计；同次结算内的治疗会与伤害相抵，不应作为总伤害统计。胜负、最终生命、回合数和实际费用不受此统计限制影响。");
            file.WriteLine("enemyAttackHpLoss/hostileBurnHpLoss/enemySelfBurnHpLoss及动作吸收量读取生产已提交结果，区别于上述净下降。incomingAttackTotal是提交回合前的预告总攻击。energyRecovered为真实出牌后能量变化加实付费用；药品/火药统计实际支付与剩余。");
            file.WriteLine("enemyTurnsStarted要求至少一个动作执行；enemyTurnsCompleted要求当轮脚本全部动作执行，DOT先致死空动作不计完整回合；completeEnemyCycles=完整敌方回合数/真实循环长度向下取整。wonBeforeFullCycle记录自然速杀，终局玩家回合不冒充敌方已执行回合。");
            file.WriteLine("turns.csv与actions.csv追踪前" + traceSeeds + "个种子，记录真实玩家出牌、公开回合前后状态、已提交敌方逐动作资源快照；不会根据预告伪造实际结果。");
            file.WriteLine("Observe独立防守观察：合法基础刺击/两种盾/两种治疗/抽牌/回能各2张，正常抽牌和有限资源；防守与治疗优先，每轮最多一次低优先级刺击，且保留敌方至少35%生命。最多观察2完整敌方循环，未达到就如实记录loss/enemy-defeated/timeout；达标为observed。它不是正常七牌组胜率，禁止与Combat合并。");
        }
        Console.WriteLine("DONE: " + results.Count + " battles, " + results.Count(r => r.Won) + " wins, " + results.Count(r => r.Outcome == "timeout") + " timeouts.");
        return 0;
    }

    private static bool IsProgression(Scenario scenario, Build build)
    {
        if (scenario.Group == "ordinary1") return build.Ship == 0 && build.Level == 0;
        if (scenario.Group == "route1") return build.Ship == 0 && build.Level <= 1;
        if (scenario.Group == "ordinary2" || scenario.Group == "route2") return build.Ship == 2 && build.Level == 1;
        if (scenario.Group == "legendary") return build.Ship == 7 && build.Level >= 2;
        return build.Ship == 2 && build.Level == 2;
    }

    private static bool IsProgression(string group, string shipId, int rank)
    {
        int index = Enumerable.Range(0, CoreVoyageSession.Ships.Count).Single(i => CoreVoyageSession.Ships[i].Id == shipId);
        return IsProgression(new Scenario("", group, 0), new Build(index, rank));
    }

    private static Result Run(string seed, Scenario scenario, Build build, string deckName, string[] keys, bool trace)
    {
        var s = new CoreVoyageSession();
        if (!s.SelectShip(build.Ship)) throw new InvalidOperationException("Ship selection rejected");
        s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
        s.SelectRoute(scenario.Route);
        if (s.SelectedRouteIndex != scenario.Route) throw new InvalidOperationException("Route selection rejected");
        s.SetSeed(seed);
        s.ConfigureDepartureCrew(s.CrewCountValue, 0);
        s.ConfigureCrewCapabilities(false, false, false, false, false, false);
        s.ConfigureSpecialCardSources(Array.Empty<string>(), Array.Empty<string>());
        s.ConfigureComponentBonuses(0, 0, 0, 0, 0);
        s.ConfigureBattleEquipment(1f, 1f);
        s.ConfigureCardUpgrades(CoreVoyageSession.CardLibrary.Where(c => !CoreVoyageSession.IsSpecialBattleCard(c.Id))
            .ToDictionary(c => c.Id, c => Math.Min(build.Level, s.GetCardMaximumUpgradeLevel(c.Id))));
        if (!s.SetBattleDeck(NavalBattleStage.Boarding, keys.Select(k => CardIndexes["card_boarding_" + k]).ToArray()))
            throw new InvalidOperationException("Illegal fixture deck: " + deckName);
        s.Cargo.Water = s.Cargo.Food = 10;
        s.Cargo.Medicine = 10; s.Cargo.Powder = 30;
        s.Cargo.Wood = s.Cargo.Cannonballs = s.Cargo.Beer = s.Cargo.Iron = s.Cargo.Cloth = 0;
        s.StartVoyage();
        if (s.Phase != CoreDemoPhase.Sailing) throw new InvalidOperationException("Voyage departure rejected");
        CoreEncounter encounter = CoreRouteBossCatalog.Find(scenario.Id)?.CreateEncounter()
            ?? CoreLegendaryWarships.Find(scenario.Id)?.CreateEncounter()
            ?? s.Map.Nodes.Select(n => n.Encounter).FirstOrDefault(e => e.Id == scenario.Id);
        if (encounter == null) throw new InvalidOperationException("Missing real encounter: " + scenario.Id);
        StartBattle.Invoke(s, new object[] { encounter, CoreBattleBalance.Classify(scenario.Id) != CoreBossKind.None });
        if (s.Battle.Stage != NavalBattleStage.Preparation || !s.EndBattleTurn()) throw new InvalidOperationException("Preparation failed");
        s.Battle.DistanceMeters = 60f; s.Battle.Alignment = NavalBattleAlignment.Parallel;
        if (!s.TryBoard()) throw new InvalidOperationException("Real boarding entry rejected");
        var b = s.Battle;
        if (b.Hand.Count != 5 || b.Energy != 3 || b.PlayerBoardingHealth != b.PlayerMaxBoardingHealth || b.EnemyBoardingHealth != b.EnemyMaxBoardingHealth)
            throw new InvalidOperationException("Fixture did not enter full-health, 5-card, 3-energy boarding");
        if (!EncounterRows.ContainsKey(scenario.Id))
        {
            CoreBattleProfile profile = CoreBattleBalance.Resolve(encounter, scenario.Route, CoreBattleBalance.Classify(scenario.Id) != CoreBossKind.None);
            EncounterRows.Add(scenario.Id, Csv(encounter.Id, encounter.Name, scenario.Group, b.BoardingEnemy.Deck.SchoolId,
                b.EnemyMaxBoardingHealth, profile.MeleeDamage, profile.BoardingGuard, b.BoardingEnemy.Deck.Turns.Count,
                string.Join(" / ", b.BoardingEnemy.Deck.Turns.Select(t => t.Name + ":" + string.Join("+", t.Cards.Select(c => c.Id))))));
        }
        foreach (string key in keys.Distinct())
        {
            int index = CardIndexes["card_boarding_" + key];
            var card = s.GetBattleCard(index);
            int maximum = s.GetCardMaximumUpgradeLevel(card.Id);
            if (card.UpgradeLevel != Math.Min(build.Level, maximum)) throw new InvalidOperationException("Illegal effective upgrade: " + card.Id);
            var effects = s.GetBattleCardEffects(card, NavalBattleStage.Boarding);
            UpgradeRows[build.Level + "|" + card.Id] = Csv(build.Level, card.Id, card.Name, card.Energy, card.UpgradeLevel,
                maximum, CoreBattleUpgradeBalance.UtilityMaximumLevel(card), card.Draw, s.GetBattleCardEnergyRecovery(index), effects.Damage, effects.Block, effects.Repair);
        }
        var row = new Result { Seed = seed, Enemy = scenario.Id, Group = scenario.Group, Ship = s.SelectedShip.Id, Level = build.Level,
            Deck = deckName, MaxHp = b.PlayerMaxBoardingHealth, EnemyMaxHp = b.EnemyMaxBoardingHealth, Outcome = "timeout", CycleLength = b.BoardingEnemy.Deck.Turns.Count };
        for (int turn = 1; turn <= 60 && s.Phase == CoreDemoPhase.Battle; turn++)
        {
            row.Turns = turn;
            int phaseTurn = b.PhaseTurn;
            if (trace) TraceTurn(s, b, row, phaseTurn, "start", s.GetBoardingEnemyPreview());
            bool probePlayed = false;
            for (int action = 0; action < 64 && s.Phase == CoreDemoPhase.Battle; action++)
            {
                int best = -1; double score = .01;
                CoreBoardingEnemyPreview preview = s.GetBoardingEnemyPreview();
                for (int slot = 0; slot < b.Hand.Count; slot++)
                {
                    if (!s.CanPlayCard(slot)) continue;
                    double value = Score(s, slot, preview, probePlayed);
                    if (value > score) { score = value; best = slot; }
                }
                if (best < 0) break;
                var card = s.GetBattleCard(b.Hand[best]);
                int fee = s.GetBoardingHandEnergyCost(best), medicine = s.Cargo.Medicine, powder = s.Cargo.Powder, hp = b.PlayerBoardingHealth;
                int energy = b.Energy, playerGuard = b.PlayerBlock, enemyHp = b.EnemyBoardingHealth, enemyGuard = b.EnemyBlock;
                if (!s.PlayCard(best)) throw new InvalidOperationException("Scored legal card rejected");
                if (card.BoardingSpec.Key == "basic_01") probePlayed = true;
                row.Cards++; row.Energy += fee;
                int recovered = Math.Max(0, b.Energy - energy + fee);
                row.EnergyRecovered += recovered;
                row.Medicine += Math.Max(0, medicine - s.Cargo.Medicine); row.Powder += Math.Max(0, powder - s.Cargo.Powder);
                row.Damage += Math.Max(0, hp - b.PlayerBoardingHealth);
                TrackResources(row, b.BoardingEnemy);
                if (trace) ActionTrace.WriteLine(Csv(row.Seed, row.Enemy, row.Ship, row.Level, row.Deck, phaseTurn, "player", action + 1,
                    card.Id, card.Name, fee, recovered, Math.Max(0, medicine - s.Cargo.Medicine), Math.Max(0, powder - s.Cargo.Powder),
                    "", "", "", "", "", "", "", "", hp, b.PlayerBoardingHealth, playerGuard, b.PlayerBlock, enemyHp, b.EnemyBoardingHealth,
                    enemyGuard, b.EnemyBlock, b.BoardingEnemy.SelfBurnDamage, b.BoardingEnemy.SelfBurnTurns, b.BoardingEnemy.Mana,
                    b.BoardingEnemy.ThunderChain, b.BoardingEnemy.CurseMarks, b.BoardingEnemy.TidePower, b.BoardingEnemy.RetainedGuard,
                    b.BoardingEnemy.BloodLoss, b.BoardingEnemy.PlayerBurnDamage, b.BoardingEnemy.PlayerBurnTurns));
                if (action == 63) throw new InvalidOperationException("Per-turn action watchdog reached");
            }
            if (s.Phase != CoreDemoPhase.Battle)
            {
                if (trace) TraceTurn(s, b, row, phaseTurn, "player-terminal", null);
                break;
            }
            var committedPreview = s.GetBoardingEnemyPreview();
            row.Incoming += committedPreview.AttackDamage;
            if (trace) TraceTurn(s, b, row, phaseTurn, "before-enemy", committedPreview);
            int expectedActions = b.BoardingEnemy.Deck.TurnAt(phaseTurn).Cards.Count;
            int before = b.PlayerBoardingHealth;
            if (!s.EndBattleTurn()) throw new InvalidOperationException("End turn rejected");
            row.Damage += Math.Max(0, before - b.PlayerBoardingHealth);
            var state = b.BoardingEnemy;
            if (state.LastActions.Count > 0) row.EnemyTurnsStarted++;
            if (state.LastActions.Count == expectedActions) row.EnemyTurnsCompleted++;
            row.EnemyActions += state.LastActions.Count;
            row.HostileBurnHpLoss += state.LastBurnHealthLost;
            row.EnemySelfBurnHpLoss += state.LastSelfBurnHealthLost;
            for (int action = 0; action < state.LastActions.Count; action++)
            {
                var result = state.LastActions[action];
                row.EnemyAttackHpLoss += result.HealthLost; row.Guard += result.AbsorbedDamage;
                row.EnemyHealing += result.Healed; row.EnemySelfDamage += result.SelfDamage; row.EnemyGuardGained += result.GuardGained;
                TrackResources(row, result.ResourcesAfter);
                if (trace) TraceAction(row, phaseTurn, action + 1, result);
            }
            TrackResources(row, state);
            if (trace) TraceTurn(s, b, row, phaseTurn, "after-enemy", null);
            if (Observe && row.CompleteCycles >= 2) break;
        }
        row.Hp = b.PlayerBoardingHealth; row.EnemyHp = b.EnemyBoardingHealth;
        row.MedicineLeft = s.Cargo.Medicine; row.PowderLeft = s.Cargo.Powder;
        row.Outcome = row.Hp <= 0 ? "loss" : row.EnemyHp <= 0 ? (Observe ? "enemy-defeated" : "win") : Observe && row.CompleteCycles >= 2 ? "observed" : "timeout";
        return row;
    }

    private static double Score(CoreVoyageSession s, int slot, CoreBoardingEnemyPreview preview, bool probePlayed)
    {
        var b = s.Battle; var card = s.GetBattleCard(b.Hand[slot]); string k = card.BoardingSpec.Key;
        var effect = s.GetBattleCardEffects(card, NavalBattleStage.Boarding);
        int fee = s.GetBoardingHandEnergyCost(slot), attack = preview.AttackDamage;
        int fire = b.Boarding.SelfBurn.Sum(x => x.Amount) + preview.PlayerBurnDamage;
        int need = preview.Actions.Sum(a => Math.Max(0, a.HealthLost - a.PiercingDamage));
        int lost = preview.PlayerHealthLoss + fire;
        double defense = b.PlayerBoardingHealth <= lost ? 9 : b.PlayerBoardingHealth <= lost * 2 ? 3.3 : b.PlayerBoardingHealth < b.PlayerMaxBoardingHealth * .55 ? 2.1 : 1.25;
        double scale = (double)CoreBattleUpgradeBalance.MultiplierForLevel(card.UpgradeLevel);
        bool delayed = card.BoardingSpec.IsDelayed;
        bool ignoresGuard = k == "curse_11" || k == "thunder_14";
        int raw = s.GetBoardingCardDamageQuote(card);
        if (Observe && k == "basic_01")
            return !probePlayed && b.EnemyBoardingHealth - Math.Max(0, raw * 2 - b.EnemyBlock) > b.EnemyMaxBoardingHealth * .35 ? .02 : -100;
        if (Observe) defense = Math.Max(3, defense);
        if (!delayed && raw - (ignoresGuard ? 0 : b.EnemyBlock) >= b.EnemyBoardingHealth && raw > 0) return 100000 / (fee + .5);
        double damage = raw;
        if (card.BoardingSpec.IsAttack && !delayed) damage *= 1 + s.BoardingCriticalChancePercent / 100d;
        damage = Math.Max(0, damage - (ignoresGuard ? 0 : b.EnemyBlock));
        if (delayed) damage *= .8;
        double score = damage * 1.3 + Math.Min(need, effect.Block) * defense;
        int healing = Math.Min(b.PlayerMaxBoardingHealth - b.PlayerBoardingHealth, effect.Repair);
        score += healing * (b.PlayerBoardingHealth < b.PlayerMaxBoardingHealth * .6 ? defense + .6 : .8);
        if (card.Draw > 0 && b.Hand.Count < 8) score += card.Draw * (b.Energy > fee ? 4.0 : 1.0);
        int recovery = s.GetBattleCardEnergyRecovery(b.Hand[slot]);
        if (recovery > 0)
            score += (b.Hand.Count > 1 || card.Draw > 0) && recovery > fee ? recovery * 14 : -100;
        double manaValue = 4.5 * scale, markValue = 3.3 * scale;
        switch (k)
        {
            case "basic_07": score += Math.Min(b.PlayerMaxBoardingHealth - b.PlayerBoardingHealth - healing, effect.Repair) * .8; break;
            case "fire_02": score += 8 * scale * 1.2; break;
            case "fire_03": score += 16 * scale * 1.2; break;
            case "fire_04": score += 22 * scale; break;
            case "fire_11": score -= 6 * defense; break;
            case "fire_13": score += (need > 0 ? Math.Min(8, (b.Boarding.SelfBurn.Sum(x => x.Amount) + 2) * 2) : 0) - 4 * defense; break;
            case "fire_14": score -= 6 * defense; break;
            case "arcane_01": case "arcane_02": score += Math.Min(1, 3 - b.Schools.AvailableMana) * manaValue; break;
            case "arcane_03": score += Math.Max(0, 3 - b.Schools.AvailableMana) * manaValue; break;
            case "curse_01": case "curse_03": score += Math.Min(1, 6 - b.Boarding.CurseMarks) * markValue; break;
            case "curse_02": score += Math.Min(3, 6 - b.Boarding.CurseMarks) * markValue; break;
            case "curse_07": score += Math.Min(attack / 2, (3 + b.Boarding.CurseMarks) * scale) * defense; break;
            case "curse_09": score += Math.Min(10 * scale, b.Boarding.CurseMarks * 2 * scale) * 1.7; break;
            case "tide_01": case "tide_02": case "tide_03": score += Math.Min(need, effect.Block) * .9; break;
            case "tide_10": score += Math.Min(attack / 2, 3 * scale) * defense; break;
            case "thunder_01": case "thunder_02": case "thunder_03": case "thunder_05": case "thunder_11": score += b.Energy > fee ? 3 * scale : 0; break;
            case "thunder_06": score += b.Hand.Any(i => s.GetBattleCard(i).SchoolId == "thunder" && s.GetBattleCard(i).BoardingSpec.IsAttack && s.GetBattleCardEnergyCost(i) <= b.Energy - fee) ? 8 * scale : 0; break;
            case "thunder_12": score += Math.Min(attack / 2, (2 + b.Schools.ThunderChain / 2) * scale) * defense; break;
            case "blood_01": case "blood_03": case "blood_04": score -= 3 * defense; break;
            case "blood_02": score -= 4 * defense; break;
            case "blood_05": score -= 3 * defense; break;
            case "blood_06": score += Math.Min(b.PlayerMaxBoardingHealth - b.PlayerBoardingHealth, 2 * scale) * defense; break;
            case "blood_08": score += lost > fire ? 8 * scale * 1.8 : 8 * scale * .6; break;
        }
        // Avoid deliberately paying life into an already fatal attack unless lethal.
        int lifeCost = k == "blood_02" ? 4 : k == "blood_01" || k == "blood_03" || k == "blood_04" || k == "blood_05" ? 3 : k == "fire_14" ? 2 : 0;
        if (lifeCost > 0 && b.PlayerBoardingHealth - lifeCost <= lost && effect.Block == 0 && b.Energy <= fee) score -= 100;
        return score / (fee + .65);
    }

    private static void TrackResources(Result row, CoreBoardingEnemyState state)
    {
        if (state == null) return;
        row.MaxEnemyBurn = Math.Max(row.MaxEnemyBurn, state.SelfBurnDamage);
        row.MaxEnemyMana = Math.Max(row.MaxEnemyMana, state.Mana);
        row.MaxEnemyThunder = Math.Max(row.MaxEnemyThunder, state.ThunderChain);
        row.MaxEnemyCurse = Math.Max(row.MaxEnemyCurse, state.CurseMarks);
        row.MaxEnemyTide = Math.Max(row.MaxEnemyTide, state.TidePower);
        row.MaxEnemyRetainedGuard = Math.Max(row.MaxEnemyRetainedGuard, state.RetainedGuard);
        row.MaxEnemyBlood = Math.Max(row.MaxEnemyBlood, state.BloodLoss);
    }

    private static void TraceTurn(CoreVoyageSession session, CoreBattleState battle, Result row, int phaseTurn, string step, CoreBoardingEnemyPreview preview)
    {
        var enemy = battle.BoardingEnemy;
        TurnTrace.WriteLine(Csv(row.Seed, row.Enemy, row.Ship, row.Level, row.Deck, phaseTurn, step,
            battle.PlayerBoardingHealth, battle.PlayerBlock, battle.EnemyBoardingHealth, battle.EnemyBlock, battle.Energy, battle.Hand.Count,
            session.Cargo.Medicine, session.Cargo.Powder, battle.Schools.AvailableMana, battle.Schools.ThunderChain,
            battle.Boarding.CurseMarks, battle.Boarding.TidePower, battle.Boarding.SelfBurn.Sum(s => s.Amount),
            enemy.SelfBurnDamage, enemy.SelfBurnTurns, enemy.Mana, enemy.ThunderChain, enemy.CurseMarks, enemy.TidePower,
            enemy.RetainedGuard, enemy.BloodLoss, enemy.PlayerBurnDamage, enemy.PlayerBurnTurns,
            preview?.Actions.Count, preview?.AttackDamage, preview?.PiercingDamage, preview?.PlayerHealthLoss,
            preview?.PlayerBurnDamage, preview?.EnemySelfBurnDamage, step == "after-enemy" ? enemy.LastActions.Count : 0,
            row.EnemyTurnsCompleted, row.CompleteCycles));
    }

    private static void TraceAction(Result row, int phaseTurn, int index, CoreBoardingEnemyActionResult action)
    {
        var state = action.ResourcesAfter;
        ActionTrace.WriteLine(Csv(row.Seed, row.Enemy, row.Ship, row.Level, row.Deck, phaseTurn, "enemy", index, action.CardId, action.Name,
            "", "", "", "", action.RawAttack, action.AttackDamage, action.PiercingDamage, action.AbsorbedDamage, action.HealthLost,
            action.GuardGained, action.Healed, action.SelfDamage, action.PlayerHealthBefore, action.PlayerHealthAfter,
            action.PlayerGuardBefore, action.PlayerGuardAfter, action.EnemyHealthBefore, action.EnemyHealthAfter,
            action.EnemyGuardBefore, action.EnemyGuardAfter, state?.SelfBurnDamage, state?.SelfBurnTurns, state?.Mana, state?.ThunderChain,
            state?.CurseMarks, state?.TidePower, state?.RetainedGuard, state?.BloodLoss, state?.PlayerBurnDamage, state?.PlayerBurnTurns));
    }

    private static void WriteSummary(string output, string name, List<Result> rows, string grouping)
    {
        using (var file = Writer(output, name))
        {
            file.WriteLine("enemyOrTier,ship,requestedUpgradeRank,deck,samples,wins,winPercent,turnsAll,turnsWins,remainingHpAll,remainingHpWins,remainingHpPercentWins,energySpentAll,medicineUsedAll,powderUsedAll,timeouts,enemyTurnsCompletedAll,completeEnemyCyclesAll,winsBeforeFullCycle,quickVictoryPercentAll,quickVictoryPercentWins,energyRecoveredAll,enemyAttackHpLossAll,hostileBurnHpLossAll,enemySelfBurnHpLossAll,enemyHealingAll,guardAbsorbedAll,medicineExhaustedPercent,observedTwoCycles,observationPercent,remainingHpPercentAll");
            foreach (var group in rows.GroupBy(r => (grouping == "enemy" ? r.Enemy : r.Group) + "|" + r.Ship + "|" + r.Level + (grouping == "progression" ? "" : "|" + r.Deck)))
            {
                var r = group.First(); var wins = group.Where(x => x.Won).ToArray();
                file.WriteLine(Csv(grouping == "enemy" ? r.Enemy : r.Group, r.Ship, r.Level, grouping == "progression" ? "all" : r.Deck, group.Count(), wins.Length,
                    F(100d * wins.Length / group.Count()), F(group.Average(x => x.Turns)), wins.Length == 0 ? "" : F(wins.Average(x => x.Turns)),
                    F(group.Average(x => x.Hp)), wins.Length == 0 ? "" : F(wins.Average(x => x.Hp)), wins.Length == 0 ? "" : F(wins.Average(x => 100d * x.Hp / x.MaxHp)),
                    F(group.Average(x => x.Energy)), F(group.Average(x => x.Medicine)), F(group.Average(x => x.Powder)), group.Count(x => x.Outcome == "timeout"),
                    F(group.Average(x => x.EnemyTurnsCompleted)), F(group.Average(x => x.CompleteCycles)), group.Count(x => x.QuickVictory),
                    F(100d * group.Count(x => x.QuickVictory) / group.Count()), wins.Length == 0 ? "" : F(100d * wins.Count(x => x.QuickVictory) / wins.Length),
                    F(group.Average(x => x.EnergyRecovered)), F(group.Average(x => x.EnemyAttackHpLoss)), F(group.Average(x => x.HostileBurnHpLoss)),
                    F(group.Average(x => x.EnemySelfBurnHpLoss)), F(group.Average(x => x.EnemyHealing)), F(group.Average(x => x.Guard)),
                    F(100d * group.Count(x => x.MedicineLeft == 0) / group.Count()), group.Count(x => x.Outcome == "observed"),
                    F(100d * group.Count(x => x.Outcome == "observed") / group.Count()), F(group.Average(x => 100d * x.Hp / x.MaxHp))));
            }
        }
    }
    private static string F(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static StreamWriter Writer(string directory, string name) => new StreamWriter(Path.Combine(directory, name), false, new UTF8Encoding(true));
    private static string Csv(params object[] values) => string.Join(",", values.Select(v => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""));
}
