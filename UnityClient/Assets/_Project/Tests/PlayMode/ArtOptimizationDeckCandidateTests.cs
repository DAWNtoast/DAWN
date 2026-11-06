using System;
using System.Collections;
using System.IO;
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
        public IEnumerator ArtOptimization04Candidate_DeckArtComparisonPreservesLayoutAt1080And4K()
        {
            string candidatePath = Environment.GetEnvironmentVariable("ART_OPT_DECK_CANDIDATE");
            if (string.IsNullOrWhiteSpace(candidatePath))
                Assert.Ignore("Set ART_OPT_DECK_CANDIDATE to opt into the isolated deck-art comparison.");
            Assert.That(Path.IsPathRooted(candidatePath), Is.True, "The candidate must have an absolute path.");
            candidatePath = Path.GetFullPath(candidatePath);
            Assert.That(File.Exists(candidatePath), Is.True, "The candidate PNG must already exist.");
            Assert.That(Path.GetExtension(candidatePath), Is.EqualTo(".png").IgnoreCase);
            Assert.That(Application.dataPath.Replace('\\', '/').Split('/').Any(segment =>
                string.Equals(segment, "tmp", StringComparison.OrdinalIgnoreCase)), Is.True,
                "Run this opt-in comparison only in the isolated tmp Unity project.");

            Texture2D candidate = null;
            try
            {
                candidate = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                {
                    name = "Art Optimization Deck Candidate (Test Only)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                Assert.That(ImageConversion.LoadImage(candidate, File.ReadAllBytes(candidatePath), true), Is.True);

                using (var storage = new VoyageCargoStorageScope())
                {
                    yield return LoadResponsiveBattleScene();
                    var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
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
                        Texture originalArt = deck.texture;
                        Material originalMaterial = deck.material;
                        Rect originalCrop = deck.uvRect;
                        Color originalLight = deck.color;
                        Vector3 originalDeckPosition = deck.rectTransform.localPosition;
                        Vector2 originalDeckSize = deck.rectTransform.sizeDelta;
                        Assert.That(candidate.width / (float)candidate.height,
                            Is.EqualTo(originalArt.width / (float)originalArt.height).Within(.001f),
                            "Both artworks must use the same normalized crop and aspect ratio.");
                        candidate.filterMode = originalArt.filterMode;
                        candidate.wrapModeU = originalArt.wrapModeU;
                        candidate.wrapModeV = originalArt.wrapModeV;
                        candidate.anisoLevel = originalArt.anisoLevel;
                        candidate.mipMapBias = originalArt.mipMapBias;
                        var poses = SnapshotGroundShadowFigures();
                        var shadows = Object.FindObjectsByType<BoardingContactShadowGraphic>(FindObjectsSortMode.None);
                        Assert.That(poses.Count, Is.GreaterThanOrEqualTo(2));
                        Assert.That(shadows.Length, Is.GreaterThanOrEqualTo(2));
                        int[] contactCounts = shadows.Select(shadow => shadow.ContactCount).ToArray();
                        string rules = GroundShadowRules(session);

                        try
                        {
                            // Compare source art directly; neither side receives the fine-grain shader.
                            deck.material = null;
                            foreach (int scale in new[] { 1, 2 })
                            {
                                foreach (bool useCandidate in new[] { false, true })
                                {
                                    Texture selectedArt = useCandidate ? candidate : originalArt;
                                    deck.texture = selectedArt;
                                    Canvas.ForceUpdateCanvases();
                                    NavalBattleVisualCapture.Capture(controller,
                                        "Art04Candidate_Deck_" + bands[index] + "_" +
                                        (scale == 1 ? "1080p" : "4K") + "_" +
                                        (useCandidate ? "Candidate" : "Original") + ".png",
                                        bands[index] + " / " + (useCandidate ? "candidate PNG" : "runtime original") +
                                        " " + selectedArt.width + "x" + selectedArt.height +
                                        " / same crop, light and static formations / default UI material",
                                        resolutionScale: scale);
                                    Assert.That(deck.texture, Is.SameAs(selectedArt));
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
                        finally
                        {
                            if (deck != null)
                            {
                                deck.texture = originalArt;
                                deck.material = originalMaterial;
                            }
                        }
                        Assert.That(deck.texture, Is.SameAs(originalArt));
                        Assert.That(deck.material, Is.SameAs(originalMaterial));
                    }
                }
            }
            finally
            {
                if (candidate != null) Object.Destroy(candidate);
            }
            yield return null;
            Assert.That(candidate == null, Is.True, "The temporary candidate texture must be released.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
