using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        [UnityTest, Timeout(240000)]
        public IEnumerator ArtOptimization05_SixPortShopsRebindArtWithoutMovingGoodsOrClickAreas()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                string[] ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
                string[] slugs = { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" };
                var layouts = new Dictionary<string, Rect>();
                RawImage retainedBackdrop = null;
                for (int index = 0; index < ports.Length; index++)
                {
                    typeof(CoreVoyageSession).GetProperty("CurrentPort").SetValue(controller.Session, ports[index]);
                    RenderPortArtTest(controller);
                    yield return WaitForPortArtCollection(controller);
                    ActiveButton("Port Hotspot ComponentShop").onClick.Invoke();
                    yield return WaitForPortArtCollection(controller);
                    RawImage backdrop = RequiredResponsive<RawImage>("ComponentShop Background");
                    string path = PortFacilityArtCatalog.RegionalBackgroundResourcePath(
                        PortFacilityArtCatalog.ComponentShopFacilityIndex, ports[index]);
                    Assert.That(backdrop.texture, Is.SameAs(Resources.Load<Texture2D>(path)), ports[index]);
                    Assert.That(backdrop.color, Is.EqualTo(Color.white));
                    Assert.That(backdrop.raycastTarget, Is.False);
                    if (retainedBackdrop == null) retainedBackdrop = backdrop;
                    else Assert.That(backdrop, Is.SameAs(retainedBackdrop), "The cached shell must rebind the current region.");
                    RectTransform panel = RequiredResponsive<RectTransform>("Component Shop Panel");
                    RectTransform[] controls = panel.GetComponentsInChildren<RectTransform>()
                        .Where(rect => rect.name == "Component Shop Showcase" ||
                            rect.GetComponent<Button>() != null && rect.name.StartsWith("Component Shop Category ", StringComparison.Ordinal))
                        .ToArray();
                    Assert.That(controls.Length, Is.EqualTo(6));
                    foreach (RectTransform control in controls)
                    {
                        var bounds = new Rect(control.anchoredPosition, control.sizeDelta);
                        if (index == 0) layouts.Add(control.name, bounds);
                        else Assert.That(bounds, Is.EqualTo(layouts[control.name]), ports[index] + " / " + control.name);
                        Button button = control.GetComponent<Button>();
                        if (button != null) Assert.That(button.interactable, Is.True);
                    }
                    foreach (int scale in new[] { 1, 2 })
                        NavalBattleVisualCapture.Capture(controller,
                            "Art05_Shop_" + slugs[index] + "_" + (scale == 1 ? "1080p" : "4K") + ".png",
                            ports[index] + " / production showroom and unchanged goods/click areas / " + path,
                            "Component Shop Panel", resolutionScale: scale);
                    ActiveButton("Return To Port Hub").onClick.Invoke();
                    yield return WaitForPortArtCollection(controller);
                }
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ArtOptimization06_ElevenExactStoriesShowTheirFullPaintingAndAuthoredDossier()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                string[] ids = {
                    "treasure_clue_bottle", "treasure_clue_whalebone", "treasure_clue_star", "treasure_clue_fogbell", "treasure_clue_twinmoon",
                    "event_reef_shortcut", "event_reef_sunken_boat", "event_trade_floating_auction",
                    "weathered_camp", "gilded_bonded_vault", "moonlit_pearl_moon"
                };
                foreach (string id in ids)
                {
                    bool legacy = id.StartsWith("treasure_clue_", StringComparison.Ordinal);
                    string rank = legacy || id == "weathered_camp" ? "weathered" :
                        id == "gilded_bonded_vault" ? "gilded" : id == "moonlit_pearl_moon" ? "moonlit" : null;
                    CoreVoyageSession session;
                    CoreMapNode node;
                    if (rank != null) session = ArtOptimizationStoryVoyage(id, rank, legacy, out node);
                    else
                    {
                        session = new CoreVoyageSession();
                        session.ConfigureCrewCapabilities(false, false, false, false, false, false);
                        session.SetSeed("ART06-" + id);
                        session.StartVoyage();
                        CoreEncounter encounter;
                        if (id == "event_trade_floating_auction")
                            encounter = CoreVoyageEventCatalog.CreateRandomEncounter(id, CoreVoyageSession.Routes[1].RecommendedPower);
                        else
                        {
                            CoreRouteDefinition route = CoreVoyageSession.Routes[0];
                            var pool = (IReadOnlyList<CoreEncounter>)typeof(CoreVoyageSession)
                                .GetMethod("CreateEncounterPool", BindingFlags.Instance | BindingFlags.NonPublic)
                                .Invoke(session, new object[] { route });
                            encounter = CoreVoyageEventCatalog.EnrichExploration(pool.Single(item => item.Id == id), route.Id);
                        }
                        node = new CoreMapNode { Id = 9906, Position = session.Position, Encounter = encounter };
                        session.Map.Nodes.Add(node);
                    }
                    session.RestoreGameClock(id == "treasure_clue_star" || id == "moonlit_pearl_moon" ? 14d :
                        id == "treasure_clue_twinmoon" ? 10.5d : 0d);
                    typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, session);
                    EnterGeneratedCurseEncounter(session, node);
                    RenderResponsiveBattle(controller);
                    yield return null;
                    yield return WaitForEncounterDossierPresentationCompletion();
                    CoreEncounter story = session.ActiveEncounter;
                    Assert.That(story.Id, Is.EqualTo(id));
                    Assert.That(RequiredResponsive<Text>("Encounter Title").text, Is.EqualTo(story.Name));
                    Assert.That(RequiredResponsive<Text>("Encounter Description").text, Is.EqualTo(story.Description));
                    RawImage picture = RequiredResponsive<RawImage>("Demo Event Art");
                    string path = EncounterIllustrationCatalog.OptimizedResourceRoot + id;
                    Assert.That(picture.texture, Is.SameAs(Resources.Load<Texture2D>(path)));
                    Assert.That(picture.raycastTarget, Is.False);
                    Assert.That(EncounterIllustrationCatalog.TryGetLocation(story, out var location), Is.True);
                    Assert.That(location.Uv, Is.EqualTo(new Rect(0, 0, 1, 1)));
                    // The established cover sampler keeps an eight-pixel mip guard
                    // around the image. Preserve that small centered crop without
                    // accepting an old atlas cell or a stretched composition.
                    Assert.That(picture.uvRect.width * picture.uvRect.height, Is.GreaterThan(.96f), "The dossier must retain the standalone composition after its existing mip guard.");
                    Assert.That(picture.uvRect.center.x, Is.EqualTo(.5f).Within(.0001f));
                    Assert.That(picture.uvRect.center.y, Is.EqualTo(.5f).Within(.0001f));
                    float shownAspect = picture.texture.width * picture.uvRect.width / (picture.texture.height * picture.uvRect.height);
                    float frameAspect = picture.rectTransform.rect.width / picture.rectTransform.rect.height;
                    Assert.That(frameAspect, Is.EqualTo(4f / 3f).Within(.0001f));
                    Assert.That(shownAspect, Is.EqualTo(frameAspect).Within(.0001f), "The authored image must retain its proportions inside the production frame.");
                    if (rank != null) Assert.That(session.ActiveTreasureMap.ContentVersion, Is.EqualTo(legacy ? 0 : 1));
                    string before = JsonUtility.ToJson(session.CaptureVoyageCheckpoint());
                    foreach (int scale in new[] { 1, 2 })
                        NavalBattleVisualCapture.Capture(controller,
                            "Art06_" + id + "_" + (scale == 1 ? "1080p" : "4K") + ".png",
                            story.Name + " / " + (legacy ? "resumed ContentVersion 0 voyage" : "current authored story") + " / " + story.Description,
                            "Encounter Card", resolutionScale: scale);
                    Assert.That(JsonUtility.ToJson(session.CaptureVoyageCheckpoint()), Is.EqualTo(before), "Art capture cannot change story rules or progress.");
                }
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static CoreVoyageSession ArtOptimizationStoryVoyage(string id, string rank, bool legacy, out CoreMapNode node)
        {
            // Legacy clues exist only in resumed ContentVersion 0 voyages. Generate
            // and round-trip that version; do not quietly promote a new departure.
            for (int index = 0; index < 128; index++)
            {
                string seed = "ART06-" + rank + "-" + index;
                var session = new CoreVoyageSession();
                session.RestoreTreasureProgress(new CoreTreasureProgress {
                    pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5),
                    selectedMapId = seed,
                    maps = new[] { new CoreTreasureMapSave { id = seed, seed = seed, rankId = rank, mapGeometryVersion = 4, contentVersion = 1 } }
                });
                session.Cargo.Water = session.Cargo.Food = 150;
                session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Beer = 0;
                session.Cargo.Medicine = session.Cargo.Powder = session.Cargo.Cannonballs = 0;
                Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
                if (legacy) RestoreLegacyTreasureFlow(session);
                node = session.Map.Nodes.FirstOrDefault(item => item.Encounter != null && item.Encounter.Id == id);
                if (node != null) return session;
            }
            node = null;
            Assert.Fail("No deterministic generated voyage contains the authored story " + id);
            return null;
        }
    }
}
