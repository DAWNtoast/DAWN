using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalBossCalibrationTests
    {
        [TestCase("boss_route_shattered_sail_reef",144,6,4,0)]
        [TestCase("boss_route_shattered_sail_reef_tide",135,6,3,0)]
        [TestCase("boss_route_golden_tide_trade",360,23,8,0)]
        [TestCase("boss_route_golden_tide_trade_thunder",375,25,9,0)]
        [TestCase("boss_route_moon_tide_ruins",600,33,13,0)]
        [TestCase("boss_route_moon_tide_ruins_arcane",630,35,14,0)]
        [TestCase("legendary_pirate_iron_oath",420,21,18,12)]
        [TestCase("legendary_pirate_red_widow",420,22,12,8)]
        [TestCase("legendary_pirate_thunder_crown",750,52,20,8)]
        [TestCase("legendary_pirate_ember_ark",690,44,18,8)]
        [TestCase("legendary_pirate_tide_requiem",1680,86,24,16)]
        [TestCase("legendary_pirate_last_horizon",2280,86,30,8)]
        [TestCase(CoreBattleBalance.TreasureBossId,1500,80,36,20)]
        public void BossEntryUsesCalibratedFinalNavalValues(string id,int hull,int cannon,int guard,int initial)
        {
            CoreEncounter encounter=Encounter(id);
            CoreBattleProfile profile=CoreBattleBalance.Resolve(encounter,0,true);
            Assert.That(new[]{profile.EnemyHull,profile.CannonDamage,profile.Guard,profile.InitialGuard},
                Is.EqualTo(new[]{hull,cannon,guard,initial}),id);
            Assert.That(profile.HealthMultiplier,Is.EqualTo(1.5m));
            var session=new CoreVoyageSession();
            Call(session,"StartBattle",encounter,true);
            Assert.That(session.Battle.EnemyHull,Is.EqualTo(hull));
            Assert.That(session.Battle.EnemyMaxHull,Is.EqualTo(hull));
            Assert.That(session.Battle.EnemyBlock,Is.EqualTo(initial));
            Assert.That(session.Battle.Stage,Is.EqualTo(NavalBattleStage.Preparation));
            Assert.That(session.NavalDistanceCells,Is.EqualTo(6));
        }

        [Test]
        public void AllFifteenProfilesUseBoardingCalibrationAndPreserveContactParameters()
        {
            // Boarding calibration: id, crew, HP, melee, guard, HP multiplier numerator/denominator,
            // bow/hull quality, ram probability and legacy range. Includes both tutorial paths.
            string[] baseline={
                "boss_route_shattered_sail_reef,9,300,20,10,300/112,1.8,1.6,0.12,100",
                "boss_route_shattered_sail_reef_tide,9,320,20,15,320/112,1.9,1.7,0.16,100",
                "boss_route_golden_tide_trade,16,480,30,15,480/168,2.4,2.5,0.25,100",
                "boss_route_golden_tide_trade_thunder,16,500,30,15,500/168,2.4,2.4,0.22,100",
                "boss_route_moon_tide_ruins,25,600,40,20,600/240,3,3,0.35,120",
                "boss_route_moon_tide_ruins_arcane,25,620,38,20,620/240,3.1,3,0.30,120",
                "legendary_pirate_iron_oath,55,830,42,35,830/480,4.3,5,0.40,120",
                "legendary_pirate_thunder_crown,52,960,44,20,960/456,4.3,4.4,0.35,100",
                "legendary_pirate_red_widow,66,800,48,20,800/568,4.8,4.3,0.65,140",
                "legendary_pirate_ember_ark,60,810,46,25,810/520,4.4,4.5,0.45,120",
                "legendary_pirate_tide_requiem,62,460,40,40,460/536,4.6,4.6,0.55,130",
                "legendary_pirate_last_horizon,58,610,44,30,610/504,4.6,4.7,0.45,125",
                "treasure_boss_moon_vault,31,1510,44,30,1510/288,3.7,3.9,0.40,120",
                "tutorial_profile,2,56,5,3,1,1,1,0,100",
                "tutorial_replay,2,84,5,3,1.5,1,1,0,100"
            };
            Assert.That(CoreRouteBossCatalog.All.Count+CoreLegendaryWarships.All.Count+3,Is.EqualTo(baseline.Length));
            foreach(string row in baseline)
            {
                string[] c=row.Split(',');
                CoreBattleProfile p=c[0]=="tutorial_profile"?CoreGuidedTutorialContent.BossProfile:
                    CoreBattleBalance.Resolve(Encounter(c[0]),2,true);
                Assert.That(new[]{p.OrdinaryCrewCount,p.BoardingHealth,p.MeleeDamage,p.BoardingGuard},
                    Is.EqualTo(c.Skip(1).Take(4).Select(int.Parse).ToArray()),c[0]);
                Assert.That(p.BoardingHealthMultiplier,Is.EqualTo(Ratio(c[5])),c[0]);
                Assert.That(p.BowQuality,Is.EqualTo(float.Parse(c[6],CultureInfo.InvariantCulture)).Within(.0001),c[0]);
                Assert.That(p.HullQuality,Is.EqualTo(float.Parse(c[7],CultureInfo.InvariantCulture)).Within(.0001),c[0]);
                Assert.That(p.RamChance,Is.EqualTo(double.Parse(c[8],CultureInfo.InvariantCulture)).Within(.000001),c[0]);
                Assert.That(p.RamRangeMeters,Is.EqualTo(float.Parse(c[9],CultureInfo.InvariantCulture)),c[0]);
            }
        }

        [Test]
        public void GuidedPracticeAndFinalBattleKeepIndependentHealth()
        {
            var session=new CoreVoyageSession();
            session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
            Call(session,"StartBattle",Encounter("tutorial_replay"),true);
            Assert.That(session.Battle.EnemyHull,Is.EqualTo(1000));
            Assert.That(session.Battle.EnemyBoardingHealth,Is.EqualTo(1000));
            Call(session,"StartGuidedBossLesson",GuidedBossLesson.FinalBattle);
            Assert.That(session.Battle.EnemyHull,Is.EqualTo(80));
            Assert.That(session.Battle.EnemyMaxHull,Is.EqualTo(80));
            Assert.That(session.Battle.EnemyBoardingHealth,Is.EqualTo(40));
            Assert.That(session.Battle.EnemyMaxBoardingHealth,Is.EqualTo(40));
            Assert.That(session.Battle.Profile.CannonDamage,Is.EqualTo(7));
            Assert.That(session.Battle.Profile.Guard,Is.EqualTo(3));
        }

        [Test]
        public void FiveFixedTrainingTargetsKeepNavalAndBoardingBaselines()
        {
            int[][] baseline={new[]{120,80,10,8,2,0},new[]{220,140,16,12,5,2},
                new[]{360,240,24,20,9,4},new[]{600,400,36,30,14,7},new[]{1000,680,50,44,20,10}};
            Assert.That(CoreTrainingTargets.All.Count,Is.EqualTo(5));
            for(int i=0;i<5;i++)
            {
                var t=CoreTrainingTargets.All[i];
                Assert.That(new[]{t.Hull,t.BoardingHealth,t.CannonDamage,t.MeleeDamage,t.Guard,t.Repair},Is.EqualTo(baseline[i]));
                var s=new CoreVoyageSession();Assert.That(s.StartTrainingBattle(t),Is.True);
                Assert.That(s.Battle.EnemyMaxHull,Is.EqualTo(t.Hull));
                Assert.That(s.Battle.EnemyMaxBoardingHealth,Is.EqualTo(t.BoardingHealth));
            }
        }

        static decimal Ratio(string value) { string[] parts=value.Split('/' ); return decimal.Parse(parts[0],CultureInfo.InvariantCulture) / (parts.Length==1 ? 1m : decimal.Parse(parts[1],CultureInfo.InvariantCulture)); }
        static CoreEncounter Encounter(string id)
        {
            if(id=="tutorial_replay")return new CoreEncounter(CoreGuidedTutorialContent.BossEventId,"教学","战斗","",true,false,0,0);
            var route=CoreRouteBossCatalog.Find(id);if(route!=null)return route.CreateEncounter();
            var legend=CoreLegendaryWarships.Find(id);if(legend!=null)return legend.CreateEncounter();
            Assert.That(id,Is.EqualTo(CoreBattleBalance.TreasureBossId));return CoreLegendaryWarships.CreateTreasureEncounter();
        }
        static object Call(object target,string name,params object[] args)=>target.GetType()
            .GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
    }
}
