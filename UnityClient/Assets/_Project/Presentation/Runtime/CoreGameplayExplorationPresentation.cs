using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
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
