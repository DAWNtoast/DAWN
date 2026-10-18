using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardFaceDescriptionTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EveryPublishedCard_KeepsItsWordsAcrossPortBattleSuppliesDamageAndAllowedStages(int rank)
        {
            CoreVoyageSession port = Port();
            CoreVoyageSession battle = Battle();
            int checkedCards = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                if (CoreVoyageSession.CardLibrary[index].AllowedStages == NavalBattleStages.None) continue;
                CoreBattleCard card = port.GetBattleCardAtLevel(index, rank);
                if (card == null || card.AllowedStages == NavalBattleStages.None) continue;
                string original = card.Description;
                foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                {
                    if (!card.IsAllowedInStage(stage)) continue;
                    // Healing uses medicine in boarding and wood for hull repair;
                    // within a stage, changing inventory only changes values.
                    string structure = null;
                    string fallbackStructure = null;
                    foreach (int medicine in new[] { 0, 2 })
                    {
                        port.Cargo.Medicine = medicine;
                        foreach (int crew in new[] { 7, 28 })
                        {
                            port.ConfigureDepartureCrew(crew, 0);
                            string quote = port.BattleCardFaceDescription(card, stage);
                            AssertStableQuote(quote, ref structure, card.Id + " rank " + rank + " port " + stage + " crew " + crew);
                            Assert.That(port.Cargo.Medicine, Is.EqualTo(medicine), "Port quotations do not spend supplies.");
                        }
                    }
                    // A targetless curse-guard preview exposes its per-mark formula;
                    // live combat replaces that formula with the target's actual total.
                    string battleStructure = card.SchoolEffect?.Mechanic == CoreSchoolMechanic.CurseWard ? null : structure;
                    for (int scenario = 0; scenario < 3; scenario++)
                    {
                        ConfigureState(battle, stage, scenario);
                        int medicine = battle.Cargo.Medicine, health = battle.Battle.PlayerBoardingHealth, hull = battle.Hull;
                        int mana = battle.Battle.Schools.Mana, marks = battle.Battle.Schools.CurseMarks;
                        string quote = battle.BattleCardFaceDescription(card, stage);
                        CoreBattleCard adjusted = battle.GetSupplyAdjustedBattleCard(index);
                        bool fallback = card.SchoolEffect != null && (adjusted.SchoolEffect == null ||
                            adjusted.SchoolEffect.Mechanic == CoreSchoolMechanic.None);
                        if (fallback)
                        {
                            Assert.That(quote, Does.Contain("不足，仅基础效果"), "Resource fallback has its own explicit template.");
                            AssertStableQuote(quote, ref fallbackStructure, card.Id + " rank " + rank + " battle fallback " + stage + " scenario " + scenario);
                        }
                        else AssertStableQuote(quote, ref battleStructure, card.Id + " rank " + rank + " battle " + stage + " scenario " + scenario);
                        Assert.That(battle.BattleCardFaceDescription(card, stage), Is.EqualTo(quote), "Repeated read is deterministic.");
                        Assert.That(battle.Cargo.Medicine, Is.EqualTo(medicine));
                        Assert.That(battle.Battle.PlayerBoardingHealth, Is.EqualTo(health));
                        Assert.That(battle.Hull, Is.EqualTo(hull));
                        Assert.That(battle.Battle.Schools.Mana, Is.EqualTo(mana));
                        Assert.That(battle.Battle.Schools.CurseMarks, Is.EqualTo(marks));
                    }
                }
                Assert.That(card.Description, Is.EqualTo(original), "Live quotation never mutates the authored definition.");
                checkedCards++;
            }
            Assert.That(checkedCards, Is.EqualTo(CoreVoyageSession.CardLibrary.Count(card => card.AllowedStages != NavalBattleStages.None)));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void CurseWard_PortShowsPerMarkCoefficientAndBattleShowsTotalIndependentOfCrew(int rank)
        {
            CoreVoyageSession port = Port();
            port.ConfigureDepartureCrew(7, 0);
            foreach (CoreBattleCard original in CoreVoyageSession.CardLibrary.Where(c => c.SchoolEffect?.Mechanic == CoreSchoolMechanic.CurseWard))
            {
                CoreBattleCard card = port.GetBattleCardAtLevel(Index(original.Id), rank);
                NavalBattleStage stage = card.IsAllowedInStage(NavalBattleStage.Boarding) ? NavalBattleStage.Boarding : NavalBattleStage.Naval;
                string preview = port.BattleCardFaceDescription(card, stage);
                Assert.That(preview, Does.Contain("每层获得 " + card.SchoolEffect.Power + " 基础格挡").And.Not.Contain("合计"), card.Id);
                CoreVoyageSession battle = Battle(stage);
                Set(battle.Battle, "PlayerOrdinaryCrew", 7);
                Set(battle.Battle.Schools, "CurseMarks", 3);
                string quote = battle.BattleCardFaceDescription(card, stage);
                int total = card.Block + card.SchoolEffect.Power * 3;
                Assert.That(quote, Does.Contain("合计获得 " + total + " 格挡").And.Not.Contain("每层获得"), card.Id);
                Assert.That(quote, Does.Not.Contain("格挡 +"), "The live total must not be repeated as a second additive block value: " + card.Id);
            }
        }

        [TestCase(0)] [TestCase(3)] [TestCase(11)]
        public void Cannon_PrimaryValueIncludesFirepowerAndLoadedDamageAndMatchesOneShot(int rawLoad)
        {
            CoreVoyageSession session = Battle();
            CoreBattleCard card = Card(session, "card_chain_shot");
            string before = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            session.Battle.PlayerLoadedShot = rawLoad;
            string quote = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            Assert.That(WithoutNumbers(quote), Is.EqualTo(WithoutNumbers(before)));
            int expected = session.GetBattleCardDamage(card, NavalBattleStage.Naval);
            Assert.That(DamageValue(quote), Is.EqualTo(expected));
            Assert.That(quote, Does.Not.Contain("当前炮击"), "The final damage occupies one primary slot.");
            int hp = session.Battle.EnemyHull, powder = session.Cargo.Powder, cannonballs = session.Cargo.Cannonballs;
            Put(session, card.Id);
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp - expected));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder - card.PowderCost));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(cannonballs - card.CannonballCost));
            Assert.That(session.PlayerLoadedShotDamageBonus, Is.Zero);
        }

        [TestCase(0, 5)] [TestCase(10, 2)] [TestCase(12, 0)]
        public void Load_PrimaryIncreaseUsesRemainingRoomAndKeepsTheFixedCap(int loaded, int increase)
        {
            CoreVoyageSession session = Battle();
            CoreBattleCard card = Card(session, "card_load_shot");
            string before = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            session.Battle.PlayerLoadedShot = loaded;
            string quote = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            Assert.That(WithoutNumbers(quote), Is.EqualTo(WithoutNumbers(before)));
            Assert.That(Regex.IsMatch(quote, @"下次炮击\s*\+\s*" + increase + @"(?!\d)"), Is.True, quote);
            Assert.That(Regex.IsMatch(quote, @"最多\s*\+\s*12(?!\d)"), Is.True, quote);
            Assert.That(session.GetBattleLoadedShotIncrease(card), Is.EqualTo(increase));
            Put(session, card.Id);
            int powder = session.Cargo.Powder;
            Assert.That(card.Draw, Is.EqualTo(1), "The preparation card still draws when its loading reserve is capped.");
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.PlayerLoadedShotDamageBonus, Is.EqualTo(loaded + increase));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder - card.PowderCost));
        }

        [TestCase(0)] [TestCase(6)]
        public void NavalGuard_MainBlockIncludesDeckBuffAndMatchesSettlement(int deckBuff)
        {
            CoreVoyageSession session = Battle();
            CoreBattleCard card = Card(session, "card_brace");
            string before = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            session.Battle.PlayerDeckBuff = deckBuff;
            string quote = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            Assert.That(WithoutNumbers(quote), Is.EqualTo(WithoutNumbers(before)));
            int block = session.GetBattleCardEffects(card, NavalBattleStage.Naval).Block + deckBuff;
            Assert.That(BlockValue(quote), Is.EqualTo(block));
            int previous = session.Battle.PlayerBlock;
            Put(session, card.Id);
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(previous + block));
        }

        [TestCase(7)] [TestCase(14)] [TestCase(28)] [TestCase(56)]
        public void BoardingDamageAndGuard_ShareFixedWordsAndTheRoundedCrewQuote(int ordinary)
        {
            CoreVoyageSession session = Battle(NavalBattleStage.Boarding);
            foreach (string id in new[] { "card_crew_thrust", "card_crew_guard" })
            {
                CoreBattleCard card = Card(session, id);
                Set(session.Battle, "PlayerOrdinaryCrew", 28);
                string fullCrew = session.BattleCardFaceDescription(card, NavalBattleStage.Boarding);
                Set(session.Battle, "PlayerOrdinaryCrew", ordinary);
                string quote = session.BattleCardFaceDescription(card, NavalBattleStage.Boarding);
                Assert.That(quote, Is.EqualTo(fullCrew), "Changing crew count must preserve the actual card-face values.");
                CoreBattleCardEffects effects = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
                if (card.Damage > 0) Assert.That(DamageValue(quote), Is.EqualTo(effects.Damage));
                if (card.Block > 0) Assert.That(BlockValue(quote), Is.EqualTo(effects.Block));
                int hp = session.Battle.EnemyBoardingHealth, block = session.Battle.PlayerBlock;
                Put(session, id);
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(hp - effects.Damage));
                Assert.That(session.Battle.PlayerBlock, Is.EqualTo(block + effects.Block));
            }
        }

        [TestCase(0, 0, 0, 0)] [TestCase(1, 0, 0, 0)]
        [TestCase(0, 40, 2, 0)] [TestCase(1, 40, 6, 1)]
        [TestCase(1, 1, 1, 0)] [TestCase(1, 2, 2, 0)] [TestCase(1, 5, 5, 1)] [TestCase(1, 7, 6, 1)]
        public void Healing_ChangesOnlyHealAndMedicineNumbersAndMatchesHealthAndInventory(int medicine, int wound, int heal, int cost)
        {
            CoreVoyageSession session = Battle(NavalBattleStage.Boarding);
            Set(session.Battle, "PlayerOrdinaryCrew", 14);
            CoreBattleCard card = Card(session, "card_crew_bandage");
            session.Cargo.Medicine = 0;
            string before = session.BattleCardFaceDescription(card, NavalBattleStage.Boarding);
            session.Cargo.Medicine = medicine;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth - wound;
            string quote = session.BattleCardFaceDescription(card, NavalBattleStage.Boarding);
            Assert.That(WithoutNumbers(quote), Is.EqualTo(WithoutNumbers(before)));
            Assert.That(RepairValue(quote), Is.EqualTo(card.Repair), "The card shows its full value and the explicit fallback; the live quote reports actual healing.");
            Assert.That(quote, Does.Contain("使用1药物").And.Contain("无药物恢复2"));
            Assert.That(quote, Is.EqualTo(card.Description), "Resource alternatives stay explicit on the card.");
            CoreBattleCardEffects effects = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
            Assert.That(effects.ActualRepair, Is.EqualTo(heal));
            Assert.That(effects.MedicineCost, Is.EqualTo(cost));
            int hp = session.Battle.PlayerBoardingHealth;
            Put(session, card.Id);
            Assert.That(session.PlayCard(0), Is.EqualTo(wound > 0), session.CardUnavailableReason(0));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(hp + heal));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine - cost));
            Assert.That(session.BattleCardMedicineDescription(card, NavalBattleStage.Boarding),
                Is.Empty, "The authored description already contains both supply cases.");
        }

        [Test]
        public void PortHealing_ShowsPotentialAndKeepsTheSameTemplateAsFullHealthBattle()
        {
            CoreVoyageSession port = Port();
            port.ConfigureDepartureCrew(14, 0);
            port.Cargo.Medicine = 1;
            CoreBattleCard card = Card(port, "card_crew_bandage");
            string potential = port.BattleCardFaceDescription(card, NavalBattleStage.Boarding);
            int baseline = card.Repair;
            int enhanced = baseline;
            Assert.That(RepairValue(potential), Is.EqualTo(enhanced));
            Assert.That(potential, Does.Contain("使用1药物"));
            Assert.That(port.Cargo.Medicine, Is.EqualTo(1), "Potential cost is quoted without spending it.");
            CoreVoyageSession battle = Battle(NavalBattleStage.Boarding);
            Set(battle.Battle, "PlayerOrdinaryCrew", 14);
            battle.Cargo.Medicine = 1;
            string full = battle.BattleCardFaceDescription(card, NavalBattleStage.Boarding);
            Assert.That(RepairValue(full), Is.EqualTo(enhanced));
            Assert.That(battle.GetBattleCardEffects(card, NavalBattleStage.Boarding).ActualRepair, Is.Zero);
            Assert.That(WithoutNumbers(full), Is.EqualTo(WithoutNumbers(potential)));
            Assert.That(port.BattleCardPortDescription(card), Is.EqualTo(card.Description));
        }

        [TestCase(CoreSchoolMechanic.FireExploit)] [TestCase(CoreSchoolMechanic.ArcaneRelease)]
        [TestCase(CoreSchoolMechanic.TideRiposte)] [TestCase(CoreSchoolMechanic.ThunderChain)]
        [TestCase(CoreSchoolMechanic.ThunderFinish)] [TestCase(CoreSchoolMechanic.BloodExecute)]
        public void SchoolConditionalDamage_UpdatesOnePrimarySlotAndMatchesResolvedDamage(CoreSchoolMechanic mechanic)
        {
            CoreVoyageSession session = Battle();
            CoreBattleCard card = CoreSchoolCardCatalog.All.First(value => value.SchoolEffect?.Mechanic == mechanic &&
                value.Damage > 0 && value.IsAllowedInStage(NavalBattleStage.Naval));
            string inactive = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            session.Battle.EnemyBurnDamage = 4;
            session.Battle.EnemyBurnTurns = 2;
            session.Battle.EnemyHull = 300;
            Set(session.Battle.Schools, "Mana", 12);
            Set(session.Battle.Schools, "CurseMarks", 4);
            Set(session.Battle.Schools, "ThunderChain", 4);
            Set(session.Battle.Schools, "LastGuardAbsorbed", 20);
            Set(session.Battle.Schools, "TidePower", 20);
            string active = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            Assert.That(WithoutNumbers(active), Is.EqualTo(WithoutNumbers(inactive)), card.Id);
            int damage = session.GetBattleCardDamage(card, NavalBattleStage.Naval);
            Assert.That(DamageValue(active), Is.EqualTo(damage), card.Id);
            Assert.That(DamageValue(active), Is.GreaterThan(DamageValue(inactive)), card.Id + " fixture must activate its bonus");
            int hp = session.Battle.EnemyHull;
            Put(session, card.Id);
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp - damage), card.Id);
        }

        [TestCase(CoreSchoolMechanic.ArcaneRelease)]
        [TestCase(CoreSchoolMechanic.ArcaneWard)]
        [TestCase(CoreSchoolMechanic.ArcaneMend)]
        [TestCase(CoreSchoolMechanic.CurseWard)]
        [TestCase(CoreSchoolMechanic.TideCleanse)]
        public void RedesignedSpecialCardQuotesFoldImmediateBonusesIntoOnePrimaryValue(CoreSchoolMechanic mechanic)
        {
            CoreVoyageSession session = Battle();
            CoreBattleCard card = CoreSchoolCardCatalog.All.First(value => value.RequiredComponent.Length > 0 &&
                value.SchoolEffect?.Mechanic == mechanic && value.IsAllowedInStage(NavalBattleStage.Naval));
            Set(session.Battle.Schools, "Mana", 3);
            Set(session.Battle.Schools, "CurseMarks", 4);
            Set(session.Battle.Schools, "TidePower", 10);
            Set(session, "Hull", Math.Max(1, session.MaxHull - 20));
            session.Cargo.Wood = 10;
            string quote = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            CoreBattleCardEffects effects = session.GetBattleCardEffects(card, NavalBattleStage.Naval);
            if (card.Damage > 0) Assert.That(DamageValue(quote), Is.EqualTo(session.GetBattleCardDamage(card, NavalBattleStage.Naval)));
            if (effects.Block > 0) Assert.That(BlockValue(quote), Is.EqualTo(effects.Block));
            if (effects.Repair > 0) Assert.That(RepairValue(quote), Is.EqualTo(effects.ActualRepair));
            StringAssert.DoesNotContain("额外伤害 +", quote);
            StringAssert.DoesNotContain("额外格挡 +", quote);
            StringAssert.DoesNotContain("额外恢复 ", quote);
            if (mechanic == CoreSchoolMechanic.TideCleanse || mechanic == CoreSchoolMechanic.CurseWard)
                Assert.That(Regex.Matches(quote, @"获得\s*\d+\s*格挡").Count, Is.EqualTo(1), quote);
        }

        private static int DamageValue(string quote) => Value(quote,
            @"(?:船体伤害|船员伤害|敌舰船体\s*-|敌方船员\s*-)\s*(?<value>\d+)|(?<value>\d+)\s*点(?:船体|船员)?伤害");
        private static int BlockValue(string quote) => Value(quote,
            @"格挡\s*\+?\s*(?<value>\d+)|(?<value>\d+)\s*点(?:船员)?格挡|获得\s*(?<value>\d+)\s*格挡");
        private static int RepairValue(string quote) => Value(quote,
            @"恢复\s*(?:当前阶段生命|船员)?\s*(?<value>\d+)");
        private static int Value(string quote, string expression)
        {
            Match match = Regex.Match(quote, expression);
            Assert.That(match.Success, Is.True, "Missing main value in: " + quote);
            return int.Parse(match.Groups["value"].Value);
        }

        private static void AssertStableQuote(string quote, ref string structure, string context)
        {
            Assert.That(quote, Is.Not.Empty, context);
            Assert.That(quote, Does.Not.Contain("{").And.Not.Contain("}"), context);
            Assert.That(quote, Does.Not.Contain("本次实际恢复").And.Not.Contain("当前炮击")
                .And.Not.Contain("当前装填加伤").And.Not.Contain("甲板额外格挡")
                .And.Not.Contain("无药基础").And.Not.Contain("满血不耗药"), context);
            string normalized = WithoutNumbers(quote);
            if (structure == null) structure = normalized;
            else Assert.That(normalized, Is.EqualTo(structure), context + " changes only numeric values, including hard breaks.");
        }

        private static string WithoutNumbers(string text) => Regex.Replace(text, @"\d+(?:\.\d+)?", "#");

        private static CoreVoyageSession Port()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.PortWarehouse.Restore("v2"), Is.True);
            Assert.That(session.SelectShip(7), Is.True);
            session.ConfigureDepartureCrew(28, 0);
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.ConfigureSpecialCardSources(CoreVoyageSession.CardLibrary.Select(card => card.RequiredCrew).Where(id => id.Length > 0).Distinct().ToArray(),
                CoreVoyageSession.CardLibrary.Select(card => card.RequiredComponent).Where(id => id.Length > 0).Distinct().ToArray());
            session.ConfigureCrewCapabilities(true, true, true, true, true, true);
            return session;
        }

        private static CoreVoyageSession Battle(NavalBattleStage stage = NavalBattleStage.Naval)
        {
            CoreVoyageSession session = Port();
            session.SetSeed("STABLE-CARD-FACE-QUOTES");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("stable_face", "卡面数值测试", "海战", "隔离数值测试", true, false, 1, 1000), false });
            session.Battle.Stage = stage;
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced, 1000, 0, 0, 0, 0, 28, 1, 1, healthMultiplier: 1);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 1000;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBlock = 0;
            session.Battle.DistanceMeters = 100;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Set(session.Battle, "PlayerOrdinaryCrew", 28);
            Set(session, "Morale", 0);
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            session.Cargo.Medicine = 0;
            session.Battle.Energy = 100;
            return session;
        }

        private static void ConfigureState(CoreVoyageSession session, NavalBattleStage stage, int scenario)
        {
            session.Battle.Stage = stage;
            session.Cargo.Medicine = scenario == 1 ? 2 : 0;
            session.Battle.PlayerBoardingHealth = scenario == 0 ? 1000 : 900;
            Set(session, "Hull", scenario == 0 ? session.MaxHull : Math.Max(1, session.MaxHull - 30));
            Set(session.Battle, "PlayerOrdinaryCrew", scenario == 1 ? 7 : 28);
            session.Battle.PlayerLoadedShot = scenario == 1 ? 11 : 0;
            session.Battle.PlayerDeckBuff = scenario == 1 ? 6 : 0;
            session.Battle.PlayerFireRounds = scenario == 1 ? 2 : 0;
            session.Battle.EnemyBurnDamage = scenario == 1 ? 4 : 0;
            session.Battle.EnemyBurnTurns = scenario == 1 ? 2 : 0;
            session.Battle.EnemyHull = scenario == 1 ? 200 : 1000;
            session.Battle.EnemyBoardingHealth = scenario == 1 ? 200 : 1000;
            Set(session.Battle.Schools, "Mana", scenario == 1 ? 12 : 0);
            Set(session.Battle.Schools, "CurseMarks", scenario == 1 ? 4 : 0);
            Set(session.Battle.Schools, "ThunderChain", scenario == 1 ? 4 : 0);
            Set(session.Battle.Schools, "LastGuardAbsorbed", scenario == 1 ? 20 : 0);
        }

        private static CoreBattleCard Card(CoreVoyageSession session, string id, int rank = 0) =>
            session.GetBattleCardAtLevel(Index(id), rank);
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static void Set(object target, string property, object value) =>
            target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void Put(CoreVoyageSession session, string id)
        {
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(Index(id));
            session.Battle.DrawPile.Clear();
            session.Battle.DiscardPile.Clear();
            session.Battle.Energy = 100;
        }
    }
}
