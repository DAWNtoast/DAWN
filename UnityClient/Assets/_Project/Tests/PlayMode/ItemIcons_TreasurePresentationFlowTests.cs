using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ItemIcons_TreasureWorkshopAndAllMapRanksKeepCraftingAndBrowsingReachable()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            string[] ranks = { "weathered", "gilded", "moonlit" };
            // Isolated presentation fixture, matching the existing treasure
            // navigation/provision tests. These maps were not earned in play.
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 900,
                fragments = 8,
                selectedMapId = "item-icon-weathered",
                maps = ranks.Select(rank => new CoreTreasureMapSave {
                    id = "item-icon-" + rank, seed = "ITEM-ICON-" + rank, rankId = rank
                }).ToArray()
            });

            ClickAcceptanceButton("Port Hotspot Treasure"); yield return null;
            yield return WaitForItemIconView(controller);
            RawImage fragment = AssertItemIcon("Treasure Craft Fragment Icon",
                ItemIconCatalog.ForSpecial("treasurefragment"), 32f);
            AssertIconDoesNotCoverRowText(fragment);
            AssertIconPointerReachesButton(fragment, "Treasure Identify Fragment", false);
            Assert.That(GameObject.Find("Treasure Identify Status").GetComponent<Text>().text,
                Is.EqualTo("8 片待鉴定"));
            NavalBattleVisualCapture.Capture(controller, "ItemIcons_07_Treasure_Workshop.png",
                "工坊碎片小图标；8碎片与三档藏宝图由隔离展示夹具准备，非游玩所得", "Treasure Workbench");

            ClickAcceptanceButton("Treasure Open Folio"); yield return null;
            yield return WaitForItemIconView(controller);
            ClickAcceptanceButton("Treasure Browse Unused"); yield return null;
            yield return WaitForItemIconView(controller);
            for (int page = 0; page < 2; page++)
            {
                for (int slot = 0; slot < 2 && page * 2 + slot < ranks.Length; slot++)
                {
                    int index = page * 2 + slot;
                    RawImage map = GameObject.Find("Treasure Map Preview " + slot).GetComponent<RawImage>();
                    Assert.That(map.texture.name, Is.EqualTo("treasure_chart_" + ranks[index]));
                    Assert.That(map.raycastTarget, Is.False);
                    Assert.That(GameObject.Find("Treasure Map Details " + slot).GetComponent<Text>().text,
                        Does.Contain(session.TreasureMaps[index].Rank.Name).And.Contain(session.TreasureMaps[index].Seed)
                        .And.Contain("探索度"));
                }
                Assert.That(session.TreasureFragments, Is.EqualTo(8));
                Assert.That(session.TreasureMaps.Count, Is.EqualTo(3));
                Assert.That(session.SelectedTreasureMap.Id, Is.EqualTo("item-icon-weathered"),
                    "Turning pages must preserve the equipped map and inventory.");
                NavalBattleVisualCapture.Capture(controller,
                    "ItemIcons_" + (8 + page).ToString("00") + "_Treasure_Folio_Page_" + page + ".png",
                    "藏图册每页最多两张海图，三档藏宝图分两页展示；库存为隔离展示夹具",
                    "Treasure Folio");
                if (page == 0)
                {
                    ClickAcceptanceButton("Treasure Folio Next Page"); yield return null;
                    yield return WaitForItemIconView(controller);
                }
            }

            ClickAcceptanceButton("Treasure Close Folio"); yield return null;
            yield return WaitForItemIconView(controller);
            fragment = GameObject.Find("Treasure Craft Fragment Icon").GetComponent<RawImage>();
            AssertIconPointerReachesButton(fragment, "Treasure Identify Fragment", true);
            yield return null;
            yield return WaitForItemIconView(controller);
            Assert.That(session.TreasureFragments, Is.EqualTo(7));
            Assert.That(CoreTreasureHuntCatalog.Ranks.Sum(rank => session.GetIdentifiedTreasureFragments(rank.Id)), Is.EqualTo(1));
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(3), "Identifying does not craft a map.");
            Assert.That(session.SelectedTreasureMap.Id, Is.EqualTo("item-icon-weathered"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
