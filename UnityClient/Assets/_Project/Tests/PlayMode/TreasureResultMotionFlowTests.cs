using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator TreasureResultMotion_ThirdMapUsesItsOwnPageAndRejectedCraftsNeverReveal()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 900, identifiedMoonlitFragments = 4,
                maps = new[] {
                    new CoreTreasureMapSave { id = "motion-old-0", seed = "MOTION-OLD-0", rankId = "weathered" },
                    new CoreTreasureMapSave { id = "motion-old-1", seed = "MOTION-OLD-1", rankId = "gilded" }
                }
            });
            Assert.That(session.CanCraftTreasureMap("moonlit", out string reason), Is.True, reason);
            ClickAcceptanceButton("Port Hotspot Treasure");
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            ClickAcceptanceButton("Treasure Route 2");
            yield return null;
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(3));
            string seed = session.TreasureMaps[2].Seed;
            Assert.That(seed, Is.Not.EqualTo("MOTION-OLD-0").And.Not.EqualTo("MOTION-OLD-1"));
            Assert.That(GameObject.Find("Treasure Folio Page").GetComponent<Text>().text, Is.EqualTo("2 / 2"));
            Assert.That(GameObject.Find("Treasure Map Details 0").GetComponent<Text>().text, Does.Contain(seed));
            Assert.That(GameObject.Find("Treasure Folio Card 1"), Is.Null);
            var motion = Object.FindFirstObjectByType<TreasureResultMotion>();
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.transform.parent.name, Is.EqualTo("Treasure Folio Card 0"));
            RawImage target = GameObject.Find("Treasure Map Preview 0").GetComponent<RawImage>();
            Assert.That(target.texture.name, Is.EqualTo("treasure_chart_moonlit"));
            RawImage reveal = motion.transform.Find("Unfolding Chart/Revealed Chart").GetComponent<RawImage>();
            Assert.That(reveal.texture, Is.SameAs(target.texture));
            motion.SetElapsed(1.25f);
            Canvas.ForceUpdateCanvases();
            NavalBattleVisualCapture.Capture(controller, "TreasureMotion_06_NewMapPage.png",
                "已有两张旧海图，新增第三张月蚀海图直接定位第 2 页，贴图与种子属于新增海图。", "Treasure Folio");
            string committed = JsonUtility.ToJson(session.CaptureTreasureProgress());
            ClickAcceptanceButton("Treasure Close Folio");
            yield return null;
            yield return null;
            Assert.That(motion == null || motion.IsComplete, Is.True);
            Assert.That(Object.FindFirstObjectByType<TreasureResultMotion>(), Is.Null);
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(committed));

            // The same board remains clickable for inspection, but has no fragments left.
            ClickAcceptanceButton("Treasure Route 2");
            yield return null;
            Assert.That(Object.FindFirstObjectByType<TreasureResultMotion>(), Is.Null);
            Assert.That(GameObject.Find("Treasure Folio"), Is.Null);
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(committed));

            ClickAcceptanceButton("Return To Port Hub");
            yield return null;
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 900, identifiedMoonlitFragments = 4,
                maps = Enumerable.Range(0, CoreTreasureHuntCatalog.MaxHeldMaps).Select(index =>
                    new CoreTreasureMapSave { id = "motion-full-" + index, seed = "MOTION-FULL-" + index,
                        rankId = "weathered" }).ToArray()
            });
            string full = JsonUtility.ToJson(session.CaptureTreasureProgress());
            ClickAcceptanceButton("Port Hotspot Treasure");
            yield return null;
            ClickAcceptanceButton("Treasure Route 2");
            yield return null;
            Assert.That(Object.FindFirstObjectByType<TreasureResultMotion>(), Is.Null);
            Assert.That(GameObject.Find("Treasure Folio"), Is.Null);
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(CoreTreasureHuntCatalog.MaxHeldMaps));
            Assert.That(session.GetIdentifiedTreasureFragments("moonlit"), Is.EqualTo(4));
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(full));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TreasureResultMotion_CommitsOnceSupportsImmediateContinuationAndShowsTheNewMap()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 100, fragments = 1, identifiedWeatheredFragments = 3
            });
            ClickAcceptanceButton("Port Hotspot Treasure");
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            decimal gold = session.ProfileGold;
            Button identify = ActiveButton("Treasure Identify Fragment");
            identify.onClick.Invoke(); identify.onClick.Invoke();
            yield return null;
            Assert.That(session.TreasureFragments, Is.Zero);
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(4));
            var identified = Object.FindFirstObjectByType<TreasureResultMotion>();
            Assert.That(identified, Is.Not.Null);
            Assert.That(identified.IsCrafting, Is.False);
            identified.SetElapsed(.27f);
            Canvas.ForceUpdateCanvases();
            NavalBattleVisualCapture.Capture(controller, "TreasureMotion_01_Identify.png",
                "鉴定结果已入账，碎片短暂揭示并显示所属等级，可立即继续或跳过。", "Treasure Panel");
            identified.SetElapsed(.8f);
            Canvas.ForceUpdateCanvases();
            NavalBattleVisualCapture.Capture(controller, "TreasureMotion_02_Landing.png",
                "碎片沿短弧线归入对应透视海图。", "Treasure Panel");
            Button craft = ActiveButton("Treasure Route 0");
            craft.onClick.Invoke(); craft.onClick.Invoke();
            yield return null;
            Assert.That(identified == null || identified.IsComplete, Is.True, "Continuing must cancel the old presentation.");
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.Zero);
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(1));
            string seed = session.TreasureMaps[0].Seed;
            var motion = Object.FindFirstObjectByType<TreasureResultMotion>();
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.IsCrafting, Is.True);
            motion.SetElapsed(.47f);
            Canvas.ForceUpdateCanvases();
            NavalBattleVisualCapture.Capture(controller, "TreasureMotion_03_Assemble.png",
                "四片藏宝图在图册内依次拼合，文字与返回操作不被遮挡。", "Treasure Folio");
            motion.SetElapsed(1.29f);
            Canvas.ForceUpdateCanvases();
            NavalBattleVisualCapture.Capture(controller, "TreasureMotion_04_Unfold.png",
                "拼合后纸面从中央向两侧展开，接缝轻亮收束。", "Treasure Folio");
            motion.CompleteImmediately(); motion.CompleteImmediately();
            yield return null;
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(1));
            Assert.That(session.TreasureMaps[0].Seed, Is.EqualTo(seed));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            RawImage map = GameObject.Find("Treasure Map Preview 0").GetComponent<RawImage>();
            Assert.That(map.GetComponent<CanvasGroup>(), Is.Null);
            NavalBattleVisualCapture.Capture(controller, "TreasureMotion_05_Complete.png",
                "跳过演出立即恢复完整海图，不再次合成、不重复扣除碎片。", "Treasure Folio");
            ClickAcceptanceButton("Treasure Close Folio");
            yield return null;
            ActiveButton("Treasure Identify Fragment").onClick.Invoke();
            Assert.That(Object.FindFirstObjectByType<TreasureResultMotion>(), Is.Null,
                "An unavailable identification must not create a success reveal.");
            LogAssert.NoUnexpectedReceived();
        }
    }

    public sealed class TreasureResultMotionLifecycleTests
    {
        [UnityTest]
        public IEnumerator DestroyedRevealRestoresItsTarget_AndLeavesNoMaskOnThePersistentImage()
        {
            var host = new GameObject("Treasure Result Destruction", typeof(RectTransform));
            try
            {
                var art = new GameObject("Committed Map", typeof(RectTransform), typeof(RawImage));
                art.transform.SetParent(host.transform, false);
                RawImage target = art.GetComponent<RawImage>();
                target.rectTransform.sizeDelta = new Vector2(636f, 357.75f);
                TreasureResultMotion motion = TreasureResultMotion.PlayCraft(target);
                motion.SetElapsed(1.3f);
                Assert.That(target.GetComponent<CanvasGroup>().alpha, Is.Zero);
                Object.Destroy(motion.gameObject);
                // Destruction dispatches OnDisable/OnDestroy at the end of this frame;
                // the temporary CanvasGroup is then removed by its own queued Destroy.
                yield return null;
                yield return null;
                Assert.That(target.GetComponent<CanvasGroup>(), Is.Null);
                Assert.That(target.GetComponent<RectMask2D>(), Is.Null);
                Assert.That(target.rectTransform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(target.gameObject.activeSelf, Is.True);
            }
            finally { Object.Destroy(host); }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PresentationUsesUnscaledTime_AndDisableRestoresHiddenArt()
        {
            float previousScale = Time.timeScale;
            var host = new GameObject("Treasure Motion Lifecycle", typeof(RectTransform));
            try
            {
                var art = new GameObject("Committed Map", typeof(RectTransform), typeof(RawImage));
                art.transform.SetParent(host.transform, false);
                RawImage target = art.GetComponent<RawImage>(); target.rectTransform.sizeDelta = new Vector2(636f, 357.75f);
                Time.timeScale = 0f;
                var motion = TreasureResultMotion.PlayCraft(target);
                motion.SetElapsed(TreasureResultMotion.CraftDuration - .05f);
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(motion.IsComplete, Is.True);
                Assert.That(target.GetComponent<CanvasGroup>(), Is.Null);
                var interrupted = TreasureResultMotion.PlayCraft(target);
                interrupted.SetElapsed(.4f);
                interrupted.enabled = false;
                Assert.That(interrupted.gameObject.activeSelf, Is.False);
                Assert.That(target.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                yield return null;
                Assert.That(target.GetComponent<CanvasGroup>(), Is.Null);
            }
            finally { Time.timeScale = previousScale; Object.Destroy(host); }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
