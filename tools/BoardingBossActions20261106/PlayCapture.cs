using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    // Compiled only in the borrowed Unity project's PlayMode test assembly.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator BoardingBossFourActions_RealButtonsRenderAuthoredEncounterFrames()
        {
            string[] args = Environment.GetCommandLineArgs();
            int next = Array.IndexOf(args, "-boardingLiveOutput") + 1;
            if (next == 0) Assert.Ignore("Run with the boss action capture tool.");
            string output = Path.GetFullPath(args[next]);
            int caseIndex = Array.IndexOf(args, "-boardingLiveCase") + 1;
            string selectedCase = caseIndex > 0 ? args[caseIndex] : null;
            if (!output.Replace('\\','/').StartsWith("D:/Game/outputs/boarding-boss-actions-20261106/validation/", StringComparison.OrdinalIgnoreCase) || Directory.Exists(output))
                throw new IOException("Use a fresh boss validation directory.");
            Directory.CreateDirectory(output);
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var report = new LiveReport();
                string[][] cases = {
                    new[] {"iron_hit", "铁誓 · 受击", "legendary_pirate_iron_oath", "enemy_iron_oath", "Hit"},
                    new[] {"treasure_guard", "宝藏守卫 · 格挡", CoreBattleBalance.TreasureBossId, "enemy_treasure", "Guard"},
                    new[] {"widow_attack", "绯刃寡妇 · 攻击", "legendary_pirate_red_widow", "enemy_red_widow", "Attack"},
                    new[] {"moon_cast", "月织 · 施法", "boss_route_moon_tide_ruins_arcane", "enemy_moon_weave", "Cast"}
                };
                foreach (string[] item in cases)
                {
                    if (selectedCase != null && selectedCase != item[0]) continue;
                    CoreVoyageSession session = PrepareActorCrewBattle(controller, "basic_01", out _);
                    var legend = CoreLegendaryWarships.Find(item[2]);
                    var route = CoreRouteBossCatalog.Find(item[2]);
                    CoreEncounter encounter = legend != null ? legend.CreateEncounter() : route != null
                        ? route.CreateEncounter() : CoreLegendaryWarships.CreateTreasureEncounter();
                    typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
                    if (legend != null) session.Battle.Profile = legend.Profile;
                    else if (route != null) session.Battle.Profile = route.Profile;
                    typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(session.Battle.BoardingEnemy, CoreBoardingEnemyCatalog.ForEncounter(item[2]));
                    session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 500;
                    session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 500;
                    session.Battle.EnemyBlock = item[4] == "Guard" ? 100 : 0;
                    session.Battle.PhaseTurn = item[4] == "Cast" ? 2 : 1;
                    SetResponsiveHand(session, "card_crew_thrust", "card_crew_guard");
                    RenderResponsiveBattle(controller);
                    yield return WaitResponsiveBattle(controller);
                    for (int page = 0; page < 20; page++)
                    {
                        GameObject advance = GameObject.Find("Legendary Fleet Comic Advance") ?? GameObject.Find("Treasure Story Advance");
                        if (advance == null) break;
                        advance.GetComponent<Button>().onClick.Invoke();
                        yield return null;
                    }
                    yield return WaitResponsiveBattle(controller);
                    var enemy = RequiredResponsive<BoardingActorGraphic>("Enemy Ship Art Party Illustration");
                    Assert.That(enemy.Identity, Is.EqualTo(item[3]));
                    Texture2D expected = Resources.Load<Texture2D>("BoardingBattle/BossActions/" + item[3]);
                    Assert.That(expected, Is.Not.Null);
                    bool endTurn = item[4]=="Attack" || item[4]=="Cast";
                    var clip = new LiveClip { id=item[0],title=item[1],input=endTurn?"End Battle Turn":"Battle Card 0",
                        sourceCard=endTurn?session.Battle.BoardingEnemy.Deck.TurnAt(session.Battle.PhaseTurn).Name:"card_crew_thrust" };
                    Canvas canvas = RequiredResponsive<RectTransform>("Battle Card").GetComponentInParent<Canvas>().rootCanvas;
                    bool witnessed = false;
                    using (var recorder = new LiveSceneRecorder(canvas, output, clip))
                    {
                        recorder.Sample();
                        var button = RequiredResponsive<Button>(clip.input);
                        Assert.That(button.interactable, Is.True, item[0]);
                        button.onClick.Invoke();
                        Assert.That(controller.IsBattleAnimationPlaying, Is.True, item[0]+" did not accept the real input");
                        float deadline = Time.realtimeSinceStartup + 30f;
                        float last = Time.realtimeSinceStartup;
                        do
                        {
                            if (enemy != null && enemy.Action != BoardingActorAction.Idle)
                            {
                                string state = enemy.Identity + "/" + enemy.Action;
                                if (!clip.observedActions.Contains(state)) clip.observedActions.Add(state);
                                if (enemy.Action.ToString()==item[4] && enemy.ActionProgress >= .15f && enemy.ActionProgress <= .88f)
                                {
                                    Assert.That(enemy.UsesBossActionAtlas, Is.True, state);
                                    Assert.That(enemy.GetComponent<RawImage>().texture, Is.SameAs(expected), state);
                                    if (!witnessed)
                                    {
                                        recorder.Sample();
                                        witnessed = true;
                                    }
                                }
                            }
                            if (Time.realtimeSinceStartup-last >= .055f && clip.frames.Count<180)
                            {
                                recorder.Sample();
                                last=Time.realtimeSinceStartup;
                            }
                            if (!controller.IsBattleAnimationPlaying) break;
                            yield return null;
                        } while (Time.realtimeSinceStartup<deadline);
                        Assert.That(controller.IsBattleAnimationPlaying, Is.False, item[0]);
                        Assert.That(witnessed, Is.True, item[0]+"; observed "+string.Join(",",clip.observedActions));
                        recorder.Sample();
                    }
                    report.clips.Add(clip);
                    File.WriteAllText(Path.Combine(output,"live-report.json"),JsonUtility.ToJson(report,true));
                }
                Assert.That(report.clips.Count, Is.EqualTo(selectedCase == null ? 4 : 1));
            }
        }
    }
}
