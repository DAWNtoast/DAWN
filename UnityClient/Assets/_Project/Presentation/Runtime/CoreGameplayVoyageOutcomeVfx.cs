using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private sealed class VoyageDiscoverySnapshot
        {
            public CoreVoyageMap Map;
            public bool[] Cells;
            public readonly HashSet<int> VisibleNodes = new HashSet<int>();
            public Vector2 Origin;
        }
        private VoyageDiscoverySnapshot pendingVoyageDiscovery;

        private void CaptureVoyageDiscovery()
        {
            pendingVoyageDiscovery = null;
            if (!Application.isPlaying || editorPreviewBuilding || session?.Map == null) return;
            CoreVoyageMap map = session.Map;
            var snapshot = new VoyageDiscoverySnapshot
            {
                Map = map, Cells = new bool[map.FineColumns * map.FineRows], Origin = MapPosition(session.Position)
            };
            for (int row = 0; row < map.FineRows; row++)
                for (int column = 0; column < map.FineColumns; column++)
                    snapshot.Cells[row * map.FineColumns + column] = map.IsChartCellExplored(column, row);
            foreach (CoreMapNode node in map.Nodes) if (node.IsChartVisible) snapshot.VisibleNodes.Add(node.Id);
            pendingVoyageDiscovery = snapshot;
        }

        private void BuildPendingVoyageDiscovery(RectTransform visibilityLayer)
        {
            VoyageDiscoverySnapshot snapshot = pendingVoyageDiscovery;
            pendingVoyageDiscovery = null;
            if (snapshot == null || !ReferenceEquals(snapshot.Map, session?.Map)) return;
            CoreVoyageMap map = snapshot.Map;
            var cells = new List<Rect>(); var delays = new List<float>(); var locations = new List<Vector2>();
            float width = ChartGridWidth / map.FineColumns, height = ChartGridHeight / map.FineRows;
            float maximumDistance = 1f;
            for (int row = 0; row < map.FineRows; row++)
                for (int column = 0; column < map.FineColumns; column++)
                    if (!snapshot.Cells[row * map.FineColumns + column] && map.IsChartCellExplored(column, row))
                    {
                        var rect = new Rect(-ChartGridWidth * .5f + column * width,
                            -ChartGridHeight * .5f + row * height, width, height);
                        cells.Add(rect);
                        float distance = Vector2.Distance(snapshot.Origin, rect.center);
                        delays.Add(distance); maximumDistance = Mathf.Max(maximumDistance, distance);
                    }
            foreach (CoreMapNode node in map.Nodes)
                if (node.IsChartVisible && !snapshot.VisibleNodes.Contains(node.Id)) locations.Add(MapPosition(node.Position));
            if (cells.Count == 0 && locations.Count == 0) return;
            for (int i = 0; i < delays.Count; i++) delays[i] = .32f * delays[i] / maximumDistance;
            RectTransform root = CreateRect("Voyage Discovery Reveal", visibilityLayer,
                new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            root.gameObject.AddComponent<VoyageDiscoveryMotion>().Configure(cells.ToArray(), delays.ToArray(),
                locations.ToArray(), Fog, this);
        }

        private Action EncounterFeedbackCallback(CoreVoyageSession owner, CoreEncounter encounter, int index) =>
            () => ResolveEncounterWithFeedback(owner, encounter, index);

        private void ResolveEncounterWithFeedback(CoreVoyageSession owner, CoreEncounter encounter, int index)
        {
            // Retired buttons can still have their callbacks invoked in the frame
            // their view is destroyed. Validate the original encounter identity.
            if (!isActiveAndEnabled || !ReferenceEquals(session, owner) || session.Phase != CoreDemoPhase.Encounter ||
                !ReferenceEquals(session.ActiveEncounter, encounter) || !session.IsChoiceAvailable(index)) return;
            VoyageOutcomeSnapshot before = VoyageOutcomeSnapshot.Capture(session);
            CaptureVoyageDiscovery();
            if (!session.ResolveChoice(index)) { pendingVoyageDiscovery = null; return; }
            List<VoyageOutcomeEntry> changes = before.Changes(session);
            PlaySfx("encounter_choice", .5f);
            Render();
            if (session.Phase != CoreDemoPhase.Sailing && session.Phase != CoreDemoPhase.Encounter)
                pendingVoyageDiscovery = null;
            if (!Application.isPlaying || editorPreviewBuilding || contentRoot == null) return;
            if (changes.Count == 0) changes.Add(new VoyageOutcomeEntry("", "航海记录已更新", 0));
            RectTransform root = null;
            try
            {
                RectTransform host = contentRoot;
                Vector2 position = new Vector2(0f, session.Phase == CoreDemoPhase.Battle ? 220f : -225f);
                if (session.Phase == CoreDemoPhase.Sailing || session.Phase == CoreDemoPhase.Encounter)
                {
                    // The old chart remains in the hierarchy until deferred Destroy.
                    RectTransform chart = FindActiveGuidedContent("Navigation Chart") as RectTransform;
                    if (chart != null) { host = chart; position = new Vector2(0f, -chart.rect.height * .25f); }
                }
                root = CreateRect("Encounter Outcome Feedback", host, new Vector2(920f, 230f), position);
                if (host != contentRoot)
                {
                    root.anchorMin = root.anchorMax = new Vector2(.5f, .25f);
                    root.anchoredPosition = Vector2.zero;
                }
                root.gameObject.AddComponent<VoyageOutcomeMotion>().Play(this, interfaceFont, changes);
                BringVoyageHeaderToFront();
                BringGuidedTutorialBannerToFront();
                if (treasureStoryOverlay != null) treasureStoryOverlay.SetAsLastSibling();
            }
            catch (Exception error)
            {
                if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
                Debug.LogWarning("事件已结算；结果动效暂未显示。" + error.Message);
            }
        }
    }

    public readonly struct VoyageOutcomeEntry
    {
        public readonly string Icon, Name;
        public readonly int Amount;
        public VoyageOutcomeEntry(string icon, string name, int amount) { Icon = icon; Name = name; Amount = amount; }
    }

    /// <summary>Actual before/after amounts of a single successful choice, not its advertised reward.</summary>
    public sealed class VoyageOutcomeSnapshot
    {
        private static readonly string[] Keys = { "water", "food", "beer", "medicine", "wood", "iron", "cloth", "powder",
            "cannonballs", "gold", "fame", "gem", "ancientmaterial", "relic", "blackpearl", "ancientcompass", "lantern", "curse", "hull" };
        private static readonly string[] Names = { "淡水", "食物", "啤酒", "药品", "木板", "铁料", "布料", "火药",
            "炮弹", "金币", "声望", "宝石", "古代材料", "遗物", "黑珍珠", "古代罗盘", "提灯", "诅咒", "船体" };
        private int[] amounts;
        private readonly HashSet<int> visible = new HashSet<int>();
        private CoreVoyageMap map;
        public static VoyageOutcomeSnapshot Capture(CoreVoyageSession session)
        {
            var result = new VoyageOutcomeSnapshot { amounts = Values(session), map = session.Map };
            if (session.Map != null) foreach (CoreMapNode node in session.Map.Nodes) if (node.IsChartVisible) result.visible.Add(node.Id);
            return result;
        }
        public List<VoyageOutcomeEntry> Changes(CoreVoyageSession session)
        {
            var result = new List<VoyageOutcomeEntry>();
            int[] current = Values(session);
            for (int i = 0; i < current.Length; i++)
            {
                int delta = current[i] - amounts[i];
                if (delta != 0) result.Add(new VoyageOutcomeEntry(Keys[i], Names[i], delta));
            }
            int discoveries = 0;
            if (map != null && ReferenceEquals(map, session.Map))
                foreach (CoreMapNode node in map.Nodes) if (node.IsChartVisible && !visible.Contains(node.Id)) discoveries++;
            if (discoveries > 0) result.Add(new VoyageOutcomeEntry("", "新地点", discoveries));
            if (session.Phase == CoreDemoPhase.Battle) result.Add(new VoyageOutcomeEntry("", "进入海战", 0));
            return result;
        }
        private static int[] Values(CoreVoyageSession session)
        {
            VoyageCargo c = session.Cargo;
            return new[] { c.Water, c.Food, c.Beer, c.Medicine, c.Wood, c.Iron, c.Cloth, c.Powder, c.Cannonballs,
                c.Gold, c.Fame, c.Gem, c.AncientMaterial, c.Relic, c.BlackPearl, c.AncientCompass, c.Lantern, c.Curse, session.Hull };
        }
    }
}
