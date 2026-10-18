using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardActionPointUpgradeTests
    {
        // Independent approved gains at requested ranks +0/+1/+2/+3, including each card's cap.
        private const string Curves = @"
card_approach|1/2/2/2
card_retreat|1/2/2/2
card_full_sail_turn|1/2/2/2
card_navigator_course|1/2/2/2
card_component_sail|1/2/2/2
card_school_tide_b05|1/2/2/2
card_school_thunder_b05|1/2/2/2
card_school_fire_b06|1/1/2/2
card_school_curse_b06|1/1/2/2
card_special_crew_broken_anchor_navigator_lorne|1/2/2/2
card_special_crew_bronze_bell_navigator_vick|1/1/2/2
card_special_crew_gray_gull_navigator_evelyn|1/1/2/2
card_special_crew_salt_crown_navigator_ashir|1/2/2/2
card_special_crew_white_shell_navigator_saina|1/1/2/2
card_special_bow_amber_albatross|1/2/2/2
card_special_bow_guildbreaker_crest|1/2/2/2
card_special_bow_pearl_siren|1/2/2/2
card_special_instrument_corsair_decoder|1/2/2/2
card_special_instrument_guild_engine|1/2/2/2
card_special_instrument_trade_wind_oracle|1/2/2/2
card_special_sail_evening_silk|1/2/2/2
card_special_sail_guild_silk|1/2/2/2
card_special_sail_redroute_broadsail|1/2/2/2
card_special_sail_sunthread_spinnaker|1/2/2/2
card_special_sail_whisperveil|1/2/2/2
card_special_sail_aurora_fan|1/2/2/2
card_special_sail_eclipse_shroud|1/2/2/2
card_special_sail_fogshroud_wing|1/2/2/2
card_special_sail_ghostcurrent_canvas|1/2/2/2
card_special_sail_moonweave|1/2/2/2
card_special_sail_starshear_rig|1/2/2/2
card_special_sail_crown_of_gales|1/2/2/2
card_special_sail_last_free_wind|1/2/2/2
card_special_sail_skybreaker_array|1/2/2/2
card_special_sail_tempest_wing|1/2/3/3
card_special_sail_thunderphoenix|1/2/2/2
card_special_sail_thunderreef_wing|1/2/2/2";

        private static IEnumerable<TestCaseData> AuthoredCurves()
        {
            foreach (string row in Curves.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = row.Split('|');
                yield return new TestCaseData(fields[0], fields[1].Split('/').Select(int.Parse).ToArray());
            }
        }

        [TestCaseSource(nameof(AuthoredCurves))]
        public void EveryMovementCardUsesItsIndependentCurveInPropertiesDescriptionsAndFace(string id, int[] gains)
        {
            var session = new CoreVoyageSession();
            CoreCardUpgradeProfile profile = CoreCardUpgradeDesign.Find(id);
            int lastGrowth = Enumerable.Range(1, 3).Where(rank => gains[rank] > gains[rank - 1]).Last();
            Assert.That(profile.LastActionPointGrowthLevel, Is.EqualTo(lastGrowth), id);
            for (int requested = -1; requested <= 4; requested++)
            {
                int expected = gains[Math.Max(0, Math.Min(3, requested))];
                CoreBattleCard card = session.GetBattleCardAtLevel(Index(id), requested);
                Assert.That(card.NavalActionPointGain, Is.EqualTo(expected), id + " +" + requested);
                Assert.That(profile.ActionPointBonus(requested), Is.EqualTo(expected - 1));
                Assert.That(card.Description, Does.Contain("获得 " + expected + " 点行动点"), id);
                foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval })
                    if (card.IsAllowedInStage(stage))
                        Assert.That(session.BattleCardFaceDescription(card, stage), Does.Contain("获得 " + expected + " 点行动点"), id);
                Assert.That(card.Description, Does.Not.Contain("{navalActionPoints}"));
            }
            Assert.That(session.GetCardUpgradeLevel(id), Is.Zero, "Previews must not change saved progress.");
            Assert.That(session.SpecialCardExperience, Is.Empty);
            Assert.That(session.CardUpgradeLevels, Is.Empty);
        }

        [Test]
        public void OnlyTheThirtySevenMovementCardsGainActionPointsAtAnyRank()
        {
            var expected = new HashSet<string>(Curves.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(row => row.Split('|')[0]), StringComparer.Ordinal);
            Assert.That(expected, Has.Count.EqualTo(37));
            var session = new CoreVoyageSession();
            CoreBattleCard[] live = CoreVoyageSession.CardLibrary.Where(card => card.AllowedStages != NavalBattleStages.None).ToArray();
            Assert.That(live.Where(card => card.NavalActionPointGain > 0).Select(card => card.Id), Is.EquivalentTo(expected));
            foreach (CoreBattleCard original in live.Where(card => !expected.Contains(card.Id)))
                for (int rank = 0; rank <= 3; rank++)
                {
                    Assert.That(session.GetBattleCardAtLevel(Index(original.Id), rank).NavalActionPointGain, Is.Zero, original.Id);
                    Assert.That(CoreCardUpgradeDesign.Find(original).ActionPointBonus(rank), Is.Zero, original.Id);
                }
        }

        [TestCase(NavalBattleCardAction.Approach, 1)]
        [TestCase(NavalBattleCardAction.Standard, 0)]
        public void UnknownFixtureCardsDoNotInventActionPointUpgrades(NavalBattleCardAction action, int gain)
        {
            var card = new CoreBattleCard("unpublished_ap_fixture", "测试", "机动", 1, 0, 0, 0, 0, 0,
                "获得 {navalActionPoints} 点行动点。", action: action, upgradeLevel: 3);
            Assert.That(CoreCardUpgradeDesign.Find(card), Is.Null);
            Assert.That(card.NavalActionPointGain, Is.EqualTo(gain));
            Assert.That(CoreBattleUpgradeBalance.MaximumLevelFor(card), Is.Zero);
        }

        [TestCase("card_approach", 0, 1)]
        [TestCase("card_approach", 1, 2)]
        [TestCase("card_approach", 3, 2)]
        [TestCase("card_special_crew_broken_anchor_navigator_lorne", 1, 2)]
        [TestCase("card_special_crew_gray_gull_navigator_evelyn", 1, 1)]
        [TestCase("card_special_crew_gray_gull_navigator_evelyn", 3, 2)]
        [TestCase("card_special_sail_tempest_wing", 1, 2)]
        [TestCase("card_special_sail_tempest_wing", 2, 3)]
        [TestCase("card_special_sail_tempest_wing", 3, 3)]
        public void RealPlaysMatchPreviewAndRepeatCostsWithoutMovingTheShip(string id, int rank, int gain)
        {
            CoreVoyageSession session = Battle(id, rank);
            CoreBattleCard card = session.GetBattleCard(Index(id));
            CoreBattleState battle = session.Battle;
            int points = battle.NavalActionPoints, energy = battle.Energy, revision = battle.TacticalRevision;
            VoyagePoint position = battle.PlayerPositionMeters;
            float heading = battle.PlayerHeadingDegrees;
            string face = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            Assert.That(face, Does.Contain("获得 " + gain + " 点行动点"));
            Assert.That(battle.NavalActionPoints, Is.EqualTo(points));
            Assert.That(battle.Energy, Is.EqualTo(energy));
            for (int play = 0; play < 2; play++)
            {
                Assert.That(session.GetBattleCardEnergyCost(Index(id)), Is.EqualTo(card.Energy + play));
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                energy -= card.Energy + play;
                Assert.That(battle.Energy, Is.EqualTo(energy));
                Assert.That(battle.NavalActionPoints, Is.EqualTo(points + (play + 1) * gain));
                Assert.That(battle.PlayerPositionMeters.X, Is.EqualTo(position.X));
                Assert.That(battle.PlayerPositionMeters.Y, Is.EqualTo(position.Y));
                Assert.That(battle.PlayerHeadingDegrees, Is.EqualTo(heading));
                Assert.That(battle.TacticalRevision, Is.EqualTo(revision));
            }
            battle.Hand.Clear(); battle.Hand.Add(Index(id)); battle.Energy = 0;
            int beforeFailedPlay = battle.NavalActionPoints;
            Assert.That(session.PlayCard(0), Is.False, "Repeat costs still apply even to upgraded zero-cost movement.");
            Assert.That(battle.NavalActionPoints, Is.EqualTo(beforeFailedPlay));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(battle.NavalActionPoints, Is.EqualTo(1), "Unused upgraded action points do not carry to the next turn.");
            Assert.That(session.GetBattleCardEnergyCost(Index(id)), Is.EqualTo(card.Energy));
        }

        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("thunder")]
        [TestCase("blood")]
        public void LegendarySailsNeverLoseActionPointsToEpicSailsAtMatchedOrMaximumRanks(string school)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard epic = CoreSpecialCardCatalog.All.Single(d => d.Card.ArtId == "card_school_" + school + "_e02").Card;
            CoreBattleCard legendary = CoreSpecialCardCatalog.All.Single(d => d.Card.ArtId == "card_school_" + school + "_l02").Card;
            for (int rank = 0; rank <= 3; rank++)
                Assert.That(session.GetBattleCardAtLevel(Index(legendary.Id), rank).NavalActionPointGain,
                    Is.GreaterThanOrEqualTo(session.GetBattleCardAtLevel(Index(epic.Id), rank).NavalActionPointGain), school + " +" + rank);
            Assert.That(session.GetBattleCardAtLevel(Index(legendary.Id), session.GetCardMaximumUpgradeLevel(legendary.Id)).NavalActionPointGain,
                Is.GreaterThanOrEqualTo(session.GetBattleCardAtLevel(Index(epic.Id), session.GetCardMaximumUpgradeLevel(epic.Id)).NavalActionPointGain));
        }

        private static CoreVoyageSession Battle(string id, int rank)
        {
            var session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(CoreSpecialCardCatalog.All.Where(d => d.SourceKind == CoreSpecialCardSourceKind.Crew).Select(d => d.SourceId).ToArray(),
                CoreSpecialCardCatalog.All.Where(d => d.SourceKind == CoreSpecialCardSourceKind.Component).Select(d => d.SourceId).ToArray());
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            session.SetSeed("AUTHORED-ACTION-POINTS");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.Energy = 100;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Wood = session.Cargo.Medicine = 100;
            session.Battle.Hand.Clear(); session.Battle.Hand.AddRange(new[] { Index(id), Index(id) });
            session.Battle.DiscardPile.Clear(); session.Battle.DrawPile.Clear();
            session.Battle.DrawPile.AddRange(Enumerable.Repeat(Index("card_brace"), 20));
            return session;
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
    }
}
