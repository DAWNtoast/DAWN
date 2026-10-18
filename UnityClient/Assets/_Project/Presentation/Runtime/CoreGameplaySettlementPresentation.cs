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
            SettlementChestRewardPlan chestPlan = SettlementChestRewardPlan.Create(result);
            bool playChest = Application.isPlaying && !previewSample && !editorPreviewBuilding &&
                !ReferenceEquals(settlementChestShown, result);
            settlementDrawChartButton = null;
            bool failure = result.Outcome == VoyageOutcome.Failure;
            bool hasLoot = result.BankedGold > 0 || result.BankedWood > 0 || result.BankedIron > 0 || result.BankedFame > 0 ||
                result.BankedFragments > 0 || result.BankedGem > 0 || result.BankedAncientMaterial > 0 || result.BankedRelic > 0 || result.BankedBlackPearl > 0;
            bool celebrate = hasLoot && !failure;
            Color accent = failure ? Html("#815B4C") : Html("#285B52");
            Color ink = MarketShopSkin.Ink;
            Color muted = MarketShopSkin.MutedInk;
            string returnPort = result.Outcome == VoyageOutcome.ArrivedAtPort ? result.Destination : session.CurrentPort;
            Texture2D harbor = portArt.PortBackgroundFor(returnPort);
            // Settlement owns the screen; the prototype navigation header breaks the reveal's focus.
            if (headerTitle != null && headerTitle.transform.parent != null)
                headerTitle.transform.parent.gameObject.SetActive(false);
            RectTransform card = CreateRect("Settlement Card", contentRoot, new Vector2(1860, 940), new Vector2(0, 45));
            // Borrow the painted destination the player will enter; keep the backdrop centered.
            var backdrop = CreateRawImage("Settlement Hold", card, new Vector2(1920, 1080), -card.anchoredPosition,
                harbor, new Rect(0, 0, 1, 1));
            backdrop.color = failure ? new Color(.56f, .60f, .60f) : new Color(.72f, .72f, .68f);
            backdrop.raycastTarget = false;
            CreateImage("Settlement Paper Shadow", card, new Vector2(1832, 952), new Vector2(0, -7),
                new Color(.05f, .04f, .025f, .42f)).raycastTarget = false;
            SettlementFrame(card, "Settlement Outer Frame", new Vector2(1810, 940), Vector2.zero, Color.white);
            SettlementRule(card, "Settlement Header Rule", new Vector2(1630, 1), new Vector2(0, 252));
            SettlementRule(card, "Settlement Ledger Fold", new Vector2(1, 423), new Vector2(-120, 21));
            SettlementRule(card, "Settlement Footer Rule", new Vector2(1630, 1), new Vector2(0, -357));

            var tracks = new List<RectTransform>();
            var delays = new List<float>();
            RectTransform heading = CreateRect("Settlement Headline", card, new Vector2(1490, 186), new Vector2(0, 344));
            SettlementText("Settlement Badge", heading, new Vector2(1400, 28), new Vector2(0, 75), 18, muted).text =
                previewSample ? "结算预览 · 不写入存档" : "航 海 纪 事  /  归 航 战 报";
            SettlementText("Settlement Title", heading, new Vector2(1440, 88), new Vector2(0, 14), 68, ink).text =
                failure ? "整备，再启航" : result.TreasureCompleted ? "秘藏归航" : result.Outcome == VoyageOutcome.ArrivedAtPort ? "凯旋归港" : hasLoot ? "战利归仓" : "平安归航";
            SettlementText("Settlement Destination", heading, new Vector2(1440, 38), new Vector2(0, -54), 23, muted).text =
                (failure ? "航行失败 · 返抵 " : result.Title + " · ") + returnPort;
            tracks.Add(heading); delays.Add(0f);

            RectTransform hero = CreateRect("Settlement Hero", card, new Vector2(700, 480), new Vector2(-480, 4));
            bool hasRareRewards = result.BankedGem > 0 || result.BankedAncientMaterial > 0 ||
                result.BankedRelic > 0 || result.BankedBlackPearl > 0;
            bool hasManifest = result.BankedFragments > 0 || hasRareRewards ||
                (!tutorialActive && (result.BankedPirateExperience > 0 || result.TreasureCompleted));
            // Preserve separate rows for the tutorial reward plaque and fragment manifest.
            float portraitHeight = tutorialActive ? 230f : hasManifest ? 280f : 340f;
            Vector2 portraitSize = new Vector2(660, portraitHeight);
            Vector2 portraitPosition = new Vector2(0, 232f - portraitHeight * .5f);
            RectTransform portrait = CreateRect("Settlement Harbor Portrait", hero, portraitSize, portraitPosition);
            CreateImage("Settlement Portrait Border", portrait, portraitSize + Vector2.one * 6f, Vector2.zero,
                Html("#917047")).raycastTarget = false;
            var harborImage = CreateRawImage("Settlement Harbor Painting", portrait, portraitSize, Vector2.zero,
                harbor, SettlementPortraitUv(harbor, portraitSize));
            harborImage.raycastTarget = false;
            harborImage.color = failure ? new Color(.72f, .75f, .74f) : Color.white;
            RectTransform caption = CreateRect("Settlement Harbor Caption", portrait, new Vector2(660, 48),
                new Vector2(0, -portraitHeight * .5f + 24));
            CreateImage("Settlement Harbor Caption Shade", caption, new Vector2(660, 48), Vector2.zero,
                new Color(.07f, .13f, .13f, .84f)).raycastTarget = false;
            SettlementText("Settlement Port Caption", caption, new Vector2(612, 36), Vector2.zero, 22,
                Html("#F8EBD0")).text = returnPort + "  ·  " + (failure ? "修整待发" : "落帆归港");
            var sparkClip = CreateRect("Settlement Celebration Clip", portrait, portraitSize, Vector2.zero);
            sparkClip.gameObject.AddComponent<RectMask2D>();
            var sparkRoot = CreateRect("Settlement Gold Burst", sparkClip, portraitSize, Vector2.zero);
            var sparks = sparkRoot.gameObject.AddComponent<SettlementCelebrationGraphic>();
            sparks.raycastTarget = false;
            sparks.gameObject.SetActive(celebrate);
            if (!hasManifest && !tutorialActive)
            {
                SettlementText("Settlement Quiet Caption", hero, new Vector2(640, 44), new Vector2(0, -151), 23, ink).text =
                    failure ? "保住的物资，是下一次远航的底气" : hasLoot ? "收起风帆，记下这一程的收获" : "船员平安，便有下一次远航";
                SettlementText("Settlement Journal Caption", hero, new Vector2(640, 28), new Vector2(0, -195), 17, muted).text =
                    "航程已结束 · 收获与足迹载入本页";
            }
            tracks.Add(hero); delays.Add(0.20f);
            if (hasManifest)
            {
                var treasureIcons = new List<string>();
                if (result.TreasureCompleted || hasRareRewards) treasureIcons.AddRange(new[] { "gem", "ancientmaterial", "relic", "blackpearl" });
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
                if (result.TreasureCompleted || hasRareRewards)
                    treasures = "宝石 +" + result.BankedGem + "　古代材料 +" + result.BankedAncientMaterial +
                        "　稀有遗物 +" + result.BankedRelic + "\n黑珍珠 +" + result.BankedBlackPearl + "　·　" + treasures;
                SettlementText("Settlement Treasure Rewards", manifest, new Vector2(iconColumn ? 620 - iconColumns * 44 : 620, 100),
                    new Vector2(iconColumn ? iconColumns * 22 : 0, iconStrip ? -22 : 0),
                    20, ink).text = treasures;
            }

            int[] rewards = { result.BankedGold, result.BankedWood, result.BankedIron, result.BankedFame };
            string[] ids = { "Gold", "Wood", "Iron", "Fame" };
            string[] names = { "金币", "木材", "铁矿", "声望" };
            int featured = Array.FindIndex(rewards, value => value > 0); if (featured < 0) featured = 0;
            var values = new List<Text>(); var targets = new List<int>(); var countDelays = new List<float>();
            RectTransform rewardRoot = CreateRect("Banked Rewards", card, new Vector2(880, 438), new Vector2(390, 12));
            SettlementText("Settlement Reward Heading", rewardRoot, new Vector2(820, 34), new Vector2(0, 200), 23, ink).text =
                failure ? "抢救物资 · 已扣除损失" : hasLoot ? "本次战利品 · 已按保护率折算" : "本次暂无新增战利品";
            RectTransform main = CreateRect("Settlement Featured Reward", rewardRoot, new Vector2(830, 215), new Vector2(0, 63));
            ItemIconArt featuredIcon = SettlementItemIcon(ids[featured]);
            if (featuredIcon.IsAvailable)
                CreateItemIcon("Settlement Featured Icon " + ids[featured], main, featuredIcon, new Vector2(-326, 0), 104f);
            SettlementText("Settlement Featured Name", main, new Vector2(610, 40), new Vector2(65, 70), 27, ink).text = names[featured];
            Text mainValue = SettlementText("Settlement Reward " + ids[featured], main, new Vector2(610, 106),
                new Vector2(65, featured == 0 ? -5 : -18), rewards[featured] >= 1000000 ? 62 : 92, hasLoot ? accent : muted);
            if (featured == 0)
                SettlementText("Settlement Featured Detail", main, new Vector2(610, 28), new Vector2(65, -79), 18, muted).text = "船长资金";
            SettlementRule(main, "Settlement Featured Underline", new Vector2(800, 1), new Vector2(0, -112));
            tracks.Add(main); delays.Add(0.55f); values.Add(mainValue); targets.Add(rewards[featured]); countDelays.Add(0.65f);
            int slot = 0;
            for (int i = 0; i < rewards.Length; i++)
            {
                if (i == featured) continue;
                RectTransform tile = CreateRect("Settlement Reward Tile " + ids[i], rewardRoot, new Vector2(260, 133), new Vector2(-276 + slot * 276, -131));
                if (slot > 0) SettlementRule(tile, "Settlement Reward Divider " + ids[i], new Vector2(1, 108), new Vector2(-138, 0));
                ItemIconArt resourceIcon = SettlementItemIcon(ids[i]);
                if (resourceIcon.IsAvailable)
                    CreateItemIcon("Settlement Resource Icon " + ids[i], tile, resourceIcon, new Vector2(-91, 40), 42f);
                SettlementText("Settlement Resource " + ids[i], tile, new Vector2(resourceIcon.IsAvailable ? 164 : 246, 32),
                    new Vector2(resourceIcon.IsAvailable ? 16 : 0, 40), 23, ink).text = names[i];
                Text number = SettlementText("Settlement Reward " + ids[i], tile, new Vector2(254, 54), new Vector2(0, i == 0 ? -7 : -20), rewards[i] >= 1000000 ? 28 : 43, rewards[i] > 0 ? accent : muted);
                if (i == 0)
                    SettlementText("Settlement Resource Hint Gold", tile, new Vector2(246, 26), new Vector2(0, -49), 16, muted).text = "船长资金";
                tracks.Add(tile); delays.Add(0.90f + slot * 0.16f); values.Add(number); targets.Add(rewards[i]); countDelays.Add(1.0f + slot * 0.16f); slot++;
            }

            RectTransform ribbon = CreateRect("Settlement Protection Seal", card, new Vector2(1630, 57), new Vector2(0, -249));
            CreateImage("Settlement Protection Tint", ribbon, ribbon.sizeDelta, Vector2.zero,
                failure ? new Color(.48f, .26f, .18f, .09f) : new Color(.13f, .35f, .29f, .10f)).raycastTarget = false;
            settlementProtectionLabel = SettlementText("Settlement Protection", ribbon, new Vector2(1500, 40), Vector2.zero, 21, accent);
            settlementProtectionLabel.text = SettlementProtectionText(result);
            tracks.Add(ribbon); delays.Add(2.05f);
            RectTransform metrics = CreateRect("Settlement Metrics", card, new Vector2(1590, 66), new Vector2(0, -315));
            SettlementText("Settlement Distance", metrics, new Vector2(480, 64), new Vector2(-525, 0), 23, ink).text = "航程  " + result.DistanceNm.ToString("N0") + " 海里\n用时 " + result.Hours.ToString("0.0") + " 小时";
            SettlementText("Settlement Exploration", metrics, new Vector2(480, 64), Vector2.zero, 23, ink).text = "探索  " + result.Coverage.ToString("0") + "%";
            SettlementText("Settlement Events", metrics, new Vector2(480, 64), new Vector2(525, 0), 23, ink).text = "已处理事件  " + result.CompletedEvents + " / " + result.TotalEvents + (result.Outcome == VoyageOutcome.ArrivedAtPort ? "\n到港后可再次出航" : "");
            bool canDrawChart = !previewSample && !failure && session.CanDrawSeaChart;
            BuildSettlementStorageOption(card, result, previewSample);
            if (canDrawChart)
            {
                Button draw = CreateButton("Draw Sea Chart", card, new Vector2(330, 64), new Vector2(-8, -410), PanelRaised, ink, 22);
                MarketShopSkin.ApplyLightButton(draw);
                draw.GetComponentInChildren<Text>().text = pendingDrawnChart == null ? "航海士 · 绘制海图" : "海图已绘制";
                CenterSettlementText(draw.GetComponentInChildren<Text>());
                draw.interactable = pendingDrawnChart == null;
                settlementDrawChartButton = draw;
                draw.onClick.AddListener(() =>
                {
                    if (previewSample || HasActiveSettlementChest || draw == null || !draw.gameObject.activeInHierarchy ||
                        !ReferenceEquals(session.Settlement, result) || !ReferenceEquals(displayedSettlement, result)) return;
                    DrawCurrentSeaChart();
                });
            }
            Button finish = CreateButton("Return To Port", card, new Vector2(600, 72), new Vector2(510, -410), Gold, ink, 28);
            finish.GetComponentInChildren<Text>().text = "确认结算 · 返回港口";
            CenterSettlementText(finish.GetComponentInChildren<Text>());
            finish.onClick.AddListener(() =>
            {
                if (previewSample || HasActiveSettlementChest || session.Phase != CoreDemoPhase.Settlement || !ReferenceEquals(session.Settlement, result)) return;
                ConfirmVoyageSettlement();
            });
            Button skip = CreateButton("Skip Settlement Reveal", card, new Vector2(162, 40), new Vector2(768, 419), PanelRaised, ink, 17);
            MarketShopSkin.ApplyTextButton(skip);
            skip.GetComponentInChildren<Text>().text = "跳过演出";
            CenterSettlementText(skip.GetComponentInChildren<Text>());
            var reveal = card.gameObject.AddComponent<SettlementRevealController>();
            reveal.Configure(tracks.ToArray(), delays.ToArray(), values.ToArray(), targets.ToArray(), countDelays.ToArray(), sparks, skip,
                celebrate, PlaySfx, false,
                SettlementRevealAudio.OutcomeCue(result), result.TreasureCompleted);
            skip.onClick.AddListener(reveal.CompleteImmediately);
            BuildSettlementChestIntro(result, chestPlan, card, harbor, skip, playChest);
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
            RectTransform panel = CreateRect(name, parent, size, position);
            Image paper = panel.gameObject.AddComponent<Image>();
            paper.color = Color.white;
            MarketShopSkin.ApplyPanel(paper);
            // Match the fine timber/brass edge of the port's current reading panels.
            paper.pixelsPerUnitMultiplier = name == "Settlement Outer Frame" ? 2f : 4f;
            paper.raycastTarget = false;
            return panel;
        }

        private void SettlementRule(Transform parent, string name, Vector2 size, Vector2 position)
        {
            CreateImage(name, parent, size, position, new Color(.45f, .32f, .18f, .28f)).raycastTarget = false;
        }

        private static Rect SettlementPortraitUv(Texture2D texture, Vector2 size)
        {
            if (texture == null) return new Rect(0, 0, 1, 1);
            float imageAspect = texture.width / (float)texture.height;
            float viewAspect = size.x / size.y;
            if (viewAspect >= imageAspect)
            {
                float height = imageAspect / viewAspect;
                return new Rect(0, (1f - height) * .65f, 1, height);
            }
            float width = viewAspect / imageAspect;
            return new Rect((1f - width) * .5f, 0, width, 1);
        }

        private Text SettlementText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, alignment, color);
            if (name == "Settlement Title" || name == "Settlement Featured Name" || name == "Settlement Reward Heading")
                GameTypography.Apply(text, fontSize, FontStyle.Bold);
            text.color = color;
            CenterSettlementText(text);
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false; text.raycastTarget = false;
            return text;
        }

        private static void CenterSettlementText(Text text)
        {
            text.alignByGeometry = true;
            text.gameObject.AddComponent<SettlementTextCentering>();
        }
    }
}

