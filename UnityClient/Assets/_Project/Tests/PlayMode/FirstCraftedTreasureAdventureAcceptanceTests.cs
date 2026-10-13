using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(1800000)]
        public IEnumerator NewGame_FirstCraftedTreasureAdventure_CompletesThroughRealActions()
        {
            yield return NewGame_FirstTreasureMap_TraversesRealTutorialAndCapturesScenes();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            using (var driver = new TreasureAdventureDriver(controller))
            {
                CoreVoyageSession session = controller.Session;
                Assert.That(session.TreasureFragments, Is.EqualTo(1), "The tutorial boss guarantees the first fragment, committed on return.");
                int tutorialFragments = session.TreasureFragments;
                Assert.That(session.TreasureMaps, Is.Empty);
                Assert.That(AcceptanceReadyTreasureBoard(session), Is.EqualTo(-1));
                driver.Shot("Workbench_BeforeFragments", "Treasure Panel", "UI_TreasureMap");
                yield return driver.IdentifyFragments();
                yield return driver.Click("Return To Port Hub");
                yield return driver.PrepareCaptain();
                for (int voyage = 0; AcceptanceReadyTreasureBoard(session) < 0 && voyage < 10; voyage++)
                {
                    yield return driver.PrepareSupplies(350);
                    yield return driver.Click("Port Hotspot Voyage");
                    yield return driver.Click("Route 0");
                    driver.Shot("Ordinary_Departure_" + voyage, "Voyage Setup", "UI_VoyageDeparture");
                    yield return driver.Click("Start Voyage");
                    Assert.That(session.IsTreasureVoyage, Is.False);
                    yield return driver.Explore(false);
                    yield return driver.ConfirmSettlement(false);
                    yield return driver.Click("Port Hotspot Treasure");
                    yield return driver.IdentifyFragments();
                    yield return driver.Click("Return To Port Hub");
                }
                int readyBoard = AcceptanceReadyTreasureBoard(session);
                Assert.That(readyBoard, Is.GreaterThanOrEqualTo(0), "Earn and identify more fragments until one map has four; fragments from different maps cannot be combined.");
                string rankId = CoreTreasureHuntCatalog.Ranks[readyBoard].Id;
                Assert.That(session.PirateLevel, Is.GreaterThanOrEqualTo(2));
                Assert.That(driver.EarnedFragments, Is.GreaterThanOrEqualTo(CoreTreasureHuntCatalog.FragmentCost - tutorialFragments),
                    "The remaining fragments must come from verified real encounter choices or battle drops. " + driver.FragmentSources);
                Assert.That(session.TreasureFragments + AcceptanceIdentifiedFragments(session), Is.EqualTo(tutorialFragments + driver.EarnedFragments),
                    "Every held fragment must be accounted for by the tutorial and actual reward actions. " + driver.FragmentSources);
                yield return driver.Click("Port Hotspot Treasure");
                int fragments = session.GetIdentifiedTreasureFragments(rankId), maps = session.TreasureMaps.Count;
                int unidentified = session.TreasureFragments;
                driver.Shot("Fragments_Ready", "Treasure Panel", "UITreasureMap/UITreasureMap_FragmentsReady");
                yield return driver.Click("Treasure Route " + readyBoard);
                Assert.That(session.GetIdentifiedTreasureFragments(rankId), Is.EqualTo(fragments - 4));
                Assert.That(session.TreasureFragments, Is.EqualTo(unidentified));
                Assert.That(session.TreasureMaps.Count, Is.EqualTo(maps + 1));
                CoreTreasureMapRecord crafted = session.TreasureMaps.Last();
                Assert.That(crafted.RankId, Is.EqualTo(rankId));
                Assert.That(session.SelectedTreasureMap, Is.Null);
                driver.Shot("First_Crafted_Map", "Treasure Folio", "UITreasureMap/UITreasureMap_MapDecoded");
                yield return driver.PrepareTreasureSupplies(crafted);
                yield return driver.Click("Treasure Close Folio");
                yield return driver.Click("Return To Port Hub");
                yield return driver.Click("Port Hotspot Voyage");
                Assert.That(session.SelectedTreasureMap, Is.Null, "Opening departure must not silently equip a seed.");
                yield return driver.Click("Departure Treasure Seed Picker");
                yield return driver.Click("Departure Treasure Seed Option " + (session.TreasureMaps.Count - 1));
                Assert.That(session.SelectedTreasureMap.Id, Is.EqualTo(crafted.Id));
                Assert.That(session.TreasureMaps.Any(map => map.Id == crafted.Id), Is.True);
                Assert.That(session.CanStartTreasureVoyage(out string reason), Is.True, reason);
                driver.Shot("Treasure_Departure", "Voyage Setup", "UIVoyageDeparture/UIVoyageDeparture_TreasureReady");
                yield return driver.Click("Start Voyage");
                Assert.That(session.IsTreasureVoyage, Is.True);
                Assert.That(session.ActiveTreasureMap.Id, Is.EqualTo(crafted.Id));
                Assert.That(session.TreasureMaps.Any(map => map.Id == crafted.Id), Is.False);
                Assert.That(session.CanDrawSeaChart, Is.False);
                Assert.That(session.TreasureClues, Is.Zero);
                driver.Shot("Treasure_Exploration_Start", "Navigation Chart", "UINavigation/UINavigation_TreasureExploration");
                yield return driver.Explore(true);
                Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.TreasureRecovered));
                Assert.That(session.Settlement.TreasureCompleted, Is.True);
                Assert.That(session.Settlement.ProtectionRatio, Is.EqualTo(.95f).Within(.001f));
                Assert.That(session.TreasureAdventureCompleted, Is.True);
                Assert.That(driver.ClueChoices, Is.GreaterThanOrEqualTo(crafted.Rank.RequiredClues));
                yield return driver.ConfirmSettlement(true);
                Assert.That(session.CompletedTreasureSeeds, Does.Contain(crafted.Seed));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Does.Contain(crafted.Seed));
                yield return driver.Click("Port Hotspot Treasure");
                yield return driver.Click("Treasure Open Folio");
                yield return driver.Click("Treasure Browse Used");
                Assert.That(GameObject.Find("Treasure Map Details 0").GetComponent<Text>().text, Does.Contain(crafted.Seed));
                driver.Shot("Used_Treasure_Archive", "Treasure Folio", "UITreasureMap/UITreasureMap_RewardClaim");
                driver.Record("Acceptance completed", string.Empty);
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
                yield return null; yield return null;
                CoreGameplayDemoController reloaded = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(reloaded.Session.CompletedTreasureSeeds, Does.Contain(crafted.Seed));
                Assert.That(reloaded.Session.TreasureMaps.Any(map => map.Id == crafted.Id), Is.False);
                Assert.That(reloaded.IsTutorialActive, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
        }

        private sealed class TreasureAdventureDriver : IDisposable
        {
            private readonly CoreGameplayDemoController controller;
            private readonly NewGameSceneCapture capture = new NewGameSceneCapture();
            private readonly AdventureActionLog log = new AdventureActionLog();
            private readonly Dictionary<uint, HashSet<int>> observedCells = new Dictionary<uint, HashSet<int>>();
            private readonly Dictionary<uint, HashSet<int>> observedWater = new Dictionary<uint, HashSet<int>>();
            private readonly Dictionary<uint, Dictionary<int, VoyagePoint>> observedNodes = new Dictionary<uint, Dictionary<int, VoyagePoint>>();
            private int shot, encounters;
            private bool chartClickAccepted;
            private int fragmentsFromChoices, fragmentsFromBattles, settledFragments;
            public int EarnedFragments => fragmentsFromChoices + fragmentsFromBattles;
            public string FragmentSources => "Choice fragments=" + fragmentsFromChoices +
                ", battle fragments=" + fragmentsFromBattles + ", settled fragments=" + settledFragments;
            public int ClueChoices { get; private set; }
            private CoreVoyageSession Session => controller.Session;
            public TreasureAdventureDriver(CoreGameplayDemoController value) { controller = value; Record("Actual tutorial completed", ""); }
            public void Dispose() => log.Save();
            private string State() => JsonUtility.ToJson(new AdventureState {
                phase = Session.Phase.ToString(), gold = (double)Session.ProfileGold, voyageGold = Session.Cargo.Gold,
                water = Session.Cargo.Water, food = Session.Cargo.Food, hull = Session.Hull,
                experience = Session.PirateExperience, level = Session.PirateLevel, fragments = Session.TreasureFragments,
                weatheredFragments = Session.GetIdentifiedTreasureFragments("weathered"), gildedFragments = Session.GetIdentifiedTreasureFragments("gilded"),
                moonlitFragments = Session.GetIdentifiedTreasureFragments("moonlit"),
                pendingFragments = Session.VoyageTreasureFragments, maps = Session.TreasureMaps.Count,
                clues = Session.TreasureClues, requiredClues = Session.TreasureRequiredClues,
                distance = Session.TotalDistanceNm, x = Session.Position.X, y = Session.Position.Y,
                rank = (Session.ActiveTreasureMap ?? Session.SelectedTreasureMap)?.RankId ?? "",
                crew = Session.EffectiveCrewCount, capacity = Session.CargoCapacityValue, usedVolume = Session.Cargo.UsedVolume,
                gameHours = Session.TotalHours,
                encounter = Session.ActiveEncounter?.Id ?? "", seed = Session.Map?.Seed ?? "" });
            public void Record(string action, string before)
            {
                log.actions.Add(new AdventureAction { sequence = log.actions.Count, realtime = Time.realtimeSinceStartup,
                    action = action, before = before, after = State() });
                log.Save();
            }
            public void Shot(string name, string root, string reference) =>
                capture.Capture(controller, "Adventure_" + (shot++).ToString("000") + "_" + name, root, reference);
            public IEnumerator IdentifyFragments()
            {
                while (Session.TreasureFragments > 0 && Session.PirateLevel >= 2)
                {
                    int unidentified = Session.TreasureFragments, identified = AcceptanceIdentifiedFragments(Session);
                    yield return Click("Treasure Identify Fragment");
                    Assert.That(Session.TreasureFragments, Is.EqualTo(unidentified - 1));
                    Assert.That(AcceptanceIdentifiedFragments(Session), Is.EqualTo(identified + 1));
                    Assert.That(Session.TreasureMaps, Is.Empty, "Identifying the fourth piece still requires clicking its puzzle to create a seed.");
                }
            }
            public IEnumerator Click(string name)
            {
                if (name.StartsWith("Event Node ", StringComparison.Ordinal))
                {
                    GameObject marker = GameObject.Find(name);
                    Assert.That(marker, Is.Not.Null, "A chart-visible event must have its marker.");
                    Canvas.ForceUpdateCanvases();
                    RectTransform markerRect = marker.GetComponent<RectTransform>();
                    NavigationMapViewport viewport = marker.GetComponentInParent<NavigationMapViewport>();
                    Assert.That(viewport, Is.Not.Null);
                    // IsChartVisible means discovered, not currently inside the
                    // cropped viewport. Pan before using the normal pointer path.
                    viewport.EnsureVisible(viewport.transform.InverseTransformPoint(markerRect.TransformPoint(markerRect.rect.center)));
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                }
                if (name.StartsWith("Encounter Choice ", StringComparison.Ordinal))
                {
                    yield return WaitForEncounterDossierPresentationCompletion();
                    Button choice = ActiveButton(name);
                    Assert.That(choice, Is.Not.Null);
                    ScrollRect scroll = choice.GetComponentInParent<ScrollRect>();
                    Assert.That(scroll, Is.Not.Null);
                    var corners = new Vector3[4];
                    for (int attempt = 0; attempt < 8; attempt++)
                    {
                        choice.GetComponent<RectTransform>().GetWorldCorners(corners);
                        float bottom = corners.Min(p => scroll.viewport.InverseTransformPoint(p).y);
                        float top = corners.Max(p => scroll.viewport.InverseTransformPoint(p).y);
                        Rect bounds = scroll.viewport.rect;
                        if (bottom >= bounds.yMin - .5f && top <= bounds.yMax + .5f) break;
                        Assert.That(scroll.vertical, Is.True, "Overflowing choices must support real scrolling.");
                        float delta = top > bounds.yMax ? top - bounds.yMax + 2f : bottom - bounds.yMin - 2f;
                        scroll.OnScroll(new PointerEventData(EventSystem.current) {
                            scrollDelta = new Vector2(0f, delta / scroll.scrollSensitivity) });
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                    }
                    choice.GetComponent<RectTransform>().GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector2 local = scroll.viewport.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(scroll.viewport.rect.xMin - .5f, scroll.viewport.rect.xMax + .5f));
                        Assert.That(local.y, Is.InRange(scroll.viewport.rect.yMin - .5f, scroll.viewport.rect.yMax + .5f),
                            "The complete chosen action must be revealed before the actual pointer click.");
                    }
                }
                string before = State();
                Record("Attempt click: " + name, before);
                try { ClickAcceptanceButton(name); }
                catch (AssertionException)
                {
                    Shot("Blocked_" + name.Replace(' ', '_'), Session.Phase == CoreDemoPhase.Sailing ? "Navigation Chart" :
                        Session.Phase == CoreDemoPhase.Encounter ? "Encounter Card" : Session.Phase == CoreDemoPhase.Battle ? "Battle Card" : "Phase Content", "");
                    throw;
                }
                yield return null;
                float deadline = Time.realtimeSinceStartup + 15;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    AdvanceRamComicIfWaiting(controller);
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying, Is.False, name + " animation stalled.");
                // Sailing commits the encounter before its marker animation
                // renders the corresponding controls. Follow the presented UI.
                yield return WaitForNavigationMotionCompletion(controller);
                yield return null;
                Record("Click: " + name, before);
                if (GameObject.Find("Treasure Story Overlay") != null)
                {
                    string storyBefore = State();
                    yield return DismissTreasureStoryIfPresent(controller);
                    Record("Clicks: Treasure Story Advance through all comic pages", storyBefore);
                }
            }
            private IEnumerator EnsureGold(int required, string reason)
            {
                if (Session.ProfileGold >= required) yield break;
                string progression = JsonUtility.ToJson(Session.CaptureTreasureProgress()), before = State();
                InputSettings originalSettings = InputSystem.settings;
                HideFlags originalSettingsFlags = originalSettings.hideFlags;
                // InputManager destroys its default HideAndDontSave settings on
                // replacement. Retain it until the input-only test scope ends.
                if (originalSettingsFlags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.DontSave;
                InputSettings playthroughSettings = Object.Instantiate(originalSettings);
                playthroughSettings.hideFlags = HideFlags.DontSave;
                playthroughSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                playthroughSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                playthroughSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings = playthroughSettings;
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                try
                {
                    keyboard.MakeCurrent();
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F10));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    Assert.That(controller.IsGameMasterOpen, Is.True, "GM must open through actual F10 input.");
                    yield return ClickDestinationTestGraphic(GameObject.Find("Game Master Unlock"), Vector2.zero);
                    Assert.That(GameObject.Find("Game Master Unlock").GetComponent<Toggle>().isOn, Is.True);
                    InputField field = GameObject.Find("Game Master Gold Input").GetComponent<InputField>();
                    Assert.That(field.IsInteractable(), Is.True);
                    field.text = required.ToString(CultureInfo.InvariantCulture);
                    yield return null;
                    Shot("GM_Gold_" + reason, "Game Master Sheet", "");
                    yield return Click("Game Master Set Gold");
                    Assert.That(Session.ProfileGold, Is.EqualTo(required));
                    Assert.That(JsonUtility.ToJson(Session.CaptureTreasureProgress()), Is.EqualTo(progression));
                    yield return new WaitForSecondsRealtime(.45f);
                    yield return Click("Close Game Master");
                    Record("F10 gold assistance: " + reason, before);
                }
                finally
                {
                    if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                    InputSystem.settings = originalSettings;
                    originalSettings.hideFlags = originalSettingsFlags;
                    Object.Destroy(playthroughSettings);
                }
            }
            public IEnumerator PrepareCaptain()
            {
                int portIndex = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count)
                    .Single(index => CoreVoyageSession.DestinationPorts[index] == Session.CurrentPort);
                int supplyShip = CoreShipMarketCatalog.ShipsForPort(portIndex).Where(index => index != 0)
                    .OrderByDescending(index => CoreVoyageSession.Ships[index].CargoCapacity).First();
                int shipCost = CoreShipMarketCatalog.PurchaseCost(supplyShip);
                int contracts = Enumerable.Range(1, 5).Sum(i => CrewNarrativeCatalog.ForPortAndProfession(Session.CurrentPort, i).ContractCost);
                yield return EnsureGold(contracts + shipCost + 3 * 840 + 3000, "preparation_budget");
                yield return Click("Port Hotspot Tavern");
                for (int profession = 1; profession < 6; profession++)
                {
                    CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession(Session.CurrentPort, profession);
                    yield return Click("Tavern Crew " + profession);
                    Shot("Recruitment_Offer_" + profession, "Tavern Crew Detail", "UITavern/UITavern_RecruitmentOffer");
                    decimal gold = Session.ProfileGold;
                    yield return Click("Recruit Tavern Crew " + profession);
                    Assert.That(Session.ProfileGold, Is.EqualTo(gold - crew.ContractCost));
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id)), Is.EqualTo(1));
                    Shot("Contract_Signed_" + profession, "Tavern Crew Detail", "UITavern/UITavern_ContractSigned");
                    yield return Click("Return To Port Hub");
                    Assert.That(GameObject.Find("Tavern Crew Stage"), Is.Not.Null);
                }
                yield return Click("Return To Port Hub");
                yield return Click("Port Hotspot Shipyard");
                for (int n = 0; ActiveButton("Shipyard Ship " + supplyShip) == null && n < CoreVoyageSession.Ships.Count; n++)
                    yield return Click("Shipyard Next Vessel");
                yield return Click("Shipyard Ship " + supplyShip);
                decimal purchaseGold = Session.ProfileGold;
                yield return Click("Confirm Shipyard Vessel");
                Assert.That(Session.SelectedShipIndex, Is.EqualTo(supplyShip));
                Assert.That(Session.ProfileGold, Is.EqualTo(purchaseGold - shipCost));
                Shot("Purchased_Local_Supply_Ship", "Shipyard Detail", "UI_ShipyardDetail");
                yield return Click("Back To Shipyard Gallery");
                yield return Click("Return To Port Hub");
                yield return Click("Port Hotspot CardWorkshop");
                yield return Click("Workshop Preview Stage Naval"); yield return Click("Open Battle Card Upgrades");
                foreach (int index in new[] { 0, 5, 6 })
                {
                    for (int page = 0; ActiveButton("Select Upgrade Card " + index) == null && page < 8; page++) yield return Click("Next Upgrade Cards");
                    yield return Click("Select Upgrade Card " + index);
                    Shot("Upgrade_Preview_" + index, "Battle Card Upgrade Panel", "UICardWorkshop/UICardWorkshop_UpgradePreview");
                    while (Session.GetBattleCard(index).UpgradeLevel < 3)
                    {
                        int rank = Session.GetBattleCard(index).UpgradeLevel;
                        int cost = Session.GetCardUpgradeCost(index);
                        decimal gold = Session.ProfileGold;
                        yield return Click("Confirm Battle Card Upgrade");
                        Assert.That(Session.GetBattleCard(index).UpgradeLevel, Is.EqualTo(rank + 1));
                        Assert.That(Session.ProfileGold, Is.EqualTo(gold - cost));
                    }
                    Shot("Upgrade_Complete_" + index, "Battle Card Upgrade Panel", "UICardWorkshop/UICardWorkshop_UpgradeComplete");
                }
                yield return Click("Back From Card Upgrades");
                yield return Click("Return To Port Hub");
            }
            public IEnumerator PrepareTreasureSupplies(CoreTreasureMapRecord crafted)
            {
                Assert.That(GameObject.Find("Treasure Map Details 0").GetComponent<Text>().text,
                    Does.Contain(crafted.Seed).And.Contain(crafted.Rank.Name).And.Contain("探索度"));
                // This driver searches unrevealed water instead of following a
                // known trade lane. Budget a second suggested voyage duration
                // for that search, using full crew before any cook discount.
                int explorationDays = crafted.Rank.RecommendedVoyageDays * 2 + 2;
                int rationTarget = Session.CrewCountValue * explorationDays;
                Record("Treasure preparation: rank=" + crafted.RankId + "; seed=" + crafted.Seed +
                    "; suggestedDays=" + crafted.Rank.RecommendedVoyageDays + "; explorationDays=" + explorationDays +
                    "; targetEach=" + rationTarget + "; capacity=" + Session.CargoCapacityValue, "");
                yield return Click("Treasure Close Folio");
                yield return Click("Return To Port Hub");
                yield return PrepareSupplies(rationTarget, true);
                Assert.That(Session.Cargo.Water, Is.GreaterThanOrEqualTo(rationTarget));
                Assert.That(Session.Cargo.Food, Is.GreaterThanOrEqualTo(rationTarget));
                Assert.That(Session.Cargo.UsedVolume, Is.LessThanOrEqualTo(Session.CargoCapacityValue + .001f));
                Assert.That(Session.TreasureMaps.Any(map => map.Id == crafted.Id), Is.True, "Provisioning must preserve the actual crafted map.");
                Assert.That(Session.SelectedTreasureMap, Is.Null);
                Record("Treasure supplies purchased: rank=" + crafted.RankId + "; suggestedDays=" +
                    crafted.Rank.RecommendedVoyageDays + "; targetEach=" + rationTarget + "; water=" + Session.Cargo.Water +
                    "; food=" + Session.Cargo.Food + "; volume=" + Session.Cargo.UsedVolume + "/" + Session.CargoCapacityValue, "");
                yield return Click("Port Hotspot Treasure");
                yield return Click("Treasure Open Folio");
                yield return Click("Treasure Browse Unused");
                Assert.That(GameObject.Find("Treasure Map Details 0").GetComponent<Text>().text, Does.Contain(crafted.Seed));
            }
            public IEnumerator PrepareSupplies(int rationTarget, bool exactQuantities = false)
            {
                bool marketShortage = false;
                // The final treasure preparation uses the existing budget and
                // fails explicitly if it cannot afford its actual market quote.
                if (!exactQuantities) yield return EnsureGold(2500, "market_supplies");
                yield return Click("Port Hotspot Market");
                foreach (PortMarketItem item in new[] { PortMarketItem.Iron, PortMarketItem.Cloth })
                {
                    string key = PortMarketCatalog.Key(item);
                    for (int n = 0; Session.GetMarketCargoAmount(key) > 0 && n < 100; n++)
                    {
                        yield return OpenMarketTrade(key, false);
                        Button sell = ActiveButton("Confirm Market Sell " + key);
                        if (sell == null || !sell.IsInteractable()) break;
                        yield return Click(sell.name);
                    }
                }
                foreach (var purchase in new[] {
                    new KeyValuePair<PortMarketItem,int>(PortMarketItem.Water, rationTarget),
                    new KeyValuePair<PortMarketItem,int>(PortMarketItem.Food, rationTarget),
                    new KeyValuePair<PortMarketItem,int>(PortMarketItem.Powder, 12),
                    new KeyValuePair<PortMarketItem,int>(PortMarketItem.Cannonballs, 20) })
                {
                    string key = PortMarketCatalog.Key(purchase.Key);
                    while (Session.GetMarketCargoAmount(key) < purchase.Value)
                    {
                        yield return OpenMarketItem(key);
                        Button direction = RequireGuidedButton("Market Buy " + key);
                        if (!direction.IsInteractable()) { marketShortage = true; break; }
                        yield return Click(direction.name);
                        if (exactQuantities)
                        {
                            int missing = purchase.Value - Session.GetMarketCargoAmount(key);
                            Assert.That((double)Session.Cargo.UsedVolume + missing * (double)PortMarketCatalog.CargoVolume(purchase.Key),
                                Is.LessThanOrEqualTo(Session.CargoCapacityValue + .001d), "The legal exploration supplies must fit: " + key);
                            yield return EnterMarketQuantity(key, missing.ToString(CultureInfo.InvariantCulture));
                        }
                        Button buy = RequireGuidedButton("Confirm Market Buy " + key);
                        if (exactQuantities) Assert.That(buy.IsInteractable(), Is.True,
                            "Cannot fund or load the actual treasure preparation quote: " + key + "; gold=" + Session.ProfileGold +
                            "; volume=" + Session.Cargo.UsedVolume + "/" + Session.CargoCapacityValue);
                        if (!buy.IsInteractable())
                        {
                            marketShortage = true;
                            Record("Market purchase unavailable: " + key, State());
                            break;
                        }
                        Slider quantityInput = MarketQuantityField(key);
                        int amount = Mathf.RoundToInt(quantityInput.value);
                        Assert.That(amount, Is.GreaterThan(0));
                        int oldAmount = Session.GetMarketCargoAmount(key);
                        if (exactQuantities) Assert.That(amount, Is.EqualTo(purchase.Value - oldAmount),
                            "The market must retain the exact requested supply shortfall.");
                        decimal gold = Session.ProfileGold;
                        decimal cost = PortMarketCatalog.CalculateBuyCost(Session.CurrentPort, purchase.Key, Session.GameDateTime, amount, Session.Cargo.Fame);
                        Assert.That(GameObject.Find("Market Trade Total").GetComponent<Text>().text,
                            Does.Contain(PortMarketCatalog.FormatPrice(cost)));
                        yield return Click(buy.name);
                        Assert.That(Session.GetMarketCargoAmount(key), Is.EqualTo(oldAmount + amount));
                        Assert.That(Session.ProfileGold, Is.EqualTo(gold - cost));
                    }
                }
                Shot("Purchased_Supplies", "Market Panel", "UI_PortMarket");
                if (marketShortage)
                {
                    int level = PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(Session.CurrentPort), 0);
                    Assert.That(level, Is.LessThan(PortInvestmentCatalog.MaxLevel));
                    PortInvestmentCost investment = PortInvestmentCatalog.CostForLevel(Session.CurrentPort, level + 1);
                    yield return EnsureGold(investment.Gold + 1500, "port_investment");
                    foreach (var purchase in new[] {
                        new KeyValuePair<PortMarketItem,int>(PortMarketItem.Wood, investment.Wood),
                        new KeyValuePair<PortMarketItem,int>(PortMarketItem.Iron, investment.Iron),
                        new KeyValuePair<PortMarketItem,int>(investment.RequiredItem, investment.RequiredQuantity) })
                    {
                        string key = PortMarketCatalog.Key(purchase.Key);
                        while (Session.GetMarketCargoAmount(key) < purchase.Value)
                        {
                            yield return OpenMarketTrade(key);
                            yield return Click("Confirm Market Buy " + key);
                        }
                    }
                    if (GameObject.Find("Cancel Market Trade") != null) yield return Click("Cancel Market Trade");
                    if (GameObject.Find("Market Detail Back") != null) yield return Click("Market Detail Back");
                    yield return Click("Market Open Investment");
                    decimal gold = Session.ProfileGold;
                    int wood = Session.Cargo.Wood;
                    yield return Click("Confirm Port Investment");
                    Assert.That(Session.ProfileGold, Is.EqualTo(gold - investment.Gold));
                    Assert.That(Session.Cargo.Wood, Is.EqualTo(wood - investment.Wood));
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(Session.CurrentPort)), Is.EqualTo(level + 1));
                    Shot("Port_Investment_Expanded_Quota", "Port Investment Panel", "UI_Maintenance");
                    yield return Click("Open Port Market");
                    foreach (var purchase in new[] {
                        new KeyValuePair<PortMarketItem,int>(PortMarketItem.Water, rationTarget),
                        new KeyValuePair<PortMarketItem,int>(PortMarketItem.Food, rationTarget),
                        new KeyValuePair<PortMarketItem,int>(PortMarketItem.Powder, 12),
                        new KeyValuePair<PortMarketItem,int>(PortMarketItem.Cannonballs, 20) })
                    {
                        string key = PortMarketCatalog.Key(purchase.Key);
                        while (Session.GetMarketCargoAmount(key) < purchase.Value)
                        {
                            yield return OpenMarketTrade(key);
                            yield return Click("Confirm Market Buy " + key);
                        }
                    }
                    Shot("Supplies_After_Port_Investment", "Market Panel", "UI_Market");
                }
                yield return Click("Return To Port Hub");
            }
            public IEnumerator Explore(bool treasure)
            {
                var rejected = new HashSet<string>();
                var encounterVisits = new Dictionary<string, int>();
                int steps = 0;
                while (Session.Phase != CoreDemoPhase.Settlement && steps++ < 1600)
                {
                    if (Session.Phase == CoreDemoPhase.Battle) { yield return Battle(); continue; }
                    if (Session.Phase == CoreDemoPhase.Encounter)
                    {
                        string location = Session.ActiveEncounter.Id + ":" + Session.Position.X.ToString("F1") + ":" + Session.Position.Y.ToString("F1");
                        encounterVisits.TryGetValue(location, out int visits);
                        encounterVisits[location] = visits + 1;
                        Assert.That(visits, Is.LessThan(5), "Encounter repeatedly reopened without being resolved: " + location);
                        yield return Encounter(); continue;
                    }
                    Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                    RememberChart();
                    if (!treasure && (Session.VoyageTreasureFragments >= 2 ||
                        Session.Cargo.Water < 35 || Session.Cargo.Food < 35 || Session.Hull < 20))
                    { yield return Click("Safe Return"); continue; }
                    AdventureGoal goal = ChooseGoal(treasure, rejected);
                    Assert.That(goal, Is.Not.Null, "No reachable unexplored water or useful observed event remains.");
                    string before = State();
                    if (goal.node != null && goal.node.IsChartVisible) yield return Click("Event Node " + goal.node.Id);
                    else
                    {
                        Vector2 seaSize = GameObject.Find("Ocean Tile Layer").GetComponent<RectTransform>().rect.size;
                        Vector2 local = new Vector2((goal.point.X / Session.Map.WorldWidth - .5f) * seaSize.x,
                            (goal.point.Y / Session.Map.WorldHeight - .5f) * seaSize.y);
                        yield return ClickChartPoint(local);
                        Record("Chart pointer: " + goal.key, before);
                        if (!chartClickAccepted) { rejected.Add(goal.key); continue; }
                    }
                    if (Session.Phase != CoreDemoPhase.Sailing) continue;
                    SailingForecast forecast = Session.GetForecast();
                    if (forecast == null || forecast.Blocked || forecast.ActualDistanceNm < 1)
                    { rejected.Add(goal.key); Record("Rejected route preview: " + goal.key, State()); continue; }
                    if (forecast.WaterCost > Session.Cargo.Water || forecast.FoodCost > Session.Cargo.Food)
                    {
                        Assert.That(treasure, Is.False, "Treasure rations exhausted; do not inject resources or bypass failure.");
                        yield return Click("Safe Return"); continue;
                    }
                    VoyagePoint previous = Session.Position;
                    yield return Click("Execute Sail");
                    bool moved = Distance(previous, Session.Position) > .01f;
                    Assert.That(moved || Session.Phase != CoreDemoPhase.Sailing, Is.True, "Valid sailing must advance.");
                    if (Distance(previous, Session.Position) > 2f) rejected.Clear();
                    if (steps % 20 == 0 && Session.Phase == CoreDemoPhase.Sailing)
                        Shot((treasure ? "Treasure" : "Ordinary") + "_Navigation_" + steps, "Navigation Chart", "UI_Navigation");
                }
                Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement), "Navigation exceeded its bounded action budget.");
                if (treasure) Assert.That(Session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.TreasureRecovered));
            }
            private void RememberChart()
            {
                uint hash = Session.Map.Hash;
                if (!observedCells.TryGetValue(hash, out HashSet<int> cells)) observedCells[hash] = cells = new HashSet<int>();
                if (!observedWater.TryGetValue(hash, out HashSet<int> water)) observedWater[hash] = water = new HashSet<int>();
                if (!observedNodes.TryGetValue(hash, out Dictionary<int,VoyagePoint> nodes)) observedNodes[hash] = nodes = new Dictionary<int,VoyagePoint>();
                for (int i = 0; i < Session.Map.CellCount; i++) if (Session.Map.ExploredCells[i])
                {
                    cells.Add(i);
                    // Only remember terrain after it has actually been revealed.
                    if (Session.Map.IsPassable(i % Session.Map.Columns, i / Session.Map.Columns)) water.Add(i);
                }
                foreach (CoreMapNode node in Session.Map.Nodes.Where(n => n.IsChartVisible)) nodes[node.Id] = node.Position;
            }
            private IEnumerator ClickChartPoint(Vector2 local)
            {
                chartClickAccepted = false;
                GameObject surface = GameObject.Find("Map Course Input");
                Assert.That(surface, Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                RectTransform rect = surface.GetComponent<RectTransform>();
                NavigationMapViewport viewport = rect.GetComponentInParent<NavigationMapViewport>();
                Assert.That(viewport, Is.Not.Null);
                // The square-cell map is cropped; pan its production viewport to
                // the chosen chart point before performing the normal raycast.
                viewport.EnsureVisible(viewport.transform.InverseTransformPoint(rect.TransformPoint(rect.rect.center + local)));
                yield return null;
                Canvas.ForceUpdateCanvases();
                Canvas canvas = surface.GetComponentInParent<Canvas>().rootCanvas;
                var pointer = new PointerEventData(EventSystem.current) {
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                        rect.TransformPoint(rect.rect.center + local)) };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0) yield break;
                GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                if (handler == null || !handler.transform.IsChildOf(surface.transform.parent)) yield break;
                Button button = handler.GetComponent<Button>();
                if (button != null && !button.IsInteractable()) yield break;
                chartClickAccepted = true;
                string before = State();
                string clickedName = handler.name;
                ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
                yield return null;
                Record("Actual chart hit: " + clickedName, before);
            }
            private AdventureGoal ChooseGoal(bool treasure, HashSet<string> rejected)
            {
                CoreVoyageMap map = Session.Map;
                Dictionary<int,VoyagePoint> known = observedNodes[map.Hash];
                bool needsRations = treasure && Session.TreasureClues < Session.TreasureRequiredClues &&
                    Math.Min(Session.Cargo.Water, Session.Cargo.Food) <
                    Session.CrewCountValue * Session.ActiveTreasureMap.Rank.RecommendedVoyageDays / 2;
                bool VisibleSupply(CoreMapNode node) => needsRations && node.Discovered &&
                    node.Encounter.Choices.Any(c => !c.StartsBattle && (c.Effect.Water > 0 || c.Effect.Food > 0));
                bool Useful(CoreMapNode node)
                {
                    bool readyCore = treasure && node.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId && Session.TreasureClues >= Session.TreasureRequiredClues;
                    if (node.Visited || (node.Skipped && !readyCore) || !known.ContainsKey(node.Id)) return false;
                    if (!node.Discovered) return !treasure || Session.TreasureClues < Session.TreasureRequiredClues;
                    if (VisibleSupply(node)) return true;
                    if (treasure) return Session.TreasureClues >= Session.TreasureRequiredClues ?
                        node.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId : node.Encounter.Choices.Any(c => c.Effect.TreasureClues > 0);
                    return node.Encounter.Choices.Any(c => c.Effect.MapFragments > 0 || c.Effect.Reveal > 0);
                }
                CoreMapNode selected = map.Nodes.Where(Useful).Where(n => !rejected.Contains("node:" + n.Id))
                    .OrderByDescending(n => VisibleSupply(n) ? 10000 : n.Discovered ?
                        n.Encounter.Choices.Max(c => c.Effect.MapFragments * 100 + c.Effect.TreasureClues * 100 + c.Effect.Reveal) : 0)
                    .ThenBy(n => Distance(Session.Position, known[n.Id])).FirstOrDefault();
                if (selected != null) return new AdventureGoal { node = selected, point = known[selected.Id], key = "node:" + selected.Id };
                HashSet<int> seen = observedCells[map.Hash];
                int UnknownNeighbours(int cell, int radius)
                {
                    int count = 0, column = cell % map.Columns, row = cell / map.Columns;
                    for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = column + dx, y = row + dy;
                        if (x > 0 && x < map.Columns - 1 && y > 0 && y < map.Rows - 1 &&
                            !seen.Contains(y * map.Columns + x)) count++;
                    }
                    return count;
                }
                var frontier = observedWater[map.Hash].Where(i => !rejected.Contains("cell:" + i) && UnknownNeighbours(i, 1) > 0)
                    .Select(i => new { index = i, point = map.CellCenter(i % map.Columns, i / map.Columns) })
                    .Where(g => g.point.X >= 2 && g.point.X <= map.WorldWidth - 2 && g.point.Y >= 2 && g.point.Y <= map.WorldHeight - 2 && Distance(Session.Position, g.point) > 2f)
                    .OrderByDescending(g => UnknownNeighbours(g.index, 3) / (2f + Distance(Session.Position, g.point))).FirstOrDefault();
                if (frontier != null) return new AdventureGoal { point = frontier.point, key = "cell:" + frontier.index };
                // A free route preview probes unknown space only when no observed
                // water frontier remains; no hidden terrain is inspected here.
                return Enumerable.Range(0, map.CellCount).Where(i => !seen.Contains(i) && !rejected.Contains("cell:" + i))
                    .Select(i => new AdventureGoal { point = map.CellCenter(i % map.Columns, i / map.Columns), key = "cell:" + i })
                    .Where(g => g.point.X >= 2 && g.point.X <= map.WorldWidth - 2 && g.point.Y >= 2 && g.point.Y <= map.WorldHeight - 2 && Distance(Session.Position, g.point) > 2f)
                    .OrderBy(g => Distance(Session.Position, g.point)).FirstOrDefault();
            }
            private bool CrossesLockedCore(SailingForecast forecast)
            {
                if (!Session.IsTreasureVoyage || Session.TreasureClues >= Session.TreasureRequiredClues) return false;
                CoreMapNode core = Session.Map.Nodes.FirstOrDefault(n => n.IsChartVisible && n.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId);
                if (core == null) return false;
                Vector2 p = new Vector2(core.Position.X, core.Position.Y);
                for (int i = 1; i < forecast.PathPoints.Count; i++)
                {
                    VoyagePoint a0 = forecast.PathPoints[i - 1], b0 = forecast.PathPoints[i];
                    Vector2 a = new Vector2(a0.X,a0.Y), b = new Vector2(b0.X,b0.Y), delta = b - a;
                    float t = delta.sqrMagnitude < .00001f ? 0 : Mathf.Clamp01(Vector2.Dot(p-a, delta)/delta.sqrMagnitude);
                    if (Vector2.Distance(p,a+delta*t) < 1.6f) return true;
                }
                return false;
            }
            private IEnumerator Encounter()
            {
                CoreEncounter encounter = Session.ActiveEncounter;
                Shot("Encounter_" + (encounters++) + "_" + encounter.Id, "Encounter Card", "UI_Encounter");
                int index;
                if (encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId)
                {
                    if (Session.TreasureClues < Session.TreasureRequiredClues)
                    {
                        Assert.That(Session.IsChoiceAvailable(0), Is.False);
                        yield return Click("Encounter Choice 3");
                        Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                        yield break;
                    }
                    Assert.That(Session.IsChoiceAvailable(0), Is.True, Session.ChoiceUnavailableReason(0));
                    index = 0;
                }
                else
                {
                    index = Enumerable.Range(0, encounter.Choices.Count).Where(Session.IsChoiceAvailable)
                        .OrderByDescending(i => ChoiceScore(encounter.Choices[i])).DefaultIfEmpty(-1).First();
                    if (index < 0)
                    {
                        Button skip = ActiveButton("Skip Encounter");
                        Assert.That(skip != null && skip.IsInteractable(), Is.True, "No affordable real choice for " + encounter.Id);
                        yield return Click("Skip Encounter"); yield break;
                    }
                }
                CoreEncounterChoice chosen = encounter.Choices[index];
                VoyageEffect effective = Session.EffectiveChoiceEffect(index);
                int fragments = Session.VoyageTreasureFragments, clues = Session.TreasureClues;
                yield return Click("Encounter Choice " + index);
                Record(encounter.Id + " choice " + index + ": " + chosen.Label, "");
                if (!chosen.StartsBattle)
                {
                    int gained = Session.VoyageTreasureFragments - fragments;
                    Assert.That(gained, Is.EqualTo(effective.MapFragments),
                        "Only the currently earned encounter effect may add fragments: " + encounter.Id);
                    fragmentsFromChoices += gained;
                    if (gained > 0) Record("Verified encounter fragments +" + gained + ": " + encounter.Id, "");
                }
                else Assert.That(Session.VoyageTreasureFragments, Is.EqualTo(fragments),
                    "A battle choice must not pay its fragment reward before victory: " + encounter.Id);
                if (!chosen.StartsBattle && chosen.Effect.TreasureClues > 0)
                { ClueChoices++; Assert.That(Session.TreasureClues, Is.EqualTo(clues + chosen.Effect.TreasureClues)); }
            }
            private static double ChoiceScore(CoreEncounterChoice c) => c.Effect.MapFragments * 10000 + c.Effect.TreasureClues * 5000 +
                c.Effect.Reveal * 300 + Math.Max(0,c.Effect.Water) + Math.Max(0,c.Effect.Food) + c.Effect.Hull * 3 + c.Effect.Gold * .1 -
                c.Hours * 3 - (c.StartsBattle ? 1000 : 0) - (c.Effect.ForceReturn ? 100000 : 0);
            private IEnumerator Battle()
            {
                int fragmentsBefore = Session.VoyageTreasureFragments;
                string encounterId = Session.ActiveEncounter.Id;
                var turns = new Dictionary<string,int>(); int actions = 0;
                while (Session.Phase == CoreDemoPhase.Battle && actions++ < 350)
                {
                    CoreBattleState battle = Session.Battle;
                    string turn = battle.Stage + ":" + battle.Turn;
                    turns.TryGetValue(turn,out int count); turns[turn] = count + 1;
                    int selected = -1; float best = float.NegativeInfinity;
                    if (count < 24) for (int hand = 0; hand < battle.Hand.Count; hand++)
                    {
                        if (!Session.CanPlayCard(hand)) continue;
                        CoreBattleCard card = Session.GetBattleCard(battle.Hand[hand]);
                        float value = card.Damage * 4 + card.Draw * 2 + card.Block * .6f + Math.Min(Session.MaxHull-Session.Hull,card.Repair)*2;
                        if (card.Action == NavalBattleCardAction.Approach || card.Action == NavalBattleCardAction.Navigator) value += battle.DistanceMeters > 350 ? 50 : 2;
                        if (card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad) value += 8;
                        if (card.Action == NavalBattleCardAction.Retreat) value -= 100;
                        if (value > best) { best = value; selected = hand; }
                    }
                    yield return Click(selected >= 0 && best >= 0 ? "Battle Card " + selected : "End Battle Turn");
                }
                Assert.That(Session.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle), "Battle exceeded action budget.");
                Assert.That(Session.Hull, Is.GreaterThan(0), "Never replace a real defeat with injected victory.");
                int gained = Session.VoyageTreasureFragments - fragmentsBefore;
                Assert.That(gained, Is.GreaterThanOrEqualTo(0), "A completed battle must preserve previously earned fragments.");
                fragmentsFromBattles += gained;
                Record("Verified battle fragments +" + gained + ": " + encounterId, "");
            }
            public IEnumerator ConfirmSettlement(bool treasure)
            {
                Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                Button reveal = ActiveButton("Skip Settlement Reveal");
                if (reveal != null && reveal.IsInteractable()) yield return Click("Skip Settlement Reveal");
                int fragments = Session.TreasureFragments, experience = Session.PirateExperience;
                int earned = Session.Settlement.BankedFragments, xp = Session.Settlement.BankedPirateExperience;
                Assert.That(earned, Is.EqualTo(EarnedFragments - settledFragments),
                    "Settlement must bank exactly the fragments observed from this voyage's real actions. " + FragmentSources);
                if (treasure) Assert.That(Session.Settlement.TreasureCompleted, Is.True);
                Shot(treasure ? "Treasure_Completed_Settlement" : "Fragment_Settlement", "Settlement Card", "UI_Settlement");
                yield return Click("Return To Port");
                Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(Session.TreasureFragments, Is.EqualTo(fragments + earned));
                Assert.That(Session.PirateExperience, Is.EqualTo(experience + xp));
                settledFragments += earned;
                Record("Verified fragment settlement: " + FragmentSources, "");
            }
            private static float Distance(VoyagePoint a,VoyagePoint b) => Vector2.Distance(new Vector2(a.X,a.Y),new Vector2(b.X,b.Y));
            private sealed class AdventureGoal { public CoreMapNode node; public VoyagePoint point; public string key; }
        }
        [Serializable] private sealed class AdventureActionLog
        {
            public List<AdventureAction> actions = new List<AdventureAction>();
            public void Save()
            {
                string[] args = Environment.GetCommandLineArgs();
                int flag = Array.IndexOf(args, "-auditActionLogPath");
                bool auditOnly = flag >= 0;
                if (!auditOnly) flag = Array.IndexOf(args,"-navalVisualCapturePath");
                if (flag < 0 || flag + 1 >= args.Length) return;
                string directory = Path.GetFullPath(args[flag+1]);
                if (auditOnly) Directory.CreateDirectory(directory);
                else Assert.That(Directory.Exists(directory), Is.True);
                File.WriteAllText(Path.Combine(directory, auditOnly ? "treasure-adventure-actions.json" : "actions.json"),
                    JsonUtility.ToJson(this,true),new UTF8Encoding(false));
            }
        }
        [Serializable] private sealed class AdventureAction { public int sequence; public float realtime; public string action,before,after; }
        [Serializable] private sealed class AdventureState
        { public string phase,encounter,seed,rank; public double gold,gameHours; // JSON display only; actual balance assertions remain decimal.
            public int voyageGold,water,food,hull,experience,level,fragments,weatheredFragments,gildedFragments,moonlitFragments,pendingFragments,maps,clues,requiredClues,crew;
            public float distance,x,y,capacity,usedVolume; }
    }
}
