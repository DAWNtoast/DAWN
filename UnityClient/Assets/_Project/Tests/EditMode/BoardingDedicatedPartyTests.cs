using System.IO;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BoardingDedicatedPartyTests
    {
        [TestCase("legendary_pirate_last_horizon", "last_horizon")]
        [TestCase("boss_route_moon_tide_ruins_arcane", "moon_weave")]
        public void ExactProfileRecoversDedicatedPartyWithoutEncounterOrRouteHint(string id, string suffix)
        {
            CoreBattleProfile profile = CoreLegendaryWarships.Find(id)?.Profile ?? CoreRouteBossCatalog.Find(id).Profile;
            var battle = new CoreBattleState { Profile = profile, EnemyName = "unrelated display title" };
            string path = "BoardingBattle/Characters/boarding_enemy_" + suffix + "_v1";
            Assert.That(BoardingBattleArtCatalog.EnemyResourcePath(battle, null, -1), Is.EqualTo(path));
            Assert.That(BoardingBattleArtCatalog.ForEnemy(battle, null, -1), Is.SameAs(Resources.Load<Texture2D>(path)));
        }

        [TestCase("boarding_enemy_last_horizon_v1")]
        [TestCase("boarding_enemy_moon_weave_v1")]
        public void SixMeasuredShadowsFollowActualPngSolesAndGroundedFigure(string name)
        {
            Texture2D asset = Resources.Load<Texture2D>("BoardingBattle/Characters/" + name);
            Assert.That(asset, Is.Not.Null);
            var readable = new Texture2D(2, 2);
            var host = new GameObject("Measured party", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            try
            {
                Assert.That(readable.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(asset))), Is.True);
                int w = readable.width, h = readable.height;
                Assert.That(w, Is.EqualTo(1536));
                Assert.That(h, Is.EqualTo(1024));
                Color32[] pixels = readable.GetPixels32();
                int transparent = 0, visible = 0, lowest = h;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = pixels[y * w + x].a;
                        if (alpha == 0) transparent++;
                        if (alpha < 64) continue;
                        visible++;
                        lowest = Mathf.Min(lowest, y);
                        Assert.That(x > 3 && x < w - 4 && y > 3 && y < h - 4, Is.True,
                            "A boot, head or held prop touches the canvas edge.");
                    }
                Assert.That(transparent, Is.GreaterThan(w * h * .35f), "The sprite needs real empty alpha, not a painted backdrop.");
                Assert.That(visible, Is.GreaterThan(w * h * .30f));
                Vector3[] contacts = BoardingFootShadowProfiles.For(asset, new Rect(0, 0, 1, 1));
                Assert.That(contacts, Has.Length.EqualTo(6));
                // Independent spatial windows separate visible boots, then derive soles from
                // source alpha. This catches a profile copied from the old generic party.
                int[] edges = { 0, 350, 590, 800, 1070, 1300, 1536 };
                for (int boot = 0; boot < 6; boot++)
                {
                    int sole = -1;
                    for (int y = 0; y < h / 4; y++)
                    {
                        int count = 0, run = 0, longest = 0;
                        for (int x = edges[boot]; x < edges[boot + 1]; x++)
                        {
                            if (pixels[y * w + x].a >= 96) { count++; run++; longest = Mathf.Max(longest, run); }
                            else run = 0;
                        }
                        if (count >= 8 && longest >= 6) { sole = y; break; }
                    }
                    Assert.That(sole, Is.GreaterThanOrEqualTo(0), "Missing visible boot " + boot);
                    int lo = w, hi = -1;
                    for (int y = sole; y <= sole + 12; y++)
                        for (int x = edges[boot]; x < edges[boot + 1]; x++)
                            if (pixels[y * w + x].a >= 96) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
                    Assert.That(contacts[boot].y * h, Is.EqualTo(sole + .5f).Within(.6f), "Boot " + boot + " height");
                    Assert.That(contacts[boot].x * w, Is.EqualTo((lo + hi + 1) * .5f).Within(.6f), "Boot " + boot + " center");
                    Assert.That(contacts[boot].z * w, Is.EqualTo(hi - lo + 1).Within(.6f), "Boot " + boot + " width");
                }
                RawImage figure = host.GetComponent<RawImage>();
                figure.texture = asset;
                figure.uvRect = new Rect(0, 0, 1, 1);
                figure.rectTransform.sizeDelta = UiImageAspect.FitSize(asset, figure.uvRect, BoardingBattleLayout.PartySize);
                BoardingBattleLayout.GroundFigure(figure);
                float actualFloor = figure.rectTransform.anchoredPosition.y + figure.rectTransform.rect.yMin +
                    (lowest + .5f) / h * figure.rectTransform.rect.height;
                Assert.That(actualFloor, Is.EqualTo(-BoardingBattleLayout.PartySize.y * .5f).Within(.5f));
                Assert.That(BoardingBattleLayout.PartySize, Is.EqualTo(new Vector2(640, 336)));
                Assert.That(BoardingBattleLayout.PartyPosition(true), Is.EqualTo(new Vector2(350, 123)));
            }
            finally { Object.DestroyImmediate(readable); Object.DestroyImmediate(host); }
        }
    }
}
