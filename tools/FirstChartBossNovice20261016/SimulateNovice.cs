using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Policies inspect only the current hand and visible enemy intent, never future
// draws or future enemy turns. Keep the actual starter deck and scarce medicine.
internal static class SimulateNovice
{
    private static readonly MethodInfo StartBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly string[] BossIds = { "boss_route_shattered_sail_reef", "boss_route_shattered_sail_reef_tide" };
    private static readonly string[] Policies = { "balanced", "conservative", "aggressive" };
    private static StreamWriter Turns, Cards;
    private sealed class Result
    {
        public string Boss, Policy, Outcome;
        public int Seed, EntryPercent, EntryHp, MaxHp, EnemyMaxHp, Turns, Hp, EnemyHp, MedicineUsed, Cards, Energy, Incoming, Lost;
    }
    private static int Main(string[] args)
    {
        string output = args[0]; int seeds = args.Length > 1 ? int.Parse(args[1]) : 30;
        if (seeds < 1 || seeds > 500) throw new ArgumentOutOfRangeException(nameof(seeds));
        Directory.CreateDirectory(output); var rows = new List<Result>(); ExportStarter(output);
        using (Turns = Writer(output, "turns.csv"))
        using (Cards = Writer(output, "actions.csv"))
        using (var runs = Writer(output, "runs.csv"))
        {
            Turns.WriteLine("boss,policy,entryPercent,seed,turn,hpBefore,guardBefore,enemyHpBefore,enemyGuardBefore,previewAttack,previewHpLost,enemyEnergy,actualAttack,actualGuard,actualHeal,actualHpLost,hpAfter,enemyHpAfter,medicineLeft");
            Cards.WriteLine("boss,policy,entryPercent,seed,turn,action,card,energyCost,hpBefore,hpAfter,enemyHpBefore,enemyHpAfter,guardBefore,guardAfter,medicineLeft");
            runs.WriteLine("boss,policy,entryPercent,seed,outcome,turns,entryHp,maxHp,remainingHp,enemyMaxHp,enemyRemainingHp,medicineUsed,cards,energySpent,incomingAttack,actualHpLost");
            foreach (string boss in BossIds) foreach (int entry in new[] { 100, 75 }) foreach (string policy in Policies)
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    var r = Run(boss, policy, entry, seed, seed < 3); rows.Add(r);
                    runs.WriteLine(Csv(r.Boss, r.Policy, r.EntryPercent, r.Seed, r.Outcome, r.Turns, r.EntryHp, r.MaxHp,
                        r.Hp, r.EnemyMaxHp, r.EnemyHp, r.MedicineUsed, r.Cards, r.Energy, r.Incoming, r.Lost));
                }
                Console.WriteLine(boss + " " + policy + " " + entry + "% complete.");
            }
        }
        using (var summary = Writer(output, "summary.csv"))
        {
            summary.WriteLine("boss,policy,entryPercent,runs,wins,winPercent,averageRemainingHp,averageWinningHp,minWinningHp,averageTurns,averageMedicineUsed,timeouts");
            foreach (var group in rows.GroupBy(r => new { r.Boss, r.Policy, r.EntryPercent }))
            {
                var wins = group.Where(r => r.Outcome == "win").ToArray();
                string row = Csv(group.Key.Boss, group.Key.Policy, group.Key.EntryPercent, group.Count(), wins.Length,
                    F(wins.Length * 100d / group.Count()), F(group.Average(r => r.Hp)), wins.Length > 0 ? F(wins.Average(r => r.Hp)) : "",
                    wins.Length > 0 ? wins.Min(r => r.Hp).ToString() : "", F(group.Average(r => r.Turns)),
                    F(group.Average(r => r.MedicineUsed)), group.Count(r => r.Outcome == "timeout"));
                summary.WriteLine(row); Console.WriteLine(row);
            }
        }
        Console.WriteLine("DONE: " + rows.Count + " battles, " + rows.Count(r => r.Outcome == "win") + " wins, " + rows.Count(r => r.Outcome == "timeout") + " timeouts.");
        return 0;
    }
    private static CoreVoyageSession NewStarter()
    {
        var s = new CoreVoyageSession();
        s.ConfigureDepartureCrew(s.CrewCountValue, 0);
        s.ConfigureCrewCapabilities(false, false, false, false, false, false);
        s.ConfigureSpecialCardSources(Array.Empty<string>(), Array.Empty<string>());
        s.ConfigureComponentBonuses(0, 0, 0, 0, 0); s.ConfigureBattleEquipment(1f, 1f);
        if (s.SelectedShipIndex != 0 || s.SelectedRouteIndex != 0 || s.Cargo.Medicine != 2 || s.GetBattleDeck(NavalBattleStage.Boarding).Count != 12)
            throw new InvalidOperationException("Starter baseline changed; review simulation.");
        foreach (int index in s.GetBattleDeck(NavalBattleStage.Boarding))
            if (s.GetBattleCard(index).UpgradeLevel != 0) throw new InvalidOperationException("Starter card unexpectedly upgraded.");
        return s;
    }
    private static void ExportStarter(string output)
    {
        var s = NewStarter();
        using (var file = Writer(output, "starter-deck.csv"))
        {
            file.WriteLine("libraryIndex,id,key,name,copies,upgrade,energy,damage,guard,heal,draw");
            foreach (var group in s.GetBattleDeck(NavalBattleStage.Boarding).GroupBy(i => i))
            {
                var c = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(group.Key));
                file.WriteLine(Csv(group.Key, c.Id, c.BoardingSpec?.Key, c.Name, group.Count(), c.UpgradeLevel, c.Energy, c.Damage, c.Block, c.Repair, c.Draw));
            }
        }
        File.WriteAllText(Path.Combine(output, "baseline.txt"),
            "New CoreVoyageSession default ship and original 12-card boarding deck; no SetBattleDeck call. All cards unupgraded.\n" +
            "Full ordinary crew; no specialists or equipment bonuses. Initial cargo untouched, including 2 medicines.\n" +
            "Only navigation, naval approach and preparation are bypassed. Real TryBoard executes at orthogonal 100m contact with matching headings.\n" +
            "Full health or 75% current boarding HP models prior wounds; maximum HP and boss HP remain production values.\n" +
            "Three fixed one-step policies use only present hand, basic card effects and visible enemy intent. No lookahead, optimized deck, unlimited medicine or future knowledge.\n" +
            "Traces cover first 3 seeds per scenario. All scenarios share the seed prefix for comparable shuffles and critical rolls.\n", new UTF8Encoding(false));
    }
    private static Result Run(string boss, string policy, int entryPercent, int seed, bool trace)
    {
        var s = NewStarter(); s.SetSeed("FIRST-CHART-NOVICE-20261016-" + seed.ToString("000")); s.StartVoyage();
        if (s.Phase != CoreDemoPhase.Sailing) throw new InvalidOperationException("Starter departure rejected.");
        StartBattle.Invoke(s, new object[] { CoreRouteBossCatalog.Find(boss).CreateEncounter(), true });
        if (s.Battle.Stage != NavalBattleStage.Preparation || !s.EndBattleTurn()) throw new InvalidOperationException("Preparation failed.");
        s.Battle.DistanceMeters = 100f;
        typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
        s.Battle.Alignment = NavalBattleAlignment.Parallel;
        if (!s.TryBoard()) throw new InvalidOperationException("Real boarding entry rejected.");
        var b = s.Battle;
        if (b.PlayerMaxBoardingHealth != 60 || b.Hand.Count != 5 || b.Energy != 3 || s.Cargo.Medicine != 2)
            throw new InvalidOperationException("Starter boarding baseline changed.");
        b.PlayerBoardingHealth = b.PlayerMaxBoardingHealth * entryPercent / 100;
        var r = new Result { Boss = boss, Policy = policy, EntryPercent = entryPercent, Seed = seed, EntryHp = b.PlayerBoardingHealth,
            MaxHp = b.PlayerMaxBoardingHealth, EnemyMaxHp = b.EnemyMaxBoardingHealth };
        for (int turn = 1; turn <= 60 && s.Phase == CoreDemoPhase.Battle; turn++)
        {
            r.Turns = turn;
            for (int action = 0; action < 32 && s.Phase == CoreDemoPhase.Battle; action++)
            {
                int chosen = -1; double best = .01; var preview = s.GetBoardingEnemyPreview();
                for (int slot = 0; slot < b.Hand.Count; slot++)
                {
                    if (!s.CanPlayCard(slot)) continue;
                    double score = Score(s, slot, preview, policy);
                    if (score > best) { best = score; chosen = slot; }
                }
                if (chosen < 0) break;
                string name = s.GetBattleCard(b.Hand[chosen]).Name;
                int fee = s.GetBoardingHandEnergyCost(chosen), hp = b.PlayerBoardingHealth, enemyHp = b.EnemyBoardingHealth, guard = b.PlayerBlock, medicine = s.Cargo.Medicine;
                if (!s.PlayCard(chosen)) throw new InvalidOperationException("Chosen legal card rejected.");
                r.Cards++; r.Energy += fee; r.MedicineUsed += Math.Max(0, medicine - s.Cargo.Medicine);
                if (trace) Cards.WriteLine(Csv(boss, policy, entryPercent, seed, turn, action + 1, name, fee, hp, b.PlayerBoardingHealth,
                    enemyHp, b.EnemyBoardingHealth, guard, b.PlayerBlock, s.Cargo.Medicine));
                if (action == 31) throw new InvalidOperationException("Action watchdog reached.");
            }
            if (s.Phase != CoreDemoPhase.Battle) break;
            var intent = s.GetBoardingEnemyPreview();
            int hpBefore = b.PlayerBoardingHealth, guardBefore = b.PlayerBlock, enemyBefore = b.EnemyBoardingHealth, enemyGuard = b.EnemyBlock;
            int feeTotal = b.BoardingEnemy.Deck.TurnAt(b.PhaseTurn).TotalEnergy; r.Incoming += intent.AttackDamage;
            if (!s.EndBattleTurn()) throw new InvalidOperationException("End turn rejected.");
            int loss = Math.Max(0, hpBefore - b.PlayerBoardingHealth); r.Lost += loss;
            if (trace) Turns.WriteLine(Csv(boss, policy, entryPercent, seed, turn, hpBefore, guardBefore, enemyBefore, enemyGuard,
                intent.AttackDamage, intent.PlayerHealthLoss, feeTotal, b.BoardingEnemy.LastActions.Sum(a => a.AttackDamage),
                b.BoardingEnemy.LastActions.Sum(a => a.GuardGained), b.BoardingEnemy.LastActions.Sum(a => a.Healed),
                loss, b.PlayerBoardingHealth, b.EnemyBoardingHealth, s.Cargo.Medicine));
        }
        r.Hp = b.PlayerBoardingHealth; r.EnemyHp = b.EnemyBoardingHealth;
        r.Outcome = r.Hp <= 0 ? "loss" : r.EnemyHp <= 0 ? "win" : "timeout"; return r;
    }
    private static double Score(CoreVoyageSession s, int slot, CoreBoardingEnemyPreview preview, string policy)
    {
        var b = s.Battle; int index = b.Hand[slot]; var c = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(index));
        var effect = s.GetBattleCardEffects(c, NavalBattleStage.Boarding); int fee = s.GetBoardingHandEnergyCost(slot);
        int damage = Math.Max(0, s.GetBoardingCardDamageQuote(c) - b.EnemyBlock);
        if (damage >= b.EnemyBoardingHealth && damage > 0) return 10000d / (fee + .75);
        int recovery = s.GetBattleCardEnergyRecovery(index);
        if (recovery > 0) return recovery > fee && b.Hand.Count > 1 ? 80 : -1;
        int incoming = preview.PlayerHealthLoss;
        int guard = Math.Min(Math.Max(0, incoming - preview.PiercingDamage), effect.Block);
        int missing = b.PlayerMaxBoardingHealth - b.PlayerBoardingHealth, heal = Math.Min(missing, effect.Repair);
        bool danger = b.PlayerBoardingHealth <= incoming + 8;
        double dw = policy == "aggressive" ? 1.45 : policy == "conservative" ? .85 : 1.1;
        double gw = policy == "aggressive" ? 1.0 : policy == "conservative" ? 2.4 : 1.65;
        double hw = policy == "aggressive" ? 1.05 : policy == "conservative" ? 2.2 : 1.6;
        if (danger) { gw += 2.3; hw += 1.8; }
        if (s.Cargo.Medicine > 0 && missing < Math.Min(4, effect.Repair) && !danger) heal = 0;
        double draw = c.Draw * (b.Energy > fee ? 2.4 : .2);
        return (damage * dw + guard * gw + heal * hw + draw) / (fee + .75);
    }
    private static StreamWriter Writer(string output, string name) => new StreamWriter(Path.Combine(output, name), false, new UTF8Encoding(false));
    private static string F(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Csv(params object[] fields) => string.Join(",", fields.Select(value => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""));
}
