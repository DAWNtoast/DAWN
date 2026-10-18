using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildNavigationExplorationStatus(RectTransform chart)
        {
            if (tutorialActive) return;
            RectTransform panel = CreateFixedPanel("Voyage Exploration Status", chart,
                new Vector2(810f, 54f), new Vector2(0f, 204f), Html("#E1CCA1"));
            Image backing = panel.GetComponent<Image>();
            backing.sprite = backing.overrideSprite = null;
            backing.color = Html("#E1CCA1");
            backing.raycastTarget = false;
            Text status = NavigationText("Voyage Exploration Status Text", panel,
                new Vector2(782f, 42f), Vector2.zero, 18, Ink);
            string summary = session.ExplorationStatusSummary;
            string expedition = session.TreasureExpeditionStatusSummary;
            status.text = !string.IsNullOrEmpty(expedition)
                ? expedition + (string.IsNullOrEmpty(summary) ? string.Empty : "\n航海增益 · " + summary)
                : string.IsNullOrEmpty(summary)
                    ? "探索收获 · 调查海上事件，收集线索并获得临时航海增益。"
                    : "探索收获 · " + summary;
            status.resizeTextForBestFit = false;
        }

        private void AppendExplorationChoiceDetails(List<string> values, VoyageEffect effect)
        {
            if (effect == null) return;
            string expedition = CoreVoyageSession.TreasureExpeditionEffectSummary(effect);
            if (!string.IsNullOrEmpty(expedition)) values.Add(expedition);
            if (effect.SailingBoostHours > 0)
                values.Add("航速 +25% / " + effect.SailingBoostHours.ToString("0.#") + " 小时");
            if (effect.SurveyBoostHours > 0)
                values.Add("瞭望范围 +35% / " + effect.SurveyBoostHours.ToString("0.#") + " 小时");
            if (!string.IsNullOrEmpty(effect.GrantDiscovery))
                values.Add((session.HasDiscovery(effect.GrantDiscovery) ? "已掌握" : "取得") +
                    CoreVoyageEventCatalog.DiscoveryName(effect.GrantDiscovery) + "（本航次）");
            if (!string.IsNullOrEmpty(effect.RevealEncounterId))
            {
                CoreMapNode target = null;
                float nearest = float.PositiveInfinity;
                if (session.Map != null)
                    foreach (CoreMapNode node in session.Map.Nodes)
                    {
                        if (node.Encounter == null || node.Encounter.Id != effect.RevealEncounterId ||
                            !node.Encounter.HasChartLocation || node.Visited) continue;
                        float dx = node.Position.X - session.Position.X, dy = node.Position.Y - session.Position.Y;
                        float distance = dx * dx + dy * dy;
                        if (distance < nearest || (distance == nearest && (target == null || node.Id < target.Id)))
                        { target = node; nearest = distance; }
                    }
                string prefix = target == null ? "已无未访问的「" : target.IsChartVisible ? "已标出「" : "标出「";
                values.Add(prefix + CoreVoyageEventCatalog.DiscoveryTargetName(effect.RevealEncounterId) + "」");
            }
        }
    }
}
