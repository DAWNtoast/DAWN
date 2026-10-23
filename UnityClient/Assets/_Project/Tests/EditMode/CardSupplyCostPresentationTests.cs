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
    public sealed class CardSupplyCostPresentationTests
    {
        [Test]
        public void FieldComponentsDoNotDisplayRetiredSupplyOrSchoolResourceCostsAtAnyRank()
        {
            var session = new CoreVoyageSession();
            foreach (CoreBattleCard basis in CoreSpecialCardCatalog.All.Select(definition => definition.Card)
                .Where(CoreComponentFieldCards.IsFieldCard))
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(basis.Id); rank++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(Index(basis.Id), rank);
                    Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.Empty, card.Id + " +" + rank);
                    Assert.That(CardSupplyCostPresentation.ResourceType(card, NavalBattleStage.Naval), Is.Empty, card.Id + " +" + rank);
                }
        }

        [TestCase("card_school_curse_n03", "curse")]
        [TestCase("card_school_tide_n02", "tide")]
        [TestCase("card_school_tide_n05", "tide")]
        [TestCase("card_school_tide_n06", "tide")]
        [TestCase("card_boarding_arcane_04", "mana")]
        [TestCase("card_boarding_arcane_06", "mana")]
        [TestCase("card_boarding_arcane_11", "mana")]
        public void AllResourceCardsDisplayXOnTheMatchingIconAtEveryRank(string id, string resource)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.EqualTo(resource));
                Assert.That(CardSpecialCostPresentation.CostText(card), Is.EqualTo("X"));
                Assert.That(card.Description, Does.Not.Match(@"X\s*(?:为|是|表示)").And.Not.Contain("选择 1～"));
            }
        }

        [TestCase("card_emergency_repair", "wood")]
        [TestCase("card_school_tide_n03", "wood")]
        [TestCase("card_boarding_basic_06", "medicine")]
        [TestCase("card_boarding_basic_07", "medicine")]
        [TestCase("card_boarding_fire_05", "medicine")]
        [TestCase("card_boarding_arcane_05", "medicine")]
        [TestCase("card_boarding_curse_12", "medicine")]
        [TestCase("card_boarding_tide_11", "medicine")]
        [TestCase("card_boarding_tide_12", "medicine")]
        [TestCase("card_boarding_blood_09", "medicine")]
        [TestCase("card_brace", "")]
        [TestCase("card_boarding_blood_06", "")]
        public void Catalog_SeparatesSupplyFromSchoolResources(string id, string resource)
        {
            CoreBattleCard card = Card(id);
            Assert.That(CardSupplyCostPresentation.ResourceType(card), Is.EqualTo(resource));
            Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.Not.EqualTo("wood"));
            Assert.That(CardSupplyCostPresentation.CostText(card), Is.EqualTo(resource.Length == 0 ? "" : "1"));
        }

        [TestCase(0, 8, "0")]
        [TestCase(1, 0, "0")]
        [TestCase(1, 2, "0")]
        [TestCase(1, 8, "1")]
        public void HullRepairBadge_MatchesTheTransactionWithoutSpendingOnRead(int wood, int wound, string expected)
        {
            CoreVoyageSession session = Battle(NavalBattleStage.Naval);
            session.Cargo.Wood = wood;
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull - wound);
            CoreBattleCard card = Card("card_emergency_repair");
            string amount = CardSupplyCostPresentation.CostText(card, session, NavalBattleStage.Naval);
            Assert.That(amount, Is.EqualTo(expected));
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Index(card.Id));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(wood - session.Cargo.Wood, Is.EqualTo(int.Parse(amount)));
        }

        [TestCase("basic_06", 0, 8, "0")]
        [TestCase("basic_06", 1, 2, "0")]
        [TestCase("basic_06", 1, 8, "1")]
        [TestCase("basic_07", 1, 0, "1")]
        [TestCase("tide_12", 1, 0, "1")]
        [TestCase("arcane_05", 0, 8, "0")]
        [TestCase("arcane_05", 1, 8, "1")]
        public void BoardingMedicineBadge_RespectsFallbackAndPayOnCast(string key, int medicine, int wound, string expected)
        {
            CoreVoyageSession session = Battle(NavalBattleStage.Boarding);
            session.Cargo.Medicine = medicine;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth - wound;
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
            Assert.That(CardSupplyCostPresentation.CostText(card, session, NavalBattleStage.Boarding), Is.EqualTo(expected));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine));
        }

        [TestCase("arcane_05")]
        [TestCase("curse_12")]
        [TestCase("tide_11")]
        [TestCase("tide_12")]
        public void NoMedicineFallback_AlsoRemovesTheLinkedSchoolPayment(string key)
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
            Assert.That(CardSpecialCostPresentation.CostText(card, availableMedicine: 0), Is.EqualTo("0"));
            Assert.That(CardSpecialCostPresentation.CostText(card, availableMedicine: 1), Is.Not.EqualTo("0"));
        }

        [Test]
        public void FireLifePayment_IsShownAsASpecialResource()
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey("fire_14");
            Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.EqualTo("health"));
            Assert.That(CardSpecialCostPresentation.CostText(card), Is.EqualTo("2"));
        }

        [Test]
        public void SharedRepair_UsesTheDisplayedStageAndDoesNotDependOnPortInventory()
        {
            var card = new CoreBattleCard("supply_stage_test", "阶段治疗", "维修", 1,
                0, 0, 5, 0, 0, "", NavalBattleStages.Ship | NavalBattleStages.Boarding);
            var session = new CoreVoyageSession();
            session.Cargo.Wood = session.Cargo.Medicine = 0;
            Assert.That(CardSupplyCostPresentation.ResourceType(card, NavalBattleStage.Naval), Is.EqualTo("wood"));
            Assert.That(CardSupplyCostPresentation.ResourceType(card, NavalBattleStage.Boarding), Is.EqualTo("medicine"));
            Assert.That(CardSupplyCostPresentation.CostText(card, session, NavalBattleStage.Naval), Is.EqualTo("1"));
            Assert.That(CardSupplyCostPresentation.CostText(card, session, NavalBattleStage.Boarding), Is.EqualTo("1"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BattleDeckPreview_ExplicitStageOverridesTheRunningBattle(bool sharedCard)
        {
            GameObject host = new GameObject("Supply Cost Stage Audit");
            host.SetActive(false);
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
                CoreVoyageSession session = Battle(NavalBattleStage.Naval);
                session.Cargo.Medicine = 0;
                typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, session);
                CoreBattleCard card = sharedCard
                    ? new CoreBattleCard("supply_stage_test", "阶段治疗", "维修", 1, 0, 0, 5, 0, 0, "",
                        NavalBattleStages.Ship | NavalBattleStages.Boarding)
                    : Card("card_boarding_arcane_05");
                MethodInfo factory = typeof(CoreGameplayDemoController).GetMethod("CreateCollectionCardFace", BindingFlags.Instance | BindingFlags.NonPublic);
                RectTransform face = (RectTransform)factory.Invoke(controller, new object[] {
                    host.transform, card, "Stage Audit", "Stage Art", "Stage Name", 1f, null, "Stage Effect" });
                MethodInfo refresh = typeof(CoreGameplayDemoController).GetMethod("RefreshCardSupplyCost", BindingFlags.Instance | BindingFlags.NonPublic);
                refresh.Invoke(controller, new object[] { face, card, session.Battle, NavalBattleStage.Boarding });
                CardSupplyCostView supply = face.GetComponentInChildren<CardSupplyCostView>(true);
                Assert.That(supply, Is.Not.Null);
                Assert.That(supply.gameObject.activeSelf, Is.True, "Boarding-only medicine stays visible in the boarding tab during a naval battle.");
                Assert.That(supply.ResourceType, Is.EqualTo("medicine"));
                Assert.That(supply.Amount.text, Is.EqualTo("1"), "A different-stage preview shows its standard cost rather than quoting the running battle.");
                Assert.That(CardSchoolArtCatalog.FrameResourcePath(card), Does.EndWith("-medicine"));
                Assert.That(face.GetComponentInChildren<CardSchoolFrameGraphic>().mainTexture,
                    Is.SameAs(CardSchoolArtCatalog.Frame(card)), "Boarding medicine is painted into its complete frame.");
                if (sharedCard)
                {
                    RawImage icon = supply.GetComponentInChildren<RawImage>(true);
                    foreach (NavalBattleStage displayedStage in new[] { NavalBattleStage.Naval, NavalBattleStage.Boarding, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                    {
                        refresh.Invoke(controller, new object[] { face, card, session.Battle, displayedStage });
                        bool wood = displayedStage == NavalBattleStage.Naval;
                        Assert.That(supply.ResourceType, Is.EqualTo(wood ? "wood" : "medicine"));
                        Assert.That(face.GetComponentInChildren<CardSchoolFrameGraphic>().mainTexture,
                            Is.SameAs(CardSchoolArtCatalog.Frame(card, displayedStage)));
                        Assert.That(CardSchoolArtCatalog.FrameResourcePath(card, displayedStage),
                            Does.EndWith(wood ? "frame-neutral-ap-wood" : "frame-neutral-ap-medicine"));
                        Assert.That(icon.enabled, Is.False, "The baked supply must not receive a duplicate live icon.");
                        Assert.That(supply.Amount.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                        Assert.That(supply.Amount.fontSize, Is.EqualTo(24));
                        AssertFaceCalibration(face, card, displayedStage);
                    }
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase("card_emergency_repair", "wood")]
        [TestCase("card_school_tide_n03", "wood")]
        [TestCase("card_boarding_basic_06", "medicine")]
        [TestCase("card_boarding_fire_05", "medicine")]
        [TestCase("card_boarding_arcane_05", "medicine")]
        [TestCase("card_boarding_curse_12", "medicine")]
        [TestCase("card_boarding_tide_11", "medicine")]
        [TestCase("card_boarding_blood_09", "medicine")]
        [TestCase("card_brace", "")]
        public void ProductionCardFace_UsesBakedSupplyIconWithVisibleCenteredAmount(string id, string resource)
        {
            GameObject host = new GameObject("Supply Cost Face Audit");
            host.SetActive(false);
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
                MethodInfo factory = typeof(CoreGameplayDemoController).GetMethod("CreateCollectionCardFace", BindingFlags.Instance | BindingFlags.NonPublic);
                RectTransform face = (RectTransform)factory.Invoke(controller, new object[] {
                    host.transform, Card(id), "Audit", "Audit Art", "Audit Name", 1f, null, "Audit Effect" });
                CardSupplyCostView supply = face.GetComponentInChildren<CardSupplyCostView>(true);
                if (resource.Length == 0) { Assert.That(supply, Is.Null); return; }
                Assert.That(supply, Is.Not.Null);
                Assert.That(supply.ResourceType, Is.EqualTo(resource));
                Assert.That(supply.Amount.text, Is.EqualTo("1"));
                Assert.That(supply.transform.Find("Supply Frame"), Is.Null, "The slot belongs to the complete authored card frame.");
                RawImage icon = supply.GetComponentInChildren<RawImage>();
                Assert.That(icon.texture, Is.SameAs(ItemIconCatalog.ForCargo(resource).Texture));
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(icon.enabled, Is.False, "The sole resource icon belongs to the complete PNG.");
                Assert.That(face.GetComponentInChildren<CardSchoolFrameGraphic>().mainTexture.name, Does.EndWith("-" + resource));
                Assert.That(supply.Amount.fontSize, Is.EqualTo(24));
                Assert.That(supply.Amount.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero),
                    "Supply cost overlays the painted icon just like the energy amount overlays its gem.");
                RectTransform amountRect = supply.Amount.rectTransform;
                TextGenerator generator = supply.Amount.cachedTextGenerator;
                Assert.That(generator.Populate(supply.Amount.text, supply.Amount.GetGenerationSettings(amountRect.rect.size)), Is.True);
                Assert.That(generator.vertexCount, Is.GreaterThanOrEqualTo(4), "A nonempty value must generate visible digit vertices.");
                var glyph = generator.verts.Take(generator.vertexCount).Select(vertex => vertex.position).ToArray();
                Assert.That(glyph.Max(vertex => vertex.x) - glyph.Min(vertex => vertex.x), Is.GreaterThan(0f));
                Assert.That(glyph.Max(vertex => vertex.y) - glyph.Min(vertex => vertex.y), Is.GreaterThan(0f));
                Assert.That(((RectTransform)supply.transform).anchoredPosition.y, Is.LessThan(CardFaceLayout.SpecialCostIconPosition.y));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase("card_cook_rations")]
        [TestCase("card_special_crew_gray_gull_cook_pepe")]
        public void SharedCrewSupply_ChangesWithAuthoredRankAndDisplayedStageOnTheSameFace(string id)
        {
            GameObject host = new GameObject("Shared Crew Supply Refresh Audit");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
                var session = new CoreVoyageSession();
                int index = Index(id);
                MethodInfo factory = typeof(CoreGameplayDemoController).GetMethod("CreateCollectionCardFace", BindingFlags.Instance | BindingFlags.NonPublic);
                RectTransform face = (RectTransform)factory.Invoke(controller, new object[] {
                    host.transform, session.GetBattleCardAtLevel(index, 0), "Crew Audit", "Crew Art", "Crew Name", 1f, null, "Crew Effect" });
                MethodInfo refresh = typeof(CoreGameplayDemoController).GetMethod("RefreshCardSupplyCost", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (int rank in new[] { 0, 1, 0, 1 })
                foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                    bool pays = id != "card_cook_rations" || rank > 0;
                    string supplyType = pays ? stage == NavalBattleStage.Boarding ? "medicine" : "wood" : "";
                    // Availability refresh can choose the new texture before
                    // the shared supply/layout refresh runs on a retained face.
                    face.GetComponentInChildren<CardSchoolFrameGraphic>(true).Configure(card.SchoolId, true,
                        card.PowderCost, card.CannonballCost, CardSpecialCostPresentation.ResourceType(card), supplyType: supplyType);
                    refresh.Invoke(controller, new object[] { face, card, null, stage });
                    string expectedFrame = "frame-neutral-ap" + (pays ? "-" + supplyType : "");
                    Assert.That(face.GetComponentInChildren<CardSchoolFrameGraphic>(true).mainTexture.name, Is.EqualTo(expectedFrame));
                    CardSupplyCostView supply = face.GetComponentInChildren<CardSupplyCostView>(true);
                    if (!pays) Assert.That(supply == null || !supply.gameObject.activeSelf, Is.True);
                    else
                    {
                        Assert.That(supply.gameObject.activeSelf, Is.True);
                        Assert.That(supply.ResourceType, Is.EqualTo(supplyType));
                        Assert.That(supply.GetComponentInChildren<RawImage>(true).enabled, Is.False);
                        Assert.That(supply.Amount.text, Is.EqualTo("1"));
                        Assert.That(supply.Amount.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                    }
                    AssertFaceCalibration(face, card, stage);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void AssertFaceCalibration(RectTransform face, CoreBattleCard card, NavalBattleStage stage)
        {
            CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>(true);
            Rect bounds = CardSchoolArtCatalog.FrameBounds(card, stage);
            Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(bounds.size));
            Assert.That(frame.rectTransform.anchoredPosition, Is.EqualTo(bounds.center));
            foreach (RawImage image in face.GetComponentsInChildren<RawImage>(true))
                if (image.GetComponentInParent<CardSupplyCostView>(true) == null)
                    Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(CardFaceLayout.IllustrationLayout(image).center));
            foreach (CardOrnamentGraphic ornament in face.GetComponentsInChildren<CardOrnamentGraphic>(true))
                Assert.That(ornament.rectTransform.anchoredPosition, Is.EqualTo(ornament.Kind == CardOrnamentKind.NameRibbon
                    ? CardFaceLayout.NamePlateLayout(ornament.rectTransform)
                    : CardSchoolArtCatalog.CostPosition(card, ornament.Kind, stage)));
        }

        private static CoreBattleCard Card(string id) => CoreVoyageSession.CardLibrary.Single(value => value.Id == id);
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static CoreVoyageSession Battle(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            session.SetSeed("CARD-SUPPLY-FRAME");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("card_supply", "费用回归敌舰", "海战", "卡面费用", true, false, 1, 500), false });
            session.Battle.Stage = stage;
            session.Battle.Energy = 10;
            return session;
        }
    }
}
