using System;
using System.IO;
using System.Text;
using System.Xml;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(ShatteredSail.Presentation.Tests.AudioCapturePlayerResults))]

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>Explicitly opt-in, Player-only NUnit evidence writer for isolated audio capture.</summary>
    [Preserve]
    public sealed class AudioCapturePlayerResults : ITestRunCallback
    {
        private string outputPath;
        private bool finished;

        public void RunStarted(ITest testsToRun)
        {
            if (!Requested()) return;
            try
            {
                outputPath = ValueAfter("-audioCaptureResults");
                if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathRooted(outputPath))
                    throw new InvalidOperationException("Provide a fresh absolute -audioCaptureResults path.");
                outputPath = Path.GetFullPath(outputPath);
                if (!Directory.Exists(Path.GetDirectoryName(outputPath)) ||
                    File.Exists(outputPath) || File.Exists(outputPath + ".pending"))
                    throw new InvalidOperationException("Player result directory must exist; evidence paths must be new: " + outputPath);
                Debug.Log("AUDIO_CAPTURE_PLAYER_TESTS_STARTED " + outputPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Application.Quit(2);
                throw;
            }
        }

        public void RunFinished(ITestResult testResults)
        {
            if (!Requested() || finished) return;
            finished = true;
            try
            {
                if (string.IsNullOrEmpty(outputPath))
                    throw new InvalidOperationException("Player test result writer was not initialized.");
                // The entire result tree, including failures and assertions, comes
                // directly from NUnit. No test result is inferred from the capture files.
                var results = new XmlDocument();
                results.LoadXml(testResults.ToXml(true).OuterXml);
                if (results.DocumentElement.Name != "test-run")
                {
                    // Unity's loaded test tree normally serializes as test-suite.
                    // Retain that complete native tree and mirror its actual counts
                    // onto a test-run envelope used by the existing report reader.
                    XmlElement tree = results.DocumentElement;
                    XmlElement envelope = results.CreateElement("test-run");
                    foreach (XmlAttribute attribute in tree.Attributes)
                        envelope.SetAttribute(attribute.Name, attribute.Value);
                    results.RemoveChild(tree);
                    results.AppendChild(envelope);
                    envelope.AppendChild(tree);
                }
                string pending = outputPath + ".pending";
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
                    { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
                        results.Save(writer);
                    stream.Flush(true);
                }
                File.Move(pending, outputPath);
                bool passed = testResults.ResultState.Status == TestStatus.Passed &&
                    testResults.PassCount == 3 && testResults.FailCount == 0 &&
                    testResults.SkipCount == 0 && testResults.InconclusiveCount == 0;
                Debug.Log("AUDIO_CAPTURE_PLAYER_TESTS_FINISHED " + testResults.ResultState + " " + outputPath);
                Application.Quit(passed ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Application.Quit(2);
            }
        }

        public void TestStarted(ITest test) { }
        public void TestFinished(ITestResult result) { }

        private static bool Requested() => !Application.isEditor && HasArgument("-audioCaptureManualPlayer");

        private static bool HasArgument(string expected)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
                if (string.Equals(argument, expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string ValueAfter(string expected)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
                if (string.Equals(arguments[index], expected, StringComparison.Ordinal)) return arguments[index + 1];
            return null;
        }
    }
}
