using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleRepairSupplyTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void HullRepair_QuotesAndPaysOneWoodOnlyBeyondFixedFallback(int rank)
        {
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval })
                foreach (int wood in new[] { 0, 1 })
                    foreach (int wound in new[] { 0, 1, 2, 3, 40 })
                    {
                        CoreVoyageSession session = Battle(rank, stage);
                        session.Cargo.Wood = wood;
                        SetHull(session, session.MaxHull - wound);
                        CoreBattleCard card = session.GetBattleCard(Repair);
                        int heal = Math.Min(wound, wood > 0 ? card.Repair : 2);
                        int cost = wood > 0 && wound > 2 ? 1 : 0;
                        CoreBattleCardEffects quote = session.GetBattleCardEffects(card, stage);
                        Assert.That(quote.FullRepair, Is.EqualTo(card.Repair));
                        Assert.That(quote.FallbackRepair, Is.EqualTo(2));
                        Assert.That(quote.ActualRepair, Is.EqualTo(heal));
                        Assert.That(quote.WoodCost, Is.EqualTo(cost));
                        Assert.That(quote.MedicineCost, Is.Zero);
                        Assert.That(session.Cargo.Wood, Is.EqualTo(wood), "Quotes cannot spend wood.");
                        Assert.That(session.BattleCardFaceDescription(card, stage), Does.Contain("修复 2 点船体")
                            .And.Contain("消耗 1 木板额外修复 " + (quote.FullRepair - 2) + " 点船体"));
                        Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                        Assert.That(session.Hull, Is.EqualTo(session.MaxHull - wound + heal));
                        Assert.That(session.Cargo.Wood, Is.EqualTo(wood - cost));
                        Assert.That(session.Cargo.Medicine, Is.EqualTo(4));
                    }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void OrdinaryGuard_KeepsFullBlockAndNeverUsesWood(int rank)
        {
            foreach (int wood in new[] { 0, 1 })
            {
                CoreVoyageSession session = Battle(rank, NavalBattleStage.Naval);
                session.Cargo.Wood = wood;
                int guard = Index("card_brace");
                CoreBattleCard card = session.GetBattleCardAtLevel(guard, rank);
                session.Battle.Hand.Clear(); session.Battle.Hand.Add(guard);
                int before = session.Battle.PlayerBlock;
                Assert.That(session.GetBattleCardEffects(card, NavalBattleStage.Naval).Block, Is.EqualTo(card.Block));
                Assert.That(session.GetBattleCardWoodCost(guard), Is.Zero);
                Assert.That(session.PlayCard(0), Is.True);
                Assert.That(session.Battle.PlayerBlock, Is.EqualTo(before + card.Block));
                Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
            }
        }

        [Test]
        public void PortPreviewAndRejectedRepairNeverSpendWood()
        {
            var port = new CoreVoyageSession();
            port.Cargo.Wood = 1;
            CoreBattleCard card = port.GetBattleCardAtLevel(Repair, 3);
            CoreBattleCardEffects quote = port.GetBattleCardEffects(card, NavalBattleStage.Naval);
            Assert.That(quote.IsRepairPreview, Is.True);
            Assert.That(quote.Repair, Is.EqualTo(card.Repair));
            Assert.That(quote.WoodCost, Is.Zero);
            Assert.That(port.BattleCardFaceDescription(card, NavalBattleStage.Naval), Does.Contain("消耗 1 木板"));
            Assert.That(port.Cargo.Wood, Is.EqualTo(1));
            CoreVoyageSession battle = Battle(3, NavalBattleStage.Naval);
            SetHull(battle, battle.MaxHull - 40);
            battle.Cargo.Wood = 1;
            battle.Battle.Energy = 0;
            Assert.That(battle.PlayCard(0), Is.False);
            Assert.That(battle.Hull, Is.EqualTo(battle.MaxHull - 40));
            Assert.That(battle.Cargo.Wood, Is.EqualTo(1));
        }

        private static int Repair => Index("card_emergency_repair");
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void SetHull(CoreVoyageSession session, int hull) => typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, hull);
        private static CoreVoyageSession Battle(int rank, NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_emergency_repair", rank }, { "card_brace", rank } });
            session.SetSeed("REPAIR-SUPPLY-REGRESSION");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("repair_supply", "维修隔离敌舰", "海战", "维修物资回归", true, false, 1, 500), false });
            session.Battle.Stage = stage;
            session.Battle.Energy = 10;
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Repair);
            session.Cargo.Medicine = 4;
            return session;
        }
    }
}
