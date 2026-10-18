using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class CardSchoolPresentationTests
    {
        [Test]
        public void WesternCardArt_GivesEveryActiveLegacyCardItsOwnIllustrationWithoutChangingArtIds()
        {
            CardSchoolArtCatalog.ReleaseLoadedTextures();
            CoreBattleCard[] cards = CoreVoyageSession.CardLibrary.Where(card => card.AllowedStages != NavalBattleStages.None &&
                !CardSchoolArtCatalog.HasSchoolIllustration(card) && !card.Id.StartsWith("card_boarding_")).ToArray();
            Assert.That(cards.Length, Is.GreaterThan(0));
            Assert.That(cards.Select(card => card.ArtId).Distinct().Count(), Is.LessThan(cards.Length),
                "This audit must cover older cards that shared atlas cells.");
            var textures = new System.Collections.Generic.HashSet<Texture2D>();
            foreach (CoreBattleCard card in cards)
            {
                string originalArtId = card.ArtId;
                string reusedId = card.Id == "card_energy_order" ? "card_brace" :
                    card.Id == "card_tactical_draw" ? "card_full_sail_turn" : null;
                Texture2D texture = CardSchoolArtCatalog.Illustration(card, null);
                Assert.That(texture, Is.Not.Null, card.Id);
                Assert.That(texture.name, Is.EqualTo(reusedId ?? card.Id), card.Id + " resolves to its dedicated art or an explicit starter reuse.");
                if (reusedId == null) Assert.That(textures.Add(texture), Is.True, card.Id + " reused another card's illustration.");
                Assert.That(card.ArtId, Is.EqualTo(originalArtId), "Presentation must not rewrite the saved card definition.");
                Assert.That(CardSchoolArtCatalog.DedicatedIllustrationPath(card), Is.EqualTo("CardArt/Western/" + (reusedId ?? card.Id)));
                Assert.That(CardSchoolArtCatalog.NormalizeSchool(card.SchoolId), Is.EqualTo("neutral"), card.Id);
                Assert.That(texture.width, Is.EqualTo(1024), card.Id + " imported texture budget");
                Assert.That(texture.height, Is.EqualTo(1024), card.Id + " imported texture budget");
                Rect uv = CardSchoolArtCatalog.IllustrationUv(card, null);
                Assert.That(uv, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), card.Id + " keeps the complete original source");
                Assert.That(texture.width * uv.width / (texture.height * uv.height),
                    Is.EqualTo(CardFaceLayout.IllustrationBounds.width / CardFaceLayout.IllustrationBounds.height).Within(.0001f), card.Id + " must not stretch.");
            }
        }

        [TestCase("card_turn_port")]
        [TestCase("card_turn_starboard")]
        [TestCase("card_turn_about")]
        [TestCase("card_sail_forward")]
        public void RetiredNavalManoeuvres_ResolveToRetainedApproachIllustration(string cardId)
        {
            CardSchoolArtCatalog.ReleaseLoadedTextures();
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(candidate => CoreVoyageSession.CardLibrary[candidate].Id == cardId);
            Assert.That(CoreVoyageSession.CardLibrary[index].AllowedStages, Is.EqualTo(NavalBattleStages.None));
            CoreBattleCard card = new CoreVoyageSession().GetBattleCard(index);
            Assert.That(card.Id, Is.EqualTo("card_approach"));
            string expectedPath = "CardArt/Western/card_approach";
            Texture2D completeArt = Resources.Load<Texture2D>(expectedPath);
            Assert.That(completeArt, Is.Not.Null, expectedPath + " must be shipped as a complete resource.");
            Assert.That(card.ArtId, Is.EqualTo("card_full_sail_turn"));
            Assert.That(CardSchoolArtCatalog.WesternIllustrationPath(card), Is.EqualTo(expectedPath));
            Assert.That(CardSchoolArtCatalog.DedicatedIllustrationPath(card), Is.EqualTo(expectedPath));
            Assert.That(CardSchoolArtCatalog.HasDedicatedArt(card), Is.True);
            Assert.That(CardSchoolArtCatalog.Illustration(card, null), Is.SameAs(completeArt));
            Assert.That(CardSchoolArtCatalog.IllustrationUv(card, null), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        }

        [Test]
        public void WesternCardArt_MissingReplacementKeepsLegacyAtlasFallbackForCompatibleCards()
        {
            var card = new CoreBattleCard("card_unshipped_art_compatibility_probe", "兼容检查", "防御", 1,
                0, 3, 0, 0, 0, "格挡 +3。", NavalBattleStages.Ship, artId: "card_brace");
            var legacy = new LegacyDemoArtCatalog();
            Assert.That(CardSchoolArtCatalog.WesternIllustrationPath(card),
                Is.EqualTo("CardArt/Western/card_unshipped_art_compatibility_probe"));
            Assert.That(CardSchoolArtCatalog.DedicatedIllustrationPath(card),
                Is.EqualTo("CardArt/Western/card_unshipped_art_compatibility_probe"));
            Assert.That(CardSchoolArtCatalog.HasDedicatedArt(card), Is.False);
            Assert.That(CardSchoolArtCatalog.Illustration(card, null), Is.Null,
                "Unapproved reuse must remain missing in the strict packaged-art check.");
            Assert.That(CardSchoolArtCatalog.Illustration(card, legacy), Is.SameAs(legacy.CardSheet));
            Assert.That(CardSchoolArtCatalog.IllustrationUv(card, legacy),
                Is.EqualTo(CardFaceLayout.IllustrationUv(legacy.CardUv("card_brace"))));
            Assert.That(card.ArtId, Is.EqualTo("card_brace"));
        }

        [Test]
        public void SchoolCatalog_HasDistinctAuthoredArtAndAllThirtySixIntegratedFrameVariants()
        {
            CardSchoolArtCatalog.ReleaseLoadedTextures();
            CoreBattleCard[] schoolCards = CoreVoyageSession.CardLibrary.Where(card => card.AllowedStages != NavalBattleStages.None &&
                CardSchoolArtCatalog.NormalizeSchool(card.SchoolId) != "neutral").ToArray();
            var boardingIds = new System.Collections.Generic.HashSet<string>(CoreBoardingCardCatalog.All.Select(card => card.Id));
            CoreBattleCard[] expansion = CoreNavalSchoolExpansionCatalog.All.ToArray();
            var expansionIds = new System.Collections.Generic.HashSet<string>(expansion.Select(card => card.Id));
            CoreBattleCard[] activeExpansion = schoolCards.Where(card => expansionIds.Contains(card.Id)).ToArray();
            Assert.That(expansion, Has.Length.EqualTo(27));
            Assert.That(expansionIds, Has.Count.EqualTo(27), "Every naval expansion card needs a distinct stable identity.");
            Assert.That(activeExpansion, Has.Length.EqualTo(27));
            Assert.That(activeExpansion.Select(card => card.Id), Is.EquivalentTo(expansionIds),
                "All naval expansion cards must remain active and classified in a known school.");
            CoreBattleCard[] cards = schoolCards.Where(card => !boardingIds.Contains(card.Id) && !expansionIds.Contains(card.Id)).ToArray();
            Assert.That(cards, Has.Length.EqualTo(132), "Six schools each contain ten base, two crew, five epic and five legendary cards.");
            Assert.That(cards.Select(card => card.ArtId).Distinct().Count(), Is.EqualTo(132), "Each card needs its own authored illustration.");
            Assert.That(schoolCards, Has.Length.EqualTo(249));
            var originalArtIds = new System.Collections.Generic.HashSet<string>(cards.Select(card => card.ArtId));
            Assert.That(originalArtIds.SetEquals(schoolCards.Select(card => card.ArtId)), Is.True,
                "Boarding and naval expansion cards must reuse the complete approved set of 132 school illustrations.");
            var legacy = new LegacyDemoArtCatalog();
            foreach (CoreBattleCard card in cards.Concat(expansion))
            {
                Assert.That(CardSchoolArtCatalog.HasDedicatedArt(card), Is.True, card.Id + " must resolve to a complete approved image.");
                Texture2D art = CardSchoolArtCatalog.Illustration(card, legacy);
                Assert.That(art, Is.Not.Null, card.Id + " / " + card.ArtId);
                Assert.That(art.width, Is.EqualTo(1024), card.Id + " preserves high-density card illustration detail");
                Assert.That(art.height, Is.EqualTo(1024), card.Id + " preserves high-density card illustration detail");
                Assert.That(art.name, Is.EqualTo(card.ArtId), card.Id + " must not resolve to a generic atlas or a reused school image.");
                Rect uv = CardSchoolArtCatalog.IllustrationUv(card, legacy);
                Assert.That(uv, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), card.Id + " keeps the complete original source");
                Assert.That(uv.xMin, Is.GreaterThanOrEqualTo(0f)); Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(uv.xMax, Is.LessThanOrEqualTo(1f)); Assert.That(uv.yMax, Is.LessThanOrEqualTo(1f));
                Assert.That(art.width * uv.width / (art.height * uv.height),
                    Is.EqualTo(CardFaceLayout.IllustrationBounds.width / CardFaceLayout.IllustrationBounds.height).Within(.0001f), card.Id + " artwork must not stretch.");
            }
            var frames = new System.Collections.Generic.HashSet<Texture2D>();
            foreach (string school in CardSchoolArtCatalog.SchoolIds.Concat(new[] { "neutral" }))
            {
                foreach (int powder in new[] { 0, 1 })
                    foreach (int cannonball in new[] { 0, 1 })
                    {
                        string variant = CardSchoolArtCatalog.FrameVariant(powder, cannonball);
                        Texture2D frame = CardSchoolArtCatalog.Frame(school, powder, cannonball);
                        Assert.That(frame, Is.Not.Null, school + " / " + variant);
                        Assert.That(frame.name, Is.EqualTo("frame-" + school + "-" + variant));
                        Assert.That(CardSchoolArtCatalog.FrameResourcePath(school, powder, cannonball),
                            Is.EqualTo("CardSchools/Frames/Integrated/frame-" + school + "-" + variant));
                        Assert.That(frames.Add(frame), Is.True, "Each school and cost combination needs its own complete PNG.");
                    }
            }
            Assert.That(frames, Has.Count.EqualTo(28));
            foreach (var special in new[] { new[] { "arcane", "mana" }, new[] { "blood", "health" },
                new[] { "curse", "curse" }, new[] { "thunder", "chain" } })
                foreach (int cost in new[] { 0, 1 })
                {
                    string variant = cost == 0 ? "ap" : "both";
                    Texture2D frame = CardSchoolArtCatalog.Frame(special[0], cost, cost, special[1]);
                    Assert.That(frame, Is.Not.Null, special[0] + " / " + variant + " / " + special[1]);
                    Assert.That(frame.name, Is.EqualTo("frame-" + special[0] + "-" + variant + "-" + special[1]));
                    Assert.That(frames.Add(frame), Is.True, "Special payments require their own complete PNG.");
                }
            Assert.That(frames, Has.Count.EqualTo(36));
            Assert.That(CardSchoolArtCatalog.Frame((string)null), Is.SameAs(CardSchoolArtCatalog.Frame("neutral")));
            Assert.That(CardSchoolArtCatalog.Frame("unrecognized"), Is.SameAs(CardSchoolArtCatalog.Frame("neutral")));
        }

        [Test]
        public void BoardingDesigns_ReuseExistingCompleteIllustrationsAndAvailableFrames()
        {
            Assert.That(CoreBoardingCardCatalog.All.Count, Is.EqualTo(103));
            foreach (CoreBattleCard card in CoreBoardingCardCatalog.All)
            {
                Texture2D illustration = CardSchoolArtCatalog.Illustration(card, null);
                Assert.That(illustration, Is.Not.Null, card.Id + " must reuse an existing image");
                Assert.That(illustration.name, Is.EqualTo(card.ArtId), card.Id);
                Assert.That(CardSchoolArtCatalog.IllustrationUv(card, null), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), card.Id);
                Assert.That(CardSchoolArtCatalog.Frame(card), Is.Not.Null, card.Id + " / " + CardSpecialCostPresentation.ResourceType(card));
            }
            Assert.That(CoreBoardingCardCatalog.All.Select(card => card.ArtId).Distinct().Count(), Is.LessThan(103),
                "The new set intentionally reuses existing art, as requested.");
        }

        [Test]
        public void ProductionCardFactory_KeepsLiveCostsTextAndIllustrationIndependentOfTheSchoolFrame()
        {
            GameObject host = new GameObject("School Card Production Face Audit");
            try
            {
                host.SetActive(false);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                MethodInfo factory = typeof(CoreGameplayDemoController).GetMethod("CreateCollectionCardFace", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(factory, Is.Not.Null);
                foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary.Where(card => card.AllowedStages != NavalBattleStages.None))
                {
                    RectTransform face = (RectTransform)factory.Invoke(controller, new object[] { host.transform, card,
                        "Audit", "Audit Art", "Audit Name", 1f, null, "Audit Effect" });
                    try
                    {
                        CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
                        Assert.That(frame, Is.Not.Null, card.Id);
                        Assert.That(frame.SchoolId, Is.EqualTo(CardSchoolArtCatalog.NormalizeSchool(card.SchoolId)), card.Id);
                        Assert.That(frame.mainTexture, Is.SameAs(CardSchoolArtCatalog.Frame(card)), card.Id);
                        Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(CardSchoolArtCatalog.FrameBounds(card).size), card.Id);
                        Assert.That(frame.rectTransform.anchoredPosition, Is.EqualTo(CardSchoolArtCatalog.FrameBounds(card).center), card.Id);
                        Assert.That(frame.raycastTarget, Is.False, card.Id);
                        Assert.That(face.GetComponentInChildren<BattleCardFrameGraphic>(), Is.Null, card.Id + " must not retain a second functional-category rim.");
                        RawImage art = face.GetComponentInChildren<RawImage>();
                        Rect artWindow = CardFaceLayout.IllustrationLayout(art);
                        Vector2 artSize = art.rectTransform.sizeDelta;
                        Assert.That(artSize.x / artSize.y, Is.EqualTo((float)art.texture.width / art.texture.height).Within(.0001f), card.Id);
                        Assert.That(artSize.x, Is.LessThanOrEqualTo(artWindow.width + .001f), card.Id);
                        Assert.That(artSize.y, Is.LessThanOrEqualTo(artWindow.height + .001f), card.Id);
                        Assert.That(Mathf.Min(Mathf.Abs(artSize.x - artWindow.width), Mathf.Abs(artSize.y - artWindow.height)), Is.LessThan(.001f), card.Id);
                        Assert.That(art.rectTransform.anchoredPosition, Is.EqualTo(artWindow.center), card.Id);
                        Text[] fields = face.GetComponentsInChildren<Text>();
                        Text name = fields.Single(value => value.name == "Audit Name");
                        Text effect = fields.Single(value => value.name == "Audit Effect");
                        Assert.That(name.text, Is.EqualTo(CardFaceLayout.DisplayName(card)), card.Id);
                        NavalBattleStage previewStage = card.IsAllowedInStage(NavalBattleStage.Boarding) ? NavalBattleStage.Boarding :
                            card.IsAllowedInStage(NavalBattleStage.Naval) ? NavalBattleStage.Naval : NavalBattleStage.Preparation;
                        CardDescriptionTextTests.AssertVisibleWordsAndClauseBoundaries(
                            CardFaceLayout.FormatDescription(controller.Session.BattleCardFaceDescription(card, previewStage)),
                            effect.text, card.Id);
                        Assert.That(effect.text, Does.Not.Contain("人接舷实际").And.Not.Contain("当前炮击").And.Not.Contain("无药基础"), card.Id);
                        Assert.That(effect.preferredHeight, Is.LessThanOrEqualTo(CardFaceLayout.EffectSize.y + .1f), card.Id);
                        Assert.That(fields.Single(value => value.name == "Audit Energy Cost").text, Is.EqualTo(card.Energy.ToString()));
                        CardOrnamentGraphic energy = face.GetComponentsInChildren<CardOrnamentGraphic>().Single(item => item.Kind == CardOrnamentKind.Energy);
                        Assert.That(energy.IsMarkerOnly, Is.True, "Cost symbols are already painted into the frame PNG.");
                        Assert.That(energy.transform.GetSiblingIndex(), Is.GreaterThan(frame.transform.GetSiblingIndex()), "The fee badge must remain above the border.");
                    }
                    finally { Object.DestroyImmediate(face.gameObject); }
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(0, 0, "ap")]
        [TestCase(0, 1, "cannonball")]
        [TestCase(1, 0, "powder")]
        [TestCase(1, 1, "both")]
        [TestCase(3, 2, "both")]
        public void FrameVariant_SelectsResourcePresenceInsteadOfCombiningQuantities(int powder, int cannonballs, string variant)
        {
            Assert.That(CardSchoolArtCatalog.FrameVariant(powder, cannonballs), Is.EqualTo(variant));
        }

        [Test]
        public void AllActiveCards_SelectAnExistingIntegratedFrameFromActualCostsIncludingSpecialPayments()
        {
            var paths = new System.Collections.Generic.HashSet<string>();
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary.Where(card => card.AllowedStages != NavalBattleStages.None))
            {
                string special = CardSpecialCostPresentation.ResourceType(card);
                string expected = "CardSchools/Frames/Integrated/frame-" + CardSchoolArtCatalog.NormalizeSchool(card.SchoolId) +
                    "-" + CardSchoolArtCatalog.FrameVariant(card.PowderCost, card.CannonballCost) +
                    (special.Length == 0 || special == "wood" || special == "tide" ? "" : "-" + special);
                Assert.That(CardSchoolArtCatalog.FrameResourcePath(card), Is.EqualTo(expected), card.Id);
                Assert.That(CardSchoolArtCatalog.Frame(card), Is.Not.Null, card.Id + " needs a complete cost-matching frame.");
                paths.Add(expected);
            }
            Assert.That(paths.Any(path => path.EndsWith("-mana")), Is.True);
            Assert.That(paths.Any(path => path.EndsWith("-health")), Is.True);
            Assert.That(paths.Any(path => path.EndsWith("-curse")), Is.True);
            Assert.That(paths.Any(path => path.EndsWith("-chain")), Is.True);
        }

        [TestCase(1f)]
        [TestCase(.5f)]
        public void FrameMesh_RendersTheWholeAuthoredPngAsOneQuadWithFullUv(float scale)
        {
            var host = new GameObject("School Frame Geometry Audit", typeof(RectTransform), typeof(CardSchoolFrameGraphic));
            var mesh = new Mesh();
            try
            {
                var graphic = host.GetComponent<CardSchoolFrameGraphic>();
                MethodInfo populate = typeof(CardSchoolFrameGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null);
                foreach (var variant in IntegratedFrameVariants())
                {
                            Rect bounds = CardSchoolArtCatalog.FrameBounds(variant.School, variant.Powder, variant.Cannonball, variant.Special);
                            graphic.rectTransform.sizeDelta = bounds.size * scale;
                            graphic.rectTransform.anchoredPosition = bounds.center * scale;
                            graphic.Configure(variant.School, true, variant.Powder, variant.Cannonball, variant.Special);
                            using (var helper = new VertexHelper()) { populate.Invoke(graphic, new object[] { helper }); helper.FillMesh(mesh); }
                            Assert.That(mesh.vertexCount, Is.EqualTo(4), variant.School + " cannot slice or reconstruct the frame.");
                            Assert.That(mesh.triangles, Has.Length.EqualTo(6));
                            Assert.That(mesh.uv, Is.EquivalentTo(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }));
                            Rect rect = graphic.rectTransform.rect;
                            Assert.That(mesh.vertices.Select(vertex => new Vector2(vertex.x, vertex.y)),
                                Is.EquivalentTo(new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
                                    new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin) }));
                            Assert.That(graphic.mainTexture, Is.SameAs(CardSchoolArtCatalog.Frame(variant.School, variant.Powder, variant.Cannonball, variant.Special)));
                    Assert.That(graphic.raycastTarget, Is.False);
                }
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(host); }
        }

        [Test]
        public void AllThirtySixFrameLayouts_KeepCalibratedNumbersAtCornersInsideTheVisualEnvelope()
        {
            var variants = IntegratedFrameVariants().ToArray();
            Assert.That(variants, Has.Length.EqualTo(36));
            foreach (var variant in variants)
            {
                CoreSchoolMechanic mechanic = variant.Special == "mana" ? CoreSchoolMechanic.ArcaneRelease :
                    variant.Special == "health" ? CoreSchoolMechanic.BloodRisk :
                    variant.Special == "curse" ? CoreSchoolMechanic.CurseDetonate : CoreSchoolMechanic.ThunderFinish;
                var card = new CoreBattleCard("frame_layout_probe", "角位检查", "策略", 1, 0, 0, 0, 0, variant.Powder,
                    "", NavalBattleStages.Ship, cannonballCost: variant.Cannonball, schoolId: variant.School,
                    schoolEffect: variant.Special.Length == 0 ? null : new CoreSchoolCardEffect(mechanic, threshold: 2));
                Rect bounds = CardSchoolArtCatalog.FrameBounds(card);
                Assert.That(bounds.width, Is.GreaterThan(0f));
                Assert.That(bounds.height, Is.GreaterThan(0f));
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(CardFaceLayout.VisualBounds.xMin));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(CardFaceLayout.VisualBounds.xMax));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(CardFaceLayout.VisualBounds.yMin));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(CardFaceLayout.VisualBounds.yMax));
                foreach (CardOrnamentKind kind in new[] { CardOrnamentKind.Energy, CardOrnamentKind.Gunpowder,
                    CardOrnamentKind.Cannonball, CardOrnamentKind.Mana })
                {
                    Vector2 expected = kind == CardOrnamentKind.Energy ? CardFaceLayout.EnergyIconPosition :
                        kind == CardOrnamentKind.Gunpowder ? CardFaceLayout.PowderIconPosition :
                        kind == CardOrnamentKind.Cannonball ? CardFaceLayout.CannonballIconPosition : CardFaceLayout.SpecialCostIconPosition;
                    Vector2 actual = CardSchoolArtCatalog.CostPosition(card, kind);
                    Assert.That(Vector2.Distance(actual, expected), Is.LessThanOrEqualTo(12f), variant + " / " + kind);
                    Assert.That(actual, Is.Not.EqualTo(Vector2.zero), variant + " / " + kind);
                    Assert.That(bounds.Contains(actual), Is.True, variant + " / " + kind);
                }
            }
        }

        [Test]
        public void MissingNestedCostCalibration_FromRealJsonUtilityDeserializationKeepsDefaultCorner()
        {
            System.Type entryType = typeof(CardSchoolArtCatalog).GetNestedType("FrameLayoutEntry", BindingFlags.NonPublic);
            object entry = JsonUtility.FromJson("{\"id\":\"frame-fire-both\",\"rect\":{\"x\":-137,\"y\":-201.5,\"width\":272,\"height\":389}}", entryType);
            MethodInfo corner = typeof(CardSchoolArtCatalog).GetMethod("CostCorner", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var pair in new[] { ("energyPosition", CardFaceLayout.EnergyIconPosition),
                ("powderPosition", CardFaceLayout.PowderIconPosition), ("cannonballPosition", CardFaceLayout.CannonballIconPosition),
                ("specialPosition", CardFaceLayout.SpecialCostIconPosition) })
            {
                object missingPoint = entryType.GetField(pair.Item1).GetValue(entry);
                Assert.That((Vector2)corner.Invoke(null, new[] { missingPoint, (object)pair.Item2 }), Is.EqualTo(pair.Item2), pair.Item1);
            }
        }

        [TestCase(CardOrnamentKind.Energy)]
        [TestCase(CardOrnamentKind.Gunpowder)]
        [TestCase(CardOrnamentKind.Cannonball)]
        [TestCase(CardOrnamentKind.Mana)]
        public void CalibratedCostCorners_KeepBothUpperCrystalsEqualAndNumbersInsideTheCompleteFrame(CardOrnamentKind kind)
        {
            Vector2 position = kind == CardOrnamentKind.Energy ? CardFaceLayout.EnergyIconPosition :
                kind == CardOrnamentKind.Gunpowder ? CardFaceLayout.PowderIconPosition :
                kind == CardOrnamentKind.Cannonball ? CardFaceLayout.CannonballIconPosition : CardFaceLayout.SpecialCostIconPosition;
            Vector2 size = kind == CardOrnamentKind.Energy ? CardFaceLayout.EnergyIconSize :
                kind == CardOrnamentKind.Gunpowder ? CardFaceLayout.PowderIconSize :
                kind == CardOrnamentKind.Cannonball ? CardFaceLayout.CannonballIconSize : CardFaceLayout.SpecialCostIconSize;
            Rect marker = new Rect(position - size * .5f, size);
            Rect frame = CardFaceLayout.DefaultFrameBounds;
            Assert.That(marker.xMin, Is.GreaterThanOrEqualTo(frame.xMin));
            Assert.That(marker.xMax, Is.LessThanOrEqualTo(frame.xMax));
            Assert.That(marker.yMin, Is.GreaterThanOrEqualTo(frame.yMin));
            Assert.That(marker.yMax, Is.LessThanOrEqualTo(frame.yMax));
            Assert.That(CardFaceLayout.SpecialCostIconSize, Is.EqualTo(CardFaceLayout.EnergyIconSize));
            Assert.That(CardFaceLayout.NameSize, Is.EqualTo(new Vector2(174f, 26f)), "The taller frame cannot reduce the name region");
            Assert.That(CardFaceLayout.EffectSize, Is.EqualTo(new Vector2(176f, 94f)), "The taller frame cannot reduce the effect region");
        }

        private static System.Collections.Generic.IEnumerable<(string School, int Powder, int Cannonball, string Special)> IntegratedFrameVariants()
        {
            foreach (string school in CardSchoolArtCatalog.SchoolIds.Concat(new[] { "neutral" }))
                foreach (int powder in new[] { 0, 1 })
                    foreach (int cannonball in new[] { 0, 1 })
                        yield return (school, powder, cannonball, "");
            foreach (var special in new[] { ("arcane", "mana"), ("blood", "health"), ("curse", "curse"), ("thunder", "chain") })
                foreach (int cost in new[] { 0, 1 }) yield return (special.Item1, cost, cost, special.Item2);
        }

        [TestCase("fire")] [TestCase("arcane")] [TestCase("curse")] [TestCase("tide")]
        [TestCase("thunder")] [TestCase("blood")] [TestCase("neutral")]
        public void IntegratedFrameInterior_EmitsNoExtraProceduralRimOrTitlePaper(string school)
        {
            var surfaceHost = new GameObject("No Procedural Card Rim", typeof(RectTransform), typeof(BattleCardSurfaceGraphic));
            var ribbonHost = new GameObject("No Procedural Title Paper", typeof(RectTransform), typeof(CardOrnamentGraphic));
            var mesh = new Mesh();
            try
            {
                BattleCardSurfaceGraphic surface = surfaceHost.GetComponent<BattleCardSurfaceGraphic>();
                surface.rectTransform.sizeDelta = CardFaceLayout.CardSize;
                surface.Configure("炮击", true, school);
                MethodInfo surfacePopulate = typeof(BattleCardSurfaceGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null);
                using (var helper = new VertexHelper())
                {
                    surfacePopulate.Invoke(surface, new object[] { helper });
                    Assert.That(helper.currentVertCount, Is.Zero, school + " frame PNG owns the sole card rim");
                }
                CardOrnamentGraphic ribbon = ribbonHost.GetComponent<CardOrnamentGraphic>();
                ribbon.rectTransform.sizeDelta = CardFaceLayout.NamePlateSize;
                ribbon.Configure(CardOrnamentKind.NameRibbon, "炮击", school);
                MethodInfo ribbonPopulate = typeof(CardOrnamentGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null);
                using (var helper = new VertexHelper()) { ribbonPopulate.Invoke(ribbon, new object[] { helper }); helper.FillMesh(mesh); }
                Assert.That(mesh.vertexCount, Is.Zero, school + " complete PNG owns the title paper and its ornament");
                Assert.That(mesh.triangles, Is.Empty);
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(surfaceHost); Object.DestroyImmediate(ribbonHost); }
        }

        [Test]
        public void SchoolHud_ShowsManaCurseTicksAndCurrentTideWithoutRetiredReactionState()
        {
            var battle = new CoreBattleState();
            SetSchoolState(battle, "Mana", 2);
            SetSchoolState(battle, "TemporaryMana", 4);
            SetSchoolState(battle, "ThunderChain", 4);
            SetSchoolState(battle, "CurseMarks", 3);
            SetSchoolState(battle, "CurseTurns", 2);
            SetSchoolState(battle, "CurseWeaknessPercent", 5);
            SetSchoolState(battle, "CurseDamagePerMark", 2);
            SetSchoolState(battle, "CurseNextTickBonus", 1);
            SetSchoolState(battle, "TidePower", 18);
            SetSchoolState(battle, "TidePaidBlock", 6);
            SetSchoolState(battle, "TideCounter", 18);
            SetSchoolState(battle, "TideCounterMinimum", 6);
            SetSchoolState(battle, "TideCounterTurns", 1);
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod("BattleSchoolStatusText", BindingFlags.Static | BindingFlags.NonPublic);
            string active = (string)method.Invoke(null, new object[] { battle });
            Assert.That(active, Does.Contain("法力 2/3 · 临时 +4"));
            Assert.That(active, Does.Contain("本回合雷链 4/6"));
            Assert.That(active, Does.Contain("敌附咒 3 层 · 2 回合"));
            Assert.That(active, Does.Contain("下次敌回合末扣血 9"));
            Assert.That(active, Does.Contain("潮势 18/20 · 付木护盾 6"));
            Assert.That(active, Does.Not.Contain("敌出伤").And.Not.Contain("潮反伤").And.Not.Contain("须吸收"));
            SetSchoolState(battle, "TideCounterTurns", 0);
            string expired = (string)method.Invoke(null, new object[] { battle });
            Assert.That(expired, Does.Not.Contain("潮反伤"), "An expired reaction must not appear active just because its stored damage is nonzero.");
        }

        [Test]
        public void BloodTooltip_QuotesLifePaymentAndRejectsLethalPaymentExplicitly()
        {
            var host = new GameObject("School Blood Payment UI Audit");
            try
            {
                host.SetActive(false);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller.Session, new object[] { NavalBattleStage.Boarding });
                CoreBattleCard card = CoreBoardingCardCatalog.FindByKey("blood_01");
                int payment = 3;
                controller.Session.Battle.PlayerBoardingHealth = payment + 7;
                MethodInfo method = typeof(CoreGameplayDemoController).GetMethod("SchoolCardKeywordHelp", BindingFlags.Instance | BindingFlags.NonPublic);
                string permitted = (string)method.Invoke(controller, new object[] { card });
                Assert.That(VisibleEffectWithoutLineBreaks(permitted), Does.Contain("支付 " + payment + "；支付后 7"));
                Assert.That(permitted, Does.Not.Contain("生命不足"));
                controller.Session.Battle.PlayerBoardingHealth = payment;
                string blocked = (string)method.Invoke(controller, new object[] { card });
                Assert.That(VisibleEffectWithoutLineBreaks(blocked), Does.Contain("生命不足，不能出牌"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static string VisibleEffectWithoutLineBreaks(string text) =>
            text.Replace("<b>", string.Empty).Replace("</b>", string.Empty)
                .Replace("\r", string.Empty).Replace("\n", string.Empty);

        private static void SetSchoolState(CoreBattleState battle, string property, int value) =>
            typeof(CoreSchoolBattleState).GetProperty(property).SetValue(battle.Schools, value);
    }
}
