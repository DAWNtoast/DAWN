using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class SpecialCardCatalogTests
    {
        [Test]
        public void Catalog_HasOneStableCardPerSourceAndImmutableCollections()
        {
            var all = CoreSpecialCardCatalog.All;
            Assert.That(all.Count, Is.EqualTo(126));
            Assert.That(all.Count(value => value.SourceKind == CoreSpecialCardSourceKind.Crew), Is.EqualTo(36));
            Assert.That(all.Count(value => value.SourceKind == CoreSpecialCardSourceKind.Component), Is.EqualTo(90));
            Assert.That(all.Count(value => value.Card.SchoolId.Length > 0), Is.EqualTo(72));
            Assert.That(all.Count(value => value.Card.SchoolId.Length == 0), Is.EqualTo(54));
            Assert.That(all.Select(value => value.SourceId).Distinct().Count(), Is.EqualTo(all.Count));
            Assert.That(all.Select(value => value.Card.Id).Distinct().Count(), Is.EqualTo(all.Count));
            Assert.That(all.Select(value => value.Card.Name).Distinct().Count(), Is.EqualTo(all.Count));
            Assert.Throws<NotSupportedException>(() => ((IList<CoreSpecialCardDefinition>)all).Add(all[0]));
            foreach (CoreSpecialCardDefinition definition in all)
            {
                Assert.That(definition.Card.Id, Is.EqualTo("card_special_" + definition.SourceId));
                Assert.That(CoreSpecialCardCatalog.FindByCardId(definition.Card.Id), Is.SameAs(definition));
                Assert.That(definition.SourceName, Is.Not.Empty);
                Assert.That(definition.Lore.Length, Is.GreaterThanOrEqualTo(40), definition.Card.Id);
                Assert.That(Regex.IsMatch(definition.StoryId, "^seventh_berth_(0[1-9]|1[0-5])$"), Is.True);
                Assert.That(definition.Card.Description, Is.Not.Empty);
                Assert.That(definition.LegacySourceKey, Is.Not.Empty);
                if (definition.SourceKind == CoreSpecialCardSourceKind.Crew)
                {
                    Assert.That(definition.Card.RequiredCrew, Is.EqualTo(definition.SourceId));
                    Assert.That(definition.Card.RequiredComponent, Is.Empty);
                    Assert.That(CoreSpecialCardCatalog.ForComponent(definition.SourceId), Is.Null);
                }
                else
                {
                    Assert.That(definition.Card.RequiredComponent, Is.EqualTo(definition.SourceId));
                    Assert.That(definition.Card.RequiredCrew, Is.Empty);
                    Assert.That(CoreSpecialCardCatalog.ForCrew(definition.SourceId), Is.Null);
                }
            }
        }

        [Test]
        public void EquipmentCoverage_MatchesEveryBlueOrHigherExpandedIdentityAndItsExistingStory()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            Assert.That(catalog.ExpandedEntries.Count, Is.EqualTo(150));
            var expected = new HashSet<string>();
            foreach (ComponentArtEntry item in catalog.ExpandedEntries)
            {
                string id = item.Id;
                bool eligible = item.Rarity >= ComponentRarity.Rare;
                CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(id);
                if (!eligible)
                {
                    Assert.That(definition, Is.Null, "White/green equipment cannot grant special cards: " + id);
                    continue;
                }
                expected.Add(id);
                Assert.That(definition, Is.Not.Null, id);
                Assert.That(definition.SourceName, Is.EqualTo(item.Name), id);
                Assert.That(definition.StoryId, Is.EqualTo(item.StoryId), id);
                Assert.That(definition.Card.SchoolId.Length > 0, Is.EqualTo(item.Rarity >= ComponentRarity.Epic), id);
            }
            Assert.That(expected.Count, Is.EqualTo(90));
            Assert.That(catalog.ExpandedEntries.Count(item => item.Rarity == ComponentRarity.Rare), Is.EqualTo(30));
            CollectionAssert.AreEquivalent(expected, CoreSpecialCardCatalog.All
                .Where(value => value.SourceKind == CoreSpecialCardSourceKind.Component).Select(value => value.SourceId));
        }

        [Test]
        public void EachSchool_UsesTwoActualCrewAndOneEpicAndLegendaryPerEquipmentSlot()
        {
            ComponentArtCatalog equipment = new ComponentArtCatalog();
            foreach (CoreSchoolUnlockDefinition school in CoreSchoolUnlockCatalog.All)
            {
                CoreSpecialCardDefinition[] sources = CoreSpecialCardCatalog.All
                    .Where(value => value.Card.SchoolId == school.Id).ToArray();
                Assert.That(sources.Length, Is.EqualTo(12), school.Id);
                Assert.That(sources.Count(value => value.SourceKind == CoreSpecialCardSourceKind.Crew), Is.EqualTo(2), school.Id);
                foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                    foreach (ComponentRarity rarity in new[] { ComponentRarity.Epic, ComponentRarity.Legendary })
                        Assert.That(sources.Count(value => value.SourceKind == CoreSpecialCardSourceKind.Component &&
                            equipment.GetById(value.SourceId)?.Slot == slot && equipment.GetById(value.SourceId)?.Rarity == rarity),
                            Is.EqualTo(1), school.Id + " / " + slot + " / " + rarity);
            }
        }

        [Test]
        public void CrewCoverage_UsesActualRegionalIdentitiesAndTheirExistingStoryChapters()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Content/Runtime/CrewNarrativeCatalog.cs"));
            MatchCollection matches = Regex.Matches(source,
                "Crew\\(\"(?<id>crew_[^\"]+)\", \"[^\"]+\", (?<profession>[0-5]), \"(?<name>[^\"]+)\"(?<tail>[^\\r\\n]+)");
            Assert.That(matches.Count, Is.EqualTo(36));
            var expected = new HashSet<string>();
            foreach (Match match in matches)
            {
                string id = match.Groups["id"].Value;
                CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(id);
                expected.Add(id);
                Assert.That(definition, Is.Not.Null, id);
                Assert.That(definition.SourceName, Is.EqualTo(match.Groups["name"].Value), id);
                Assert.That(definition.StoryId, Is.EqualTo(Regex.Match(match.Groups["tail"].Value, "seventh_berth_[0-9]{2}").Value), id);
            }
            CollectionAssert.AreEquivalent(expected, CoreSpecialCardCatalog.All
                .Where(value => value.SourceKind == CoreSpecialCardSourceKind.Crew).Select(value => value.SourceId));
        }

        [TestCase("bow", "rare")]
        [TestCase("bow", "epic")]
        [TestCase("bow", "legendary")]
        [TestCase("sail", "rare")]
        [TestCase("sail", "epic")]
        [TestCase("sail", "legendary")]
        [TestCase("cannon", "rare")]
        [TestCase("cannon", "epic")]
        [TestCase("cannon", "legendary")]
        [TestCase("plating", "rare")]
        [TestCase("plating", "epic")]
        [TestCase("plating", "legendary")]
        [TestCase("instrument", "rare")]
        [TestCase("instrument", "epic")]
        [TestCase("instrument", "legendary")]
        public void EveryEligibleSlotRarity_HasSixDistinctCollectibleCards(string slot, string rarity)
        {
            ComponentSlot targetSlot = (ComponentSlot)Enum.Parse(typeof(ComponentSlot), slot, true);
            ComponentRarity targetRarity = (ComponentRarity)Enum.Parse(typeof(ComponentRarity), rarity, true);
            var ids = new ComponentArtCatalog().ExpandedEntries
                .Where(item => item.Slot == targetSlot && item.Rarity == targetRarity).Select(item => item.Id).ToArray();
            Assert.That(ids.Length, Is.EqualTo(6));
            Assert.That(ids.All(id => CoreSpecialCardCatalog.ForComponent(id) != null), Is.True);
            Assert.That(ids.Select(id => Signature(CoreSpecialCardCatalog.ForComponent(id).Card)).Distinct().Count(), Is.GreaterThanOrEqualTo(4));
        }

        [TestCase("navigator")]
        [TestCase("carpenter")]
        [TestCase("doctor")]
        [TestCase("gunner")]
        [TestCase("cook")]
        [TestCase("diver")]
        public void SameProfessionAcrossSixPorts_PreservesAuthoredMechanicGroupsAndUniqueStories(string profession)
        {
            var definitions = CoreSpecialCardCatalog.All.Where(value => value.SourceKind == CoreSpecialCardSourceKind.Crew &&
                value.SourceId.Contains("_" + profession + "_")).ToArray();
            Assert.That(definitions.Length, Is.EqualTo(6));
            string[] expectedSharedGroups = Array.Empty<string>();
            string[] sharedGroups = definitions.GroupBy(value => Signature(value.Card))
                .Where(group => group.Count() > 1)
                .Select(group => string.Join("|", group.Select(value => value.SourceId).OrderBy(id => id, StringComparer.Ordinal)))
                .ToArray();
            Assert.That(sharedGroups, Is.EquivalentTo(expectedSharedGroups),
                "Each regional source now has a distinct tactical expression.");
            Assert.That(definitions.Select(value => value.Lore).Distinct().Count(), Is.EqualTo(6));
        }

        [Test]
        public void AuthoredRules_StayInsideImplementedStagesActionsAndExistingArt()
        {
            var knownArt = new HashSet<string> { "card_cannon_barrage", "card_full_sail_turn", "card_boarding_hook",
                "card_cutlass_raid", "card_emergency_repair", "card_brace" };
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                CoreBattleCard card = definition.Card;
                Assert.That(knownArt.Contains(card.ArtId) || card.ArtId.StartsWith("card_school_", StringComparison.Ordinal), Is.True, card.Id);
                Assert.That(card.Energy, Is.InRange(0, 3), card.Id);
                Assert.That(card.UpgradeLevel, Is.Zero, card.Id);
                Assert.That(card.Damage, Is.GreaterThanOrEqualTo(0), card.Id);
                Assert.That(card.Block, Is.GreaterThanOrEqualTo(0), card.Id);
                Assert.That(card.Repair, Is.GreaterThanOrEqualTo(0), card.Id);
                if (card.Id == "card_special_instrument_tradeledger_oracle")
                    Assert.That(card.Draw, Is.EqualTo(3), card.Id);
                else Assert.That(card.Draw, Is.InRange(0, 2), card.Id);
                Assert.That(Enum.IsDefined(typeof(NavalBattleCardAction), card.Action), Is.True, card.Id);
                Assert.That(card.AllowedStages, Is.Not.EqualTo(NavalBattleStages.None), card.Id);
                Assert.That(card.IsAllowedInStage(NavalBattleStage.Preparation) && card.Damage > 0, Is.False, card.Id);
                if (card.Action == NavalBattleCardAction.Cannon)
                {
                    Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Naval), card.Id);
                    int expectedPowder = card.SchoolId == "fire" ? card.ArtId == "card_school_fire_l03" ? 3 : 2 : 1;
                    Assert.That(card.PowderCost, Is.EqualTo(expectedPowder), card.Id);
                    Assert.That(card.CannonballCost, Is.EqualTo(1), card.Id);
                    StringAssert.Contains("4 格", card.Description);
                }
                else
                {
                    Assert.That(card.CannonballCost, Is.Zero, card.Id);
                    CoreSchoolMechanic mechanic = card.SchoolEffect?.Mechanic ?? CoreSchoolMechanic.None;
                    bool loading = card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad;
                    bool powderFireEffect = mechanic == CoreSchoolMechanic.FireLoad || mechanic == CoreSchoolMechanic.FireIgnite ||
                        mechanic == CoreSchoolMechanic.FireExtend || mechanic == CoreSchoolMechanic.FireBoardingCover ||
                        mechanic == CoreSchoolMechanic.FireNaturalGuard || mechanic == CoreSchoolMechanic.FireScorch ||
                        mechanic == CoreSchoolMechanic.FireOverflow;
                    int expectedPowder = card.ArtId == "card_school_fire_l01" ? 2 :
                        loading || powderFireEffect || mechanic == CoreSchoolMechanic.FireIgniteOrExtend ||
                        card.BoardingSpec?.Key == "fire_05" ? 1 : 0;
                    Assert.That(card.PowderCost, Is.EqualTo(expectedPowder), card.Id);
                }
                if (card.Action == NavalBattleCardAction.Sabotage)
                {
                    Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Naval), card.Id);
                    StringAssert.Contains("150 米", card.Description);
                }
                if (card.AllowedStages == NavalBattleStages.Boarding)
                    Assert.That(card.Action, Is.EqualTo(NavalBattleCardAction.Standard), card.Id);
            }
            Assert.That(CoreSpecialCardCatalog.All.Select(value => Signature(value.Card)).Distinct().Count(), Is.GreaterThanOrEqualTo(50));
        }

        [Test]
        public void BlueFireGunner_KeepsHerAuthoredIdentityAndFireRoundMechanics()
        {
            CoreSpecialCardDefinition yilan = CoreSpecialCardCatalog.ForCrew("crew_twilight_tide_gunner_yilan");
            Assert.That(yilan.SourceName, Is.EqualTo("蓝火炮巫 伊岚"));
            Assert.That(yilan.Card.Action, Is.EqualTo(NavalBattleCardAction.Standard));
            Assert.That(yilan.Card.AllowedStages, Is.EqualTo(NavalBattleStages.Ship));
            Assert.That(yilan.Card.SchoolId, Is.EqualTo("fire"));
            Assert.That(yilan.Card, Is.SameAs(CoreSchoolCardCatalog.ForSource(yilan.SourceId)));
            Assert.That(yilan.Card.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.FireLoad));
            Assert.That(yilan.Card.SchoolEffect.Secondary, Is.EqualTo(3));
            Assert.That(yilan.Card.SchoolEffect.Power, Is.EqualTo(6));
            Assert.That(yilan.Card.Draw, Is.EqualTo(1));
            Assert.That(yilan.Card.PowderCost, Is.EqualTo(1));
            Assert.That(yilan.Card.SchoolEffect.Duration, Is.EqualTo(2));
            Assert.That(yilan.LegacySourceKey, Is.EqualTo("gunner"));
            Assert.That(yilan.StoryId, Is.EqualTo("seventh_berth_05"));
            StringAssert.Contains("命中", yilan.Lore);
        }

        [TestCase("crew_broken_anchor_doctor_hector", "blood_09", 2, 12, 0, 0)]
        [TestCase("crew_white_shell_doctor_mirei", "tide_12", 3, 16, 0, 0)]
        [TestCase("crew_bronze_bell_doctor_ada", "basic_06", 2, 12, 9, 0)]
        [TestCase("crew_salt_crown_doctor_naji", "basic_07", 3, 34, 8, 0)]
        [TestCase("crew_twilight_tide_doctor_viya", "arcane_05", 1, 17, 0, 0)]
        [TestCase("crew_gray_gull_doctor_moer", "basic_06", 1, 10, 5, 1)]
        public void DoctorCards_AreCrewHealingOnlyAndUseAuthoredProgression(string crewId, string boardingKey,
            int maximum, int finalRepair, int finalBlock, int finalDraw)
        {
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(crewId);
            CoreBattleCard card = definition.Card;
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(value => CoreVoyageSession.CardLibrary[value].Id == card.Id);
            var session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(new[] { crewId }, null);

            Assert.That(definition.LegacySourceKey, Is.EqualTo("doctor"));
            if (card.SchoolId.Length == 0)
            {
                Assert.That(card.Action, Is.EqualTo(NavalBattleCardAction.Standard));
                Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Boarding));
                Assert.That(card.Damage, Is.Zero);
                Assert.That(card.Repair, Is.GreaterThan(0));
                StringAssert.Contains("船员生命", card.Description);
                StringAssert.DoesNotContain("船体", card.Description);
            }
            else
            {
                Assert.That(card, Is.SameAs(CoreSchoolCardCatalog.ForSource(crewId)));
                Assert.That(card.BoardingSpec, Is.Not.Null);
            }
            Assert.That(card.CannonballCost + card.PowderCost, Is.Zero);
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                Assert.That(session.GetBonusBattleCards(stage).Contains(index), Is.EqualTo(card.IsAllowedInStage(stage)));

            // Source metadata retains its saved identity; progression uses the current boarding design.
            CoreBattleCard current = session.GetBattleCardAtLevel(index, 0);
            Assert.That(current.Id, Is.EqualTo(card.Id));
            Assert.That(current.RequiredCrew, Is.EqualTo(card.RequiredCrew));
            Assert.That(current.ArtId, Is.EqualTo(card.ArtId));
            if (card.SchoolId.Length > 0)
            {
                Assert.That(current.BoardingSpec, Is.Not.Null);
                Assert.That(current.BoardingSpec.Key, Is.EqualTo(boardingKey));
                Assert.That(current.Energy, Is.EqualTo(Math.Max(0, CoreBoardingCardCatalog.FindByKey(boardingKey).Energy - 1)));
            }
            else Assert.That(current.BoardingSpec, Is.Null, "Neutral crew keep their complete authored heal/guard/draw combination.");
            Assert.That(current.AllowedStages, Is.EqualTo(NavalBattleStages.Boarding));
            Assert.That(current.Damage, Is.Zero);
            Assert.That(current.Repair, Is.GreaterThan(0));
            Assert.That(current.CannonballCost + current.PowderCost, Is.Zero);
            // Saved high ranks resolve to each doctor's independently authored ceiling.
            CoreBattleCard ranked = session.GetBattleCardAtLevel(index, 3);
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(maximum));
            Assert.That(ranked.UpgradeLevel, Is.EqualTo(maximum));
            Assert.That(ranked.Repair, Is.EqualTo(finalRepair));
            Assert.That(ranked.Block, Is.EqualTo(finalBlock));
            Assert.That(ranked.Energy, Is.EqualTo(current.Energy));
            Assert.That(ranked.Draw, Is.EqualTo(finalDraw));
            Assert.That(ranked.Damage, Is.Zero);
            Assert.That(ranked.SchoolId, Is.EqualTo(current.SchoolId));
            Assert.That(ranked.BoardingSpec?.Key, Is.EqualTo(current.BoardingSpec?.Key));
            Assert.That(ranked.SchoolEffect, Is.Null, "Current boarding effects are defined by BoardingSpec.");
            StringAssert.Contains("恢复" + ranked.Repair, ranked.Description.Replace(" ", string.Empty));
            StringAssert.DoesNotContain("船体", ranked.Description);

            session.ConfigureSpecialCardSources(Array.Empty<string>(), null);
            Assert.That(session.GetBonusBattleCards(NavalBattleStage.Boarding), Has.No.Member(index));
            Assert.That(session.AcquiredSpecialCardIds, Has.Member(card.Id), "Standby must not erase a collected card.");
        }

        [Test]
        public void DoctorCards_AreAppendedAfterExistingCardsWithoutMovingSavedIndexes()
        {
            Assert.That(CoreSpecialCardCatalog.All.Take(99).Any(value => value.SourceId.Contains("_doctor_")), Is.False);
            Assert.That(CoreSpecialCardCatalog.All.Skip(99).Take(6).All(value => value.SourceId.Contains("_doctor_")), Is.True);
            Assert.That(CoreVoyageSession.CardLibrary[131].Id, Is.EqualTo("card_special_instrument_last_horizon_lens"));
            Assert.That(CoreVoyageSession.CardLibrary[132].Id, Is.EqualTo("card_special_crew_broken_anchor_doctor_hector"));
            Assert.That(CoreVoyageSession.CardLibrary[137].Id, Is.EqualTo("card_special_crew_gray_gull_doctor_moer"));
        }

        [Test]
        public void PepeCard_KeepsItsSavedPositionAndExistingDefenseDrawProgression()
        {
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew("crew_gray_gull_cook_pepe");
            Assert.That(CoreSpecialCardCatalog.All[105], Is.SameAs(definition));
            Assert.That(CoreVoyageSession.CardLibrary[138].Id, Is.EqualTo(definition.Card.Id),
                "The new cook card follows all surviving saved library positions.");
            Assert.That(definition.SourceName, Is.EqualTo("厨师 佩佩"));
            Assert.That(definition.StoryId, Is.EqualTo("seventh_berth_10"));
            Assert.That(definition.LegacySourceKey, Is.EqualTo("cook"));
            CoreBattleCard card = definition.Card;
            Assert.That(card.Action, Is.EqualTo(NavalBattleCardAction.Standard));
            Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.All));
            Assert.That(card.ArtId, Is.EqualTo("card_brace"));
            Assert.That(card.Energy, Is.EqualTo(1));
            Assert.That(card.Block, Is.EqualTo(6));
            Assert.That(card.BoardingBlock, Is.Null);
            Assert.That(card.Draw, Is.EqualTo(1));
            Assert.That(card.Repair, Is.EqualTo(3));
            Assert.That(card.Damage + card.PowderCost + card.CannonballCost, Is.Zero);
            Assert.That(CoreSpecialCardCatalog.All.Count(value => value.SourceId.Contains("_cook_")), Is.EqualTo(6));

            var session = new CoreVoyageSession();
            Assert.That(session.GetCardMaximumUpgradeLevel(138), Is.EqualTo(1));
            for (int rank = 0; rank <= 3; rank++)
            {
                CoreBattleCard ranked = session.GetBattleCardAtLevel(138, rank);
                Assert.That(ranked.UpgradeLevel, Is.EqualTo(Math.Min(rank, 1)));
                Assert.That(ranked.Block, Is.EqualTo(rank == 0 ? 6 : 8));
                Assert.That(ranked.BoardingBlock, Is.Null);
                Assert.That(ranked.Energy, Is.EqualTo(1));
                Assert.That(ranked.Draw, Is.EqualTo(1));
                Assert.That(ranked.Damage, Is.Zero);
                Assert.That(ranked.Repair, Is.EqualTo(rank == 0 ? 3 : 4));
                StringAssert.Contains("获得 " + ranked.Block + " 格挡", ranked.Description);
                StringAssert.Contains("抽 " + ranked.Draw + " 张", ranked.Description);
            }
        }

        [TestCase("cook")]
        [TestCase("crew_salt_pot_cook")]
        [TestCase("crew_cook_pepe")]
        [TestCase("tavern_crew_4")]
        public void PepeAliases_PreserveOldCookExperienceOnTheOneCookCard(string alias)
        {
            CoreSpecialCardDefinition pepe = CoreSpecialCardCatalog.ForCrew("crew_gray_gull_cook_pepe");
            Assert.That(CoreSpecialCardCatalog.ForCrew(alias), Is.SameAs(pepe));
            var session = new CoreVoyageSession();
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { "card_cook_rations", 840 } });
            session.ConfigureSpecialCardSources(new[] { alias }, null);
            Assert.That(session.AcquiredSpecialCardIds, Has.Member(pepe.Card.Id));
            Assert.That(session.GetSpecialCardExperience(pepe.Card.Id), Is.EqualTo(840));
            Assert.That(session.GetCardUpgradeLevel(pepe.Card.Id), Is.EqualTo(1));
        }

        [TestCase("doctor")]
        [TestCase("crew_cold_doctor")]
        [TestCase("crew_doctor_moer")]
        [TestCase("tavern_crew_2")]
        public void DoctorAliases_ResolveMoerAndMigrateOnlyTheAcquiredIdentity(string alias)
        {
            CoreSpecialCardDefinition moer = CoreSpecialCardCatalog.ForCrew("crew_gray_gull_doctor_moer");
            Assert.That(CoreSpecialCardCatalog.ForCrew(alias), Is.SameAs(moer));
            var session = new CoreVoyageSession();
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { "card_doctor_sutures", 360 } });
            session.ConfigureSpecialCardSources(new[] { alias }, null);
            Assert.That(session.AcquiredSpecialCardIds, Has.Member(moer.Card.Id));
            Assert.That(session.GetSpecialCardExperience(moer.Card.Id), Is.EqualTo(360));
            Assert.That(session.GetCardUpgradeLevel(moer.Card.Id), Is.EqualTo(1));
            foreach (CoreSpecialCardDefinition other in CoreSpecialCardCatalog.All.Where(value =>
                value.SourceId.Contains("_doctor_") && value.SourceId != moer.SourceId))
            {
                Assert.That(session.AcquiredSpecialCardIds, Has.No.Member(other.Card.Id));
                Assert.That(session.GetSpecialCardExperience(other.Card.Id), Is.Zero);
            }
        }

        [TestCase("navigator", "crew_gray_gull_navigator_evelyn")]
        [TestCase("carpenter", "crew_gray_gull_carpenter_barlow")]
        [TestCase("doctor", "crew_gray_gull_doctor_moer")]
        [TestCase("gunner", "crew_gray_gull_gunner_leisa")]
        [TestCase("cook", "crew_gray_gull_cook_pepe")]
        [TestCase("diver", "crew_gray_gull_diver_noya")]
        public void LegacyAliases_ResolveOneGrayGullIdentityWithoutDuplicateRecords(string alias, string expected)
        {
            Assert.That(CoreSpecialCardCatalog.ForCrew(alias), Is.SameAs(CoreSpecialCardCatalog.ForCrew(expected)));
            Assert.That(CoreSpecialCardCatalog.ForCrew(alias).LegacySourceKey, Is.EqualTo(alias));
            Assert.That(CoreSpecialCardCatalog.All.Count(value => value.SourceId == alias), Is.Zero);
        }

        [Test]
        public void UnknownOrIneligibleSources_DoNotInventSpecialCards()
        {
            foreach (string id in new[] { null, "", "unknown", "crew_white_shell_cook_missing", "bow", "cannon", "instrument", "sail", "plating" })
            {
                Assert.That(CoreSpecialCardCatalog.ForCrew(id), Is.Null);
                Assert.That(CoreSpecialCardCatalog.ForComponent(id), Is.Null);
                Assert.That(CoreSpecialCardCatalog.FindByCardId(id), Is.Null);
            }
        }

        private static string Signature(CoreBattleCard card) => string.Join("|", new object[]
        {
            card.Action, card.AllowedStages, card.Energy, card.Damage, card.Block,
            card.Repair, card.Draw, card.PowderCost, card.CannonballCost, card.BoardingBlock,
            card.SchoolId, card.SchoolEffect?.Mechanic, card.SchoolEffect?.Power,
            card.SchoolEffect?.Secondary, card.SchoolEffect?.Threshold, card.SchoolEffect?.Duration,
            card.SchoolEffect?.Limit, card.SchoolEffect?.Movement, card.SchoolEffect?.WoodCost,
            card.SchoolEffect?.FallbackDamage, card.SchoolEffect?.FallbackBlock, card.BoardingSpec?.Key,
        });
    }
}
