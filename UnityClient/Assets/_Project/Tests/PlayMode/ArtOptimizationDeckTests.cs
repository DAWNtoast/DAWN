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
        [UnityTest, Timeout(180000)]
        public IEnumerator ArtOptimization04_DeckDetailPreservesCropActorsAndRulesAt1080And4K()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Texture2D previousArt = Resources.Load<Texture2D>("BoardingBattle/Backgrounds/boarding_joined_deck_v2");
                Assert.That(previousArt, Is.Not.Null, "Keep the previous approved art for the visual comparison.");
                Texture2D productionArt = BoardingBattleArtCatalog.JoinedDeck();
                Assert.That(BoardingBattleArtCatalog.JoinedDeckResourcePath,
                    Is.EqualTo("BoardingBattle/Backgrounds/boarding_joined_deck_v3"));
                Assert.That(productionArt, Is.Not.Null);
                Assert.That(productionArt.width, Is.EqualTo(1774), "The detail revision must retain native dimensions.");
                Assert.That(productionArt.height, Is.EqualTo(887));
                Assert.That(productionArt.width / (float)productionArt.height, Is.EqualTo(2f).Within(.0001f));
                Assert.That(productionArt.format, Is.EqualTo(TextureFormat.RGBA32));
                Assert.That(productionArt.isReadable, Is.False);
                Assert.That(productionArt.mipmapCount, Is.EqualTo(1));
                AssertArtOptimizationDeckTransparency(productionArt);
                double[] hours = { 0d, 14d };
                string[] bands = { "Day", "Night" };
                for (int index = 0; index < hours.Length; index++)
                {
                    CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, gameHours: hours[index]);
                    ArtOptimizationSet(session, "BattleTerrain", SeaTerrainType.OpenWater);
                    session.Battle.Stage = NavalBattleStage.Boarding;
                    RenderResponsiveBattle(controller);
                    yield return ArtOptimizationWaitForStableBattle(controller);
                    RawImage deck = RequiredResponsive<RawImage>("Boarding Joined Deck");
                    Assert.That(deck.texture, Is.SameAs(productionArt));
                    Assert.That(deck.material, Is.SameAs(deck.defaultMaterial));
                    Rect originalCrop = deck.uvRect;
                    Color originalLight = deck.color;
                    Vector3 originalDeckPosition = deck.rectTransform.localPosition;
                    Vector2 originalDeckSize = deck.rectTransform.sizeDelta;
                    var poses = SnapshotGroundShadowFigures();
                    var shadows = Object.FindObjectsByType<BoardingContactShadowGraphic>(FindObjectsSortMode.None);
                    Assert.That(poses.Count, Is.GreaterThanOrEqualTo(2));
                    Assert.That(shadows.Length, Is.GreaterThanOrEqualTo(2));
                    int[] contactCounts = shadows.Select(shadow => shadow.ContactCount).ToArray();
                    string rules = GroundShadowRules(session);

                    try
                    {
                        foreach (int scale in new[] { 1, 2 })
                        {
                            foreach (bool useProduction in new[] { false, true })
                            {
                                deck.texture = useProduction ? productionArt : previousArt;
                                Canvas.ForceUpdateCanvases();
                                NavalBattleVisualCapture.Capture(controller,
                                    "Art04_Deck_" + bands[index] + "_" + (scale == 1 ? "1080p" : "4K") + "_" +
                                    (useProduction ? "After" : "Before") + ".png",
                                    bands[index] + " / " + (useProduction ? "v3 detail revision" : "previous v2") +
                                    " / 1774x887 source / same crop, light and static formations / default UI material",
                                    resolutionScale: scale);
                                Assert.That(deck.texture, Is.SameAs(useProduction ? productionArt : previousArt));
                                Assert.That(deck.material, Is.SameAs(deck.defaultMaterial));
                                Assert.That(deck.uvRect, Is.EqualTo(originalCrop));
                                Assert.That(deck.color, Is.EqualTo(originalLight));
                                Assert.That(deck.rectTransform.localPosition, Is.EqualTo(originalDeckPosition));
                                Assert.That(deck.rectTransform.sizeDelta, Is.EqualTo(originalDeckSize));
                                foreach (var pose in poses) pose.AssertUnchanged();
                                for (int shadow = 0; shadow < shadows.Length; shadow++)
                                {
                                    Assert.That(shadows[shadow], Is.Not.Null);
                                    Assert.That(shadows[shadow].enabled, Is.True);
                                    Assert.That(shadows[shadow].ContactCount, Is.EqualTo(contactCounts[shadow]));
                                }
                                Assert.That(GroundShadowRules(session), Is.EqualTo(rules));
                            }
                        }
                    }
                    finally { if (deck != null) deck.texture = productionArt; }

                    Assert.That(deck.material, Is.SameAs(deck.defaultMaterial));
                    Assert.That(previousArt, Is.Not.Null);
                    Assert.That(productionArt, Is.Not.Null);
                    Assert.That(deck.texture, Is.SameAs(productionArt));
                    Assert.That(GroundShadowRules(session), Is.EqualTo(rules));
                }
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static void AssertArtOptimizationDeckTransparency(Texture2D source)
        {
            RenderTexture active = RenderTexture.active;
            RenderTexture target = null;
            Texture2D readback = null;
            try
            {
                target = RenderTexture.GetTemporary(source.width, source.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                readback = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readback.Apply(false, false);
                // Source-image coordinates use a top-left origin. Check open sky,
                // two rigging holes, and both railing openings after actual import.
                var transparent = new[] { new Vector2Int(887, 100), new Vector2Int(887, 285),
                    new Vector2Int(275, 100), new Vector2Int(155, 70), new Vector2Int(160, 150),
                    new Vector2Int(500, 320), new Vector2Int(1450, 320) };
                foreach (Vector2Int point in transparent)
                    Assert.That(readback.GetPixel(point.x, source.height - 1 - point.y).a, Is.LessThan(.04f),
                        "Sky and rigging/railing openings must stay transparent at " + point);
                foreach (Vector2Int point in new[] { new Vector2Int(887, 650), new Vector2Int(50, 200) })
                    Assert.That(readback.GetPixel(point.x, source.height - 1 - point.y).a, Is.GreaterThan(.94f),
                        "The deck and mast must remain opaque at " + point);
            }
            finally
            {
                RenderTexture.active = active;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (readback != null) Object.Destroy(readback);
            }
        }
    }
}
