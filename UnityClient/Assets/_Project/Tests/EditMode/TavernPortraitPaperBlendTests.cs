using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class TavernPortraitPaperBlendTests
    {
        private const int Size = 64;
        private static readonly Color32 Paper = new Color32(224, 190, 133, 255);
        private static readonly Color32 Outline = new Color32(40, 37, 33, 255);

        [Test]
        public void BoundaryConnectedWarmPaperClearsWhileEnclosedPaperRemainsOpaque()
        {
            Color32[] source = Filled(Size, Size, Paper);
            Fill(source, 18, 18, 28, 28, Outline);
            Fill(source, 23, 23, 18, 18, Paper);

            Color32[] mask = BuildCoverage(source);

            Assert.That(mask[At(0, 32)].r, Is.Zero, "Left edge paper");
            Assert.That(mask[At(63, 32)].r, Is.Zero, "Right edge paper");
            Assert.That(mask[At(32, 0)].r, Is.Zero, "Bottom edge paper");
            Assert.That(mask[At(32, 63)].r, Is.Zero, "Top edge paper");
            for (int y = 23; y < 41; y++)
                for (int x = 23; x < 41; x++)
                    Assert.That(mask[At(x, y)].r, Is.EqualTo(255), "Enclosed pale region at " + x + ", " + y);
            Assert.That(mask[At(19, 32)].r, Is.EqualTo(255), "The silhouette cannot be removed to reach an enclosed paper-colored region.");
        }

        [Test]
        public void WhiteClothingAtTheImageEdgeAndAnEnclosedFaceKeepTheirCoverage()
        {
            Color32 whiteClothing = new Color32(240, 240, 235, 255);
            Color32 face = new Color32(158, 96, 56, 255);
            Color32[] source = Filled(Size, Size, Paper);
            Fill(source, 20, 0, 24, 35, whiteClothing);
            Fill(source, 20, 36, 24, 23, Outline);
            Fill(source, 25, 39, 14, 16, face);

            Color32[] mask = BuildCoverage(source);

            for (int index = 0; index < source.Length; index++)
            {
                if (source[index].Equals(whiteClothing) || source[index].Equals(face))
                    Assert.That(mask[index].r, Is.EqualTo(255), "Garment/face pixel " + index + " must not be keyed by brightness.");
            }
            Assert.That(mask[At(0, 0)].r, Is.Zero, "There is removable paper beside the coat at the bottom edge.");
            Assert.That(mask[At(63, 63)].r, Is.Zero);
        }

        [TestCase(255, 255, 255)]
        [TestCase(180, 180, 180)]
        [TestCase(120, 180, 220)]
        public void NonWarmBackgroundIsConservativelyKeptOpaque(int red, int green, int blue)
        {
            Color32[] source = Filled(Size, Size, new Color32((byte)red, (byte)green, (byte)blue, 255));
            Fill(source, 22, 18, 20, 35, Outline);

            Color32[] mask = BuildCoverage(source);

            Assert.That(mask.All(pixel => pixel.r == 255), Is.True,
                "Without a confident warm-paper model, neutral and cool backgrounds must stay intact.");
        }

        [Test]
        public void FeatherIsLimitedToTwoPixelsBesideConfirmedBackgroundAndKeepsDarkEdgesOpaque()
        {
            Color32[] source = Filled(Size, Size, Paper);
            // A shaded, uncertain antialiased fringe surrounds the dark shape.
            // Exact bright-paper pixels may clear completely even beside hair.
            Fill(source, 19, 17, 26, 30, new Color32(187, 158, 114, 255));
            Fill(source, 20, 18, 24, 28, Outline);

            Color32[] mask = BuildCoverage(source);

            int softened = 0;
            for (int index = 0; index < mask.Length; index++)
            {
                if (source[index].Equals(Outline))
                    Assert.That(mask[index].r, Is.EqualTo(255), "Dark silhouette pixel " + index);
                if (mask[index].r == 0 || mask[index].r == 255) continue;
                softened++;
                int x = index % Size;
                int y = index / Size;
                bool besideBackground = false;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) > 2 || x + dx < 0 || x + dx >= Size || y + dy < 0 || y + dy >= Size) continue;
                        if (mask[At(x + dx, y + dy)].r == 0) besideBackground = true;
                    }
                Assert.That(besideBackground, Is.True, "Fringe coverage cannot spread into an uncertain interior at " + x + ", " + y);
            }
            Assert.That(softened, Is.GreaterThan(0), "A silhouette should receive a small antialiased paper fringe.");
        }

        [Test]
        public void PaleWarmPaperBesideDarkHairClearsWithoutLeavingAPaintedHalo()
        {
            // Actual White Shell atlas samples: top-corner paper and the much
            // paler paper beside the carpenter's hair have different chroma.
            Color32 cornerPaper = new Color32(225, 185, 124, 255);
            Color32 haloPaper = new Color32(239, 208, 159, 255);
            Color32[] source = Filled(Size, Size, cornerPaper);
            Fill(source, 10, 12, 44, 42, haloPaper);
            Fill(source, 26, 24, 16, 22, Outline);

            Color32[] mask = BuildCoverage(source);

            for (int index = 0; index < source.Length; index++)
            {
                if (source[index].Equals(haloPaper))
                    Assert.That(mask[index].r, Is.Zero, "Connected halo-paper pixel " + index);
                if (source[index].Equals(Outline))
                    Assert.That(mask[index].r, Is.EqualTo(255), "Dark hair pixel " + index);
            }
        }

        [Test]
        public void CreamHeadwearWithLessYellowChromaRemainsOpaque()
        {
            Color32 cream = new Color32(214, 197, 168, 255);
            Color32[] source = Filled(Size, Size, new Color32(225, 185, 124, 255));
            Fill(source, 18, 22, 28, 25, cream);

            Color32[] mask = BuildCoverage(source);

            for (int index = 0; index < source.Length; index++)
                if (source[index].Equals(cream))
                    Assert.That(mask[index].r, Is.EqualTo(255), "The wider warm-paper tolerance must preserve cream headwear pixel " + index);
        }

        [Test]
        public void CoverageUsesAnIndependentLinearDataArrayWithoutChangingSourceColorsOrAlpha()
        {
            Color32[] source = Filled(Size, Size, Paper);
            Fill(source, 18, 18, 28, 28, Outline);
            Fill(source, 24, 24, 16, 16, new Color32(246, 221, 199, 127));
            Color32[] before = (Color32[])source.Clone();

            Color32[] mask = BuildCoverage(source);

            Assert.That(mask, Is.Not.SameAs(source));
            Assert.That(source, Is.EqualTo(before), "Mask construction must not rewrite original RGB or alpha.");
            Assert.That(mask.Length, Is.EqualTo(source.Length));
            Assert.That(mask.All(pixel => pixel.r == pixel.g && pixel.g == pixel.b && pixel.a == 255), Is.True,
                "Coverage belongs to the RGB data channels; original image alpha is sampled separately by the shader.");
        }

        [Test]
        public void TooFewCornerSamplesLeaveThePortraitOpaque()
        {
            const int smallSize = 16;
            Color32[] source = Filled(smallSize, smallSize, Paper);

            Color32[] mask = BuildCoverage(source, smallSize, smallSize);

            Assert.That(mask.All(pixel => pixel.r == 255), Is.True, "Uncertain corner evidence must not erase portrait pixels.");
        }

        [Test]
        public void DetachedWarmWhiteSpecksClearWhileConnectedHeadwearAndDiagonalWhiteHairStayOpaque()
        {
            Color32 warmWhite = new Color32(244, 235, 210, 255);
            Color32 visible = new Color32(255, 255, 255, 255);
            Color32[] source = Filled(Size, Size, Paper);
            Color32[] mask = Filled(Size, Size, new Color32(0, 0, 0, 255));
            Fill(source, 24, 12, 16, 30, Outline);
            Fill(mask, 24, 12, 16, 30, visible);
            Fill(source, 22, 42, 20, 8, warmWhite);
            Fill(mask, 22, 42, 20, 8, visible);
            source[At(42, 50)] = warmWhite;
            mask[At(42, 50)] = visible;
            Fill(source, 8, 50, 2, 2, warmWhite);
            Fill(mask, 8, 50, 2, 2, visible);
            mask[At(8, 8)] = visible;
            Color32[] original = (Color32[])source.Clone();

            RemoveSpecks(source, mask, Size);

            Assert.That(mask[At(8, 50)].r, Is.Zero, "Detached warm-white residue");
            Assert.That(mask[At(9, 51)].r, Is.Zero);
            Assert.That(mask[At(8, 8)].r, Is.Zero, "Detached paper-colored residue");
            Assert.That(mask[At(42, 50)].r, Is.EqualTo(255), "A diagonal connection still belongs to the white hair.");
            for (int y = 42; y < 50; y++)
                for (int x = 22; x < 42; x++)
                    Assert.That(mask[At(x, y)].r, Is.EqualTo(255), "Attached white headwear");
            Assert.That(source, Is.EqualTo(original), "Speck cleanup must not rewrite source pixels.");
        }

        [TestCase(35, 30, 25)]
        [TestCase(40, 170, 200)]
        [TestCase(210, 150, 70)]
        public void DetachedDarkHairAndColoredAccessoriesArePreserved(int red, int green, int blue)
        {
            Color32 visible = new Color32(255, 255, 255, 255);
            Color32[] source = Filled(Size, Size, Paper);
            Color32[] mask = Filled(Size, Size, new Color32(0, 0, 0, 255));
            Fill(source, 24, 12, 16, 35, Outline);
            Fill(mask, 24, 12, 16, 35, visible);
            Fill(source, 8, 50, 2, 2, new Color32((byte)red, (byte)green, (byte)blue, 255));
            Fill(mask, 8, 50, 2, 2, visible);

            RemoveSpecks(source, mask, Size);

            Assert.That(mask[At(8, 50)].r, Is.EqualTo(255));
            Assert.That(mask[At(9, 51)].r, Is.EqualTo(255));
        }

        [Test]
        public void OneDefiniteInkStrokeProtectsAnOtherwisePaleDetachedComponent()
        {
            const int cellSize = 256;
            Color32 visible = new Color32(255, 255, 255, 255);
            Color32[] source = Filled(cellSize, cellSize, Paper);
            Color32[] mask = Filled(cellSize, cellSize, new Color32(0, 0, 0, 255));
            FillSized(source, cellSize, 80, 30, 90, 180, Outline);
            FillSized(mask, cellSize, 80, 30, 90, 180, visible);
            FillSized(source, cellSize, 10, 10, 8, 8, new Color32(244, 235, 210, 255));
            FillSized(mask, cellSize, 10, 10, 8, 8, visible);
            source[10 * cellSize + 10] = Outline;

            RemoveSpecks(source, mask, cellSize);

            Assert.That(mask[10 * cellSize + 10].r, Is.EqualTo(255));
            Assert.That(mask[17 * cellSize + 17].r, Is.EqualTo(255), "A 98% pale component with a definite dark stroke may be hair or an accessory.");
        }

        [TestCase(64, 2)]
        [TestCase(128, 4)]
        [TestCase(512, 16)]
        public void DetachedSpeckLimitScalesWithCellAreaAndKeepsLargerPaleShapes(int cellSize, int speckSide)
        {
            Color32 visible = new Color32(255, 255, 255, 255);
            Color32 pale = new Color32(244, 235, 210, 255);
            Color32[] source = Filled(cellSize, cellSize, Paper);
            Color32[] mask = Filled(cellSize, cellSize, new Color32(0, 0, 0, 255));
            FillSized(source, cellSize, cellSize / 3, cellSize / 3, cellSize / 3, cellSize / 2, Outline);
            FillSized(mask, cellSize, cellSize / 3, cellSize / 3, cellSize / 3, cellSize / 2, visible);
            FillSized(source, cellSize, 2, 2, speckSide, speckSide, pale);
            FillSized(mask, cellSize, 2, 2, speckSide, speckSide, visible);
            int largerX = cellSize - speckSide - 4;
            FillSized(source, cellSize, largerX, 2, speckSide + 1, speckSide + 1, pale);
            FillSized(mask, cellSize, largerX, 2, speckSide + 1, speckSide + 1, visible);

            RemoveSpecks(source, mask, cellSize);

            Assert.That(mask[2 * cellSize + 2].r, Is.Zero, "A small fragment at the scaled area limit");
            Assert.That(mask[2 * cellSize + largerX].r, Is.EqualTo(255), "A larger pale shape must not be classified as a speck.");
        }

        private static void RemoveSpecks(Color32[] source, Color32[] mask, int cellSize)
        {
            MethodInfo cleanup = typeof(TavernPortraitPaperBlend).GetMethod("RemoveDetachedPaperSpecks", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(cleanup, Is.Not.Null);
            cleanup.Invoke(null, new object[] { source, mask, cellSize, cellSize,
                new Vector3(Paper.r / 255f, Paper.g / 255f, Paper.b / 255f) });
        }

        [Test]
        public void LocalPaperComparisonClearsPaleOuterResidueWithoutShrinkingDarkHair()
        {
            Color32 brightPaper = new Color32(242, 230, 201, 255);
            Color32 fringe = new Color32(244, 235, 210, 255);
            Color32[] source = Filled(Size, Size, brightPaper);
            Color32[] mask = Filled(Size, Size, new Color32(0, 0, 0, 255));
            Fill(source, 20, 18, 24, 30, fringe);
            Fill(mask, 20, 18, 24, 30, new Color32(255, 255, 255, 255));
            Fill(source, 21, 19, 22, 28, Outline);
            Color32[] original = (Color32[])source.Clone();

            RefineFringe(source, mask);

            Assert.That(mask[At(20, 30)].r, Is.Zero, "Pale paper residue adjacent to actual bright paper");
            Assert.That(mask[At(21, 30)].r, Is.EqualTo(255), "Dark contour stays intact");
            Assert.That(source, Is.EqualTo(original));
        }

        [TestCase(244, 235, 210)]
        [TestCase(214, 197, 168)]
        [TestCase(240, 240, 235)]
        public void LocalFringeComparisonPreservesLightClothWithMatchingInterior(int r, int g, int b)
        {
            Color32 cloth = new Color32((byte)r, (byte)g, (byte)b, 255);
            Color32[] source = Filled(Size, Size, new Color32(242, 230, 201, 255));
            Color32[] mask = Filled(Size, Size, new Color32(0, 0, 0, 255));
            Fill(source, 20, 0, 24, 40, cloth);
            Fill(mask, 20, 0, 24, 40, new Color32(255, 255, 255, 255));

            RefineFringe(source, mask);

            for (int i = 0; i < source.Length; i++)
                if (source[i].Equals(cloth)) Assert.That(mask[i].r, Is.EqualTo(255), "Light garment pixel " + i);
        }

        [Test]
        public void LocalFringeComparisonDoesNotExpandIntoUncertainInterior()
        {
            Color32[] source = Filled(Size, Size, new Color32(242, 230, 201, 255));
            Color32[] mask = Filled(Size, Size, new Color32(0, 0, 0, 255));
            Fill(source, 20, 18, 24, 30, Outline);
            Fill(mask, 20, 18, 24, 30, new Color32(255, 255, 255, 255));
            source[At(23, 30)] = new Color32(244, 235, 210, 255);

            RefineFringe(source, mask);

            Assert.That(mask[At(23, 30)].r, Is.EqualTo(255), "A pale interior detail cannot be erased by edge cleanup");
        }

        private static void RefineFringe(Color32[] source, Color32[] mask)
        {
            MethodInfo refine = typeof(TavernPortraitPaperBlend).GetMethod("RefinePaperFringe", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(refine, Is.Not.Null);
            refine.Invoke(null, new object[] { source, mask, Size, Size });
        }

        [Test]
        public void ExplicitPaperOpeningSeedClearsOnlyTheAnnotatedEnclosedPaper()
        {
            Color32[] source = Filled(Size, Size, Paper);
            Fill(source, 18, 18, 28, 28, Outline);
            Fill(source, 23, 23, 18, 18, Paper);
            Color32[] defaultMask = BuildCoverage(source);

            Color32[] seededMask = BuildCoverageWithSeeds(source, new Vector2(0.5f, 0.5f));

            Assert.That(defaultMask[At(32, 32)].r, Is.EqualTo(255), "Unannotated enclosed pale content remains protected by default.");
            for (int y = 23; y < 41; y++)
                for (int x = 23; x < 41; x++)
                    Assert.That(seededMask[At(x, y)].r, Is.Zero, "The explicitly identified empty paper opening");
            Assert.That(seededMask[At(19, 32)].r, Is.EqualTo(255), "The opening seed must not erase its dark boundary.");
        }

        [Test]
        public void MistakenDarkSkinAndInvalidOpeningSeedsLeaveTheDefaultMaskUnchanged()
        {
            Color32[] source = Filled(Size, Size, Paper);
            Fill(source, 18, 18, 28, 28, Outline);
            Fill(source, 23, 23, 18, 18, new Color32(158, 96, 56, 255));
            Color32[] defaultMask = BuildCoverage(source);

            Color32[] seededMask = BuildCoverageWithSeeds(source, new Vector2(0.5f, 0.5f),
                new Vector2(-0.1f, 0.5f), new Vector2(1.1f, 0.5f), new Vector2(0.5f, float.NaN));

            Assert.That(seededMask, Is.EqualTo(defaultMask), "An annotation can only seed valid coordinates whose original pixels match warm paper.");
            Assert.That(seededMask[At(32, 32)].r, Is.EqualTo(255), "A mistaken facial annotation cannot override the source-color check.");
        }

        [Test]
        public void AnnotatedOpeningCanUseLocalPaperWithoutKeyingUnannotatedCreamCloth()
        {
            Color32 bright = new Color32(239, 219, 181, 255);
            Color32[] source = Filled(Size, Size, new Color32(207, 168, 112, 255));
            Fill(source, 18, 18, 28, 28, Outline);
            Fill(source, 30, 30, 4, 4, bright);
            Fill(source, 23, 23, 3, 3, bright);

            Color32[] mask = BuildCoverageWithSeeds(source, new Vector2(0.5f, 0.5f));

            Assert.That(mask[At(32, 32)].r, Is.Zero, "Verified opening with a paler local paper sample");
            Assert.That(mask[At(24, 24)].r, Is.EqualTo(255), "Other enclosed cream details are not background seeds");
            Assert.That(mask[At(29, 32)].r, Is.EqualTo(255), "Dark curl remains intact");
        }

        [Test]
        public void TwilightPaleProfileClearsConnectedBrightPaperBeyondTheCornerColorTolerance()
        {
            Color32 corner = new Color32(203, 170, 118, 255);
            Color32 brightPaper = new Color32(242, 230, 201, 255);
            Color32[] source = Filled(Size, Size, corner);
            Fill(source, 10, 12, 44, 42, brightPaper);
            Fill(source, 26, 24, 16, 22, Outline);
            Color32[] defaultMask = BuildCoverage(source);

            Color32[] profiledMask = BuildWithTwilightPaleProfile(source);

            Assert.That(defaultMask[At(12, 20)].r, Is.EqualTo(255), "Default portraits do not enable a brighter paper key.");
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index].Equals(brightPaper))
                    Assert.That(profiledMask[index].r, Is.Zero, "Confirmed connected bright paper");
                if (source[index].Equals(Outline))
                    Assert.That(profiledMask[index].r, Is.EqualTo(255), "Dark silhouette");
            }
        }

        [TestCase(190, 186, 184)]
        [TestCase(238, 230, 214)]
        [TestCase(214, 197, 168)]
        public void TwilightPaleProfilePreservesGrayWhiteHairCreamClothAndDarkSkin(int red, int green, int blue)
        {
            Color32 paleForeground = new Color32((byte)red, (byte)green, (byte)blue, 255);
            Color32 skin = new Color32(158, 96, 56, 255);
            Color32[] source = Filled(Size, Size, new Color32(203, 170, 118, 255));
            Fill(source, 20, 12, 24, 43, paleForeground);
            Fill(source, 25, 23, 14, 20, skin);

            Color32[] mask = BuildWithTwilightPaleProfile(source);

            for (int index = 0; index < source.Length; index++)
                if (source[index].Equals(paleForeground) || source[index].Equals(skin))
                    Assert.That(mask[index].r, Is.EqualTo(255), "Pale foreground / dark skin pixel " + index);
        }

        [Test]
        public void PalePaperProfileIsRestrictedToTheTwoFullTwilightAtlasCells()
        {
            MethodInfo select = typeof(TavernPortraitPaperBlend).GetMethod("UsesTwilightPalePaperModel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(select, Is.Not.Null);
            foreach (string port in new[] { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" })
                for (int profession = 0; profession < 6; profession++)
                {
                    Rect cell = new Rect((profession % 3) / 3f, profession < 3 ? 0.5f : 0f, 1f / 3f, 0.5f);
                    bool enabled = (bool)select.Invoke(null, new object[] { "crew_" + port + "_portraits_v2", cell });
                    Assert.That(enabled, Is.EqualTo(port == "twilight_tide" && (profession == 0 || profession == 5)), port + " / " + profession);
                }
            Assert.That((bool)select.Invoke(null, new object[] { "crew_twilight_tide_portraits_v2", new Rect(0f, 0.5f, 0.3f, 0.5f) }), Is.False,
                "A cropped face UV must not silently inherit a full-cell paper profile.");
        }

        private static Color32[] BuildWithTwilightPaleProfile(Color32[] source)
        {
            MethodInfo build = typeof(TavernPortraitPaperBlend).GetMethod("BuildCoverageWithPaperProfile", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(build, Is.Not.Null);
            return (Color32[])build.Invoke(null, new object[] { source, Size, Size, null, true });
        }

        private static Color32[] BuildCoverageWithSeeds(Color32[] source, params Vector2[] seeds)
        {
            MethodInfo build = typeof(TavernPortraitPaperBlend).GetMethod("BuildCoverageWithSeeds", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(build, Is.Not.Null);
            return (Color32[])build.Invoke(null, new object[] { source, Size, Size, seeds });
        }

        private static void FillSized(Color32[] source, int stride, int x, int y, int width, int height, Color32 color)
        {
            for (int row = y; row < y + height; row++)
                for (int column = x; column < x + width; column++)
                    source[row * stride + column] = color;
        }

        private static Color32[] BuildCoverage(Color32[] source, int width = Size, int height = Size)
        {
            MethodInfo build = typeof(TavernPortraitPaperBlend).GetMethod("BuildCoverage", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(build, Is.Not.Null, "Keep the CPU matte algorithm independently testable without a graphics device.");
            return (Color32[])build.Invoke(null, new object[] { source, width, height });
        }

        private static Color32[] Filled(int width, int height, Color32 color)
        {
            Color32[] result = new Color32[width * height];
            for (int index = 0; index < result.Length; index++) result[index] = color;
            return result;
        }

        private static void Fill(Color32[] source, int x, int y, int width, int height, Color32 color)
        {
            for (int row = y; row < y + height; row++)
                for (int column = x; column < x + width; column++)
                    source[At(column, row)] = color;
        }

        private static int At(int x, int y) => y * Size + x;
    }
}
