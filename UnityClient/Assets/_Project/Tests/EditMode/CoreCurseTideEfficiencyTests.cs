using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCurseTideEfficiencyTests
    {
        private const string Nail = "card_school_curse_b03", Mark = "card_school_curse_b04";
        private const string Finish = "card_school_curse_n03", Field = "card_school_curse_n01";
        private const string Detonate = "card_school_curse_b05", Ward = "card_school_curse_b06";

        [Test]
        public void ThreeTurnTenCardDeckAuditUsesRealDrawDiscardCostsFieldsAndExecutions()
        {
            var lines = new List<string> { "health,guard,path,seed,round,enemy_health,marks,energy_left,player_health_lost,hand_count" };
            foreach (int health in new[] { 66, 107, 252 })
            foreach (int guard in new[] { 0, health == 66 ? 6 : 10 })
            foreach (string path in new[] { "marks", "cashout", "field_cashout", "mixed_damage", "ordinary", "tide" })
            {
                List<string> result = null;
                for (int seed = 0; seed < 100 && result == null; seed++) result = Run(health, guard, path, seed);
                Assert.That(result, Is.Not.Null, path + " must have a legal arrival order within this fixed ten-card deck.");
                lines.AddRange(result);
            }
            int nailPower = CoreVoyageSession.CardLibrary[Index(Nail)].SchoolEffect.Power;
            string output = "D:/Game/outputs/six-school-audit-20261024/curse-three-turn-power-" + nailPower + ".csv";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllLines(output, lines);
            TestContext.WriteLine(output);
        }

        [Test]
        public void MarkAndOrdinaryDamageCanExecuteWithoutClearingMarks()
        {
            var s = Start(66, 6, 0);
            CoreBattleState battle = s.Battle;
            s.Battle.EnemyHull = 13;
            s.ApplyNavalEnemyCurseMarks(9);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(13));
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index("card_chain_shot"));
            s.Battle.EnemyBlock = 6;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(battle.EnemyHull, Is.Zero, "After ordinary damage lowers remaining HP to 9, nine marks meet its 100% threshold and execute.");
            Assert.That(battle.Schools.CurseMarks, Is.EqualTo(9), "Execution does not require a clear-all finisher.");
        }

        private static List<string> Run(int health, int guard, string path, int seed)
        {
            var s = Start(health, guard, seed);
            CoreBattleState battle = s.Battle;
            string[] first = path == "field_cashout" ? new[] { Field, Nail, Finish, Detonate, Ward }
                : path == "mixed_damage" ? new[] { Mark, "card_chain_shot", Finish, Detonate, Ward }
                : new[] { Nail, Mark, Finish, Detonate, Ward };
            string[] second = path == "field_cashout" ? new[] { Nail, Mark, Finish, Detonate, Ward }
                : path == "mixed_damage" ? new[] { Nail, Mark, "card_chain_shot", Detonate, Ward }
                : new[] { Nail, Mark, Finish, Detonate, Ward };
            if (path == "ordinary") first = second = new[] { "card_chain_shot", "card_cannon_barrage", "card_brace", "card_evasive_maneuver", "card_retreat" };
            if (path == "tide") first = second = new[] { "card_school_tide_b04", "card_cannon_barrage", "card_school_tide_n02", "card_brace", "card_evasive_maneuver" };
            Assert.That(first.Concat(second).GroupBy(id => id).All(g => g.Count() <= 2), Is.True);
            s.Battle.Hand.Clear(); s.Battle.Hand.AddRange(first.Select(Index));
            s.Battle.DrawPile.Clear(); s.Battle.DrawPile.AddRange(second.Reverse().Select(Index));
            s.Battle.DiscardPile.Clear();
            int startHull = s.Hull;
            var rows = new List<string>();
            for (int round = 1; round <= 3; round++)
            {
                Assert.That(s.Battle.Hand.Count, Is.EqualTo(5), "Initial draw and every new turn draw exactly five.");
                Assert.That(s.Battle.Energy, Is.EqualTo(3));
                string[] plays = round == 1 ? (path == "field_cashout" ? new[] { Field, Nail }
                    : path == "mixed_damage" ? new[] { Mark, "card_chain_shot" } : new[] { Nail, Mark })
                    : round == 2 ? (path == "mixed_damage" ? new[] { Nail, "card_chain_shot", Detonate } : new[] { Nail, Mark })
                    : path == "marks" ? Array.Empty<string>() : path == "mixed_damage" ? new[] { Mark, Detonate } : new[] { Finish };
                if (path == "ordinary") plays = new[] { "card_chain_shot", "card_cannon_barrage" };
                if (path == "tide") plays = new[] { round == 2 ? "card_school_tide_n02" : "card_school_tide_b04", "card_cannon_barrage" };
                foreach (string id in plays)
                {
                    int slot = s.Battle.Hand.FindIndex(i => CoreVoyageSession.CardLibrary[i].Id == id);
                    if (slot < 0) return null;
                    Assert.That(s.GetBattleCardEnergyCost(s.Battle.Hand[slot]), Is.LessThanOrEqualTo(s.Battle.Energy));
                    bool played = id == Field ? s.PlayNavalCard(slot, new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 1, AnchorY = 0 })
                        : s.PlayCard(slot);
                    Assert.That(played, Is.True, path + " " + id);
                    Assert.That(battle.Hand.Count, Is.LessThanOrEqualTo(8));
                    if (battle.EnemyHull <= 0) break;
                }
                int energyLeft = battle.Energy;
                if (battle.EnemyHull > 0) Assert.That(s.EndBattleTurn(), Is.True);
                rows.Add(string.Join(",", health, guard, path, seed, round, battle.EnemyHull,
                    battle.Schools.CurseMarks, energyLeft, startHull - s.Hull, battle.Hand.Count));
                if (battle.EnemyHull <= 0) break;
            }
            return rows;
        }

        private static CoreVoyageSession Start(int health, int guard, int seed)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("CURSE-EFFICIENCY-" + seed);
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                health, health == 66 ? 9 : health == 107 ? 15 : 19, 6, guard, 0, 8, 1f, 1f, healthMultiplier: 1m);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = health;
            s.Battle.EnemyBlock = 0; s.Battle.PlayerBlock = 0;
            s.Battle.EnemyLoadedShot = 0; s.Battle.PlayerLoadedShot = 0;
            s.Battle.MaxEnergy = s.Battle.Energy = 3;
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { true, new VoyagePoint(0, 0), 0f });
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { false, new VoyagePoint(100, 0), 180f });
            s.Cargo.Powder = s.Cargo.Cannonballs = 20;
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, s.MaxHull);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            return s;
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
    }
}
