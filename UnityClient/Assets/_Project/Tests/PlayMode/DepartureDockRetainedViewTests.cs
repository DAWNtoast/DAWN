using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureDockRetained_CyclingShipsKeepsDockAndRebindsOnlyTheVesselPreview()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            foreach (int ship in new[] { 1, 4 }) PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 1);
            SeedShipPortLocationsForFixture("断锚港", 0, 1, 4);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            string[] names = { "Voyage Setup", "Route Selection", "Voyage Background", "Departure Dock Foreground",
                "Port Facility Header", "Inspect Departure Vessel", "Departure Dock Selected Sea", "Setup Title",
                "Departure Previous Vessel", "Departure Next Vessel", "Departure Dock Ordinary Readout", "Departure Dock Speed Readout",
                "Departure Dock Vessel Controls", "Departure Dock Crew Readout Panel", "Departure Buy Provisions",
                "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewIncreaseButton", "Start Voyage",
                "Departure Dock Water Blend", "Departure Dock Water Ripple" };
            Dictionary<string, GameObject> retained = names.ToDictionary(name => name, GameObject.Find);
            foreach (var item in retained) Assert.That(item.Value, Is.Not.Null, item.Key);
            int chartRevision = RetainedDockChartRevision(controller);
            var oldCrewCallback = ActiveButton("DepartureOrdinaryCrewDecreaseButton").onClick;
            var oldSupplyCallback = ActiveButton("Departure Buy Provisions").onClick;
            foreach (int expectedShip in new[] { 1, 4, 0, 1, 4, 0 })
            {
                GameObject previousShip = GameObject.Find("Starter Modular Ship");
                yield return ClickShipyardControl("Departure Next Vessel");
                AssertDepartureFlagship(controller, expectedShip);
                foreach (var item in retained)
                    Assert.That(GameObject.Find(item.Key), Is.SameAs(item.Value), item.Key + " must remain fixed while changing ships.");
                GameObject ship = GameObject.Find("Starter Modular Ship");
                Assert.That(ship, Is.Not.SameAs(previousShip));
                RawImage hull = ship.GetComponentsInChildren<RawImage>().Single();
                Button inspect = retained["Inspect Departure Vessel"].GetComponent<Button>();
                Assert.That(inspect.targetGraphic, Is.SameAs(hull), "Hover must not reference the retired hull.");
                Assert.That(inspect.GetComponent<EventTrigger>().triggers.Count, Is.EqualTo(2), "Rebinding cannot accumulate hover callbacks.");
                RectTransform contact = GameObject.Find("Departure Dock Water Contact").GetComponent<RectTransform>();
                Vector3 waterline = contact.InverseTransformPoint(hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
                Assert.That(waterline.y, Is.EqualTo(0f).Within(.1f));
                Vector2 contactLocal = ShipWaterlineLayout.HullContactLocal(hull);
                foreach (string waterLayer in new[] { "Departure Dock Water Blend", "Departure Dock Water Ripple" })
                {
                    DepartureDockWaterContactGraphic effect = retained[waterLayer].GetComponent<DepartureDockWaterContactGraphic>();
                    Assert.That(effect, Is.Not.Null, waterLayer);
                    Assert.That(effect.transform.parent, Is.SameAs(retained["Voyage Setup"].transform));
                    Assert.That(effect.IsRippleLayer, Is.EqualTo(waterLayer == "Departure Dock Water Ripple"));
                    Assert.That(effect.raycastTarget, Is.False, "Water contact must not intercept vessel inspection.");
                    Vector2 expectedContact = effect.rectTransform.InverseTransformPoint(hull.rectTransform.TransformPoint(contactLocal));
                    Vector2 widthEnd = effect.rectTransform.InverseTransformPoint(hull.rectTransform.TransformPoint(
                        contactLocal + new Vector2(ShipWaterlineLayout.HullWidthLocal(hull), 0f)));
                    Assert.That(Vector2.Distance(effect.HullContact, expectedContact), Is.LessThan(.1f), waterLayer + " must follow the selected hull.");
                    Assert.That(effect.HullWidth, Is.GreaterThan(0f));
                    Assert.That(effect.HullWidth, Is.EqualTo(Mathf.Abs(widthEnd.x - expectedContact.x)).Within(.1f),
                        waterLayer + " must resize to the selected hull without replacing its scene object.");
                }
                Assert.That(GameObject.Find("Setup Title").GetComponent<Text>().text, Does.Contain(controller.Session.SelectedShip.Name));
                Assert.That(GameObject.Find("Departure Dock Ordinary Readout").GetComponent<Text>().text,
                    Does.Contain(controller.Session.SelectedOrdinaryCrew + "/" + controller.Session.CrewCountValue));
                Assert.That(GameObject.Find("Departure Dock Speed Readout").GetComponent<Text>().text,
                    Does.Contain(controller.Session.GetEffectiveSpeedKnots(controller.Session.SelectedOrdinaryCrew).ToString("0.0")));
                Assert.That(RetainedDockChartRevision(controller), Is.EqualTo(chartRevision));
                Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.EqualTo(controller.Session.CanStartOrdinaryVoyage(out _)));
            }
            // Returning to the original ship must not revive a saved event from
            // its original binding, even though the view objects remain alive.
            int ordinary = controller.Session.SelectedOrdinaryCrew;
            oldCrewCallback.Invoke();
            oldSupplyCallback.Invoke();
            yield return null;
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(ordinary));
            Assert.That(GameObject.Find("Departure Basic Supplies Overlay"), Is.Null);
            yield return ClickShipyardControl("DepartureOrdinaryCrewDecreaseButton");
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(ordinary - 1), "The current crew binding must remain usable after a full cycle.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureDockRetained_ChartDraftDecksAndSupplyControlSurviveShipSelection()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            SeedShipPortLocationsForFixture("断锚港", 0, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var decks = new Dictionary<NavalBattleStage, int[]>();
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                decks[stage] = controller.Session.GetBattleDeck(stage).Reverse().ToArray();
                Assert.That(controller.Session.SetBattleDeck(stage, decks[stage]), Is.True);
            }
            yield return ClickShipyardControl("Port Hotspot Voyage");
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            yield return EnterDepartureChartSeed(seed, false);
            InputField input = GameObject.Find("Seed Input").GetComponent<InputField>();
            GameObject chart = GameObject.Find("Departure Chart Selection");
            Button buy = ActiveButton("Departure Buy Provisions");
            var previousBuy = buy.onClick;
            int chartRevision = RetainedDockChartRevision(controller);
            yield return ClickShipyardControl("Departure Next Vessel");
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(1));
            Assert.That(GameObject.Find("Seed Input").GetComponent<InputField>(), Is.SameAs(input));
            Assert.That(GameObject.Find("Departure Chart Selection"), Is.SameAs(chart));
            Assert.That(ActiveButton("Departure Buy Provisions"), Is.SameAs(buy));
            Assert.That(input.text, Is.EqualTo(seed));
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(RetainedDockChartRevision(controller), Is.EqualTo(chartRevision));
            foreach (var deck in decks) CollectionAssert.AreEqual(deck.Value, controller.Session.GetBattleDeck(deck.Key));
            previousBuy.Invoke();
            Assert.That(GameObject.Find("Departure Basic Supplies Overlay"), Is.Null);
            yield return ClickShipyardControl("Departure Buy Provisions");
            Assert.That(GameObject.Find("Departure Basic Supplies Overlay"), Is.Not.Null, "The retained purchase button must use the new vessel binding.");
            yield return ClickShipyardControl("Departure Basic Supplies Close");
            // Editing after the switch exercises the retained input's owner and
            // revision guards instead of merely checking its displayed text.
            input.text = "invalid-retained-chart";
            yield return null;
            Assert.That(controller.Session.IsChartSeedSelected, Is.False);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.False);
            input.text = seed;
            input.onEndEdit.Invoke(seed);
            yield return null;
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.EqualTo(controller.Session.CanStartOrdinaryVoyage(out _)));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureDockRetained_SameFrameChartRequoteAndShipChangeBindTheVisibleSupplyButton()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            SeedShipPortLocationsForFixture("断锚港", 0, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreTreasureProgress progress = controller.Session.CaptureTreasureProgress();
            progress.pirateExperience = 1500;
            controller.Session.RestoreTreasureProgress(progress);
            Assert.That(controller.Session.CanSelectRoute(1, out _), Is.True);
            yield return ClickShipyardControl("Port Hotspot Voyage");
            string original = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            yield return EnterDepartureChartSeed(original);
            InputField input = GameObject.Find("Seed Input").GetComponent<InputField>();
            GameObject setup = GameObject.Find("Voyage Setup");
            Button retiredBuy = ActiveButton("Departure Buy Provisions");
            var retiredCallback = retiredBuy.onClick;
            int revision = RetainedDockChartRevision(controller);
            string changedRoute = CoreVoyageSession.CreateChartSeed(1, "断锚港", "铜钟港");
            // Leave the text pending so the arrow commits it. The resulting
            // route change replaces the quote immediately before changing ships,
            // while Unity still retains the disabled old button until frame end.
            input.SetTextWithoutNotify(changedRoute);
            ActiveButton("Departure Next Vessel").onClick.Invoke();
            Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(1));
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(1));
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(changedRoute));
            Assert.That(GameObject.Find("Voyage Setup"), Is.SameAs(setup));
            Assert.That(GameObject.Find("Seed Input").GetComponent<InputField>(), Is.SameAs(input));
            Assert.That(RetainedDockChartRevision(controller), Is.EqualTo(revision));
            Assert.That(retiredBuy.gameObject.activeSelf, Is.False);
            Button visibleBuy = ActiveButton("Departure Buy Provisions");
            Assert.That(visibleBuy, Is.Not.Null);
            Assert.That(visibleBuy, Is.Not.SameAs(retiredBuy));
            retiredCallback.Invoke();
            Assert.That(GameObject.Find("Departure Basic Supplies Overlay"), Is.Null);
            // Invoke before yielding: a lookup that chose the old same-name
            // child leaves this visible control bound to the previous ship.
            visibleBuy.onClick.Invoke();
            Assert.That(GameObject.Find("Departure Basic Supplies Overlay"), Is.Not.Null);
            yield return null;
            Assert.That(GameObject.Find("Departure Basic Supplies Overlay"), Is.Not.Null);
            yield return ClickShipyardControl("Departure Basic Supplies Close");
            LogAssert.NoUnexpectedReceived();
        }
        private static int RetainedDockChartRevision(CoreGameplayDemoController controller) =>
            (int)typeof(CoreGameplayDemoController).GetField("departureChartRevision", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
    }
}