using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    /// <summary>Import mastered files without mono conversion or automatic gain changes.</summary>
    public static class AudioQualityImportSync
    {
        [Serializable] private sealed class Entry
        {
            public string path, guid, loadType, compression;
            public int channels, frequency, samples;
            public float seconds, quality;
            public bool forceToMono, normalizationPropertyFound, normalize;
        }
        [Serializable] private sealed class Report
        {
            public string unityVersion = Application.unityVersion;
            public List<Entry> clips = new List<Entry>();
        }

        public static void ConfigureFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped Editor required.");
            string output = Argument("-audioAuditPath");
            if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output)) throw new ArgumentException("Supply absolute -audioAuditPath.");
            Directory.CreateDirectory(output);
            var report = new Report();
            foreach (string group in new[] { "Music", "BattleSfx", "WorldSfx", "InterfaceSfx", "Ambience" })
                foreach (string file in Directory.GetFiles("Assets/_Project/Resources/Audio/" + group,"*.wav"))
                {
                    string path = file.Replace('\\','/');
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null) throw new InvalidOperationException("Missing audio importer: " + path);
                    bool music = group == "Music", ambience = group == "Ambience";
                    importer.forceToMono = false;
                    importer.ambisonic = false;
                    importer.loadInBackground = music || ambience;
                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = music || ambience ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                    settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    settings.quality = .95f;
                    settings.preloadAudioData = !music;
                    importer.defaultSampleSettings = settings;
                    // Use Unity's actual serialized property if supported by this Editor.
                    var serialized = new SerializedObject(importer);
                    SerializedProperty normalization = serialized.FindProperty("m_Normalize") ?? serialized.FindProperty("normalize");
                    bool found = normalization != null;
                    if (found) { normalization.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                    importer.SaveAndReimport();
                    importer = (AudioImporter)AssetImporter.GetAtPath(path);
                    settings = importer.defaultSampleSettings;
                    serialized = new SerializedObject(importer);
                    normalization = serialized.FindProperty("m_Normalize") ?? serialized.FindProperty("normalize");
                    found = normalization != null;
                    bool normalized = found && normalization.boolValue;
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null || clip.frequency != 48000 || clip.samples == 0 ||
                        importer.forceToMono || normalized || AssetDatabase.AssetPathToGUID(path) != guid)
                        throw new InvalidOperationException("Audio import lost samples, frequency, stereo or identity: " + path);
                    if ((music || ambience) && clip.channels != 2) throw new InvalidOperationException("Stereo master was collapsed: " + path);
                    report.clips.Add(new Entry { path = path, guid = guid, loadType = settings.loadType.ToString(),
                        compression = settings.compressionFormat.ToString(), channels = clip.channels,
                        frequency = clip.frequency, samples = clip.samples, seconds = clip.length,
                        quality = settings.quality, forceToMono = importer.forceToMono,
                        normalizationPropertyFound = found, normalize = normalized });
                }
            if (report.clips.Count != 63) throw new InvalidOperationException("Expected all 63 audio assets; got " + report.clips.Count);
            File.WriteAllText(Path.Combine(output,"unity-import-report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("AUDIO_QUALITY_IMPORT_OK: 63 assets, 48kHz, preserved GUIDs, stereo music/ambience and PCM one-shots.");
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < args.Length; index++) if (args[index] == name) return args[index+1];
            return null;
        }
    }
}
