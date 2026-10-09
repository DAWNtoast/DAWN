using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private VoyageSettlement displayedSettlement;
        private Text settlementProtectionLabel;
        private Button settlementDrawChartButton;

        private void BuildSettlementPresentation(VoyageSettlement result, bool previewSample)
        {
            if (result == null) return;
            displayedSettlement = result;
            settlementDrawChartButton = null;
            bool failure = result.Outcome == VoyageOutcome.Failure;
            bool hasLoot = result.BankedGold > 0 || result.BankedWood > 0 || result.BankedIron > 0 || result.BankedFame > 0 ||
                result.BankedFragments > 0 || result.BankedGem > 0 || result.BankedAncientMaterial > 0 || result.BankedRelic > 0 || result.BankedBlackPearl > 0;
            bool celebrate = hasLoot && !failure;
            Color accent = failure ? Html("#BCB6AE") : Html("#F6CD73");
            // Settlement owns the screen; the prototype navigation header breaks the reveal's focus.
            if (headerTitle != null && headerTitle.transform.parent != null)
                headerTitle.transform.parent.gameObject.SetActive(false);
            RectTransform card = CreateRect("Settlement Card", contentRoot, new Vector2(1860, 940), new Vector2(0, 45));
            // The card is raised 45px; compensate so the backdrop still covers
            // the full canvas rather than leaving a black strip along the bottom.
            var backdrop = CreateRawImage("Settlement Hold", card, new Vector2(1920, 1080), -card.anchoredPosition,
                Resources.Load<Texture2D>("SettlementReveal/settlement_hold_v1"), new Rect(0, 0, 1, 1));
            backdrop.color = failure ? new Color(0.53f, 0.60f, 0.65f) : Color.white;
            backdrop.raycastTarget = false;
            CreateImage("Settlement Shade", card, card.sizeDelta, Vector2.zero, new Color(0.035f, 0.025f, 0.025f, 0.42f)).raycastTarget = false;
            SettlementFrame(card, "Settlement Outer Frame", card.sizeDelta, Vector2.zero, new Color(0.01f, 0.01f, 0.01f, 0.12f));

            var tracks = new List<RectTransform>();
            var delays = new List<float>();
            RectTransform heading = CreateRect("Settlement Headline", card, new Vector2(1490, 186), new Vector2(0, 338));
            SettlementText("Settlement Badge", heading, new Vector2(1400, 28), new Vector2(0, 75), 19, accent).text =
                previewSample ? "结算预览 · 不写入存档" : "远航结束 · 船长战报";
            SettlementText("Settlement Title", heading, new Vector2(1440, 88), new Vector2(0, 14), 68, accent).text =
                failure ? "整备，再启航" : result.TreasureCompleted ? "秘藏归航" : result.Outcome == VoyageOutcome.ArrivedAtPort ? "凯旋归港" : hasLoot ? "战利归仓" : "平安归航";
            SettlementText("Settlement Destination", heading, new Vector2(1440, 38), new Vector2(0, -54), 25, Ink).text =
                (failure ? "航行失败 · " : result.Title + " · ") + result.Destination;
            tracks.Add(heading); delays.Add(0f);

            RectTransform hero = CreateRect("Settlement Hero", card, new Vector2(700, 480), new Vector2(-510, 4));
            var sparkRoot = CreateRect("Settlement Gold Burst", hero, new Vector2(680, 450), Vector2.zero);
            var sparks = sparkRoot.gameObject.AddComponent<SettlementCelebrationGraphic>();
            sparks.raycastTarget = false;
            sparks.gameObject.SetActive(celebrate);
            if (celebrate)
            {
                var chest = CreateRawImage("Settlement Treasure Chest", hero, new Vector2(690, 460), new Vector2(0, -5),
                    Resources.Load<Texture2D>("SettlementReveal/settlement_chest_v1"), new Rect(0, 0, 1, 1));
                chest.raycastTarget = false;
                sparkRoot.SetAsLastSibling();
            }
            else
            {
                var journal = CreateRawImage("Settlement Voyage Journal Still Life", hero, new Vector2(630, 420), new Vector2(0, 20),
                    Resources.Load<Texture2D>("SettlementReveal/settlement_journal_v1"), new Rect(0, 0, 1, 1));
                journal.raycastTarget = false;
                journal.color = failure ? new Color(0.68f, 0.72f, 0.76f) : Color.white;
                SettlementText("Settlement Quiet Caption", hero, new Vector2(620, 54), new Vector2(0, -198), 25, Ink).text =
                    failure ? "保住的物资，是下一次远航的底气" : "船员平安，便有下一次远航";
            }
            tracks.Add(hero); delays.Add(0.20f);
            if (result.BankedFragments > 0 || (!tutorialActive && (result.BankedPirateExperience > 0 || result.TreasureCompleted)))
            {
                var treasureIcons = new List<string>();
                if (result.TreasureCompleted) treasureIcons.AddRange(new[] { "gem", "ancientmaterial", "relic", "blackpearl" });
                if (result.BankedFragments > 0) treasureIcons.Add("treasurefragment");
                bool showIcons = treasureIcons.Count > 0;
                // The tutorial reward plaque occupies the space above this
                // manifest. Keep its original height and put icons beside text.
                bool iconStrip = showIcons && !tutorialActive;
                bool iconColumn = showIcons && tutorialActive;
                int iconColumns = treasureIcons.Count > 3 ? 2 : 1;
                int iconRows = (treasureIcons.Count + iconColumns - 1) / iconColumns;
                RectTransform manifest = SettlementFrame(hero, "Settlement Treasure Manifest", new Vector2(660, iconStrip ? 152 : 112),
                    new Vector2(0, iconStrip ? -137 : -157), new Color(0.08f, 0.045f, 0.015f, 0.95f));
                for (int i = 0; i < treasureIcons.Count; i++)
                {
                    string key = treasureIcons[i];
                    CreateItemIcon("Settlement Treasure Icon " + key, manifest,
                        key == "treasurefragment" ? ItemIconCatalog.ForSpecial(key) : ItemIconCatalog.ForCargo(key),
                        iconStrip ? new Vector2((i - (treasureIcons.Count - 1) * .5f) * 44f, 52f) :
                            new Vector2(-302f + i % iconColumns * 40f, ((iconRows - 1) * .5f - i / iconColumns) * 36f), 32f);
                }
                string treasures = result.BankedFragments > 0 ? "藏宝图碎片 +" + result.BankedFragments : string.Empty;
                if (result.BankedPirateExperience > 0)
                    treasures += (treasures.Length > 0 ? "　·　" : string.Empty) + "海盗经验 +" + result.BankedPirateExperience;
                if (result.TreasureCompleted)
                    treasures = "宝石 +" + result.BankedGem + "　古代材料 +" + result.BankedAncientMaterial +
                        "　稀有遗物 +" + result.BankedRelic + "\n黑珍珠 +" + result.BankedBlackPearl + "　·　" + treasures;
                SettlementText("Settlement Treasure Rewards", manifest, new Vector2(iconColumn ? 620 - iconColumns * 44 : 620, 100),
                    new Vector2(iconColumn ? iconColumns * 22 : 0, iconStrip ? -22 : 0),
                    20, Ink).text = treasures;
            }

            int[] rewards = { result.BankedGold, result.BankedWood, result.BankedIron, result.BankedFame };
            string[] ids = { "Gold", "Wood", "Iron", "Fame" };
            string[] names = { "金币", "木材", "铁矿", "声望" };
            string[] details = { "船长资金", "船体修缮", "铸造储备", "远航声名" };
            int featured = Array.FindIndex(rewards, value => value > 0); if (featured < 0) featured = 0;
            var values = new List<Text>(); var targets = new List<int>(); var countDelays = new List<float>();
            RectTransform rewardRoot = CreateRect("Banked Rewards", card, new Vector2(880, 438), new Vector2(420, 12));
            SettlementText("Settlement Reward Heading", rewardRoot, new Vector2(820, 34), new Vector2(0, 200), 23, accent).text =
                failure ? "抢救物资 · 已扣除损失" : hasLoot ? "本次战利品 · 已按保护率折算" : "本次暂无新增战利品";
            RectTransform main = SettlementFrame(rewardRoot, "Settlement Featured Reward", new Vector2(850, 215), new Vector2(0, 63), new Color(0.06f, 0.045f, 0.025f, 0.90f));
            ItemIconArt featuredIcon = SettlementItemIcon(ids[featured]);
            if (featuredIcon.IsAvailable)
                CreateItemIcon("Settlement Featured Icon " + ids[featured], main, featuredIcon, new Vector2(-374, 76), 48f);
            SettlementText("Settlement Featured Name", main, new Vector2(featuredIcon.IsAvailable ? 730 : 790, 48),
                new Vector2(featuredIcon.IsAvailable ? 30 : 0, 76), 25, accent).text = names[featured];
            Text mainValue = SettlementText("Settlement Reward " + ids[featured], main, new Vector2(790, 112), new Vector2(0, -4), 96, hasLoot ? accent : MutedInk);
            SettlementText("Settlement Featured Detail", main, new Vector2(790, 30), new Vector2(0, -79), 18, MutedInk).text = details[featured] + "";
            tracks.Add(main); delays.Add(0.55f); values.Add(mainValue); targets.Add(rewards[featured]); countDelays.Add(0.65f);
            int slot = 0;
            for (int i = 0; i < rewards.Length; i++)
            {
                if (i == featured) continue;
                RectTransform tile = SettlementFrame(rewardRoot, "Settlement Reward Tile " + ids[i], new Vector2(270, 133), new Vector2(-290 + slot * 290, -131), new Color(0.045f, 0.035f, 0.025f, 0.88f));
                ItemIconArt resourceIcon = SettlementItemIcon(ids[i]);
                if (resourceIcon.IsAvailable)
                    CreateItemIcon("Settlement Resource Icon " + ids[i], tile, resourceIcon, new Vector2(-104, 46), 32f);
                SettlementText("Settlement Resource " + ids[i], tile, new Vector2(resourceIcon.IsAvailable ? 198 : 246, 30),
                    new Vector2(resourceIcon.IsAvailable ? 24 : 0, 46), 22, Ink).text = names[i];
                Text number = SettlementText("Settlement Reward " + ids[i], tile, new Vector2(245, 59), new Vector2(0, 0), rewards[i] >= 1000000 ? 28 : 43, rewards[i] > 0 ? accent : MutedInk);
                SettlementText("Settlement Resource Hint " + ids[i], tile, new Vector2(246, 26), new Vector2(0, -44), 16, MutedInk).text = details[i];
                tracks.Add(tile); delays.Add(0.90f + slot * 0.16f); values.Add(number); targets.Add(rewards[i]); countDelays.Add(1.0f + slot * 0.16f); slot++;
            }

            RectTransform ribbon = SettlementFrame(card, "Settlement Protection Seal", new Vector2(1590, 57), new Vector2(0, -249), new Color(0.10f, 0.065f, 0.025f, 0.9f));
            settlementProtectionLabel = SettlementText("Settlement Protection", ribbon, new Vector2(1500, 40), Vector2.zero, 24, accent);
            settlementProtectionLabel.text = SettlementProtectionText(result);
            tracks.Add(ribbon); delays.Add(2.05f);
            RectTransform metrics = CreateRect("Settlement Metrics", card, new Vector2(1590, 66), new Vector2(0, -315));
            SettlementText("Settlement Distance", metrics, new Vector2(480, 64), new Vector2(-525, 0), 23, Ink).text = "航程  " + result.DistanceNm.ToString("N0") + " 海里\n用时 " + result.Hours.ToString("0.0") + " 小时";
            SettlementText("Settlement Exploration", metrics, new Vector2(480, 64), Vector2.zero, 23, Ink).text = "探索  " + result.Coverage.ToString("0") + "%";
            SettlementText("Settlement Events", metrics, new Vector2(480, 64), new Vector2(525, 0), 23, Ink).text = "已处理事件  " + result.CompletedEvents + " / " + result.TotalEvents + "\n" + (result.Outcome == VoyageOutcome.ArrivedAtPort ? "到港后可再次出航" : "");
            bool canDrawChart = !previewSample && !failure && session.CanDrawSeaChart;
            BuildSettlementStorageOption(card, result, previewSample);
            if (canDrawChart)
            {
                Button draw = CreateButton("Draw Sea Chart", card, new Vector2(350, 72), new Vector2(-18, -410), PanelRaised, Ink, 23);
                draw.GetComponentInChildren<Text>().text = pendingDrawnChart == null ? "航海士 · 绘制海图" : "海图已绘制";
                draw.interactable = pendingDrawnChart == null;
                settlementDrawChartButton = draw;
                draw.onClick.AddListener(() =>
                {
                    if (previewSample || draw == null || !draw.gameObject.activeInHierarchy ||
                        !ReferenceEquals(session.Settlement, result) || !ReferenceEquals(displayedSettlement, result)) return;
                    DrawCurrentSeaChart();
                });
            }
            Button finish = CreateButton("Return To Port", card, new Vector2(600, 80), new Vector2(510, -410), Gold, Ink, 28);
            finish.GetComponentInChildren<Text>().text = "确认结算 · 返回港口";
            finish.onClick.AddListener(() =>
            {
                if (previewSample || session.Phase != CoreDemoPhase.Settlement || !ReferenceEquals(session.Settlement, result)) return;
                ConfirmVoyageSettlement();
            });
            Button skip = CreateButton("Skip Settlement Reveal", card, new Vector2(185, 42), new Vector2(793, 426), PanelRaised, Ink, 17);
            skip.GetComponentInChildren<Text>().text = "跳过演出";
            var reveal = card.gameObject.AddComponent<SettlementRevealController>();
            reveal.Configure(tracks.ToArray(), delays.ToArray(), values.ToArray(), targets.ToArray(), countDelays.ToArray(), sparks, skip,
                celebrate, PlaySfx, Application.isPlaying && !previewSample,
                SettlementRevealAudio.OutcomeCue(result), result.TreasureCompleted);
            skip.onClick.AddListener(reveal.CompleteImmediately);
        }

        private string SettlementProtectionText(VoyageSettlement result) =>
            "保全 " + (result.ProtectionRatio * 100f).ToString("0") + "% 战利品　 /　" +
            (result.TreasureCompleted ? "秘藏已开启 · 完成种子已记录" : result.Outcome == VoyageOutcome.ArrivedAtPort ? "首领航道已打通" :
                result.Outcome == VoyageOutcome.Failure ? "已计入抢救所得，不再重复扣损" : "主动返航 · 收获留存") +
            "　 /　" + (session.IsTreasureVoyage ? "藏宝图已消耗 · 不可绘图" : pendingDrawnChart != null ? "海图已绘制 · 确认后收录" :
                result.ChartSaved ? "航海士可绘制本程海图" : "海图未绘制");

        private static ItemIconArt SettlementItemIcon(string id) => id == "Gold"
            ? ItemIconCatalog.ForSpecial("gold") : ItemIconCatalog.ForCargo(id);

        private void RefreshSettlementChartState()
        {
            if (session.Phase != CoreDemoPhase.Settlement || !ReferenceEquals(displayedSettlement, session.Settlement) ||
                settlementProtectionLabel == null || !settlementProtectionLabel.gameObject.activeInHierarchy) return;
            // Restore ordinary button state before redecorating the tutorial;
            // this unlocks its confirmation step without replaying the rewards.
            RestoreGuidedTutorialControls(false);
            UpdateGuidedTutorialProgress();
            settlementProtectionLabel.text = SettlementProtectionText(session.Settlement);
            if (settlementDrawChartButton != null)
            {
                settlementDrawChartButton.GetComponentInChildren<Text>().text = "海图已绘制";
                settlementDrawChartButton.interactable = false;
            }
            BuildGuidedTutorialOverlay();
        }

        private RectTransform SettlementFrame(Transform parent, string name, Vector2 size, Vector2 position, Color fill)
        {
            RectTransform panel = CreateFixedPanel(name, parent, size, position, fill);
            panel.GetComponent<Image>().raycastTarget = false;
            Image rim = CreateImage(name + " Carved Rim", panel, size, Vector2.zero, Color.white, WoodButtonSkin.Face);
            rim.type = Image.Type.Sliced; rim.fillCenter = false; rim.pixelsPerUnitMultiplier = 11f; rim.raycastTarget = false;
            return panel;
        }

        private Text SettlementText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, alignment, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false; text.raycastTarget = false;
            return text;
        }
    }
}

