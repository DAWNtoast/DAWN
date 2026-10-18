using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Policies inspect only the current hand and visible enemy intent, never future
// draws or future enemy turns. Keep the actual starter deck, two explicitly
// sourced specialist cards and finite supplies. Never alter an enemy profile.
internal static class SimulateLateCharts
{
    private static readonly MethodInfo StartBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly string[] BossIds = {
        "boss_route_moon_tide_ruins", "boss_route_moon_tide_ruins_arcane",
        "legendary_pirate_iron_oath", "legendary_pirate_red_widow",
        "legendary_pirate_thunder_crown", "legendary_pirate_ember_ark",
        "legendary_pirate_tide_requiem", "legendary_pirate_last_horizon",
        CoreBattleBalance.TreasureBossId };
    private static readonly string[] Policies = { "balanced", "conservative", "aggressive" };
    private static StreamWriter Turns, Cards;
    private static string Scenario;
    private static string School;
    private static SchoolSpec Spec;
    private sealed class SchoolSpec
    {
        public string School;
        public int MinimumLevel, Medicine, Powder;
        public string[] BaseIds, CrewIds, SourceKeys;
    }
    private sealed class Candidate
    {
        public string Id, BossId;
        public int Hp, MainAttack, FireConsumeAttack;
    }
    private sealed class Result
    {
        public string Boss, Policy, Outcome, Candidate;
        public int Seed, EntryPercent, EntryHp, MaxHp, EnemyMaxHp, Turns, Hp, EnemyHp, MedicineUsed, PowderUsed, MedicineLeft, PowderLeft, Cards, Energy, Incoming, Lost;
    }
    private static int Main(string[] args)
    {
        string output = args[0]; int seeds = args.Length > 1 ? int.Parse(args[1]) : 30;
        int startSeed = args.Length > 2 ? int.Parse(args[2]) : 0;
        Scenario = args.Length > 3 ? args[3] : "B0";
        string policySet = args.Length > 4 ? args[4] : "balanced";
        int entryPercent = args.Length > 5 ? int.Parse(args[5]) : 100;
        string candidateFile = args.Length > 6 ? args[6] : "-";
        School = args.Length > 7 ? args[7] : "reference";
        if (School != "reference")
        {
            if (!new[] { "B1", "B2", "B3" }.Contains(Scenario)) throw new ArgumentException("School decks require growth build B1/B2/B3.");
            string[] cells = File.ReadAllLines(args[8]).Skip(1).Select(line => line.Split('|')).Single(row => row[0] == School);
            Spec = new SchoolSpec { School = cells[0], MinimumLevel = int.Parse(cells[1]), BaseIds = cells[2].Split(','),
                CrewIds = cells[3].Split(','), SourceKeys = cells[4].Split(','), Medicine = int.Parse(cells[5]), Powder = int.Parse(cells[6]) };
        }
        if (!new[] { "B0", "B1", "B2", "B3", "B4" }.Contains(Scenario))
            throw new ArgumentException("Unknown fixture scenario.");
        if (candidateFile != "-") throw new ArgumentException("Audit does not permit enemy candidate overrides.");
        var candidates = LoadCandidates(candidateFile);
        var policies = policySet == "all" ? Policies : new[] { "balanced" };
        if (seeds < 1 || seeds > 500) throw new ArgumentOutOfRangeException(nameof(seeds));
        Directory.CreateDirectory(output); var rows = new List<Result>(); ExportStarter(output);
        File.WriteAllText(Path.Combine(output, "run-settings.txt"), "scenario=" + Scenario + "\nschool=" + School + "\nstartSeed=" + startSeed +
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
            Turns.WriteLine("candidate,boss,policy,entryPercent,seed,turn,hpBefore,guardBefore,enemyHpBefore,enemyGuardBefore,previewAttack,previewHpLost,enemyEnergy,actualAttack,actualGuard,actualHeal,actualHpLost,hpAfter,enemyHpAfter,medicineLeft,powderLeft,resourcesBefore,resourcesAfter");
            Cards.WriteLine("candidate,boss,policy,entryPercent,seed,turn,action,card,energyCost,hpBefore,hpAfter,enemyHpBefore,enemyHpAfter,guardBefore,guardAfter,medicineLeft,powderLeft,key,handBefore,resourcesBefore,resourcesAfter,energyBefore,energyAfter");
            runs.WriteLine("candidate,boss,policy,entryPercent,seed,outcome,turns,entryHp,maxHp,remainingHp,enemyMaxHp,enemyRemainingHp,medicineUsed,cards,energySpent,incomingAttack,actualHpLost,powderUsed,medicineLeft,powderLeft");
            foreach (var candidate in candidates) foreach (string boss in BossIds.Where(id => string.IsNullOrEmpty(candidate.BossId) || id == candidate.BossId)) foreach (int entry in new[] { entryPercent }) foreach (string policy in policies)
            {
                for (int seed = startSeed; seed < startSeed + seeds; seed++)
                {
                    var r = Run(boss, policy, entry, seed, seed < startSeed + 3, candidate); rows.Add(r);
                    runs.WriteLine(Csv(r.Candidate, r.Boss, r.Policy, r.EntryPercent, r.Seed, r.Outcome, r.Turns, r.EntryHp, r.MaxHp,
                        r.Hp, r.EnemyMaxHp, r.EnemyHp, r.MedicineUsed, r.Cards, r.Energy, r.Incoming, r.Lost, r.PowderUsed, r.MedicineLeft, r.PowderLeft));
                }
                Console.WriteLine(candidate.Id + " " + boss + " " + policy + " " + entry + "% complete.");
            }
        }
        using (var summary = Writer(output, "summary.csv"))
        {
            summary.WriteLine("candidate,boss,policy,entryPercent,runs,wins,winPercent,averageRemainingHp,averageWinningHp,minWinningHp,averageTurns,averageMedicineUsed,timeouts,averagePowderUsed,medicineExhaustedPercent,powderExhaustedPercent");
            foreach (var group in rows.GroupBy(r => new { r.Candidate, r.Boss, r.Policy, r.EntryPercent }))
            {
                var wins = group.Where(r => r.Outcome == "win").ToArray();
                string row = Csv(group.Key.Candidate, group.Key.Boss, group.Key.Policy, group.Key.EntryPercent, group.Count(), wins.Length,
                    F(wins.Length * 100d / group.Count()), F(group.Average(r => r.Hp)), wins.Length > 0 ? F(wins.Average(r => r.Hp)) : "",
                    wins.Length > 0 ? wins.Min(r => r.Hp).ToString() : "", F(group.Average(r => r.Turns)),
                    F(group.Average(r => r.MedicineUsed)), group.Count(r => r.Outcome == "timeout"), F(group.Average(r => r.PowderUsed)),
                    F(group.Count(r => r.MedicineLeft == 0) * 100d / group.Count()), F(group.Count(r => r.PowderLeft == 0 && r.PowderUsed > 0) * 100d / group.Count()));
                summary.WriteLine(row); Console.WriteLine(row);
            }
        }
        Console.WriteLine("DONE: " + rows.Count + " battles, " + rows.Count(r => r.Outcome == "win") + " wins, " + rows.Count(r => r.Outcome == "timeout") + " timeouts.");
        return 0;
    }
    private static CoreVoyageSession NewStarter()
    {
        if (Spec != null) return NewSchoolStarter();
        var s = new CoreVoyageSession();
        int shipIndex = Scenario == "B0" ? 4 : Scenario == "B1" ? 2 : 7;
        int requestedRank = Scenario == "B0" ? 1 : Scenario == "B3" || Scenario == "B4" ? 3 : 2;
        int specialXp = Scenario == "B0" ? 0 : Scenario == "B1" ? 120 : Scenario == "B2" ? 360 : 840;
        int supplies = Scenario == "B0" ? 4 : Scenario == "B1" ? 6 : 8;
        if (!s.SelectShip(shipIndex)) throw new InvalidOperationException("Fixture ship selection rejected.");
        s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[2].UnlockLevel) });
        s.SelectRoute(2);
        if (Scenario == "B4")
        {
            string[] ids = { "card_boarding_hook", "card_cutlass_raid", "card_crew_bandage", "card_energy_order", "card_tactical_draw" };
            int[] indexes = ids.SelectMany(id => Enumerable.Repeat(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(index => CoreVoyageSession.CardLibrary[index].Id == id), 2)).ToArray();
            if (!s.SetBattleDeck(NavalBattleStage.Boarding, indexes)) throw new InvalidOperationException("Legal active 10-card deck rejected.");
        }
        s.ConfigureDepartureCrew(s.CrewCountValue - 2, 2);
        s.ConfigureCrewCapabilities(false, false, false, false, true, true);
        string[] crewIds = { "crew_broken_anchor_cook_tam", "crew_salt_crown_diver_isha" };
        s.ConfigureSpecialCardSources(crewIds, Array.Empty<string>());
        s.ConfigureCardUpgrades(s.GetBattleDeck(NavalBattleStage.Boarding).Select(index => s.GetBattleCard(index)).Distinct()
            .Where(card => Scenario != "B0" || new[] { "card_cutlass_raid", "card_crew_thrust", "card_crew_guard" }.Contains(card.Id))
            .GroupBy(card => card.Id).ToDictionary(group => group.Key, group => Math.Min(requestedRank, s.GetCardMaximumUpgradeLevel(group.Key))));
        s.RestoreSpecialCardExperience(crewIds.ToDictionary(id => CoreSpecialCardCatalog.ForCrew(id).Card.Id, id => specialXp));
        s.Cargo.Medicine = s.Cargo.Powder = supplies;
        s.ConfigureComponentBonuses(0, 0, 0, 0, 0); s.ConfigureBattleEquipment(1f, 1f);
        if (s.SelectedShipIndex != shipIndex || s.SelectedRouteIndex != 2 ||
            s.Cargo.Medicine != supplies || s.Cargo.Curse != 0 || s.GetBattleDeck(NavalBattleStage.Boarding).Count != (Scenario == "B4" ? 10 : 12))
            throw new InvalidOperationException("Fixture baseline changed; review simulation.");
        foreach (int index in s.GetBattleDeck(NavalBattleStage.Boarding))
        {
            var card = s.GetBattleCard(index);
            int expected = Scenario == "B0" ? (new[] { "card_cutlass_raid", "card_crew_thrust", "card_crew_guard" }.Contains(card.Id) ? 1 : 0)
                : Math.Min(requestedRank, s.GetCardMaximumUpgradeLevel(card.Id));
            if (card.UpgradeLevel != expected) throw new InvalidOperationException("Partial upgrade fixture mismatch: " + card.Id);
        }
        if (s.GetBonusBattleCards(NavalBattleStage.Boarding).Count != 2)
            throw new InvalidOperationException("Unexpected specialist boarding cards.");
        foreach (int index in s.GetBonusBattleCards(NavalBattleStage.Boarding))
        {
            var card = s.GetBattleCard(index);
            int specialRank = specialXp >= 840 ? 3 : specialXp >= 360 ? 2 : specialXp >= 120 ? 1 : 0;
            if (card.UpgradeLevel != Math.Min(specialRank, s.GetCardMaximumUpgradeLevel(card.Id)))
                throw new InvalidOperationException("Special card experience fixture mismatch: " + card.Id);
        }
        var actualKeys = s.GetBonusBattleCards(NavalBattleStage.Boarding)
            .Select(index => CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(index)).BoardingSpec?.Key).OrderBy(key => key);
        if (!actualKeys.SequenceEqual(new[] { "blood_07", "fire_05" })) throw new InvalidOperationException("Real specialist card mapping mismatch.");
        return s;
    }
    private static CoreVoyageSession NewSchoolStarter()
    {
        var s = new CoreVoyageSession();
        int shipIndex = Scenario == "B1" ? 2 : 7, rank = Scenario == "B3" ? 3 : 2;
        int specialXp = Scenario == "B1" ? 120 : Scenario == "B2" ? 360 : 840;
        int pirateLevel = Math.Max(CoreVoyageSession.Routes[2].UnlockLevel, Spec.MinimumLevel);
        if (!s.SelectShip(shipIndex)) throw new InvalidOperationException("School ship rejected.");
        s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(pirateLevel) });
        s.SelectRoute(2);
        s.ConfigureDepartureCrew(s.CrewCountValue - 2, 2);
        s.ConfigureCrewCapabilities(false, School == "curse", School == "arcane" || School == "tide" || School == "thunder" || School == "blood",
            false, School == "fire" || School == "curse" || School == "tide", School == "fire" || School == "arcane" || School == "thunder" || School == "blood");
        s.ConfigureSpecialCardSources(Spec.CrewIds, Array.Empty<string>());
        var available = s.GetAvailableBattleCards(NavalBattleStage.Boarding);
        int[] indexes = Spec.BaseIds.Select(id => {
            string key = id.Substring("card_boarding_".Length);
            return available.Single(index => CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(index)).BoardingSpec?.Key == key);
        }).ToArray();
        if (indexes.Length != 10 || !s.SetBattleDeck(NavalBattleStage.Boarding, indexes)) throw new InvalidOperationException("Legal school deck rejected: " + School);
        s.ConfigureCardUpgrades(indexes.Select(index => s.GetBattleCard(index)).GroupBy(card => card.Id)
            .ToDictionary(group => group.Key, group => Math.Min(rank, s.GetCardMaximumUpgradeLevel(group.Key))));
        s.RestoreSpecialCardExperience(Spec.CrewIds.ToDictionary(id => CoreSpecialCardCatalog.ForCrew(id).Card.Id, id => specialXp));
        s.Cargo.Medicine = Spec.Medicine; s.Cargo.Powder = Spec.Powder;
        s.ConfigureComponentBonuses(0, 0, 0, 0, 0); s.ConfigureBattleEquipment(1f, 1f);
        var bonus = s.GetBonusBattleCards(NavalBattleStage.Boarding);
        if (bonus.Count != 2 || !bonus.Select(index => CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(index)).BoardingSpec.Key)
            .OrderBy(key => key).SequenceEqual(Spec.SourceKeys.OrderBy(key => key))) throw new InvalidOperationException("Actual school source keys mismatch.");
        if (!bonus.Select(index => s.GetBattleCard(index).Id).OrderBy(id => id).SequenceEqual(
            Spec.CrewIds.Select(id => CoreSpecialCardCatalog.ForCrew(id).Card.Id).OrderBy(id => id))) throw new InvalidOperationException("School source identities mismatch.");
        foreach (int index in indexes.Concat(bonus))
        {
            var card = s.GetBattleCard(index);
            int requested = bonus.Contains(index) ? (Scenario == "B1" ? 1 : rank) : rank;
            if (card.UpgradeLevel != Math.Min(requested, s.GetCardMaximumUpgradeLevel(card.Id))) throw new InvalidOperationException("School card level mismatch: " + card.Id);
        }
        if (s.Cargo.Curse != 0 || s.SelectedOrdinaryCrew != (Scenario == "B1" ? 20 : 26) || s.SelectedSpecialCrewCount != 2)
            throw new InvalidOperationException("School health/crew/curse baseline mismatch.");
        return s;
    }
    private static void ExportStarter(string output)
    {
        var s = NewStarter();
        using (var file = Writer(output, "starter-deck.csv"))
        {
            file.WriteLine("libraryIndex,id,key,name,copies,upgrade,maxUpgrade,energy,damage,guard,heal,draw,requiredCrew,specialXp");
            foreach (var group in s.GetBattleDeck(NavalBattleStage.Boarding).Concat(s.GetBonusBattleCards(NavalBattleStage.Boarding)).GroupBy(i => i))
            {
                var c = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(group.Key));
                var effect = s.GetBattleCardEffects(c, NavalBattleStage.Boarding);
                file.WriteLine(Csv(group.Key, c.Id, c.BoardingSpec?.Key, c.Name, group.Count(), c.UpgradeLevel,
                    s.GetCardMaximumUpgradeLevel(c.Id), c.Energy, effect.Damage, effect.Block, effect.Repair, c.Draw,
                    c.RequiredCrew, s.GetSpecialCardExperience(c.Id)));
            }
        }
        using (var file = Writer(output, "encounters.csv"))
        {
            file.WriteLine("id,name,school,boardingHp,boardingAttackBase,boardingGuardBase,turnFees,treasureRank");
            foreach (string id in BossIds)
            {
                var route = CoreRouteBossCatalog.Find(id); var legend = CoreLegendaryWarships.Find(id);
                var encounter = route?.CreateEncounter() ?? legend?.CreateEncounter() ?? CoreLegendaryWarships.CreateTreasureEncounter();
                var profile = CoreBattleBalance.Resolve(encounter, 2, true);
                var deck = CoreBoardingEnemyCatalog.ForEncounter(id);
                string rank = id.Contains("iron_oath") || id.Contains("red_widow") ? "weathered" :
                    id.Contains("thunder_crown") || id.Contains("ember_ark") ? "gilded" :
                    id.Contains("tide_requiem") || id.Contains("last_horizon") ? "moonlit" : "";
                file.WriteLine(Csv(id, encounter.Name, deck.SchoolId, profile.BoardingHealth, profile.MeleeDamage,
                    profile.BoardingGuard, string.Join("/", deck.Turns.Select(turn => turn.TotalEnergy)), rank));
            }
        }
        File.WriteAllText(Path.Combine(output, "baseline.txt"),
            "scenario=" + Scenario + "; school=" + School + "; ship=" + s.SelectedShip.Id + "; ordinaryCrew=" + s.SelectedOrdinaryCrew +
            "; specialCrew=" + s.SelectedSpecialCrewCount + "; expectedBoardingHp=" + CoreBattleBalance.BoardingHealthForOrdinaryCrew(s.SelectedOrdinaryCrew) +
            "; medicine=" + s.Cargo.Medicine + "; powder=" + s.Cargo.Powder + "\n" +
            (Spec != null ? "System deck: ten base cards validated through SetBattleDeck plus two explicit source cards; school-spec.txt contains exact cards and crew.\n" :
             Scenario == "B4" ? "Active 10-card deck validated through SetBattleDeck; each of hook/cutlass/bandage/energy_order/tactical_draw has two copies.\n" :
                "Original 12-card boarding deck retained; no SetBattleDeck call.\n") +
            "Fixture upgrades and two specialist bonus cards are enumerated in starter-deck.csv. No equipment bonuses.\n" +
            "Only navigation, naval approach and preparation are bypassed. Real TryBoard executes at orthogonal 100m contact with matching headings.\n" +
            "EntryPercent sets current player HP; player maximum HP stays production. Enemy definitions and runtime profiles remain production.\n" +
            "Pirate level=" + (Spec == null ? 5 : Math.Max(5, Spec.MinimumLevel)) + "; no voyage curse, component cards or equipment multipliers.\n" +
            (Spec == null ? "Reference policies use only present hand, basic card effects and visible enemy intent; they do not fairly value arbitrary school decks.\n" :
                "SystemPolicy evaluates actual school keys, public resource state, current hand combinations and visible intent; it never executes speculative gameplay, inspects future deck order or rolls future RNG. The fixed policy is a reproducible estimate, not optimal human play.\n") +
            "Treasure legend targets use the same catalog encounters/profile/script as injected into current treasure maps. This is independent full-health boarding, not voyage completion probability or chained encounters.\n" +
            "Traces cover first 3 seeds per scenario. All scenarios share the seed prefix for comparable shuffles and critical rolls.\n", new UTF8Encoding(false));
    }
    private static Result Run(string boss, string policy, int entryPercent, int seed, bool trace, Candidate candidate)
    {
        var s = NewStarter(); int initialMedicine = s.Cargo.Medicine;
        s.SetSeed("LATE-CHART-AUDIT-20261016-" + seed.ToString("000")); s.StartVoyage();
        if (s.Phase != CoreDemoPhase.Sailing) throw new InvalidOperationException("Starter departure rejected.");
        CoreEncounter encounter = CoreRouteBossCatalog.Find(boss)?.CreateEncounter()
            ?? CoreLegendaryWarships.Find(boss)?.CreateEncounter()
            ?? (boss == CoreBattleBalance.TreasureBossId ? CoreLegendaryWarships.CreateTreasureEncounter() : null);
        if (encounter == null) throw new InvalidOperationException("No real encounter for " + boss);
        StartBattle.Invoke(s, new object[] { encounter, true });
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
                    double score = Spec == null ? Score(s, slot, preview, policy) : SystemPolicy.Score(s, slot, preview, policy);
                    if (score > best) { best = score; chosen = slot; }
                }
                if (chosen < 0) break;
                string name = s.GetBattleCard(b.Hand[chosen]).Name;
                string key = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(b.Hand[chosen])).BoardingSpec.Key;
                string resources = ResourceState(b), hand = trace ? string.Join(";", b.Hand.Select(index => CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(index)).BoardingSpec.Key)) : "";
                int fee = s.GetBoardingHandEnergyCost(chosen), hp = b.PlayerBoardingHealth, enemyHp = b.EnemyBoardingHealth, guard = b.PlayerBlock,
                    medicine = s.Cargo.Medicine, powder = s.Cargo.Powder, energy = b.Energy;
                if (!s.PlayCard(chosen)) throw new InvalidOperationException("Chosen legal card rejected.");
                r.Cards++; r.Energy += fee; r.MedicineUsed += Math.Max(0, medicine - s.Cargo.Medicine);
                r.PowderUsed += Math.Max(0, powder - s.Cargo.Powder);
                if (trace) Cards.WriteLine(Csv(candidate.Id, boss, policy, entryPercent, seed, turn, action + 1, name, fee, hp, b.PlayerBoardingHealth,
                    enemyHp, b.EnemyBoardingHealth, guard, b.PlayerBlock, s.Cargo.Medicine, s.Cargo.Powder, key, hand, resources, ResourceState(b), energy, b.Energy));
                if (action == 31) throw new InvalidOperationException("Action watchdog reached.");
            }
            if (s.Phase != CoreDemoPhase.Battle) break;
            var intent = s.GetBoardingEnemyPreview();
            int hpBefore = b.PlayerBoardingHealth, guardBefore = b.PlayerBlock, enemyBefore = b.EnemyBoardingHealth, enemyGuard = b.EnemyBlock;
            string resourcesBefore = ResourceState(b);
            int feeTotal = b.BoardingEnemy.Deck.TurnAt(b.PhaseTurn).TotalEnergy; r.Incoming += intent.AttackDamage;
            if (!s.EndBattleTurn()) throw new InvalidOperationException("End turn rejected.");
            int loss = Math.Max(0, hpBefore - b.PlayerBoardingHealth); r.Lost += loss;
            if (trace) Turns.WriteLine(Csv(candidate.Id, boss, policy, entryPercent, seed, turn, hpBefore, guardBefore, enemyBefore, enemyGuard,
                intent.AttackDamage, intent.PlayerHealthLoss, feeTotal, b.BoardingEnemy.LastActions.Sum(a => a.AttackDamage),
                b.BoardingEnemy.LastActions.Sum(a => a.GuardGained), b.BoardingEnemy.LastActions.Sum(a => a.Healed),
                loss, b.PlayerBoardingHealth, b.EnemyBoardingHealth, s.Cargo.Medicine, s.Cargo.Powder, resourcesBefore, ResourceState(b)));
        }
        r.Hp = b.PlayerBoardingHealth; r.EnemyHp = b.EnemyBoardingHealth;
        r.MedicineLeft = s.Cargo.Medicine; r.PowderLeft = s.Cargo.Powder;
        r.Outcome = r.Hp <= 0 ? "loss" : r.EnemyHp <= 0 ? "win" : "timeout"; return r;
    }
    private static string ResourceState(CoreBattleState b) => "mana=" + b.Schools.AvailableMana + ";marks=" + b.Boarding.CurseMarks +
        ";tide=" + b.Boarding.TidePower + ";chain=" + b.Schools.ThunderChain + ";selfburn=" + b.Boarding.SelfBurn.Sum(stack => stack.Amount) +
        ";enemyburn=" + b.Boarding.EnemyBurn.Sum(stack => stack.Amount) + ";delayCount=" + b.Boarding.DelayedDamage.Count +
        ";delayed=" + b.Boarding.DelayedDamage.Sum(damage => damage.Amount);
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
        // Existing burn bypasses block. Include it for survival urgency without
        // inflating the amount a normal guard card can actually prevent.
        bool danger = b.PlayerBoardingHealth <= incoming + preview.PlayerBurnDamage + 8;
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
