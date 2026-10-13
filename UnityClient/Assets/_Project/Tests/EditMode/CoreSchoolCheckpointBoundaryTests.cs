using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSchoolCheckpointBoundaryTests
    {
        [Test]
        public void ActiveSchoolBattle_CannotCaptureCheckpointOrTreatStateAsVoyageProgress()
        {
            var session = ReadyBattle();
            DirtySchoolState(session.Battle.Schools);
            Assert.That(session.Battle.Schools.Mana, Is.GreaterThan(0));
            Assert.That(session.Battle.Schools.AvailableMana, Is.EqualTo(14), "The read-only total must reflect both dirtied balances.");
            Assert.That(session.Battle.Schools.OverflowUsed, Is.True);
            Assert.That(session.CanCaptureVoyageCheckpoint, Is.False);
            Assert.Throws<InvalidOperationException>(() => session.CaptureVoyageCheckpoint());
        }

        [Test]
        public void CompletedSchoolBattle_JsonVoyageRoundTripStartsNextBattleWithFreshStateAndUseCosts()
        {
            var source = ReadyBattle();
            CoreBattleState completed = source.Battle;
            DirtySchoolState(completed.Schools);
            completed.Stage = NavalBattleStage.Naval;
            completed.DistanceMeters = 100f;
            completed.EnemyBlock = 0;
            completed.EnemyHull = 1;
            int shot = Find("card_chain_shot");
            completed.Hand.Clear();
            completed.Hand.Add(shot);
            Assert.That(source.PlayCard(0), Is.True);
            Assert.That(source.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(source.Battle, Is.Null);
            Assert.That(source.CanCaptureVoyageCheckpoint, Is.True);

            string json = JsonUtility.ToJson(source.CaptureVoyageCheckpoint());
            CoreVoyageCheckpoint decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(json);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(decoded, out string reason), Is.True, reason);
            Assert.That(restored.Battle, Is.Null);
            Assert.That(restored.Cargo.Powder, Is.EqualTo(source.Cargo.Powder));
            Assert.That(restored.Cargo.Cannonballs, Is.EqualTo(source.Cargo.Cannonballs));
            Assert.That(restored.Cargo.Gold, Is.EqualTo(source.Cargo.Gold));
            Assert.That(restored.VoyageTreasureFragments, Is.EqualTo(source.VoyageTreasureFragments));
            StartBattle(source);
            StartBattle(restored);
            Assert.That(restored.Battle.Schools, Is.Not.SameAs(completed.Schools));
            Assert.That(restored.Battle.Schools, Is.Not.SameAs(source.Battle.Schools));
            AssertFresh(source.Battle.Schools);
            AssertFresh(restored.Battle.Schools);
            Assert.That(restored.GetBattleCardEnergyCost(shot), Is.EqualTo(restored.GetBattleCard(shot).Energy));
            Assert.That(source.GetBattleCardEnergyCost(shot), Is.EqualTo(source.GetBattleCard(shot).Energy));
            DirtySchoolState(completed.Schools);
            AssertFresh(restored.Battle.Schools);
        }

        [Test]
        public void BattlePhaseCheckpoint_IsRejectedAtomicallyWithoutRestoringPartialState()
        {
            var source = new CoreVoyageSession();
            source.StartVoyage();
            CoreVoyageCheckpoint invalid = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(source.CaptureVoyageCheckpoint()));
            invalid.phase = (int)CoreDemoPhase.Battle;
            var target = new CoreVoyageSession();
            decimal gold = target.ProfileGold;
            Assert.That(target.TryRestoreVoyageCheckpoint(invalid, out _), Is.False);
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(target.Battle, Is.Null);
            Assert.That(target.ProfileGold, Is.EqualTo(gold));
        }

        private static CoreVoyageSession ReadyBattle()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            StartBattle(session);
            return session;
        }
        private static void StartBattle(CoreVoyageSession session) => typeof(CoreVoyageSession)
            .GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("school_checkpoint_boundary", "续航检查船", "海战", "条件夹具", true, false, 1, 70), false });
        private static int Find(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static void DirtySchoolState(CoreSchoolBattleState state)
        {
            foreach (PropertyInfo property in typeof(CoreSchoolBattleState).GetProperties())
            {
                MethodInfo setter = property.GetSetMethod(true);
                if (setter != null) setter.Invoke(state, new[] { property.PropertyType == typeof(bool) ? (object)true : 7 });
            }
        }
        private static void AssertFresh(CoreSchoolBattleState state)
        {
            foreach (PropertyInfo property in typeof(CoreSchoolBattleState).GetProperties())
            {
                object expected = property.PropertyType == typeof(bool) ? (object)false :
                    property.Name == nameof(CoreSchoolBattleState.ManaLimit) ? 3 :
                    property.Name == nameof(CoreSchoolBattleState.ThunderLimit) ? 6 :
                    property.Name == nameof(CoreSchoolBattleState.TideLimit) ? 20 : 0;
                Assert.That(property.GetValue(state), Is.EqualTo(expected), property.Name);
            }
        }
    }
}
