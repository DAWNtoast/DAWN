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
        public IEnumerator ItemIcons_TreasureWorkshopAndAllMapRanksKeepCraftingAndSelectionReachable()
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
            AssertIconPointerReachesButton(fragment, "Treasure Craft Map", false);
            Assert.That(GameObject.Find("Treasure Workbench Status").GetComponent<Text>().text,
                Does.Contain("图碎片 8/" + CoreTreasureHuntCatalog.FragmentCost));
            NavalBattleVisualCapture.Capture(controller, "ItemIcons_07_Treasure_Workshop.png",
                "工坊碎片小图标；8碎片与三档藏宝图由隔离展示夹具准备，非游玩所得", "Treasure Workbench");

            ClickAcceptanceButton("Treasure Open Folio"); yield return null;
            yield return WaitForItemIconView(controller);
            for (int index = 0; index < ranks.Length; index++)
            {
                RawImage map = AssertItemIcon("Treasure Map Rank Icon " + index,
                    ItemIconCatalog.ForTreasureMap(ranks[index]), 40f);
                AssertIconDoesNotCoverRowText(map);
                AssertIconPointerReachesButton(map, "Treasure Map Select " + index, false);
                Assert.That(ActiveButton("Treasure Map Select " + index).GetComponentInChildren<Text>().text,
                    Does.Contain(session.TreasureMaps[index].Name));
            }

            for (int index = 0; index < ranks.Length; index++)
            {
                // Click the art itself through the actual raycast path. Its
                // button must still select the matching detail panel.
                RawImage map = GameObject.Find("Treasure Map Rank Icon " + index).GetComponent<RawImage>();
                AssertIconPointerReachesButton(map, "Treasure Map Select " + index, true);
                yield return null;
                yield return WaitForItemIconView(controller);
                RawImage heading = AssertItemIcon("Treasure Map Heading Icon",
                    ItemIconCatalog.ForTreasureMap(ranks[index]), 40f);
                AssertIconDoesNotCoverRowText(heading);
                Assert.That(GameObject.Find("Treasure Map Heading").GetComponent<Text>().text,
                    Does.StartWith(session.TreasureMaps[index].Name));
                Assert.That(session.TreasureFragments, Is.EqualTo(8));
                Assert.That(session.TreasureMaps.Count, Is.EqualTo(3));
                Assert.That(session.SelectedTreasureMap.Id, Is.EqualTo("item-icon-weathered"),
                    "Browsing map art must not silently equip or consume a map.");
                NavalBattleVisualCapture.Capture(controller,
                    "ItemIcons_" + (8 + index).ToString("00") + "_Treasure_Folio_" + ranks[index] + ".png",
                    session.TreasureMaps[index].Name + "：列表与详情共用品阶图标，保留原大型路线地图；库存为隔离展示夹具",
                    "Treasure Folio");
            }

            ClickAcceptanceButton("Treasure Close Folio"); yield return null;
            yield return WaitForItemIconView(controller);
            fragment = GameObject.Find("Treasure Craft Fragment Icon").GetComponent<RawImage>();
            AssertIconPointerReachesButton(fragment, "Treasure Craft Map", true);
            yield return null;
            yield return WaitForItemIconView(controller);
            Assert.That(session.TreasureFragments, Is.EqualTo(8 - CoreTreasureHuntCatalog.FragmentCost));
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(4), "Clicking the icon must craft exactly one map.");
            Assert.That(session.SelectedTreasureMap, Is.SameAs(session.TreasureMaps.Last()));
            AssertItemIcon("Treasure Map Heading Icon",
                ItemIconCatalog.ForTreasureMap(session.SelectedTreasureMap.RankId), 40f);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
