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
        private void Click(string name) { Find<Button>(name).onClick.Invoke(); Canvas.ForceUpdateCanvases(); }
        private string Progress() => JsonUtility.ToJson(controller.Session.CaptureTreasureProgress());
        private Text[] Details() => Find<RectTransform>("Treasure Folio").GetComponentsInChildren<Text>(true)
            .Where(label => label.name.StartsWith("Treasure Map Details ")).OrderBy(label => label.name).ToArray();

        [Test]
        public void IdentifyingThenClickingCompletedBoard_CreatesSeedAndOpensUnusedBrowserWithoutEquipping()
        {
            Create(TreasureMapPreviewState.MapUnopened);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100, fragments = 4 });
            Render();
            for (int i = 0; i < 4; i++)
            {
                Assert.That(Find<RawImage>("Treasure Route Art 0").color.a, Is.LessThan(.5f));
                Click("Treasure Identify Fragment");
                Assert.That(controller.Session.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(i + 1));
                Assert.That(Find<RawImage>("Treasure Fragment 0 " + i).color.a, Is.EqualTo(1f));
            }
            Assert.That(Find<Text>("Treasure Route Name 0").text, Is.EqualTo("残旧藏宝图"));
            Assert.That(Find<Text>("Treasure Route Progress 0").text, Does.Contain("4/4").And.Contain("点击合成"));
            Click("Treasure Route 0");
            Assert.That(controller.Session.TreasureFragments, Is.Zero);
            Assert.That(controller.Session.GetIdentifiedTreasureFragments("weathered"), Is.Zero);
            Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(1));
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Assert.That(Find<RawImage>("Treasure Map Preview 0").texture.name, Is.EqualTo("treasure_chart_weathered"));
            Assert.That(Find<Text>("Treasure Map Details 0").text,
                Does.Contain(controller.Session.TreasureMaps[0].Seed).And.Contain("残旧").And.Contain("探索度"));
            Assert.That(host.GetComponentsInChildren<Button>(true).Any(button => button.name == "Treasure Equip And Depart"), Is.False);
        }

        [TestCase(0, 4, false)]
        [TestCase(100, 0, false)]
        [TestCase(100, 1, true)]
        public void IdentifyAvailability_UsesOneUnidentifiedFragment(int experience, int fragments, bool enabled)
        {
            Create(TreasureMapPreviewState.MapUnopened);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = experience, fragments = fragments });
            Render();
            Assert.That(Find<Button>("Treasure Identify Fragment").interactable, Is.EqualTo(enabled));
            Assert.That(controller.Session.TreasureMaps, Is.Empty);
        }

        [TestCase(100)]
        [TestCase(260)]
        [TestCase(900)]
        public void Identification_IgnoresClickedBoardAndOnlyAssignsUnlockedRank(int experience)
        {
            Create(TreasureMapPreviewState.MapUnopened);
            var fixture = new CoreTreasureProgress {
                pirateExperience = experience, fragments = 1,
                identificationSeed = "TREASURE-BOARD-SELECTION", identificationSequence = 17,
            };
            controller.Session.RestoreTreasureProgress(fixture);
            Render();
            Click("Treasure Identify Fragment");
            string expectedRank = CoreTreasureHuntCatalog.Ranks
                .Single(rank => controller.Session.GetIdentifiedTreasureFragments(rank.Id) == 1).Id;

            for (int board = 0; board < CoreTreasureHuntCatalog.Ranks.Count; board++)
            {
                controller.Session.RestoreTreasureProgress(fixture);
                Render();
                Click("Treasure Route " + board);
                Click("Treasure Identify Fragment");
                CoreTreasureMapRank assigned = CoreTreasureHuntCatalog.Ranks
                    .Single(rank => controller.Session.GetIdentifiedTreasureFragments(rank.Id) == 1);
                Assert.That(assigned.Id, Is.EqualTo(expectedRank), "Clicking a board must not choose the identification result.");
                Assert.That(assigned.UnlockLevel, Is.LessThanOrEqualTo(controller.Session.PirateLevel));
                Assert.That(CoreTreasureHuntCatalog.Ranks.Sum(rank => controller.Session.GetIdentifiedTreasureFragments(rank.Id)), Is.EqualTo(1));
                Assert.That(controller.Session.TreasureFragments, Is.Zero);
                Assert.That(controller.Session.TreasureMaps, Is.Empty);
            }
        }

        [Test]
        public void Workbench_RemovesUpperStatusRailAndItsText()
        {
            Create(TreasureMapPreviewState.MapUnopened);
            foreach (int fragments in new[] { 0, 4 })
            {
                controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100, fragments = fragments });
                Render();
                foreach (string name in new[] { "Treasure Workbench Status Rail", "Treasure Workbench Status", "Treasure Workbench Notice" })
                    Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == name), Is.False, name);
                Assert.That(Find<Text>("Treasure Identify Status").text, Is.EqualTo(fragments + " 片待鉴定"));
            }
            Click("Treasure Identify Fragment");
            foreach (string name in new[] { "Treasure Workbench Status Rail", "Treasure Workbench Status", "Treasure Workbench Notice" })
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == name), Is.False, name);
            Assert.That(Find<Text>("Treasure Identify Status").text, Is.EqualTo("3 片待鉴定"));
        }

        [Test]
        public void EmptyFolio_UsesOneLargeParchmentAndOnlyTheRequestedEmptyMessage()
        {
            Create(TreasureMapPreviewState.MapUnopened);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100 });
            Render();
            Click("Treasure Open Folio");
            Image paper = Find<Image>("Treasure Folio");
            Assert.That(paper.sprite, Is.SameAs(MarketShopSkin.Paper));
            Assert.That(paper.type, Is.EqualTo(Image.Type.Sliced));
            Text empty = Find<Text>("Treasure Empty Inventory");
            Assert.That(empty.text, Is.EqualTo("尚未获得藏宝种子"));
            Assert.That(empty.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(empty.rectTransform.anchoredPosition.x, Is.EqualTo(0f).Within(.1f));
            Assert.That(Details(), Is.Empty);
            Assert.That(Find<Text>("Treasure Folio Title").text, Is.EqualTo("藏图册"));
            Assert.That(Find<Button>("Treasure Browse Unused").GetComponentInChildren<Text>().text, Is.EqualTo("查看未使用的藏宝图"));
            Assert.That(Find<Button>("Treasure Browse Used").GetComponentInChildren<Text>().text, Is.EqualTo("查看已使用的藏宝图"));
            string[] removed = { "一次性的寻宝航程", "先鉴定获得的碎片", "从碎片到藏宝图", "秘藏完成回执", "取消装备", "前往出航码头" };
            foreach (string text in removed)
                Assert.That(paper.GetComponentsInChildren<Text>(true).Any(label => label.text.Contains(text)), Is.False, text);
            string before = Progress();
            Click("Treasure Browse Unused");
            Assert.That(Find<Text>("Treasure Empty Inventory").text, Is.EqualTo("尚未获得藏宝种子"));
            Click("Treasure Folio Back");
            Click("Treasure Browse Used");
            Assert.That(Details(), Is.Empty);
            Assert.That(Progress(), Is.EqualTo(before));
        }

        [Test]
        public void FolioOverview_ShowsUnusedCountAndBrowsingPreservesEquippedMapAndRoute()
        {
            Create(TreasureMapPreviewState.MapDecoded);
            string before = Progress();
            int route = controller.Session.SelectedRouteIndex;
            Click("Treasure Folio Back");
            Assert.That(Find<Text>("Treasure Inventory Summary").text, Is.EqualTo("未使用的藏宝图 · 1 张"));
            Assert.That(Details(), Is.Empty);
            Click("Treasure Browse Unused");
            Assert.That(Details(), Has.Length.EqualTo(1));
            Click("Treasure Folio Back");
            Click("Treasure Browse Used");
            Click("Treasure Close Folio");
            Click("Treasure Route 1");
            Assert.That(controller.Session.SelectedTreasureMap, Is.Not.Null);
            Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(route));
            Assert.That(Progress(), Is.EqualTo(before));
        }

        [Test]
        public void FiveUnusedMaps_TurnPagesTwoAtATimeWithoutChangingInventory()
        {
            Create(TreasureMapPreviewState.MapUnopened);
            controller.Session.RestoreTreasureProgress(UnusedFixture(5));
            Render();
            string before = Progress();
            Click("Treasure Open Folio");
            Click("Treasure Browse Unused");
            for (int page = 0; page < 3; page++)
            {
                int count = page == 2 ? 1 : 2;
                Assert.That(Details(), Has.Length.EqualTo(count));
                for (int slot = 0; slot < count; slot++)
                {
                    int index = page * 2 + slot;
                    Assert.That(Find<Text>("Treasure Map Details " + slot).text,
                        Does.Contain("FOLIO-SEED-" + index).And.Contain("探索度").And.Contain("0%"));
                    Assert.That(Find<RawImage>("Treasure Map Preview " + slot).texture.name,
                        Is.EqualTo("treasure_chart_" + CoreTreasureHuntCatalog.Ranks[index % 3].Id));
                }
                Assert.That(Find<Button>("Treasure Folio Previous Page").interactable, Is.EqualTo(page > 0));
                Assert.That(Find<Button>("Treasure Folio Next Page").interactable, Is.EqualTo(page < 2));
                if (page < 2) Click("Treasure Folio Next Page");
            }
            Click("Treasure Folio Previous Page");
            Click("Treasure Folio Previous Page");
            Assert.That(Find<Text>("Treasure Map Details 0").text, Does.Contain("FOLIO-SEED-0"));
            Assert.That(Progress(), Is.EqualTo(before));
        }

        [Test]
        public void UsedBrowser_ShowsFullSeaChartsRankSeedAndRecordedExploration()
        {
            Create(TreasureMapPreviewState.MapUnopened);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 900,
                usedMaps = Enumerable.Range(0, 3).Select(index => new CoreUsedTreasureMapSave
                {
                    id = "used-" + index, seed = "USED-FOLIO-SEED-" + index,
                    rankId = CoreTreasureHuntCatalog.Ranks[index].Id, departurePort = "断锚港",
                    hasExplorationCoverage = true, explorationCoverage = 37.5f + index,
                }).ToArray(),
            });
            Render();
            string before = Progress();
            Click("Treasure Open Folio");
            Assert.That(Find<Text>("Treasure Empty Inventory").text, Is.EqualTo("尚未获得藏宝种子"),
                "Used history does not count as an unused seed.");
            Click("Treasure Browse Used");
            for (int page = 0; page < 2; page++)
            {
                int count = page == 0 ? 2 : 1;
                Assert.That(Details(), Has.Length.EqualTo(count));
                for (int slot = 0; slot < count; slot++)
                {
                    int index = page * 2 + slot;
                    Text details = Find<Text>("Treasure Map Details " + slot);
                    Assert.That(details.text, Does.Contain(CoreTreasureHuntCatalog.Ranks[index].Name)
                        .And.Contain("USED-FOLIO-SEED-" + index).And.Contain((37.5f + index).ToString("0.#") + "%"));
                    RawImage preview = Find<RawImage>("Treasure Map Preview " + slot);
                    Assert.That(preview.GetComponent<VoyageChartSurfaceGraphic>().Field, Is.Not.Null);
                    Assert.That(preview.texture, Is.Not.Null);
                    Assert.That(preview.texture, Is.Not.SameAs(Resources.Load<Texture2D>(
                        "TreasureHunt/treasure_chart_" + CoreTreasureHuntCatalog.Ranks[index].Id)),
                        "A used record displays its generated sea chart, including areas beyond explored water.");
                    Assert.That(preview.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                    RawImage water = Find<RawImage>("Treasure Map Water " + slot);
                    Assert.That(water.texture, Is.SameAs(Resources.Load<Texture2D>("VoyageMapV2/water_chart_base_v2")));
                    Assert.That(water.texture, Is.Not.Null);
                    Assert.That(water.color.a, Is.EqualTo(1f));
                    Assert.That(water.raycastTarget, Is.False);
                    Assert.That(water.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                    Assert.That(water.transform.parent, Is.SameAs(preview.transform.parent));
                    Assert.That(water.transform.GetSiblingIndex(), Is.LessThan(preview.transform.GetSiblingIndex()),
                        "The opaque ocean base must draw beneath the partially transparent coast layer.");
                    Assert.That(water.rectTransform.sizeDelta, Is.EqualTo(preview.rectTransform.sizeDelta));
                    Assert.That(water.rectTransform.anchoredPosition, Is.EqualTo(preview.rectTransform.anchoredPosition));
                }
                if (page == 0) Click("Treasure Folio Next Page");
            }
            Assert.That(Progress(), Is.EqualTo(before));
        }

        [Test]
        public void LegacyCompletion_UnknownRankAndExplorationRemainUnrecordedWhenBrowsed()
        {
            Create(TreasureMapPreviewState.RewardClaim);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 100, completedSeeds = new[] { "LEGACY-COMPLETED-SEED" },
                gems = 7, ancientMaterials = 11,
            });
            Render();
            string before = Progress();
            Text details = Find<Text>("Treasure Map Details 0");
            Assert.That(details.text, Does.Contain("LEGACY-COMPLETED-SEED").And.Contain("未记录"));
            Assert.That(details.text, Does.Not.Contain("100%"));
            Click("Treasure Folio Back");
            Click("Treasure Browse Used");
            Assert.That(Progress(), Is.EqualTo(before));
            Assert.That(host.GetComponentsInChildren<Button>(true).Any(button => button.name.Contains("Claim Reward")), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullFolio_LongSeedsStayInsideParchmentAcrossEveryPage(bool used)
        {
            Create(TreasureMapPreviewState.MapUnopened);
            CoreTreasureProgress fixture = UnusedFixture(6);
            foreach (CoreTreasureMapSave map in fixture.maps) map.seed = new string('W', 63) + map.id.Last();
            if (used)
            {
                fixture.usedMaps = fixture.maps.Select((map, index) => new CoreUsedTreasureMapSave
                {
                    id = "long-used-" + index, seed = map.seed, rankId = map.rankId,
                    departurePort = "断锚港", hasExplorationCoverage = true, explorationCoverage = 99.5f,
                }).ToArray();
                fixture.maps = new CoreTreasureMapSave[0];
                fixture.selectedMapId = string.Empty;
            }
            controller.Session.RestoreTreasureProgress(fixture);
            Render();
            Click("Treasure Open Folio");
            Click(used ? "Treasure Browse Used" : "Treasure Browse Unused");
            for (int pageIndex = 0; pageIndex < 3; pageIndex++)
            {
                RectTransform page = Find<RectTransform>("Treasure Folio");
                var corners = new Vector3[4];
                foreach (RectTransform child in page.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child == page) continue;
                    child.GetWorldCorners(corners);
                    foreach (Vector3 world in corners)
                    {
                        Vector3 local = page.InverseTransformPoint(world);
                        Assert.That(local.x, Is.InRange(page.rect.xMin - .1f, page.rect.xMax + .1f), child.name);
                        Assert.That(local.y, Is.InRange(page.rect.yMin - .1f, page.rect.yMax + .1f), child.name);
                    }
                }
                foreach (Text label in Details())
                {
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), label.name);
                    using (var generator = new TextGenerator())
                    {
                        Assert.That(generator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size)), Is.True);
                        int visibleGlyphs = 0;
                        for (int quad = 0; quad + 3 < generator.verts.Count; quad += 4)
                        {
                            Vector3[] points = Enumerable.Range(0, 4).Select(index => generator.verts[quad + index].position).ToArray();
                            if (points.Max(point => point.x) - points.Min(point => point.x) > .001f &&
                                points.Max(point => point.y) - points.Min(point => point.y) > .001f) visibleGlyphs++;
                        }
                        Assert.That(visibleGlyphs, Is.EqualTo(label.text.Count(character => !char.IsWhiteSpace(character))),
                            label.name + ": every seed character and the final exploration line must render.");
                    }
                }
                if (pageIndex < 2) Click("Treasure Folio Next Page");
            }
        }

        private static CoreTreasureProgress UnusedFixture(int count) => new CoreTreasureProgress
        {
            pirateExperience = 900, fragments = 8, selectedMapId = "map-0",
            maps = Enumerable.Range(0, count).Select(index => new CoreTreasureMapSave
            {
                id = "map-" + index, seed = "FOLIO-SEED-" + index,
                rankId = CoreTreasureHuntCatalog.Ranks[index % 3].Id,
            }).ToArray(),
        };
    }
}
