using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool treasureFolioOpen;
        private bool treasureFolioShowCompleted;
        private string treasureDetailMapId;

        private void BuildTreasureWorkbench(RectTransform room)
        {
            RectTransform desk = TreasureWoodPanel("Treasure Workbench", room,
                new Vector2(1240f, 176f), new Vector2(0f, 265f));
            TreasureLabel("Treasure Workbench Heading", desk, new Vector2(1160f, 32f),
                new Vector2(0f, 57f), 24, Gold, "拼合藏宝图 · 探索隐藏海域", TextAnchor.MiddleCenter);
            string moonlitStatus = session.PirateLevel >= CoreTreasureHuntCatalog.MoonlitUnlockLevel
                ? "月蚀率 " + ((int)(session.MoonlitCraftChance * 100d)) + "%"
                : "月蚀 5级解锁";
            TreasureLabel("Treasure Workbench Status", desk, new Vector2(1160f, 28f),
                new Vector2(0f, 23f), 19, Ink,
                "海盗等级 " + session.PirateLevel + "　·　图碎片 " + session.TreasureFragments +
                "/" + CoreTreasureHuntCatalog.FragmentCost + "　·　持有藏宝图 " + session.TreasureMaps.Count +
                "/" + CoreTreasureHuntCatalog.MaxHeldMaps + "　·　" + moonlitStatus, TextAnchor.MiddleCenter);
            string reason = TreasureCraftUnavailableReason();
            string notice = !string.IsNullOrEmpty(reason) ? reason : "每次消耗 4 个图碎片；拼合后可查看品级、种子并装备出航。";
            if (session.SelectedTreasureMap != null)
                notice = "已装备「" + session.SelectedTreasureMap.Name + "」 · 出航校验通过后消耗；撤离或失败不返还。";
            TreasureLabel("Treasure Workbench Notice", desk, new Vector2(1160f, 28f),
                new Vector2(0f, -9f), 17, Ink, notice, TextAnchor.MiddleCenter);
            Button craft = CreateButton("Treasure Craft Map", desk, new Vector2(350f, 42f), new Vector2(-390f, -57f), Gold, Ink, 20);
            Text craftLabel = craft.GetComponentInChildren<Text>();
            craftLabel.text = "拼合藏宝图 · 4 碎片";
            RawImage fragmentIcon = CreateItemIcon("Treasure Craft Fragment Icon", craft.transform,
                ItemIconCatalog.ForSpecial("treasurefragment"), new Vector2(-143f, 0f), 32f);
            if (fragmentIcon.enabled)
            {
                craftLabel.rectTransform.sizeDelta = new Vector2(282f, 32f);
                craftLabel.rectTransform.anchoredPosition = new Vector2(21f, 0f);
            }
            craft.interactable = string.IsNullOrEmpty(reason);
            craft.onClick.AddListener(() =>
            {
                bool crafted = session.TryCraftTreasureMap(null, out string result);
                portNotice = result;
                if (crafted)
                {
                    PlaySfx("treasure_craft", 0.58f);
                    treasureFolioOpen = true;
                    treasureFolioShowCompleted = false;
                    treasureDetailMapId = session.SelectedTreasureMap?.Id;
                    SaveProgress();
                }
                Render();
            });
            Button folio = CreateButton("Treasure Open Folio", desk, new Vector2(350f, 42f), new Vector2(0f, -57f), PanelRaised, Ink, 20);
            folio.GetComponentInChildren<Text>().text = "打开藏图册 · " + session.TreasureMaps.Count + " 张";
            folio.onClick.AddListener(() => { treasureFolioOpen = true; treasureFolioShowCompleted = false; Render(); });
            BuildCursePurificationButton(desk, new Vector2(350f, 42f), new Vector2(390f, -57f));
            if (treasureFolioOpen) BuildTreasureFolio();
        }

        private string TreasureCraftUnavailableReason()
        {
            if (session.PirateLevel < 2) return "海盗等级 2 解锁拼图；先完成航行，积累海盗经验。";
            if (session.TreasureMaps.Count >= CoreTreasureHuntCatalog.MaxHeldMaps) return "藏图册已满（6/6）；先装备一张藏宝图出航。";
            if (session.TreasureFragments < CoreTreasureHuntCatalog.FragmentCost)
                return "还需 " + (CoreTreasureHuntCatalog.FragmentCost - session.TreasureFragments) + " 个图碎片；航行事件与测绘可收集，击败首领有 50% 概率掉落 1 个。";
            return string.Empty;
        }

        private void BuildTreasureFolio()
        {
            RectTransform overlay = CreateFixedPanel("Treasure Folio Overlay", contentRoot,
                PhaseSurfaceLayout.BackgroundSize, PhaseSurfaceLayout.BackgroundPosition, new Color(0.012f, 0.018f, 0.024f, 0.78f));
            RectTransform folio = TreasureWoodPanel("Treasure Folio", overlay, new Vector2(1540f, 720f), new Vector2(0f, 14f));
            TreasureLabel("Treasure Folio Title", folio, new Vector2(1200f, 44f), new Vector2(-60f, 309f),
                28, Gold, "藏图册 · 一次性的寻宝航程", TextAnchor.MiddleCenter);
            Button close = CreateButton("Treasure Close Folio", folio, new Vector2(94f, 44f), new Vector2(678f, 309f), PanelRaised, Ink, 18);
            close.GetComponentInChildren<Text>().text = "关闭";
            close.onClick.AddListener(() => { treasureFolioOpen = false; Render(); });

            CoreTreasureMapRecord selected = session.TreasureMaps.FirstOrDefault(map => map.Id == treasureDetailMapId)
                ?? session.SelectedTreasureMap ?? session.TreasureMaps.FirstOrDefault();
            treasureDetailMapId = selected?.Id;
            for (int index = 0; index < session.TreasureMaps.Count; index++)
            {
                int captured = index;
                CoreTreasureMapRecord map = session.TreasureMaps[index];
                Button card = CreateButton("Treasure Map Select " + index, folio, new Vector2(430f, 65f),
                    new Vector2(-510f, 221f - index * 77f), PanelRaised, Ink, 18);
                Text cardLabel = card.GetComponentInChildren<Text>();
                cardLabel.text = map.Name + (session.SelectedTreasureMap?.Id == map.Id ? " · 已装备" : string.Empty) +
                    "\n推荐战力 " + map.Rank.RecommendedPower + " · 线索 " + map.Rank.RequiredClues;
                RawImage rankIcon = CreateItemIcon("Treasure Map Rank Icon " + index, card.transform,
                    ItemIconCatalog.ForTreasureMap(map.RankId), new Vector2(-182f, 0f), 40f);
                if (rankIcon.enabled)
                {
                    cardLabel.rectTransform.sizeDelta = new Vector2(340f, 55f);
                    cardLabel.rectTransform.anchoredPosition = new Vector2(25f, 0f);
                }
                WoodButtonSkin.SetSelected(card, !treasureFolioShowCompleted && map.Id == selected?.Id);
                card.onClick.AddListener(() =>
                {
                    treasureDetailMapId = session.TreasureMaps[captured].Id;
                    treasureFolioShowCompleted = false;
                    Render();
                });
            }
            if (session.TreasureMaps.Count == 0)
            {
                CreateItemIcon("Treasure Empty Inventory Fragment Icon", folio,
                    ItemIconCatalog.ForSpecial("treasurefragment"), new Vector2(-510f, 245f), 40f);
                TreasureLabel("Treasure Empty Inventory", folio, new Vector2(420f, 190f), new Vector2(-510f, 126f),
                    23, Ink, "藏图册中还没有藏宝图\n\n收集 4 个图碎片\n回到藏宝工作台拼合", TextAnchor.MiddleCenter);
            }

            RectTransform paper = CreateFixedPanel("Treasure Folio Parchment", folio, new Vector2(945f, 480f),
                new Vector2(240f, 0f), new Color(0.97f, 0.90f, 0.74f));
            Image face = paper.GetComponent<Image>();
            face.sprite = NauticalSurfaceSkin.Paper;
            face.type = Image.Type.Tiled;
            face.pixelsPerUnitMultiplier = face.sprite == null ? 1f : face.sprite.rect.width / 400f;
            Color ink = Html("#302116");
            string heading;
            string details;
            if (treasureFolioShowCompleted)
            {
                heading = "秘藏完成回执 · " + session.CompletedTreasureSeeds.Count + " 次";
                details = session.CompletedTreasureSeeds.Count == 0 ? "尚未完成隐藏海域。\n\n拼合并装备藏宝图，在隐藏海域收集线索后开启秘藏。" :
                    "最近完成的种子（最多 5 条）\n" + string.Join("\n", session.CompletedTreasureSeeds.Reverse().Take(5)) +
                    "\n\n奖励已随返港结算保存；完成记录永久保留，不会重复领取。";
                details += "\n\n当前随船库存：宝石 " + session.Cargo.Gem + " · 古代材料 " + session.Cargo.AncientMaterial +
                    "\n稀有遗物 " + session.Cargo.Relic + " · 黑珍珠 " + session.Cargo.BlackPearl;
            }
            else if (selected != null)
            {
                heading = selected.Name + (session.SelectedTreasureMap?.Id == selected.Id ? " · 已装备" : " · 待装备");
                details = "种子\n" + selected.Seed + "\n\n推荐战力  " + selected.Rank.RecommendedPower +
                    "\n建议航期  " + selected.Rank.RecommendedVoyageDays + " 天\n开启秘藏需要  " + selected.Rank.RequiredClues +
                    " 条线索\n在隐藏海域的支线地点收集线索。";
            }
            else
            {
                heading = "从碎片到藏宝图";
                details = "① 海盗等级达到 2。\n② 收集 4 个图碎片，在工作台拼合一张藏宝图。\n   航行事件、测绘可获碎片；击败首领有 50% 概率掉落 1 个。\n   新手引导末尾的首领必定掉落 1 个。\n③ 查看品级与准备要求，装备后到出航码头。\n④ 在隐藏海域收集线索，开启秘藏并带回奖励。\n\n未使用藏宝图最多保存 6 张。\n出航准备校验失败时不会消耗藏宝图。";
            details += "\n\n月蚀保底：5级解锁，初始15%；每次非月蚀 +15%，最高100%，月蚀后重置。";
            }
            bool showMap = selected != null && !treasureFolioShowCompleted;
            ItemIconArt headingArt = treasureFolioShowCompleted ? default : selected != null
                ? ItemIconCatalog.ForTreasureMap(selected.RankId) : ItemIconCatalog.ForSpecial("treasurefragment");
            if (headingArt.IsAvailable)
                CreateItemIcon("Treasure Map Heading Icon", paper, headingArt, new Vector2(-412f, 184f), 40f);
            TreasureLabel("Treasure Map Heading", paper,
                new Vector2(headingArt.IsAvailable ? 813f : 865f, 44f),
                new Vector2(headingArt.IsAvailable ? 26f : 0f, 184f), 25, ink, heading, TextAnchor.MiddleLeft);
            TreasureLabel("Treasure Map Details", paper, showMap ? new Vector2(435f, 260f) : new Vector2(865f, 340f),
                showMap ? new Vector2(-215f, 18f) : new Vector2(0f, -29f), showMap ? 18 : 20, ink, details, TextAnchor.UpperLeft);
            if (showMap)
            {
                Texture2D texture = Resources.Load<Texture2D>("TreasureHunt/treasure_hunt_route_map");
                RawImage map = CreateRawImage("Treasure Map Preview", paper, new Vector2(390f, 219f),
                    new Vector2(238f, 39f), texture, new Rect(0f, 0f, 1f, 1f));
                map.raycastTarget = false;
                TreasureLabel("Treasure Map Preview Note", paper, new Vector2(390f, 44f), new Vector2(238f, -108f),
                    18, ink, "寻访支线收集线索，再开启秘藏", TextAnchor.MiddleCenter);
                TreasureLabel("Treasure Map Use Rules", paper, new Vector2(865f, 76f), new Vector2(0f, -182f),
                    18, ink, "出航时消耗：物资、金币与货舱校验通过后扣除本图。\n中途撤离或失败不返还；隐藏海域不能保存为普通海图。\n装备后前往码头，确认船只、船员和补给。", TextAnchor.UpperLeft);
            }

            Button history = CreateButton("Treasure Open Completion", folio, new Vector2(430f, 42f), new Vector2(-510f, -279f), PanelRaised, Ink, 18);
            history.GetComponentInChildren<Text>().text = "秘藏完成回执 · " + session.CompletedTreasureSeeds.Count;
            history.onClick.AddListener(() => { treasureFolioShowCompleted = true; Render(); });
            Button equip = CreateButton("Treasure Equip And Depart", folio, new Vector2(590f, 52f), new Vector2(389f, -289f), Gold, Ink, 21);
            equip.GetComponentInChildren<Text>().text = "装备藏宝图 · 前往出航码头";
            equip.interactable = selected != null && !treasureFolioShowCompleted;
            equip.onClick.AddListener(() =>
            {
                if (selected != null && session.TrySelectTreasureMap(selected.Id, out string reason))
                {
                    portNotice = reason;
                    PlaySfx("treasure_equip", 0.5f);
                    SaveProgress();
                    treasureFolioOpen = false;
                    activePortFacility = 8;
                }
                Render();
            });
            Button clear = CreateButton("Treasure Unequip Map", folio, new Vector2(250f, 52f), new Vector2(-73f, -289f), PanelRaised, Ink, 18);
            clear.GetComponentInChildren<Text>().text = "取消装备";
            clear.interactable = session.SelectedTreasureMap != null;
            clear.onClick.AddListener(() => { session.ClearSelectedTreasureMap(); SaveProgress(); Render(); });
        }

        private RectTransform TreasureWoodPanel(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform panel = CreateFixedPanel(name, parent, size, position, Color.white);
            Image face = panel.GetComponent<Image>();
            face.sprite = WoodButtonSkin.Face;
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = 14f;
            return panel;
        }

        private Text TreasureLabel(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, Color color, string value, TextAnchor alignment)
        {
            Text label = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, alignment, color);
            label.text = value;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }
    }
}
