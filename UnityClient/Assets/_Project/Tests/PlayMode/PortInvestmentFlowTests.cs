using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        // These are isolated UI transaction fixtures. Port and resource setup
        // does not represent earned money, a market purchase, or a sailed route.
        // The shared SetUp/TearDown preserves all six investment keys and cargo.
        [UnityTest, Timeout(120000)]
        public IEnumerator PortInvestment_SixPortsConsumeFourQuotedResourcesAndPersistOnlyTheirOwnLevel()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var completedPorts = new HashSet<string>();
            int captureIndex = 0;
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                PrepareInvestmentResources(controller, port);
                yield return OpenInvestmentThroughMarket();
                PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, 1);
                AssertInvestmentQuote(port, cost);
                InvestmentResourceSnapshot before = SnapshotInvestmentResources(controller.Session);
                Button confirm = ActiveButton("Confirm Port Investment");
                Assert.That(confirm.IsInteractable(), Is.True);
                Button.ButtonClickedEvent staleConfirmation = confirm.onClick;
                NavalBattleVisualCapture.Capture(controller,
                    "PortInvestment_" + (++captureIndex).ToString("00") + "_" + port + ".png",
                    "港口投资 UI 事务夹具 · " + port + "四项真实报价；不代表自然积累资源", "Port Investment Panel");

                ClickAcceptanceButton("Confirm Port Investment"); yield return null;
                AssertInvestmentDeduction(controller.Session, before, cost);
                completedPorts.Add(port);
                AssertInvestmentLevels(completedPorts);
                Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.True,
                    "The fixture deliberately affords Lv2, so stale-callback rejection cannot pass only for insufficient resources.");
                InvestmentResourceSnapshot after = SnapshotInvestmentResources(controller.Session);
                staleConfirmation.Invoke(); staleConfirmation.Invoke(); yield return null;
                AssertInvestmentResourcesEqual(controller.Session, after);
                AssertInvestmentLevels(completedPorts);

                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
                yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.CurrentPort, Is.EqualTo(port));
                AssertInvestmentResourcesEqual(controller.Session, after);
                AssertInvestmentLevels(completedPorts);
            }
            Assert.That(completedPorts.Count, Is.EqualTo(6));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortInvestment_MissingEachPortsSpecialResourceCannotPartiallyChargeOrUpgrade()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                PrepareInvestmentResources(controller, port);
                PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, 1);
                Assert.That(cost.RequiredQuantity, Is.GreaterThan(0));
                SetInvestmentCargo(controller.Session.Cargo, cost.RequiredItem, cost.RequiredQuantity - 1);
                yield return OpenInvestmentThroughMarket();
                InvestmentResourceSnapshot before = SnapshotInvestmentResources(controller.Session);
                Button confirm = ActiveButton("Confirm Port Investment");
                Assert.That(confirm.IsInteractable(), Is.False);
                Assert.That(GameObject.Find("Port Investment Notice").GetComponent<Text>().text,
                    Does.Contain(PortMarketCatalog.Name(cost.RequiredItem) + " 1"), "The missing resource must use this port's actual specialty.");
                // Defensive event test: disabling a button alone must not permit
                // an old/programmatic callback to partially deduct common costs.
                confirm.onClick.Invoke(); yield return null;
                AssertInvestmentResourcesEqual(controller.Session, before);
                AssertInvestmentLevels(new HashSet<string>());
                ClickAcceptanceButton("Return To Port Hub"); yield return null;
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortInvestment_QuoteCapturedInAnotherPortCannotInvestAfterPortChanges()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string source = CoreVoyageSession.DestinationPorts[0], destination = CoreVoyageSession.DestinationPorts[1];
            PrepareInvestmentResources(controller, source);
            yield return OpenInvestmentThroughMarket();
            Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.True);
            Button.ButtonClickedEvent staleConfirmation = ActiveButton("Confirm Port Investment").onClick;
            ClickAcceptanceButton("Return To Port Hub"); yield return null;

            PrepareInvestmentResources(controller, destination);
            yield return OpenInvestmentThroughMarket();
            Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.True);
            InvestmentResourceSnapshot before = SnapshotInvestmentResources(controller.Session);
            staleConfirmation.Invoke(); yield return null;
            AssertInvestmentResourcesEqual(controller.Session, before);
            AssertInvestmentLevels(new HashSet<string>());
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(destination));

            // The destination's own fresh quote is still actionable after the
            // stale source callback is rejected.
            ClickAcceptanceButton("Confirm Port Investment"); yield return null;
            AssertInvestmentDeduction(controller.Session, before, PortInvestmentCatalog.CostForLevel(destination, 1));
            AssertInvestmentLevels(new HashSet<string> { destination });
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortInvestment_RareDonationsAtBothHighTiersUseLocalStockThenShipAndPersist()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (string port in CoreVoyageSession.DestinationPorts)
            foreach (int target in new[] { 4, 5 })
            {
                PortInvestmentCost cost = PrepareRareInvestmentFixture(controller, port, target);
                yield return OpenInvestmentThroughMarket();
                string resources = GameObject.Find("Port Investment Resources").GetComponent<Text>().text;
                string tier = GameObject.Find("Port Investment Tier Label " + target).GetComponent<Text>().text;
                string button = ActiveButton("Confirm Port Investment").GetComponentInChildren<Text>().text;
                foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                {
                    string name = CoreWarehouseItemCatalog.Find(rare.Key).Name;
                    Assert.That(resources, Does.Contain(name + " " + (rare.Quantity + 1)));
                    Assert.That(tier, Does.Contain(rare.Quantity + " " + name));
                    Assert.That(button, Does.Contain(rare.Quantity + " " + name));
                }
                Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.True);
                Button.ButtonClickedEvent stale = ActiveButton("Confirm Port Investment").onClick;
                decimal goldBefore = controller.Session.ProfileGold;
                string other = CoreVoyageSession.DestinationPorts.First(candidate => candidate != port);
                if (target == 5 || port == "断锚港")
                    NavalBattleVisualCapture.Capture(controller, "PortInvestment_Rare_" + port + "_Lv" + target + ".png",
                        "高阶投资隔离UI夹具：全部基础与珍稀报价；不代表自然赚取资源", "Port Investment Panel");

                ClickAcceptanceButton("Confirm Port Investment"); yield return null;
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port)), Is.EqualTo(target));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBefore - cost.Gold));
                foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                {
                    Assert.That(controller.Session.PortWarehouse.GetAmount(port, rare.Key), Is.Zero);
                    Assert.That(controller.Session.GetWarehouseCargoAmount(rare.Key), Is.EqualTo(1),
                        "Stored quantity is consumed first and precisely one carried item supplies the shortfall.");
                    Assert.That(controller.Session.PortWarehouse.GetAmount(other, rare.Key), Is.EqualTo(20));
                }
                Assert.That(controller.Session.PortWarehouse.GetAmount(port, "wood"), Is.Zero);
                Assert.That(controller.Session.PortWarehouse.GetAmount(port, "iron"), Is.Zero);
                Assert.That(controller.Session.PortWarehouse.GetAmount(port, PortMarketCatalog.Key(cost.RequiredItem)), Is.Zero);
                string after = SnapshotRareInvestment(controller.Session);
                AssertRareWarehouseSavedSplit(controller.Session);
                stale.Invoke(); stale.Invoke(); yield return null;
                Assert.That(SnapshotRareInvestment(controller.Session), Is.EqualTo(after));

                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
                yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.CurrentPort, Is.EqualTo(port));
                Assert.That(SnapshotRareInvestment(controller.Session), Is.EqualTo(after),
                    "Treasure progress must not resurrect donated carried rares on reload.");
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port)), Is.EqualTo(target));
                AssertRareWarehouseSavedSplit(controller.Session);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortInvestment_ForeignRareCannotHideLocalShortfallOrPartiallyPayAnyPort()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, 5);
                string missingKey = cost.RareMaterials[1].Key;
                PrepareRareInvestmentFixture(controller, port, 5, missingKey);
                yield return OpenInvestmentThroughMarket();
                Assert.That(controller.Session.GetPortInvestmentMaterialAmount(missingKey), Is.Zero);
                string other = CoreVoyageSession.DestinationPorts.First(candidate => candidate != port);
                Assert.That(controller.Session.PortWarehouse.GetAmount(other, missingKey), Is.EqualTo(20));
                Button confirm = ActiveButton("Confirm Port Investment");
                Assert.That(confirm.IsInteractable(), Is.False);
                Assert.That(GameObject.Find("Port Investment Notice").GetComponent<Text>().text,
                    Does.Contain(CoreWarehouseItemCatalog.Find(missingKey).Name + " 1"));
                string before = SnapshotRareInvestment(controller.Session);
                confirm.onClick.Invoke(); yield return null;
                Assert.That(SnapshotRareInvestment(controller.Session), Is.EqualTo(before));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port)), Is.EqualTo(4));
                if (port == "白贝港")
                    NavalBattleVisualCapture.Capture(controller, "PortInvestment_Rare_Shortfall.png",
                        "其他港罗盘库存不能替本港投资付款，缺口显示与整单拒绝", "Port Investment Panel");
                ClickAcceptanceButton("Return To Port Hub"); yield return null;
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortInvestment_ExistingMaximumLevelsNeverBackChargeNewRareRequirements()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (string port in CoreVoyageSession.DestinationPorts)
                PlayerPrefs.SetInt(CoreGameplayDemoController.PortInvestmentKey(port), PortInvestmentCatalog.MaxLevel);
            controller.Session.RestoreProfile(1234.5678m, "白贝港");
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
                SetInvestmentWarehouseCargo(controller.Session, item.Key, 0);
            Assert.That(controller.Session.PortWarehouse.Restore("v1;白贝港,wood,3;断锚港,iron,2"), Is.True);
            // This is an existing-save fixture: persist Arrange explicitly.
            // Merely opening a fully built facility need not trigger a save.
            typeof(CoreGameplayDemoController).GetMethod("SaveProgress",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controller, null);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(1234.5678m));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.PortWarehouseCargoKey),
                Is.EqualTo(controller.Session.PortWarehouse.Serialize()));
            string before = SnapshotRareInvestment(controller.Session);
            yield return OpenInvestmentThroughMarket();
            Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.False);
            Assert.That(GameObject.Find("Port Investment Notice").GetComponent<Text>().text, Does.Contain("建设已完成"));
            ActiveButton("Confirm Port Investment").onClick.Invoke(); yield return null;
            Assert.That(SnapshotRareInvestment(controller.Session), Is.EqualTo(before));
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(SnapshotRareInvestment(controller.Session), Is.EqualTo(before));
            foreach (string port in CoreVoyageSession.DestinationPorts)
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port)), Is.EqualTo(5));
            LogAssert.NoUnexpectedReceived();
        }

        private static PortInvestmentCost PrepareRareInvestmentFixture(CoreGameplayDemoController controller,
            string port, int target, string missingRare = null)
        {
            PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, target);
            PlayerPrefs.SetInt(CoreGameplayDemoController.PortInvestmentKey(port), target - 1);
            controller.Session.RestoreProfile(cost.Gold + 10000.4321m, port);
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
                SetInvestmentWarehouseCargo(controller.Session, item.Key, 0);
            string other = CoreVoyageSession.DestinationPorts.First(candidate => candidate != port);
            string warehouse = "v2;" + port + ",wood," + cost.Wood + ";" + port + ",iron," + cost.Iron +
                ";" + port + "," + PortMarketCatalog.Key(cost.RequiredItem) + "," + cost.RequiredQuantity;
            foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
            {
                warehouse += ";" + other + "," + rare.Key + ",20";
                if (rare.Key == missingRare) continue;
                if (rare.Quantity > 1) warehouse += ";" + port + "," + rare.Key + "," + (rare.Quantity - 1);
                SetInvestmentWarehouseCargo(controller.Session, rare.Key, 2);
            }
            Assert.That(controller.Session.PortWarehouse.Restore(warehouse), Is.True);
            Assert.That(controller.Session.Cargo.UsedVolume, Is.LessThan(controller.Session.CargoCapacityValue));
            return cost;
        }

        private static void SetInvestmentWarehouseCargo(CoreVoyageSession session, string key, int amount) =>
            typeof(VoyageCargo).GetProperties().Single(property => property.Name.Equals(key,
                System.StringComparison.OrdinalIgnoreCase)).SetValue(session.Cargo, amount);

        private static string SnapshotRareInvestment(CoreVoyageSession session) =>
            session.ProfileGold + "|" + session.PortWarehouse.Serialize() + "|" + string.Join(",",
                CoreWarehouseItemCatalog.All.Select(item => item.Key + "=" + session.GetWarehouseCargoAmount(item.Key)));

        private static IEnumerator OpenInvestmentThroughMarket()
        {
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            ClickAcceptanceButton("Market Open Investment"); yield return null;
            Assert.That(GameObject.Find("Port Investment Panel"), Is.Not.Null);
        }

        private static void PrepareInvestmentResources(CoreGameplayDemoController controller, string port)
        {
            PortInvestmentCost first = PortInvestmentCatalog.CostForLevel(port, 1);
            PortInvestmentCost second = PortInvestmentCatalog.CostForLevel(port, 2);
            controller.Session.RestoreProfile(first.Gold + second.Gold + 500, port);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                SetInvestmentCargo(controller.Session.Cargo, item, 7);
            controller.Session.Cargo.Wood = first.Wood + second.Wood + 7;
            controller.Session.Cargo.Iron = first.Iron + second.Iron + 7;
            Assert.That(first.RequiredItem, Is.Not.EqualTo(PortMarketItem.Wood));
            Assert.That(first.RequiredItem, Is.Not.EqualTo(PortMarketItem.Iron));
            Assert.That(first.RequiredItem, Is.EqualTo(second.RequiredItem));
            SetInvestmentCargo(controller.Session.Cargo, first.RequiredItem, first.RequiredQuantity + second.RequiredQuantity + 7);
        }

        private static void AssertInvestmentQuote(string port, PortInvestmentCost cost)
        {
            Assert.That(cost.RequiredItem, Is.EqualTo(PortInvestmentCatalog.RequiredItemFor(port)));
            string requiredName = PortMarketCatalog.Name(cost.RequiredItem);
            string resources = GameObject.Find("Port Investment Resources").GetComponent<Text>().text;
            string hint = GameObject.Find("Port Investment Ledger Hint").GetComponent<Text>().text;
            string tier = GameObject.Find("Port Investment Tier Label 1").GetComponent<Text>().text;
            string button = ActiveButton("Confirm Port Investment").GetComponentInChildren<Text>().text;
            Assert.That(GameObject.Find("Port Investment Port").GetComponent<Text>().text, Does.Contain(port));
            foreach (string name in new[] { "金币", "木材", "铁矿", requiredName })
            {
                Assert.That(resources, Does.Contain(name));
                Assert.That(hint, Does.Contain(name));
            }
            foreach (string requirement in new[] { cost.Gold + " 金币", cost.Wood + " 木材",
                cost.Iron + " 铁矿", cost.RequiredQuantity + " " + requiredName })
            {
                Assert.That(tier, Does.Contain(requirement));
                Assert.That(button, Does.Contain(requirement));
            }
            if (cost.RequiredItem != PortMarketItem.Cloth)
                Assert.That(resources + hint + tier + button, Does.Not.Contain("帆布"),
                    "A port with another specialty must not retain the old universal-cloth requirement.");
        }

        private static void AssertInvestmentDeduction(CoreVoyageSession session, InvestmentResourceSnapshot before, PortInvestmentCost cost)
        {
            Assert.That(session.ProfileGold, Is.EqualTo(before.gold - cost.Gold));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                int spent = item == PortMarketItem.Wood ? cost.Wood : item == PortMarketItem.Iron ? cost.Iron :
                    item == cost.RequiredItem ? cost.RequiredQuantity : 0;
                Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), Is.EqualTo(before.cargo[item] - spent),
                    "Exactly the quoted four resources must be charged; " + item);
            }
        }

        private static void AssertInvestmentResourcesEqual(CoreVoyageSession session, InvestmentResourceSnapshot expected)
        {
            Assert.That(session.ProfileGold, Is.EqualTo(expected.gold));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), Is.EqualTo(expected.cargo[item]), item.ToString());
        }

        private static void AssertInvestmentLevels(HashSet<string> completed)
        {
            foreach (string port in CoreVoyageSession.DestinationPorts)
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port), 0),
                    Is.EqualTo(completed.Contains(port) ? 1 : 0), "Investment level must only change in the quoted port: " + port);
        }

        private sealed class InvestmentResourceSnapshot
        { public decimal gold; public Dictionary<PortMarketItem, int> cargo; }

        private static InvestmentResourceSnapshot SnapshotInvestmentResources(CoreVoyageSession session) =>
            new InvestmentResourceSnapshot { gold = session.ProfileGold,
                cargo = PortMarketCatalog.AllItems.ToDictionary(item => item, item => session.GetMarketCargoAmount(PortMarketCatalog.Key(item))) };

        private static void SetInvestmentCargo(VoyageCargo cargo, PortMarketItem item, int amount)
        {
            switch (item)
            {
                case PortMarketItem.Water: cargo.Water = amount; break;
                case PortMarketItem.Food: cargo.Food = amount; break;
                case PortMarketItem.Beer: cargo.Beer = amount; break;
                case PortMarketItem.Medicine: cargo.Medicine = amount; break;
                case PortMarketItem.Wood: cargo.Wood = amount; break;
                case PortMarketItem.Iron: cargo.Iron = amount; break;
                case PortMarketItem.Cloth: cargo.Cloth = amount; break;
                case PortMarketItem.Powder: cargo.Powder = amount; break;
                case PortMarketItem.Cannonballs: cargo.Cannonballs = amount; break;
                default: Assert.Fail("Unsupported market resource: " + item); break;
            }
        }
    }
}
