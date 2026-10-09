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
            Color ink = Html("#302116");
            Color muted = Html("#725235");
            RectTransform panel = CreateFixedPanel("Battle Card Upgrade Panel", contentRoot,
                new Vector2(1770f, 674f), new Vector2(0f, -46f), Color.clear);
            Image paper = CreateImage("Battle Upgrade Parchment", panel, new Vector2(1730f, 656f), Vector2.zero,
                new Color(0.97f, 0.9f, 0.74f), NauticalSurfaceSkin.Paper);
            paper.type = Image.Type.Tiled;
            paper.pixelsPerUnitMultiplier = NauticalSurfaceSkin.Paper == null ? 1f : NauticalSurfaceSkin.Paper.rect.width / 400f;
            paper.raycastTarget = false;
            Outline outline = paper.gameObject.AddComponent<Outline>();
            outline.effectColor = Html("#785027");
            outline.effectDistance = new Vector2(5f, -5f);
            Text title = CreateText("Card Upgrade Heading", panel, new Vector2(1560f, 46f), new Vector2(0f, 288f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            title.text = WorkshopStageName(selectedBattleDeckStage) + " · 基础卡牌强化";
            Text rule = CreateText("Card Upgrade Rules", panel, new Vector2(1550f, 36f), new Vector2(0f, 245f), 17, FontStyle.Bold, TextAnchor.MiddleCenter, muted);
            rule.text = "Ⅰ 主效果大幅提升　→　Ⅱ 能量消耗 −1（最低 0）　→　Ⅲ 额外抽 1 张　｜　效果累计，同名卡共享";

            for (int i = 0; i < WorkshopBattleStages.Length; i++)
            {
                NavalBattleStage stage = WorkshopBattleStages[i];
                Button tab = UiKit.CreateTab("Upgrade Stage " + stage, panel, new Vector2(228f, 42f),
                    new Vector2(-654f + i * 248f, 195f), WorkshopStageName(stage), Gold, Ink, 18,
                    stage == selectedBattleDeckStage);
                tab.onClick.AddListener(() => { selectedBattleDeckStage = stage; battleCardUpgradePage = 0; selectedBattleCardUpgrade = -1; Render(); });
            }

            List<int> cards = new List<int>(session.GetAvailableBattleCards(selectedBattleDeckStage));
            if (!cards.Contains(selectedBattleCardUpgrade)) selectedBattleCardUpgrade = cards.Count > 0 ? cards[0] : -1;
            int pageSize = ReadableInspectionPageSize;
            int pages = Mathf.Max(1, (cards.Count + pageSize - 1) / pageSize);
            battleCardUpgradePage = Mathf.Clamp(battleCardUpgradePage, 0, pages - 1);
            int first = battleCardUpgradePage * pageSize;
            for (int slot = 0; slot < pageSize && first + slot < cards.Count; slot++)
            {
                int index = cards[first + slot];
                CoreBattleCard data = session.GetBattleCard(index);
                Vector2 position = new Vector2(-689f + slot % 4 * 186f, 62f - slot / 4 * 180f);
                Button select = CreateButton("Select Upgrade Card " + index, panel, new Vector2(176f, 166f), position, PanelRaised, Ink, 15);
                select.GetComponentInChildren<Text>().text = string.Empty;
                WoodButtonSkin.SetSelected(select, index == selectedBattleCardUpgrade);
                RawImage artView = CreateRawImage("Upgrade Collection Art " + index, select.transform,
                    new Vector2(84f, 84f), new Vector2(0f, 29f), art.CardSheet, art.CardUv(data.ArtId));
                artView.raycastTarget = false;
                Text name = CreateText("Upgrade Collection Name " + index, select.transform,
                    new Vector2(166f, 40f), new Vector2(0f, -38f), 15, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
                name.text = data.Name;
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 12; name.resizeTextMaxSize = 15;
                Text rank = CreateText("Upgrade Collection Rank " + index, select.transform,
                    new Vector2(166f, 26f), new Vector2(0f, -68f), 14, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
                rank.text = "强化 " + data.UpgradeLevel + "/3 · " + data.Energy + " 能量";
                rank.horizontalOverflow = HorizontalWrapMode.Wrap;
                rank.verticalOverflow = VerticalWrapMode.Truncate;
                LayoutReadableInspectionCard(select, slot);
                select.onClick.AddListener(() => { selectedBattleCardUpgrade = index; Render(); });
            }
            Button prev = CreateButton("Previous Upgrade Cards", panel, new Vector2(126f, 34f), new Vector2(-700f, -242f), PanelRaised, Ink, 16);
            prev.GetComponentInChildren<Text>().text = "← 上一页";
            prev.interactable = battleCardUpgradePage > 0;
            prev.onClick.AddListener(() => { battleCardUpgradePage--; Render(); });
            Text page = CreateText("Upgrade Collection Page", panel, new Vector2(390f, 30f), new Vector2(-412f, -242f), 15, FontStyle.Bold, TextAnchor.MiddleCenter, muted);
            page.text = "基础战术　" + (battleCardUpgradePage + 1) + "/" + pages + " 页";
            Button next = CreateButton("Next Upgrade Cards", panel, new Vector2(126f, 34f), new Vector2(-126f, -242f), PanelRaised, Ink, 16);
            next.GetComponentInChildren<Text>().text = "下一页 →";
            next.interactable = battleCardUpgradePage < pages - 1;
            next.onClick.AddListener(() => { battleCardUpgradePage++; Render(); });

            if (selectedBattleCardUpgrade >= 0) BuildBattleCardUpgradeDetail(panel, selectedBattleCardUpgrade, ink, muted);
            Button back = CreateButton("Back From Card Upgrades", panel, new Vector2(258f, 42f), new Vector2(-638f, -297f), PanelRaised, Ink, 18);
            back.GetComponentInChildren<Text>().text = "返回四座卡箱";
            back.onClick.AddListener(() => { ResetCardWorkshopView(); Render(); });
            Button editDeck = CreateButton("Edit Deck From Card Upgrades", panel, new Vector2(200f, 42f), new Vector2(-378f, -297f), PanelRaised, Ink, 18);
            editDeck.GetComponentInChildren<Text>().text = "编组本阶段牌组";
            editDeck.onClick.AddListener(() => { battleCardUpgradeOpen = false; battleDeckOverviewOpen = false; Render(); });
            Text note = CreateText("Card Upgrade Notice", panel, new Vector2(1050f, 38f), new Vector2(285f, -296f), 15, FontStyle.Bold, TextAnchor.MiddleCenter, muted);
            note.text = string.IsNullOrEmpty(portNotice) ? "特殊牌在第四箱；仅来源随船航行时获得经验。" : portNotice;
            note.resizeTextForBestFit = true; note.resizeTextMinSize = 12; note.resizeTextMaxSize = 15;
            StyleReadableInspectionControls(panel);
            ReadableParagraph(note, panel, new Vector2(970f, 58f), new Vector2(345f, -296f), 18, true);
            editDeck.GetComponent<RectTransform>().sizeDelta = new Vector2(270f, 44f * GameReadability.Scale);
            editDeck.GetComponent<RectTransform>().anchoredPosition = new Vector2(-340f, -297f);
            editDeck.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(254f, 44f * GameReadability.Scale - 8f);
            editDeck.GetComponentInChildren<Text>().text = "编组本阶段";
        }

        private void BuildBattleCardUpgradeDetail(RectTransform panel, int index, Color ink, Color muted)
        {
            CoreBattleCard data = session.GetBattleCard(index);
            CoreBattleCard next = session.GetBattleCardAtLevel(index, Mathf.Min(3, data.UpgradeLevel + 1));
            int cost = session.GetCardUpgradeCost(index);
            Text name = CreateText("Upgrade Selected Name", panel, new Vector2(720f, 44f), new Vector2(407f, 189f), 25, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            name.text = data.Name + "　+" + data.UpgradeLevel;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 20; name.resizeTextMaxSize = 25;
            Text source = CreateText("Upgrade Selected Source", panel, new Vector2(710f, 28f), new Vector2(407f, 151f), 16, FontStyle.Bold, TextAnchor.MiddleCenter, muted);
            source.text = !string.IsNullOrEmpty(data.RequiredCrew) ? "特殊船员专属 · 同职业卡牌强化保留" :
                !string.IsNullOrEmpty(data.RequiredComponent) ? "船只部件专属 · 同类型卡牌强化保留" : "基础战术 · 所有同名副本同步强化";
            source.horizontalOverflow = HorizontalWrapMode.Wrap;
            source.verticalOverflow = VerticalWrapMode.Truncate;
            CreateUpgradeComparison(panel, "Current", "当前 · " + data.Energy + " 能量", session.BattleCardPortDescription(data), new Vector2(215f, 22f), ink, muted);
            CreateUpgradeComparison(panel, "Next", data.UpgradeLevel == 3 ? "三阶已满" : "下一阶 · " + next.Energy + " 能量", session.BattleCardPortDescription(next), new Vector2(600f, 22f), ink, muted);
            Text effect = CreateText("Upgrade Next Effect", panel, new Vector2(704f, 74f), new Vector2(407f, -134f), 20, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            effect.text = data.UpgradeLevel == 0 ? "一阶 · 主效果翻倍\n伤害、防御、治疗及策略效果同步强化" : data.UpgradeLevel == 1 ?
                "二阶 · 能量消耗 −1\n最低为 0，保留一阶强化" : data.UpgradeLevel == 2 ?
                "三阶 · 每次使用额外抽 1 张\n弃牌可重新洗入，构筑低费连击" : "全部强化已完成\n主效果强化 + 减费 + 额外抽牌";
            effect.horizontalOverflow = HorizontalWrapMode.Wrap;
            effect.verticalOverflow = VerticalWrapMode.Truncate;
            effect.resizeTextForBestFit = true; effect.resizeTextMinSize = 16; effect.resizeTextMaxSize = 20;
            Button confirm = CreateButton("Confirm Battle Card Upgrade", panel, new Vector2(556f, 52f), new Vector2(407f, -227f), Gold, Ink, 21);
            Text confirmLabel = confirm.GetComponentInChildren<Text>();
            confirmLabel.text = data.UpgradeLevel == 3 ? "已达三阶" : "确认强化 · " + cost + " 金币";
            confirmLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            confirmLabel.verticalOverflow = VerticalWrapMode.Truncate;
            confirmLabel.resizeTextForBestFit = true; confirmLabel.resizeTextMinSize = 16; confirmLabel.resizeTextMaxSize = 21;
            confirm.interactable = session.Phase == CoreDemoPhase.Port && data.UpgradeLevel < 3 && session.ProfileGold >= cost;
            confirm.onClick.AddListener(() =>
            {
                // The core performs the atomic affordability and rank checks again.
                if (session.TryUpgradeBattleCard(index, out string reason)) SaveProgress();
                portNotice = reason;
                Render();
            });
            StackReadableInspection(panel, "Card Upgrade Reading Viewport", new Vector2(720f, 370f), new Vector2(407f, -8f),
                new[] { "Upgrade Selected Name", "Upgrade Selected Source", "Upgrade Comparison Heading Current", "Upgrade Comparison Body Current", "Upgrade Comparison Heading Next", "Upgrade Comparison Body Next", "Upgrade Next Effect" });
        }

        private void CreateUpgradeComparison(RectTransform panel, string suffix, string heading, string body,
            Vector2 position, Color ink, Color muted)
        {
            Text title = CreateText("Upgrade Comparison Heading " + suffix, panel, new Vector2(340f, 34f), position + new Vector2(0f, 92f), 19, FontStyle.Bold, TextAnchor.MiddleCenter, muted);
            title.text = heading;
            CreateCardEffectArea("Upgrade Comparison Body " + suffix, panel, new Vector2(340f, 182f),
                position + new Vector2(0f, -21f), 20, ink, body);
        }
    }
}
