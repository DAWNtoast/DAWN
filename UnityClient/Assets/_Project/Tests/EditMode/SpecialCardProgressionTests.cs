using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class SpecialCardProgressionTests
    {
        [Test]
        public void Library_AppendsCatalogWithoutMovingSavedCardPositions()
        {
            Assert.That(CoreVoyageSession.CardLibrary[0].Id, Is.EqualTo("card_chain_shot"));
            Assert.That(CoreVoyageSession.CardLibrary[27].Id, Is.EqualTo("card_component_bow"));
            Assert.That(CoreVoyageSession.CardLibrary[31].Id, Is.EqualTo("card_component_instrument"));
            Assert.That(CoreVoyageSession.CardLibrary.Count, Is.EqualTo(CoreVoyageSession.LegacyBattleCardSlotCount + 1 + CoreSpecialCardCatalog.All.Count + 60 + 2 + CoreBoardingCardCatalog.All.Count + 4 + CoreNavalSchoolExpansionCatalog.All.Count));
            Assert.That(CoreVoyageSession.CardLibrary.Select(card => card.Id).Distinct().Count(), Is.EqualTo(CoreVoyageSession.CardLibrary.Count));
            Assert.That(CoreSpecialCardCatalog.All.All(definition => CoreVoyageSession.IsSpecialBattleCard(definition.Card.Id)), Is.True);
        }

        [Test]
        public void RemovedPlaceholderRecords_AreIgnoredOnEveryRestorePathAndNeverMigrateIntoYilan()
        {
            string[] removed = { "card_witch_fire_rounds", "card_witch_hex", "card_special_crew_witch_jinchao" };
            CoreVoyageSession session = new CoreVoyageSession();
            session.RestoreAcquiredSpecialCards(removed);
            session.RestoreSpecialCardExperience(removed.ToDictionary(id => id, id => 900));
            session.ConfigureCardUpgrades(removed.ToDictionary(id => id, id => 3));
            session.ConfigureSpecialCardSources(new[] { "witch", "crew_witch_jinchao" }, null);
            Assert.That(session.AcquiredSpecialCardIds, Is.Empty);
            Assert.That(session.SpecialCardExperience, Is.Empty);
            Assert.That(session.CardUpgradeLevels, Is.Empty);
            Assert.That(AllBonus(session), Is.Empty);
            Assert.That(CoreSpecialCardCatalog.ForCrew("witch"), Is.Null);
            Assert.That(CoreSpecialCardCatalog.ForCrew("crew_witch_jinchao"), Is.Null);
            foreach (string id in removed)
            {
                Assert.That(CoreVoyageSession.CardLibrary.Any(card => card.Id == id), Is.False);
                Assert.That(CoreSpecialCardCatalog.FindByCardId(id), Is.Null);
                Assert.That(CoreVoyageSession.IsSpecialBattleCard(id), Is.False);
            }
            session.ConfigureSpecialCardSources(new[] { "crew_twilight_tide_gunner_yilan" }, null);
            Assert.That(session.AcquiredSpecialCardIds, Is.EqualTo(new[] { "card_special_crew_twilight_tide_gunner_yilan" }));
            Assert.That(session.GetSpecialCardExperience("card_special_crew_twilight_tide_gunner_yilan"), Is.Zero);
            Assert.That(session.GetCardUpgradeLevel("card_special_crew_twilight_tide_gunner_yilan"), Is.Zero);
        }

        [TestCase(25)]
        [TestCase(26)]
        [TestCase(56)]
        public void RetiredSavedSlots_AreInvisibleAndCannotBeConfiguredUpgradedOrPlayed(int index)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            CoreBattleCard retired = CoreVoyageSession.CardLibrary[index];
            Assert.That(retired.Name + retired.Description + retired.ArtId + retired.RequiredCrew + retired.RequiredComponent, Is.Empty);
            Assert.That(retired.AllowedStages, Is.EqualTo(NavalBattleStages.None));
            Assert.That(session.GetBattleCard(index), Is.Null);
            Assert.That(session.GetBattleCardAtLevel(index, 3), Is.Null);
            Assert.That(session.GetCardUpgradeCost(index), Is.Zero);
            session.RestoreProfile(10000, session.CurrentPort);
            Assert.That(session.TryUpgradeBattleCard(index, out _), Is.False);
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { retired.Id, 3 } });
            Assert.That(session.CardUpgradeLevels, Is.Empty);
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                Assert.That(session.GetAvailableBattleCards(stage), Has.No.Member(index));
                Assert.That(session.GetBonusBattleCards(stage), Has.No.Member(index));
                int[] oldDeck = session.GetBattleDeck(stage).ToArray();
                int[] invalidDeck = oldDeck.ToArray();
                invalidDeck[0] = index;
                Assert.That(session.SetBattleDeck(stage, invalidDeck), Is.False);
                Assert.That(session.SetBattleDeck(invalidDeck), Is.False);
                Assert.That(session.GetBattleDeck(stage), Is.EqualTo(oldDeck));
            }
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("retired_slot_test", "测试船", "海战", "测试", true, false, 1, 70), false });
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(index);
            int energy = session.Battle.Energy;
            Assert.That(session.CanPlayCard(0), Is.False);
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);
        }

        [Test]
        public void SurvivingSavedPositions_KeepAllBaseAndAuthoredCardIndexes()
        {
            string[] baseIds = { "card_chain_shot", "card_full_sail_turn", "card_evasive_maneuver", "card_boarding_hook",
                "card_cutlass_raid", "card_emergency_repair", "card_cannon_barrage", "card_brace", "card_deck_polish",
                "card_load_shot", "card_approach", "card_retreat", "card_circle_enemy", "card_parallel_course",
                "card_crew_thrust", "card_crew_guard", "card_crew_rally", "card_crew_bandage", "card_crew_pistol",
                "card_navigator_course", "card_carpenter_shoring", "card_doctor_sutures", "card_gunner_precision",
                "card_cook_rations", "card_diver_sabotage" };
            Assert.That(CoreVoyageSession.CardLibrary.Take(baseIds.Length).Select(card => card.Id), Is.EqualTo(baseIds));
            int index = CoreVoyageSession.LegacyBattleCardSlotCount;
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                if (index == 56) index++;
                Assert.That(CoreVoyageSession.CardLibrary[index++].Id, Is.EqualTo(definition.Card.Id));
            }
            Assert.That(CoreVoyageSession.CardLibrary[57].Id, Is.EqualTo("card_special_bow_gilded_mermaid"));
            Assert.That(CoreVoyageSession.CardLibrary[131].Id, Is.EqualTo("card_special_instrument_last_horizon_lens"));
        }

        [Test]
        public void ExplicitSources_OnlyInjectTheirOwnCardsAndOverrideLegacyCapabilities()
        {
            CoreSpecialCardDefinition crew = Crew();
            CoreSpecialCardDefinition part = Part();
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(true, true, true, true, true, true);
            session.ConfigureComponentBattleCards(new[] { "bow", "sail", "cannon", "plating", "instrument" });
            session.ConfigureSpecialCardSources(new[] { crew.SourceId, crew.SourceId, "doctor", "cook", "missing_crew" },
                new[] { part.SourceId, part.SourceId, "bow", "cannon", "missing_component" });
            string doctorCardId = CoreSpecialCardCatalog.ForCrew("doctor").Card.Id;
            string cookCardId = CoreSpecialCardCatalog.ForCrew("cook").Card.Id;
            Assert.That(AllBonus(session).Select(index => CoreVoyageSession.CardLibrary[index].Id),
                Is.EquivalentTo(new[] { crew.Card.Id, part.Card.Id, doctorCardId, cookCardId }));
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                foreach (int index in session.GetBonusBattleCards(stage))
                    Assert.That(session.GetAvailableBattleCards(stage), Has.No.Member(index));
            session.ConfigureSpecialCardSources(null, null);
            Assert.That(AllBonus(session), Is.Empty);
            Assert.That(session.AcquiredSpecialCardIds, Is.EquivalentTo(new[] { crew.Card.Id, part.Card.Id, doctorCardId, cookCardId }));
        }

        [Test]
        public void Pepe_LevelsWithActualSailingOnceAcrossThreeStagesAndRetainsProgressOnStandby()
        {
            CoreSpecialCardDefinition pepe = CoreSpecialCardCatalog.ForCrew("crew_gray_gull_cook_pepe");
            int index = Find(pepe.Card.Id);
            var session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(new[] { pepe.SourceId, pepe.SourceId }, null);
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                Assert.That(session.GetBonusBattleCards(stage), Has.Member(index));
            Assert.That(session.GetCardUpgradeLevel(pepe.Card.Id), Is.Zero);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { pepe.Card.Id, 119 } });
            session.SetSeed("PEPE-SUPPORT-XP-REGRESSION");
            session.StartVoyage();
            foreach (CoreMapNode node in session.Map.Nodes) node.Visited = true;
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, true);
            Assert.That(session.GetSpecialCardExperience(pepe.Card.Id), Is.EqualTo(119), "Departure alone grants no XP.");
            Assert.That(SailSomewhere(session), Is.True);
            int earned = (int)Math.Floor(session.TotalDistanceNm * CoreVoyageSession.SpecialCardExperiencePerNauticalMile);
            Assert.That(earned, Is.GreaterThan(0));
            Assert.That(session.GetSpecialCardExperience(pepe.Card.Id), Is.EqualTo(119 + earned),
                "The same card in three stage decks receives the sailed distance only once.");
            Assert.That(session.GetCardUpgradeLevel(pepe.Card.Id), Is.EqualTo(1));
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            session.ConfigureSpecialCardSources(new[] { "crew_white_shell_cook_flora" }, null);
            Assert.That(AllBonus(session), Is.EqualTo(new[] { Find("card_special_crew_white_shell_cook_flora") }));
            Assert.That(session.GetSpecialCardExperience("card_special_crew_white_shell_cook_flora"), Is.Zero);
            Assert.That(session.AcquiredSpecialCardIds, Has.Member(pepe.Card.Id));
            Assert.That(session.GetCardUpgradeLevel(pepe.Card.Id), Is.EqualTo(1));

            var restored = new CoreVoyageSession();
            restored.RestoreSpecialCardExperience(session.SpecialCardExperience);
            restored.RestoreAcquiredSpecialCards(session.AcquiredSpecialCardIds);
            Assert.That(restored.GetSpecialCardExperience(pepe.Card.Id), Is.EqualTo(119 + earned));
            Assert.That(restored.GetCardUpgradeLevel(pepe.Card.Id), Is.EqualTo(1));
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void Pepe_MatureCardActuallyBlocksDrawsAndUsesExistingRepeatedPlayCost(NavalBattleStage stage)
        {
            CoreSpecialCardDefinition pepe = CoreSpecialCardCatalog.ForCrew("crew_gray_gull_cook_pepe");
            int index = Find(pepe.Card.Id);
            var session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(new[] { pepe.SourceId }, null);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { pepe.Card.Id, 840 } });
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(session, new object[] { new CoreEncounter("pepe_fixture", "隔离敌舰", "海战", "热汤支援回归", true, false, 1, 500), false });
            session.Battle.Stage = stage;
            typeof(CoreBattleState).GetProperty(nameof(CoreBattleState.PlayerOrdinaryCrew))
                .SetValue(session.Battle, CoreBattleCardEffects.ReferenceOrdinaryCrew);
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(index);
            session.Battle.DrawPile.Clear();
            int filler = session.GetBattleDeck(stage)[0];
            session.Battle.DrawPile.Add(filler);
            session.Battle.DrawPile.Add(filler);
            session.Battle.Energy = 1;
            int hull = session.Hull, crewHealth = session.Battle.PlayerBoardingHealth;
            int food = session.Cargo.Food, water = session.Cargo.Water, medicine = session.Cargo.Medicine;
            int powder = session.Cargo.Powder, cannonballs = session.Cargo.Cannonballs;
            int expectedBlock = 8; // Pepe reaches +1 with the same authored shield in all three stages.
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(expectedBlock));
            Assert.That(session.Battle.Hand, Is.EqualTo(new[] { filler }));
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(crewHealth));
            Assert.That(new[] { session.Cargo.Food, session.Cargo.Water, session.Cargo.Medicine, session.Cargo.Powder, session.Cargo.Cannonballs },
                Is.EqualTo(new[] { food, water, medicine, powder, cannonballs }));
            Assert.That(session.GetBattleCardEnergyCost(index), Is.EqualTo(2));
            session.Battle.Hand.Add(index);
            Assert.That(session.PlayCard(1), Is.False, "A mastered card keeps its base cost and also pays the next same-turn use fee.");
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(expectedBlock));
        }

        [Test]
        public void AllSpecialCards_RejectMoneyUpgradeRegardlessOfSourceOrGold()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.RestoreProfile(1000000, session.CurrentPort);
            session.ConfigureSpecialCardSources(CoreSpecialCardCatalog.All.Where(item => item.SourceKind == CoreSpecialCardSourceKind.Crew).Select(item => item.SourceId).ToArray(),
                CoreSpecialCardCatalog.All.Where(item => item.SourceKind == CoreSpecialCardSourceKind.Component).Select(item => item.SourceId).ToArray());
            decimal before = session.ProfileGold;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                if (!CoreVoyageSession.IsSpecialBattleCard(CoreVoyageSession.CardLibrary[index].Id)) continue;
                Assert.That(session.GetCardUpgradeCost(index), Is.Zero);
                Assert.That(session.TryUpgradeBattleCard(index, out string reason), Is.False);
                Assert.That(reason, Does.Contain("航行"));
            }
            Assert.That(session.ProfileGold, Is.EqualTo(before));
            Assert.That(session.TryUpgradeBattleCard(0, out _), Is.True, "Base cards keep the paid three-tier upgrade route.");
        }

        [TestCase(0, 0, 120)]
        [TestCase(119, 0, 120)]
        [TestCase(120, 1, 360)]
        [TestCase(359, 1, 360)]
        [TestCase(360, 2, 0)]
        [TestCase(839, 2, 0)]
        [TestCase(840, 2, 0)]
        [TestCase(int.MaxValue, 2, 0)]
        public void Experience_ClampsTheAuthoredCapWithoutLosingCumulativeExperience(int xp, int level, int threshold)
        {
            CoreSpecialCardDefinition definition = Crew();
            CoreVoyageSession session = new CoreVoyageSession();
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { definition.Card.Id, xp } });
            int index = Find(definition.Card.Id);
            Assert.That(session.GetSpecialCardExperience(definition.Card.Id), Is.EqualTo(xp));
            Assert.That(session.GetSpecialCardNextThreshold(definition.Card.Id), Is.EqualTo(threshold));
            Assert.That(session.GetCardUpgradeLevel(definition.Card.Id), Is.EqualTo(level));
            Assert.That(session.GetBattleCard(index).UpgradeLevel, Is.EqualTo(level));
            Assert.That(definition.Card.Id, Is.EqualTo("card_special_crew_broken_anchor_navigator_lorne"));
            Assert.That(session.GetBattleCard(index).Damage, Is.Zero);
            Assert.That(session.GetBattleCard(index).Block, Is.EqualTo(new[] { 6, 8, 10 }[level]));
            Assert.That(session.GetBattleCard(index).Energy, Is.EqualTo(definition.Card.Energy));
            Assert.That(session.GetBattleCard(index).Draw, Is.EqualTo(1));
            session.GetBattleCardAtLevel(index, 3);
            Assert.That(session.GetCardUpgradeLevel(definition.Card.Id), Is.EqualTo(level), "Preview never grants experience.");
        }

        [Test]
        public void LegacyPaidUpgrades_MigrateOnceAndSurviveBothRestoreOrders()
        {
            CoreSpecialCardDefinition definition = Crew();
            string legacyId = CoreVoyageSession.CardLibrary.Take(32).First(card => card.RequiredCrew == definition.LegacySourceKey).Id;
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(new[] { definition.SourceId }, null);
            Dictionary<string, int> grades = new Dictionary<string, int> { { legacyId, 2 }, { "card_chain_shot", 1 } };
            session.ConfigureCardUpgrades(grades);
            session.RestoreSpecialCardExperience(new Dictionary<string, int>());
            Assert.That(session.GetSpecialCardExperience(legacyId), Is.EqualTo(360));
            Assert.That(session.GetSpecialCardExperience(definition.Card.Id), Is.EqualTo(360));
            Assert.That(session.CardUpgradeLevels.Keys, Is.EquivalentTo(new[] { "card_chain_shot" }));
            session.ConfigureCardUpgrades(grades);
            session.RestoreSpecialCardExperience(session.SpecialCardExperience);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { definition.Card.Id, 120 } });
            Assert.That(session.GetSpecialCardExperience(definition.Card.Id), Is.EqualTo(360));

            CoreVoyageSession reloaded = new CoreVoyageSession();
            reloaded.RestoreSpecialCardExperience(session.SpecialCardExperience);
            reloaded.ConfigureCardUpgrades(grades);
            reloaded.RestoreAcquiredSpecialCards(session.AcquiredSpecialCardIds);
            reloaded.ConfigureSpecialCardSources(new[] { definition.SourceId }, null);
            Assert.That(reloaded.GetSpecialCardExperience(definition.Card.Id), Is.EqualTo(360));
            Assert.That(reloaded.GetBattleCard(Find(definition.Card.Id)).UpgradeLevel, Is.EqualTo(2));
        }

        [Test]
        public void SameProfessionNewIdentities_DoNotShareNewVoyageExperience()
        {
            CoreSpecialCardDefinition first = Crew();
            CoreSpecialCardDefinition second = CoreSpecialCardCatalog.All.First(item => item.SourceKind == CoreSpecialCardSourceKind.Crew && item.SourceId != first.SourceId && item.LegacySourceKey == first.LegacySourceKey);
            CoreVoyageSession session = new CoreVoyageSession();
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { first.Card.Id, 900 } });
            session.ConfigureSpecialCardSources(new[] { second.SourceId }, null);
            Assert.That(session.GetSpecialCardExperience(second.Card.Id), Is.Zero);
            Assert.That(session.GetCardUpgradeLevel(first.Card.Id), Is.EqualTo(2));

            string legacyId = CoreVoyageSession.CardLibrary.Take(32).First(card => card.RequiredCrew == first.LegacySourceKey).Id;
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { legacyId, 1 } });
            Assert.That(session.GetSpecialCardExperience(second.Card.Id), Is.EqualTo(120));
            Assert.That(session.GetSpecialCardExperience(first.Card.Id), Is.EqualTo(900), "Migration is a historical floor, not an overwrite.");
        }

        [Test]
        public void Collection_OnlyAcceptsKnownSpecialCardsAndNeverInjectsUnequippedOnes()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            string id = Crew().Card.Id;
            session.AcquireSpecialCards(new[] { id, id, "card_chain_shot", "unknown", null });
            session.RestoreAcquiredSpecialCards(session.AcquiredSpecialCardIds);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { "unknown", 999 }, { "card_chain_shot", 999 }, { Part().Card.Id, -100 } });
            session.ConfigureSpecialCardSources(null, null);
            Assert.That(session.AcquiredSpecialCardIds, Is.EqualTo(new[] { id }));
            Assert.That(session.SpecialCardExperience, Is.Empty);
            Assert.That(AllBonus(session), Is.Empty);
        }

        [Test]
        public void RealSailing_GrantsOnlyActualDistanceToDepartureLockedCards()
        {
            CoreSpecialCardDefinition crew = Crew();
            CoreSpecialCardDefinition part = Part();
            CoreSpecialCardDefinition other = CoreSpecialCardCatalog.All.First(item => item.SourceKind == CoreSpecialCardSourceKind.Crew && item.SourceId != crew.SourceId);
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(new[] { crew.SourceId }, new[] { part.SourceId });
            session.AcquireSpecialCards(new[] { other.Card.Id });
            session.SetSeed("SPECIAL-CARDS-REAL-SAILING");
            session.StartVoyage();
            foreach (CoreMapNode node in session.Map.Nodes) node.Visited = true;
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, true);
            Assert.That(session.GetSpecialCardExperience(crew.Card.Id), Is.Zero, "Departure itself is not sailing.");
            session.ConfigureSpecialCardSources(new[] { other.SourceId }, null);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { crew.Card.Id, 9999 } });
            session.RestoreAcquiredSpecialCards(new[] { "card_special_crew_twilight_tide_gunner_yilan" });
            Assert.That(session.GetSpecialCardExperience(crew.Card.Id), Is.Zero, "Port-only APIs cannot mutate an active voyage.");
            for (int step = 0; step < 3; step++)
            {
                Assert.That(SailSomewhere(session), Is.True);
                int expected = (int)Math.Floor(session.TotalDistanceNm * CoreVoyageSession.SpecialCardExperiencePerNauticalMile);
                Assert.That(session.GetSpecialCardExperience(crew.Card.Id), Is.EqualTo(expected).Within(1));
                Assert.That(session.GetSpecialCardExperience(part.Card.Id), Is.EqualTo(session.GetSpecialCardExperience(crew.Card.Id)));
                Assert.That(session.GetSpecialCardExperience(other.Card.Id), Is.Zero);
                int before = session.GetSpecialCardExperience(crew.Card.Id);
                session.SetDistance(180);
                session.GetForecast();
                session.GetForecast();
                Assert.That(session.GetSpecialCardExperience(crew.Card.Id), Is.EqualTo(before), "Distance selection and UI forecast reads do not grant experience.");
            }
            session.SafeReturn();
            int atSettlement = session.GetSpecialCardExperience(crew.Card.Id);
            Assert.That(session.Sail(), Is.False);
            session.ReturnToPortAfterSettlement();
            session.ConfigureSpecialCardSources(null, null);
            Assert.That(session.GetSpecialCardExperience(crew.Card.Id), Is.EqualTo(atSettlement));
            Assert.That(session.AcquiredSpecialCardIds, Has.Member(crew.Card.Id));
        }

        [Test]
        public void RejectedSailAndPortActions_DoNotGrantExperience()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            string id = Crew().Card.Id;
            session.ConfigureSpecialCardSources(new[] { Crew().SourceId }, null);
            Assert.That(session.Sail(), Is.False);
            Award(session, 500f);
            Assert.That(session.GetSpecialCardExperience(id), Is.Zero);
            session.StartVoyage();
            session.Cargo.Water = 0;
            session.Cargo.Food = 0;
            Assert.That(session.Sail(), Is.False);
            Assert.That(session.GetSpecialCardExperience(id), Is.Zero);
            Assert.That(session.TotalDistanceNm, Is.Zero);
        }

        [Test]
        public void FractionalDistanceAndOverflow_AreSafeAndOnlyCrossThresholdsOnce()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            string id = Crew().Card.Id;
            session.ConfigureSpecialCardSources(new[] { Crew().SourceId }, null);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { id, 119 } });
            session.StartVoyage();
            Award(session, 1f);
            Award(session, 1f);
            Award(session, float.NaN);
            Award(session, float.PositiveInfinity);
            Award(session, -5f);
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(119));
            Award(session, 2f);
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(120));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(1));
            Award(session, float.MaxValue);
            Award(session, float.MaxValue);
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(int.MaxValue));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(2));
        }

        [Test]
        public void HistoricalSourceSequence_PreservesAll106OriginalIDsAndTheirLibraryPositions()
        {
            string[] originalSourceIds =
            {
                "crew_broken_anchor_navigator_lorne", "crew_broken_anchor_carpenter_kia", "crew_broken_anchor_gunner_mira",
                "crew_broken_anchor_diver_udo", "crew_white_shell_navigator_saina", "crew_white_shell_carpenter_atang",
                "crew_white_shell_gunner_gallo", "crew_white_shell_diver_kela", "crew_bronze_bell_navigator_vick",
                "crew_bronze_bell_carpenter_osser", "crew_bronze_bell_gunner_helen", "crew_bronze_bell_diver_duke",
                "crew_salt_crown_navigator_ashir", "crew_salt_crown_carpenter_saf", "crew_salt_crown_gunner_rokan",
                "crew_salt_crown_diver_isha", "crew_twilight_tide_navigator_selene", "crew_twilight_tide_carpenter_orlan",
                "crew_twilight_tide_gunner_yilan", "crew_twilight_tide_diver_naive", "crew_gray_gull_navigator_evelyn",
                "crew_gray_gull_carpenter_barlow", "crew_gray_gull_gunner_leisa", "crew_gray_gull_diver_noya",
                "bow_gilded_mermaid", "bow_sunlion_figurehead", "bow_pearl_siren",
                "bow_guildbreaker_crest", "bow_amber_albatross", "bow_abyssal_fang",
                "bow_nightkraken_tusk", "bow_gravecurrent_spur", "bow_blackreef_hammer",
                "bow_voidray_edge", "bow_leviathan_ram", "bow_crownless_colossus",
                "bow_worldsplitter", "bow_dragonwake_ram", "bow_last_horizon",
                "sail_guild_silk", "sail_emerald_trade_wing", "sail_sunthread_spinnaker",
                "sail_redroute_broadsail", "sail_whisperveil", "sail_moonweave",
                "sail_eclipse_shroud", "sail_starshear_rig", "sail_ghostcurrent_canvas",
                "sail_aurora_fan", "sail_tempest_wing", "sail_skybreaker_array",
                "sail_crown_of_gales", "sail_thunderphoenix", "sail_last_free_wind",
                "cannon_chain_repeater", "cannon_anchorbreaker", "cannon_redwake_repeater",
                "cannon_stormnail_array", "cannon_echochamber", "cannon_starfire_battery",
                "cannon_cometfall_battery", "cannon_blueflare_organ", "cannon_voidbell_mortar",
                "cannon_sunforge_salvo", "cannon_queens_broadside", "cannon_krakens_thunder",
                "cannon_worlds_end_volley", "cannon_dragonbreath_battery", "cannon_liberty_salute",
                "plating_gilded_bulwark", "plating_suncoin_rampart", "plating_coralsteel_hide",
                "plating_redbanner_wall", "plating_merchants_oath", "plating_moonsilver_skin",
                "plating_eclipse_mail", "plating_starseal_panels", "plating_ghostreef_carapace",
                "plating_aurora_laminate", "plating_living_hull", "plating_leviathan_scales",
                "plating_worldtree_bark", "plating_dragonwake_armor", "plating_freedom_ark",
                "instrument_guild_engine", "instrument_starledger", "instrument_corsair_decoder",
                "instrument_deepwater_abacus", "instrument_trade_wind_oracle", "instrument_lunar_astrolabe",
                "instrument_eclipse_orrery", "instrument_whalestar_receiver", "instrument_ghostmap_projector",
                "instrument_aurora_calculator", "instrument_allseeing_sextant", "instrument_eye_of_depths",
                "instrument_worldcompass", "instrument_celestial_engine", "instrument_last_horizon_lens",
                "crew_broken_anchor_doctor_hector", "crew_white_shell_doctor_mirei", "crew_bronze_bell_doctor_ada",
                "crew_salt_crown_doctor_naji", "crew_twilight_tide_doctor_viya", "crew_gray_gull_doctor_moer",
                "crew_gray_gull_cook_pepe",
            };
            Assert.That(CoreSpecialCardCatalog.All.Take(106).Select(value => value.SourceId), Is.EqualTo(originalSourceIds));
            int libraryIndex = CoreVoyageSession.LegacyBattleCardSlotCount;
            foreach (string sourceId in originalSourceIds)
            {
                if (libraryIndex == 56) libraryIndex++;
                Assert.That(CoreVoyageSession.CardLibrary[libraryIndex++].Id, Is.EqualTo("card_special_" + sourceId));
            }
        }

        [Test]
        public void HistoricalIdentityExperience_RestoresWithoutRedistributionOrExtraPrimaryCards()
        {
            CoreSpecialCardDefinition[] original = CoreSpecialCardCatalog.All.Take(106).ToArray();
            var experience = original.Select((value, index) => new { value.Card.Id, Experience = 17 * (index + 1) })
                .ToDictionary(value => value.Id, value => value.Experience);
            var session = new CoreVoyageSession();
            session.RestoreSpecialCardExperience(experience);
            session.ConfigureSpecialCardSources(original.Where(value => value.SourceKind == CoreSpecialCardSourceKind.Crew)
                .Select(value => value.SourceId).ToArray(), original.Where(value => value.SourceKind == CoreSpecialCardSourceKind.Component)
                .Select(value => value.SourceId).ToArray());
            Assert.That(session.SpecialCardExperience.Count, Is.EqualTo(106));
            foreach (CoreSpecialCardDefinition source in original)
            {
                Assert.That(session.GetSpecialCardExperience(source.Card.Id), Is.EqualTo(experience[source.Card.Id]), source.SourceId);
                Assert.That(CoreSpecialCardCatalog.All.Count(value => value.SourceId == source.SourceId), Is.EqualTo(1), source.SourceId);
            }
            foreach (CoreSpecialCardDefinition added in CoreSpecialCardCatalog.All.Skip(106))
                Assert.That(session.GetSpecialCardExperience(added.Card.Id), Is.Zero, added.SourceId);
        }

        private static CoreSpecialCardDefinition Crew() => CoreSpecialCardCatalog.All.First(item => item.SourceKind == CoreSpecialCardSourceKind.Crew && item.LegacySourceKey == "navigator");
        private static CoreSpecialCardDefinition Part() => CoreSpecialCardCatalog.All.First(item => item.SourceKind == CoreSpecialCardSourceKind.Component);
        private static int Find(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static int[] AllBonus(CoreVoyageSession session) => Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>().SelectMany(session.GetBonusBattleCards).Distinct().ToArray();
        private static void Award(CoreVoyageSession session, float distance) => typeof(CoreVoyageSession).GetMethod("AwardSpecialCardVoyageDistance", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, new object[] { distance });
        private static bool SailSomewhere(CoreVoyageSession session)
        {
            session.SetDistance(70);
            foreach (SailingDirection heading in Enum.GetValues(typeof(SailingDirection)))
            {
                session.SetHeading(heading);
                SailingForecast forecast = session.GetForecast();
                if (forecast != null && !forecast.Blocked && forecast.ActualDistanceNm >= 1f && session.Sail()) return true;
            }
            return false;
        }
    }
}
