using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleStatusRowEditModeTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void EditorPreview_PopulatedStatusRowHidesAndRestoresItsGraphicsAndPointerAccess()
        {
            Assert.That(Application.isPlaying, Is.False, "This regression specifically exercises the editor preview path.");
            host = new GameObject("Status row editor preview", typeof(RectTransform));
            RectTransform overlay = host.GetComponent<RectTransform>();
            overlay.sizeDelta = new Vector2(1920f, 1080f);
            var rowObject = new GameObject("Player Battle Status Row", typeof(RectTransform), typeof(BattleStatusRow));
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.SetParent(overlay, false);
            rowRect.sizeDelta = new Vector2(340f, 50f);
            rowRect.pivot = new Vector2(.5f, 1f);
            BattleStatusRow row = rowObject.GetComponent<BattleStatusRow>();
            bool moving = false;
            row.Configure(null, overlay, () => moving);
            row.SetStatuses(new[]
            {
                new CoreBattleStatus("guard", "格挡", "12", "优先吸收伤害。", CoreBattleStatusKind.Guard),
                new CoreBattleStatus("burn", "燃烧", "3", "回合末失去生命。", CoreBattleStatusKind.Fire, true)
            });

            CanvasGroup visibility = row.GetComponent<CanvasGroup>();
            Assert.That(visibility != null, Is.True, "Editor-created status rows must own a real CanvasGroup.");
            BattleStatusIconGraphic[] icons = row.GetComponentsInChildren<BattleStatusIconGraphic>();
            Assert.That(icons, Has.Length.EqualTo(2), "Suppression must be tested with populated, active symbols.");
            Assert.That(row.VisibleStatusCount, Is.EqualTo(2));
            Assert.That(visibility.alpha, Is.EqualTo(1f));
            Assert.That(visibility.blocksRaycasts, Is.True);
            Assert.That(visibility.interactable, Is.True);

            moving = true;
            row.RefreshVisibility();
            Assert.That(row.IsSuppressed, Is.True);
            Assert.That(row.VisibleStatusCount, Is.Zero);
            Assert.That(visibility.alpha, Is.Zero);
            Assert.That(visibility.blocksRaycasts, Is.False, "Hidden buffs must not intercept movement input.");
            Assert.That(visibility.interactable, Is.False);

            moving = false;
            row.RefreshVisibility();
            Assert.That(row.IsSuppressed, Is.False);
            Assert.That(row.VisibleStatusCount, Is.EqualTo(2));
            Assert.That(visibility.alpha, Is.EqualTo(1f));
            Assert.That(visibility.blocksRaycasts, Is.True);
            Assert.That(visibility.interactable, Is.True);
            CollectionAssert.AreEqual(icons, row.GetComponentsInChildren<BattleStatusIconGraphic>(),
                "Leaving movement restores the retained status icons instead of requiring a rebuild.");

            row.Configure(null, overlay, () => moving);
            Assert.That(row.GetComponents<CanvasGroup>(), Has.Length.EqualTo(1));
            Assert.That(row.GetComponent<CanvasGroup>(), Is.SameAs(visibility));
        }
    }
}
