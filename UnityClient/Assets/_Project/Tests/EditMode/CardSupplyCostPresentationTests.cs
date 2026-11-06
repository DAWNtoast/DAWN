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
        [TestCase("card_school_blood_b01", 5)]
        [TestCase("card_school_blood_b02", 3)]
        [TestCase("card_school_blood_b03", 5)]
        [TestCase("card_school_blood_b05", 5)]
        public void OptionalBloodFeeBadgeUsesActualAffordablePayment(string id, int cost)
        {
            var session = Battle(NavalBattleStage.Naval);
            CoreBattleCard card = Card(id);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, cost);
            Assert.That(CardSpecialCostPresentation.CostText(card, session.Battle, session: session), Is.EqualTo("0"));
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, cost + 1);
            Assert.That(CardSpecialCostPresentation.CostText(card, session.Battle, session: session), Is.EqualTo(cost.ToString()));
            Assert.That(CardSpecialCostPresentation.CostText(card), Is.EqualTo(cost.ToString()));
        }

        [Test]
        public void DesperateCannonBadgeRequiresBothDesperationAndAffordablePayment()
        {
            var session = Battle(NavalBattleStage.Naval);
            CoreBattleCard card = Card("card_school_blood_n03");
            foreach (int health in new[] { session.MaxHull, session.MaxHull * 40 / 100, 5 })
            {
                typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, health);
                string expected = health > 5 && (long)health * 100 <= (long)session.MaxHull * 40 ? "5" : "0";
                Assert.That(CardSpecialCostPresentation.CostText(card, session.Battle, session: session), Is.EqualTo(expected));
            }
        }

        [TestCase("card_school_thunder_b03")]
        [TestCase("card_school_thunder_b04")]
        [TestCase("card_school_thunder_n02")]
        [TestCase("card_school_thunder_n03")]
        [TestCase("card_school_thunder_n04")]
        [TestCase("card_school_thunder_n05")]
        [TestCase("card_school_thunder_n07")]
        [TestCase("card_school_thunder_n08")]
        [TestCase("card_boarding_thunder_09")]
        public void NonFinisherThunderCardsHaveNoChainCostBadgeAtAnyRank(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.Empty, id + " +" + rank);
                Assert.That(CardSpecialCostPresentation.CostText(card), Is.Empty, id + " +" + rank);
            }
        }

        [Test]
        public void OnlyTheThreePublishedNavalFinishersDisplayChainCostBadges()
        {
            string[] ids = CoreVoyageSession.CardLibrary
                .Where(card => card.SchoolId == "thunder" && card.IsAllowedInStage(NavalBattleStage.Naval) &&
                    CardSpecialCostPresentation.ResourceType(card) == "chain")
                .Select(card => card.Id).ToArray();
            Assert.That(ids, Is.EquivalentTo(new[] { "card_school_thunder_b06", "card_school_thunder_n06", "card_school_thunder_n09" }));
        }

        [Test]
        public void TideBarrierCostsFourAndPermanentConversionHasNoResourcePaymentAtEveryRank()
        {
            var session = new CoreVoyageSession();
            foreach (string key in new[] { "tide_05", "tide_15" })
            {
                int index = Index(CoreBoardingCardCatalog.FindByKey(key).Id);
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                    Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.EqualTo(key == "tide_05" ? "" : "tide"));
                    Assert.That(CardSpecialCostPresentation.CostText(card), Is.EqualTo(key == "tide_05" ? "" : "4"));
                }
            }
        }

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

        [TestCase("card_school_arcane_n04", "mana")]
        [TestCase("card_school_curse_n03", "curse")]
        [TestCase("card_school_tide_n02", "tide")]
        [TestCase("card_school_tide_n05", "tide")]
        [TestCase("card_school_tide_n06", "tide")]
        [TestCase("card_boarding_arcane_04", "mana")]
        [TestCase("card_boarding_arcane_06", "mana")]
        [TestCase("card_boarding_arcane_11", "mana")]
        [TestCase("card_school_thunder_b06", "chain")]
        [TestCase("card_school_thunder_n06", "chain")]
        [TestCase("card_school_thunder_n09", "chain")]
        [TestCase("card_boarding_thunder_13", "chain")]
        [TestCase("card_boarding_thunder_14", "chain")]
        [TestCase("card_boarding_thunder_15", "chain")]
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
        [TestCase("tide_12", 1, 0, "0")]
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
        [TestCase("tide_11")]
        [TestCase("tide_12")]
        public void NoMedicineFallback_AlsoRemovesTheLinkedSchoolPayment(string key)
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
            Assert.That(CardSpecialCostPresentation.CostText(card, availableMedicine: 0), Is.EqualTo("0"));
            Assert.That(CardSpecialCostPresentation.CostText(card, availableMedicine: 1), Is.Not.EqualTo("0"));
        }

        [TestCase("card_boarding_tide_11", 11, 0)]
        [TestCase("card_boarding_tide_11", 12, 12)]
        [TestCase("card_boarding_tide_12", 15, 0)]
        [TestCase("card_boarding_tide_12", 16, 16)]
        [TestCase("card_special_crew_white_shell_doctor_mirei", 15, 0)]
        [TestCase("card_special_crew_white_shell_doctor_mirei", 16, 16)]
        public void TideHealingBadgesMatchActualPaymentWhenMedicineExists(string id, int tide, int paid)
        {
            var session = Battle(NavalBattleStage.Boarding, true);
            CoreBattleCard card = Card(id);
            session.Cargo.Medicine = 2;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth - 20;
            typeof(CoreVoyageSession).GetMethod("CTBGainTide", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { tide });
            Assert.That(CardSpecialCostPresentation.CostText(card, session.Battle, session: session), Is.EqualTo(paid.ToString()));
            Assert.That(CardSupplyCostPresentation.CostText(card, session), Is.EqualTo(paid > 0 ? "1" : "0"));
            Assert.That(session.Battle.Boarding.TidePower, Is.EqualTo(tide), "Quoting must not spend resources.");
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2));
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Index(id));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.Boarding.TidePower, Is.EqualTo(tide - paid));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(paid > 0 ? 1 : 2));
        }

        [TestCase("card_school_curse_n02")]
        [TestCase("card_boarding_curse_12")]
        [TestCase("card_boarding_curse_14")]
        [TestCase("card_boarding_curse_15")]
        public void CurseRequirementsNeverDisplayAnActualMarkPayment(string id)
        {
            CoreBattleCard card = Card(id);
            Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.Empty);
            Assert.That(CardSpecialCostPresentation.CostText(card), Is.Empty);
            Assert.That(CardSpecialCostPresentation.CostText(card, availableMedicine: 0), Is.Empty);
        }

        [TestCase(0, "0")]
        [TestCase(1, "0")]
        [TestCase(2, "1")]
        public void CurseHealingMedicineBadgeTracksItsMarkRequirement(int marks, string cost)
        {
            CoreVoyageSession session = Battle(NavalBattleStage.Boarding);
            session.Cargo.Medicine = 3;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth - 20;
            typeof(CoreVoyageSession).GetMethod("CTBAddMarks", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { marks });
            Assert.That(CardSupplyCostPresentation.CostText(Card("card_boarding_curse_12"), session,
                NavalBattleStage.Boarding), Is.EqualTo(cost));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(3));
            Assert.That(session.Battle.Boarding.CurseMarks, Is.EqualTo(marks));
        }

        [Test]
        public void FireIgnition_HasNoLifeCostOrSpecialResourceAtAnyRank()
        {
            var session = new CoreVoyageSession();
            int index = System.Linq.Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_boarding_fire_14");
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.Empty);
                Assert.That(CardSpecialCostPresentation.CostText(card), Is.Empty);
            }
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

        [Test]
        public void CrewSupportWithoutSupplyCost_RemainsFreeAtEveryRankAndStageOnTheSameFace()
        {
            const string id = "card_special_crew_gray_gull_cook_pepe";
            GameObject host = new GameObject("Shared Crew Supply Refresh Audit");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
                var session = new CoreVoyageSession();
                int index = Index(id);
                // Exercise the retained native UI refresh directly. The complete
                // card-face artwork has its own asset-backed tests above.
                RectTransform face = new GameObject("Crew Supply Audit Face", typeof(RectTransform)).GetComponent<RectTransform>();
                face.SetParent(host.transform, false);
                var frameObject = new GameObject("Crew Supply Audit Frame", typeof(RectTransform), typeof(CardSchoolFrameGraphic));
                frameObject.transform.SetParent(face, false);
                var oldSupply = new GameObject("Reused Supply View", typeof(RectTransform), typeof(CardSupplyCostView));
                oldSupply.transform.SetParent(face, false);
                MethodInfo refresh = typeof(CoreGameplayDemoController).GetMethod("RefreshCardSupplyCost", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (int rank in new[] { 0, 1, 0, 1 })
                foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                    Assert.That(CardSupplyCostPresentation.ResourceType(card, stage), Is.Empty);
                    // Availability refresh can choose the new texture before
                    // the shared supply/layout refresh runs on a retained face.
                    face.GetComponentInChildren<CardSchoolFrameGraphic>(true).Configure(card.SchoolId, true,
                        card.PowderCost, card.CannonballCost, CardSpecialCostPresentation.ResourceType(card), supplyType: string.Empty);
                    oldSupply.SetActive(true);
                    refresh.Invoke(controller, new object[] { face, card, null, stage });
                    CardSupplyCostView supply = face.GetComponentInChildren<CardSupplyCostView>(true);
                    Assert.That(supply == null || !supply.gameObject.activeSelf, Is.True);
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
        private static CoreVoyageSession Battle(NavalBattleStage stage, bool unlockTide = false)
        {
            var session = new CoreVoyageSession();
            if (unlockTide)
            {
                session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
                Assert.That(session.RestoreCardPackProgress(null, session.PirateLevel), Is.True,
                    "The resource/field fixture starts with a migrated owned school-card collection before departure.");
                session.ConfigureSpecialCardSources(new[] { "crew_white_shell_doctor_mirei" }, new string[0]);
            }
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
