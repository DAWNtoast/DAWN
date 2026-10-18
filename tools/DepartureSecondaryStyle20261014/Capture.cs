// Opt-in live capture driver. Installed only in the isolated validation project's
// PlayMode test assembly; reuses the existing real departure and persistence fixture.
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureSecondaryStyleCapture_SuppliesPurchaseAndTreasurePickerStates()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    int quantity = item == PortMarketItem.Water || item == PortMarketItem.Food ? 24 :
                        item == PortMarketItem.Cannonballs ? 8 : item == PortMarketItem.Powder ? 6 : 2;
                    for (int index = 0; index < quantity; index++)
                        ClickAcceptanceButton("Departure Basic Supply Increase " + key);
                }
                yield return null;
                Assert.That(ActiveButton("Departure Basic Supplies Purchase").IsInteractable(), Is.True,
                    "Capture must show an actionable live quote.");
                NavalBattleVisualCapture.Capture(controller, "DepartureSecondary_01_SuppliesQuote.png",
                    "基础物资：真实待购数量与可购买报价", "Departure Basic Supplies");
                decimal previousGold = controller.Session.ProfileGold;
                ClickAcceptanceButton("Departure Basic Supplies Purchase"); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.LessThan(previousGold));
                Assert.That(GameObject.Find("Departure Basic Supplies Hint").GetComponent<Text>().text,
                    Does.Contain("已装入船舱"));
                NavalBattleVisualCapture.Capture(controller, "DepartureSecondary_02_SuppliesPurchased.png",
                    "基础物资：实际购买成功后的库存、数量和反馈", "Departure Basic Supplies");
                ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;

                ClickAcceptanceButton("Departure Treasure Seed Picker"); yield return null;
                Assert.That(GameObject.Find("Departure Treasure Seed Empty"), Is.Not.Null);
                NavalBattleVisualCapture.Capture(controller, "DepartureSecondary_03_TreasureEmpty.png",
                    "藏宝海图种子：空库存说明和操作入口", "Departure Treasure Seed Card");
                ClickAcceptanceButton("Close Departure Treasure Seed Selection"); yield return null;

                controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
                {
                    pirateExperience = 1500,
                    maps = Enumerable.Range(0, 6).Select(index => new CoreTreasureMapSave
                    {
                        id = "departure-secondary-capture-" + index,
                        rankId = index % 2 == 0 ? "weathered" : "gilded",
                        seed = "DS-20261014-" + (101 + index) + "-TREASURE-SEA",
                    }).ToArray(),
                });
                ClickAcceptanceButton("Departure Treasure Seed Picker"); yield return null;
                Assert.That(GameObject.Find("Departure Treasure Seed Option 5"), Is.Not.Null);
                NavalBattleVisualCapture.Capture(controller, "DepartureSecondary_04_TreasureList.png",
                    "藏宝海图种子：六个可用种子的完整列表", "Departure Treasure Seed Card");
                ClickAcceptanceButton("Departure Treasure Seed Option 1"); yield return null;
                Assert.That(controller.Session.SelectedTreasureMap, Is.Not.Null);
                ClickAcceptanceButton("Departure Treasure Seed Picker"); yield return null;
                NavalBattleVisualCapture.Capture(controller, "DepartureSecondary_05_TreasureSelected.png",
                    "藏宝海图种子：真实选择后的选中状态", "Departure Treasure Seed Card");
                ClickAcceptanceButton("Close Departure Treasure Seed Selection"); yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }
    }
}
