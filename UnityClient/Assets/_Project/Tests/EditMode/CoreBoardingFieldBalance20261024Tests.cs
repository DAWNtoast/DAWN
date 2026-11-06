using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    // Unit boundaries for the boarding rebase: card outputs may grow, but field
    // durations, persistent resources and status ticking retain their own units.
    public sealed class CoreBoardingFieldBalance20261024Tests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void FiveTurnInheritedFireUsesFullDurationThenOwnerClock(bool playerOwned)
        {
            var s=Battle(CoreNavalFieldKind.Fire,5,playerOwned);
            Assert.That(s.Battle.BoardingFields.Fields.Single().PlayerOwned,Is.EqualTo(playerOwned));
            Invoke(s,"BPlayerEnd");
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage,Is.EqualTo(5));
            Assert.That(s.Battle.PlayerBoardingHealth,Is.EqualTo(10000));
            Assert.That(s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire),Is.EqualTo(playerOwned?5:4));
            Invoke(s,"BEnemyEnd");
            Assert.That(s.Battle.BoardingEnemy.SelfBurnDamage,Is.EqualTo(playerOwned?5:4));
            Assert.That(s.Battle.EnemyBoardingHealth,Is.EqualTo(10000));
            Assert.That(s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire),Is.EqualTo(4));
            Invoke(s,"BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth,Is.EqualTo(9995));
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage,Is.EqualTo(8));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void CardBurnPaysActualLayerCountThenLosesOnlyOneLayer(int rank)
        {
            var s=Battle(rank:rank);Play(s,"fire_03");
            int burn=s.Battle.BoardingEnemy.SelfBurnDamage;
            Assert.That(burn,Is.GreaterThan(2));s.Battle.EnemyBlock=2;
            Invoke(s,"BEnemyEnd");
            Assert.That(s.Battle.EnemyBlock,Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth,Is.EqualTo(10000-(burn-2)));
            Assert.That(s.Battle.BoardingEnemy.SelfBurnDamage,Is.EqualTo(burn-1));
        }

        [TestCase(0)]
        [TestCase(3)]
        public void ContinuousRecoveryUsesOneLayerAsOneHealthAndDecaysByOne(int rank)
        {
            var s=Battle(rank:rank);s.Battle.PlayerBoardingHealth=100;s.Cargo.Medicine=1;
            Play(s,"basic_07");int recovery=s.Battle.Boarding.ContinuousRecovery;
            Assert.That(recovery,Is.GreaterThan(2));Assert.That(s.Cargo.Medicine,Is.Zero);
            Invoke(s,"BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth,Is.EqualTo(100+recovery));
            Assert.That(s.Battle.Boarding.ContinuousRecovery,Is.EqualTo(recovery-1));
            Invoke(s,"BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth,Is.EqualTo(100+recovery+(recovery-1)));
            Assert.That(s.Battle.Boarding.ContinuousRecovery,Is.EqualTo(recovery-2));
        }

        [Test]
        public void ArcaneFieldAddsThreeActualManaWithoutMultiplyingCardGeneration()
        {
            var plain=Battle();var field=Battle(CoreNavalFieldKind.Arcane,3);
            Assert.That(field.Battle.Schools.Mana-plain.Battle.Schools.Mana,Is.EqualTo(3));
            Play(plain,"arcane_03");Play(field,"arcane_03");
            Assert.That(field.Battle.Schools.Mana-plain.Battle.Schools.Mana,Is.EqualTo(3));
            field.Battle.Schools.Mana=999;
            Assert.That(field.EndBattleTurn(),Is.True);
            Assert.That(field.Battle.Schools.Mana,Is.EqualTo(field.Battle.Schools.ManaLimit+2));
        }

        [Test]
        public void TideChargesOnlyActualBurnAbsorbedAndKeepsReserveAfterTurn()
        {
            var s=Battle();Play(s,"tide_01");int guard=s.Battle.PlayerBlock;
            Invoke(s,"BApplyPlayerBurn",guard+3,false);
            Invoke(s,"BPlayerEnd");
            Assert.That(s.Battle.PlayerBlock,Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth,Is.EqualTo(9997));
            Assert.That(s.Battle.Boarding.TidePower,Is.EqualTo(guard));
            Invoke(s,"BPlayerStart");
            Assert.That(s.Battle.Boarding.TidePower,Is.EqualTo(guard));
        }

        [Test]
        public void BloodPoolReturnsTwiceActualPaymentWithoutRebasingItAgain()
        {
            var s=Battle(CoreNavalFieldKind.Blood,5);Play(s,"blood_01");
            int paid=10000-s.Battle.PlayerBoardingHealth;
            Assert.That(paid,Is.GreaterThan(0));
            Assert.That(s.Battle.PlayerBlock,Is.EqualTo(2*paid));
        }

        [Test]
        public void ThunderStillOverloadsAtFiftyForExactlyOneThousandBlockableDamage()
        {
            var s=Battle(CoreNavalFieldKind.Thunder,5);s.Battle.Schools.ThunderChain=49;
            s.Battle.PlayerBoardingHealth=2000;s.Battle.PlayerBlock=100;
            Play(s,"thunder_01");
            Assert.That(s.Battle.Schools.ThunderChain,Is.EqualTo(50));
            Assert.That(s.Battle.PlayerBlock,Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth,Is.EqualTo(1100));
            Play(s,"thunder_01");
            Assert.That(s.Battle.Schools.ThunderChain,Is.EqualTo(51));
            Assert.That(s.Battle.PlayerBoardingHealth,Is.EqualTo(100));
        }

        [Test]
        public void ThunderFieldPreservesFinisherCostAndOrdinaryCardChain()
        {
            var s=Battle(CoreNavalFieldKind.Thunder,5);s.Battle.Schools.ThunderChain=10;
            Play(s,"thunder_13");Assert.That(s.Battle.Schools.ThunderChain,Is.EqualTo(11));
            Play(s,"basic_04");Assert.That(s.Battle.Schools.ThunderChain,Is.EqualTo(11));
            Invoke(s,"BPlayerStart");Assert.That(s.Battle.Schools.ThunderChain,Is.EqualTo(11));
            s.Battle.BoardingFields.Fields.Clear();Play(s,"thunder_13");
            Assert.That(s.Battle.Schools.ThunderChain,Is.EqualTo(1));
        }

        [TestCase("arcane_04",4,5,6)]
        [TestCase("curse_06",4,5,1)]
        [TestCase("tide_06",4,5,1)]
        [TestCase("thunder_13",4,5,7)]
        [TestCase("thunder_15",10,11,16)]
        [TestCase("thunder_15",20,21,32)]
        public void OneExtraResourceKeepsItsAuthoredMarginalDamage(string key,int low,int high,int expectedExtra)
        {
            Assert.That(ResourceDamage(key,high)-ResourceDamage(key,low),Is.EqualTo(expectedExtra),
                "A resource already counts actual mana/marks/absorbed damage/chain; the boarding rebase must not multiply its coefficient a second time.");
        }

        [TestCase("fire_13")]
        [TestCase("blood_08")]
        public void MultiHitEnemyPreviewUsesTheSameRebasedRetaliationAsExecution(string key)
        {
            var s=Battle(key=="fire_13"?(CoreNavalFieldKind?)CoreNavalFieldKind.Fire:null,key=="fire_13"?5:0);
            s.Battle.Profile=new CoreBattleProfile(CoreBossKind.None,CoreBattleArchetype.Balanced,
                1000,0,10,4,0,28,1,1,healthMultiplier:1,boardingHealthMultiplier:1);
            s.Battle.BoardingEnemy.Deck=CoreBoardingEnemyCatalog.ForSchool("thunder");
            Play(s,key);s.Battle.PlayerBlock=100;
            var preview=s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Count,Is.GreaterThan(1));
            int expectedEnemyHealth=preview.Actions.Last().EnemyHealthAfter-preview.EnemySelfBurnDamage;
            int expectedPlayerHealth=10000-preview.PlayerBurnDamage-preview.PlayerHealthLoss;
            Assert.That(s.EndBattleTurn(),Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth,Is.EqualTo(expectedEnemyHealth));
            Assert.That(s.Battle.PlayerBoardingHealth,Is.EqualTo(expectedPlayerHealth));
        }

        private static int ResourceDamage(string key,int amount)
        {
            var s=Battle();
            if(key.StartsWith("arcane_",StringComparison.Ordinal))s.Battle.Schools.Mana=amount;
            else if(key.StartsWith("curse_",StringComparison.Ordinal))Invoke(s,"CTBAddMarks",amount);
            else if(key.StartsWith("tide_",StringComparison.Ordinal))Invoke(s,"CTBGainTide",amount);
            else s.Battle.Schools.ThunderChain=amount;
            Play(s,key);return 10000-s.Battle.EnemyBoardingHealth;
        }
        private static CoreVoyageSession Battle(CoreNavalFieldKind? kind=null,int turns=0,bool owner=true,int rank=0)
        {
            var s=new CoreVoyageSession();s.SetSeed("BOARDING-FIELD-REBASE-REGRESSION");
            s.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10)});
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            if(rank>0)s.ConfigureCardUpgrades(CoreBoardingCardCatalog.All.ToDictionary(c=>c.Id,c=>rank));
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]),Is.True);Assert.That(s.EndBattleTurn(),Is.True);
            Invoke(s.Battle,"SetTacticalPose",true,new VoyagePoint(0,0),90f);
            Invoke(s.Battle,"SetTacticalPose",false,new VoyagePoint(100,0),90f);
            s.Battle.Alignment=NavalBattleAlignment.Parallel;s.Battle.NavalActionPoints=1;
            if(kind.HasValue)
            {
                Assert.That(s.PlaceNavalField(kind.Value,owner,0,0,false,1,0),Is.True);
                foreach(var cell in s.Battle.NavalFields.Cells)cell.ExpiresAtOwnerTurn+=turns-(kind==CoreNavalFieldKind.Arcane?3:5);
            }
            Assert.That(s.TryBoard(),Is.True);
            s.Battle.PlayerBoardingHealth=s.Battle.PlayerMaxBoardingHealth=10000;
            s.Battle.EnemyBoardingHealth=s.Battle.EnemyMaxBoardingHealth=10000;
            s.Battle.PlayerBlock=s.Battle.EnemyBlock=0;s.Cargo.Powder=100;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s,0);
            return s;
        }
        private static void Play(CoreVoyageSession s,string key)
        {
            int index=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).Single(i=>CoreVoyageSession.CardLibrary[i].Id=="card_boarding_"+key);
            s.Battle.Hand.Clear();s.Battle.Hand.Add(index);s.Battle.Energy=100;
            Assert.That(s.PlayCard(0),Is.True,key+": "+s.CardUnavailableReason(0));
        }
        private static object Invoke(object target,string method,params object[] args)=>target.GetType()
            .GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
    }
}
