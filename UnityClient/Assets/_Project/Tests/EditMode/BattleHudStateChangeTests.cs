using System;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleHudStateChangeTests
    {
        private GameObject host;
        private bool suppressed;
        [TearDown] public void TearDown() { if (host != null) Object.DestroyImmediate(host); }

        [Test]
        public void StatusBaselineIsSilent_ValuesStayAuthoritative_AndDuplicateRefreshDoesNotReplay()
        {
            BattleStatusRow row = Row();
            row.SetStatuses(new[] { Status("guard", "12") }, true);
            var slot = row.transform.Find("Status guard");
            var motion = slot.GetComponent<BattleHudChangeMotion>();
            Assert.That(motion.PlayCount, Is.Zero);
            row.SetStatuses(new[] { Status("guard", "8") }, true);
            Assert.That(slot.Find("Amount").GetComponent<Text>().text, Is.EqualTo("8"));
            Assert.That(motion.LastText, Is.EqualTo("−4"));
            Assert.That(motion.LastKind, Is.EqualTo(BattleHudChangeKind.Decreased));
            Assert.That(motion.PlayCount, Is.EqualTo(1));
            row.SetStatuses(new[] { Status("guard", "8") }, true);
            Assert.That(motion.PlayCount, Is.EqualTo(1));
            foreach (Graphic graphic in slot.Find("Battle HUD Change").GetComponentsInChildren<Graphic>())
                Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(slot.GetComponent<BattleStatusHover>().enabled, Is.True);
            Assert.That(slot.Find("Battle HUD Change").GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        [Test]
        public void AddedAndRemovedStatesHaveFiniteFeedback_AndRemovedStatesLosePointerAccessImmediately()
        {
            BattleStatusRow row = Row();
            row.SetStatuses(Array.Empty<CoreBattleStatus>(), true);
            row.SetStatuses(new[] { Status("burn", "3", true) }, true);
            Transform slot = row.transform.Find("Status burn");
            var motion = slot.GetComponent<BattleHudChangeMotion>();
            Assert.That(motion.LastKind, Is.EqualTo(BattleHudChangeKind.Added));
            Assert.That(row.VisibleStatusCount, Is.EqualTo(1));
            row.SetStatuses(Array.Empty<CoreBattleStatus>(), true);
            Assert.That(row.VisibleStatusCount, Is.Zero);
            Assert.That(row.transform.Find("Status burn"), Is.Null);
            Assert.That(row.RetiringStatusCount, Is.EqualTo(1));
            Assert.That(slot.GetComponent<BattleStatusHover>().enabled, Is.False);
            Assert.That(slot.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(motion.LastKind, Is.EqualTo(BattleHudChangeKind.Removed));
            motion.SetElapsed(BattleHudChangeMotion.RemovalDuration);
            Invoke(row, "LateUpdate");
            Assert.That(row.RetiringStatusCount, Is.Zero);
            Assert.That(row.transform.Find("Retiring Status burn"), Is.Null);
        }

        [Test]
        public void CompoundAndTooltipChangesAreNotMisreportedAsNumericStacks()
        {
            BattleStatusRow row = Row();
            row.SetStatuses(new[] { Status("burn", "3×2", true, "old duration") }, true);
            row.SetStatuses(new[] { Status("burn", "4×1", true, "new duration") }, true);
            var motion = row.transform.Find("Status burn").GetComponent<BattleHudChangeMotion>();
            Assert.That(motion.LastText, Is.EqualTo("更新"));
            Assert.That(motion.LastKind, Is.EqualTo(BattleHudChangeKind.Changed));
            row.SetStatuses(new[] { Status("burn", "4×1", true, "a batch expires sooner") }, true);
            Assert.That(motion.PlayCount, Is.EqualTo(2));
            Assert.That(row.transform.Find("Status burn/Amount").GetComponent<Text>().text, Is.EqualTo("4×1"));
        }

        [Test]
        public void SuppressedMovementDefersNetChangesUntilVisible_WithoutReplayingFinalRefresh()
        {
            BattleStatusRow row = Row();
            row.SetStatuses(new[] { Status("guard", "12"), Status("burn", "3", true) }, true);
            suppressed = true; row.RefreshVisibility();
            row.SetStatuses(new[] { Status("guard", "8"), Status("anchor", "1") }, true);
            row.SetStatuses(new[] { Status("guard", "6"), Status("anchor", "1") }, true);
            var guard = row.transform.Find("Status guard").GetComponent<BattleHudChangeMotion>();
            var anchor = row.transform.Find("Status anchor").GetComponent<BattleHudChangeMotion>();
            var retired = row.transform.Find("Retiring Status burn").GetComponent<BattleHudChangeMotion>();
            Assert.That(row.transform.Find("Status guard/Amount").GetComponent<Text>().text, Is.EqualTo("6"));
            Assert.That(guard.PlayCount, Is.Zero); Assert.That(anchor.PlayCount, Is.Zero);
            Invoke(row, "LateUpdate");
            Assert.That(row.RetiringStatusCount, Is.EqualTo(1), "A pending disappearance must survive the movement animation.");
            suppressed = false; row.RefreshVisibility();
            Assert.That(guard.LastText, Is.EqualTo("−6"));
            Assert.That(anchor.LastKind, Is.EqualTo(BattleHudChangeKind.Added));
            Assert.That(retired.LastKind, Is.EqualTo(BattleHudChangeKind.Removed));
            row.SetStatuses(new[] { Status("guard", "6"), Status("anchor", "1") }, true);
            Assert.That(guard.PlayCount, Is.EqualTo(1)); Assert.That(anchor.PlayCount, Is.EqualTo(1));
            Assert.That(retired.PlayCount, Is.EqualTo(1));
        }

        [Test]
        public void SuppressedTransientStatesAndDisabledRowsDoNotCreatePhantomGains()
        {
            BattleStatusRow row = Row();
            row.SetStatuses(Array.Empty<CoreBattleStatus>(), true);
            suppressed = true; row.RefreshVisibility();
            row.SetStatuses(new[] { Status("anchor", "1") }, true);
            row.SetStatuses(Array.Empty<CoreBattleStatus>(), true);
            suppressed = false; row.RefreshVisibility();
            Assert.That(row.VisibleStatusCount, Is.Zero);
            Assert.That(row.RetiringStatusCount, Is.Zero);
            suppressed = true;
            row.SetStatuses(new[] { Status("guard", "7") }, true);
            Invoke(row, "OnDisable");
            suppressed = false;
            row.SetStatuses(new[] { Status("guard", "7") }, true);
            Assert.That(row.transform.Find("Status guard").GetComponent<BattleHudChangeMotion>().PlayCount, Is.Zero);
        }

        [Test]
        public void OwnerDisableClearsDeferredRetirements_AndResumeBuildsASilentBaseline()
        {
            BattleStatusRow row = Row();
            Canvas owner = host.AddComponent<Canvas>();
            row.SetOwner(owner);
            row.SetStatuses(new[] { Status("burn", "3", true) }, true);
            suppressed = true;
            row.SetStatuses(new[] { Status("guard", "7") }, true);
            Assert.That(row.RetiringStatusCount, Is.EqualTo(1));
            owner.enabled = false;
            row.RefreshVisibility();
            Assert.That(row.RetiringStatusCount, Is.Zero);
            Assert.That(row.IsSuppressed, Is.True);
            owner.enabled = true; suppressed = false;
            row.RefreshVisibility();
            row.SetStatuses(new[] { Status("guard", "7") }, true);
            var motion = row.transform.Find("Status guard").GetComponent<BattleHudChangeMotion>();
            Assert.That(motion.PlayCount, Is.Zero);
            Assert.That(motion.IsPlaying, Is.False);
        }

        [Test]
        public void ResourcesTrackMainAndTemporaryPoolsIndependently_AndCapOnlyChangesAreNotGains()
        {
            BattleResourceChangeTracker tracker = Resource();
            tracker.Observe(3, 2, 6, null, true, true);
            Assert.That(tracker.Motion.PlayCount, Is.Zero);
            tracker.Observe(4, 1, 6, null, true, true);
            Assert.That(tracker.LastDelta, Is.EqualTo(1));
            Assert.That(tracker.LastTemporaryDelta, Is.EqualTo(-1));
            Assert.That(tracker.Motion.LastText, Is.EqualTo("+1\n临−1"));
            Assert.That(tracker.Motion.LastKind, Is.EqualTo(BattleHudChangeKind.Changed));
            tracker.Observe(4, 1, 6, null, true, true);
            Assert.That(tracker.Motion.PlayCount, Is.EqualTo(1));
            tracker.Observe(4, 1, 8, null, true, true);
            Assert.That(tracker.Motion.LastText, Is.EqualTo("上限+2"));
            Assert.That(tracker.Motion.LastKind, Is.EqualTo(BattleHudChangeKind.Changed));
            tracker.Observe(4, 0, 8, null, true, true);
            Assert.That(tracker.Motion.LastText, Is.EqualTo("临−1"));
            Assert.That(tracker.Motion.LastKind, Is.EqualTo(BattleHudChangeKind.Spent));
        }

        [Test]
        public void ResourceDisableResetsBaseline_AndIdleMotionDoesNotChangeTheBadgeLayoutOrValue()
        {
            BattleResourceChangeTracker tracker = Resource();
            RectTransform rect = (RectTransform)tracker.transform;
            rect.anchoredPosition = new Vector2(64f, 120f); rect.localScale = Vector3.one * .8f;
            var numberObject = new GameObject("Amount", typeof(RectTransform), typeof(Text));
            numberObject.transform.SetParent(rect, false); Text number = numberObject.GetComponent<Text>(); number.text = "7/10";
            tracker.Observe(3, 0, 10, null, true);
            tracker.Observe(7, 0, 10, null, true);
            tracker.Motion.SetElapsed(.3f);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(64f, 120f)));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one * .8f));
            Assert.That(number.text, Is.EqualTo("7/10"));
            tracker.Motion.SetElapsed(BattleHudChangeMotion.ChangeDuration);
            rect.hasChanged = false;
            for (int frame = 0; frame < 10; frame++) Invoke(tracker.Motion, "Update");
            Assert.That(rect.hasChanged, Is.False);
            Invoke(tracker, "OnDisable");
            tracker.Observe(10, 0, 10, null, true);
            Assert.That(tracker.Motion.PlayCount, Is.EqualTo(1));
            Assert.That(tracker.Motion.IsPlaying, Is.False);
        }

        private BattleStatusRow Row()
        {
            host = new GameObject("Status feedback test", typeof(RectTransform));
            ((RectTransform)host.transform).sizeDelta = new Vector2(1920f, 1080f);
            var root = new GameObject("Player Battle Status Row", typeof(RectTransform), typeof(BattleStatusRow));
            var rect = (RectTransform)root.transform; rect.SetParent(host.transform, false);
            rect.sizeDelta = new Vector2(340f, 50f); rect.pivot = new Vector2(.5f, 1f);
            BattleStatusRow row = root.GetComponent<BattleStatusRow>();
            row.Configure(null, (RectTransform)host.transform, () => suppressed);
            return row;
        }

        private BattleResourceChangeTracker Resource()
        {
            host = new GameObject("Resource Mana", typeof(RectTransform), typeof(BattleResourceChangeTracker));
            ((RectTransform)host.transform).sizeDelta = new Vector2(60f, 52f);
            return host.GetComponent<BattleResourceChangeTracker>();
        }

        private static CoreBattleStatus Status(string id, string value, bool debuff = false, string detail = "A stable rule") =>
            new CoreBattleStatus(id, id, value, detail, id == "burn" ? CoreBattleStatusKind.Fire : CoreBattleStatusKind.Guard, debuff);
        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
