using System;
using System.Collections.Generic;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string SpecialCardExperienceKey = "ShatteredSail.SpecialCards.Experience.v1";
        public const string AcquiredSpecialCardsKey = "ShatteredSail.SpecialCards.Acquired.v1";
        private bool specialCardCollectionOpen;
        private int specialCardCollectionPage;
        private int specialCardSourceFilter;
        private int selectedSpecialCardIndex = -1;

        private void RefreshSpecialCardSources()
        {
            if (editorPreviewBuilding || session == null || session.Phase != CoreDemoPhase.Port || componentArt == null) return;
            List<string> crewIds = new List<string>();
            List<string> componentIds = new List<string>();
            List<string> acquired = new List<string>();
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
            {
                CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(crew.Id);
                if (definition != null && IsCrewRecruited(crew)) acquired.Add(definition.Card.Id);
            }
            // Workshop/dossier previews and the departing deck share one roster.
            // A locally available identity may still be deliberately on standby.
            EnsureDepartureCrewSelection();
            crewIds.AddRange(selectedDepartureSpecialCrew);
            for (int slot = 0; slot < ComponentArtCatalog.SlotCount; slot++)
            {
                ComponentArtEntry item = EquippedComponent((ComponentSlot)slot);
                if (item != null && item.Rarity >= ComponentRarity.Rare) componentIds.Add(item.Id);
            }
            session.AcquireSpecialCards(acquired);
            session.ConfigureSpecialCardSources(crewIds, componentIds);
        }

        private void RestoreSpecialCardProgress()
        {
            if (editorPreviewBuilding) return;
            Dictionary<string, int> xp = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string token in PlayerPrefs.GetString(SpecialCardExperienceKey, string.Empty).Split(';'))
            {
                string[] pair = token.Split('=');
                if (pair.Length == 2 && int.TryParse(pair[1], out int value) && value >= 0) xp[pair[0]] = value;
            }
            // Core merges migrated paid-special-card ranks before this XP restore.
            session.RestoreSpecialCardExperience(xp);
            session.RestoreAcquiredSpecialCards(PlayerPrefs.GetString(AcquiredSpecialCardsKey, string.Empty).Split(';'));
            RefreshSpecialCardSources();
            SaveSpecialCardProgress();
        }

        private void SaveSpecialCardProgress(bool flush = true)
        {
            // Voyage progress affects this run immediately, but is committed with the port's
            // resource settlement. Saving XP alone would let a reload restore spent supplies.
            if (editorPreviewBuilding || session == null || session.Phase != CoreDemoPhase.Port) return;
            List<string> ids = new List<string>(session.SpecialCardExperience.Keys);
            ids.Sort(StringComparer.Ordinal);
            List<string> entries = new List<string>();
            foreach (string id in ids) entries.Add(id + "=" + session.GetSpecialCardExperience(id));
            List<string> acquired = new List<string>(session.AcquiredSpecialCardIds);
            acquired.Sort(StringComparer.Ordinal);
            PlayerPrefs.SetString(SpecialCardExperienceKey, string.Join(";", entries));
            PlayerPrefs.SetString(AcquiredSpecialCardsKey, string.Join(";", acquired));
            if (flush) PlayerPrefs.Save();
        }

        private static int SpecialCardIndex(string id)
        {
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                if (CoreVoyageSession.CardLibrary[index].Id == id) return index;
            return -1;
        }

        private bool IsSpecialCardSailing(int index)
        {
            foreach (NavalBattleStage stage in WorkshopBattleStages)
                foreach (int active in session.GetBonusBattleCards(stage))
                    if (active == index) return true;
            return false;
        }

        private static string SpecialCardStages(CoreBattleCard card)
        {
            List<string> stages = new List<string>();
            foreach (NavalBattleStage stage in WorkshopBattleStages)
                if (card.IsAllowedInStage(stage)) stages.Add(WorkshopStageName(stage));
            return string.Join(" / ", stages);
        }

        private void BuildSpecialCardCollection()
        {
            Color ink = Html("#302116");
            Color muted = Html("#725235");
            RectTransform panel = CreateFixedPanel("Special Card Collection Panel", contentRoot,
                new Vector2(1770f, 674f), new Vector2(0f, -46f), Color.clear);
            Image paper = CreateImage("Special Card Collection Parchment", panel, new Vector2(1730f, 656f), Vector2.zero,
                new Color(0.97f, 0.9f, 0.74f), NauticalSurfaceSkin.Paper);
            paper.type = Image.Type.Tiled;
            paper.pixelsPerUnitMultiplier = NauticalSurfaceSkin.Paper == null ? 1f : NauticalSurfaceSkin.Paper.rect.width / 400f;
            paper.raycastTarget = false;
            Text heading = CreateText("Special Card Collection Heading", panel, new Vector2(1550f, 46f), new Vector2(0f, 288f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            heading.text = "船长珍藏 · 已获得的特殊卡牌";
            Text rule = CreateText("Special Card Collection Rule", panel, new Vector2(1550f, 40f), new Vector2(0f, 243f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
            rule.text = "每实际航行约 4 海里积累 1 经验 · 累计 120 / 360 / 840 经验达到三阶 · 本次航行实时成长，返航结算后保存";
            string[] filters = { "全部收藏", "船员战术", "部件战术" };
            for (int i = 0; i < filters.Length; i++)
            {
                int filter = i;
                Button tab = CreateButton("Special Card Filter " + i, panel, new Vector2(228f, 42f), new Vector2(-654f + i * 248f, 195f), PanelRaised, Ink, 18);
                tab.GetComponentInChildren<Text>().text = filters[i];
                WoodButtonSkin.SetSelected(tab, i == specialCardSourceFilter);
                tab.onClick.AddListener(() => { specialCardSourceFilter = filter; specialCardCollectionPage = 0; selectedSpecialCardIndex = -1; Render(); });
            }
            List<int> cards = new List<int>();
            foreach (string id in session.AcquiredSpecialCardIds)
            {
                int index = SpecialCardIndex(id);
                if (index < 0) continue;
                CoreBattleCard card = session.GetBattleCard(index);
                bool crew = card.RequiredCrew.Length > 0;
                if (specialCardSourceFilter == 1 && !crew || specialCardSourceFilter == 2 && crew) continue;
                if (!cards.Contains(index)) cards.Add(index);
            }
            cards.Sort();
            if (!cards.Contains(selectedSpecialCardIndex)) selectedSpecialCardIndex = cards.Count == 0 ? -1 : cards[0];
            int pageSize = ReadableInspectionPageSize;
            int pageCount = Mathf.Max(1, (cards.Count + pageSize - 1) / pageSize);
            specialCardCollectionPage = Mathf.Clamp(specialCardCollectionPage, 0, pageCount - 1);
            int first = specialCardCollectionPage * pageSize;
            for (int slot = 0; slot < pageSize && first + slot < cards.Count; slot++)
            {
                int index = cards[first + slot];
                CoreBattleCard card = session.GetBattleCard(index);
                Button select = CreateButton("Select Special Card " + index, panel, new Vector2(176f, 166f), new Vector2(-689f + slot % 4 * 186f, 62f - slot / 4 * 180f), PanelRaised, Ink, 15);
                select.GetComponentInChildren<Text>().text = string.Empty;
                WoodButtonSkin.SetSelected(select, index == selectedSpecialCardIndex);
                RawImage icon = CreateRawImage("Special Card Icon " + index, select.transform, new Vector2(84f, 84f), new Vector2(0f, 29f), art.CardSheet, art.CardUv(card.ArtId));
                icon.raycastTarget = false;
                Text name = CreateText("Special Card Name " + index, select.transform, new Vector2(166f, 40f), new Vector2(0f, -38f), 15, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
                name.text = card.Name;
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 12; name.resizeTextMaxSize = 15;
                Text rank = CreateText("Special Card Rank " + index, select.transform, new Vector2(166f, 26f), new Vector2(0f, -68f), 14, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
                rank.text = "熟练 " + card.UpgradeLevel + "/3 · " + (IsSpecialCardSailing(index) ? "随船启用" : "已收藏");
                rank.horizontalOverflow = HorizontalWrapMode.Wrap;
                rank.verticalOverflow = VerticalWrapMode.Truncate;
                LayoutReadableInspectionCard(select, slot);
                select.onClick.AddListener(() => { selectedSpecialCardIndex = index; Render(); });
            }
            if (cards.Count == 0)
            {
                Text empty = CreateText("Special Card Collection Empty", panel, new Vector2(730f, 210f), new Vector2(-400f, -40f), 24, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
                empty.text = "暂无此类特殊牌\n\n招募专属战术船员，\n或装备蓝色及以上部件后收录。";
            }
            else BuildSpecialCardCollectionDetail(panel, selectedSpecialCardIndex, ink, muted);
            Button previous = CreateButton("Previous Special Cards", panel, new Vector2(126f, 34f), new Vector2(-700f, -242f), PanelRaised, Ink, 16);
            previous.GetComponentInChildren<Text>().text = "← 上一页";
            previous.interactable = specialCardCollectionPage > 0;
            previous.onClick.AddListener(() => { specialCardCollectionPage--; Render(); });
            Text count = CreateText("Special Card Collection Count", panel, new Vector2(390f, 30f), new Vector2(-412f, -242f), 16, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
            count.text = cards.Count + " 张已获得　" + (specialCardCollectionPage + 1) + "/" + pageCount + " 页";
            Button next = CreateButton("Next Special Cards", panel, new Vector2(126f, 34f), new Vector2(-126f, -242f), PanelRaised, Ink, 16);
            next.GetComponentInChildren<Text>().text = "下一页 →";
            next.interactable = specialCardCollectionPage < pageCount - 1;
            next.onClick.AddListener(() => { specialCardCollectionPage++; Render(); });
            Button back = CreateButton("Back From Special Cards", panel, new Vector2(258f, 42f), new Vector2(-638f, -297f), PanelRaised, Ink, 18);
            back.GetComponentInChildren<Text>().text = "返回四座卡箱";
            back.onClick.AddListener(() => { ResetCardWorkshopView(); Render(); });
            Text note = CreateText("Special Card Read Only Notice", panel, new Vector2(1110f, 38f), new Vector2(214f, -296f), 17, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
            note.text = "特殊牌不接受金币强化；来源离队或卸下后保留经验，但不参战、不再获航行经验。";
            StyleReadableInspectionControls(panel);
            ReadableParagraph(note, panel, new Vector2(1110f, 58f), new Vector2(214f, -296f), 18, true);
        }

        private void BuildSpecialCardCollectionDetail(Transform panel, int index, Color ink, Color muted)
        {
            CoreBattleCard card = session.GetBattleCard(index);
            CoreSpecialCardDefinition source = CoreSpecialCardCatalog.FindByCardId(card.Id);
            Text title = CreateText("Special Card Detail Name", panel, new Vector2(720f, 44f), new Vector2(407f, 194f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            title.text = card.Name + "　+" + card.UpgradeLevel;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 22; title.resizeTextMaxSize = 28;
            Text owner = CreateText("Special Card Detail Source", panel, new Vector2(720f, 36f), new Vector2(407f, 149f), 20, FontStyle.Bold, TextAnchor.MiddleCenter, muted);
            owner.text = source == null ? "旧版专属战术 · 已保留成长进度" : "来源：" + source.SourceName;
            owner.horizontalOverflow = HorizontalWrapMode.Wrap;
            owner.verticalOverflow = VerticalWrapMode.Truncate;
            Text stage = CreateText("Special Card Detail Stages", panel, new Vector2(720f, 32f), new Vector2(407f, 111f), 19, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
            stage.text = SpecialCardStages(card) + "　·　" + card.Energy + " 能量";
            stage.horizontalOverflow = HorizontalWrapMode.Wrap;
            stage.verticalOverflow = VerticalWrapMode.Truncate;
            CreateCardEffectArea("Special Card Detail Effect", panel, new Vector2(710f, 244f),
                new Vector2(407f, -35f), 23, ink, session.BattleCardPortDescription(card),
                loreName: "Special Card Detail Lore",
                lore: source == null ? "这张战术保留了旧航海档案中的强化。新的身份专属卡请查看对应船员或部件的特殊卡牌标签。" : source.Lore,
                loreSize: 21, loreColor: muted);
            int xp = session.GetSpecialCardExperience(card.Id);
            int threshold = session.GetSpecialCardNextThreshold(card.Id);
            Text experience = CreateText("Special Card Detail Experience", panel, new Vector2(720f, 44f), new Vector2(407f, -190f), 22, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            int remainingExperience = Mathf.Max(0, threshold - xp);
            int remainingNauticalMiles = threshold == 0 ? 0 : Mathf.CeilToInt((float)remainingExperience / (float)CoreVoyageSession.SpecialCardExperiencePerNauticalMile);
            experience.text = card.UpgradeLevel >= 3 ? "熟练三阶已满 · 航程经验 " + xp : "航程经验 " + xp + " / " + threshold + "　·　再航行约 " + remainingNauticalMiles + " 海里升级";
            experience.horizontalOverflow = HorizontalWrapMode.Wrap;
            experience.verticalOverflow = VerticalWrapMode.Truncate;
            Text active = CreateText("Special Card Detail Active", panel, new Vector2(720f, 42f), new Vector2(407f, -240f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
            active.text = IsSpecialCardSailing(index) ? "当前来源随船 · 每实际航行约 4 海里获得 1 点经验" : "收藏保留 · 对应船员上船或重新装备对应部件后启用";
            active.horizontalOverflow = HorizontalWrapMode.Wrap;
            active.verticalOverflow = VerticalWrapMode.Truncate;
            StackReadableInspection(panel, "Special Card Detail Reading Viewport", new Vector2(720f, 430f), new Vector2(407f, -38f),
                new[] { "Special Card Detail Name", "Special Card Detail Source", "Special Card Detail Stages", "Special Card Detail Effect", "Special Card Detail Lore", "Special Card Detail Experience", "Special Card Detail Active" });
        }
    }
}
