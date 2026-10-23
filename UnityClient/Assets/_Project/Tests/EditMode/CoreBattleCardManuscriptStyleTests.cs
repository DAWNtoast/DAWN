using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardManuscriptStyleTests
    {
        [TestCase("card_load_shot", "额外装药：", "下次炮击伤害提高")]
        [TestCase("card_deck_polish", "额外格挡：", "船体防御牌提供")]
        [TestCase("card_chain_shot", "范围1-4格：", "对4格内的")]
        [TestCase("card_boarding_thunder_05", "延迟：", "伤害在使用本牌时确定")]
        [TestCase("card_boarding_thunder_11", "点伤害2次", "每次造成")]
        [TestCase("card_boarding_blood_05", "获得", "恢复2点能量")]
        [TestCase("card_boarding_thunder_07", "连锁2：", "连锁达到")]
        public void RemainingCardsAndAllUpgradesFollowTheManuscript(string id, string expected, string retired)
        {
            var session = new CoreVoyageSession();
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                string text = Compact(card.Description);
                Assert.That(text, Does.Contain(expected).And.Not.Contain(retired), id + " +" + rank);
            }
        }

        [Test]
        public void AllCardSurfacesUseSingleStackStatusWordingAndKeepZeroEffects()
        {
            var session = new CoreVoyageSession();
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                if (CoreVoyageSession.CardLibrary[index].AllowedStages == NavalBattleStages.None) continue;
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                    foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                    {
                        if (!card.IsAllowedInStage(stage)) continue;
                        foreach (string text in new[] { card.Description, session.BattleCardFaceDescription(card, stage), session.BattleCardEffectDescription(card, stage) })
                        {
                            Assert.That(text, Does.Not.Contain("咒痕").And.Not.Match(@"(?:燃烧|灼烧|自燃)\s*\d+×\d+\s*回合"), card.Id + " +" + rank);
                            Assert.That(text, Does.Not.Match(@"每层[^；。]*伤害[^；。]*持续\s*\d+"), card.Id + " +" + rank);
                        }
                    }
                }
            }
            CoreBattleCard zero = CoreVoyageSession.CardLibrary.Single(card => card.Id == "card_full_sail_turn");
            Assert.That(Compact(zero.Description), Does.Contain("获得0点格挡"));
        }

        private static string Compact(string text) => Regex.Replace(text, @"\s+", string.Empty);
    }
}
