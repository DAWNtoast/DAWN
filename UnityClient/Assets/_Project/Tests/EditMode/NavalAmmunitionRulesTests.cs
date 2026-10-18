using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavalAmmunitionRulesTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EveryCardRank_UsesTheSameFiringLoadingAndMagicAmmunitionRules(int level)
        {
            var session = new CoreVoyageSession();
            int cannons = 0, loading = 0, magic = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                if (card == null) continue;
                if (card.Action == NavalBattleCardAction.Cannon)
                {
                    cannons++;
                    // The naval expansion's cannons each cost one powder; their reused school art is not a cost rule.
                    bool expansion = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id) != null;
                    int expectedPowder = expansion ? 1 : card.SchoolId == "fire" ? card.ArtId == "card_school_fire_l03" ? 3 : 2 : 1;
                    Assert.That(card.PowderCost, Is.EqualTo(expectedPowder), card.Id);
                    Assert.That(card.CannonballCost, Is.EqualTo(1), card.Id);
                    Assert.That(card.Description, Does.Contain(expectedPowder + " 火药").And.Contain("1 炮弹"), card.Id);
                }
                else if (card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad)
                {
                    loading++;
                    int expectedPowder = card.Id == "card_component_instrument" ? 0 : 1;
                    Assert.That(card.PowderCost, Is.EqualTo(expectedPowder), card.Id);
                    Assert.That(card.CannonballCost, Is.Zero, card.Id);
                    if (expectedPowder > 0) Assert.That(card.Description, Does.Contain("消耗 1 火药"), card.Id);
                }
                else if (card.Action == NavalBattleCardAction.FireRounds)
                {
                    magic++;
                    Assert.That(card.PowderCost, Is.Zero, card.Id);
                    Assert.That(card.CannonballCost, Is.Zero, card.Id);
                }
            }
            Assert.That(cannons, Is.GreaterThan(3), "Include special cannons, not just the base deck.");
            Assert.That(loading, Is.GreaterThan(3), "Include crew and component loading effects.");
            Assert.That(magic, Is.GreaterThan(0));
        }

        [TestCase("card_chain_shot", 0, 1, "火药不足")]
        [TestCase("card_chain_shot", 1, 0, "炮弹不足")]
        [TestCase("card_chain_shot", 0, 0, "火药不足")]
        [TestCase("card_cannon_barrage", 0, 1, "火药不足")]
        [TestCase("card_cannon_barrage", 1, 0, "炮弹不足")]
        [TestCase("card_special_cannon_anchorbreaker", 0, 1, "火药不足")]
        [TestCase("card_special_cannon_anchorbreaker", 1, 0, "炮弹不足")]
        public void CannonMissingEitherSupply_CannotSpendEnergyCardsOrTheOtherResource(string id, int powder, int balls, string reason)
        {
            CoreVoyageSession session = Battle(id, powder, balls);
            session.Battle.PlayerLoadedShot = 4;
            string before = Snapshot(session);
            Assert.That(session.CanPlayCard(0), Is.False);
            Assert.That(session.CardUnavailableReason(0), Does.StartWith(reason));
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(before), "A rejected shot also preserves the extra loading charge.");
        }

        [TestCase("card_chain_shot")]
        [TestCase("card_cannon_barrage")]
        [TestCase("card_component_cannon")]
        [TestCase("card_special_cannon_anchorbreaker")]
        public void SuccessfulCannon_SpendsExactlyOnePowderAndOneBall(string id)
        {
            CoreVoyageSession session = Battle(id, 3, 4);
            int energy = session.Battle.Energy;
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            int hull = session.Battle.EnemyHull;
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Cargo.Powder, Is.EqualTo(2));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(3));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - card.Energy));
            Assert.That(session.Battle.EnemyHull, Is.LessThan(hull));
        }

        [TestCase(0)] [TestCase(3)]
        public void LoadingThenFiring_SpendsTwoPowderAndOneBallAcrossTheTwoActions(int level)
        {
            CoreVoyageSession session = Battle("card_load_shot", 2, 1, level);
            session.Battle.Hand.Add(Find("card_chain_shot"));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Cargo.Powder, Is.EqualTo(1));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(1), "Extra loading cannot spend the projectile before firing.");
            Assert.That(session.Battle.PlayerLoadedShot, Is.GreaterThan(0));
            int firing = session.Battle.Hand.FindIndex(index => session.GetBattleCard(index).Id == "card_chain_shot");
            Assert.That(session.PlayCard(firing), Is.True, session.CardUnavailableReason(firing));
            Assert.That(session.Cargo.Powder, Is.Zero);
            Assert.That(session.Cargo.Cannonballs, Is.Zero);
            Assert.That(session.Battle.PlayerLoadedShot, Is.Zero);
        }

        [Test]
        public void ExtraLoadingCannotSubstituteForThePowderNeededToFire()
        {
            CoreVoyageSession session = Battle("card_load_shot", 1, 1);
            session.Battle.Hand.Add(Find("card_chain_shot"));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Cargo.Powder, Is.Zero);
            Assert.That(session.Battle.PlayerLoadedShot, Is.EqualTo(session.GetBattleCard(Find("card_load_shot")).StrategyAmount));
            string loaded = Snapshot(session);
            Assert.That(session.CardUnavailableReason(0), Is.EqualTo("火药不足"));
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(loaded));
        }

        [TestCase("card_load_shot")]
        [TestCase("card_gunner_precision")]
        public void LoadingWithoutPowder_IsAtomicAndCannotCreateABuff(string id)
        {
            CoreVoyageSession session = Battle(id, 0, 1);
            string before = Snapshot(session);
            Assert.That(session.CardUnavailableReason(0), Is.EqualTo("火药不足"));
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(before));
        }

        [Test]
        public void CappedLoading_StillPaysForItsDrawWithoutRaisingTheReserve()
        {
            CoreVoyageSession session = Battle("card_load_shot", 1, 1);
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            session.Battle.PlayerLoadedShot = card.StrategyLimit;
            Assert.That(card.Draw, Is.EqualTo(1));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.PlayerLoadedShot, Is.EqualTo(card.StrategyLimit));
            Assert.That(session.Cargo.Powder, Is.Zero);
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(1));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1), "The played card can be redrawn from the otherwise empty deck.");
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
        }

        [TestCase("card_component_instrument")]
        [TestCase("card_special_crew_salt_crown_gunner_rokan")]
        public void CalibrationAndMagicRemainFreeButCannotMakeAnUnfundedCannonShot(string id)
        {
            CoreVoyageSession session = Battle(id, 0, 0);
            session.Battle.Hand.Add(Find("card_chain_shot"));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Cargo.Powder, Is.Zero);
            Assert.That(session.Cargo.Cannonballs, Is.Zero);
            Assert.That(session.Battle.PlayerLoadedShot + session.Battle.PlayerFireRounds, Is.GreaterThan(0));
            int firing = session.Battle.Hand.FindIndex(index => session.GetBattleCard(index).Id == "card_chain_shot");
            string prepared = Snapshot(session);
            Assert.That(session.PlayCard(firing), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(prepared));
        }

        private static CoreVoyageSession Battle(string id, int powder, int balls, int level = 0)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(true, true, true, true, true, true);
            session.ConfigureComponentBattleCards(new[] { "bow", "sail", "cannon", "plating", "instrument" });
            CoreSpecialCardDefinition source = CoreSpecialCardCatalog.FindByCardId(id);
            if (source != null)
                session.ConfigureSpecialCardSources(source.SourceKind == CoreSpecialCardSourceKind.Crew ? new[] { source.SourceId } : Array.Empty<string>(),
                    source.SourceKind == CoreSpecialCardSourceKind.Component ? new[] { source.SourceId } : Array.Empty<string>());
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, level } });
            session.SetSeed("AMMUNITION-RULES-TEST");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("ammunition_test", "弹药规则测试敌舰", "海战", "测试", true, false, 1, 70), false });
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.DistanceMeters = 200f;
            session.Battle.Energy = 10;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 500;
            session.Battle.EnemyBlock = 0;
            session.Battle.Hand.Clear(); session.Battle.DrawPile.Clear(); session.Battle.DiscardPile.Clear();
            session.Battle.Hand.Add(Find(id));
            session.Cargo.Powder = powder; session.Cargo.Cannonballs = balls;
            return session;
        }

        private static int Find(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private static string Snapshot(CoreVoyageSession session)
        {
            CoreBattleState battle = session.Battle;
            return session.Phase + ":" + battle.Energy + ":" + battle.CardsPlayedThisTurn + ":" + session.Cargo.Powder + ":" +
                session.Cargo.Cannonballs + ":" + session.Cargo.Medicine + ":" + battle.PlayerLoadedShot + ":" + battle.PlayerFireRounds + ":" +
                battle.PlayerBlock + ":" + battle.EnemyHull + ":" + battle.EnemyBlock + ":" +
                string.Join(",", battle.Hand) + ":" + string.Join(",", battle.DrawPile) + ":" + string.Join(",", battle.DiscardPile);
        }
    }
}
