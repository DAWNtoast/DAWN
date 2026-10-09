using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class SpecialCardCatalogTests
    {
        [Test]
        public void Catalog_HasOneStableCardPerSourceAndImmutableCollections()
        {
            var all = CoreSpecialCardCatalog.All;
            Assert.That(all.Count, Is.EqualTo(105));
            Assert.That(all.Count(value => value.SourceKind == CoreSpecialCardSourceKind.Crew), Is.EqualTo(30));
            Assert.That(all.Count(value => value.SourceKind == CoreSpecialCardSourceKind.Component), Is.EqualTo(75));
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
        public void EquipmentCoverage_MatchesEveryBlueOrHigherCsvIdentityAndItsExistingStory()
        {
            string[] lines = File.ReadAllLines(Path.Combine(Application.dataPath, "_Project/Resources/Data/component_catalog_v2.csv"));
            string[] headers = lines[0].Split(',');
            int idColumn = Array.IndexOf(headers, "component_id");
            int nameColumn = Array.IndexOf(headers, "name");
            int rarityColumn = Array.IndexOf(headers, "rarity");
            int storyColumn = Array.IndexOf(headers, "story_id");
            var expected = new HashSet<string>();
            foreach (string line in lines.Skip(1).Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                string[] fields = line.Split(',');
                string id = fields[idColumn];
                bool eligible = fields[rarityColumn] == "rare" || fields[rarityColumn] == "epic" || fields[rarityColumn] == "legendary";
                CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(id);
                if (!eligible)
                {
                    Assert.That(definition, Is.Null, "White/green equipment cannot grant special cards: " + id);
                    continue;
                }
                expected.Add(id);
                Assert.That(definition, Is.Not.Null, id);
                Assert.That(definition.SourceName, Is.EqualTo(fields[nameColumn]), id);
                Assert.That(definition.StoryId, Is.EqualTo(fields[storyColumn]), id);
            }
            CollectionAssert.AreEquivalent(expected, CoreSpecialCardCatalog.All
                .Where(value => value.SourceKind == CoreSpecialCardSourceKind.Component).Select(value => value.SourceId));
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
                int profession = int.Parse(match.Groups["profession"].Value);
                CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(id);
                if (profession == 4)
                {
                    Assert.That(definition, Is.Null, "Cooking remains a voyage passive, not an automatic combat card: " + id);
                    continue;
                }
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
        public void EveryEligibleSlotRarity_HasFiveDistinctCollectibleCards(string slot, string rarity)
        {
            string[] lines = File.ReadAllLines(Path.Combine(Application.dataPath, "_Project/Resources/Data/component_catalog_v2.csv"));
            var ids = lines.Skip(1).Select(line => line.Split(','))
                .Where(fields => fields.Length > 3 && fields[2] == slot && fields[3] == rarity).Select(fields => fields[0]).ToArray();
            Assert.That(ids.Length, Is.EqualTo(5));
            Assert.That(ids.All(id => CoreSpecialCardCatalog.ForComponent(id) != null), Is.True);
            Assert.That(ids.Select(id => Signature(CoreSpecialCardCatalog.ForComponent(id).Card)).Distinct().Count(), Is.GreaterThanOrEqualTo(4));
        }

        [TestCase("navigator")]
        [TestCase("carpenter")]
        [TestCase("doctor")]
        [TestCase("gunner")]
        [TestCase("diver")]
        public void SameProfessionAcrossSixPorts_HasDistinctMechanicsNotRenamedCopies(string profession)
        {
            var definitions = CoreSpecialCardCatalog.All.Where(value => value.SourceKind == CoreSpecialCardSourceKind.Crew &&
                value.SourceId.Contains("_" + profession + "_")).ToArray();
            Assert.That(definitions.Length, Is.EqualTo(6));
            Assert.That(definitions.Select(value => Signature(value.Card)).Distinct().Count(), Is.EqualTo(6));
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
                Assert.That(knownArt.Contains(card.ArtId), Is.True, card.Id);
                Assert.That(card.Energy, Is.InRange(1, 2), card.Id);
                Assert.That(card.UpgradeLevel, Is.Zero, card.Id);
                Assert.That(card.Damage, Is.GreaterThanOrEqualTo(0), card.Id);
                Assert.That(card.Block, Is.GreaterThanOrEqualTo(0), card.Id);
                Assert.That(card.Repair, Is.GreaterThanOrEqualTo(0), card.Id);
                Assert.That(card.Draw, Is.InRange(0, 2), card.Id);
                Assert.That(Enum.IsDefined(typeof(NavalBattleCardAction), card.Action), Is.True, card.Id);
                Assert.That(card.AllowedStages, Is.Not.EqualTo(NavalBattleStages.None), card.Id);
                Assert.That(card.IsAllowedInStage(NavalBattleStage.Preparation) && card.Damage > 0, Is.False, card.Id);
                if (card.Action == NavalBattleCardAction.Cannon)
                {
                    Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Naval), card.Id);
                    Assert.That(card.CannonballCost, Is.EqualTo(1), card.Id);
                    StringAssert.Contains("350 米", card.Description);
                }
                else
                {
                    Assert.That(card.CannonballCost, Is.Zero, card.Id);
                    Assert.That(card.PowderCost, Is.Zero, card.Id);
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
            Assert.That(yilan.Card.Action, Is.EqualTo(NavalBattleCardAction.FireRounds));
            Assert.That(yilan.Card.AllowedStages, Is.EqualTo(NavalBattleStages.Ship));
            Assert.That(yilan.Card.StrategyAmount, Is.EqualTo(2));
            Assert.That(yilan.Card.BurnDamage, Is.EqualTo(5));
            Assert.That(yilan.LegacySourceKey, Is.EqualTo("gunner"));
            Assert.That(yilan.StoryId, Is.EqualTo("seventh_berth_05"));
            StringAssert.Contains("命中", yilan.Lore);
        }

        [TestCase("crew_broken_anchor_doctor_hector")]
        [TestCase("crew_white_shell_doctor_mirei")]
        [TestCase("crew_bronze_bell_doctor_ada")]
        [TestCase("crew_salt_crown_doctor_naji")]
        [TestCase("crew_twilight_tide_doctor_viya")]
        [TestCase("crew_gray_gull_doctor_moer")]
        public void DoctorCards_AreCrewHealingOnlyAndUseExistingProgression(string crewId)
        {
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(crewId);
            CoreBattleCard card = definition.Card;
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(value => CoreVoyageSession.CardLibrary[value].Id == card.Id);
            var session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(new[] { crewId }, null);

            Assert.That(definition.LegacySourceKey, Is.EqualTo("doctor"));
            Assert.That(card.Action, Is.EqualTo(NavalBattleCardAction.Standard));
            Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Boarding));
            Assert.That(card.Damage, Is.Zero);
            Assert.That(card.Repair, Is.GreaterThan(0));
            Assert.That(card.CannonballCost + card.PowderCost, Is.Zero);
            StringAssert.Contains("船员生命", card.Description);
            StringAssert.DoesNotContain("船体", card.Description);
            Assert.That(session.GetBonusBattleCards(NavalBattleStage.Preparation), Has.No.Member(index));
            Assert.That(session.GetBonusBattleCards(NavalBattleStage.Naval), Has.No.Member(index));
            Assert.That(session.GetBonusBattleCards(NavalBattleStage.Boarding), Has.Member(index));

            // These cards share the existing upgrade path, including a useful but bounded third rank.
            CoreBattleCard ranked = session.GetBattleCardAtLevel(index, 3);
            Assert.That(ranked.Repair, Is.EqualTo(card.Repair * 2));
            Assert.That(ranked.Block, Is.EqualTo(card.Block * 2));
            Assert.That(ranked.Energy, Is.EqualTo(card.Energy - 1));
            Assert.That(ranked.Draw, Is.EqualTo(card.Draw + 1));
            Assert.That(ranked.Damage, Is.Zero);
            StringAssert.Contains("恢复船员", ranked.Description);
            StringAssert.DoesNotContain("船体", ranked.Description);

            session.ConfigureSpecialCardSources(Array.Empty<string>(), null);
            Assert.That(session.GetBonusBattleCards(NavalBattleStage.Boarding), Has.No.Member(index));
            Assert.That(session.AcquiredSpecialCardIds, Has.Member(card.Id), "Standby must not erase a collected card.");
        }

        [Test]
        public void DoctorCards_AreAppendedAfterExistingCardsWithoutMovingSavedIndexes()
        {
            Assert.That(CoreSpecialCardCatalog.All.Take(99).Any(value => value.SourceId.Contains("_doctor_")), Is.False);
            Assert.That(CoreSpecialCardCatalog.All.Skip(99).All(value => value.SourceId.Contains("_doctor_")), Is.True);
            Assert.That(CoreVoyageSession.CardLibrary[131].Id, Is.EqualTo("card_special_instrument_last_horizon_lens"));
            Assert.That(CoreVoyageSession.CardLibrary[132].Id, Is.EqualTo("card_special_crew_broken_anchor_doctor_hector"));
            Assert.That(CoreVoyageSession.CardLibrary[137].Id, Is.EqualTo("card_special_crew_gray_gull_doctor_moer"));
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
            Assert.That(session.GetCardUpgradeLevel(moer.Card.Id), Is.EqualTo(2));
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
            foreach (string id in new[] { null, "", "unknown", "cook", "bow", "cannon", "instrument", "sail", "plating" })
            {
                Assert.That(CoreSpecialCardCatalog.ForCrew(id), Is.Null);
                Assert.That(CoreSpecialCardCatalog.ForComponent(id), Is.Null);
                Assert.That(CoreSpecialCardCatalog.FindByCardId(id), Is.Null);
            }
        }

        private static string Signature(CoreBattleCard card) => string.Join("|", new object[]
        {
            card.Action, card.AllowedStages, card.Energy, card.Damage, card.Block,
            card.Repair, card.Draw, card.PowderCost, card.CannonballCost,
        });
    }
}
