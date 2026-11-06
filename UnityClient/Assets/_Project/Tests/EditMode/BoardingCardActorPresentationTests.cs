using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class BoardingCardActorPresentationTests
    {
        // Reviewed intent of the authored cards: blades swing, spells cast, supplies prepare.
        private static IEnumerable<TestCaseData> AuthoredActions()
        {
            string[] schools = { "basic", "fire", "arcane", "curse", "tide", "thunder", "blood" };
            string[] actions =
            {
                "Attack Attack Attack Guard Guard Heal Heal Prepare Prepare Prepare Attack Guard Prepare",
                "Attack Attack Cast Cast Heal Attack Cast Guard Prepare Prepare Attack Cast Guard Prepare Cast",
                "Attack Guard Prepare Cast Heal Cast Cast Guard Prepare Prepare Cast Prepare Cast Prepare Cast",
                "Attack Cast Cast Cast Cast Cast Cast Cast Cast Cast Cast Heal Prepare Prepare Guard",
                "Guard Guard Guard Attack Guard Attack Attack Cast Cast Cast Heal Heal Prepare Prepare Guard",
                "Cast Attack Attack Cast Prepare Prepare Prepare Prepare Prepare Prepare Attack Attack Cast Cast Cast",
                "Attack Attack Guard Prepare Prepare Attack Attack Cast Heal Attack Attack Guard Prepare Prepare Cast"
            };
            for (int school = 0; school < schools.Length; school++)
            {
                string[] expected = actions[school].Split(' ');
                for (int i = 0; i < expected.Length; i++)
                    yield return new TestCaseData(schools[school] + "_" + (i + 1).ToString("00"),
                        (BoardingActorAction)Enum.Parse(typeof(BoardingActorAction), expected[i]));
            }
        }

        [TestCaseSource(nameof(AuthoredActions))]
        public void EveryAuthoredCardUsesItsOwnIntentAcrossAllUpgradeLevels(string key, BoardingActorAction expected)
        {
            var session = new CoreVoyageSession();
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_boarding_" + key);
            for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                Assert.That(BoardingCardActorPresentation.ForCard(card), Is.EqualTo(expected), key + " +" + level);
                CoreBattleCard renamed = Rename(card, "治疗 火焰挥刀 护盾 别名", "施法 攻击 医疗");
                Assert.That(BoardingCardActorPresentation.ForCard(renamed), Is.EqualTo(expected),
                    "Translated labels must not change the authored action: " + key + " +" + level);
            }
        }

        [Test]
        public void AuthoredExpectationsCoverTheCompleteBoardingCatalog()
        {
            Assert.That(CoreBoardingCardCatalog.All.Count, Is.EqualTo(103));
            Assert.That(AuthoredActions().Count(), Is.EqualTo(CoreBoardingCardCatalog.All.Count));
            foreach (CoreBattleCard card in CoreBoardingCardCatalog.All)
                Assert.That(BoardingCardActorPresentation.ForCard(card), Is.Not.EqualTo(BoardingActorAction.Idle), card.Id);
        }

        [TestCase("card_crew_thrust", BoardingActorAction.Attack)]
        [TestCase("card_crew_guard", BoardingActorAction.Guard)]
        [TestCase("card_crew_bandage", BoardingActorAction.Heal)]
        [TestCase("card_school_fire_b08", BoardingActorAction.Attack)]
        [TestCase("card_school_arcane_b08", BoardingActorAction.Cast)]
        [TestCase("card_school_curse_b10", BoardingActorAction.Cast)]
        [TestCase("card_school_tide_b07", BoardingActorAction.Attack)]
        [TestCase("card_school_thunder_b10", BoardingActorAction.Cast)]
        [TestCase("card_school_blood_b08", BoardingActorAction.Attack)]
        public void LegacySharedIdentitiesUseTheirBoardingDefinition(string id, BoardingActorAction expected)
        {
            var legacy = new CoreBattleCard(id, "Unrelated translated name", "Unrelated type", 1,
                999, 999, 999, 1, 0, "No presentation parses this description", NavalBattleStages.All);
            Assert.That(CoreBoardingCardCatalog.IsLegacyAlias(legacy), Is.True);
            Assert.That(BoardingCardActorPresentation.ForCard(legacy), Is.EqualTo(expected));
        }

        [TestCase("card_tactical_draw", "card_boarding_basic_08")]
        [TestCase("card_energy_order", "card_boarding_basic_09")]
        public void SplitSupportIdentitiesOnlyAnimateTheirSeparateBoardingCard(string shipId, string boardingId)
        {
            var session = new CoreVoyageSession();
            int shipIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(index => CoreVoyageSession.CardLibrary[index].Id == shipId);
            int boardingIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(index => CoreVoyageSession.CardLibrary[index].Id == boardingId);
            for (int level = 0; level <= 3; level++)
            {
                CoreBattleCard ship = session.GetBattleCardAtLevel(shipIndex, level);
                CoreBattleCard boarding = session.GetBattleCardAtLevel(boardingIndex, level);
                Assert.That(ship.Id, Is.EqualTo(shipId));
                Assert.That(ship.AllowedStages, Is.EqualTo(NavalBattleStages.Ship));
                Assert.That(CoreBoardingCardCatalog.IsLegacyAlias(ship), Is.False);
                Assert.That(BoardingCardActorPresentation.ForCard(ship), Is.EqualTo(BoardingActorAction.Idle));
                Assert.That(boarding.Id, Is.EqualTo(boardingId));
                Assert.That(boarding.AllowedStages, Is.EqualTo(NavalBattleStages.Boarding));
                Assert.That(CoreBoardingCardCatalog.IsLegacyAlias(boarding), Is.False);
                Assert.That(BoardingCardActorPresentation.ForCard(boarding), Is.EqualTo(BoardingActorAction.Prepare));
                Assert.That(BoardingCardActorPresentation.ForCard(Rename(boarding, "Attack Heal Cast", "Guard")),
                    Is.EqualTo(BoardingActorAction.Prepare), boardingId + " +" + level + " keeps its authored intent");
            }
        }

        [TestCase("card_special_crew_broken_anchor_cook_tam", BoardingActorAction.Cast)]
        [TestCase("card_special_crew_twilight_tide_doctor_viya", BoardingActorAction.Cast)]
        [TestCase("card_special_crew_twilight_tide_carpenter_orlan", BoardingActorAction.Cast)]
        [TestCase("card_special_crew_white_shell_doctor_mirei", BoardingActorAction.Cast)]
        [TestCase("card_special_crew_broken_anchor_doctor_hector", BoardingActorAction.Cast)]
        [TestCase("card_special_crew_salt_crown_diver_isha", BoardingActorAction.Attack)]
        public void SpecialCrewCardsRetainTheirAuthoredActionAndSourceIdentity(string id, BoardingActorAction expected)
        {
            var session = new CoreVoyageSession();
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
            for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                Assert.That(CoreBoardingCardCatalog.ForBoarding(card).Id, Is.EqualTo(id));
                Assert.That(BoardingCardActorPresentation.ForCard(card), Is.EqualTo(expected), id + " +" + level);
            }
        }

        [TestCase("basic_13", BoardingActorAction.Prepare, BoardingActorAction.Attack)]
        [TestCase("thunder_05", BoardingActorAction.Prepare, BoardingActorAction.Attack)]
        [TestCase("blood_13", BoardingActorAction.Prepare, BoardingActorAction.Attack)]
        [TestCase("arcane_11", BoardingActorAction.Cast, BoardingActorAction.Cast)]
        public void DelayedAttackPreparationDoesNotSwingUntilTheAttackResolves(string key,
            BoardingActorAction setup, BoardingActorAction resolution)
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
            Assert.That(card.BoardingSpec.IsDelayed, Is.True);
            Assert.That(BoardingCardActorPresentation.ForCard(card), Is.EqualTo(setup));
            Assert.That(BoardingCardActorPresentation.ForCard(card, resolvingDelayed: true), Is.EqualTo(resolution));
            Assert.That(BoardingCardActorPresentation.ForResolved(card.Id, card.SchoolId), Is.EqualTo(resolution));
        }

        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("thunder")]
        [TestCase("blood")]
        public void FieldCardsCastEvenWhenTheyAlsoDealDamageOrGrantGuard(string school)
        {
            foreach (CoreSchoolMechanic mechanic in new[] { CoreSchoolMechanic.ComponentFieldExpand,
                CoreSchoolMechanic.ComponentFieldExtend, CoreSchoolMechanic.ComponentFieldStrike,
                CoreSchoolMechanic.ComponentFieldShelter, CoreSchoolMechanic.ComponentFieldPulse,
                CoreSchoolMechanic.ComponentFieldMastery })
            {
                var field = new CoreBattleCard("field_action_probe", "出刀治疗", "登船", 1,
                    20, 10, 7, 0, 0, "", NavalBattleStages.All, schoolId: school,
                    schoolEffect: new CoreSchoolCardEffect(mechanic));
                Assert.That(BoardingCardActorPresentation.ForCard(field), Is.EqualTo(BoardingActorAction.Cast), school + "/" + mechanic);
            }
        }

        [Test]
        public void MissingAndOtherStageCardsDoNotAnimateAndUnknownResolvedSourcesUseSchoolFallback()
        {
            Assert.That(BoardingCardActorPresentation.ForCard(null), Is.EqualTo(BoardingActorAction.Idle));
            var naval = new CoreBattleCard("naval_only_probe", "Attack", "Attack", 1, 20, 0, 0, 0, 0, "",
                NavalBattleStages.Ship, schoolId: "fire");
            Assert.That(BoardingCardActorPresentation.ForCard(naval), Is.EqualTo(BoardingActorAction.Idle));
            Assert.That(BoardingCardActorPresentation.ForResolved("unknown", "fire"), Is.EqualTo(BoardingActorAction.Cast));
            Assert.That(BoardingCardActorPresentation.ForResolved(null, null), Is.EqualTo(BoardingActorAction.Attack));
            Assert.That(BoardingCardActorPresentation.ForResolved("card_school_blood_b08", "blood"), Is.EqualTo(BoardingActorAction.Attack));
        }

        private static CoreBattleCard Rename(CoreBattleCard card, string name, string type) =>
            new CoreBattleCard(card.Id, name, type, card.Energy, card.Damage, card.Block, card.Repair,
                card.Draw, card.PowderCost, card.DescriptionTemplate, card.AllowedStages, card.Action,
                card.RequiredCrew, card.ArtId, card.CannonballCost, card.RequiredComponent, card.UpgradeLevel,
                card.SchoolId, card.SchoolEffect, card.BoardingBlock, card.BoardingSpec);
    }
}
