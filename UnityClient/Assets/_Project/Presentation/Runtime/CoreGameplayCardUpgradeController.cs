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
            CoreBattleCard data = session.GetBattleCard(index, selectedBattleDeckStage);
            int maximumLevel = session.GetCardMaximumUpgradeLevel(index);
            int currentLevel = session.GetCardUpgradeLevel(data.Id);
            bool atMaximum = currentLevel >= maximumLevel;
            CoreBattleCard next = session.GetBattleCardAtLevel(index, Mathf.Min(maximumLevel, currentLevel + 1));
            if (selectedBattleDeckStage == NavalBattleStage.Boarding) next = CoreBoardingCardCatalog.ForBoarding(next);
            int cost = session.GetCardUpgradeCost(index);
            Text name = CreateText("Upgrade Selected Name", panel, new Vector2(660f, 54f), new Vector2(0f, 294f), 34, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            name.text = CardFaceLayout.DisplayName(data);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 24; name.resizeTextMaxSize = 34;
            CreateUpgradeComparison(panel, "Current", "强化前  +" + data.UpgradeLevel, data, new Vector2(-176f, -34f), muted);
            CreateUpgradeComparison(panel, "Next", atMaximum ? CardMaximumLevelLabel(maximumLevel) : "强化后  +" + next.UpgradeLevel, next, new Vector2(176f, -34f), muted);
            RectTransform arrow = CreateCardOrnament("Upgrade Comparison Arrow", panel, new Vector2(60f, 42f),
                new Vector2(0f, -34f), CardOrnamentKind.UpgradeArrow, data.Type);
            if (atMaximum) arrow.GetComponent<CardOrnamentGraphic>().color = new Color(1f, 1f, 1f, .3f);
            string unavailable = session.BattleCardUpgradeUnavailableReason(index);
            CreateText("Upgrade Availability", panel, new Vector2(640f, 26f), new Vector2(0f, -315f),
                21, FontStyle.Normal, TextAnchor.MiddleCenter, muted).text = atMaximum ? string.Empty : unavailable;
            Button confirm = CreateButton("Confirm Battle Card Upgrade", panel, new Vector2(570f, 64f), new Vector2(0f, -365f), Gold, Ink, 30);
            Text confirmLabel = confirm.GetComponentInChildren<Text>();
            confirmLabel.text = !session.HasBattleCardUpgradeBenefit(index) ? "固定功能牌不支持金币强化" :
                atMaximum ? CardMaximumLevelLabel(maximumLevel) : "强化 · " + cost + " 金币";
            confirm.interactable = !atMaximum && unavailable.Length == 0;
            confirm.onClick.AddListener(() =>
            {
                // Revalidate the current session and persist only a successful upgrade.
                if (session.TryUpgradeBattleCard(index, out string reason)) SaveProgress();
                portNotice = reason;
                Render();
            });
        }

        private static string CardMaximumLevelLabel(int maximumLevel) => maximumLevel <= 0 ? "固定效果" :
            maximumLevel == 1 ? "已达一阶" : maximumLevel == 2 ? "已达二阶" : "已达三阶";

        private string UpgradeCardEffectDescription(CoreBattleCard card)
        {
            return session.BattleCardFaceDescription(card, selectedBattleDeckStage);
        }

        private void CreateUpgradeComparison(RectTransform panel, string suffix, string heading, CoreBattleCard card,
            Vector2 position, Color muted)
        {
            Text title = CreateText("Upgrade Comparison Heading " + suffix, panel, new Vector2(260f, 38f), position + new Vector2(0f, CardSchoolArtCatalog.FrameBounds(card).height * .5f + 28f), 24, FontStyle.Bold, TextAnchor.MiddleCenter, muted);
            title.text = heading;
            string description = UpgradeCardEffectDescription(card);
            RectTransform face = CreateCollectionCardFace(panel, card, "Upgrade Comparison " + suffix,
                "Upgrade Comparison Art " + suffix, "Upgrade Comparison Name " + suffix, 1f,
                description, "Upgrade Comparison Body " + suffix);
            RefreshCardSupplyCost(face, card, previewStage: selectedBattleDeckStage);
            Rect bounds = CardSchoolArtCatalog.FrameBounds(card);
            face.anchoredPosition = position - bounds.center;
            Image hit = face.GetComponent<Image>();
            hit.raycastTarget = true;
            hit.raycastPadding = new Vector4(bounds.xMin - face.rect.xMin, bounds.yMin - face.rect.yMin,
                face.rect.xMax - bounds.xMax, face.rect.yMax - bounds.yMax);
            CollectionCardKeywordHover.Bind(face.gameObject, face, card, FormatBattleCardDescription(description));
        }
    }
}
