using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
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
        public void FieldChoice_RejectsAnyOutOfRangeCellAndCancelNeverPays()
        {
            int energy = session.Battle.Energy, powder = session.Cargo.Powder;
            bool cancelled = false, committed = false;
            NavalCardChoiceOverlay overlay = Choice("card_school_fire_n01", _ => committed = true, () => cancelled = true);
            overlay.SelectCell(3, 0); // One corner (4,1) is five Manhattan cells away.
            Assert.That(Button(overlay, "Confirm Naval Card Choice").interactable, Is.False);
            Assert.That(overlay.GetComponentInChildren<NavalFieldGraphic>().Cells.Any(c => c.Invalid && c.Position.X == 400 && c.Position.Y == 100), Is.True);
            overlay.ConfirmSelection();
            Assert.That(committed, Is.False);
            Button(overlay, "Cancel Naval Card Choice").onClick.Invoke();
            Assert.That(cancelled, Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy)); Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1)); Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void FieldChoice_OnlyConfirmationCommitsTheSelectedWorldShape()
        {
            bool committed = false;
            NavalCardChoiceOverlay overlay = Choice("card_school_fire_n01", options => committed = session.PlayNavalCard(0, options));
            int energy = session.Battle.Energy;
            overlay.SelectCell(-1, 1);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(Button(overlay, "Confirm Naval Card Choice").interactable, Is.True);
            overlay.ConfirmSelection();
            Assert.That(committed, Is.True); Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            Assert.That(session.Battle.NavalFields.Cells.All(c => c.X >= -1 && c.X <= 0 && c.Y >= 1 && c.Y <= 2), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - 2));
        }

        [Test]
        public void WaveChoice_SelectsOnlyDirectionAndPerpendicularShapeThenConsumesAllTide()
        {
            typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(session.Battle.Schools, 5);
            CoreNavalCardPlayOptions selected = null;
            NavalCardChoiceOverlay overlay = Choice("card_school_tide_n06", options => selected = options);
            Assert.That(overlay.GetComponentsInChildren<Button>(true).Any(b => b.name == "Naval resource plus" || b.name == "Naval resource minus"), Is.False);
            Button(overlay, "Naval field direction 1").onClick.Invoke();
            overlay.SelectCell(0, 0);
            Assert.That(overlay.Draft.ResourceAmount, Is.Zero, "All-resource cards have no selectable resource amount.");
            Assert.That(overlay.Draft.DirectionX, Is.Zero); Assert.That(overlay.Draft.DirectionY, Is.EqualTo(1));
            var selectedCells = overlay.GetComponentInChildren<NavalFieldGraphic>().Cells.Where(c => c.Fill.a > .3f).ToArray();
            Assert.That(selectedCells.Length, Is.EqualTo(3));
            Assert.That(selectedCells.All(c => c.Position.Y == 0), Is.True, "A northbound wave occupies a horizontal row.");
            overlay.ConfirmSelection();
            Assert.That(selected, Is.Not.Null); Assert.That(session.Battle.Schools.TidePower, Is.EqualTo(5), "The draft callback has not played the card.");
            Assert.That(session.GetNavalChoiceUnavailableReason(0, selected), Is.Empty);
            Assert.That(session.PlayNavalCard(0, selected), Is.True);
            Assert.That(session.Battle.Schools.TidePower, Is.Zero, "Committing the selected area pays the entire current tide total.");
        }

        [Test]
        public void PersistentFields_FollowWorldProjectionAndShowOwnershipExpiryAndWaterDirection()
        {
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Tide, true, -1, 1, false, 0, 1), Is.True);
            NavalFieldBoardOverlay overlay = board.gameObject.AddComponent<NavalFieldBoardOverlay>();
            overlay.Configure(session, board, font);
            NavalFieldGraphic graphic = board.GetComponentsInChildren<NavalFieldGraphic>().Single(g => g.name == "Persistent Naval Fields");
            Assert.That(graphic.Cells.Count, Is.EqualTo(4));
            Assert.That(graphic.Cells.All(c => c.DirectionX == 0 && c.DirectionY == 1), Is.True);
            Text label = graphic.GetComponentsInChildren<Text>().First();
            Assert.That(label.text, Is.EqualTo("我·流2"));
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
            anchor.onClick.Invoke();
            Assert.That(session.PlayerRefuseNavalForcedMovement, Is.False);
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
        private static Button Button(Component parent, string name) => parent.GetComponentsInChildren<Button>().Single(b => b.name == name);
        private RectTransform Rect(string name)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(fixture.transform, false); rect.sizeDelta = new Vector2(1860f, 900f); return rect;
        }
        private void Pose(bool player, int x, int y) => typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(session.Battle, new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
    }
}
