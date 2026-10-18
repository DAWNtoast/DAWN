using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DesktopReleaseBuilderTests
    {
        private string directory;
        // Keep the existing test assembly independent of the Editor assembly.
        private static Type Builder => Type.GetType("ShatteredSail.Editor.DesktopReleaseBuilder, ShatteredSail.Editor", true);

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ShatteredSailReleaseTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ExplicitVersionOverridesTheCompatibleDefaultWithoutChangingSource()
        {
            Assert.That(Version(), Is.EqualTo("0.3.20261015.3"));
            Assert.That(Version("-batchmode", "-releaseVersion", "0.3.20261016.1", "-quit"), Is.EqualTo("0.3.20261016.1"));
            Assert.That(Version("-releaseVersion", "0.3.20261016"), Is.EqualTo("0.3.20261016"));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("0.3")]
        [TestCase("0.3.20261016.1.2")]
        [TestCase("v0.3.20261016")]
        [TestCase("0.3.20261016\n")]
        [TestCase("0.3.20261016\nResult: Succeeded")]
        public void InvalidVersionCannotReachBuildSettingsOrInjectReportLines(string value)
        {
            Assert.Throws<ArgumentException>(() => Version("-releaseVersion", value));
        }

        [Test]
        public void MissingOrDuplicateValuesAreRejectedInsteadOfSilentlySelectingARelease()
        {
            Assert.Throws<ArgumentException>(() => Version("-releaseVersion"));
            Assert.Throws<ArgumentException>(() => Version("-releaseVersion", "-quit"));
            Assert.Throws<ArgumentException>(() => Version("-releaseVersion", "0.3.20261016", "-releaseVersion", "0.3.20261017"));
            Assert.Throws<ArgumentException>(() => Call("Argument", new[] { "-releaseOutput", "-logFile", "log.txt" }, "-releaseOutput"));
            Assert.Throws<ArgumentException>(() => Call("Argument", new[] { "-releaseOutput", "first.exe", "-releaseOutput", "second.exe" }, "-releaseOutput"));
        }

        [TestCase(BuildTarget.StandaloneWindows64, "第七港口.exe")]
        [TestCase(BuildTarget.StandaloneOSX, "第七港口.app")]
        public void EmptyAndNewOutputDirectoriesAreAcceptedWithoutWritingFiles(BuildTarget target, string name)
        {
            string existing = Path.Combine(directory, name);
            Assert.That(Call("ValidateOutput", existing, target), Is.EqualTo(Path.GetFullPath(existing)));
            Assert.That(Call("ValidateOutput", existing.Replace('\\', '/'), target), Is.EqualTo(Path.GetFullPath(existing)));
            string fresh = Path.Combine(directory, "new", name);
            Assert.That(Call("ValidateOutput", fresh, target), Is.EqualTo(Path.GetFullPath(fresh)));
            Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
        }

        [TestCase(BuildTarget.StandaloneWindows64, "第七港口.exe")]
        [TestCase(BuildTarget.StandaloneOSX, "第七港口.app")]
        public void ExistingFilesBundlesAndMixedDirectoriesCannotBeOverwritten(BuildTarget target, string name)
        {
            string output = Path.Combine(directory, name);
            File.WriteAllText(output, "existing release");
            Assert.Throws<IOException>(() => Call("ValidateOutput", output, target));
            Assert.That(File.ReadAllText(output), Is.EqualTo("existing release"));
            File.Delete(output);
            Directory.CreateDirectory(output);
            Assert.Throws<IOException>(() => Call("ValidateOutput", output, target));
            Directory.Delete(output);
            string other = Path.Combine(directory, "old-data.txt");
            File.WriteAllText(other, "preserve me");
            Assert.Throws<IOException>(() => Call("ValidateOutput", output, target));
            Assert.That(File.ReadAllText(other), Is.EqualTo("preserve me"));
        }

        [Test]
        public void WrongPlatformExtensionAndRelativePathsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => Call("ValidateOutput", "player.exe", BuildTarget.StandaloneWindows64));
            Assert.Throws<ArgumentException>(() => Call("ValidateOutput", Path.Combine(directory, "player.app"), BuildTarget.StandaloneWindows64));
            Assert.Throws<ArgumentException>(() => Call("ValidateOutput", Path.Combine(directory, "player.exe"), BuildTarget.StandaloneOSX));
            Assert.Throws<ArgumentException>(() => Call("ValidateOutput", Path.Combine(directory, "player.exe"), BuildTarget.Android));
            if (Path.DirectorySeparatorChar == '\\')
            {
                Assert.Throws<ArgumentException>(() => Call("ValidateOutput", "C:player.exe", BuildTarget.StandaloneWindows64));
                Assert.Throws<ArgumentException>(() => Call("ValidateOutput", "\\player.exe", BuildTarget.StandaloneWindows64));
            }
        }

        private static string Version(params string[] args) => (string)Call("ResolveVersion", (object)args);

        private static object Call(string name, params object[] args)
        {
            try { return Builder.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
