using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string BattleCardUpgradeKey = "ShatteredSail.BattleCardUpgrades.v1";
        private bool battleCardUpgradeOpen;
        private int battleCardUpgradePage;
        private int selectedBattleCardUpgrade = -1;

        private void RestoreBattleCardUpgrades()
        {
            Dictionary<string, int> levels = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string token in PlayerPrefs.GetString(BattleCardUpgradeKey, string.Empty).Split(';'))
            {
                string[] pair = token.Split('=');
                if (pair.Length == 2 && int.TryParse(pair[1], out int level))
                    levels[pair[0]] = Mathf.Clamp(level, 0, 3);
            }
            session.ConfigureCardUpgrades(levels);
        }

        private void SaveBattleCardUpgrades()
        {
            if (session == null || !Application.isPlaying || editorPreviewBuilding) return;
            List<string> ids = new List<string>(session.CardUpgradeLevels.Keys);
            ids.Sort(StringComparer.Ordinal);
            List<string> entries = new List<string>();
            foreach (string id in ids)
                if (session.CardUpgradeLevels[id] > 0) entries.Add(id + "=" + session.CardUpgradeLevels[id]);
            PlayerPrefs.SetString(BattleCardUpgradeKey, string.Join(";", entries));
        }

        private void BuildBattleCardUpgradeFacility()
        {
            BuildCardWorkshopPreview();
        }

        private void BuildBattleCardUpgradeDetail(RectTransform panel, int index, Color ink, Color muted)
        {
            CoreBattleCard data = session.GetBattleCard(index);
            CoreBattleCard next = session.GetBattleCardAtLevel(index, Mathf.Min(3, data.UpgradeLevel + 1));
            int cost = session.GetCardUpgradeCost(index);
            Text name = CreateText("Upgrade Selected Name", panel, new Vector2(660f, 54f), new Vector2(0f, 294f), 34, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            name.text = CardFaceLayout.DisplayName(data);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 24; name.resizeTextMaxSize = 34;
            CreateUpgradeComparison(panel, "Current", "强化前  +" + data.UpgradeLevel, data, new Vector2(-176f, -34f), muted);
            CreateUpgradeComparison(panel, "Next", data.UpgradeLevel == 3 ? "已达三阶" : "强化后  +" + next.UpgradeLevel, next, new Vector2(176f, -34f), muted);
            RectTransform arrow = CreateCardOrnament("Upgrade Comparison Arrow", panel, new Vector2(60f, 42f),
                new Vector2(0f, -34f), CardOrnamentKind.UpgradeArrow, data.Type);
            if (data.UpgradeLevel == 3) arrow.GetComponent<CardOrnamentGraphic>().color = new Color(1f, 1f, 1f, .3f);
            string unavailable = session.BattleCardUpgradeUnavailableReason(index);
            CreateText("Upgrade Availability", panel, new Vector2(640f, 36f), new Vector2(0f, -310f),
                22, FontStyle.Normal, TextAnchor.MiddleCenter, muted).text = data.UpgradeLevel < 3 ? unavailable : string.Empty;
            Button confirm = CreateButton("Confirm Battle Card Upgrade", panel, new Vector2(570f, 64f), new Vector2(0f, -365f), Gold, Ink, 30);
            Text confirmLabel = confirm.GetComponentInChildren<Text>();
            confirmLabel.text = !session.HasBattleCardUpgradeBenefit(index) ? "固定功能牌不支持金币强化" :
                data.UpgradeLevel == 3 ? "已达三阶" : "强化 · " + cost + " 金币";
            confirm.interactable = unavailable.Length == 0;
            confirm.onClick.AddListener(() =>
            {
                // Revalidate the current session and persist only a successful upgrade.
                if (session.TryUpgradeBattleCard(index, out string reason)) SaveProgress();
                portNotice = reason;
                Render();
            });
        }

        private string UpgradeCardEffectDescription(CoreBattleCard card)
        {
            return session.BattleCardFaceDescription(card, selectedBattleDeckStage);
        }

        private void CreateUpgradeComparison(RectTransform panel, string suffix, string heading, CoreBattleCard card,
            Vector2 position, Color muted)
        {
            Text title = CreateText("Upgrade Comparison Heading " + suffix, panel, new Vector2(260f, 38f), position + new Vector2(0f, CardSchoolArtCatalog.FrameBounds(card).height * .5f + 28f), 24, FontStyle.Bold, TextAnchor.MiddleCenter, muted);
            title.text = heading;
            RectTransform face = CreateCollectionCardFace(panel, card, "Upgrade Comparison " + suffix,
                "Upgrade Comparison Art " + suffix, "Upgrade Comparison Name " + suffix, 1f,
                UpgradeCardEffectDescription(card), "Upgrade Comparison Body " + suffix);
            face.anchoredPosition = position - CardSchoolArtCatalog.FrameBounds(card).center;
        }
    }
}
