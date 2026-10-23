using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardAuthoredDamageTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EveryPublishedCard_QuotesItsDefinitionAtReferenceCrewWithoutASecondDamageScale(int rank)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.SelectShip(7), Is.True);
            session.ConfigureDepartureCrew(28, 0);
            session.Cargo.Medicine = session.Cargo.Wood = 1;
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(28));
            int cards = 0, schools = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard authored = CoreVoyageSession.CardLibrary[index];
                if (authored.AllowedStages == NavalBattleStages.None) continue;
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                {
                    if (!card.IsAllowedInStage(stage)) continue;
                    CoreBattleCardEffects quote = session.GetBattleCardEffects(card, stage);
                    string context = card.Id + " rank " + rank + " " + stage;
                    Assert.That(quote.Damage, Is.EqualTo(card.Damage), context);
                    Assert.That(quote.Block, Is.EqualTo(stage == NavalBattleStage.Boarding ? card.BoardingBlock ?? card.Block : card.Block), context);
                    Assert.That(quote.FullRepair, Is.EqualTo(card.Repair), context);
                    Assert.That(quote.Repair, Is.EqualTo(card.Repair), context);
                }
                Assert.That(CoreVoyageSession.CardLibrary[index], Is.SameAs(authored));
                Assert.That(session.GetCardUpgradeLevel(card.Id), Is.Zero, "A quotation must not purchase an upgrade.");
                cards++;
                if (card.SchoolId.Length > 0) schools++;
            }
            Assert.That(cards, Is.EqualTo(CoreVoyageSession.CardLibrary.Count(card => card.AllowedStages != NavalBattleStages.None)));
            Assert.That(schools, Is.EqualTo(CoreVoyageSession.CardLibrary.Count(card => card.AllowedStages != NavalBattleStages.None && card.SchoolId.Length > 0)));
            Assert.That(cards - schools, Is.EqualTo(CoreVoyageSession.CardLibrary.Count(card => card.AllowedStages != NavalBattleStages.None && card.SchoolId.Length == 0)));
            Assert.That(session.GetBattleCardAtLevel(Index("card_chain_shot"), rank).Damage,
                Is.EqualTo(new[] { 10, 12, 15, 19 }[rank]), "Each rank scales once along Chain Shot's authored curve.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EveryPublishedBoardingCardKeepsItsAuthoredFixedValuesAtEveryCrewCount(int rank)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.SelectShip(7), Is.True);
            session.Cargo.Medicine = session.Cargo.Wood = 1;
            foreach (int crew in new[] { 0, 7, 10, 14, 22, 28 })
            {
                session.ConfigureDepartureCrew(crew, 0);
                Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(crew));
                for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                {
                    if (!CoreVoyageSession.CardLibrary[index].IsAllowedInStage(NavalBattleStage.Boarding)) continue;
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                    if (!card.IsAllowedInStage(NavalBattleStage.Boarding)) continue;
                    CoreBattleCardEffects quote = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
                    string context = card.Id + " rank=" + rank + " crew=" + crew;
                    Assert.That(quote.Damage, Is.EqualTo(card.Damage), context);
                    Assert.That(quote.Block, Is.EqualTo(card.BoardingBlock ?? card.Block), context);
                    Assert.That(quote.FullRepair, Is.EqualTo(card.Repair), context);
                    Assert.That(quote.BoardingScale, Is.EqualTo(1m), context);
                }
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void CannonLoadFirepowerAndBurn_ResolveExactlyTheirQuotedAuthoredValues(int rank)
        {
            CoreVoyageSession session = Battle(rank);
            Assert.That(session.EffectiveFirepower, Is.EqualTo(16), "The fixture contributes exactly four firepower damage.");
            CoreBattleCard load = session.GetBattleCard(Index("card_load_shot"));
            int loadIncrease = Math.Min(load.StrategyAmount, load.StrategyLimit), loadLimit = load.StrategyLimit;
            int burn = session.GetBattleCard(Index("card_special_crew_salt_crown_gunner_rokan")).BurnDamage;
            int cannonDamage = session.GetBattleCard(Index("card_chain_shot")).Damage + loadIncrease + 4;
            Assert.That(session.GetBattleLoadedShotIncrease(load), Is.EqualTo(loadIncrease));
            Assert.That(session.GetBattleLoadedShotLimit(load), Is.EqualTo(loadLimit));
            int powder = session.Cargo.Powder;
            Play(session, load.Id);
            Assert.That(session.Battle.PlayerLoadedShot, Is.EqualTo(loadIncrease));
            Assert.That(session.PlayerLoadedShotDamageBonus, Is.EqualTo(loadIncrease));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder - 1));

            Play(session, "card_special_crew_salt_crown_gunner_rokan");
            Assert.That(session.Battle.PlayerFireRoundDamage, Is.EqualTo(burn));
            CoreBattleCard cannon = session.GetBattleCard(Index("card_chain_shot"));
            Assert.That(session.GetBattleCardDamage(cannon, NavalBattleStage.Naval), Is.EqualTo(cannonDamage));
            Assert.That(Regex.IsMatch(session.BattleCardFaceDescription(cannon, NavalBattleStage.Naval),
                @"(?:船体伤害\s*" + cannonDamage + @"(?!\d)|" + cannonDamage + @"\s*点船体伤害)"), Is.True);
            int hull = session.Battle.EnemyHull, balls = session.Cargo.Cannonballs;
            powder = session.Cargo.Powder;
            session.Battle.EnemyBlock = 3;
            Play(session, cannon.Id);
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hull - cannonDamage + 3));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder - 1));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(balls - 1));
            Assert.That(session.PlayerLoadedShotDamageBonus, Is.Zero);
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(burn));
            Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(burn));
            hull = session.Battle.EnemyHull;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hull - burn));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Boarding_UsesTheUpgradedDamageWithoutCrewScaling(int rank)
        {
            CoreVoyageSession session = Battle(rank);
            session.Battle.Stage = NavalBattleStage.Boarding;
            typeof(CoreBattleState).GetProperty("PlayerOrdinaryCrew").SetValue(session.Battle, 14);
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 1000;
            CoreBattleCard thrust = session.GetBattleCard(Index("card_crew_thrust"));
            int damage = new[] { 8, 10, 12, 16 }[rank];
            Assert.That(thrust.Damage, Is.EqualTo(damage));
            Assert.That(session.GetBattleCardEffects(thrust, NavalBattleStage.Boarding).Damage, Is.EqualTo(damage));
            Assert.That(session.BattleCardEffectDescription(thrust, NavalBattleStage.Boarding), Does.Contain(damage + " 点伤害"));
            Play(session, thrust.Id);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(1000 - damage));
        }

        private static CoreVoyageSession Battle(int rank)
        {
            var session = new CoreVoyageSession();
            session.ConfigureComponentBonuses(0, 0, 10, 0, 0);
            session.ConfigureSpecialCardSources(new[] { "crew_salt_crown_gunner_rokan" }, null);
            session.ConfigureCardUpgrades(new Dictionary<string, int>
            {
                { "card_chain_shot", rank }, { "card_load_shot", rank }, { "card_crew_thrust", rank }
            });
            session.RestoreSpecialCardExperience(new Dictionary<string, int>
            {
                { "card_special_crew_salt_crown_gunner_rokan", new[] { 0, 120, 360, 840 }[rank] }
            });
            session.SetSeed("AUTHORED-CARD-DAMAGE");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("authored_damage", "原始数值测试", "海战", "隔离卡牌效果", true, false, 1, 70), false });
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced, 1000, 0, 0, 0, 0, 28, 1, 1, healthMultiplier: 1);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 1000;
            session.Battle.EnemyBlock = 0;
            session.Battle.DistanceMeters = 100;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            session.Battle.Energy = 100;
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            return session;
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private static void Play(CoreVoyageSession session, string id)
        {
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(Index(id));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
        }
    }
}
