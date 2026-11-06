using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    // These are immutable, legally earned encounter checkpoints from the separate
    // continuous campaign. This verifies native rendering and callbacks, not a
    // claim that the whole campaign was played through UI or manually.
    public sealed class CampaignBossCheckpointUiTests
    {
        [Serializable] private sealed class Input
        {
            public CoreVoyageCheckpoint checkpoint;
            public int choice;
            public EarnedState state;
        }
        [Serializable] private sealed class EarnedState
        {
            public EarnedComponent[] components;
            public int[] ownedShips;
        }
        [Serializable] private sealed class EarnedComponent
        {
            public string InstanceId, ComponentId;
            public int ShipIndex;
        }
        [Serializable] private sealed class Inventory
        {
            public int version = 2;
            public int[] initializedShips;
            public Item[] items;
        }
        [Serializable] private sealed class Item
        {
            public string instanceId, componentId;
            public int shipIndex;
            public bool installedByPlayer = true;
        }
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;

        [UnityTest, Timeout(600000)]
        public IEnumerator EarnedFirstKillCheckpoints_RealEncounterCardAndTurnButtonsMatchCore()
        {
            Assert.That(Application.companyName, Is.EqualTo("ShatteredSailCampaignBossReview"));
            Assert.That(Application.productName, Is.EqualTo("CampaignBossReview20261027CheckpointUI"));
            string output = Argument("-auditActionLogPath");
            string inputDirectory = Path.Combine(Application.dataPath, "_Project/Tests/PlayMode/CampaignBossCheckpointInputs");
            string[] paths = Directory.GetFiles(inputDirectory, "*.json").OrderBy(x => x).ToArray();
            Assert.That(paths.Length, Is.EqualTo(13));
            Directory.CreateDirectory(output);
            var results = new List<string>();
            foreach (string path in paths)
            {
                string raw = File.ReadAllText(path);
                Input input = JsonUtility.FromJson<Input>(raw);
                Assert.That(input?.checkpoint, Is.Not.Null, path);
                Assert.That(input.state?.components, Is.Not.Null, path);
                CoreVoyageSession expected = Restore(input.checkpoint);
                CoreVoyageSession actual = Restore(input.checkpoint);
                string id = actual.ActiveEncounter.Id;
                Assert.That(actual.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
                Assert.That(actual.IsChoiceAvailable(input.choice), Is.True, id);
                Assert.That(actual.PirateLevel, Is.GreaterThanOrEqualTo(CoreVoyageSession.Routes[actual.SelectedRouteIndex].UnlockLevel), id);

                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
                yield return null; yield return null;
                CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game, Is.Not.Null);
                typeof(CoreGameplayDemoController).GetField("session", Private).SetValue(game, actual);
                typeof(CoreGameplayDemoController).GetField("tutorialActive", Private).SetValue(game, false);
                var inventory = new OwnedShipComponentInventory(new ComponentArtCatalog());
                var save = new Inventory { initializedShips = input.state.ownedShips,
                    items = input.state.components.Select(x => new Item { instanceId = x.InstanceId, componentId = x.ComponentId, shipIndex = x.ShipIndex }).ToArray() };
                Assert.That(inventory.Import(JsonUtility.ToJson(save), ship => input.state.ownedShips.Contains(ship)), Is.True);
                typeof(CoreGameplayDemoController).GetField("componentInventory", Private).SetValue(game, inventory);
                typeof(CoreGameplayDemoController).GetMethod("Render", Private).Invoke(game, null);
                yield return null; yield return null;
                AssertEqual(actual, expected, id + " earned encounter after render");
                NavalBattleVisualCapture.Capture(game, id + "-01-encounter.png", "合法连续新档首杀前检查点；实际遭遇按钮", "Encounter Choice " + input.choice);
                Assert.That(expected.ResolveChoice(input.choice), Is.True);
                Click("Encounter Choice " + input.choice);
                yield return Wait(game);
                AssertEqual(actual, expected, id + " encounter choice");
                Assert.That(actual.Battle.Profile, Is.SameAs(CoreBattleBalance.Resolve(actual.ActiveEncounter, actual.SelectedRouteIndex, true)));

                // Use only an actually drawn, legal simple card. Never replace the
                // hand, deck order, hull, resources or enemy state for this probe.
                int hand = actual.Battle.Hand.FindIndex(index =>
                    new[] { "card_brace", "card_emergency_repair", "card_energy_order" }.Contains(actual.GetBattleCard(index).Id) &&
                    actual.CanPlayCard(actual.Battle.Hand.IndexOf(index)));
                string played = "none-in-first-hand";
                if (hand >= 0)
                {
                    played = actual.GetBattleCard(actual.Battle.Hand[hand]).Id;
                    Assert.That(expected.PlayCard(hand), Is.True);
                    Click("Battle Card " + hand);
                    yield return Wait(game);
                    AssertEqual(actual, expected, id + " actual card callback " + played);
                }
                for (int turn = 0; turn < 2 && actual.Phase == CoreDemoPhase.Battle; turn++)
                {
                    Assert.That(expected.EndBattleTurn(), Is.True);
                    Click("End Battle Turn");
                    yield return Wait(game);
                    AssertEqual(actual, expected, id + " turn callback " + turn);
                }
                Assert.That(actual.Phase, Is.EqualTo(CoreDemoPhase.Battle), "This probe must not overwrite campaign victory evidence.");
                NavalBattleVisualCapture.Capture(game, id + "-02-battle.png", "实际出牌与回合按钮；资源、手牌、伤害和意图与同检查点Core一致", "End Battle Turn");
                string completeOutcome = "two-turn-ui-probe";
                if (id == "boss_route_golden_tide_trade" || id == "boss_route_moon_tide_ruins")
                {
                    var actionLog = new List<string>();
                    int actions = 0;
                    while (actual.Phase == CoreDemoPhase.Battle && actions++ < 420)
                    {
                        string action;
                        if (actual.CanRam(out _))
                        {
                            Assert.That(expected.TryRam(), Is.True);
                            Click("Battle Ram");
                            action = "Ram";
                        }
                        else if (actual.CanBoard(out _))
                        {
                            Assert.That(expected.TryBoard(), Is.True);
                            Click("Battle Board");
                            action = "Board";
                        }
                        else
                        {
                            int best = -1;
                            double score = .01;
                            for (int h = 0; h < actual.Battle.Hand.Count; h++)
                            {
                                if (!actual.CanPlayCard(h) || actual.GetNavalCardChoice(h)?.RequiresChoice == true || actual.GetBoardingPlayChoice(h)?.RequiresChoice == true) continue;
                                double candidate = Score(actual, actual.Battle.Hand[h]);
                                if (candidate > score) { score = candidate; best = h; }
                            }
                            if (best >= 0)
                            {
                                action = "Card " + actual.GetBattleCard(actual.Battle.Hand[best]).Id;
                                Assert.That(expected.PlayCard(best), Is.True);
                                Click("Battle Card " + best);
                            }
                            else
                            {
                                action = "EndTurn";
                                Assert.That(expected.EndBattleTurn(), Is.True);
                                Click("End Battle Turn");
                            }
                        }
                        yield return Wait(game);
                        AssertEqual(actual, expected, id + " complete action " + actions + " " + action);
                        actionLog.Add(actions + " " + action + " phase=" + actual.Phase + " hull=" + actual.Hull + " enemy=" + actual.Battle?.EnemyBoardingHealth);
                        File.WriteAllLines(Path.Combine(output, id + "-complete-actions.txt"), actionLog);
                    }
                    Assert.That(actual.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle), "Legal UI battle watchdog reached; do not force a win.");
                    completeOutcome = actual.Phase == CoreDemoPhase.Sailing ? "victory" : "defeat";
                    File.WriteAllText(Path.Combine(output, id + "-complete-outcome.txt"), completeOutcome + Environment.NewLine + State(actual));
                    NavalBattleVisualCapture.Capture(game, id + "-03-result.png", "真实按钮完整战斗结果：" + completeOutcome,
                        actual.Phase == CoreDemoPhase.Sailing ? "Navigation Chart" : "Settlement Card");
                }
                results.Add("PASS " + id + " level=" + actual.PirateLevel + " ship=" + actual.SelectedShip.Id +
                    " gold=" + actual.ProfileGold.ToString(CultureInfo.InvariantCulture) + " played=" + played + " outcome=" + completeOutcome + " input=" + Path.GetFileName(path));
                File.WriteAllLines(Path.Combine(output, "checkpoint-ui-results.txt"), results);
                File.WriteAllText(Path.Combine(output, id + "-observed.txt"), State(actual));
                LogAssert.NoUnexpectedReceived();
            }
            Assert.That(results.Count, Is.EqualTo(paths.Length));
        }

        private static CoreVoyageSession Restore(CoreVoyageCheckpoint checkpoint)
        {
            var s = new CoreVoyageSession();
            Assert.That(s.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            return s;
        }
        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), name);
            return args[index + 1];
        }
        private static void Click(string name)
        {
            Button button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(x => x.name == name && x.gameObject.activeInHierarchy);
            Assert.That(button, Is.Not.Null, "Missing actual UI control: " + name);
            Assert.That(button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }
        private static IEnumerator Wait(CoreGameplayDemoController game)
        {
            yield return null; yield return null;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (game.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                CoreGameplayDemoSceneTests.AdvanceRamComicIfWaiting(game);
                yield return null;
            }
            Assert.That(game.IsBattleAnimationPlaying, Is.False);
            yield return null;
        }
        private static void AssertEqual(CoreVoyageSession actual, CoreVoyageSession expected, string stage) =>
            Assert.That(State(actual), Is.EqualTo(State(expected)), stage);
        private static double Score(CoreVoyageSession s, int index)
        {
            var c = s.GetBattleCard(index); var b = s.Battle; var e = s.GetBattleCardEffects(c, b.Stage);
            bool boarding = b.Stage == NavalBattleStage.Boarding, preparing = b.Stage == NavalBattleStage.Preparation;
            if (c.BoardingSpec?.Key == "curse_10") return 1000;
            if (c.Action == NavalBattleCardAction.Energize || c.BoardingSpec?.Key == "basic_09") return 100;
            int enemy = boarding ? b.EnemyBoardingHealth : b.EnemyHull;
            int health = boarding ? b.PlayerBoardingHealth : s.Hull, max = boarding ? b.PlayerMaxBoardingHealth : s.MaxHull;
            int attack = CoreBattleIntentPresentation.Build(s).Where(x => x.IsAttack).Sum(x => x.Amount ?? 0);
            int repair = Math.Min(max - health, e.Repair);
            int block = Math.Min(Math.Max(0, (preparing ? 14 : attack) - b.PlayerBlock), e.Block + (c.Block > 0 && !boarding ? b.PlayerDeckBuff : 0));
            double score = repair * (health < max * .55 ? 2.5 : .65) + block * (health <= attack * 2 ? 2 : .9);
            if (boarding) score = repair * (health <= attack * 2 ? 3.5 : 1.2) + block * (health <= attack * 2 ? 3.5 : 2.1);
            int damage = e.Damage + (c.Action == NavalBattleCardAction.Cannon ? Math.Max(0, (s.EffectiveFirepower - 8) / 2) + b.PlayerLoadedShot : 0);
            if (damage >= enemy + b.EnemyBlock) return 10000 / (s.GetBattleCardEnergyCost(index) + .65);
            score += damage * (boarding ? 1.7 : !preparing ? .5 : 0);
            if (c.Draw > 0 && b.Hand.Count < 7) score += c.Draw * (b.Hand.Count < 4 ? 6 : 2);
            switch (c.Action)
            {
                case NavalBattleCardAction.Polish: if (b.PlayerDeckBuff < c.StrategyLimit) score += preparing ? 35 : 9; break;
                case NavalBattleCardAction.Approach: case NavalBattleCardAction.Circle: case NavalBattleCardAction.Turn:
                    if (b.DistanceMeters > 60) score += 40; else if (b.Alignment == NavalBattleAlignment.HeadOn && b.DistanceMeters <= 100 && c.Action != NavalBattleCardAction.Approach) score += 14; break;
                case NavalBattleCardAction.Navigator: case NavalBattleCardAction.Parallel:
                    if (b.DistanceMeters > 60) score += 44; else if (b.Alignment != NavalBattleAlignment.Parallel) score += 65; break;
                case NavalBattleCardAction.Retreat: return 0;
            }
            return score / (s.GetBattleCardEnergyCost(index) + .65);
        }
        private static string State(CoreVoyageSession s)
        {
            var rows = new List<string> { "phase=" + s.Phase, "profileGold=" + s.ProfileGold.ToString(CultureInfo.InvariantCulture),
                "hull=" + s.Hull, "maxHull=" + s.MaxHull, "level=" + s.PirateLevel, "xp=" + s.PirateExperience,
                "ship=" + s.SelectedShipIndex, "route=" + s.SelectedRouteIndex, "hours=" + s.TotalHours.ToString("R", CultureInfo.InvariantCulture) };
            Append(rows, "cargo", s.Cargo);
            if (s.Battle != null)
            {
                Append(rows, "battle", s.Battle);
                rows.Add("hand=" + string.Join(",", s.Battle.Hand));
                rows.Add("draw=" + string.Join(",", s.Battle.DrawPile));
                rows.Add("discard=" + string.Join(",", s.Battle.DiscardPile));
                rows.Add("intent=" + s.GetEnemyIntent());
            }
            return string.Join("\n", rows);
        }
        private static void Append(List<string> rows, string name, object value)
        {
            bool scalar(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);
            string format(object v) => v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "null";
            foreach (PropertyInfo p in value.GetType().GetProperties(Public).OrderBy(x => x.Name))
                if (p.GetIndexParameters().Length == 0 && scalar(p.PropertyType)) rows.Add(name + "." + p.Name + "=" + format(p.GetValue(value)));
            foreach (FieldInfo f in value.GetType().GetFields(Public).OrderBy(x => x.Name))
                if (scalar(f.FieldType)) rows.Add(name + "." + f.Name + "=" + format(f.GetValue(value)));
        }
    }
}
