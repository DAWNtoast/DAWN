using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class CardResourceBadgeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private CoreGameplayDemoController controller;
        private RectTransform fixture;

        [SetUp]
        public void CreateDisposablePreview()
        {
            // Edit-mode Awake returns before profile loading. The editor preview
            // builds its own fixture data; this suite never invokes UI callbacks
            // or the controller's profile save path.
            Assert.That(Application.isPlaying, Is.False);
            host = new GameObject("Card Resource Badge Audit");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
            fixture = new GameObject("Isolated Card Face", typeof(RectTransform)).GetComponent<RectTransform>();
            fixture.SetParent(host.transform, false);
            fixture.sizeDelta = CardFaceLayout.CardSize;
        }

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase("Battle", 0)] [TestCase("Battle", 1)] [TestCase("Battle", 2)] [TestCase("Battle", 3)]
        [TestCase("Workshop", 0)] [TestCase("Workshop", 1)] [TestCase("Workshop", 2)] [TestCase("Workshop", 3)]
        [TestCase("Collection", 0)] [TestCase("Collection", 1)] [TestCase("Collection", 2)] [TestCase("Collection", 3)]
        public void EveryLiveCardAndUpgrade_AllThreeFacesMatchItsResourceDefinition(string entry, int level)
        {
            CoreVoyageSession session = CreateBattleSession(level);
            UseSession(session);
            int checkedCards = 0, cannonballCards = 0, dualResourceCards = 0, resourceFreeCards = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard data = session.GetBattleCard(index);
                if (data == null) continue;
                Assert.That(data.UpgradeLevel, Is.EqualTo(level), entry + " / " + data.Id + " must exercise the requested rank");
                RectTransform face = CreateFace(entry, index);
                AssertResourceBadges(face, data, entry + " / " + data.Id + " / rank " + level,
                    entry == "Battle" ? session.Battle : null);
                checkedCards++;
                if (data.CannonballCost > 0) cannonballCards++;
                if (data.CannonballCost > 0 && data.PowderCost > 0) dualResourceCards++;
                if (data.CannonballCost == 0 && data.PowderCost == 0) resourceFreeCards++;
                ClearFixture();
            }
            Assert.That(checkedCards, Is.EqualTo(CoreVoyageSession.CardLibrary.Count(card => card.AllowedStages != NavalBattleStages.None)));
            Assert.That(checkedCards, Is.GreaterThan(100), "Include the complete ordinary and special card catalog.");
            Assert.That(cannonballCards, Is.GreaterThan(0));
            Assert.That(dualResourceCards, Is.GreaterThan(0));
            Assert.That(resourceFreeCards, Is.GreaterThan(0), "Cards without fixed ammunition costs must not gain a false badge.");
        }

        [TestCase("Battle")]
        [TestCase("Workshop")]
        [TestCase("Collection")]
        public void TutorialFaces_UseTheSameLoadingAndFiringResourceRulesAsNormalBattle(string entry)
        {
            CoreVoyageSession session = CoreGuidedBossScriptTests.StartBoss(5, 3);
            Assert.That(session.IsGuidedBossBattle, Is.True);
            UseSession(session);
            int loadIndex = FindCard("card_load_shot");
            CoreBattleCard lessonLoad = session.GetBattleCard(loadIndex);
            Assert.That(lessonLoad, Is.SameAs(CoreVoyageSession.CardLibrary[loadIndex]), "The lesson uses the ordinary base definition instead of a hidden cost override.");
            Assert.That(lessonLoad.PowderCost, Is.EqualTo(1));
            Assert.That(lessonLoad.CannonballCost, Is.Zero);
            AssertResourceBadges(CreateFace(entry, loadIndex), lessonLoad, entry + " / tutorial load");
            ClearFixture();

            int chainIndex = FindCard("card_chain_shot");
            CoreBattleCard lessonChain = session.GetBattleCard(chainIndex);
            Assert.That(lessonChain.CannonballCost, Is.EqualTo(1));
            Assert.That(lessonChain.PowderCost, Is.EqualTo(1));
            Assert.That(lessonChain, Is.SameAs(CoreVoyageSession.CardLibrary[chainIndex]));
            AssertResourceBadges(CreateFace(entry, chainIndex), lessonChain, entry + " / tutorial chain shot");
        }

        [Test]
        public void TutorialActionFaces_KeepFullOriginalArtAndReadingRegionsInsideTallerSlots()
        {
            typeof(CoreGameplayDemoController).GetField("tutorial", PrivateInstance)
                .SetValue(controller, new TutorialVoyageSession());
            var examples = new[] {
                (TutorialBattleAction.FireCannons, "card_cannon_barrage", "火炮齐射", "造成 9 点伤害"),
                (TutorialBattleAction.Brace, "card_brace", "全员防御", "获得 8 点格挡"),
                (TutorialBattleAction.EmergencyRepair, "card_emergency_repair", "紧急修理", "木材 -2，船体 +6") };
            foreach (var example in examples)
            {
                Invoke("AddTutorialBattleAction", fixture, "Tutorial Geometry Probe", example.Item3,
                    example.Item4, example.Item1, example.Item2, Color.white);
                RawImage art = fixture.GetComponentsInChildren<RawImage>().Single(image => image.name == "Action Art");
                Assert.That(art.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), example.Item2);
                Assert.That(art.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).size), example.Item2);
                Assert.That(art.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).center));
                Assert.That(art.texture, Is.SameAs(CardSchoolArtCatalog.Illustration(CoreVoyageSession.CardLibrary[FindCard(example.Item2)], null)));
                Text name = fixture.GetComponentsInChildren<Text>().Single(text => text.name == "Tutorial Action Name");
                Assert.That(name.rectTransform.sizeDelta, Is.EqualTo(new Vector2(174f, 26f)));
                AssertRibbonTitle(name, example.Item2);
                Text effect = fixture.GetComponentsInChildren<Text>().Single(text => text.name == "Label");
                Assert.That(effect.rectTransform.sizeDelta, Is.EqualTo(new Vector2(176f, 94f)));
                Text energy = fixture.GetComponentsInChildren<Text>().Single(text => text.name == "Tutorial Action Energy Cost");
                Assert.That(energy.text, Is.EqualTo("0"));
                Assert.That(((RectTransform)energy.transform.parent).anchoredPosition,
                    Is.EqualTo(CardSchoolArtCatalog.CostPosition(CoreVoyageSession.CardLibrary[FindCard(example.Item2)], CardOrnamentKind.Energy)),
                    example.Item2 + " tutorial cost follows the calibrated wooden crystal center");
                RectTransform slot = fixture.Find("Tutorial Geometry Probe Slot") as RectTransform;
                CardSchoolFrameGraphic frame = slot.GetComponentInChildren<CardSchoolFrameGraphic>();
                Vector3[] corners = new Vector3[4];
                frame.rectTransform.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector2 point = slot.InverseTransformPoint(corner);
                    Assert.That(slot.rect.Contains(point), Is.True, example.Item2 + " complete taller frame fits its tutorial slot");
                }
                ClearFixture();
            }
        }

        [Test]
        public void RetainedBattleCard_ReindexingKeepsTheSameCannonballBadgeAndRenamesItsNumber()
        {
            CoreVoyageSession session = CreateBattleSession(3);
            UseSession(session);
            int chainIndex = FindCard("card_chain_shot");
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(chainIndex);
            object view = Invoke("CreateRetainedBattleHandCard", fixture, session.Battle, 0);
            Button button = (Button)view.GetType().GetField("Button").GetValue(view);
            CardOrnamentGraphic badge = FindBadge(button.transform, CardOrnamentKind.Cannonball);
            Text count = badge.GetComponentInChildren<Text>();
            session.Battle.Hand.Insert(0, FindCard("card_brace"));
            Invoke("RefreshRetainedHandCard", view, 1, 2);
            Assert.That(button.name, Is.EqualTo("Battle Card 1"));
            Assert.That(FindBadge(button.transform, CardOrnamentKind.Cannonball), Is.SameAs(badge));
            Assert.That(badge.name, Is.EqualTo("Card Cannonball Plate 1"));
            Assert.That(count.name, Is.EqualTo("Card Cannonball Cost 1"));
            Assert.That(count.text, Is.EqualTo("1"));
            AssertResourceBadges(button.GetComponent<RectTransform>(), session.GetBattleCard(chainIndex), "retained chain shot");
        }

        [TestCase("card_chain_shot")]
        [TestCase("card_cannon_barrage")]
        public void BattleResourceNumbers_MatchTheSuppliesActuallySpentBySuccessfulPlay(string cardId)
        {
            CoreVoyageSession session = CreateBattleSession(0);
            UseSession(session);
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.DistanceMeters = 200f;
            session.Battle.Energy = 10;
            session.Cargo.Powder = session.Cargo.Cannonballs = 10;
            RectTransform face = CreateFace("Battle", FindCard(cardId));
            int displayedPowder = BadgeValue(face, CardOrnamentKind.Gunpowder);
            int displayedCannonballs = BadgeValue(face, CardOrnamentKind.Cannonball);
            int powderBefore = session.Cargo.Powder, ballsBefore = session.Cargo.Cannonballs;
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(powderBefore - session.Cargo.Powder, Is.EqualTo(displayedPowder), cardId);
            Assert.That(ballsBefore - session.Cargo.Cannonballs, Is.EqualTo(displayedCannonballs), cardId);
            Assert.That(displayedCannonballs, Is.GreaterThan(0));
        }

        [TestCase(CoreSchoolMechanic.ArcaneRelease)]
        [TestCase(CoreSchoolMechanic.ArcaneWard)]
        [TestCase(CoreSchoolMechanic.ArcaneMend)]
        [TestCase(CoreSchoolMechanic.ArcaneEcho)]
        [TestCase(CoreSchoolMechanic.BloodRisk)]
        [TestCase(CoreSchoolMechanic.BloodWard)]
        [TestCase(CoreSchoolMechanic.CurseDrain)]
        [TestCase(CoreSchoolMechanic.TideWard)]
        [TestCase(CoreSchoolMechanic.TideRiposte)]
        [TestCase(CoreSchoolMechanic.TideRenew)]
        [TestCase(CoreSchoolMechanic.TideCleanse)]
        [TestCase(CoreSchoolMechanic.ThunderFinish)]
        public void SpecialResourceNumber_MatchesTheResourceActuallyPaidBySuccessfulPlay(CoreSchoolMechanic mechanic)
        {
            AssertSuccessfulSpecialPayment(mechanic);
        }

        [TestCase(CoreSchoolMechanic.BloodRisk, NavalBattleStage.Naval)]
        [TestCase(CoreSchoolMechanic.BloodRisk, NavalBattleStage.Boarding)]
        [TestCase(CoreSchoolMechanic.BloodWard, NavalBattleStage.Naval)]
        [TestCase(CoreSchoolMechanic.BloodWard, NavalBattleStage.Boarding)]
        public void LifePaymentNumber_TracksTheHealthPoolOfTheActualStage(CoreSchoolMechanic mechanic, NavalBattleStage stage)
        {
            AssertSuccessfulSpecialPayment(mechanic, stage);
        }

        private void AssertSuccessfulSpecialPayment(CoreSchoolMechanic mechanic, NavalBattleStage? requestedStage = null)
        {
            CoreBattleCard card = CoreVoyageSession.CardLibrary.First(value => value.SchoolEffect?.Mechanic == mechanic &&
                (!requestedStage.HasValue || value.IsAllowedInStage(requestedStage.Value)));
            CoreVoyageSession session = CreateBattleSession(0, card);
            Assert.That(session.PirateLevel, Is.EqualTo(10), "Establish unlocks before departure; restore APIs are port-only.");
            NavalBattleStage stage = requestedStage ?? Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>().First(card.IsAllowedInStage);
            if (stage == NavalBattleStage.Boarding)
                typeof(CoreVoyageSession).GetMethod("EnterBattleStage", PrivateInstance).Invoke(session, new object[] { stage });
            else session.Battle.Stage = stage;
            session.Battle.DistanceMeters = 150f;
            session.Battle.Energy = 20;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 10000;
            session.Battle.EnemyBlock = 0;
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            session.Cargo.Medicine = session.Cargo.Wood = 10;
            if (stage == NavalBattleStage.Boarding)
                session.Battle.PlayerBoardingHealth = Math.Max(card.SchoolEffect.Threshold + 1,
                    session.Battle.PlayerMaxBoardingHealth - 10);
            SetSchoolResource(session, "Mana", 12);
            SetSchoolResource(session, "CurseMarks", 6);
            SetSchoolResource(session, "ThunderChain", 6);
            SetSchoolResource(session, "TidePower", 20);
            UseSession(session);
            RectTransform face = CreateFace("Battle", FindCard(card.Id));
            CardOrnamentGraphic marker = FindBadge(face, CardSpecialCostPresentation.OrnamentKind(card));
            Assert.That(marker, Is.Not.Null, card.Id);
            int shown = int.Parse(marker.GetComponentInChildren<Text>().text);
            string resource = CardSpecialCostPresentation.ResourceType(card);
            int before = PaidResource(session, resource);
            Assert.That(shown, Is.GreaterThan(0));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle, Is.Not.Null, "The fixture enemy survives so payment remains observable.");
            int refundedChain = mechanic == CoreSchoolMechanic.ThunderFinish ? 1 : 0;
            Assert.That(before - PaidResource(session, resource) + refundedChain, Is.EqualTo(shown),
                card.Id + " / " + resource + ": a thunder finisher pays existing chain, then its attack generates one new chain.");
        }

        [TestCase(CoreSchoolMechanic.TideRiposte, "TidePower")]
        [TestCase(CoreSchoolMechanic.ThunderFinish, "ThunderChain")]
        public void ConsumeAllCost_ShowsCollectionAllAndCurrentBattleQuantityWithoutSpending(CoreSchoolMechanic mechanic, string property)
        {
            CoreVoyageSession session = CreateBattleSession(0);
            CoreBattleCard card = CoreVoyageSession.CardLibrary.First(value => value.SchoolEffect?.Mechanic == mechanic);
            Assert.That(CardSpecialCostPresentation.CostText(card), Is.EqualTo("全"));
            SetSchoolResource(session, property, 5);
            Assert.That(CardSpecialCostPresentation.CostText(card, session.Battle), Is.EqualTo("5"));
            SetSchoolResource(session, property, 0);
            Assert.That(CardSpecialCostPresentation.CostText(card, session.Battle), Is.EqualTo("0"));
            Assert.That((int)typeof(CoreSchoolBattleState).GetProperty(property).GetValue(session.Battle.Schools), Is.Zero,
                "Reading the cost never pays it or grants the resource.");
        }

        [TestCase(CoreSchoolMechanic.TideRiposte, "TidePower")]
        [TestCase(CoreSchoolMechanic.ThunderFinish, "ThunderChain")]
        public void RetainedConsumeAllNumber_RefreshesInPlaceWhenBattleResourceChanges(CoreSchoolMechanic mechanic, string property)
        {
            CoreBattleCard card = CoreVoyageSession.CardLibrary.First(value => value.SchoolEffect?.Mechanic == mechanic);
            CoreVoyageSession session = CreateBattleSession(0, card);
            UseSession(session);
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(FindCard(card.Id));
            SetSchoolResource(session, property, 0);
            object view = Invoke("CreateRetainedBattleHandCard", fixture, session.Battle, 0);
            Button button = (Button)view.GetType().GetField("Button").GetValue(view);
            CardOrnamentGraphic marker = FindBadge(button.transform, CardSpecialCostPresentation.OrnamentKind(card));
            Text number = marker.GetComponentInChildren<Text>();
            Assert.That(number.text, Is.EqualTo("0"));

            foreach (int amount in new[] { 6, 2, 0 })
            {
                SetSchoolResource(session, property, amount);
                Invoke("RefreshRetainedHandCard", view, 0, 1);
                Assert.That(FindBadge(button.transform, marker.Kind), Is.SameAs(marker));
                Assert.That(marker.GetComponentInChildren<Text>(), Is.SameAs(number));
                Assert.That(number.text, Is.EqualTo(amount.ToString()), card.Id);
                Assert.That((int)typeof(CoreSchoolBattleState).GetProperty(property).GetValue(session.Battle.Schools),
                    Is.EqualTo(amount), "A view refresh must not pay or grant resources.");
            }
        }

        [TestCase(CoreSchoolMechanic.ArcaneCharge)]
        [TestCase(CoreSchoolMechanic.CurseMark)]
        [TestCase(CoreSchoolMechanic.CurseDetonate)]
        [TestCase(CoreSchoolMechanic.ArcaneCapacity)]
        [TestCase(CoreSchoolMechanic.ArcaneTemporary)]
        [TestCase(CoreSchoolMechanic.ThunderReady)]
        [TestCase(CoreSchoolMechanic.ThunderChain)]
        [TestCase(CoreSchoolMechanic.ThunderEcho)]
        [TestCase(CoreSchoolMechanic.BloodExecute)]
        public void BonusesAndThresholdConditions_DoNotInventSpecialPayments(CoreSchoolMechanic mechanic)
        {
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary.Where(value => value.SchoolEffect?.Mechanic == mechanic))
            {
                Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.Empty, card.Id);
                Assert.That(CardSpecialCostPresentation.CostText(card), Is.Empty, card.Id);
            }
        }

        private static void SetSchoolResource(CoreVoyageSession session, string property, int value) =>
            typeof(CoreSchoolBattleState).GetProperty(property).SetValue(session.Battle.Schools, value);

        [Test]
        public void RetainedFireFallback_RefreshesZeroPowderToFullCostWithoutPayingSupplies()
        {
            CoreBattleCard authored = CoreVoyageSession.CardLibrary.Single(card => card.Id == "card_school_fire_b07");
            CoreVoyageSession session = CreateBattleSession(3, authored);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Cargo.Powder = 0;
            UseSession(session);
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(FindCard(authored.Id));
            object view = Invoke("CreateRetainedBattleHandCard", fixture, session.Battle, 0);
            Button button = (Button)view.GetType().GetField("Button").GetValue(view);
            CardOrnamentGraphic marker = FindBadge(button.transform, CardOrnamentKind.Gunpowder);
            Text number = marker.GetComponentInChildren<Text>();
            Assert.That(number.text, Is.EqualTo("0"));
            Assert.That(button.transform.Find("Label").GetComponent<Text>().text, Does.Contain("仅基础效果"));
            foreach (int powder in new[] { 1, 0 })
            {
                session.Cargo.Powder = powder;
                Invoke("RefreshRetainedHandCard", view, 0, 1);
                Assert.That(FindBadge(button.transform, CardOrnamentKind.Gunpowder), Is.SameAs(marker));
                Assert.That(number.text, Is.EqualTo(powder.ToString()));
                Assert.That(session.Cargo.Powder, Is.EqualTo(powder), "Retained quotation cannot pay powder.");
                Assert.That(button.transform.Find("Label").GetComponent<Text>().text.Contains("仅基础效果"), Is.EqualTo(powder == 0));
            }
        }

        [TestCase(CoreSchoolMechanic.TideWard, CardOrnamentKind.Wood)]
        [TestCase(CoreSchoolMechanic.TideRiposte, CardOrnamentKind.Tide)]
        public void TidePayments_DrawTheirOwnGlyphOnTheExistingFrame(CoreSchoolMechanic mechanic, CardOrnamentKind kind)
        {
            CoreBattleCard authored = CoreVoyageSession.CardLibrary.First(card => card.SchoolEffect?.Mechanic == mechanic);
            CoreVoyageSession session = CreateBattleSession(0, authored);
            session.Cargo.Wood = 0;
            UseSession(session);
            RectTransform face = CreateFace("Battle", FindCard(authored.Id));
            CardOrnamentGraphic marker = FindBadge(face, kind);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.IsMarkerOnly, Is.False, "Wood and tide are not baked into the reused frame and need a visible glyph.");
            Assert.That(CardSchoolArtCatalog.Frame(authored), Is.Not.Null);
            Assert.That(CardSchoolArtCatalog.FrameResourcePath(authored), Is.EqualTo(
                CardSchoolArtCatalog.FrameResourcePath(authored.SchoolId, authored.PowderCost, authored.CannonballCost)),
                "Wood and tide reuse the existing ammunition frame, with no missing new resource suffix.");
            if (mechanic == CoreSchoolMechanic.TideWard)
                Assert.That(marker.GetComponentInChildren<Text>().text, Is.EqualTo("0"), "Unpaid tide guard is only the free fallback.");
        }

        private static int PaidResource(CoreVoyageSession session, string resource)
        {
            switch (resource)
            {
                case "mana": return session.Battle.Schools.Mana;
                case "curse": return session.Battle.Schools.CurseMarks;
                case "chain": return session.Battle.Schools.ThunderChain;
                case "tide": return session.Battle.Schools.TidePower;
                case "wood": return session.Cargo.Wood;
                case "health": return session.Battle.Stage == NavalBattleStage.Boarding ? session.Battle.PlayerBoardingHealth : session.Hull;
                default: throw new ArgumentException(resource);
            }
        }

        [TestCase(6)]
        [TestCase(8)]
        public void CrowdedRealHand_CompleteFramesAndAllCostNumbersRemainSeparate(int count)
        {
            PrepareCrowdedHand(count);
            AssertHandCostsAreUncovered(HandFaces(), "hand of " + count);
            Assert.That((bool)Invoke("TryRefreshRetainedBattleView"), Is.True);
            Canvas.ForceUpdateCanvases();
            AssertHandCostsAreUncovered(HandFaces(), "refreshed hand of " + count);
        }

        [Test]
        public void RetainedHandShrinkingFromEightToFive_RescalesWholeCardsWithoutReplacingCostMarkers()
        {
            PrepareCrowdedHand(8);
            RectTransform[] crowded = HandFaces();
            AssertHandCostsAreUncovered(crowded, "crowded before refresh");
            var survivors = crowded.Take(5).ToArray();
            var survivorArt = survivors.Select(face => face.GetComponentInChildren<RawImage>()).ToArray();
            var survivorBadges = survivors.Select(face => face.GetComponentsInChildren<CardOrnamentGraphic>(true)
                .Where(badge => badge.Kind == CardOrnamentKind.Gunpowder || badge.Kind == CardOrnamentKind.Cannonball).ToArray()).ToArray();
            Assert.That(survivors.Any(face => face.localScale.x < .99f),
                Is.True, "Exercise automatic whole-card scaling in an eight-card hand.");
            Assert.That(survivorBadges.SelectMany(badges => badges).All(badge => badge.transform.localScale == Vector3.one),
                Is.True, "An integrated frame scales with the whole card, never individual cost labels.");
            controller.Session.Battle.Hand.RemoveRange(5, 3);
            // Edit mode cannot execute runtime Destroy. Remove only the three
            // discarded fixture objects now; the actual retained-view refresh
            // still owns matching, resizing and repositioning all five survivors.
            foreach (RectTransform removed in crowded.Skip(5)) Object.DestroyImmediate(removed.gameObject);
            Assert.That((bool)Invoke("TryRefreshRetainedBattleView"), Is.True);
            Canvas.ForceUpdateCanvases();
            RectTransform[] restored = HandFaces();
            Assert.That(restored, Has.Length.EqualTo(5));
            for (int index = 0; index < restored.Length; index++)
            {
                Assert.That(restored[index], Is.SameAs(survivors[index]), "Surviving card identity " + index);
                RawImage art = restored[index].GetComponentInChildren<RawImage>();
                Assert.That(art, Is.SameAs(survivorArt[index]), "Retained refresh must keep the original illustration object");
                Assert.That(art.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), "Retained refresh cannot restore the old crop");
                Assert.That(art.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).size));
                Assert.That(art.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).center));
                foreach (CardOrnamentGraphic original in survivorBadges[index])
                {
                    CardOrnamentGraphic badge = FindBadge(restored[index], original.Kind);
                    Assert.That(badge, Is.SameAs(original), "Surviving resource identity " + index);
                    Assert.That(badge.rectTransform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(badge.rectTransform.sizeDelta, Is.EqualTo(badge.Kind == CardOrnamentKind.Cannonball
                        ? CardFaceLayout.CannonballIconSize : CardFaceLayout.PowderIconSize));
                    Assert.That(badge.rectTransform.anchoredPosition, Is.EqualTo(badge.Kind == CardOrnamentKind.Cannonball
                        ? CardSchoolArtCatalog.CostPosition(controller.Session.GetBattleCard(controller.Session.Battle.Hand[index]), CardOrnamentKind.Cannonball)
                        : CardSchoolArtCatalog.CostPosition(controller.Session.GetBattleCard(controller.Session.Battle.Hand[index]), CardOrnamentKind.Gunpowder)));
                }
                RectTransform repeat = restored[index].Find("Card Repeat Cost Plate " + index) as RectTransform;
                Assert.That(repeat.anchoredPosition, Is.EqualTo(CardFaceLayout.RepeatCostPosition));
                Assert.That(LocalRect(repeat).Overlaps(CardFaceLayout.IllustrationBounds), Is.False,
                    "The repeated-play surcharge must stay outside the illustration");
            }
            AssertHandCostsAreUncovered(restored, "restored hand of five");
        }

        private void PrepareCrowdedHand(int count)
        {
            CoreVoyageSession session = CreateBattleSession(0);
            UseSession(session);
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.DistanceMeters = 200f;
            session.Battle.Energy = 10;
            session.Cargo.Powder = session.Cargo.Cannonballs = 10;
            int loading = FindCard("card_load_shot"), firing = FindCard("card_chain_shot");
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(loading);
            session.Battle.Hand.Add(firing);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            session.Battle.Hand.Clear();
            for (int i = 0; i < count; i++) session.Battle.Hand.Add(i % 2 == 0 ? loading : firing);
            Invoke("Render");
            Canvas.ForceUpdateCanvases();
            Assert.That(HandFaces(), Has.Length.EqualTo(count));
        }

        private RectTransform[] HandFaces()
        {
            RectTransform hand = host.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == "Battle Hand");
            return hand.Cast<Transform>().Where(child => child.gameObject.activeSelf && child.GetComponent<Button>() != null)
                .OrderBy(child => child.GetSiblingIndex()).Select(child => child as RectTransform).ToArray();
        }

        private static void AssertHandCostsAreUncovered(RectTransform[] faces, string context)
        {
            for (int index = 0; index < faces.Length; index++)
            {
                RectTransform face = faces[index];
                CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
                Assert.That(frame, Is.Not.Null, context);
                Rect faceBounds = WorldRect(frame.rectTransform);
                Assert.That(faceBounds.width, Is.GreaterThan(1f), context + " requires real canvas geometry");
                for (int later = index + 1; later < faces.Length; later++)
                {
                    CardSchoolFrameGraphic other = faces[later].GetComponentInChildren<CardSchoolFrameGraphic>();
                    Assert.That(faceBounds.Overlaps(WorldRect(other.rectTransform)), Is.False,
                        context + " complete integrated frame overlaps card " + later);
                }
                RectTransform[] badges = face.GetComponentsInChildren<CardOrnamentGraphic>(true)
                    .Where(item => item.Kind == CardOrnamentKind.Energy || item.Kind == CardOrnamentKind.Gunpowder || item.Kind == CardOrnamentKind.Cannonball)
                    .Select(item => item.rectTransform).ToArray();
                RectTransform repeat = face.Find("Card Repeat Cost Plate " + index) as RectTransform;
                Assert.That(repeat, Is.Not.Null);
                Assert.That(repeat.gameObject.activeSelf, Is.True, context + " must exercise a visible repeated-use surcharge");
                Assert.That(repeat.GetComponentInChildren<Text>().text, Is.EqualTo("+1"));
                RectTransform[] allCosts = badges.Concat(new[] { repeat }).ToArray();
                for (int cost = 0; cost < allCosts.Length; cost++)
                {
                    Rect bounds = WorldRect(allCosts[cost]);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(faceBounds.xMin - .01f), context);
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(faceBounds.xMax + .01f), context);
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(faceBounds.yMin - .01f), context);
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(faceBounds.yMax + .01f), context);
                    Rect numberBounds = WorldRect(allCosts[cost].GetComponentInChildren<Text>().rectTransform);
                    for (int later = index + 1; later < faces.Length; later++)
                    {
                        Rect coveringCard = WorldRect(faces[later].GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
                        Assert.That(bounds.Overlaps(coveringCard), Is.False,
                            context + " / " + allCosts[cost].name + " backing covered by card " + later);
                        Assert.That(numberBounds.Overlaps(coveringCard), Is.False,
                            context + " / " + allCosts[cost].name + " number covered by card " + later);
                    }
                    for (int previous = 0; previous < cost; previous++)
                        Assert.That(bounds.Overlaps(WorldRect(allCosts[previous])), Is.False,
                            context + " / " + allCosts[cost].name + " overlaps another cost on the same card");
                }
            }
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y),
                corners.Max(point => point.x), corners.Max(point => point.y));
        }

        private CoreVoyageSession CreateBattleSession(int level, CoreBattleCard requiredCard = null)
        {
            var session = new CoreVoyageSession();
            if (requiredCard != null)
            {
                session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
                session.ConfigureSpecialCardSources(requiredCard.RequiredCrew.Length > 0 ? new[] { requiredCard.RequiredCrew } : Array.Empty<string>(),
                    requiredCard.RequiredComponent.Length > 0 ? new[] { requiredCard.RequiredComponent } : Array.Empty<string>());
            }
            session.ConfigureCardUpgrades(CoreVoyageSession.CardLibrary.Where(card => card.AllowedStages != NavalBattleStages.None &&
                    !CoreVoyageSession.IsSpecialBattleCard(card.Id))
                .ToDictionary(card => card.Id, card => level));
            // Special cards advance through voyage experience. Seed that public
            // restore interface explicitly instead of relying on paid-upgrade migration.
            int experience = new[] { 0, 120, 360, 840 }[level];
            session.RestoreSpecialCardExperience(CoreVoyageSession.CardLibrary
                .Where(card => card.AllowedStages != NavalBattleStages.None && CoreVoyageSession.IsSpecialBattleCard(card.Id))
                .ToDictionary(card => card.Id, card => experience));
            session.SetSeed("CARD-RESOURCE-BADGE-TEST");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", PrivateInstance).Invoke(session,
                new object[] { new CoreEncounter("badge_test", "弹药角标测试敌舰", "海战", "测试", true, false, 1, 70), false });
            return session;
        }

        private void UseSession(CoreVoyageSession session) =>
            typeof(CoreGameplayDemoController).GetField("session", PrivateInstance).SetValue(controller, session);

        private RectTransform CreateFace(string entry, int index)
        {
            CoreVoyageSession session = controller.Session;
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(index);
            switch (entry)
            {
                case "Battle":
                    object view = Invoke("CreateRetainedBattleHandCard", fixture, session.Battle, 0);
                    return ((Button)view.GetType().GetField("Button").GetValue(view)).GetComponent<RectTransform>();
                case "Workshop":
                    Button workshopCard = (Button)Invoke("CreateDeckBuilderCard", fixture, index,
                        "Add Deck Card " + index, "Workshop Card " + index, Vector2.zero, .76f, true);
                    return workshopCard.GetComponentInChildren<CardSchoolFrameGraphic>().transform.parent as RectTransform;
                case "Collection":
                    return (RectTransform)Invoke("CreateCollectionCardFace", fixture, session.GetBattleCard(index),
                        "Audit Collection", "Audit Card Art", "Audit Card Name", 0.5f, null, null);
                default: throw new ArgumentException(entry);
            }
        }

        private static void AssertResourceBadges(RectTransform face, CoreBattleCard data, string context, CoreBattleState battle = null)
        {
            CardOrnamentGraphic energy = FindBadge(face, CardOrnamentKind.Energy);
            Assert.That(energy, Is.Not.Null, context);
            Assert.That(BadgeValue(face, CardOrnamentKind.Energy), Is.EqualTo(data.Energy), context);
            CardOrnamentGraphic powder = AssertBadge(face, CardOrnamentKind.Gunpowder, data.PowderCost, context);
            CardOrnamentGraphic cannonballs = AssertBadge(face, CardOrnamentKind.Cannonball, data.CannonballCost, context);
            string specialType = CardSpecialCostPresentation.ResourceType(data);
            var specialKinds = new[] { CardOrnamentKind.Mana, CardOrnamentKind.Health, CardOrnamentKind.Curse, CardOrnamentKind.Chain, CardOrnamentKind.Wood, CardOrnamentKind.Tide };
            CardOrnamentGraphic[] specialMarkers = face.GetComponentsInChildren<CardOrnamentGraphic>(true)
                .Where(item => specialKinds.Contains(item.Kind)).ToArray();
            Assert.That(specialMarkers, Has.Length.EqualTo(specialType.Length > 0 ? 1 : 0), context + " shows only resources actually paid");
            CardOrnamentGraphic special = specialMarkers.SingleOrDefault();
            if (special != null)
            {
                Assert.That(special.Kind, Is.EqualTo(CardSpecialCostPresentation.OrnamentKind(data)), context);
                Assert.That(special.GetComponentInChildren<Text>().text,
                    Is.EqualTo(CardSpecialCostPresentation.CostText(data, battle)), context);
            }
            CardOrnamentGraphic[] costs = { energy, powder, cannonballs, special };
            CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(frame, Is.Not.Null, context);
            Assert.That(frame.mainTexture, Is.SameAs(CardSchoolArtCatalog.Frame(data)), context);
            Text title = face.GetComponentsInChildren<CardOrnamentGraphic>(true)
                .Single(item => item.Kind == CardOrnamentKind.NameRibbon).GetComponentInChildren<Text>(true);
            Assert.That(title.text, Is.EqualTo(CardFaceLayout.DisplayName(data)), context);
            AssertRibbonTitle(title, context);
            Rect frameBounds = CardSchoolArtCatalog.FrameBounds(data);
            Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(frameBounds.size), context);
            Assert.That(frame.rectTransform.anchoredPosition, Is.EqualTo(frameBounds.center), context);
            Image backing = face.GetComponent<Image>();
            Assert.That(backing, Is.Not.Null, context);
            Assert.That(backing.color.a, Is.Zero, context + " full PNG owns the card backing");
            foreach (Image paper in face.GetComponentsInChildren<Image>(true).Where(image =>
                image.name.EndsWith(" Effect Paper", StringComparison.Ordinal) ||
                image.name.StartsWith("Card Description Parchment ", StringComparison.Ordinal) ||
                image.name.StartsWith("Workshop Card Effect Paper ", StringComparison.Ordinal)))
                Assert.That(paper.enabled, Is.False, context + " must not draw another writing-paper image over the PNG");
            foreach (Text label in face.GetComponentsInChildren<Text>(true))
            {
                Transform layer = label.transform;
                while (layer.parent != null && layer.parent != face) layer = layer.parent;
                Assert.That(layer.parent, Is.SameAs(face), context + " text belongs to this complete card");
                Assert.That(layer.GetSiblingIndex(), Is.GreaterThan(frame.transform.GetSiblingIndex()),
                    context + " / " + label.name + " live text must render over the authored name and effect paper");
            }
            foreach (RawImage illustration in face.GetComponentsInChildren<RawImage>(true))
            {
                Assert.That(illustration.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), context + " full original UVs");
                Vector2 actualSize = illustration.rectTransform.sizeDelta;
                Assert.That(actualSize.x / actualSize.y,
                    Is.EqualTo((float)illustration.texture.width / illustration.texture.height).Within(.0001f),
                    context + " original aspect ratio cannot stretch");
                Rect target = CardFaceLayout.IllustrationLayout(illustration);
                Assert.That(actualSize.x, Is.LessThanOrEqualTo(target.width + .001f), context);
                Assert.That(actualSize.y, Is.LessThanOrEqualTo(target.height + .001f), context);
                Assert.That(Mathf.Min(Mathf.Abs(actualSize.x - target.width), Mathf.Abs(actualSize.y - target.height)),
                    Is.LessThan(.001f), context + " full art fits the authored window without stretching");
                Assert.That(illustration.rectTransform.anchoredPosition,
                    Is.EqualTo(CardFaceLayout.IllustrationLayout(illustration).center), context);
                Transform layer = illustration.transform;
                while (layer.parent != null && layer.parent != face) layer = layer.parent;
                Assert.That(layer.GetSiblingIndex(), Is.LessThan(frame.transform.GetSiblingIndex()),
                    context + " illustration stays beneath the complete frame");
            }
            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i] == null) continue;
                CardOrnamentGraphic cost = costs[i];
                Vector2 position = CardSchoolArtCatalog.CostPosition(data, cost.Kind);
                Vector2 size = cost.Kind == CardOrnamentKind.Energy ? CardFaceLayout.EnergyIconSize :
                    cost.Kind == CardOrnamentKind.Cannonball ? CardFaceLayout.CannonballIconSize :
                    cost.Kind == CardOrnamentKind.Gunpowder ? CardFaceLayout.PowderIconSize : CardFaceLayout.SpecialCostIconSize;
                Assert.That(cost.IsMarkerOnly, Is.EqualTo(cost.Kind != CardOrnamentKind.Wood && cost.Kind != CardOrnamentKind.Tide),
                    context + " existing resource glyphs are baked into PNGs; wood and tide draw a visible fallback glyph");
                Assert.That(cost.rectTransform.anchoredPosition, Is.EqualTo(position), context);
                Assert.That(position, Is.Not.EqualTo(Vector2.zero), context + " cost calibration cannot fall into the card center");
                Assert.That(cost.rectTransform.sizeDelta, Is.EqualTo(size), context);
                Assert.That(cost.rectTransform.localScale, Is.EqualTo(Vector3.one), context);
                Rect bounds = LocalRect(costs[i].rectTransform);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(frameBounds.xMin), context);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(frameBounds.xMax), context);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(frameBounds.yMin), context);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(frameBounds.yMax), context);
                MethodInfo populate = typeof(CardOrnamentGraphic).GetMethod("OnPopulateMesh", PrivateInstance,
                    null, new[] { typeof(VertexHelper) }, null);
                using (var mesh = new VertexHelper())
                {
                    populate.Invoke(cost, new object[] { mesh });
                    if (cost.Kind == CardOrnamentKind.Wood || cost.Kind == CardOrnamentKind.Tide)
                        Assert.That(mesh.currentVertCount, Is.GreaterThan(0), context + " unbaked resource glyph must remain visible");
                    else
                        Assert.That(mesh.currentVertCount, Is.Zero, context + " cost markers must not draw a second crystal/icon");
                }
                for (int j = 0; j < i; j++)
                    if (costs[j] != null)
                        Assert.That(bounds.Overlaps(LocalRect(costs[j].rectTransform)), Is.False,
                            context + " must show each cost separately without overlapping energy or the other resource");
            }
            if (powder != null && cannonballs != null)
                Assert.That(LocalRect(cannonballs.rectTransform).xMax, Is.LessThan(LocalRect(powder.rectTransform).xMin), context);
        }

        private static CardOrnamentGraphic AssertBadge(Transform face, CardOrnamentKind kind, int expected, string context)
        {
            CardOrnamentGraphic[] badges = face.GetComponentsInChildren<CardOrnamentGraphic>(true).Where(item => item.Kind == kind).ToArray();
            Assert.That(badges.Length, Is.EqualTo(expected > 0 ? 1 : 0), context + " / " + kind);
            if (expected <= 0) return null;
            CardOrnamentGraphic badge = badges[0];
            Assert.That(badge.gameObject.activeSelf, Is.True, context);
            Assert.That(badge.enabled, Is.True, context);
            Assert.That(badge.raycastTarget, Is.False, context + " cannot steal card clicks");
            Text[] numbers = badge.GetComponentsInChildren<Text>(true);
            Assert.That(numbers, Has.Length.EqualTo(1), context);
            Assert.That(numbers[0].text, Is.EqualTo(expected.ToString()), context + " / " + kind);
            Assert.That(numbers[0].enabled, Is.True, context);
            return badge;
        }

        private static void AssertRibbonTitle(Text title, string context)
        {
            Assert.That(title.font, Is.SameAs(GameTypography.InterfaceFont), context);
            Assert.That(title.fontStyle, Is.EqualTo(FontStyle.Normal), context + " keep authored strokes without synthetic italic/bold");
            Assert.That(title.GetComponents<Shadow>(), Is.Empty, context + " title glyph quads must not be duplicated by Shadow/Outline before curving");
            CardRibbonTitleEffect ribbon = title.GetComponent<CardRibbonTitleEffect>();
            Assert.That(ribbon, Is.Not.Null, context);
            Assert.That(ribbon.GeneratedTextFits(title), Is.True, context + " complete dynamic title fits the measured paper band");
            Assert.That(title.fontSize, Is.GreaterThanOrEqualTo(15), context + " readable logical title size must not shrink with Canvas resolution");
            foreach (char character in title.text.Where(character => !char.IsWhiteSpace(character)))
                Assert.That(title.font.HasCharacter(character), Is.True, context + " missing authored-font character: " + character);
            using (var mesh = new VertexHelper())
            {
                TextGenerationSettings settings = title.GetGenerationSettings(title.rectTransform.rect.size);
                settings.scaleFactor = 1f;
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                title.cachedTextGeneratorForLayout.Populate(title.text, settings);
                var before = title.cachedTextGeneratorForLayout.verts.ToArray();
                Assert.That(before.Length % 4, Is.Zero, context + " Text supplies one quad per glyph");
                ribbon.ModifyMesh(mesh);
                Assert.That(mesh.currentVertCount, Is.EqualTo(before.Length), context + " no glyph may be removed or duplicated");
                Assert.That(ribbon.RenderedGlyphCount, Is.EqualTo(title.text.Count(character => !char.IsWhiteSpace(character))), context);
                Assert.That(ribbon.FitsAuthoredRibbon, Is.True, context + " actual rotated vertices stay on paper");
                int slopedEdges = 0;
                for (int i = 0; i < mesh.currentVertCount; i++)
                {
                    UIVertex actual = default, next = default;
                    mesh.PopulateUIVertex(ref actual, i);
                    int nextIndex = i / 4 * 4 + (i + 1) % 4;
                    mesh.PopulateUIVertex(ref next, nextIndex);
                    Assert.That(actual.uv0, Is.EqualTo(before[i].uv0), context + " preserve original glyph texture");
                    Assert.That(Vector3.Distance(actual.position, next.position),
                        Is.EqualTo(Vector3.Distance(before[i].position, before[nextIndex].position)).Within(.002f),
                        context + " glyph strokes rotate rigidly without stretching");
                    if (Mathf.Abs(before[i].position.y - before[nextIndex].position.y) < .001f &&
                        Mathf.Abs(actual.position.y - next.position.y) > .02f) slopedEdges++;
                }
                Assert.That(slopedEdges, Is.GreaterThan(0), context + " title must follow the ribbon tangent instead of staying flat");
            }
        }

        private static CardOrnamentGraphic FindBadge(Transform face, CardOrnamentKind kind) =>
            face.GetComponentsInChildren<CardOrnamentGraphic>(true).SingleOrDefault(item => item.Kind == kind);

        private static int BadgeValue(Transform face, CardOrnamentKind kind)
        {
            CardOrnamentGraphic badge = FindBadge(face, kind);
            return badge == null ? 0 : int.Parse(badge.GetComponentInChildren<Text>().text);
        }

        private static Rect LocalRect(RectTransform rect)
        {
            Rect result = rect.rect;
            result.position += rect.anchoredPosition;
            return result;
        }

        private object Invoke(string name, params object[] args)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, PrivateInstance);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(controller, args);
        }

        private static int FindCard(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private void ClearFixture()
        {
            for (int i = fixture.childCount - 1; i >= 0; i--) Object.DestroyImmediate(fixture.GetChild(i).gameObject);
        }
    }
}
