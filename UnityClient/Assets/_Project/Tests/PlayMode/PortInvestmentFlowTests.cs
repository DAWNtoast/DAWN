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
                AssertInvestmentComparison(1);
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
                string requiredKey = PortMarketCatalog.Key(cost.RequiredItem);
                string other = CoreVoyageSession.DestinationPorts.First(candidate => candidate != port);
                SetInvestmentCargo(controller.Session.Cargo, cost.RequiredItem, cost.RequiredQuantity - 2);
                Assert.That(controller.Session.PortWarehouse.Restore("v2;" + port + "," + requiredKey + ",1;" +
                    other + "," + requiredKey + ",999;" + other + ",wood,300;" + other + ",iron,500"), Is.True);
                yield return OpenInvestmentThroughMarket();
                InvestmentResourceSnapshot before = SnapshotInvestmentResources(controller.Session);
                string warehouseBefore = controller.Session.PortWarehouse.Serialize();
                Button confirm = ActiveButton("Confirm Port Investment");
                Assert.That(confirm.IsInteractable(), Is.False);
                Assert.That(controller.Session.GetPortInvestmentMaterialAmount(PortMarketCatalog.Key(cost.RequiredItem)),
                    Is.EqualTo((long)cost.RequiredQuantity - 1), "The missing resource must use this port's actual specialty.");
                AssertInvestmentQuote(port, cost);
                AssertInvestmentRequirement(requiredKey, PortMarketCatalog.Name(cost.RequiredItem),
                    (cost.RequiredQuantity - 1) + "/" + cost.RequiredQuantity);
                // Defensive event test: disabling a button alone must not permit
                // an old/programmatic callback to partially deduct common costs.
                confirm.onClick.Invoke(); yield return null;
                AssertInvestmentResourcesEqual(controller.Session, before);
                Assert.That(controller.Session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore),
                    "A foreign warehouse must neither inflate the owned quantity nor pay the local shortfall.");
                AssertInvestmentLevels(new HashSet<string>());
                ClickAcceptanceButton("Return To Port Hub"); yield return null;
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortInvestment_OwnedRequirementsCombineLocalAndCarriedStockRefreshAndRemainComplete()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            const string port = "断锚港", other = "白贝港";
            session.RestoreProfile(1600.4321m, port);
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
                SetInvestmentWarehouseCargo(session, item.Key, 0);
            session.Cargo.Wood = 10;
            session.Cargo.Iron = 2;
            session.Cargo.Medicine = 2;
            Assert.That(session.PortWarehouse.Restore("v2;" + port + ",wood,20;" + port + ",iron,3;" + port +
                ",medicine,3;" + other + ",wood,300;" + other + ",iron,500;" + other + ",medicine,999"), Is.True);
            yield return OpenInvestmentThroughMarket();
            AssertInvestmentComparison(0);
            AssertInvestmentRequirement("gold", "金币", "1600.4321/600");
            AssertInvestmentRequirement("wood", "木材", "30/30");
            AssertInvestmentRequirement("iron", "铁矿", "5/5");
            AssertInvestmentRequirement("medicine", "药品", "5/6");
            Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.False);
            NavalBattleVisualCapture.Capture(controller, "PortInvestment_OwnedShortfall.png",
                "数量展示隔离夹具：本港仓库药品3 + 船上药品2 = 5/6；其他港库存999不参与", "Port Investment Panel");

            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            session.Cargo.Medicine = 3;
            yield return OpenInvestmentThroughMarket();
            AssertInvestmentRequirement("medicine", "药品", "6/6");
            Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.True);
            ClickAcceptanceButton("Confirm Port Investment"); yield return null;
            AssertInvestmentComparison(1);
            AssertInvestmentRequirement("gold", "金币", "1000.4321/1000");
            AssertInvestmentRequirement("wood", "木材", "0/50");
            AssertInvestmentRequirement("iron", "铁矿", "0/12");
            AssertInvestmentRequirement("medicine", "药品", "0/10");
            Assert.That(session.PortWarehouse.GetAmount(other, "medicine"), Is.EqualTo(999));

            // Legacy stock can exceed an int after adding carried and stored
            // amounts. Keep the exact numerator visible without wrapping it.
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            session.RestoreProfile(2147483646.4321m, port);
            session.Cargo.Wood = 3;
            session.Cargo.Iron = 4;
            session.Cargo.Medicine = 2;
            Assert.That(session.PortWarehouse.Restore("v2;" + port + ",wood,2147483647;" + port +
                ",iron,2147483647;" + port + ",medicine,2147483647"), Is.True);
            yield return OpenInvestmentThroughMarket();
            AssertInvestmentComparison(1);
            AssertInvestmentRequirement("gold", "金币", "2147483646.4321/1000");
            AssertInvestmentRequirement("wood", "木材", "2147483650/50");
            AssertInvestmentRequirement("iron", "铁矿", "2147483651/12");
            AssertInvestmentRequirement("medicine", "药品", "2147483649/10");
            foreach (string key in new[] { "gold", "wood", "iron", "medicine" })
                AssertInvestmentQuantityComplete(key);
            NavalBattleVisualCapture.Capture(controller, "PortInvestment_OwnedLargeStock.png",
                "大额旧存档隔离夹具：四位金币小数及超过int的本港加随船材料量完整显示", "Port Investment Panel");
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
                AssertInvestmentComparison(target - 1);
                foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                {
                    Assert.That(controller.Session.GetPortInvestmentMaterialAmount(rare.Key), Is.EqualTo((long)rare.Quantity + 1));
                    AssertInvestmentRequirement(rare.Key, CoreWarehouseItemCatalog.Find(rare.Key).Name,
                        (rare.Quantity + 1) + "/" + rare.Quantity);
                }
                AssertInvestmentLedgerGeometry(4 + cost.RareMaterials.Count);
                Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.True);
                Button.ButtonClickedEvent stale = ActiveButton("Confirm Port Investment").onClick;
                decimal goldBefore = controller.Session.ProfileGold;
                string other = CoreVoyageSession.DestinationPorts.First(candidate => candidate != port);
                if (target == 5 || port == "断锚港")
                    NavalBattleVisualCapture.Capture(controller, "PortInvestment_Rare_" + port + "_Lv" + target + ".png",
                        "高阶投资隔离UI夹具：全部基础与珍稀报价；不代表自然赚取资源", "Port Investment Panel");

                ClickAcceptanceButton("Confirm Port Investment"); yield return null;
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port)), Is.EqualTo(target));
                AssertInvestmentComparison(target);
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
                Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(3));
                Assert.That(controller.Session.Cargo.Iron, Is.EqualTo(4));
                Assert.That(controller.Session.GetMarketCargoAmount(PortMarketCatalog.Key(cost.RequiredItem)), Is.EqualTo(2),
                    "Displayed totals include carried stock even when the local warehouse alone pays the quoted common cost.");
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
                AssertInvestmentRequirement(missingKey, CoreWarehouseItemCatalog.Find(missingKey).Name, "0/1");
                AssertInvestmentComparison(4);
                string before = SnapshotRareInvestment(controller.Session);
                confirm.onClick.Invoke(); yield return null;
                Assert.That(SnapshotRareInvestment(controller.Session), Is.EqualTo(before));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port)), Is.EqualTo(4));
                if (port == "白贝港")
                    NavalBattleVisualCapture.Capture(controller, "PortInvestment_Rare_Shortfall.png",
                        "其他港罗盘库存不能替本港投资付款，真实报价与整单拒绝", "Port Investment Panel");
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
            AssertInvestmentComparison(PortInvestmentCatalog.MaxLevel);
            AssertInvestmentLedgerGeometry(0);
            NavalBattleVisualCapture.Capture(controller, "PortInvestment_Maximum.png",
                "既有满级存档隔离夹具：不显示材料报价，也不会追扣新增珍稀材料", "Port Investment Panel");
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
            controller.Session.Cargo.Wood = 3;
            controller.Session.Cargo.Iron = 4;
            SetInvestmentCargo(controller.Session.Cargo, cost.RequiredItem, 2);
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
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            ClickAcceptanceButton("Port Hotspot Maintenance"); yield return null;
            Assert.That(GameObject.Find("Port Investment Panel"), Is.Not.Null);
            yield return WaitForInvestmentAndCrewScreenToSettle("Port Investment Panel");
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
            AssertInvestmentComparison(0);
            if (cost.RequiredItem != PortMarketItem.Cloth)
                Assert.That(GameObject.Find("Port Investment Requirement cloth"), Is.Null,
                    "A port with another specialty must not retain the old universal-cloth requirement.");
        }

        private static void AssertInvestmentRequirement(string key, string name, string quantity)
        {
            GameObject row = GameObject.Find("Port Investment Requirement " + key);
            Assert.That(row, Is.Not.Null, key);
            Assert.That(row.transform.parent, Is.EqualTo(GameObject.Find("Port Investment Ledger").transform), key);
            Assert.That(InvestmentComparisonText("Requirement Name " + key), Is.EqualTo(name), key);
            Assert.That(InvestmentComparisonText("Requirement Quantity " + key), Is.EqualTo(quantity), key);
            Transform iconTransform = row.transform.Find("Port Investment Requirement Icon " + key);
            Assert.That(iconTransform, Is.Not.Null, key + " icon");
            RawImage icon = iconTransform.GetComponent<RawImage>();
            Assert.That(icon, Is.Not.Null, key + " icon");
            ItemIconArt art = key == "gold" ? ItemIconCatalog.ForSpecial(key) : ItemIconCatalog.ForCargo(key);
            Assert.That(art.Texture, Is.Not.Null, key + " catalog art");
            Assert.That(icon.texture, Is.SameAs(art.Texture), key + " item art");
            Assert.That(icon.uvRect, Is.EqualTo(art.Uv), key + " atlas cell");
            Assert.That(icon.raycastTarget, Is.False, key + " icon");
            Assert.That(icon.gameObject.activeInHierarchy && icon.enabled, Is.True, key + " icon");
            Assert.That(icon.color.a, Is.GreaterThan(0f), key + " icon");
            string[] amounts = quantity.Split('/');
            decimal owned = decimal.Parse(amounts[0], System.Globalization.CultureInfo.InvariantCulture);
            decimal needed = decimal.Parse(amounts[1], System.Globalization.CultureInfo.InvariantCulture);
            GameObject shortfall = GameObject.Find("Port Investment Requirement Shortfall " + key);
            if (owned < needed)
            {
                Assert.That(shortfall, Is.Not.Null, key + " identifies the missing amount");
                Assert.That(shortfall.GetComponent<Text>().text,
                    Is.EqualTo("缺 " + (needed - owned).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)));
                Text amountLabel = GameObject.Find("Port Investment Requirement Quantity " + key).GetComponent<Text>();
                Assert.That(amountLabel.color.r, Is.GreaterThan(amountLabel.color.g * 2f), key + " shortfall has visible red ink");
            }
            else Assert.That(shortfall, Is.Null, key + " affordable row must not retain an old shortfall");
            AssertInvestmentQuantityComplete(key);
        }

        private static void AssertInvestmentLedgerGeometry(int expectedRows)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform ledger = GameObject.Find("Port Investment Ledger").GetComponent<RectTransform>();
            RectTransform[] rows = ledger.GetComponentsInChildren<RectTransform>()
                .Where(item => item.parent == ledger && item.name.StartsWith("Port Investment Requirement "))
                .ToArray();
            Assert.That(rows.Length, Is.EqualTo(expectedRows), "Every quoted material gets a visible row.");
            RectTransform confirm = ActiveButton("Confirm Port Investment").GetComponent<RectTransform>();
            Rect confirmBounds = InvestmentBoundsIn(ledger, confirm);
            var rowBounds = new List<Rect>();
            foreach (RectTransform row in rows)
            {
                Rect bounds = InvestmentBoundsIn(ledger, row);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(ledger.rect.xMin - .5f), row.name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(ledger.rect.xMax + .5f), row.name);
                Assert.That(bounds.yMin, Is.GreaterThan(confirmBounds.yMax + 4f), row.name + " clears confirmation button");
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(ledger.rect.yMax), row.name);
                foreach (Rect earlier in rowBounds)
                    Assert.That(bounds.Overlaps(earlier), Is.False, row.name + " must not cover another quoted material");
                rowBounds.Add(bounds);
            }
        }

        private static Rect InvestmentBoundsIn(RectTransform parent, RectTransform item)
        {
            var corners = new Vector3[4]; item.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y),
                local.Max(point => point.x), local.Max(point => point.y));
        }

        private static void AssertInvestmentNextRequirements(int level)
        {
            GameObject ledger = GameObject.Find("Port Investment Ledger");
            Assert.That(ledger, Is.Not.Null);
            Transform[] elements = ledger.GetComponentsInChildren<Transform>(true);
            Assert.That(elements.Any(item => item.name.StartsWith("Port Investment Tier ")), Is.False,
                "The ledger must not show the costs of other investment levels.");
            string[] visibleKeys = elements.Where(item => item.parent == ledger.transform &&
                item.name.StartsWith("Port Investment Requirement ") && item.GetComponent<Image>() != null)
                .Select(item => item.name.Substring("Port Investment Requirement ".Length)).ToArray();
            if (level == PortInvestmentCatalog.MaxLevel)
            {
                Assert.That(visibleKeys, Is.Empty, "A completed port must not quote any materials.");
                Assert.That(InvestmentComparisonText("Ledger Title"), Is.EqualTo("港口建设已完成"));
                Assert.That(InvestmentComparisonText("Completed"), Is.EqualTo("已达到最高投资等级"));
                Assert.That(ActiveButton("Confirm Port Investment").GetComponentInChildren<Text>().text,
                    Is.EqualTo("港口已达到最高等级"));
                return;
            }
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(controller.Session.CurrentPort, level + 1);
            var expectedKeys = new List<string> { "gold", "wood", "iron", PortMarketCatalog.Key(cost.RequiredItem) };
            expectedKeys.AddRange(cost.RareMaterials.Select(item => item.Key));
            CollectionAssert.AreEquivalent(expectedKeys, visibleKeys,
                "Only the resources required for the immediately next level may appear.");
            AssertInvestmentRequirement("gold", "金币", InvestmentOwnedRequirement(controller.Session, "gold", cost.Gold));
            AssertInvestmentRequirement("wood", "木材", InvestmentOwnedRequirement(controller.Session, "wood", cost.Wood));
            AssertInvestmentRequirement("iron", "铁矿", InvestmentOwnedRequirement(controller.Session, "iron", cost.Iron));
            AssertInvestmentRequirement(PortMarketCatalog.Key(cost.RequiredItem),
                PortMarketCatalog.Name(cost.RequiredItem), InvestmentOwnedRequirement(controller.Session,
                    PortMarketCatalog.Key(cost.RequiredItem), cost.RequiredQuantity));
            foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                AssertInvestmentRequirement(rare.Key, CoreWarehouseItemCatalog.Find(rare.Key).Name,
                    InvestmentOwnedRequirement(controller.Session, rare.Key, rare.Quantity));
            Assert.That(InvestmentComparisonText("Quantity Header"), Is.EqualTo("拥有 / 需求"));
            Assert.That(InvestmentComparisonText("Ledger Title"), Is.EqualTo("升级至 Lv." + (level + 1) + " 所需材料"));
            CoreVoyageSession session = controller.Session;
            bool enough = session.ProfileGold >= cost.Gold &&
                session.GetPortInvestmentMaterialAmount("wood") >= cost.Wood &&
                session.GetPortInvestmentMaterialAmount("iron") >= cost.Iron &&
                session.GetPortInvestmentMaterialAmount(PortMarketCatalog.Key(cost.RequiredItem)) >= cost.RequiredQuantity &&
                cost.RareMaterials.All(item => session.GetPortInvestmentMaterialAmount(item.Key) >= item.Quantity);
            Text confirmLabel = ActiveButton("Confirm Port Investment").GetComponentInChildren<Text>();
            Assert.That(confirmLabel.text, Is.EqualTo(enough ? "升级至 Lv." + (level + 1) : "材料不足"));
            if (!enough)
                Assert.That(confirmLabel.color, Is.EqualTo((Color)new Color32(240, 107, 88, 255)));
            Assert.That(GameObject.Find("Port Investment Completed"), Is.Null);
        }

        private static string InvestmentOwnedRequirement(CoreVoyageSession session, string key, int required)
        {
            string owned = key == "gold"
                ? session.ProfileGold.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
                : ((long)session.GetWarehouseCargoAmount(key) + session.PortWarehouse.GetAmount(session.CurrentPort, key))
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
            return owned + "/" + required.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void AssertInvestmentQuantityComplete(string key)
        {
            Text quantity = GameObject.Find("Port Investment Requirement Quantity " + key).GetComponent<Text>();
            Assert.That(quantity.preferredWidth, Is.LessThanOrEqualTo(quantity.rectTransform.rect.width + .5f),
                key + " the exact balance must fit the column, including when overflow is enabled");
            TextGenerationSettings visibleSettings = quantity.GetGenerationSettings(quantity.rectTransform.rect.size);
            using (var visible = new TextGenerator())
            using (var complete = new TextGenerator())
            {
                Assert.That(visible.Populate(quantity.text, visibleSettings), Is.True, key + " visible quantity");
                TextGenerationSettings completeSettings = visibleSettings;
                completeSettings.horizontalOverflow = HorizontalWrapMode.Overflow;
                completeSettings.verticalOverflow = VerticalWrapMode.Overflow;
                completeSettings.resizeTextForBestFit = false;
                completeSettings.fontSize = quantity.resizeTextForBestFit ? visible.fontSizeUsedForBestFit : quantity.fontSize;
                Assert.That(complete.Populate(quantity.text, completeSettings), Is.True, key + " complete quantity");
                Assert.That(visible.characterCount, Is.EqualTo(complete.characterCount), key + " no clipped characters");
                Assert.That(visible.vertexCount, Is.EqualTo(complete.vertexCount), key + " no clipped glyphs");
                Assert.That(visible.lineCount, Is.EqualTo(1), key + " stays on one line");
            }
        }

        private static void AssertInvestmentComparison(int level)
        {
            // Independent values verify both the visible forecast and the actual
            // catalog benefits after investment, including the nonuniform quota steps.
            decimal[] quality = { 13.0m, 14.5m, 16.0m, 17.5m, 19.0m, 20.5m };
            int[] components = { 6, 8, 10, 12, 14, 16 };
            int[] quotas = { 200, 300, 450, 650, 900, 1200 };
            int next = System.Math.Min(level + 1, PortInvestmentCatalog.MaxLevel);
            Assert.That(PortInvestmentCatalog.HighQualityChance(level) * 100f,
                Is.EqualTo((float)quality[level]).Within(.001f));
            Assert.That(PortComponentShopCatalog.StockCount(level), Is.EqualTo(components[level]));
            Assert.That(PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Wood, level), Is.EqualTo(quotas[level]));
            Assert.That(InvestmentComparisonText("Current Level"), Is.EqualTo("Lv." + level));
            Assert.That(InvestmentComparisonText("Next Level"), Is.EqualTo("Lv." + next));
            GameObject arrowObject = GameObject.Find("Port Investment Comparison Arrow");
            Assert.That(arrowObject, Is.Not.Null);
            RectTransform arrow = arrowObject.GetComponent<RectTransform>();
            Assert.That(arrow, Is.Not.Null);
            Assert.That(arrow.GetComponentsInChildren<Image>(true).Count(image => image.transform != arrow), Is.EqualTo(3));
            Assert.That(arrow.GetComponent<Image>().raycastTarget, Is.False);
            foreach (string name in new[] { "Port Investment Arrow Shaft", "Port Investment Arrow Upper", "Port Investment Arrow Lower" })
            {
                Transform child = arrow.Find(name);
                Assert.That(child, Is.Not.Null, name);
                Image segment = child.GetComponent<Image>();
                Assert.That(segment, Is.Not.Null, name);
                Assert.That(segment.gameObject.activeInHierarchy && segment.enabled, Is.True, name);
                Assert.That(segment.color.a, Is.GreaterThan(0f), name);
                Assert.That(segment.sprite, Is.Null, name);
                Assert.That(segment.raycastTarget, Is.False, name);
            }
            Assert.That(InvestmentComparisonText("Current Quality Label"), Is.EqualTo("新部件稀有及以上"));
            Assert.That(InvestmentComparisonText("Current Component Label"), Is.EqualTo("每月部件数量"));
            Assert.That(InvestmentComparisonText("Current Quota Label"), Is.EqualTo("普通货物月额度"));
            Assert.That(InvestmentComparisonText("Current Quality Value"),
                Is.EqualTo(quality[level].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"));
            Assert.That(InvestmentComparisonText("Current Component Value"), Is.EqualTo(components[level] + " 件"));
            Assert.That(InvestmentComparisonText("Current Quota Value"), Is.EqualTo(quotas[level].ToString()));
            Assert.That(InvestmentComparisonText("Next Quality Value"),
                Is.EqualTo(quality[next].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"));
            Assert.That(InvestmentComparisonText("Next Component Value"), Is.EqualTo(components[next] + " 件"));
            Assert.That(InvestmentComparisonText("Next Quota Value"), Is.EqualTo(quotas[next].ToString()));
            Assert.That(InvestmentComparisonText("Quality Increase"),
                Is.EqualTo("+" + (quality[next] - quality[level]).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " 个百分点"));
            Assert.That(InvestmentComparisonText("Component Increase"), Is.EqualTo("+" + (components[next] - components[level]) + " 件"));
            Assert.That(InvestmentComparisonText("Quota Increase"), Is.EqualTo("+" + (quotas[next] - quotas[level])));
            AssertInvestmentNextRequirements(level);
        }

        private static string InvestmentComparisonText(string name)
        {
            GameObject label = GameObject.Find("Port Investment " + name);
            Assert.That(label, Is.Not.Null, name);
            return label.GetComponent<Text>().text;
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
