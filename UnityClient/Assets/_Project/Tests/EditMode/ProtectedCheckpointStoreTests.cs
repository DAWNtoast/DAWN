using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Persistence;

namespace ShatteredSail.Core.Tests
{
    public sealed class ProtectedCheckpointStoreTests
    {
        private string directory;
        private ProtectedCheckpointStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ShatteredSailCheckpointTests-" + Guid.NewGuid().ToString("N"));
            store = new ProtectedCheckpointStore(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void MissingSaveReturnsNoPayloadOrWarning()
        {
            Assert.That(store.TryLoad(out string payload, out string notice), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(notice, Is.Null);
        }

        [Test]
        public void UnicodePayloadRoundTripsWithoutChangingLineBreaks()
        {
            const string expected = "{\"航程\":\"第七港口\",\"gold\":1.2345}\r\n\n⚓";
            Save(expected);
            var restarted = new ProtectedCheckpointStore(directory);
            Assert.That(restarted.TryLoad(out string payload, out string notice), Is.True);
            Assert.That(payload, Is.EqualTo(expected));
            Assert.That(notice, Is.Null);
        }

        [Test]
        public void FirstSaveAlreadyHasRecoverableSameGenerationBackup()
        {
            Save("first-voyage");
            File.WriteAllText(store.PrimaryPath, "interrupted primary");
            Assert.That(store.TryLoad(out string payload, out string notice), Is.True);
            Assert.That(payload, Is.EqualTo("first-voyage"));
            Assert.That(notice, Does.Contain("备份"));
            Assert.That(File.ReadAllText(store.PrimaryPath), Is.EqualTo("interrupted primary"));
        }

        [Test]
        public void ThreeDifferentPriorGenerationsAreRetained()
        {
            Save("one");
            Save("two");
            Save("three");
            Save("four");
            File.WriteAllText(store.PrimaryPath, "bad");
            Assert.That(Load(), Is.EqualTo("three"));
            File.WriteAllText(store.PrimaryPath + ".bak1", "bad");
            Assert.That(Load(), Is.EqualTo("two"));
            File.WriteAllText(store.PrimaryPath + ".bak2", "bad");
            Assert.That(Load(), Is.EqualTo("one"));
            Assert.That(File.Exists(store.PrimaryPath + ".bak4"), Is.False);
        }

        [Test]
        public void RepeatedIdenticalSavesDoNotConsumeHistory()
        {
            Save("one");
            Save("two");
            for (int index = 0; index < 10; index += 1) Save("two");
            File.WriteAllText(store.PrimaryPath, "bad");
            Assert.That(Load(), Is.EqualTo("one"));
        }

        [Test]
        public void PayloadTamperingFailsChecksumAndUsesBackup()
        {
            Save("older");
            Save("newer");
            string[] lines = File.ReadAllText(store.PrimaryPath).Split('\n');
            lines[4] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("tampered"));
            File.WriteAllText(store.PrimaryPath, string.Join("\n", lines));
            Assert.That(Load(), Is.EqualTo("older"));
        }

        [Test]
        public void AllCorruptGenerationsBlockWritesAndRemainByteIdentical()
        {
            Save("original");
            File.WriteAllBytes(store.PrimaryPath, new byte[] { 0xff, 0xfe, 0x00, 0x19 });
            File.WriteAllText(store.PrimaryPath + ".bak1", "broken-backup");
            byte[] original = File.ReadAllBytes(store.PrimaryPath);
            Assert.That(store.TryLoad(out string payload, out string notice), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(notice, Does.Contain("停止"));
            Assert.That(store.TrySave("new default progress", out string error), Is.False);
            Assert.That(error, Does.Contain("拒绝覆盖"));
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(original));
            Assert.That(File.ReadAllText(store.PrimaryPath + ".bak1"), Is.EqualTo("broken-backup"));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void UnsupportedSchemaBlocksFallbackAndOverwrite(int version)
        {
            Save("older");
            Save("newer");
            string[] lines = File.ReadAllText(store.PrimaryPath).Split('\n');
            lines[1] = version.ToString();
            string unsupported = string.Join("\n", lines);
            File.WriteAllText(store.PrimaryPath, unsupported);
            Assert.That(store.TryLoad(out string payload, out string notice), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(notice, Does.Contain("版本"));
            Assert.That(store.TrySave("overwrite", out string error), Is.False);
            Assert.That(error, Does.Contain("版本"));
            Assert.That(File.ReadAllText(store.PrimaryPath), Is.EqualTo(unsupported));
        }

        [Test]
        public void UnsupportedBackupAlsoBlocksSilentDowngrade()
        {
            Save("current");
            string[] lines = File.ReadAllText(store.PrimaryPath + ".bak1").Split('\n');
            lines[1] = "99";
            File.WriteAllText(store.PrimaryPath + ".bak1", string.Join("\n", lines));
            Assert.That(store.TryLoad(out string payload, out string notice), Is.False);
            Assert.That(notice, Does.Contain("版本"));
            Assert.That(store.TrySave("current", out string error), Is.False);
        }

        [Test]
        public void RecoverySavePreservesDamagedBytesBeforeReplacingPrimary()
        {
            Save("original");
            byte[] damaged = { 0xc3, 0x28, 0xff, 0x10 };
            File.WriteAllBytes(store.PrimaryPath, damaged);
            Assert.That(Load(), Is.EqualTo("original"));
            Save("continued");
            Assert.That(Load(), Is.EqualTo("continued"));
            string preserved = Directory.GetFiles(directory, ProtectedCheckpointStore.FileName + ".corrupt-*").Single();
            Assert.That(File.ReadAllBytes(preserved), Is.EqualTo(damaged));
        }

        [Test]
        public void UncommittedPartialTemporaryFilesAreIgnored()
        {
            Save("committed");
            File.WriteAllText(store.PrimaryPath + ".tmp-interrupted", "incomplete");
            Assert.That(Load(), Is.EqualTo("committed"));
            Save("next");
            Assert.That(Load(), Is.EqualTo("next"));
        }

        [Test]
        public void FailedBackupWriteDoesNotReplacePrimary()
        {
            Save("original");
            File.Delete(store.PrimaryPath + ".bak1");
            Directory.CreateDirectory(store.PrimaryPath + ".bak1");
            Assert.That(store.TrySave("not committed", out string error), Is.False);
            Assert.That(error, Does.Contain("失败"));
            Assert.That(Load(), Is.EqualTo("original"));
            Assert.That(Directory.GetFiles(directory, "*.tmp-*").Length, Is.Zero);
        }

        [Test]
        public void ConcurrentStoreLockReportsFailureInsteadOfRacingWrites()
        {
            Save("original");
            using (var held = new FileStream(store.PrimaryPath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.That(new ProtectedCheckpointStore(directory).TrySave("racing", out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(store.TryLoad(out string payload, out string notice), Is.False);
                Assert.That(notice, Is.Not.Empty);
            }
            Assert.That(Load(), Is.EqualTo("original"));
        }

        [Test]
        public void ClearMarkerPreventsOldPrimaryAndBackupsFromResurrecting()
        {
            Save("already claimed rewards");
            byte[] stale = File.ReadAllBytes(store.PrimaryPath);
            Assert.That(store.TryClear(out string error), Is.True, error);
            // Simulate cleanup interrupted after the durable clear commit.
            File.WriteAllBytes(store.PrimaryPath, stale);
            File.WriteAllBytes(store.PrimaryPath + ".bak1", stale);
            Assert.That(new ProtectedCheckpointStore(directory).TryLoad(out string payload, out string notice), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(notice, Is.Null);
        }

        [Test]
        public void FirstSaveAfterClearHasItsOwnRedundantBackup()
        {
            Save("old voyage");
            Assert.That(store.TryClear(out string error), Is.True, error);
            Save("new voyage");
            File.WriteAllText(store.PrimaryPath, "bad");
            Assert.That(Load(), Is.EqualTo("new voyage"));
        }

        [Test]
        public void FailedNewSaveKeepsClearMarkerAndCannotLoadOldVoyage()
        {
            Save("old voyage");
            Assert.That(store.TryClear(out string error), Is.True, error);
            Directory.CreateDirectory(store.PrimaryPath + ".bak1");
            Assert.That(store.TrySave("new voyage", out error), Is.False);
            Assert.That(store.TryLoad(out string payload, out string notice), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(notice, Is.Null);
        }

        [Test]
        public void CorruptClearMarkerBlocksReadingAndSaving()
        {
            Save("old voyage");
            File.WriteAllText(store.PrimaryPath + ".cleared", "incomplete marker");
            Assert.That(store.TryLoad(out string payload, out string notice), Is.False);
            Assert.That(notice, Is.Not.Empty);
            Assert.That(store.TrySave("new voyage", out string error), Is.False);
            Assert.That(File.ReadAllText(store.PrimaryPath + ".cleared"), Is.EqualTo("incomplete marker"));
        }

        [Test]
        public void ExplicitClearPreservesCorruptEvidenceAndAllowsNewVoyage()
        {
            Save("old voyage");
            File.WriteAllText(store.PrimaryPath, "corrupt record");
            File.WriteAllText(store.PrimaryPath + ".bak1", "corrupt backup");
            Assert.That(store.TryClear(out string error), Is.True, error);
            Assert.That(Directory.GetFiles(directory, "*.corrupt-*").Length, Is.EqualTo(2));
            Save("fresh voyage");
            Assert.That(Load(), Is.EqualTo("fresh voyage"));
        }

        private void Save(string payload)
        {
            Assert.That(store.TrySave(payload, out string error), Is.True, error);
        }

        private string Load()
        {
            Assert.That(store.TryLoad(out string payload, out string notice), Is.True, notice);
            return payload;
        }
    }
}
