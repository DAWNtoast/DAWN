using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Real runtime buttons on an isolated canvas, without gameplay Awake/audio.
    /// Each preference touched by a command is restored, including key absence.
    /// </summary>
    public sealed class GameMasterRuntimeTests
    {
        private GameObject host;
        private GameObject canvasObject;
        private GameObject eventObject;
        private EventSystem previousEvents;
        private EventSystem[] suspendedEvents;
        private GameObject previousSelection;
        private CoreGameplayDemoController controller;
        private CoreVoyageSession session;
        private RectTransform page;
        private Button underlay;
        private IntPreference[] changedInts;
        private IntPreference[] protectedInts;
        private bool scopeExisted;
        private string scopeBefore;
        private bool decimalGoldExisted;
        private string decimalGoldBefore;
        private ComponentArtCatalog componentArt;
        private PortComponentShopStockItem oldOffer;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.True);
            decimalGoldExisted = PlayerPrefs.HasKey(ProfileGoldPersistence.DecimalKey);
            decimalGoldBefore = PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey, string.Empty);
            PlayerPrefs.DeleteKey(ProfileGoldPersistence.DecimalKey);
            componentArt = new ComponentArtCatalog();
            oldOffer = PortComponentShopCatalog.BuildStock(componentArt, "灰鸥港", CoreVoyageSession.GameClockOrigin)[0];
            changedInts = new[]
            {
                IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey),
                IntPreference.Read(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港")),
                IntPreference.Read(CoreGameplayDemoController.ComponentShopPurchaseKey(oldOffer.Id)),
            };
            protectedInts = new[]
            {
                IntPreference.Read(CoreGameplayDemoController.GameMasterShopRefreshKey("白贝港")),
                IntPreference.Read(CoreGameplayDemoController.CrewRecruitmentKey(0)),
                IntPreference.Read(CoreGameplayDemoController.CargoKey("Gold")),
                IntPreference.Read(CoreGameplayDemoController.LegacyWitchRecruitedKey),
            };
            scopeExisted = PlayerPrefs.HasKey(CoreGameplayDemoController.GameMasterShopScopeKey);
            scopeBefore = PlayerPrefs.GetString(CoreGameplayDemoController.GameMasterShopScopeKey, string.Empty);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GameMasterShopScopeKey);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港"), 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(oldOffer.Id), 1);

            host = new GameObject("Inactive GM Test Controller");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.Null);
            session = new CoreVoyageSession();
            session.RestoreProfile(1234, "灰鸥港");
            Write("session", session);
            Write("interfaceFont", Invoke("CreateInterfaceFont"));
            Invoke("BuildCanvas", "Isolated Game Master Canvas");
            page = Read<RectTransform>("contentRoot");
            canvasObject = page.parent.gameObject;
            canvasObject.transform.SetParent(null, false);
            var underlayObject = new GameObject("Underlying GM Draft", typeof(RectTransform), typeof(Image), typeof(Button));
            underlayObject.transform.SetParent(page, false);
            underlay = underlayObject.GetComponent<Button>();
            underlay.targetGraphic = underlayObject.GetComponent<Image>();
            previousEvents = EventSystem.current;
            previousSelection = previousEvents == null ? null : previousEvents.currentSelectedGameObject;
            suspendedEvents = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Where(events => events.enabled).ToArray();
            foreach (EventSystem events in suspendedEvents) events.enabled = false;
            eventObject = new GameObject("Isolated GM EventSystem", typeof(EventSystem));
            EventSystem.current = eventObject.GetComponent<EventSystem>();
            EventSystem.current.SetSelectedGameObject(underlayObject);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (controller != null) Invoke("CloseGameMaster");
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (eventObject != null) eventObject.GetComponent<EventSystem>().enabled = false;
            if (suspendedEvents != null) foreach (EventSystem events in suspendedEvents) if (events != null) events.enabled = true;
            if (previousEvents != null)
            {
                EventSystem.current = previousEvents;
                if (previousSelection != null && previousSelection.activeInHierarchy)
                    previousEvents.SetSelectedGameObject(previousSelection);
            }
            if (canvasObject != null) Object.Destroy(canvasObject);
            if (eventObject != null) Object.Destroy(eventObject);
            if (host != null) Object.Destroy(host);
            foreach (IntPreference preference in changedInts) preference.Restore();
            if (decimalGoldExisted) PlayerPrefs.SetString(ProfileGoldPersistence.DecimalKey, decimalGoldBefore);
            else PlayerPrefs.DeleteKey(ProfileGoldPersistence.DecimalKey);
            if (scopeExisted) PlayerPrefs.SetString(CoreGameplayDemoController.GameMasterShopScopeKey, scopeBefore);
            else PlayerPrefs.DeleteKey(CoreGameplayDemoController.GameMasterShopScopeKey);
            PlayerPrefs.Save();
            yield return null;
            foreach (IntPreference preference in protectedInts) preference.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator OpeningAndSameFrameReopening_StayLockedAndPreserveDrafts()
        {
            VoyageSupplyOrder order = Read<VoyageSupplyOrder>("pendingSupplyOrder");
            order.Water = 70;
            int[] children = page.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray();
            IntPreference goldBefore = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
            Invoke("OpenGameMaster");
            Assert.That(controller.IsGameMasterOpen, Is.True);
            Assert.That(Read<Toggle>("gameMasterUnlockToggle").isOn, Is.False);
            Assert.That(Read<Button>("gameMasterSetGoldButton").interactable, Is.False);
            Assert.That(Read<Button>("gameMasterRefreshShopButton").interactable, Is.False);
            Assert.That(Read<Button>("gameMasterRefreshMarketButton").interactable, Is.False);
            Read<Button>("gameMasterSetGoldButton").onClick.Invoke();
            Read<Button>("gameMasterRefreshShopButton").onClick.Invoke();
            Read<Button>("gameMasterRefreshMarketButton").onClick.Invoke();
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港")), Is.Zero);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GameMasterShopScopeKey), Is.False);
            goldBefore.AssertUnchanged();
            CanvasGroup group = page.GetComponent<CanvasGroup>();
            Assert.That(underlay.IsInteractable(), Is.False);
            Read<Toggle>("gameMasterUnlockToggle").isOn = true;
            Invoke("CloseGameMaster");
            Invoke("OpenGameMaster");
            Assert.That(Read<Toggle>("gameMasterUnlockToggle").isOn, Is.False);
            yield return null;
            Assert.That(page.GetComponent<CanvasGroup>(), Is.SameAs(group));
            Assert.That(page.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
            Assert.That(underlay.IsInteractable(), Is.False);
            Assert.That(canvasObject.GetComponentsInChildren<Transform>().Count(item => item.name == "Game Master Overlay"), Is.EqualTo(1));
            NavalBattleVisualCapture.Capture(controller, "40_Game_Master_Locked.png",
                "实际 GM 面板：默认锁定、精确金币范围、部件商店刷新与立即存档提示", "Game Master Sheet");
            Invoke("CloseGameMaster");
            Assert.That(underlay.IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(underlay.gameObject));
            CollectionAssert.AreEqual(children, page.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray());
            Assert.That(order.Water, Is.EqualTo(70));
            goldBefore.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator SettingGold_SavesExactZeroAndMaximumAndRejectsDuplicateCallbacks()
        {
            OpenUnlocked();
            InputField input = Read<InputField>("gameMasterGoldInput");
            Button set = Read<Button>("gameMasterSetGoldButton");
            int cargoGold = session.Cargo.Gold;
            input.text = "0";
            set.onClick.Invoke();
            Assert.That(session.ProfileGold, Is.Zero);
            Assert.That(ProfileGoldPersistence.Load(), Is.Zero);
            input.text = "1000000000";
            set.onClick.Invoke();
            Assert.That(session.ProfileGold, Is.Zero, "A duplicate callback in the same frame must not perform another command.");
            Assert.That(set.interactable, Is.False);
            yield return new WaitForSecondsRealtime(0.45f);
            set.onClick.Invoke();
            Assert.That(session.ProfileGold, Is.EqualTo(CoreGameMasterTools.MaximumProfileGold));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(CoreGameMasterTools.MaximumProfileGold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(cargoGold));
        }

        [TestCase("-1")]
        [TestCase("+1")]
        [TestCase("1.00001")]
        [TestCase(" 50")]
        [TestCase("1,000")]
        [TestCase("1000000001")]
        [TestCase("99999999999999999999999")]
        public void InvalidInput_DisablesAndRejectsActionWithoutChangingAmount(string input)
        {
            IntPreference before = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
            OpenUnlocked();
            Read<InputField>("gameMasterGoldInput").text = input;
            Button set = Read<Button>("gameMasterSetGoldButton");
            Assert.That(set.interactable, Is.False);
            set.onClick.Invoke();
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            before.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator RefreshShop_PersistsNewBatchAndRetainsPurchasesCrewAndOtherPorts()
        {
            OpenUnlocked();
            Button refresh = Read<Button>("gameMasterRefreshShopButton");
            refresh.onClick.Invoke();
            refresh.onClick.Invoke();
            string key = CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港");
            string scope = PlayerPrefs.GetString(CoreGameplayDemoController.GameMasterShopScopeKey);
            Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1));
            Assert.That(scope.Length, Is.EqualTo(32));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(oldOffer.Id)), Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            var refreshed = PortComponentShopCatalog.BuildStock(componentArt, session.CurrentPort, CoreVoyageSession.GameClockOrigin, 1, scope);
            Assert.That(refreshed.Any(item => item.Id == oldOffer.Id), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(oldOffer, session.CurrentPort, CoreVoyageSession.GameClockOrigin, 1, scope), Is.False);
            Invoke("CloseGameMaster");
            Invoke("OpenGameMaster");
            Assert.That((int)InvokeStatic("ComponentShopRefreshRevision", "灰鸥港"), Is.EqualTo(1));
            Assert.That((string)InvokeStatic("ComponentShopRefreshScope"), Is.EqualTo(scope));
            yield return null;
            foreach (IntPreference preference in protectedInts) preference.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator RefreshMarket_RealUnlockAndButtonResetOnlyThisPortsMonthLedgerWithSharedCooldown()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.AddMonths(1).Date.AddHours(12);
            const string port = "灰鸥港";
            var receipts = new List<IntPreference>();
            var expectedReceipts = new Dictionary<string, int>();
            var cargoPreferences = new List<IntPreference>();
            string warehouseKey = CoreGameplayDemoController.PortWarehouseCargoKey;
            bool warehouseExisted = PlayerPrefs.HasKey(warehouseKey);
            string warehouseBefore = PlayerPrefs.GetString(warehouseKey, string.Empty);
            try
            {
                Write("marketTimeProvider", (Func<DateTime>)(() => now));
                // Every harbor and previous month have distinct fixture receipts.
                // Preserve presence as well as values, even if an assertion fails.
                for (int harbor = 0; harbor < CoreVoyageSession.DestinationPorts.Count; harbor++)
                foreach (int offset in new[] { -1, 0 })
                for (int item = 0; item < PortMarketCatalog.AllItems.Count; item++)
                {
                    string key = CoreGameplayDemoController.MarketPurchaseKey(CoreVoyageSession.DestinationPorts[harbor],
                        now.AddMonths(offset), PortMarketCatalog.AllItems[item]);
                    receipts.Add(IntPreference.Read(key));
                    int purchased = 11 + harbor * 20 + item + (offset < 0 ? 200 : 0);
                    expectedReceipts[key] = purchased;
                    PlayerPrefs.SetInt(key, purchased);
                }
                session.RestoreProfile(1234.5678m, port);
                ProfileGoldPersistence.Save(session.ProfileGold);
                PropertyInfo[] cargoFields = typeof(VoyageCargo).GetProperties()
                    .Where(property => property.PropertyType == typeof(int) && property.CanWrite)
                    .OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
                for (int index = 0; index < cargoFields.Length; index++)
                {
                    PropertyInfo property = cargoFields[index];
                    property.SetValue(session.Cargo, index + 1);
                    string key = CoreGameplayDemoController.CargoKey(property.Name);
                    cargoPreferences.Add(IntPreference.Read(key));
                    PlayerPrefs.SetInt(key, index + 1);
                }
                Assert.That(session.PortWarehouse.Restore("v1;灰鸥港,water,83;灰鸥港,wood,9;白贝港,food,77"), Is.True);
                string warehouse = session.PortWarehouse.Serialize();
                PlayerPrefs.SetString(warehouseKey, warehouse);
                PlayerPrefs.Save();
                int[] cargo = cargoFields.Select(property => (int)property.GetValue(session.Cargo)).ToArray();
                string treasure = JsonUtility.ToJson(session.CaptureTreasureProgress());
                string decimalBalance = PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey);
                IntPreference wholeBalance = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
                var prices = PortMarketCatalog.AllItems.Select(item => new
                {
                    Item = item, Buy = PortMarketCatalog.BuyUnitPrice(port, item, now),
                    Sell = PortMarketCatalog.SellUnitPrice(port, item, now),
                    Stock = PortMarketCatalog.DayState(port, item, now).Stock,
                }).ToArray();

                Invoke("OpenGameMaster");
                yield return null;
                Toggle unlock = Read<Toggle>("gameMasterUnlockToggle");
                Button market = Read<Button>("gameMasterRefreshMarketButton");
                Button.ButtonClickedEvent callback = market.onClick;
                Assert.That(unlock.isOn, Is.False);
                ClickGameMasterGraphic(market);
                callback.Invoke(); // Even a stale callback cannot bypass the lock.
                foreach (var receipt in expectedReceipts)
                    Assert.That(PlayerPrefs.GetInt(receipt.Key), Is.EqualTo(receipt.Value), receipt.Key);
                ClickGameMasterGraphic(unlock);
                Assert.That(unlock.isOn, Is.True);
                Assert.That(market.IsInteractable(), Is.True);
                ClickGameMasterGraphic(market);

                foreach (var receipt in expectedReceipts)
                {
                    bool reset = PortMarketCatalog.AllItems.Any(item => receipt.Key == CoreGameplayDemoController.MarketPurchaseKey(port, now, item));
                    Assert.That(PlayerPrefs.HasKey(receipt.Key), Is.EqualTo(!reset), receipt.Key);
                    Assert.That(PlayerPrefs.GetInt(receipt.Key), Is.EqualTo(reset ? 0 : receipt.Value), receipt.Key);
                }
                Assert.That(market.interactable, Is.False);
                // Reseeding a receipt makes a duplicate deletion observable.
                string waterKey = CoreGameplayDemoController.MarketPurchaseKey(port, now, PortMarketItem.Water);
                PlayerPrefs.SetInt(waterKey, 7);
                callback.Invoke();
                Read<Button>("gameMasterRefreshShopButton").onClick.Invoke();
                Assert.That(PlayerPrefs.GetInt(waterKey), Is.EqualTo(7), "Duplicate market callbacks must respect cooldown.");
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GameMasterShopRefreshKey(port)), Is.Zero,
                    "Market and component-shop refresh share one command cooldown.");
                yield return new WaitForSecondsRealtime(.45f);
                Invoke("RefreshGameMasterControls"); // The isolated controller stays inactive; no LateUpdate is running.
                ClickGameMasterGraphic(market);
                Assert.That(PlayerPrefs.HasKey(waterKey), Is.False);

                Assert.That(session.ProfileGold, Is.EqualTo(1234.5678m));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(1234.5678m));
                Assert.That(PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey), Is.EqualTo(decimalBalance));
                wholeBalance.AssertUnchanged();
                CollectionAssert.AreEqual(cargo, cargoFields.Select(property => (int)property.GetValue(session.Cargo)).ToArray());
                for (int index = 0; index < cargoFields.Length; index++)
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey(cargoFields[index].Name)), Is.EqualTo(index + 1));
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
                Assert.That(PlayerPrefs.GetString(warehouseKey), Is.EqualTo(warehouse));
                Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(treasure));
                Assert.That(session.CurrentPort, Is.EqualTo(port));
                Assert.That((DateTime)Invoke("get_MarketNow"), Is.EqualTo(now));
                foreach (var price in prices)
                {
                    Assert.That(PortMarketCatalog.BuyUnitPrice(port, price.Item, now), Is.EqualTo(price.Buy));
                    Assert.That(PortMarketCatalog.SellUnitPrice(port, price.Item, now), Is.EqualTo(price.Sell));
                    Assert.That(PortMarketCatalog.DayState(port, price.Item, now).Stock, Is.EqualTo(price.Stock));
                    int restored = (int)Invoke("MarketRemaining", port, price.Item, now, 0);
                    Assert.That(restored, Is.EqualTo(Math.Min(price.Stock, PortMarketCatalog.MaxPurchaseQuota(price.Item, 0))));
                }
                NavalBattleVisualCapture.Capture(controller, "41_Game_Master_Market_Refreshed.png",
                    "真实解锁及刷新：只恢复本港本月九类采购额度，精确金币、货舱和分港仓储均未变", "Game Master Sheet");
                Invoke("CloseGameMaster");
                PlayerPrefs.SetInt(waterKey, 9);
                callback.Invoke();
                Assert.That(PlayerPrefs.GetInt(waterKey), Is.EqualTo(9), "A closed GM panel cannot run a stored callback.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                foreach (IntPreference receipt in receipts) receipt.Restore();
                foreach (IntPreference preference in cargoPreferences) preference.Restore();
                if (warehouseExisted) PlayerPrefs.SetString(warehouseKey, warehouseBefore);
                else PlayerPrefs.DeleteKey(warehouseKey);
                PlayerPrefs.Save();
            }
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void PhaseChange_RejectsStaleGoldAndRefreshCallbacks(CoreDemoPhase phase)
        {
            IntPreference before = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
            OpenUnlocked();
            Read<InputField>("gameMasterGoldInput").text = "100";
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
            Read<Button>("gameMasterSetGoldButton").onClick.Invoke();
            Read<Button>("gameMasterRefreshShopButton").onClick.Invoke();
            Read<Button>("gameMasterRefreshMarketButton").onClick.Invoke();
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港")), Is.Zero);
            before.AssertUnchanged();
            Invoke("CloseGameMaster");
            Invoke("OpenGameMaster");
            Assert.That(controller.IsGameMasterOpen, Is.False);
        }

        [Test]
        public void TutorialAndRevisionOverflow_CannotModifyProgress()
        {
            Write("tutorialActive", true);
            Invoke("OpenGameMaster");
            Assert.That(controller.IsGameMasterOpen, Is.False);
            Write("tutorialActive", false);
            string key = CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港");
            PlayerPrefs.SetInt(key, int.MaxValue);
            OpenUnlocked();
            Button refresh = Read<Button>("gameMasterRefreshShopButton");
            Assert.That(refresh.interactable, Is.False);
            refresh.onClick.Invoke();
            Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(int.MaxValue));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GameMasterShopScopeKey), Is.False);
        }

        [Test]
        public void OpeningOrdinarySettings_DoesNotEnableGameMasterOrChangeProgress()
        {
            IntPreference before = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
            Invoke("OpenPortSettings");
            Assert.That(controller.IsGameMasterOpen, Is.False);
            Assert.That(Read<RectTransform>("portSettingsOverlay"), Is.Not.Null);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港")), Is.Zero);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GameMasterShopScopeKey), Is.False);
            before.AssertUnchanged();
            Invoke("ClosePortSettings");
        }

        [Test]
        public void DisableClosesModalAndDoesNotRenderOrSaveDuringTeardown()
        {
            OpenUnlocked();
            Read<InputField>("gameMasterGoldInput").text = "42";
            Read<Button>("gameMasterSetGoldButton").onClick.Invoke();
            // Simulate a real facility without loading its art. An erroneous Render
            // during OnDisable would destroy this draft and try to build that UI.
            Write("activePortFacility", 3);
            int children = page.childCount;
            Invoke("OnDisable");
            Assert.That(controller.IsGameMasterOpen, Is.False);
            Assert.That(page.childCount, Is.EqualTo(children));
            Assert.That(underlay.IsInteractable(), Is.True);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(42));
        }

        private void OpenUnlocked()
        {
            Invoke("OpenGameMaster");
            Read<Toggle>("gameMasterUnlockToggle").isOn = true;
        }

        private static void ClickGameMasterGraphic(Selectable target)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)target.transform;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, target.name);
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(target.gameObject), target.name + " is obstructed by " + hits[0].gameObject.name);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private object Invoke(string method, params object[] args)
        {
            return typeof(CoreGameplayDemoController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, args);
        }

        private static object InvokeStatic(string method, params object[] args)
        {
            return typeof(CoreGameplayDemoController).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        }

        private T Read<T>(string name)
        {
            return (T)typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        }

        private void Write(string name, object value)
        {
            typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
        }

        private sealed class IntPreference
        {
            private string key;
            private bool existed;
            private int value;

            public static IntPreference Read(string key)
            {
                return new IntPreference { key = key, existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key, int.MinValue) };
            }

            public void Restore()
            {
                if (existed) PlayerPrefs.SetInt(key, value);
                else PlayerPrefs.DeleteKey(key);
            }

            public void AssertUnchanged()
            {
                Assert.That(PlayerPrefs.HasKey(key), Is.EqualTo(existed), key);
                Assert.That(PlayerPrefs.GetInt(key, int.MinValue), Is.EqualTo(value), key);
            }
        }
    }
}
