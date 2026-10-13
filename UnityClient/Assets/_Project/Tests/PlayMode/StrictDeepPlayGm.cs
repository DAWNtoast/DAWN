using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private sealed partial class StrictDeepDriver
        {
            private bool inspectedAllowedGmRefresh;

            public IEnumerator RefreshAllowedMarketIfNeeded(bool force = false)
            {
                Assert.That(NoGmFirstTreasure, Is.False, "The no-GM acceptance cannot refresh stock through F10.");
                yield return OpenStrictMarket();
                bool depleted = VisibleMarketRemaining("water") < 100 || VisibleMarketRemaining("food") < 100 ||
                    new[] { "wood", "medicine", "beer", "iron", "cloth" }.Any(key => VisibleMarketRemaining(key) == 0);
                if (!force && !depleted) { yield return EnsureHub(); yield break; }
                decimal gold = Session.ProfileGold;
                string cargo = StrictGmCargoSnapshot(), warehouse = Session.PortWarehouse.Serialize();
                string treasure = JsonUtility.ToJson(Session.CaptureTreasureProgress());
                InputSettings original = InputSystem.settings;
                HideFlags flags = original.hideFlags;
                if (flags == HideFlags.HideAndDontSave) original.hideFlags = HideFlags.DontSave;
                InputSettings temporary = Object.Instantiate(original);
                temporary.hideFlags = HideFlags.DontSave;
                temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                temporary.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings = temporary;
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                try
                {
                    keyboard.MakeCurrent();
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F10));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    Assert.That(controller.IsGameMasterOpen, Is.True);
                    yield return ClickDestinationTestGraphic(GameObject.Find("Game Master Unlock"), Vector2.zero);
                    yield return new WaitForSecondsRealtime(.45f);
                    Record("Allowed GM market restock: " + Session.CurrentPort);
                    yield return Click("Game Master Refresh Market");
                    Shot("Allowed_GM_Market_Restock");
                    yield return Click("Close Game Master");
                    Assert.That(Session.ProfileGold, Is.EqualTo(gold));
                    Assert.That(StrictGmCargoSnapshot(), Is.EqualTo(cargo));
                    Assert.That(Session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
                    Assert.That(JsonUtility.ToJson(Session.CaptureTreasureProgress()), Is.EqualTo(treasure));
                    Assert.That(VisibleMarketRemaining("water"), Is.GreaterThanOrEqualTo(100));
                    Assert.That(VisibleMarketRemaining("food"), Is.GreaterThanOrEqualTo(100));
                }
                finally
                {
                    if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                    InputSystem.settings = original; original.hideFlags = flags;
                    Object.Destroy(temporary);
                }
                yield return EnsureHub();
            }

            public IEnumerator InspectAllowedGmRefresh()
            {
                Assert.That(NoGmFirstTreasure, Is.False, "The no-GM acceptance cannot inspect or refresh the GM shop.");
                Assert.That(inspectedAllowedGmRefresh, Is.False, "The allowed refresh inspection runs only once.");
                inspectedAllowedGmRefresh = true;
                yield return EnsureHub();
                yield return Click("Port Hotspot ComponentShop");
                var previous = new List<string>();
                yield return ReadStrictComponentShelf("Before", previous);

                InputSettings original = InputSystem.settings;
                HideFlags flags = original.hideFlags;
                if (flags == HideFlags.HideAndDontSave) original.hideFlags = HideFlags.DontSave;
                InputSettings temporary = Object.Instantiate(original);
                temporary.hideFlags = HideFlags.DontSave;
                temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                temporary.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings = temporary;
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                try
                {
                    keyboard.MakeCurrent();
                    for (int refresh = 1; refresh <= 2; refresh++)
                    {
                        decimal gold = Session.ProfileGold;
                        string cargo = StrictGmCargoSnapshot();
                        string treasure = JsonUtility.ToJson(Session.CaptureTreasureProgress());
                        string warehouse = Session.PortWarehouse.Serialize();
                        string port = Session.CurrentPort;
                        yield return null;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F10));
                        yield return null;
                        yield return null;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        yield return null;
                        Assert.That(controller.IsGameMasterOpen, Is.True, "GM must open through actual F10 input.");
                        yield return ClickDestinationTestGraphic(GameObject.Find("Game Master Unlock"), Vector2.zero);
                        Assert.That(GameObject.Find("Game Master Unlock").GetComponent<Toggle>().isOn, Is.True);
                        Record("Allowed GM shop refresh " + refresh + " before: " + PortText("Game Master Status")?.text);
                        yield return Click("Game Master Refresh Shop");
                        Record("Allowed GM shop refresh " + refresh + " result: " + PortText("Game Master Status")?.text);
                        yield return new WaitForSecondsRealtime(.45f);
                        yield return Click("Close Game Master");
                        Assert.That(controller.IsGameMasterOpen, Is.False);
                        Assert.That(Session.ProfileGold, Is.EqualTo(gold), "Shop refresh must not grant money.");
                        Assert.That(StrictGmCargoSnapshot(), Is.EqualTo(cargo), "Shop refresh must not alter carried cargo.");
                        Assert.That(JsonUtility.ToJson(Session.CaptureTreasureProgress()), Is.EqualTo(treasure), "Shop refresh must not alter treasure progression.");
                        Assert.That(Session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
                        Assert.That(Session.CurrentPort, Is.EqualTo(port));
                        var current = new List<string>();
                        yield return ReadStrictComponentShelf("After_" + refresh, current);
                        // Observe ineffective refreshes too: this acceptance run reports
                        // design defects without manufacturing different shop inventory.
                        Record("GM refresh comparison " + refresh + ": exact visible offers unchanged=" + previous.SequenceEqual(current) +
                            "; before=" + string.Join(" || ", previous) + "; after=" + string.Join(" || ", current));
                        previous = current;
                    }
                }
                finally
                {
                    if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                    InputSystem.settings = original;
                    original.hideFlags = flags;
                    Object.Destroy(temporary);
                }
                yield return EnsureHub();
            }

            private string StrictGmCargoSnapshot()
            {
                VoyageCargo cargo = Session.Cargo;
                return string.Join(",", new[] { cargo.Water, cargo.Food, cargo.Beer, cargo.Medicine,
                    cargo.Wood, cargo.Iron, cargo.Cloth, cargo.Powder, cargo.Cannonballs,
                    cargo.Gold, cargo.Fame, cargo.Gem, cargo.AncientMaterial, cargo.Relic,
                    cargo.BlackPearl, cargo.AncientCompass, cargo.Lantern, cargo.Curse });
            }

            private IEnumerator ReadStrictComponentShelf(string label, List<string> result)
            {
                while (PortButton("Component Shop Previous Display")?.IsInteractable() == true)
                    yield return Click("Component Shop Previous Display");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int page = 0; page < 10; page++)
                {
                    Shot("GM_Shop_" + label + "_Page_" + page, "Component Shop Panel");
                    string[] offers = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                        .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Component Shop Offer ", StringComparison.Ordinal))
                        .OrderBy(button => button.transform.position.x).Select(button => button.name).ToArray();
                    Assert.That(offers, Is.Not.Empty, "The real showroom must expose its offers.");
                    foreach (string name in offers)
                    {
                        Assert.That(seen.Add(name), Is.True, "Showroom paging repeated an offer.");
                        Button offer = PortButton(name);
                        string plaque = string.Join("; ", offer.GetComponentsInChildren<Text>()
                            .Where(text => text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
                            .Select(text => text.name + "=" + text.text));
                        string visible = name + " | " + plaque;
                        if (offer.IsInteractable())
                        {
                            // The offer root deliberately has no hit surface. Click its
                            // visible plaque, still requiring the real Button handler.
                            RawImage sign = offer.GetComponentsInChildren<RawImage>().First(image => image.name.StartsWith("Component Shop Plaque ", StringComparison.Ordinal));
                            Vector2 point = ((RectTransform)offer.transform).InverseTransformPoint(sign.rectTransform.TransformPoint(sign.rectTransform.rect.center));
                            Record("Inspect actual shop offer: " + name);
                            yield return ClickDestinationTestGraphic(offer.gameObject, point);
                            Assert.That(PortText("Component Detail Name"), Is.Not.Null);
                            Assert.That(PortText("Component Detail Quality"), Is.Not.Null);
                            visible += " | name=" + PortText("Component Detail Name").text +
                                " | rarity/slot=" + PortText("Component Detail Meta").text +
                                " | quality=" + PortText("Component Detail Quality").text;
                            yield return Click("Return To Port Hub");
                            Assert.That(PortButton(name), Is.Not.Null);
                        }
                        else visible += " | detail unavailable: sold offer";
                        result.Add(visible);
                        Record("GM shelf " + label + ": " + visible);
                    }
                    if (PortButton("Component Shop Next Display")?.IsInteractable() != true) yield break;
                    yield return Click("Component Shop Next Display");
                }
                Assert.Fail("Unexpectedly large or cyclic component showroom.");
            }
        }
    }
}
