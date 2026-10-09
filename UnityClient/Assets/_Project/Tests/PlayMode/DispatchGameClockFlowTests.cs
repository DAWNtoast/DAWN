using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator Dispatch_RealClockStaysIndependentOfActualSailingAndReload()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            yield return LoadDispatchCalendarScene();
            var controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ActiveButton("Port Hotspot Dispatch").onClick.Invoke();
            yield return null;
            ActiveButton("Dispatch Mission Chart 0").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Dispatch Duration Value").GetComponent<Text>().text, Does.Contain("现实小时"));
            Assert.That(GameObject.Find("Dispatch Deployment Mission Rule").GetComponent<Text>().text,
                Does.Contain("离线继续计时").And.Contain("不推进游戏日历"));
            double beforeDispatch = controller.Session.ElapsedGameHours;
            long beforeStart = DateTime.UtcNow.Ticks;
            ActiveButton("Confirm Dispatch Deployment").onClick.Invoke();
            long afterStart = DateTime.UtcNow.Ticks;
            yield return null;
            long deadline = ReadDispatchDeadline();
            long duration = GameplayBalance.DispatchHours(0) * TimeSpan.TicksPerHour;
            Assert.That(deadline, Is.InRange(beforeStart + duration, afterStart + duration));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.DispatchRealClockKey, 0), Is.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchGameClockKey), Is.False);
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(beforeDispatch));
            Assert.That(GameObject.Find("Active Dispatch Badge").GetComponent<Text>().text, Does.Contain("现实时间"));
            Assert.That(ActiveButton("Recall Dispatch Job"), Is.Not.Null);
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(beforeDispatch));
            Assert.That(ReadDispatchDeadline(), Is.EqualTo(deadline));

            // Only the map fixture is selected directly. Travel, event time and
            // settlement use production Core APIs, and cannot finish a UTC job.
            Assert.That(controller.Session.ConfigureGuidedTutorialVoyage(1), Is.True);
            controller.Session.StartVoyage();
            controller.Session.SetHeading(SailingDirection.East);
            controller.Session.SetDistance(CoreGuidedTutorialContent.PlannedDistanceNm);
            for (int attempt = 0; attempt < 3 && controller.Session.Phase == CoreDemoPhase.Sailing; attempt++)
                Assert.That(controller.Session.Sail(), Is.True);
            Assert.That(controller.Session.ActiveEncounter?.Id, Is.EqualTo(CoreGuidedTutorialContent.FirstEventId));
            Assert.That(controller.Session.ResolveChoice(0), Is.True);
            Assert.That(controller.Session.SafeReturn(), Is.True);
            controller.Session.ReturnToPortAfterSettlement();
            Assert.That(controller.Session.ElapsedGameHours - beforeDispatch, Is.GreaterThanOrEqualTo(GameplayBalance.DispatchHours(0)));
            InvokeDispatchCalendarController(controller, "SaveProgress");
            double afterSailing = controller.Session.ElapsedGameHours;
            yield return LoadDispatchCalendarScene();
            controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(afterSailing));
            Assert.That(ReadDispatchDeadline(), Is.EqualTo(deadline));
            ActiveButton("Port Hotspot Dispatch").onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Recall Dispatch Job"), Is.Not.Null);
            Assert.That(GameObject.Find("Claim Dispatch Reward"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Dispatch_RealDeadlineRefreshesAtPortAndPaysOnceWithoutAdvancingCalendar()
        {
            PrepareDispatchClockRecord(DateTime.UtcNow.AddHours(1d).Ticks, true, false);
            PlayerPrefs.SetString(CoreGameplayDemoController.GameClockKey, "200");
            yield return LoadDispatchCalendarScene();
            var controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ActiveButton("Port Hotspot Dispatch").onClick.Invoke();
            yield return null;
            // Compress only this test's UTC deadline; no production duration or
            // game-clock mutation is used to make a job complete.
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchEndsAtKey,
                DateTime.UtcNow.AddSeconds(0.5d).Ticks.ToString(CultureInfo.InvariantCulture));
            InvokeDispatchCalendarController(controller, "Render");
            Assert.That(ActiveButton("Recall Dispatch Job"), Is.Not.Null);
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(200d));
            Assert.That(GameObject.Find("Active Dispatch Badge").GetComponent<Text>().text, Does.Contain("任务完成"));
            decimal before = controller.Session.ProfileGold;
            Button.ButtonClickedEvent claim = ActiveButton("Claim Dispatch Reward").onClick;
            claim.Invoke();
            claim.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(before + GameplayBalance.DispatchGold(0)));
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(200d));
            AssertDispatchClockRecordCleared();
            yield return LoadDispatchCalendarScene();
            controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(200d));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(before + GameplayBalance.DispatchGold(0)));
            AssertDispatchClockRecordCleared();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Dispatch_OriginalUtcJobsKeepTheirExactDeadlineAndOfflineCompletion()
        {
            foreach (bool complete in new[] { false, true })
            {
                long originalDeadline = DateTime.UtcNow.AddHours(complete ? -1d : 1d).Ticks;
                PrepareDispatchClockRecord(originalDeadline, false, false);
                PlayerPrefs.SetString(CoreGameplayDemoController.GameClockKey, "500");
                yield return LoadDispatchCalendarScene();
                var controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(ReadDispatchDeadline(), Is.EqualTo(originalDeadline));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.DispatchRealClockKey, 0), Is.EqualTo(1));
                Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchGameClockKey), Is.False);
                Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(500d));
                yield return LoadDispatchCalendarScene();
                controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(ReadDispatchDeadline(), Is.EqualTo(originalDeadline));
                ActiveButton("Port Hotspot Dispatch").onClick.Invoke();
                yield return null;
                if (!complete)
                {
                    Assert.That(ActiveButton("Recall Dispatch Job"), Is.Not.Null);
                    continue;
                }
                decimal before = controller.Session.ProfileGold;
                Button.ButtonClickedEvent claim = ActiveButton("Claim Dispatch Reward").onClick;
                claim.Invoke();
                claim.Invoke();
                yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(before + GameplayBalance.DispatchGold(0)));
                Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(500d));
                AssertDispatchClockRecordCleared();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Dispatch_GameClockJobsConvertRemainingTimeOnceAndPreserveCompletion()
        {
            foreach (int remainingHours in new[] { 1, 720, -1 })
            {
                const double savedGameHours = 200d;
                long oldDeadline = CoreVoyageSession.GameClockOrigin.AddHours(savedGameHours + remainingHours).Ticks;
                PrepareDispatchClockRecord(oldDeadline, false, true);
                PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchDurationHoursKey, 4);
                PlayerPrefs.SetString(CoreGameplayDemoController.GameClockKey, "200");
                long beforeLoad = DateTime.UtcNow.Ticks;
                yield return LoadDispatchCalendarScene();
                long afterLoad = DateTime.UtcNow.Ticks;
                var controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
                long migratedDeadline = ReadDispatchDeadline();
                long remaining = Math.Max(0, Math.Min(4, remainingHours)) * TimeSpan.TicksPerHour;
                Assert.That(migratedDeadline, Is.InRange(beforeLoad + remaining, afterLoad + remaining));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.DispatchRealClockKey, 0), Is.EqualTo(1));
                Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchGameClockKey), Is.False);
                Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(savedGameHours));
                yield return new WaitForSecondsRealtime(1.1f);
                yield return LoadDispatchCalendarScene();
                controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(ReadDispatchDeadline(), Is.EqualTo(migratedDeadline), "Reload must not restart the remaining real time.");
                Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(savedGameHours));
                ActiveButton("Port Hotspot Dispatch").onClick.Invoke();
                yield return null;
                if (remainingHours > 0)
                {
                    Assert.That(ActiveButton("Recall Dispatch Job"), Is.Not.Null);
                    continue;
                }
                decimal before = controller.Session.ProfileGold;
                int reward = GameplayBalance.ScaleReward(GameplayBalance.DispatchGold(0),
                    GameplayBalance.DispatchPercent(0, 4, null));
                Button.ButtonClickedEvent claim = ActiveButton("Claim Dispatch Reward").onClick;
                claim.Invoke();
                claim.Invoke();
                yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(before + reward), "The prior four-hour job keeps its quoted duration bonus.");
                AssertDispatchClockRecordCleared();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Dispatch_RealClockMarkerPreventsStaleGameMarkerFromConvertingTwice()
        {
            long deadline = DateTime.UtcNow.AddHours(1d).Ticks;
            PrepareDispatchClockRecord(deadline, true, true);
            PlayerPrefs.SetString(CoreGameplayDemoController.GameClockKey, "200");
            yield return LoadDispatchCalendarScene();
            var controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(ReadDispatchDeadline(), Is.EqualTo(deadline));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.DispatchRealClockKey, 0), Is.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchGameClockKey), Is.False);
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(200d));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Dispatch_MalformedClockRecordsStayInvalidUntilRecoveryWithoutPayment()
        {
            foreach (bool gameClock in new[] { false, true })
            {
                PrepareDispatchClockRecord(CoreVoyageSession.GameClockOrigin.Ticks, false, gameClock);
                PlayerPrefs.SetString(CoreGameplayDemoController.DispatchEndsAtKey, "invalid-clock");
                PlayerPrefs.SetString(CoreGameplayDemoController.GameClockKey, "200");
                yield return LoadDispatchCalendarScene();
                var controller = UnityEngine.Object.FindFirstObjectByType<CoreGameplayDemoController>();
                decimal before = controller.Session.ProfileGold;
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.DispatchEndsAtKey), Is.EqualTo("invalid-clock"));
                ActiveButton("Port Hotspot Dispatch").onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Active Dispatch Badge").GetComponent<Text>().text, Does.Contain("异常"));
                Assert.That(ActiveButton("Recall Dispatch Job").GetComponentInChildren<Text>().text, Does.Contain("清除异常"));
                ActiveButton("Recall Dispatch Job").onClick.Invoke();
                yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(before));
                Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(200d));
                AssertDispatchClockRecordCleared();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Dispatch_OrphanClockMarkersAreClearedWhenNoJobExists()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchMissionKey);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchGameClockKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchRealClockKey, 1);
            yield return LoadDispatchCalendarScene();
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchGameClockKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchRealClockKey), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private static void PrepareDispatchClockRecord(long deadline, bool realClock, bool gameClock)
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchMissionKey, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchDurationHoursKey, GameplayBalance.DispatchHours(0));
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchBalanceVersionKey, GameplayBalance.Version);
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchEndsAtKey, deadline.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchCrewKey, string.Empty);
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchCrewIdentityKey, string.Empty);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchRealClockKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchGameClockKey);
            if (realClock) PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchRealClockKey, 1);
            if (gameClock) PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchGameClockKey, 1);
        }

        private static long ReadDispatchDeadline() => long.Parse(
            PlayerPrefs.GetString(CoreGameplayDemoController.DispatchEndsAtKey), CultureInfo.InvariantCulture);

        private static void AssertDispatchClockRecordCleared()
        {
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchMissionKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchEndsAtKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchGameClockKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchRealClockKey), Is.False);
        }

        private static IEnumerator LoadDispatchCalendarScene()
        {
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static void InvokeDispatchCalendarController(CoreGameplayDemoController controller, string method) =>
            typeof(CoreGameplayDemoController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }
}
