using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class TreasureMapPresentationTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        private void Create(TreasureMapPreviewState state)
        {
            host = new GameObject("Treasure UI Test");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TreasureMap, CoreGameplayPreviewPort.TwilightTide, 0);
            controller.ConfigureEditorTreasureState(state);
            Canvas.ForceUpdateCanvases();
        }

        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(item => item.name == name);
        private void Render() => typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

        [Test]
        public void CraftThenEquip_UsesCoreInventoryAndOpensDeparture()
        {
            Create(TreasureMapPreviewState.FragmentsReady);
            Assert.That(Find<Button>("Treasure Craft Map").interactable, Is.True);
            Find<Button>("Treasure Craft Map").onClick.Invoke();
            Assert.That(controller.Session.TreasureFragments, Is.Zero);
            Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(1));
            Assert.That(Find<RawImage>("Treasure Map Preview").texture, Is.Not.Null);
            Assert.That(Find<Text>("Treasure Map Use Rules").text, Does.Contain("出航时消耗"));
            Find<Button>("Treasure Equip And Depart").onClick.Invoke();
            Assert.That(controller.Session.SelectedTreasureMap, Is.Not.Null);
            Assert.That(typeof(CoreGameplayDemoController).GetField("activePortFacility", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.EqualTo(8));
            string selectedMapId = controller.Session.SelectedTreasureMap.Id;
            string destination = controller.Session.SelectedDestinationPort;
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                Button picker = Find<Button>("Departure Destination " + index);
                Assert.That(picker.IsInteractable(), Is.False, "A treasure voyage returns to its departure port.");
                picker.onClick.Invoke();
                Assert.That(controller.Session.SelectedTreasureMap.Id, Is.EqualTo(selectedMapId));
                Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo(destination));
            }
            Assert.That(Find<Text>("Destination Summary").text, Does.Contain("返航港：" + controller.Session.CurrentPort));
        }

        [TestCase(0, 4, false)]
        [TestCase(100, 3, false)]
        [TestCase(100, 4, true)]
        public void CraftAvailability_ExplainsLevelAndFragmentRequirements(int experience, int fragments, bool enabled)
        {
            Create(TreasureMapPreviewState.MapUnopened);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = experience, fragments = fragments });
            Render();
            Assert.That(Find<Button>("Treasure Craft Map").interactable, Is.EqualTo(enabled));
            if (!enabled) Assert.That(Find<Text>("Treasure Workbench Notice").text, Does.Contain(experience < 100 ? "等级 2" : "还需 1"));
        }

        [Test]
        public void CompletedPreview_ShowsPermanentReceiptWithoutAnotherRewardAction()
        {
            Create(TreasureMapPreviewState.RewardClaim);
            Assert.That(controller.Session.CompletedTreasureSeeds.Count, Is.EqualTo(1));
            Assert.That(Find<Text>("Treasure Map Details").text, Does.Contain("不会重复领取"));
            Assert.That(Find<Button>("Treasure Equip And Depart").interactable, Is.False);
            Assert.That(host.GetComponentsInChildren<Button>(true).Any(button => button.name.Contains("Claim Reward")), Is.False);
        }

        [Test]
        public void MissingFragmentsExplainsBossDropsAndTutorialGuaranteeWithoutClipping()
        {
            Create(TreasureMapPreviewState.MapUnopened);
            Text notice = Find<Text>("Treasure Workbench Notice");
            Assert.That(notice.text, Does.Contain("首领有 50% 概率掉落 1 个"));
            Assert.That(notice.preferredHeight, Is.LessThanOrEqualTo(notice.rectTransform.rect.height + 1f));
            Find<Button>("Treasure Open Folio").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Text details = Find<Text>("Treasure Map Details");
            Assert.That(details.text, Does.Contain("新手引导末尾的首领必定掉落 1 个"));
            Assert.That(details.preferredHeight, Is.LessThanOrEqualTo(details.rectTransform.rect.height + 1f));
        }

        [Test]
        public void ChoosingOrdinaryBoard_ClearsEquippedTreasureMap()
        {
            Create(TreasureMapPreviewState.MapDecoded);
            Find<Button>("Treasure Close Folio").onClick.Invoke();
            Assert.That(controller.Session.SelectedTreasureMap, Is.Not.Null);
            Find<Button>("Treasure Route 1").onClick.Invoke();
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(1));
        }

        [Test]
        public void FullFolio_AllSixMapsAndTextRemainInsideThePage()
        {
            Create(TreasureMapPreviewState.MapUnopened);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 100, fragments = 4,
                maps = Enumerable.Range(0, 6).Select(index => new CoreTreasureMapSave
                { id = "map-" + index, seed = "TREASURE-FULL-FOLIO-SEED-" + index, rankId = "weathered" }).ToArray(),
            });
            Render();
            Assert.That(Find<Button>("Treasure Craft Map").interactable, Is.False);
            Find<Button>("Treasure Open Folio").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            RectTransform page = Find<RectTransform>("Treasure Folio");
            var corners = new Vector3[4];
            foreach (RectTransform child in page.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == page) continue;
                child.GetWorldCorners(corners);
                foreach (Vector3 world in corners)
                {
                    Vector3 local = page.InverseTransformPoint(world);
                    Assert.That(local.x, Is.InRange(page.rect.xMin - 0.1f, page.rect.xMax + 0.1f), child.name);
                    Assert.That(local.y, Is.InRange(page.rect.yMin - 0.1f, page.rect.yMax + 0.1f), child.name);
                }
            }
            foreach (string name in new[] { "Treasure Map Details", "Treasure Map Use Rules", "Treasure Map Preview Note" })
            {
                Text label = Find<Text>(name);
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.1f), name);
            }
        }
        [Test]
        public void CompletionReceipt_FiveMaximumLengthSeedsKeepInventoryVisible()
        {
            Create(TreasureMapPreviewState.RewardClaim);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 100,
                completedSeeds = Enumerable.Range(0, 5).Select(index => new string('W', 63) + index).ToArray(),
                gems = int.MaxValue, ancientMaterials = int.MaxValue, relics = int.MaxValue, blackPearls = int.MaxValue,
            });
            Render();
            Canvas.ForceUpdateCanvases();
            Text details = Find<Text>("Treasure Map Details");
            Assert.That(controller.Session.CompletedTreasureSeeds.Count, Is.EqualTo(5));
            Assert.That(details.text, Does.Contain("黑珍珠 " + int.MaxValue));
            Assert.That(details.preferredHeight, Is.LessThanOrEqualTo(details.rectTransform.rect.height + 0.1f),
                "Five maximum-length receipt seeds must not clip the final inventory line.");
        }

    }
}
