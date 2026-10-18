using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class BoardingStageBlockTests
    {
        private static IEnumerable<object[]> ChefCases()
        {
            foreach(string crew in new[]{"crew_gray_gull_cook_pepe","crew_salt_crown_cook_maz"})
                for(int rank=0;rank<=3;rank++)
                    foreach(NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                        yield return new object[]{crew,rank,stage};
        }

        [TestCaseSource(nameof(ChefCases))]
        public void AllStageChef_QuotesAndPaysItsPhaseShieldAtEveryRank(string crew,int rank,NavalBattleStage stage)
        {
            var s=new CoreVoyageSession();s.SelectShip(2);
            var source=CoreSpecialCardCatalog.ForCrew(crew);
            int index=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(i=>CoreVoyageSession.CardLibrary[i].Id==source.Card.Id);
            s.ConfigureSpecialCardSources(new[]{crew},Array.Empty<string>());
            s.RestoreSpecialCardExperience(new Dictionary<string,int>{{source.Card.Id,new[]{0,120,360,840}[rank]}});
            var card=s.GetBattleCard(index);
            bool pepe=crew=="crew_gray_gull_cook_pepe";
            int block=(pepe?new[]{6,8,8,8}:new[]{6,8,11,11})[rank];
            int energy=pepe?1:2;
            int draw=pepe?1:new[]{2,3,4,4}[rank];
            Assert.That(card.UpgradeLevel,Is.EqualTo(Math.Min(rank,pepe?1:2)));
            Assert.That(card.Block,Is.EqualTo(block));
            Assert.That(card.BoardingBlock,Is.Null);
            Assert.That(card.Description,Does.Contain("获得 "+block+" 格挡").And.Not.Contain("{"));
            Assert.That(s.GetBattleCardEffects(card,stage).Block,Is.EqualTo(block));
            string guardText="获得 "+block+" 格挡";
            Assert.That(s.BattleCardFaceDescription(card,stage),Does.Contain(guardText).And.Not.Contain("{"));
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]),Is.True);
            var b=s.Battle;b.Stage=stage;b.PlayerBlock=0;b.Energy=3;b.Hand.Clear();b.Hand.Add(index);
            int filler=s.GetBattleDeck(stage)[0];b.DrawPile.Clear();for(int i=0;i<4;i++)b.DrawPile.Add(filler);
            int hull=s.Hull,health=b.PlayerBoardingHealth,medicine=s.Cargo.Medicine,wood=s.Cargo.Wood;
            Assert.That(s.GetBattleCardEffects(card,stage).Block,Is.EqualTo(block));
            Assert.That(s.BattleCardFaceDescription(card,stage),Does.Contain(guardText).And.Not.Contain("{"));
            string effectGuardText=stage==NavalBattleStage.Boarding?"格挡 +"+block:guardText;
            Assert.That(s.BattleCardEffectDescription(card,stage),Does.Contain(effectGuardText).And.Not.Contain("{"));
            Assert.That(s.GetBattleCardEnergyCost(index),Is.EqualTo(energy));
            Assert.That(s.PlayCard(0),Is.True);
            Assert.That(b.PlayerBlock,Is.EqualTo(block));
            Assert.That(b.Hand,Has.Count.EqualTo(draw));
            Assert.That(b.Energy,Is.EqualTo(3-energy));
            Assert.That(s.GetBattleCardEnergyCost(index),Is.EqualTo(energy+1));
            Assert.That(new[]{s.Hull,b.PlayerBoardingHealth,s.Cargo.Medicine,s.Cargo.Wood},Is.EqualTo(new[]{hull,health,medicine,wood}));
            Assert.That(source.Card.Block,Is.EqualTo(6),"Quoting/upgrading must leave the catalogue immutable.");
            Assert.That(source.Card.BoardingBlock,Is.Null);
        }

        [Test]
        public void NullableBoardingOverride_DistinguishesNoOverrideFromZero()
        {
            var s=new CoreVoyageSession();
            var ordinary=new CoreBattleCard("normal","normal","test",1,0,5,0,0,0,"格挡 +{block}。");
            var overridden=new CoreBattleCard("override","override","test",1,0,5,0,0,0,"格挡 +{block}；接舷 {boardingBlock}。",boardingBlock:0);
            Assert.That(ordinary.BoardingBlock,Is.Null);
            Assert.That(s.GetBattleCardEffects(ordinary,NavalBattleStage.Boarding).Block,Is.EqualTo(5));
            Assert.That(s.GetBattleCardEffects(overridden,NavalBattleStage.Boarding).Block,Is.Zero);
            Assert.That(s.GetBattleCardEffects(overridden,NavalBattleStage.Preparation).Block,Is.EqualTo(5));
            Assert.That(s.GetBattleCardEffects(overridden,NavalBattleStage.Naval).Block,Is.EqualTo(5));
            Assert.That(overridden.Description,Does.Contain("接舷 0").And.Not.Contain("{"));
        }

        [Test]
        public void ResourceFallback_MaterializesTwoBlockInsteadOfKeepingFullBoardingOverride()
        {
            var s=new CoreVoyageSession();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]),Is.True);
            s.Battle.Stage=NavalBattleStage.Boarding;
            var card=new CoreBattleCard("tide_override","tide override","test",1,0,8,0,0,0,"格挡 +{block}。",
                NavalBattleStages.Boarding,schoolId:"tide",
                schoolEffect:new CoreSchoolCardEffect(CoreSchoolMechanic.TideWard,fallbackBlock:2,woodCost:1),boardingBlock:10);
            s.Cargo.Wood=1;
            Assert.That(s.GetBattleCardEffects(card,NavalBattleStage.Boarding).Block,Is.EqualTo(10));
            s.Cargo.Wood=0;
            Assert.That(s.GetBattleCardEffects(card,NavalBattleStage.Boarding).Block,Is.EqualTo(2));
            Assert.That(s.BattleCardFaceDescription(card,NavalBattleStage.Boarding),Does.Contain("格挡 +2"));
            Assert.That(card.BoardingBlock,Is.EqualTo(10),"The supply quote never mutates the original definition.");
        }
    }
}
