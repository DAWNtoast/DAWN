using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavalFieldPresentationTests
    {
        private GameObject fixture;
        private CoreVoyageSession session;
        private NavalTacticalBoardView board;
        private Font font;

        [SetUp]
        public void SetUp()
        {
            session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.RestoreCardPackProgress(null, session.PirateLevel), Is.True,
                "The resource/field fixture starts with a migrated owned school-card collection before departure.");
            session.SetSeed("NAVAL-FIELD-UI"); session.StartVoyage();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            Pose(true, 0, 0); Pose(false, 2, 0);
            session.Battle.Energy = 20; session.Cargo.Powder = session.Cargo.Cannonballs = 20;
            fixture = new GameObject("Naval field UI fixture", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)fixture.transform).sizeDelta = new Vector2(1920f, 1080f);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            board = Rect("Board").gameObject.AddComponent<NavalTacticalBoardView>();
            board.Configure(session.Battle, Rect("Player"), Rect("Enemy"), Rect("Player Hull"), Rect("Enemy Hull"), font);
        }

        [TearDown]
        public void TearDown() { if (fixture != null) Object.DestroyImmediate(fixture); }

        [Test]
        public void FieldChoice_RejectsOutOfRangeAnchorAndCancelNeverPays()
        {
            int energy = session.Battle.Energy, powder = session.Cargo.Powder;
            bool cancelled = false, committed = false;
            NavalCardChoiceOverlay overlay = Choice("card_school_fire_n01", _ => committed = true, () => cancelled = true);
            overlay.SelectCell(5, 0);
            Button confirm = Button(overlay, "Confirm Naval Card Choice");
            Assert.That(confirm.image.sprite, Is.SameAs(WoodButtonSkin.Face));
            Assert.That(confirm.GetComponent<WoodButtonRoleState>().Role, Is.EqualTo(WoodButtonRole.Primary));
            Assert.That(confirm.interactable, Is.False);
            Assert.That(overlay.Draft.HasAnchor, Is.False);
            Assert.That(overlay.GetComponentInChildren<NavalFieldGraphic>().Cells.Any(c => c.Invalid && c.Position.X == 500 && c.Position.Y == 0), Is.True);
            overlay.ConfirmSelection();
            Assert.That(committed, Is.False);
            Button(overlay, "Cancel Naval Card Choice").onClick.Invoke();
            Assert.That(cancelled, Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy)); Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1)); Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void FieldChoice_ClickCommitsAndItsFarCornersCanExtendBeyondAnchorRange()
        {
            bool committed = false;
            NavalCardChoiceOverlay overlay = Choice("card_school_fire_n01", options => committed = session.PlayNavalCard(0, options));
            int energy = session.Battle.Energy;
            overlay.HoverCell(4, 0);
            Assert.That(overlay.GetComponentInChildren<NavalFieldGraphic>().Cells.Any(c => c.Invalid), Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy), "Hovering is a preview only.");
            overlay.SelectCell(4, 0);
            Assert.That(committed, Is.True); Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(9));
            Assert.That(session.Battle.NavalFields.Cells.All(c => c.X >= 4 && c.X <= 6 && c.Y >= 0 && c.Y <= 2), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - 2));
        }

        [TestCase("fire", 3)]
        [TestCase("arcane", 2)]
        [TestCase("curse", 2)]
        [TestCase("tide", 4)]
        [TestCase("thunder", 7)]
        [TestCase("blood", 2)]
        public void FieldHover_ShowsExactSquareEastAndNorthFromWorldAnchor(string school, int size)
        {
            NavalCardChoiceOverlay overlay = Choice("card_school_" + school + "_n01", _ => Assert.Fail("Hover cannot commit."));
            overlay.HoverCell(-1, 1);
            var cells = overlay.GetComponentInChildren<NavalFieldGraphic>().Cells.Where(c => c.Fill.a > .1f).ToArray();
            Assert.That(cells.Length, Is.EqualTo(size * size));
            Assert.That(cells.All(c => !c.Invalid && c.Position.X >= -100 && c.Position.X <= (size - 2) * 100 &&
                c.Position.Y >= 100 && c.Position.Y <= size * 100), Is.True);
            Assert.That(cells.Select(c => c.Position.X + ":" + c.Position.Y).Distinct().Count(), Is.EqualTo(size * size));
            Assert.That(cells.All(c => c.DirectionX == 0 && c.DirectionY == 0), Is.True, "Direction is not guessed before the second step.");
            Assert.That(overlay.Draft.HasAnchor, Is.False);
            Assert.That(Button(overlay, "Confirm Naval Card Choice").gameObject.activeSelf, Is.False);
            overlay.OnPointerExit(null);
            Assert.That(overlay.GetComponentInChildren<NavalFieldGraphic>().Cells.All(c => c.Fill.a < .1f), Is.True);
        }

        [Test]
        public void FieldHover_UsesBoardWorldProjectionAfterCameraRotation()
        {
            NavalCardChoiceOverlay overlay = Choice("card_school_arcane_n01", _ => Assert.Fail("Hover cannot commit."));
            board.ApplyFrame(new CoreNavalTacticalFrame(new VoyagePoint(0, -200), new VoyagePoint(0, 0), 90, 180));
            Vector2 local = board.WorldToBoard(new VoyagePoint(100, 100));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, board.transform.TransformPoint(local));
            overlay.OnPointerMove(new PointerEventData(null) { position = screen });
            var cells = overlay.GetComponentInChildren<NavalFieldGraphic>().Cells.Where(c => c.Fill.a > .1f).ToArray();
            Assert.That(cells.Length, Is.EqualTo(4));
            Assert.That(cells.All(c => c.Position.X >= 100 && c.Position.X <= 200 && c.Position.Y >= 100 && c.Position.Y <= 200), Is.True);
        }

        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void WaterField_RequiresAnchorThenDirectionAndCommitsExactlyOnce(int dx, int dy)
        {
            int commits = 0, energy = session.Battle.Energy;
            CoreNavalCardPlayOptions selected = null;
            NavalCardChoiceOverlay overlay = Choice("card_school_tide_n01", options => { commits++; selected = options; });
            Assert.That(overlay.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Naval field direction ")).All(b => !b.gameObject.activeSelf), Is.True);
            overlay.SelectDirection(dx, dy);
            Assert.That(overlay.Draft.DirectionX, Is.Zero); Assert.That(overlay.Draft.DirectionY, Is.Zero);
            overlay.SelectCell(1, 1);
            Assert.That(overlay.IsSelectingDirection, Is.True);
            Assert.That(commits, Is.Zero);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy)); Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            overlay.ConfirmSelection();
            Assert.That(commits, Is.Zero, "The direction step cannot be skipped.");
            overlay.HoverCell(-1, -1); overlay.SelectCell(-1, -1);
            Assert.That(overlay.Draft.AnchorX, Is.EqualTo(1)); Assert.That(overlay.Draft.AnchorY, Is.EqualTo(1));
            overlay.SelectDirection(dx, dy); overlay.SelectDirection(dx, dy); overlay.ConfirmSelection();
            Assert.That(commits, Is.EqualTo(1)); Assert.That(selected, Is.Not.SameAs(overlay.Draft));
            Assert.That(session.PlayNavalCard(0, selected), Is.True);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(16));
            Assert.That(session.Battle.NavalFields.Cells.All(c => c.DirectionX == dx && c.DirectionY == dy && c.X >= 1 && c.X <= 4 && c.Y >= 1 && c.Y <= 4), Is.True);
        }

        [Test]
        public void WaterField_BackReturnsToAnchorSelectionAndCancelNeverPays()
        {
            bool cancelled = false;
            int energy = session.Battle.Energy, powder = session.Cargo.Powder;
            NavalCardChoiceOverlay overlay = Choice("card_school_tide_n01", _ => Assert.Fail("Incomplete or cancelled selection cannot commit."), () => cancelled = true);
            overlay.SelectCell(0, 0);
            overlay.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Assert.That(cancelled, Is.False); Assert.That(overlay.IsSelectingDirection, Is.False); Assert.That(overlay.Draft.HasAnchor, Is.False);
            overlay.SelectCell(-1, -1);
            Button(overlay, "Reselect Naval Field Anchor").onClick.Invoke();
            Assert.That(overlay.Draft.HasAnchor, Is.False);
            overlay.SelectCell(2, 0);
            Button(overlay, "Cancel Naval Card Choice").onClick.Invoke();
            Assert.That(cancelled, Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy)); Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1)); Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void FieldChoice_DuplicateClickCannotInvokeCommitTwice()
        {
            int commits = 0;
            NavalCardChoiceOverlay overlay = Choice("card_school_arcane_n01", _ => commits++);
            overlay.SelectCell(0, 0); overlay.SelectCell(0, 0); overlay.ConfirmSelection();
            Assert.That(commits, Is.EqualTo(1));
        }

        [Test]
        public void ExplosionChoice_KeepsExplicitConfirmationForItsDestructiveAreaAction()
        {
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, 0, 0, false, 0, 0), Is.True);
            int commits = 0, energy = session.Battle.Energy;
            NavalCardChoiceOverlay overlay = Choice("card_school_fire_n06", _ => commits++);
            overlay.SelectCell(0, 0);
            Assert.That(commits, Is.Zero);
            Assert.That(Button(overlay, "Confirm Naval Card Choice").gameObject.activeSelf, Is.True);
            Assert.That(Button(overlay, "Confirm Naval Card Choice").interactable, Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            overlay.ConfirmSelection(); overlay.ConfirmSelection();
            Assert.That(commits, Is.EqualTo(1));
        }

        [Test]
        public void PersistentFields_FollowWorldProjectionAndShowOnlyBottomRightDuration()
        {
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Tide, true, -1, 1, false, 0, 1), Is.True);
            NavalFieldBoardOverlay overlay = board.gameObject.AddComponent<NavalFieldBoardOverlay>();
            overlay.Configure(session, board, font);
            NavalFieldGraphic graphic = board.GetComponentsInChildren<NavalFieldGraphic>().Single(g => g.name == "Persistent Naval Fields");
            Assert.That(graphic.Cells.Count, Is.EqualTo(16));
            Assert.That(graphic.Cells.All(c => c.DirectionX == 0 && c.DirectionY == 1), Is.True);
            Text label = graphic.GetComponentsInChildren<Text>().First();
            Assert.That(label.text, Is.EqualTo("5"));
            Assert.That(label.alignment, Is.EqualTo(TextAnchor.LowerRight));
            Assert.That(label.rectTransform.pivot, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(label.raycastTarget, Is.False);
            Vector2 center = board.WorldToBoard(new VoyagePoint(-100f, 100f));
            Assert.That(label.rectTransform.anchoredPosition.x, Is.GreaterThan(center.x));
            Assert.That(label.rectTransform.anchoredPosition.y, Is.LessThan(center.y));
            Assert.That(label.rectTransform.anchoredPosition.x - center.x, Is.LessThan(board.CellSizePixels * .5f));
            Assert.That(center.y - label.rectTransform.anchoredPosition.y, Is.LessThan(board.CellSizePixels * .5f));
            Vector2 before = label.rectTransform.anchoredPosition;
            board.ApplyFrame(new CoreNavalTacticalFrame(new VoyagePoint(0, -200), new VoyagePoint(0, 0), 90, 180));
            overlay.RefreshNow();
            Assert.That(label.rectTransform.anchoredPosition, Is.Not.EqualTo(before));
            Assert.That(session.Battle.NavalFields.Cells[0].X, Is.EqualTo(-1));
            Assert.That(session.Battle.NavalFields.Cells[0].Y, Is.EqualTo(1));
            Assert.That(graphic.raycastTarget, Is.False);
        }

        [Test]
        public void AnchorButton_ChoosesWhetherTheNextForcedMovementIsRefused()
        {
            session.GrantNavalAnchor(true);
            NavalFieldBoardOverlay overlay = board.gameObject.AddComponent<NavalFieldBoardOverlay>();
            overlay.Configure(session, board, font);
            Button anchor = board.GetComponentsInChildren<Button>().Single(b => b.name == "Naval anchor response");
            Assert.That(session.PlayerRefuseNavalForcedMovement, Is.True);
            Assert.That(anchor.GetComponent<WoodButtonRoleState>().IsSelected, Is.True);
            anchor.onClick.Invoke();
            Assert.That(session.PlayerRefuseNavalForcedMovement, Is.False);
            Assert.That(anchor.GetComponent<WoodButtonRoleState>().IsSelected, Is.False);
            Assert.That(session.Battle.NavalFields.Player.AnchorCharges, Is.EqualTo(1));
        }

        [TestCase("card_school_arcane_n03", "mana", "2")]
        [TestCase("card_school_blood_n01", "health", "4")]
        [TestCase("card_school_curse_n03", "curse", "X")]
        [TestCase("card_school_tide_n02", "tide", "X")]
        [TestCase("card_school_thunder_n06", "chain", "X")]
        public void WorkshopAndHand_ShowTheRealNavalResourceCost(string id, string kind, string amount)
        {
            CoreBattleCard card = CoreVoyageSession.CardLibrary.Single(c => c.Id == id);
            Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.EqualTo(kind));
            Assert.That(CardSpecialCostPresentation.CostText(card), Is.EqualTo(amount));
            Assert.That(CardSchoolArtCatalog.HasSchoolIllustration(card), Is.True);
        }

        [Test]
        public void WorkshopAndHand_ShowTideAnchorWoodInTheSeparateSupplySlot()
        {
            CoreBattleCard card = CoreVoyageSession.CardLibrary.Single(c => c.Id == "card_school_tide_n03");
            Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.Empty);
            Assert.That(CardSpecialCostPresentation.CostText(card), Is.Empty);
            Assert.That(CardSupplyCostPresentation.ResourceType(card), Is.EqualTo("wood"));
            Assert.That(CardSupplyCostPresentation.CostText(card), Is.EqualTo("1"));
            Assert.That(CardSchoolArtCatalog.HasSchoolIllustration(card), Is.True);
        }

        private NavalCardChoiceOverlay Choice(string id, System.Action<CoreNavalCardPlayOptions> confirm, System.Action cancel = null)
        {
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(index);
            NavalCardChoiceOverlay overlay = Rect("Choice").gameObject.AddComponent<NavalCardChoiceOverlay>();
            overlay.Configure(session, board, font, 0, session.GetBattleCard(index), session.GetNavalCardChoice(0), confirm, cancel);
            return overlay;
        }
        private static Button Button(Component parent, string name) => parent.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        private RectTransform Rect(string name)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(fixture.transform, false); rect.sizeDelta = new Vector2(1860f, 900f); return rect;
        }
        private void Pose(bool player, int x, int y) => typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(session.Battle, new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
    }
}
