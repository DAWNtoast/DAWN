using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardUpgradeStructureTests
    {
        [Test]
        public void EveryPublishedStageKeepsItsBaseEffectAndPaymentTypesAtEveryRank()
        {
            var session = new CoreVoyageSession();
            foreach (CoreBattleCard original in CoreVoyageSession.CardLibrary.Where(c => c.AllowedStages != NavalBattleStages.None))
                foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                {
                    if (!original.IsAllowedInStage(stage)) continue;
                    CoreBattleCard basis = stage == NavalBattleStage.Boarding ? CoreBoardingCardCatalog.ForBoarding(original) : original;
                    for (int rank = 1; rank <= session.GetCardMaximumUpgradeLevel(original.Id); rank++)
                    {
                        CoreBattleCard grown = session.GetBattleCardAtLevel(Index(original.Id), rank);
                        if (stage == NavalBattleStage.Boarding) grown = CoreBoardingCardCatalog.ForBoarding(grown);
                        string context = original.Id + "/" + stage + "/+" + rank;
                        int[] before = EffectChannels(basis), after = EffectChannels(grown);
                        for (int i = 0; i < before.Length; i++)
                            if (before[i] == 0) Assert.That(after[i], Is.Zero, context + " introduces effect channel " + i);
                        Assert.That(CoreBattleCardUpgradeStructure.HasRepairSupply(grown, stage),
                            Is.EqualTo(CoreBattleCardUpgradeStructure.HasRepairSupply(basis, stage)), context);
                        Assert.That(new[] { grown.Energy, grown.PowderCost, grown.CannonballCost, grown.SchoolEffect?.WoodCost ?? 0 },
                            Is.EqualTo(new[] { basis.Energy, basis.PowderCost, basis.CannonballCost, basis.SchoolEffect?.WoodCost ?? 0 }), context);
                    }
                }
        }

        [TestCase("card_special_crew_broken_anchor_carpenter_kia", "4,6,8")]
        [TestCase("card_special_crew_salt_crown_carpenter_saf", "4,6")]
        [TestCase("card_special_crew_gray_gull_cook_pepe", "4,6")]
        [TestCase("card_special_crew_bronze_bell_cook_basin", "4,6,8")]
        [TestCase("card_special_plating_merchants_oath", "2,4")]
        [TestCase("card_special_plating_gilded_bulwark", "2,5")]
        [TestCase("card_special_plating_suncoin_rampart", "2,5")]
        [TestCase("card_special_plating_redbanner_wall", "2,5,8")]
        public void ReviewedHealingExecutesItsFullAmountWithOrWithoutSupplies(string id, string curve)
        {
            int[] expected = curve.Split(',').Select(int.Parse).ToArray();
            CoreBattleCard basis = CoreVoyageSession.CardLibrary[Index(id)];
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                if (!basis.IsAllowedInStage(stage)) continue;
                for (int rank = 0; rank < expected.Length; rank++)
                    foreach (int stock in new[] { 0, 100 })
                    {
                        CoreVoyageSession session = Start(id, rank, stage);
                        session.Cargo.Wood = session.Cargo.Medicine = stock;
                        if (stage == NavalBattleStage.Boarding) session.Battle.PlayerBoardingHealth -= 20;
                        else typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull - 20);
                        int health = stage == NavalBattleStage.Boarding ? session.Battle.PlayerBoardingHealth : session.Hull;
                        CoreBattleCard card = session.GetBattleCard(Index(id), stage);
                        CoreBattleCardEffects quote = session.GetBattleCardEffects(card, stage);
                        string context = id + "/" + stage + "/+" + rank + "/stock=" + stock;
                        Assert.That(card.Repair, Is.EqualTo(expected[rank]), context);
                        Assert.That(quote.ActualRepair, Is.EqualTo(expected[rank]), context);
                        Assert.That(quote.WoodCost + quote.MedicineCost, Is.Zero, context);
                        Assert.That(session.PlayCard(0), Is.True, context + ": " + session.CardUnavailableReason(0));
                        int after = stage == NavalBattleStage.Boarding ? session.Battle.PlayerBoardingHealth : session.Hull;
                        Assert.That(after - health, Is.EqualTo(expected[rank]), context);
                        Assert.That(session.Cargo.Wood, Is.EqualTo(stock), context);
                        Assert.That(session.Cargo.Medicine, Is.EqualTo(stock), context);
                    }
            }
        }

        [TestCase("card_school_curse_b01")]
        [TestCase("card_school_curse_b02")]
        public void ReviewedCurseCardsExecuteTwoFiveAndEightMarks(string id)
        {
            int[] marks = { 2, 5, 8 };
            for (int rank = 0; rank < marks.Length; rank++)
            {
                CoreVoyageSession session = Start(id, rank, NavalBattleStage.Preparation);
                int before = session.Battle.Schools.CurseMarks;
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                Assert.That(session.Battle.Schools.CurseMarks - before, Is.EqualTo(marks[rank]), id + "/+" + rank);
            }
        }

        private static int[] EffectChannels(CoreBattleCard card) => new[] {
            card.Damage, card.Block, card.Repair, card.Draw, card.BoardingBlock ?? 0,
            CoreBattleUpgradeBalance.BaseEnergyRecovery(card), card.NavalActionPointGain,
            card.SchoolEffect?.Power ?? 0, card.SchoolEffect?.Secondary ?? 0,
            card.SchoolEffect?.Threshold ?? 0, card.SchoolEffect?.Duration ?? 0,
            card.SchoolEffect?.Limit ?? 0, card.SchoolEffect?.Movement ?? 0 };

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == id);

        private static CoreVoyageSession Start(string id, int rank, NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.RestoreCardPackProgress(null, session.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            session.ConfigureSpecialCardSources(CoreSpecialCardCatalog.All.Where(c => c.SourceKind == CoreSpecialCardSourceKind.Crew).Select(c => c.SourceId).ToArray(),
                CoreSpecialCardCatalog.All.Where(c => c.SourceKind == CoreSpecialCardSourceKind.Component).Select(c => c.SourceId).ToArray());
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            session.SetSeed("CARD-REVIEW-20261030");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            if (stage != NavalBattleStage.Preparation) Assert.That(session.EndBattleTurn(), Is.True);
            if (stage == NavalBattleStage.Boarding) typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { stage });
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 10000;
            session.Battle.Energy = 100;
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Index(id));
            session.Battle.DiscardPile.Clear(); session.Battle.DrawPile.Clear();
            session.Battle.DrawPile.AddRange(Enumerable.Repeat(Index(stage == NavalBattleStage.Boarding ? "card_boarding_basic_01" : "card_brace"), 20));
            return session;
        }
    }
}
