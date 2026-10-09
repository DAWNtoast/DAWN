using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string SeaChartProgressKey = "ShatteredSail.SeaChart.Progress.v1";
        public const string RecordedSeaChartsKey = "ShatteredSail.SeaChart.Archive.v1";

        [Serializable]
        private sealed class DepartureChartRecord
        {
            public string seed;
            public int routeIndex;
            public string routeName;
            public string departurePort;
            public string destinationPort;
            public bool tutorialWaters;
            public float coverage;
            public bool[] exploredCells;
            public int[] revealedLocationIds;
        }

        [Serializable]
        private sealed class DepartureChartArchive
        {
            public int version = 1;
            public DepartureChartRecord[] charts = Array.Empty<DepartureChartRecord>();
        }

        private readonly List<DepartureChartRecord> recordedDepartureCharts = new List<DepartureChartRecord>();
        private IReadOnlyList<DepartureChartRecord> RecordedDepartureCharts => recordedDepartureCharts;
        private CoreVoyageSession seaChartProgressOwner;
        private int savedSeaChartRevision = -1;
        private bool seaChartProgressReadFailed;
        private bool recordedSeaChartsReadFailed;

        private void RestoreSeaChartProgress()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null) return;
            seaChartProgressReadFailed = false;
            recordedSeaChartsReadFailed = false;
            recordedDepartureCharts.Clear();
            if (PlayerPrefs.HasKey(SeaChartProgressKey))
            {
                try
                {
                    string progressJson = PlayerPrefs.GetString(SeaChartProgressKey, string.Empty);
                    seaChartProgressReadFailed = !session.RestoreSeaChartProgress(JsonUtility.FromJson<CoreSeaChartProgress>(progressJson));
                }
                catch (ArgumentException) { seaChartProgressReadFailed = true; }
            }
            seaChartProgressOwner = session;
            savedSeaChartRevision = session.SeaChartProgressRevision;

            bool archiveExists = PlayerPrefs.HasKey(RecordedSeaChartsKey);
            if (archiveExists)
            {
                try
                {
                    DepartureChartArchive archive = JsonUtility.FromJson<DepartureChartArchive>(PlayerPrefs.GetString(RecordedSeaChartsKey));
                    // Validate the complete archive before exposing any entry. Skipping
                    // an unreadable record would silently delete it on the next save.
                    if (archive != null && archive.version == 1 && archive.charts != null &&
                        archive.charts.All(ValidateDepartureChartRecord))
                        foreach (DepartureChartRecord record in archive.charts.Reverse())
                            MergeDepartureChartRecord(record);
                    else recordedSeaChartsReadFailed = true;
                }
                catch (ArgumentException) { recordedSeaChartsReadFailed = true; }
            }
            else
            {
                // Only migrate the last authored chart once. A raw seed without
                // both ports and its geography hash cannot be safely inferred.
                try
                {
                    string oldJson = PlayerPrefs.GetString(DrawnSeaChartKey, string.Empty);
                    if (!string.IsNullOrEmpty(oldJson))
                    {
                        DrawnSeaChartRecord old = JsonUtility.FromJson<DrawnSeaChartRecord>(oldJson);
                        if (TryConvertDrawnChart(old, out DepartureChartRecord record)) MergeDepartureChartRecord(record);
                    }
                }
                catch (ArgumentException) { }
                SaveRecordedSeaCharts();
                SaveSeaChartProgress();
                PlayerPrefs.Save();
            }
            if (TryGetSeaChartSaveProtectionReason(out string reason)) portNotice = reason;
        }

        private bool TryGetSeaChartSaveProtectionReason(out string reason)
        {
            reason = null;
            if (!seaChartProgressReadFailed && !recordedSeaChartsReadFailed) return false;
            string record = seaChartProgressReadFailed && recordedSeaChartsReadFailed ? "海图进度与已绘图鉴" :
                seaChartProgressReadFailed ? "海图进度" : "已绘图鉴";
            reason = record + "无法读取或版本不兼容，原档案已保留。请修复后重新载入，再出航或保存航程。";
            return true;
        }

        private void SaveSeaChartProgress()
        {
            // Standalone voyage updates still commit depletion and defeated
            // bosses without saving/replaying active-voyage cargo.
            if (QueueSeaChartProgress()) PlayerPrefs.Save();
        }

        private bool QueueSeaChartProgress()
        {
            if (!Application.isPlaying || editorPreviewBuilding || profileSaveBlocked || session == null || seaChartProgressReadFailed) return false;
            if (ReferenceEquals(seaChartProgressOwner, session) && savedSeaChartRevision == session.SeaChartProgressRevision) return false;
            PlayerPrefs.SetString(SeaChartProgressKey, JsonUtility.ToJson(session.CaptureSeaChartProgress()));
            seaChartProgressOwner = session;
            savedSeaChartRevision = session.SeaChartProgressRevision;
            return true;
        }

        private void SaveRecordedSeaCharts()
        {
            if (!Application.isPlaying || editorPreviewBuilding || recordedSeaChartsReadFailed) return;
            PlayerPrefs.SetString(RecordedSeaChartsKey,
                JsonUtility.ToJson(new DepartureChartArchive { charts = recordedDepartureCharts.ToArray() }));
        }

        private bool TryConvertDrawnChart(DrawnSeaChartRecord source, out DepartureChartRecord record)
        {
            record = null;
            if (source == null || source.version != 1 || string.IsNullOrWhiteSpace(source.seed)) return false;
            string seed = source.seed.Trim();
            if (!CoreVoyageSession.TryParseChartSeed(seed, out CoreSeaChartDefinition definition))
            {
                if (!uint.TryParse(source.hash, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash) ||
                    !session.TryImportLegacyChart(seed, source.routeIndex, source.departurePort, source.destinationPort,
                        hash, source.tutorialWaters, out seed) ||
                    !CoreVoyageSession.TryParseChartSeed(seed, out definition)) return false;
            }
            record = new DepartureChartRecord
            {
                seed = definition.Seed,
                routeIndex = definition.RouteIndex,
                routeName = CoreVoyageSession.Routes[definition.RouteIndex].Name,
                departurePort = definition.ForwardDeparturePort,
                destinationPort = definition.ForwardDestinationPort,
                tutorialWaters = definition.TutorialWaters,
                coverage = source.coverage,
                exploredCells = source.exploredCells,
                revealedLocationIds = source.revealedLocationIds,
            };
            return ValidateDepartureChartRecord(record);
        }

        private static bool ValidateDepartureChartRecord(DepartureChartRecord record)
        {
            if (record == null || !CoreVoyageSession.TryParseChartSeed(record.seed, out CoreSeaChartDefinition definition) ||
                record.routeIndex != definition.RouteIndex || record.departurePort != definition.ForwardDeparturePort ||
                record.destinationPort != definition.ForwardDestinationPort || record.tutorialWaters != definition.TutorialWaters)
                return false;
            record.seed = definition.Seed;
            record.routeName = CoreVoyageSession.Routes[definition.RouteIndex].Name;
            if (record.exploredCells == null || record.exploredCells.Length != CoreVoyageMap.GridColumns * CoreVoyageMap.GridRows)
                record.exploredCells = new bool[CoreVoyageMap.GridColumns * CoreVoyageMap.GridRows];
            record.revealedLocationIds = (record.revealedLocationIds ?? Array.Empty<int>()).Where(id => id >= 0).Distinct().ToArray();
            record.coverage = 100f * record.exploredCells.Count(value => value) / record.exploredCells.Length;
            return true;
        }

        private void MergeDepartureChartRecord(DepartureChartRecord record)
        {
            DepartureChartRecord previous = recordedDepartureCharts.Find(value => value.seed == record.seed);
            if (previous != null)
            {
                for (int index = 0; index < record.exploredCells.Length; index++)
                    record.exploredCells[index] |= previous.exploredCells[index];
                record.revealedLocationIds = record.revealedLocationIds.Union(previous.revealedLocationIds).ToArray();
                record.coverage = 100f * record.exploredCells.Count(value => value) / record.exploredCells.Length;
                recordedDepartureCharts.Remove(previous);
            }
            recordedDepartureCharts.Insert(0, record);
        }

        private void RecordConfirmedSeaChart(DrawnSeaChartRecord chart)
        {
            if (!Application.isPlaying || editorPreviewBuilding || !TryConvertDrawnChart(chart, out DepartureChartRecord record)) return;
            MergeDepartureChartRecord(record);
            SaveRecordedSeaCharts();
            SaveSeaChartProgress();
        }

        private void ApplyRecordedSeaChartKnowledge()
        {
            if (session == null || session.Map == null || session.IsTreasureVoyage || tutorialActive) return;
            DepartureChartRecord chart = recordedDepartureCharts.Find(record => record.seed == session.CurrentChartSeed);
            if (chart == null) return;
            if (session.Map.ExploredCells.Length == chart.exploredCells.Length)
                for (int index = 0; index < chart.exploredCells.Length; index++)
                    session.Map.ExploredCells[index] |= chart.exploredCells[index];
            foreach (CoreMapNode node in session.Map.Nodes)
                if (Array.IndexOf(chart.revealedLocationIds, node.Id) >= 0)
                {
                    if (node.Encounter.CanRevealByIntel) node.IntelRevealed = true;
                    else if (node.Encounter.CanDiscoverBySight) node.Discovered = true;
                }
        }
    }
}
