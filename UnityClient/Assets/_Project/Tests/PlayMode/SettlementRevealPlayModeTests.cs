using System.Collections;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class SettlementRevealPlayModeTests
    {
        [UnityTest]
        public IEnumerator RevealRunsWithPausedGameTime_AndStopsAtExactRewards()
        {
            float previousScale = Time.timeScale;
            GameObject host = new GameObject("Isolated Settlement Animation", typeof(RectTransform));
            try
            {
                GameObject numberHost = new GameObject("Number", typeof(RectTransform), typeof(Text)); numberHost.transform.SetParent(host.transform, false);
                Text number = numberHost.GetComponent<Text>(); number.font = GameTypography.InterfaceFont;
                var reveal = host.AddComponent<SettlementRevealController>(); int cues = 0;
                reveal.Configure(new[] { number.rectTransform }, new[] { 0f }, new[] { number }, new[] { 1268 }, new[] { 0.65f }, null, null, true, (_, __) => cues++, true);
                Time.timeScale = 0; reveal.SetElapsed(2.5f);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(reveal.IsComplete, Is.True);
                Assert.That(number.text, Is.EqualTo("+1,268"));
                int cuesAtCompletion = cues;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(cues, Is.EqualTo(cuesAtCompletion), "The finite reveal must not loop sounds.");
                Assert.That(number.rectTransform.localScale.x, Is.EqualTo(1f).Within(0.0001f));
                reveal.CompleteImmediately(); Assert.That(cues, Is.EqualTo(cuesAtCompletion));
            }
            finally { Time.timeScale = previousScale; Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator SkipFinishesImmediately_WithoutPlayingMissedSoundCues()
        {
            GameObject host = new GameObject("Skippable Settlement Animation", typeof(RectTransform));
            try
            {
                var textHost = new GameObject("Number", typeof(RectTransform), typeof(Text)); textHost.transform.SetParent(host.transform, false);
                Text number = textHost.GetComponent<Text>(); number.font = GameTypography.InterfaceFont;
                int cues = 0; var reveal = host.AddComponent<SettlementRevealController>();
                reveal.Configure(new[] { number.rectTransform }, new[] { 0.9f }, new[] { number }, new[] { int.MaxValue }, new[] { 1f }, null, null, true, (_, __) => cues++, true);
                reveal.CompleteImmediately();
                yield return null;
                Assert.That(number.text, Is.EqualTo("+2,147,483,647"));
                Assert.That(cues, Is.Zero); Assert.That(reveal.IsComplete, Is.True);
            }
            finally { Object.Destroy(host); }
        }
    }
}
