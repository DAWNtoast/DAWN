from pathlib import Path

root = Path(__file__).parent
source = root.parent / 'PlayerProfileAudit/PlayerProfileCommitProbe.cs'
s = source.read_text(encoding='utf-8-sig')
s = s.replace('PlayerProfileCommitProbe', 'PlayerProfileJournalProbe').replace('ShatteredSail.ProfileAudit', 'ShatteredSail.ProfileJournalAudit')
s = s.replace('profileCommit', 'profileJournal').replace('SHATTEREDSAIL_PROFILE_AUDIT_ARMED', 'SHATTEREDSAIL_PROFILE_JOURNAL_ARMED')
start = s.index('            string stopMethod =')
end = s.index('            report.pid =', start)
s = s[:start] + s[end:]
s = s.replace('-profileJournalCrashAfterSeaChart', '-profileJournalStopPoint')
s = s.replace('public string company, product, persistentDataPath, checkpointDirectory;', 'public string company, product, persistentDataPath, checkpointDirectory, journalDirectory, recoveryNotice;')
s = s.replace('expectedSession, stoppedPlayerPrefsCache, afterComplete;', 'expectedSession, stoppedPlayerPrefsCache, afterComplete, afterRecovery;')
s = s.replace('public string source, phase, gold,', 'public string protectedValues;\n            public string source, phase, gold,')
s = s.replace('            FillDerived(value);\n            return value;\n        }\n        private static int Cargo', '            value.protectedValues = CaptureProtectedValues();\n            FillDerived(value);\n            return value;\n        }\n        private static int Cargo', 1)
start = s.index('            public string scope =')
end = s.index('            public Snapshot ', start)
s = s[:start] + '''            public string scope = "Windows isolated Player process interruption experiment for one SaveProgress journal. " +
                "Seeded profile and explicit reward fixture; not earned progression. Raw preferences may be mixed " +
                "before startup recovery. Recovery uses the production ProfileSaveJournal before any scene Awake.";
            public string stopSite;
''' + s[end:]
s = s.replace('mode != "inspect" && mode != "complete"', 'mode != "inspect" && mode != "complete" && mode != "recover" && mode != "recover_crash"')
s = s.replace('            report.initialDisk = ReadPreferences(', '''            Type journal = JournalType();
            FieldInfo journalDirectory = journal.GetField("directoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
            if (journalDirectory == null) throw new MissingFieldException(journal.FullName, "directoryOverride");
            report.journalDirectory = Path.Combine(root, "profile-journal");
            journalDirectory.SetValue(null, report.journalDirectory);
            report.stopSite = Argument("-profileJournalStopPoint") ?? "none";
            report.initialDisk = ReadPreferences(''', 1)
needle = '            if (mode == "inspect")'
insert = '''            if (mode == "recover" || mode == "recover_crash")
            {
                if (mode == "recover_crash") Environment.SetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED", "1");
                JournalCall("TryRecover");
                Environment.SetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED", null);
                if (mode == "recover_crash") throw new InvalidOperationException("Recovery stop hook was not reached.");
                report.afterRecovery = ReadPreferences("after production startup journal recovery");
                Complete();
                return;
            }
'''
s = s.replace(needle, insert + needle, 1)
s = s.replace('                PlayerPrefs.DeleteAll();', '                JournalCall("TryReset");\n                PlayerPrefs.DeleteAll();', 1)
s = s.replace('public static void RecordStopAndWait()', 'public static void RecordStopAndWait(string point)')
s = s.replace('            PlayerProfileJournalProbe probe = instance;', '''            PlayerProfileJournalProbe probe = instance;
            if (probe == null || Argument("-profileJournalStopPoint") != point ||
                Environment.GetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED") != "1") return;
            // Stop only after replay actually changed a protected value, so
            // the fourth case exercises an interrupted restoration, not an
            // unchanged prefix of the sorted key list.
            if (point == "during_recovery" && CaptureProtectedValues() == probe.report.initialDisk.protectedValues) return;''', 1)
s = s.replace('probe.report.mode != "crash"', '(probe.report.mode != "crash" && probe.report.mode != "recover_crash")')
s = s.replace('            probe.report.stoppedPlayerPrefsCache', '            probe.report.stopSite = point;\n            probe.report.stoppedPlayerPrefsCache', 1)
s = s.replace('Path.Combine(probe.root, "crash-stop.txt")', 'Path.Combine(probe.root, Path.GetFileNameWithoutExtension(probe.reportPath) + ".stop")')
# Keep the process fully alive until the runner terminates its retained handle.
# Both managed and native orderly exit can hang in Unity shutdown on this host.
s = s.replace('            Environment.Exit(exitCode);', '            while (true) System.Threading.Thread.Sleep(1000);')
needle = '        private static string Argument(string key)'
s = s.replace(needle, '''        private static Type JournalType() => typeof(MainMenuController).Assembly.GetType("ShatteredSail.Presentation.ProfileSaveJournal", true);
        private static string CaptureProtectedValues()
        {
            // Reuse the production typed capture descriptors; never guess a
            // string/integer type from registry formatting or native hash names.
            Type type = JournalType();
            Array specs = (Array)type.GetField("Specs", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            MethodInfo capture = type.GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static);
            var entries = new string[specs.Length];
            for (int i = 0; i < specs.Length; i++) entries[i] = JsonUtility.ToJson(capture.Invoke(null, new object[] { specs.GetValue(i) }));
            return "[" + string.Join(",", entries) + "]";
        }
        private void JournalCall(string method)
        {
            object[] parameters = { null };
            MethodInfo operation = JournalType().GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            if (operation == null) throw new MissingMethodException(JournalType().FullName, method);
            bool success = (bool)operation.Invoke(null, parameters);
            report.recoveryNotice = parameters[0] as string;
            if (!success) throw new IOException(method + " failed: " + report.recoveryNotice);
        }
''' + needle, 1)
(root / 'PlayerProfileJournalProbe.cs').write_text(s, encoding='utf-8')
print('Prepared journal probe; compile and runtime verification still required.')
