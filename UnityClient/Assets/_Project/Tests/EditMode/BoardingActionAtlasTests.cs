using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class BoardingActionAtlasTests
    {
        [TestCase("crew_salt_crown_diver_isha", BoardingActorAction.Attack)]
        [TestCase("crew_gray_gull_diver_noya", BoardingActorAction.Cast)]
        [TestCase("crew_white_shell_doctor_mirei", BoardingActorAction.Cast)]
        [TestCase("crew_salt_crown_cook_maz", BoardingActorAction.Cast)]
        [TestCase("crew_twilight_tide_carpenter_orlan", BoardingActorAction.Cast)]
        public void SpecialistCardUsesItsOwnersSignatureGesture(string id, BoardingActorAction expected)
        {
            var card = CoreSpecialCardCatalog.ForCrew(id).Card;
            Assert.That(card.IsAllowedInStage(NavalBattleStage.Boarding), Is.True);
            Assert.That(BoardingCardActorPresentation.ForCard(card), Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GuestReturnsBorrowedFlankOnCompletionOrCancellation(bool cancel)
        {
            GameObject root = new GameObject("boarding side station", typeof(RectTransform), typeof(RawImage));
            try
            {
                RawImage image = root.GetComponent<RawImage>();
                image.texture = Texture2D.whiteTexture;
                BoardingActorGraphic actor = root.AddComponent<BoardingActorGraphic>();
                actor.Configure(false, 1f, 0f, .1f, "resident", "resident-card", true);
                actor.PlayGuestAction("visiting", "visiting-card", "visitor", Texture2D.blackTexture, BoardingActorAction.Cast, .6f);
                actor.SetPresentationTime(.3f);
                Assert.That(actor.Identity, Is.EqualTo("visiting"));
                Assert.That(actor.CardId, Is.EqualTo("visiting-card"));
                Assert.That(image.texture, Is.SameAs(Texture2D.blackTexture));
                Assert.That(image.uvRect, Is.EqualTo(BoardingActorGraphic.FrameUv(BoardingActorAction.Cast, .5f)));
                if (cancel) actor.SetMotionSuspended(true);
                else actor.AdvancePresentation(.4f);
                Assert.That(actor.Identity, Is.EqualTo("resident"));
                Assert.That(actor.CardId, Is.EqualTo("resident-card"));
                Assert.That(image.texture, Is.SameAs(Texture2D.whiteTexture));
                Assert.That(image.uvRect, Is.EqualTo(BoardingActorGraphic.FrameUv(BoardingActorAction.Idle, 0f)));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void IncomingBlockInterruptsCastAndDeathHoldsUntilRestoration()
        {
            GameObject root = new GameObject("boarding defender", typeof(RectTransform), typeof(RawImage));
            try
            {
                var actor = root.AddComponent<BoardingActorGraphic>();
                actor.Configure(false, -1f, 0f, .1f, "enemy_party_center", null, true);
                actor.PlayAction(BoardingActorAction.Cast, .6f);
                actor.React(BoardingActorAction.Guard, .34f);
                Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Guard));
                actor.PlayAction(BoardingActorAction.Death, .72f);
                actor.SetPresentationTime(1f);
                Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Death));
                Assert.That(actor.GetComponent<RawImage>().uvRect, Is.EqualTo(BoardingActorGraphic.FrameUv(BoardingActorAction.Death, 1f)));
                actor.React(BoardingActorAction.Hit, .34f);
                Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Death));
                actor.PlayAction(BoardingActorAction.Idle);
                Assert.That(actor.GetComponent<RawImage>().uvRect, Is.EqualTo(BoardingActorGraphic.FrameUv(BoardingActorAction.Idle, 0f)));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(BoardingActorAction.Attack, 0)]
        [TestCase(BoardingActorAction.Hit, 1)]
        [TestCase(BoardingActorAction.Cast, 2)]
        [TestCase(BoardingActorAction.Heal, 2)]
        [TestCase(BoardingActorAction.Prepare, 2)]
        [TestCase(BoardingActorAction.Guard, 3)]
        public void NamedFormationPlaysEightPaintedFramesAndReturnsToRest(BoardingActorAction action, int rowFromTop)
        {
            var root = new GameObject("iron oath", typeof(RectTransform), typeof(RawImage));
            try
            {
                var figure = root.GetComponent<RawImage>();
                var initialUv = new Rect(.005f, .006f, .99f, .988f);
                figure.texture = Texture2D.whiteTexture;
                figure.uvRect = initialUv;
                var actor = root.AddComponent<BoardingActorGraphic>();
                actor.Configure(true, -1f, 0f, .03f, "enemy_iron_oath");
                var sheet = Resources.Load<Texture2D>("BoardingBattle/BossActions/enemy_iron_oath");
                Assert.That(sheet, Is.Not.Null);

                actor.PlayAction(action, 1f);
                Assert.That(figure.texture, Is.SameAs(sheet));
                for (int frame = 0; frame < 8; frame++)
                {
                    actor.SetPresentationTime((frame + .25f) / 8f);
                    Assert.That(figure.uvRect.center.x, Is.EqualTo((frame + .5f) / 8f).Within(.0001f));
                    Assert.That(figure.uvRect.center.y, Is.EqualTo(1f - (rowFromTop + .5f) / 4f).Within(.0001f));
                    Assert.That(actor.SampleOffset(new Vector2(.52f, .83f)), Is.EqualTo(Vector2.zero));
                }
                actor.AdvancePresentation(.2f);
                Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle));
                Assert.That(figure.texture, Is.SameAs(Texture2D.whiteTexture));
                Assert.That(figure.uvRect, Is.EqualTo(initialUv));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void NamedDefeatUsesItsSixPaintedFramesAndRestoresTheRestingIllustration()
        {
            var root = new GameObject("iron oath", typeof(RectTransform), typeof(RawImage));
            try
            {
                var figure = root.GetComponent<RawImage>();
                figure.rectTransform.sizeDelta = new Vector2(504f, 336f);
                figure.texture = Texture2D.whiteTexture;
                var initialUv = new Rect(.005f, .006f, .99f, .988f);
                figure.uvRect = initialUv;
                var actor = root.AddComponent<BoardingActorGraphic>();
                actor.Configure(true, -1f, 0f, .03f, "enemy_iron_oath");
                actor.PlayAction(BoardingActorAction.Attack, .5f);
                actor.SetPresentationTime(.20f);
                actor.React(BoardingActorAction.Hit, .35f);
                Assert.That(actor.UsesBossActionAtlas, Is.True);
                Assert.That(figure.uvRect.center.y, Is.EqualTo(.625f).Within(.0001f));
                actor.PlayAction(BoardingActorAction.Death, .72f);
                Assert.That(actor.UsesBossActionAtlas, Is.False);
                Assert.That(actor.UsesDefeatAtlas, Is.True);
                Assert.That(figure.texture, Is.SameAs(Resources.Load<Texture2D>("BoardingBattle/Defeats/enemy_iron_oath")));
                for (var frame = 0; frame < 6; frame++)
                {
                    actor.SetPresentationTime(.72f * ((frame + .25f) / 6f));
                    Assert.That(figure.uvRect.center.x, Is.EqualTo((frame + .5f) / 6f).Within(.0001f));
                    Assert.That(actor.SampleOffset(new Vector2(.5f, .82f)), Is.EqualTo(Vector2.zero));
                }
                actor.SetMotionSuspended(true);
                Assert.That(actor.UsesDefeatAtlas, Is.False);
                Assert.That(figure.texture, Is.SameAs(Texture2D.whiteTexture));
                Assert.That(figure.uvRect, Is.EqualTo(initialUv));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
