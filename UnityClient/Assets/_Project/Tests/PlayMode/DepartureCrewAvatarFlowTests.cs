using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureCrewAvatars_AllThirtySixIdentitiesUseTheirSmallPortraitsAfterScrollingAndToggling()
        {
            CrewVariantDefinition[] identities = CrewNarrativeCatalog.AllCrew.ToArray();
            yield return LoadDepartureCrewOverlayFixture(identities);
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            AssertDepartureAvatarRows(identities);
            int selected = controller.Session.SelectedSpecialCrewCount;
            DepartureCrewChoiceButtonForIdentity(identities[0].Id).onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(selected - 1));
            AssertDepartureAvatarRows(identities);
            DepartureCrewChoiceButtonForIdentity(identities[0].Id).onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(selected));
            var scroll = GameObject.Find("Departure Special Crew Scroll").GetComponent<ScrollRect>();
            for (int page = 0; page < 6; page++)
            {
                scroll.verticalNormalizedPosition = 1f - page / 5f;
                yield return null;
                yield return WaitForInvestmentAndCrewScreenToSettle("Departure Crew Configuration");
                AssertDepartureAvatarRows(identities);
                NavalBattleVisualCapture.Capture(controller, "CrewAvatars_" + page + ".png",
                    "真实出航船员名单：36个身份头像，滚动页 " + (page + 1), "Departure Crew Configuration");
            }
            yield return ClickShipyardControl("Confirm Departure Crew");
            Assert.That(GameObject.Find("Departure Crew Overlay"), Is.Null);
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(selected));
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertDepartureAvatarRows(CrewVariantDefinition[] identities)
        {
            Button[] rows = DepartureCrewChoiceButtons();
            Assert.That(rows.Select(DepartureCrewChoiceIdentity), Is.EqualTo(identities.Select(crew => crew.Id)));
            foreach (Button row in rows)
            {
                string identity = DepartureCrewChoiceIdentity(row);
                RawImage image = row.GetComponentsInChildren<RawImage>(true).Single(item => item.name.StartsWith("Departure Special Crew Portrait "));
                Texture2D expected = Resources.Load<Texture2D>("PortArt/CrewAvatars/" + identity + "_avatar_v1");
                Assert.That(expected, Is.Not.Null, identity);
                Assert.That(image.texture, Is.SameAs(expected), identity);
                Assert.That(image.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), identity);
                Assert.That(image.raycastTarget, Is.False, identity);
                Assert.That(image.rectTransform.rect.width, Is.LessThanOrEqualTo(82.1f));
                Assert.That(image.rectTransform.rect.height, Is.LessThanOrEqualTo(108.1f));
            }
        }
    }
}
