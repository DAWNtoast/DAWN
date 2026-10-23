using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ReleaseSmokeArgumentsTests
    {
        [TestCase("ordinary", false)]
        [TestCase("-shatteredSailSmokeTest", true)]
        [TestCase("-shatteredSailSmokeTest=output", false)]
        [TestCase("-ShatteredSailSmokeTest", false)]
        public void OnlyExactExplicitSwitchEnablesReadOnlyStartup_EvenWithoutValidOutput(string argument, bool requested)
        {
            FieldInfo field = typeof(ReleasePlayerSmokeCheck).GetField("commandLineArguments", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = field.GetValue(null);
            try
            {
                field.SetValue(null, new[] { "game.exe", argument });
                var property = typeof(ReleasePlayerSmokeCheck).GetProperty("IsRequested", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(property.GetValue(null), Is.EqualTo(requested));
            }
            finally { field.SetValue(null, previous); }
        }

        [Test]
        public void PackagedSchoolCards_ReportsBaselineAndEveryUsedVariantWithoutFixedUnionCount()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ShatteredSailSmokeCards_" + Guid.NewGuid().ToString("N"));
            GameObject host = new GameObject("Packaged Card Smoke Regression");
            host.SetActive(false);
            ReleasePlayerSmokeCheck check = host.AddComponent<ReleasePlayerSmokeCheck>();
            try
            {
                Directory.CreateDirectory(directory);
                typeof(ReleasePlayerSmokeCheck).GetField("destination", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(check, directory);
                CardSchoolArtCatalog.ReleaseLoadedTextures();

                // Exercise the same resource loading, card geometry, font checks
                // and evidence writes as the packaged player, without launching it.
                Call(check, "CheckPackagedSchoolCards");

                CoreBattleCard[] cards = CoreVoyageSession.CardLibrary
                    .Where(card => card.AllowedStages != NavalBattleStages.None).ToArray();
                var used = new HashSet<string>(cards.Select(card => CardSchoolArtCatalog.FrameResourcePath(card)));
                var baseline = BaselineFramePaths();
                Assert.That(baseline.Count, Is.EqualTo(36));
                Assert.That(used.Except(baseline), Is.Not.Empty,
                    "Current card costs exercise complete frame variants beyond the original baseline.");
                var expected = new HashSet<string>(baseline);
                expected.UnionWith(used);
                string[] exported = File.ReadAllLines(Path.Combine(directory, "packaged-integrated-card-frames.txt"));
                Assert.That(exported, Is.EquivalentTo(expected),
                    "The evidence must include every baseline frame and every actual card-cost variant exactly once.");
                Assert.That(File.ReadAllLines(Path.Combine(directory, "packaged-used-card-frames.txt")),
                    Is.EquivalentTo(used));

                string[][] illustrations = ReadCsv(directory, "packaged-school-card-assets.csv",
                    "cardId,school,artId,resourcePath,width,height,fullSourceUv,sourceAspectPreserved");
                Assert.That(illustrations.Select(row => row[0]), Is.EquivalentTo(cards.Select(card => card.Id)));
                foreach (string[] row in illustrations)
                {
                    Assert.That(row.Length, Is.EqualTo(8), row[0]);
                    Assert.That(row[6], Is.EqualTo(bool.TrueString), row[0] + " full source UV");
                    Assert.That(row[7], Is.EqualTo(bool.TrueString), row[0] + " source aspect");
                }
                string[][] titles = ReadCsv(directory, "packaged-card-title-font.csv",
                    "cardId,font,baseAndUpgradeCharactersPresent,curveCalibrated");
                Assert.That(titles.Select(row => row[0]), Is.EquivalentTo(cards.Select(card => card.Id)));
                foreach (string[] row in titles)
                {
                    Assert.That(row.Length, Is.EqualTo(4), row[0]);
                    Assert.That(row[1], Is.EqualTo(GameTypography.InterfaceFont.name), row[0]);
                    Assert.That(row[2], Is.EqualTo(bool.TrueString), row[0] + " title characters");
                    Assert.That(row[3], Is.EqualTo(bool.TrueString), row[0] + " ribbon calibration");
                }

                // A later failure must retain actual completed counts without
                // claiming the subsequent crew check has already succeeded.
                var failures = (List<string>)typeof(ReleasePlayerSmokeCheck)
                    .GetField("failures", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(check);
                failures.Add("Simulated failure before crew checks.");
                string report = (string)Call(check, "CreateReport");
                Assert.That(report, Does.StartWith("FAIL\n"));
                Assert.That(report, Does.Contain(cards.Length + " card-by-card illustration checks"));
                Assert.That(report, Does.Contain(expected.Count + " integrated card-frame variants"));
                Assert.That(report, Does.Not.Contain("integrated crew dossier paintings"));
                Assert.That(report, Does.Not.Contain("verified packaged 48kHz audio clips"));
            }
            finally
            {
                CardSchoolArtCatalog.ReleaseLoadedTextures();
                Object.DestroyImmediate(host);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static HashSet<string> BaselineFramePaths()
        {
            const string prefix = "CardSchools/Frames/Integrated/frame-";
            var paths = new HashSet<string>();
            foreach (string school in new[] { "fire", "arcane", "curse", "tide", "thunder", "blood", "neutral" })
                foreach (string variant in new[] { "ap", "powder", "cannonball", "both" })
                    paths.Add(prefix + school + "-" + variant);
            foreach (var special in new[] { ("arcane", "mana"), ("blood", "health"), ("curse", "curse"), ("thunder", "chain") })
                foreach (string variant in new[] { "ap", "both" })
                    paths.Add(prefix + special.Item1 + "-" + variant + "-" + special.Item2);
            return paths;
        }

        private static string[][] ReadCsv(string directory, string filename, string header)
        {
            string[] lines = File.ReadAllLines(Path.Combine(directory, filename));
            Assert.That(lines, Is.Not.Empty, filename);
            Assert.That(lines[0], Is.EqualTo(header), filename);
            return lines.Skip(1).Select(line => line.Split(',')).ToArray();
        }

        private static object Call(ReleasePlayerSmokeCheck check, string methodName)
        {
            MethodInfo method = typeof(ReleasePlayerSmokeCheck).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            try { return method.Invoke(check, null); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
