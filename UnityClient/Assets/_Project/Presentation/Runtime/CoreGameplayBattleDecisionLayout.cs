using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Apply after construction and retained refresh, never during an animation.
        // Keep the stable direct-child paths used by card callbacks and defense FX.
        private void ApplyBattleDecisionLayout(RectTransform root, CoreBattleState battle)
        {
            if (root == null || battle == null) return;
            RectTransform resources = root.Find("Player Resource HUD") as RectTransform;
            if (resources != null)
            {
                float height = 132f + 48f * (GameReadability.Scale - 1f);
                resources.sizeDelta = new Vector2(230f, height);
                resources.anchoredPosition = new Vector2(-735f, -200f);
                SetBattleDecisionText(resources, "Player Resource Title", new Vector2(212f, 42f),
                    new Vector2(0f, height * .5f - 28f), 18);
                Text heading = resources.Find("Player Resource Title")?.GetComponent<Text>();
                if (heading != null) heading.text = heading.text.Replace("旗舰资源", "资源");
                Text reserve = FindBattleDecisionText(resources, "Player Ammunition");
                if (reserve != null) ReadableParagraph(reserve, resources, new Vector2(216f, 34f), new Vector2(0f, -height * .5f + 20f), 18, true);
                foreach (string glyph in new[] { "能", "药" })
                {
                    Text value = resources.Find("Resource " + glyph + " Value")?.GetComponent<Text>();
                    if (value != null)
                    {
                        GameReadability.Body(value, 22);
                        value.rectTransform.sizeDelta = new Vector2(glyph == "能" ? 68f : 80f, 36f * GameReadability.Scale);
                        value.rectTransform.anchoredPosition = new Vector2(glyph == "能" ? -42f : 72f, -5f);
                    }
                    RectTransform icon = resources.Find("Resource " + glyph + " Icon") as RectTransform;
                    if (icon != null) { icon.sizeDelta = new Vector2(28f, 28f); icon.anchoredPosition = new Vector2(glyph == "能" ? -94f : 14f, -5f); GameReadability.Body(icon.GetComponentInChildren<Text>(), 18); }
                }
                Text ammunitionGlyph = resources.Find("Resource 药 Icon/Resource 药 Glyph")?.GetComponent<Text>();
                if (ammunitionGlyph != null) ammunitionGlyph.text = battle.Stage == NavalBattleStage.Boarding ? "药" : "火";
            }

            RectTransform buffs = root.Find("Battle Buff Panel") as RectTransform;
            if (buffs != null)
            {
                buffs.sizeDelta = new Vector2(230f, 88f);
                buffs.anchoredPosition = new Vector2(-735f, -331f);
                Text body = FindBattleDecisionText(buffs, "Battle Active Buffs");
                if (body != null) ReadableParagraph(body, buffs, new Vector2(216f, 80f), Vector2.zero, 18, true);
            }
            RectTransform retreat = root.Find("Retreat Battle") as RectTransform ?? root.Find("Emergency Battle Return") as RectTransform;
            if (retreat != null)
            {
                retreat.anchoredPosition = new Vector2(-735f, -417f);
                retreat.sizeDelta = new Vector2(230f, 44f * GameReadability.Scale);
                Text caption = retreat.GetComponentInChildren<Text>(); GameReadability.Body(caption, 22);
                caption.rectTransform.sizeDelta = retreat.sizeDelta - new Vector2(16f, 8f);
                caption.text = session.CanRequestEmergencyTow() ? "应急撤离 · 35%" : session.CanRetreatBattle ? "撤回港口" : session.IsGuidedBossBattle ? "完成战斗教学" : "接舷无法撤退";
            }

            RectTransform enemyResources = root.Find("Enemy Resource HUD") as RectTransform;
            if (enemyResources != null)
            {
                float height = 108f + 36f * (GameReadability.Scale - 1f);
                enemyResources.sizeDelta = new Vector2(230f, height); enemyResources.anchoredPosition = new Vector2(735f, -384f);
                SetBattleDecisionText(enemyResources, "Enemy Resource Title", new Vector2(212f, 36f), new Vector2(0f, height * .5f - 22f), 18);
                SetBattleDecisionText(enemyResources, "Enemy Burning", new Vector2(212f, 36f), new Vector2(0f, -height * .5f + 22f), 18);
                foreach (string glyph in new[] { "回", "盾" })
                {
                    Text value = enemyResources.Find("Resource " + glyph + " Value")?.GetComponent<Text>();
                    if (value != null) { GameReadability.Body(value, 22); value.rectTransform.sizeDelta = new Vector2(68f, 42f); value.rectTransform.anchoredPosition = new Vector2(glyph == "回" ? -42f : 66f, 0f); }
                    RectTransform icon = enemyResources.Find("Resource " + glyph + " Icon") as RectTransform;
                    if (icon != null) { icon.sizeDelta = new Vector2(28f, 28f); icon.anchoredPosition = new Vector2(glyph == "回" ? -94f : 14f, 0f); GameReadability.Body(icon.GetComponentInChildren<Text>(), 18); }
                }
            }
            RectTransform endTurn = root.Find("End Battle Turn") as RectTransform;
            if (endTurn != null)
            {
                endTurn.anchoredPosition = new Vector2(735f, -278f);
                endTurn.sizeDelta = new Vector2(230f, 44f * GameReadability.Scale);
                Text caption = endTurn.GetComponentInChildren<Text>(); GameReadability.Body(caption, 22);
                caption.rectTransform.sizeDelta = endTurn.sizeDelta - new Vector2(16f, 8f);
                caption.text = battle.Stage == NavalBattleStage.Preparation ? "进入炮战" : "结束回合";
            }
            RectTransform curse = root.Find("Battle Curse Panel") as RectTransform;
            if (curse != null) curse.gameObject.SetActive(session.CurseCount > 0);
        }

        private static void SetBattleDecisionText(Transform parent, string name, Vector2 size, Vector2 position, int fontSize)
        {
            Text label = FindBattleDecisionText(parent, name);
            if (label == null) return;
            label.rectTransform.sizeDelta = size;
            label.rectTransform.anchoredPosition = position;
            GameReadability.Body(label, fontSize);
            label.color = Ink;
            label.raycastTarget = false;
        }

        private static Text FindBattleDecisionText(Transform parent, string name)
        {
            foreach (Text label in parent.GetComponentsInChildren<Text>(true)) if (label.name == name) return label;
            return null;
        }
    }
}
