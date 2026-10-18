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
internal static class SimulateEarlyCharts
{
    private static readonly MethodInfo StartBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
    private static string[] BossIds = { "boss_route_shattered_sail_reef", "boss_route_shattered_sail_reef_tide" };
    private static readonly string[] Policies = { "balanced", "conservative", "aggressive" };
    private static StreamWriter Turns, Cards;
    private static string Scenario;
    private sealed class Candidate
    {
        public string Id, BossId;
        public int Hp, MainAttack, FireConsumeAttack;
    }
    private sealed class Result
    {
        public string Boss, Policy, Outcome, Candidate;
        public int Seed, EntryPercent, EntryHp, MaxHp, EnemyMaxHp, Turns, Hp, EnemyHp, MedicineUsed, Cards, Energy, Incoming, Lost;
    }
    private static int Main(string[] args)
    {
        string output = args[0]; int seeds = args.Length > 1 ? int.Parse(args[1]) : 30;
        int startSeed = args.Length > 2 ? int.Parse(args[2]) : 0;
        Scenario = args.Length > 3 ? args[3] : "first-starter";
        string policySet = args.Length > 4 ? args[4] : "balanced";
        int entryPercent = args.Length > 5 ? int.Parse(args[5]) : 100;
        string candidateFile = args.Length > 6 ? args[6] : "-";
        if (Scenario != "first-starter" && !new[] { "second-unprepared", "second-upgrades", "second-ship", "second-ship-upgrades",
            "second-prepared-no-special", "second-prepared", "second-prepared-2med", "second-prepared-cook",
            "second-prepared-isha", "second-prepared-mixed" }.Contains(Scenario))
            throw new ArgumentException("Unknown fixture scenario.");
        if (Scenario.StartsWith("second-", StringComparison.Ordinal))
            BossIds = new[] { "boss_route_golden_tide_trade", "boss_route_golden_tide_trade_thunder" };
        var candidates = LoadCandidates(candidateFile);
        var policies = policySet == "all" ? Policies : new[] { "balanced" };
        if (seeds < 1 || seeds > 500) throw new ArgumentOutOfRangeException(nameof(seeds));
        Directory.CreateDirectory(output); var rows = new List<Result>(); ExportStarter(output);
        File.WriteAllText(Path.Combine(output, "run-settings.txt"), "scenario=" + Scenario + "\nstartSeed=" + startSeed +
            "\nseedCount=" + seeds + "\npolicySet=" + policySet + "\nentryPercent=" + entryPercent + "\n", new UTF8Encoding(false));
        using (var file = Writer(output, "candidate-parameters.csv"))
        {
            file.WriteLine("id,boss,boardingHp,mainAttack,fireConsumeAttack");
            foreach (var candidate in candidates) file.WriteLine(Csv(candidate.Id, candidate.BossId, candidate.Hp, candidate.MainAttack, candidate.FireConsumeAttack));
        }
        using (Turns = Writer(output, "turns.csv"))
        using (Cards = Writer(output, "actions.csv"))
        using (var runs = Writer(output, "runs.csv"))
        {
            Turns.WriteLine("candidate,boss,policy,entryPercent,seed,turn,hpBefore,guardBefore,enemyHpBefore,enemyGuardBefore,previewAttack,previewHpLost,enemyEnergy,actualAttack,actualGuard,actualHeal,actualHpLost,hpAfter,enemyHpAfter,medicineLeft");
            Cards.WriteLine("candidate,boss,policy,entryPercent,seed,turn,action,card,energyCost,hpBefore,hpAfter,enemyHpBefore,enemyHpAfter,guardBefore,guardAfter,medicineLeft");
            runs.WriteLine("candidate,boss,policy,entryPercent,seed,outcome,turns,entryHp,maxHp,remainingHp,enemyMaxHp,enemyRemainingHp,medicineUsed,cards,energySpent,incomingAttack,actualHpLost");
            foreach (var candidate in candidates) foreach (string boss in BossIds.Where(id => string.IsNullOrEmpty(candidate.BossId) || id == candidate.BossId)) foreach (int entry in new[] { entryPercent }) foreach (string policy in policies)
            {
                for (int seed = startSeed; seed < startSeed + seeds; seed++)
                {
                    var r = Run(boss, policy, entry, seed, seed < startSeed + 3, candidate); rows.Add(r);
                    runs.WriteLine(Csv(r.Candidate, r.Boss, r.Policy, r.EntryPercent, r.Seed, r.Outcome, r.Turns, r.EntryHp, r.MaxHp,
                        r.Hp, r.EnemyMaxHp, r.EnemyHp, r.MedicineUsed, r.Cards, r.Energy, r.Incoming, r.Lost));
                }
                Console.WriteLine(candidate.Id + " " + boss + " " + policy + " " + entry + "% complete.");
            }
        }
        using (var summary = Writer(output, "summary.csv"))
        {
            summary.WriteLine("candidate,boss,policy,entryPercent,runs,wins,winPercent,averageRemainingHp,averageWinningHp,minWinningHp,averageTurns,averageMedicineUsed,timeouts");
            foreach (var group in rows.GroupBy(r => new { r.Candidate, r.Boss, r.Policy, r.EntryPercent }))
            {
                var wins = group.Where(r => r.Outcome == "win").ToArray();
                string row = Csv(group.Key.Candidate, group.Key.Boss, group.Key.Policy, group.Key.EntryPercent, group.Count(), wins.Length,
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
        bool second = Scenario.StartsWith("second-", StringComparison.Ordinal);
        bool ship = Scenario == "second-ship" || Scenario == "second-ship-upgrades" || Scenario.StartsWith("second-prepared", StringComparison.Ordinal);
        bool upgrades = Scenario == "second-upgrades" || Scenario == "second-ship-upgrades" || Scenario.StartsWith("second-prepared", StringComparison.Ordinal);
        bool specialists = Scenario == "second-prepared" || Scenario == "second-prepared-2med";
        bool cookOnly = Scenario == "second-prepared-cook";
        bool ishaOnly = Scenario == "second-prepared-isha";
        bool mixed = Scenario == "second-prepared-mixed";
        int specialistCount = specialists || mixed ? 2 : cookOnly || ishaOnly ? 1 : 0;
        bool supplies = Scenario == "second-prepared" || Scenario == "second-prepared-no-special" || cookOnly || mixed;
        if (ship && !s.SelectShip(4)) throw new InvalidOperationException("Salvage ship selection rejected.");
        if (second)
        {
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[1].UnlockLevel) });
            s.SelectRoute(1);
        }
        s.ConfigureDepartureCrew(s.CrewCountValue - specialistCount, specialistCount);
        s.ConfigureCrewCapabilities(false, false, specialists, false, specialists || cookOnly || mixed, ishaOnly || mixed);
        s.ConfigureSpecialCardSources(specialists ? new[] { "crew_broken_anchor_doctor_hector", "crew_broken_anchor_cook_tam" } :
            mixed ? new[] { "crew_broken_anchor_cook_tam", "crew_salt_crown_diver_isha" } :
            cookOnly ? new[] { "crew_broken_anchor_cook_tam" } :
            ishaOnly ? new[] { "crew_salt_crown_diver_isha" } : Array.Empty<string>(), Array.Empty<string>());
        if (upgrades) s.ConfigureCardUpgrades(new Dictionary<string, int> {
            { "card_cutlass_raid", 1 }, { "card_crew_thrust", 1 }, { "card_crew_guard", 1 } });
        if (supplies) s.Cargo.Medicine = 4;
        if (Scenario.StartsWith("second-prepared", StringComparison.Ordinal) && !ishaOnly) s.Cargo.Powder = 4;
        s.ConfigureComponentBonuses(0, 0, 0, 0, 0); s.ConfigureBattleEquipment(1f, 1f);
        if (s.SelectedShipIndex != (ship ? 4 : 0) || s.SelectedRouteIndex != (second ? 1 : 0) ||
            s.Cargo.Medicine != (supplies ? 4 : 2) || s.GetBattleDeck(NavalBattleStage.Boarding).Count != 12)
            throw new InvalidOperationException("Fixture baseline changed; review simulation.");
        foreach (int index in s.GetBattleDeck(NavalBattleStage.Boarding))
        {
            var card = s.GetBattleCard(index);
            int expected = upgrades && new[] { "card_cutlass_raid", "card_crew_thrust", "card_crew_guard" }.Contains(card.Id) ? 1 : 0;
            if (card.UpgradeLevel != expected) throw new InvalidOperationException("Partial upgrade fixture mismatch: " + card.Id);
        }
        if (s.GetBonusBattleCards(NavalBattleStage.Boarding).Count != specialistCount)
            throw new InvalidOperationException("Unexpected specialist boarding cards.");
        if (cookOnly && (s.SelectedOrdinaryCrew != 15 || s.SelectedSpecialCrewCount != 1 ||
            CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(s.GetBonusBattleCards(NavalBattleStage.Boarding)[0])).BoardingSpec?.Key != "fire_05"))
            throw new InvalidOperationException("Cook-only fixture must have 15 ordinary crew, one cook and fire_05.");
        if (ishaOnly || mixed)
        {
            var actualKeys = s.GetBonusBattleCards(NavalBattleStage.Boarding)
                .Select(index => CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(index)).BoardingSpec?.Key).OrderBy(key => key);
            string[] expectedKeys = mixed ? new[] { "blood_07", "fire_05" } : new[] { "blood_07" };
            if (s.SelectedOrdinaryCrew != (mixed ? 14 : 15) || s.SelectedSpecialCrewCount != (mixed ? 2 : 1) ||
                !actualKeys.SequenceEqual(expectedKeys)) throw new InvalidOperationException("Isha fixture crew or real specialist cards mismatch.");
        }
        return s;
    }
    private static void ExportStarter(string output)
    {
        var s = NewStarter();
        using (var file = Writer(output, "starter-deck.csv"))
        {
            file.WriteLine("libraryIndex,id,key,name,copies,upgrade,energy,damage,guard,heal,draw,requiredCrew");
            foreach (var group in s.GetBattleDeck(NavalBattleStage.Boarding).Concat(s.GetBonusBattleCards(NavalBattleStage.Boarding)).GroupBy(i => i))
            {
                var c = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(group.Key));
                file.WriteLine(Csv(group.Key, c.Id, c.BoardingSpec?.Key, c.Name, group.Count(), c.UpgradeLevel, c.Energy, c.Damage, c.Block, c.Repair, c.Draw, c.RequiredCrew));
            }
        }
        File.WriteAllText(Path.Combine(output, "baseline.txt"),
            "scenario=" + Scenario + "; ship=" + s.SelectedShip.Id + "; ordinaryCrew=" + s.SelectedOrdinaryCrew +
            "; specialCrew=" + s.SelectedSpecialCrewCount + "; expectedBoardingHp=" + CoreBattleBalance.BoardingHealthForOrdinaryCrew(s.SelectedOrdinaryCrew) +
            "; medicine=" + s.Cargo.Medicine + "; powder=" + s.Cargo.Powder + "\n" +
            "Original 12-card boarding deck retained; no SetBattleDeck call. Any fixture upgrades and specialist bonus cards are enumerated in starter-deck.csv. No equipment bonuses.\n" +
            "Only navigation, naval approach and preparation are bypassed. Real TryBoard executes at orthogonal 100m contact with matching headings.\n" +
            "EntryPercent sets current player HP; player maximum HP stays production. Candidate mode clones only the local enemy profile boarding HP and selected fixed attack card values. Global definitions are unchanged.\n" +
            "Three fixed one-step policies use only present hand, basic card effects and visible enemy intent. No lookahead, optimized deck, unlimited medicine or future knowledge.\n" +
            "Traces cover first 3 seeds per scenario. All scenarios share the seed prefix for comparable shuffles and critical rolls.\n", new UTF8Encoding(false));
    }
    private static Result Run(string boss, string policy, int entryPercent, int seed, bool trace, Candidate candidate)
    {
        var s = NewStarter(); int initialMedicine = s.Cargo.Medicine;
        s.SetSeed("FIRST-CHART-NOVICE-20261016-" + seed.ToString("000")); s.StartVoyage();
        if (s.Phase != CoreDemoPhase.Sailing) throw new InvalidOperationException("Starter departure rejected.");
        StartBattle.Invoke(s, new object[] { CoreRouteBossCatalog.Find(boss).CreateEncounter(), true });
        if (candidate.Hp > 0)
        {
            var p = s.Battle.Profile;
            s.Battle.Profile = new CoreBattleProfile(p.BossKind, p.Archetype, p.EnemyHull, p.CannonDamage,
                p.MeleeDamage, p.Guard, p.InitialGuard, p.OrdinaryCrewCount, p.BowQuality, p.HullQuality,
                p.RamChance, p.RamRangeMeters, healthMultiplier: 1m,
                boardingHealthMultiplier: (decimal)candidate.Hp / CoreBattleBalance.BoardingHealthForOrdinaryCrew(p.OrdinaryCrewCount), boardingGuard: p.BoardingGuard);
        }
        if (s.Battle.Stage != NavalBattleStage.Preparation || !s.EndBattleTurn()) throw new InvalidOperationException("Preparation failed.");
        s.Battle.DistanceMeters = 100f;
        typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
        s.Battle.Alignment = NavalBattleAlignment.Parallel;
        if (!s.TryBoard()) throw new InvalidOperationException("Real boarding entry rejected.");
        var b = s.Battle;
        int[] expectedDeck = s.GetBattleDeck(NavalBattleStage.Boarding).Concat(s.GetBonusBattleCards(NavalBattleStage.Boarding)).OrderBy(i => i).ToArray();
        if (!expectedDeck.SequenceEqual(b.Hand.Concat(b.DrawPile).OrderBy(i => i)))
            throw new InvalidOperationException("Actual boarding hand/draw pile differs from configured base and specialist cards.");
        // StartBattle caches boarding HP before stage entry, so a local profile
        // replacement must also refresh those two cached values explicitly.
        if (candidate.Hp > 0)
        {
            b.EnemyMaxBoardingHealth = b.EnemyBoardingHealth = b.Profile.BoardingHealth;
            if (b.EnemyMaxBoardingHealth != candidate.Hp) throw new InvalidOperationException("Candidate boarding HP mismatch.");
        }
        ApplyCandidate(b, candidate, boss);
        if (b.PlayerMaxBoardingHealth != CoreBattleBalance.BoardingHealthForOrdinaryCrew(s.SelectedOrdinaryCrew) ||
            b.Hand.Count != 5 || b.Energy != 3 || s.Cargo.Medicine != initialMedicine)
            throw new InvalidOperationException("Fixture boarding entry baseline changed.");
        b.PlayerBoardingHealth = b.PlayerMaxBoardingHealth * entryPercent / 100;
        var r = new Result { Candidate = candidate.Id, Boss = boss, Policy = policy, EntryPercent = entryPercent, Seed = seed, EntryHp = b.PlayerBoardingHealth,
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
                if (trace) Cards.WriteLine(Csv(candidate.Id, boss, policy, entryPercent, seed, turn, action + 1, name, fee, hp, b.PlayerBoardingHealth,
                    enemyHp, b.EnemyBoardingHealth, guard, b.PlayerBlock, s.Cargo.Medicine));
                if (action == 31) throw new InvalidOperationException("Action watchdog reached.");
            }
            if (s.Phase != CoreDemoPhase.Battle) break;
            var intent = s.GetBoardingEnemyPreview();
            int hpBefore = b.PlayerBoardingHealth, guardBefore = b.PlayerBlock, enemyBefore = b.EnemyBoardingHealth, enemyGuard = b.EnemyBlock;
            int feeTotal = b.BoardingEnemy.Deck.TurnAt(b.PhaseTurn).TotalEnergy; r.Incoming += intent.AttackDamage;
            if (!s.EndBattleTurn()) throw new InvalidOperationException("End turn rejected.");
            int loss = Math.Max(0, hpBefore - b.PlayerBoardingHealth); r.Lost += loss;
            if (trace) Turns.WriteLine(Csv(candidate.Id, boss, policy, entryPercent, seed, turn, hpBefore, guardBefore, enemyBefore, enemyGuard,
                intent.AttackDamage, intent.PlayerHealthLoss, feeTotal, b.BoardingEnemy.LastActions.Sum(a => a.AttackDamage),
                b.BoardingEnemy.LastActions.Sum(a => a.GuardGained), b.BoardingEnemy.LastActions.Sum(a => a.Healed),
                loss, b.PlayerBoardingHealth, b.EnemyBoardingHealth, s.Cargo.Medicine));
        }
        r.Hp = b.PlayerBoardingHealth; r.EnemyHp = b.EnemyBoardingHealth;
        r.Outcome = r.Hp <= 0 ? "loss" : r.EnemyHp <= 0 ? "win" : "timeout"; return r;
    }
    private static Candidate[] LoadCandidates(string path)
    {
        if (path == "-" || string.IsNullOrEmpty(path)) return new[] { new Candidate { Id = "production" } };
        var candidates = File.ReadAllLines(path).Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line =>
        {
            string[] cells = line.Split(',');
            if (cells.Length != 4 && cells.Length != 5) throw new ArgumentException("Candidate CSV requires id,[boss,]boardingHp,mainAttack,fireConsumeAttack.");
            int offset = cells.Length == 5 ? 1 : 0;
            return new Candidate { Id = cells[0], BossId = offset == 1 ? cells[1] : null,
                Hp = int.Parse(cells[1 + offset]), MainAttack = int.Parse(cells[2 + offset]), FireConsumeAttack = int.Parse(cells[3 + offset]) };
        }).ToArray();
        if (candidates.Length == 0 || candidates.Any(c => c.Hp < 0 || c.MainAttack < 0 || c.FireConsumeAttack < 0))
            throw new ArgumentException("Empty or invalid candidate grid.");
        return candidates;
    }
    private static void ApplyCandidate(CoreBattleState battle, Candidate candidate, string boss)
    {
        if (candidate.MainAttack == 0 && candidate.FireConsumeAttack == 0) return;
        var original = battle.BoardingEnemy.Deck;
        int mainChanged = 0, consumeChanged = 0;
        var turns = original.Turns.Select(turn => new CoreBoardingEnemyTurn(turn.Name, turn.Cards.Select(card =>
        {
            int attack = card.AttackPercent;
            if (candidate.MainAttack > 0 && (card.Name == "焰刃破阵" || card.Mechanic == CoreBoardingEnemyMechanic.TideStrike))
            {
                attack = CoreBattleBalance.ScaleBattleValue(candidate.MainAttack * 10, 10m / Math.Max(1, battle.Profile.MeleeDamage));
                mainChanged++;
            }
            if (candidate.FireConsumeAttack > 0 && card.Mechanic == CoreBoardingEnemyMechanic.FireConsume)
            {
                attack = CoreBattleBalance.ScaleBattleValue(candidate.FireConsumeAttack * 10, 10m / Math.Max(1, battle.Profile.MeleeDamage));
                consumeChanged++;
            }
            return new CoreBoardingEnemyCard(card.Id, card.Name, attack, card.GuardPercent, card.BurnPercent,
                card.HealPercent, card.DrainPercent, card.SelfDamagePercent, card.PiercePercent,
                card.Mechanic, card.ResourceAmount, card.BonusPercent, card.Energy);
        }).ToArray())).ToArray();
        int expectedMain = boss.EndsWith("_tide", StringComparison.Ordinal) ? 2 : 1;
        if (candidate.MainAttack > 0 && mainChanged != expectedMain) throw new InvalidOperationException("Candidate attack targets changed.");
        if (candidate.FireConsumeAttack > 0 && expectedMain == 1 && consumeChanged != 1) throw new InvalidOperationException("Candidate consume target changed.");
        battle.BoardingEnemy.Deck = new CoreBoardingEnemyDeck(original.SchoolId, turns);
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
