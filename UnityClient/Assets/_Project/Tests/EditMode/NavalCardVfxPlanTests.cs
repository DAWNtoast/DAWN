using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavalCardVfxPlanTests
    {
        // Expectations describe the visible authored intent, in each school's stable n01 order.
        private static IEnumerable<TestCaseData> ExpansionCasts()
        {
            string[] schools = { "fire", "arcane", "curse", "tide", "thunder", "blood" };
            string[] roles =
            {
                "Field Cannon Affliction Guard Field Area Cannon Cannon",
                "Field Affliction Strike Strike Guard Guard Strike",
                "Field Affliction Strike",
                "Field Cannon Guard Push Dispel Area",
                "Field Cannon Empower Affliction Empower Area Strike Empower Strike",
                "Field Affliction Cannon",
            };
            for (int s = 0; s < schools.Length; s++)
            {
                string[] expected = roles[s].Split(' ');
                for (int n = 0; n < expected.Length; n++)
                    yield return new TestCaseData("card_school_" + schools[s] + "_n" + (n + 1).ToString("00"),
                        (NavalVfxRole)Enum.Parse(typeof(NavalVfxRole), expected[n]));
            }
        }

        [TestCaseSource(nameof(ExpansionCasts))]
        public void ExpansionCard_UsesAuthoredIntentAndFinalDefinition(string id, NavalVfxRole expected)
        {
            CoreBattleCard card = Find(id);
            NavalCardVfxPlan plan = NavalCardVfxPlan.For(card);
            Assert.That(plan.Role, Is.EqualTo(expected), card.Name);
            Assert.That(plan.HasSchool, Is.True);
            Assert.That(plan.TargetsEnemy, Is.EqualTo(expected == NavalVfxRole.Cannon || expected == NavalVfxRole.Strike ||
                expected == NavalVfxRole.Affliction || expected == NavalVfxRole.Dispel || expected == NavalVfxRole.Push));
            var renamed = new CoreBattleCard(card.Id, "Unrelated translated title", "Unrelated translated type", card.Energy,
                card.Damage, card.Block, card.Repair, card.Draw, card.PowderCost, "No description parsing",
                card.AllowedStages, card.Action, schoolId: card.SchoolId, schoolEffect: card.SchoolEffect);
            Assert.That(NavalCardVfxPlan.For(renamed).Role, Is.EqualTo(plan.Role));
        }

        [Test]
        public void AllAuthoredExpansionCards_AreExplicitlyCovered()
        {
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (TestCaseData item in ExpansionCasts()) covered.Add((string)item.Arguments[0]);
            foreach (CoreBattleCard card in CoreNavalSchoolExpansionCatalog.All)
                Assert.That(covered.Remove(card.Id), Is.True, "Add the authored cast expectation for " + card.Id);
            Assert.That(covered, Is.Empty);
        }

        [TestCase("card_chain_shot", NavalVfxRole.Cannon)]
        [TestCase("card_cannon_barrage", NavalVfxRole.Cannon)]
        [TestCase("card_emergency_repair", NavalVfxRole.Heal)]
        [TestCase("card_brace", NavalVfxRole.Guard)]
        [TestCase("card_load_shot", NavalVfxRole.Empower)]
        [TestCase("card_deck_polish", NavalVfxRole.Empower)]
        [TestCase("card_approach", NavalVfxRole.Empower)]
        [TestCase("card_retreat", NavalVfxRole.Guard)]
        [TestCase("card_full_sail_turn", NavalVfxRole.Empower)]
        [TestCase("card_energy_order", NavalVfxRole.Empower)]
        [TestCase("card_tactical_draw", NavalVfxRole.Empower)]
        [TestCase("card_school_curse_b03", NavalVfxRole.Affliction)]
        [TestCase("card_school_curse_b05", NavalVfxRole.Strike)]
        [TestCase("card_school_curse_b06", NavalVfxRole.Guard)]
        [TestCase("card_school_arcane_b03", NavalVfxRole.Strike)]
        [TestCase("card_school_arcane_b06", NavalVfxRole.Empower)]
        [TestCase("card_school_blood_b03", NavalVfxRole.Cannon)]
        [TestCase("card_school_blood_b04", NavalVfxRole.Strike)]
        [TestCase("card_school_blood_b05", NavalVfxRole.Guard)]
        public void BaseCard_UsesRulesInsteadOfNameOrSchool(string id, NavalVfxRole expected)
        {
            Assert.That(NavalCardVfxPlan.For(Find(id)).Role, Is.EqualTo(expected));
        }

        [Test]
        public void AreaAndFieldTargets_DoNotInventAnEnemyShipHit()
        {
            foreach (string id in new[] { "card_school_fire_n01", "card_school_tide_n01", "card_school_fire_n06", "card_school_thunder_n06" })
            {
                NavalCardVfxPlan plan = NavalCardVfxPlan.For(Find(id));
                Assert.That(plan.UsesSelectedArea, Is.True, id);
                Assert.That(plan.TargetsEnemy, Is.False, id);
                Assert.That(plan.AffectsFields, Is.True, id);
            }
            NavalCardVfxPlan wave = NavalCardVfxPlan.For(Find("card_school_tide_n06"));
            Assert.That(wave.Role, Is.EqualTo(NavalVfxRole.Area));
            Assert.That(wave.UsesSelectedArea, Is.False, "Great Wave uses all existing tide cells, without selecting an anchor.");
            Assert.That(wave.TargetsEnemy, Is.False);
            Assert.That(wave.AffectsFields, Is.True);
        }

        [TestCase(-1, 1)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(10, 2)]
        public void ConditionalArcSalvo_UsesPrePlayChainOnly(int chainBefore, int expected)
        {
            Assert.That(NavalCardVfxPlan.For(Find("card_school_thunder_n02"), chainBefore).SalvoCount, Is.EqualTo(expected));
            Assert.That(NavalCardVfxPlan.For(Find("card_school_arcane_n03"), chainBefore).SalvoCount, Is.EqualTo(2));
            Assert.That(NavalCardVfxPlan.For(Find("card_chain_shot"), chainBefore).SalvoCount, Is.EqualTo(1));
        }

        [TestCase("fire", NavalVfxRole.Affliction, true)]
        [TestCase("arcane", NavalVfxRole.Empower, false)]
        [TestCase("curse", NavalVfxRole.Affliction, true)]
        [TestCase("tide", NavalVfxRole.Empower, false)]
        [TestCase("thunder", NavalVfxRole.Empower, false)]
        [TestCase("blood", NavalVfxRole.Heal, false)]
        public void ComponentPulse_UsesActualBeneficiary(string school, NavalVfxRole expected, bool enemy)
        {
            var card = new CoreBattleCard("pulse_probe", "Arbitrary", "Arbitrary", 1, 0, 0, 0, 1, 0, "",
                NavalBattleStages.Naval, schoolId: school,
                schoolEffect: new CoreSchoolCardEffect(CoreSchoolMechanic.ComponentFieldPulse));
            NavalCardVfxPlan plan = NavalCardVfxPlan.For(card);
            Assert.That(plan.Role, Is.EqualTo(expected));
            Assert.That(plan.TargetsEnemy, Is.EqualTo(enemy));
            Assert.That(plan.AffectsFields, Is.True);
        }

        [Test]
        public void RewrittenComponentCard_RetainsBothStrikeAndFieldIntent()
        {
            CoreBattleCard card = Find("card_special_bow_nightkraken_tusk");
            Assert.That(card.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.ComponentFieldExpand),
                "Use the final source catalog definition, not the obsolete original art template.");
            NavalCardVfxPlan plan = NavalCardVfxPlan.For(card);
            Assert.That(plan.Role, Is.EqualTo(NavalVfxRole.Strike));
            Assert.That(plan.TargetsEnemy, Is.True);
            Assert.That(plan.AffectsFields, Is.True);
            Assert.That(plan.UsesSelectedArea, Is.False);
        }

        [Test]
        public void HealingAndSelfPayment_DoNotBecomeEnemyAfflictions()
        {
            foreach (CoreSchoolMechanic mechanic in new[] { CoreSchoolMechanic.FireHeal, CoreSchoolMechanic.BloodMend,
                CoreSchoolMechanic.CurseDrain, CoreSchoolMechanic.ArcaneMend, CoreSchoolMechanic.TideRenew })
            {
                var card = new CoreBattleCard("heal_probe", "Attack", "Attack", 1, 0, 2, 0, 0, 0, "Enemy damage",
                    NavalBattleStages.Naval, schoolId: "fire", schoolEffect: new CoreSchoolCardEffect(mechanic));
                Assert.That(NavalCardVfxPlan.For(card).Role, Is.EqualTo(NavalVfxRole.Heal), mechanic.ToString());
                Assert.That(NavalCardVfxPlan.For(card).TargetsEnemy, Is.False);
            }
            Assert.That(NavalCardVfxPlan.For(Find("card_school_blood_b05")).TargetsEnemy, Is.False);
            foreach (string key in new[] { "fire_13", "fire_14", "arcane_11", "thunder_05", "blood_13" })
                Assert.That(NavalCardVfxPlan.For(CoreBoardingCardCatalog.FindByKey(key)).TargetsEnemy, Is.False, key);
        }

        [Test]
        public void EveryFinalNavalCardAndUpgrade_KeepsItsTargetIntent()
        {
            var session = new CoreVoyageSession();
            int count = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
                if (!card.IsAllowedInStage(NavalBattleStage.Naval)) continue;
                count++;
                NavalCardVfxPlan original = NavalCardVfxPlan.For(card, 2);
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                {
                    CoreBattleCard upgraded = session.GetBattleCardAtLevel(index, rank);
                    NavalCardVfxPlan plan = NavalCardVfxPlan.For(upgraded, 2);
                    Assert.That(plan.Role, Is.EqualTo(original.Role), card.Id + "+" + rank);
                    Assert.That(plan.TargetsEnemy, Is.EqualTo(original.TargetsEnemy), card.Id);
                    Assert.That(plan.UsesSelectedArea, Is.EqualTo(original.UsesSelectedArea), card.Id);
                    Assert.That(plan.SalvoCount, Is.InRange(1, 2), card.Id);
                    Assert.That(plan.IsAttack && plan.Role != NavalVfxRole.Area ? plan.TargetsEnemy : true, Is.True, card.Id);
                }
            }
            Assert.That(count, Is.GreaterThan(80));
        }

        private static CoreBattleCard Find(string id)
        {
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary)
                if (card.Id == id) return card;
            Assert.Fail("Missing authored card " + id);
            return null;
        }
    }
}
