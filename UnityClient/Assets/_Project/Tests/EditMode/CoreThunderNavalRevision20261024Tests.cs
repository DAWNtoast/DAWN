using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreThunderNavalRevision20261024Tests
    {
        [Test]
        public void PrechargeHasNoBaseResourceCostAndAddsTwoOnlyAfterNextThunderAttack()
        {
            var s = Start(); s.Battle.Energy = 0; s.Cargo.Powder = s.Cargo.Cannonballs = 0;
            Play(s, "card_school_thunder_n03");
            Assert.That(s.Battle.Energy, Is.Zero); Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            Assert.That(s.Battle.NavalExpansion.Precharge, Is.True);
            s.Battle.Energy = 20; s.Cargo.Powder = s.Cargo.Cannonballs = 10;
            Play(s, "card_school_thunder_n04");
            Assert.That(s.Battle.NavalExpansion.Precharge, Is.True, "Utility thunder cards do not consume precharge.");
            Play(s, "card_school_thunder_b03");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(3));
            Assert.That(s.Battle.NavalExpansion.Precharge, Is.False);
            Play(s, "card_school_thunder_b03");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(4));
        }

        [TestCase("card_school_thunder_b03", 0, 8, 0)]
        [TestCase("card_school_thunder_b03", 1, 8, 4)]
        [TestCase("card_school_thunder_b03", 9, 8, 4)]
        [TestCase("card_school_thunder_n02", 1, 6, 0)]
        [TestCase("card_school_thunder_n02", 2, 6, 12)]
        [TestCase("card_school_thunder_n02", 20, 6, 12)]
        [TestCase("card_school_thunder_b04", 0, 15, 0)]
        [TestCase("card_school_thunder_b04", 7, 15, 21)]
        public void NavalCannonThresholdsAndPerChainBonusUsePrePlayValue(string id, int chain, int cannonBase, int bonus)
        {
            var s = Start(); s.Battle.Schools.ThunderChain = chain;
            int expected = CoreNavalStatRules.ScaleCannonDamage(cannonBase, s.EffectiveFirepower) + bonus;
            int before = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(before - s.Battle.EnemyHull, Is.EqualTo(expected));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(chain + 1));
        }

        [TestCase(false, 0)] [TestCase(false, 8)] [TestCase(true, 8)]
        public void FinalThunderUsesTenPlusXAndClearsAfterResolutionOnlyOutsideThunder(bool inField, int chain)
        {
            var s = Start(); s.Battle.Schools.ThunderChain = chain;
            if (inField) AddCell(s, 0, 0, CoreNavalFieldKind.Thunder, false);
            int before = s.Battle.EnemyHull;
            Play(s, "card_school_thunder_b06");
            Assert.That(before - s.Battle.EnemyHull, Is.EqualTo(10 + chain));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(inField ? chain + 1 : 0));
        }

        [Test]
        public void GroundingWorksWithoutFieldEvenWhenGuardAbsorbsAllDamageAndCannotRearmSameTurn()
        {
            var s = Start(); s.Battle.EnemyBlock = 1000;
            Play(s, "card_school_thunder_n05");
            int before = s.Battle.Energy, hull = s.Battle.EnemyHull;
            Play(s, "card_school_thunder_b03");
            Assert.That(s.Battle.Energy, Is.EqualTo(before), "One energy paid and one recovered.");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
            Assert.That(s.Battle.NavalExpansion.GroundingUsed, Is.True);
            Play(s, "card_school_thunder_n05");
            before = s.Battle.Energy;
            Play(s, "card_school_thunder_n02");
            Assert.That(s.Battle.Energy, Is.EqualTo(before - 1));
            Assert.That(s.Battle.NavalExpansion.GroundingReady, Is.False);
            s.NavalExpansionEndPlayerTurn(); s.NavalExpansionBeginPlayerTurn();
            Play(s, "card_school_thunder_n05"); before = s.Battle.Energy;
            Play(s, "card_school_thunder_b04");
            Assert.That(s.Battle.Energy, Is.EqualTo(before - 1), "A new turn allows one new refund on a two-energy attack.");
        }

        [Test]
        public void CollapseMayTargetAwayFromEnemyAndRemovesOnlyOwnedThunderInSelectedSquare()
        {
            var s = Start(); Pose(s, false, 7, 7); s.Battle.Schools.ThunderChain = 3;
            AddCell(s, 1, 1, CoreNavalFieldKind.Thunder, true);
            AddCell(s, 2, 2, CoreNavalFieldKind.Thunder, true);
            AddCell(s, 2, 1, CoreNavalFieldKind.Thunder, false);
            AddCell(s, 1, 2, CoreNavalFieldKind.Fire, true);
            AddCell(s, 3, 1, CoreNavalFieldKind.Thunder, true);
            int before = s.Battle.EnemyHull;
            Play(s, "card_school_thunder_n06", Anchor(1, 1));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before));
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(3));
            Assert.That(s.GetNavalFieldAt(1, 1), Is.Null); Assert.That(s.GetNavalFieldAt(2, 2), Is.Null);
            Assert.That(s.GetNavalFieldAt(2, 1).PlayerOwned, Is.False);
            Assert.That(s.GetNavalFieldAt(1, 2).Kind, Is.EqualTo(CoreNavalFieldKind.Fire));
            Assert.That(s.GetNavalFieldAt(3, 1), Is.Not.Null);
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero, "An off-target finisher still clears its chain after resolution.");
        }

        [TestCase(true)] [TestCase(false)]
        public void CollapseDamageUsesAllPrePlayChainAndCountsOnlyRemovedOwnedCells(bool playerInside)
        {
            var s = Start(); Pose(s, false, 1, 0); s.Battle.Schools.ThunderChain = 3;
            Pose(s, true, playerInside ? 0 : -1, 0);
            AddCell(s, 0, 0, CoreNavalFieldKind.Thunder, true);
            AddCell(s, 1, 0, CoreNavalFieldKind.Thunder, true);
            AddCell(s, 0, 1, CoreNavalFieldKind.Thunder, false);
            AddCell(s, 1, 1, CoreNavalFieldKind.Blood, true);
            int enemy = s.Battle.EnemyHull, hull = s.Hull;
            Play(s, "card_school_thunder_n06", Anchor(0, 0));
            Assert.That(enemy - s.Battle.EnemyHull, Is.EqualTo(50));
            Assert.That(hull - s.Hull, Is.EqualTo(playerInside ? 50 : 0));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(playerInside ? 4 : 0),
                "The final clear is exempted by the pre-play location, even when the explosion removes that field.");
        }

        [Test]
        public void CollapseRejectionCannotConsumeChainEnergyOrFields()
        {
            var s = Start(); s.Battle.Schools.ThunderChain = 8;
            AddCell(s, 1, 1, CoreNavalFieldKind.Thunder, true);
            AddCell(s, 0, 0, CoreNavalFieldKind.Thunder, false);
            Put(s, "card_school_thunder_n06"); int energy = s.Battle.Energy;
            Assert.That(s.PlayNavalCard(0, Anchor(-1, -1)), Is.False);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(8));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(2));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void SuccessfulNonThunderPlayClearsOnlyOutsideFieldAndThunderUtilityNeverAdds(bool inField)
        {
            var s = Start(); s.Battle.Schools.ThunderChain = 8;
            if (inField) AddCell(s, 0, 0, CoreNavalFieldKind.Thunder, true);
            Play(s, "card_school_thunder_n04");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(8));
            s.Battle.Energy = 0; Put(s, "card_school_fire_n04");
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(8));
            s.Battle.Energy = 20; Play(s, "card_school_fire_n04");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(inField ? 8 : 0));
        }

        [Test]
        public void CardGainAndPrechargeAreSeparateOverloadsAndEachSubsequentGainTriggersAgain()
        {
            var s = Start(); SetHull(s, 5000); s.Battle.PlayerBlock = 1001;
            s.Battle.Schools.ThunderChain = 49; AddCell(s, 0, 0, CoreNavalFieldKind.Thunder, true);
            Play(s, "card_school_thunder_n03"); Play(s, "card_school_thunder_n02");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(52));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Hull, Is.EqualTo(4001), "The first 1000 damage uses guard; the second consumes its remaining point.");
            Play(s, "card_school_thunder_n02");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(53));
            Assert.That(s.Hull, Is.EqualTo(3001));
        }

        [Test]
        public void ThunderImmunityKeepsChainGainsAndFreeXWhilePreventingOnlyOverloadDamage()
        {
            var s = Start(); SetHull(s, s.MaxHull); s.Battle.PlayerBlock = 5;
            s.Battle.Schools.ThunderChain = 49; AddCell(s, 0, 0, CoreNavalFieldKind.Thunder, false);
            s.Battle.NavalFields.Player.FieldImmunityMask = 1 << (int)CoreNavalFieldKind.Thunder;
            s.Battle.NavalFields.Player.FieldImmunityExpires = s.Battle.NavalFields.PlayerTurn + 1;
            int hull = s.Hull;
            Play(s, "card_school_thunder_n03"); Play(s, "card_school_thunder_n02");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(52));
            Assert.That(s.Hull, Is.EqualTo(hull)); Assert.That(s.Battle.PlayerBlock, Is.EqualTo(5));
            int enemy = s.Battle.EnemyHull;
            Play(s, "card_school_thunder_b06");
            Assert.That(enemy - s.Battle.EnemyHull, Is.EqualTo(62));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(53));
            Assert.That(s.Hull, Is.EqualTo(hull)); Assert.That(s.Battle.PlayerBlock, Is.EqualTo(5));
        }

        [TestCase(49, 50, true)]
        [TestCase(48, 51, false)]
        public void LethalCardOverloadStopsRemainingChainEvents(int before, int after, bool prechargeRemains)
        {
            var s = Start(); SetHull(s, 1); s.Battle.Schools.ThunderChain = before;
            AddCell(s, 0, 0, CoreNavalFieldKind.Thunder, true);
            Play(s, "card_school_thunder_n03");
            var battle = s.Battle;
            var expansion = battle.NavalExpansion;
            Play(s, "card_school_thunder_n02");
            Assert.That(s.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
            Assert.That(s.Hull, Is.Zero);
            Assert.That(battle.Schools.ThunderChain, Is.EqualTo(after));
            Assert.That(expansion.Precharge, Is.EqualTo(prechargeRemains), "Inspect the resolved effect state before battle cleanup replaces it.");
        }

        [Test]
        public void RemovingOwnThunderBeforeCardGainPreventsAnOverloadOnTheNowEmptyCell()
        {
            var s = Start(); s.Battle.Schools.ThunderChain = 49; Pose(s, false, 1, 0);
            AddCell(s, 0, 0, CoreNavalFieldKind.Thunder, true);
            int hull = s.Hull;
            Play(s, "card_school_thunder_n06", Anchor(0, 0));
            Assert.That(s.Hull, Is.EqualTo(hull - 414));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(50));
            Assert.That(s.GetNavalFieldAt(0, 0), Is.Null);
        }

        private static CoreVoyageSession Start()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("THUNDER-NAVAL-REVISION"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 1000);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0; s.Battle.Energy = 100;
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100;
            s.Battle.Hand.Clear(); s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            return s;
        }
        private static void AddCell(CoreVoyageSession s, int x, int y, CoreNavalFieldKind kind, bool owned) =>
            s.Battle.NavalFields.Cells.Add(new CoreNavalFieldCell { X = x, Y = y, Kind = kind, PlayerOwned = owned,
                InstanceId = s.Battle.NavalFields.NextInstanceId++, ExpiresAtOwnerTurn = 5 });
        private static CoreNavalCardPlayOptions Anchor(int x, int y) => new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = x, AnchorY = y };
        private static void SetHull(CoreVoyageSession s, int hull) => typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, hull);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Put(CoreVoyageSession s, string id) { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id)); }
        private static void Play(CoreVoyageSession s, string id, CoreNavalCardPlayOptions options = null)
        {
            Put(s, id);
            Assert.That(CoreNavalSchoolExpansionCatalog.FindSpec(id) == null ? s.PlayCard(0) : s.PlayNavalCard(0, options),
                Is.True, id + ": " + s.CardUnavailableReason(0));
        }
    }
}
