using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const string PortInvestmentKeyPrefix = "ShatteredSail.PortInvestment.v1.";
        private bool investmentSupplyPanelOpen;
        private object investmentQuoteToken;

        public static string PortInvestmentKey(string portName)
        {
            return PortInvestmentKeyPrefix + (portName ?? string.Empty).Trim();
        }

        private int PortInvestmentLevel(string portName)
        {
            if (!Application.isPlaying || editorPreviewBuilding)
                return 0;
            return Mathf.Clamp(PlayerPrefs.GetInt(PortInvestmentKey(portName), 0), 0, PortInvestmentCatalog.MaxLevel);
        }

        private float PortInvestmentRarityShift(string portName)
        {
            return PortInvestmentCatalog.RarityThresholdShift(PortInvestmentLevel(portName));
        }

        private bool CanAffordPortInvestment(PortInvestmentCost cost)
        {
            if (session == null || session.ProfileGold < cost.Gold ||
                session.GetPortInvestmentMaterialAmount("wood") < cost.Wood ||
                session.GetPortInvestmentMaterialAmount("iron") < cost.Iron ||
                session.GetPortInvestmentMaterialAmount(PortMarketCatalog.Key(cost.RequiredItem)) < cost.RequiredQuantity)
                return false;
            if (cost.RareMaterials != null)
                foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                    if (session.GetPortInvestmentMaterialAmount(rare.Key) < rare.Quantity) return false;
            return true;
        }

        private static string FormatInvestmentCost(PortInvestmentCost cost)
        {
            List<string> parts = new List<string>();
            if (cost.Gold > 0) parts.Add(cost.Gold + " 金币");
            if (cost.Wood > 0) parts.Add(cost.Wood + " 木材");
            if (cost.Iron > 0) parts.Add(cost.Iron + " 铁矿");
            if (cost.RequiredQuantity > 0) parts.Add(cost.RequiredQuantity + " " + PortMarketCatalog.Name(cost.RequiredItem));
            if (cost.RareMaterials != null)
                foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                    parts.Add(rare.Quantity + " " + CoreWarehouseItemCatalog.Find(rare.Key).Name);
            return parts.Count == 0 ? "无需投入" : string.Join(" + ", parts);
        }

        private string InvestmentShortfall(PortInvestmentCost cost)
        {
            List<string> missing = new List<string>();
            if (session.ProfileGold < cost.Gold) missing.Add("金币 " + (cost.Gold - session.ProfileGold));
            long wood = session.GetPortInvestmentMaterialAmount("wood");
            long iron = session.GetPortInvestmentMaterialAmount("iron");
            long required = session.GetPortInvestmentMaterialAmount(PortMarketCatalog.Key(cost.RequiredItem));
            if (wood < cost.Wood) missing.Add("木材 " + (cost.Wood - wood));
            if (iron < cost.Iron) missing.Add("铁矿 " + (cost.Iron - iron));
            if (required < cost.RequiredQuantity) missing.Add(PortMarketCatalog.Name(cost.RequiredItem) + " " + (cost.RequiredQuantity - required));
            if (cost.RareMaterials != null)
                foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                {
                    long owned = session.GetPortInvestmentMaterialAmount(rare.Key);
                    if (owned < rare.Quantity) missing.Add(CoreWarehouseItemCatalog.Find(rare.Key).Name + " " + (rare.Quantity - owned));
                }
            return string.Join("、", missing);
        }

        private void BuildPortInvestmentFacility()
        {
            investmentSupplyPanelOpen = false;
            investmentQuoteToken = new object();

            // The investment screen sits over a busy port illustration. Use a light
            // parchment surface for the information columns and reserve dark timber
            // strips for the tier rows so every label has a stable, high-contrast
            // reading surface in both the editor preview and runtime.
            Color parchmentSurface = new Color(0.88f, 0.78f, 0.62f, 0.96f);
            Color darkInk = Html("#24170F");
            Color mutedInk = Html("#5A3A24");
            Color tierSurface = new Color(0.16f, 0.08f, 0.03f, 0.92f);
            Color completedTierSurface = new Color(0.08f, 0.30f, 0.19f, 0.94f);

            RectTransform panel = CreateFixedPanel("Port Investment Panel", contentRoot,
                new Vector2(1820f, 650f), new Vector2(0f, -55f), Color.clear);
            RectTransform summary = CreateFixedPanel("Port Investment Summary", panel,
                new Vector2(630f, 548f), new Vector2(-520f, -10f), parchmentSurface);
            RectTransform ledger = CreateFixedPanel("Port Investment Ledger", panel,
                new Vector2(850f, 570f), new Vector2(420f, -10f), parchmentSurface);
            summary.GetComponent<Image>().raycastTarget = false;
            ledger.GetComponent<Image>().raycastTarget = false;

            int level = PortInvestmentLevel(session.CurrentPort);
            PortMarketItem requiredItem = PortInvestmentCatalog.RequiredItemFor(session.CurrentPort);
            string requiredName = PortMarketCatalog.Name(requiredItem);
            float currentChance = PortInvestmentCatalog.HighQualityChance(level);
            Text summaryTitle = CreateText("Port Investment Summary Title", summary,
                new Vector2(570f, 44f), new Vector2(0f, 220f), 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, darkInk);
            summaryTitle.text = PortInvestmentCatalog.NameFor(session.CurrentPort);
            AddSceneLabelOutline(summaryTitle);
            Text summaryPort = CreateText("Port Investment Port", summary,
                new Vector2(570f, 28f), new Vector2(0f, 184f), 17, FontStyle.Bold,
                TextAnchor.MiddleCenter, mutedInk);
            summaryPort.text = session.CurrentPort + " · 港口投资建设";
            AddSceneLabelOutline(summaryPort);
            Text levelText = CreateText("Port Investment Level", summary,
                new Vector2(570f, 42f), new Vector2(0f, 132f), 26, FontStyle.Bold,
                TextAnchor.MiddleCenter, darkInk);
            levelText.text = "建设等级  Lv." + level + " / " + PortInvestmentCatalog.MaxLevel;
            AddSceneLabelOutline(levelText);
            Text chance = CreateText("Port Investment Quality Chance", summary,
                new Vector2(540f, 86f), new Vector2(0f, 58f), 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, darkInk);
            chance.text = "新解锁部件稀有及以上 " + (currentChance * 100f).ToString("0.0") + "%\n" +
                "每月部件 " + PortComponentShopCatalog.StockCount(level) + " 件（基础 6 件）";
            chance.horizontalOverflow = HorizontalWrapMode.Wrap;
            chance.verticalOverflow = VerticalWrapMode.Truncate;
            chance.resizeTextForBestFit = false;
            chance.resizeTextMinSize = 16;
            chance.resizeTextMaxSize = 20;
            AddSceneLabelOutline(chance);
            Text resources = CreateText("Port Investment Resources", summary,
                new Vector2(560f, 112f), new Vector2(0f, -46f), 16, FontStyle.Normal,
                TextAnchor.MiddleCenter, mutedInk);
            resources.text = "可用资源（本港仓储＋随船货物）\n金币 " + session.ProfileGold +
                "　木材 " + session.GetPortInvestmentMaterialAmount("wood") +
                "　铁矿 " + session.GetPortInvestmentMaterialAmount("iron") +
                "\n本港专需：" + requiredName + " " + session.GetPortInvestmentMaterialAmount(PortMarketCatalog.Key(requiredItem));
            var rareInventory = new List<string>();
            foreach (CorePortInvestmentRequirement rare in PortInvestmentCatalog.CostForLevel(session.CurrentPort,
                PortInvestmentCatalog.MaxLevel).RareMaterials)
                rareInventory.Add(CoreWarehouseItemCatalog.Find(rare.Key).Name + " " + session.GetPortInvestmentMaterialAmount(rare.Key));
            resources.text += "\n高阶珍稀：" + string.Join("　", rareInventory);
            resources.horizontalOverflow = HorizontalWrapMode.Wrap;
            resources.verticalOverflow = VerticalWrapMode.Truncate;
            resources.resizeTextForBestFit = false;
            resources.resizeTextMinSize = 13;
            resources.resizeTextMaxSize = 16;
            AddSceneLabelOutline(resources);
            Text benefit = CreateText("Port Investment Benefit", summary,
                new Vector2(560f, 64f), new Vector2(0f, -135f), 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, darkInk);
            benefit.text = "普通货物月额度 " + PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Wood, level) +
                (level < PortInvestmentCatalog.MaxLevel ? " → " + PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Wood, level + 1) : "（已满级）") +
                "；升级增加实际库存。\n每级部件 +2 件；本月已购保留，所有港口每月首日统一补货。";
            benefit.horizontalOverflow = HorizontalWrapMode.Wrap;
            benefit.verticalOverflow = VerticalWrapMode.Truncate;
            benefit.resizeTextForBestFit = false;
            benefit.resizeTextMinSize = 12;
            benefit.resizeTextMaxSize = 15;
            AddSceneLabelOutline(benefit);
            Text sourcing = CreateText("Port Investment Sourcing", summary,
                new Vector2(560f, 82f), new Vector2(0f, -218f), 16, FontStyle.Bold,
                TextAnchor.MiddleCenter, darkInk);
            sourcing.text = requiredName + "在本港价格较高，建议到" + PortInvestmentCatalog.SuggestedSourcePort(session.CurrentPort) +
                "采购。\n分批运回本港收藏室，可累计备齐建设材料。";
            sourcing.horizontalOverflow = HorizontalWrapMode.Wrap;
            sourcing.resizeTextForBestFit = false;
            sourcing.resizeTextMinSize = 13;
            sourcing.resizeTextMaxSize = 16;
            AddSceneLabelOutline(sourcing);

            Text ledgerTitle = CreateText("Port Investment Ledger Title", ledger,
                new Vector2(780f, 40f), new Vector2(0f, 242f), 25, FontStyle.Bold,
                TextAnchor.MiddleCenter, darkInk);
            ledgerTitle.text = "港口建设计划";
            AddSceneLabelOutline(ledgerTitle);
            Text ledgerHint = CreateText("Port Investment Ledger Hint", ledger,
                new Vector2(760f, 34f), new Vector2(0f, 205f), 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, mutedInk);
            ledgerHint.text = "投入金币、木材、铁矿与" + requiredName + "；Lv.4–5 追加珍稀捐献\n先扣本港仓储，不足使用随船货物；其他港库存不参与";
            ledgerHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            ledgerHint.verticalOverflow = VerticalWrapMode.Truncate;
            ledgerHint.resizeTextForBestFit = false;
            ledgerHint.resizeTextMinSize = 12;
            ledgerHint.resizeTextMaxSize = 15;
            AddSceneLabelOutline(ledgerHint);

            for (int tier = 1; tier <= PortInvestmentCatalog.MaxLevel; tier += 1)
            {
                float y = 150f - (tier - 1) * 62f;
                PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(session.CurrentPort, tier);
                bool completed = level >= tier;
                RectTransform row = CreateFixedPanel("Port Investment Tier " + tier, ledger,
                    new Vector2(760f, 50f), new Vector2(0f, y),
                    completed ? completedTierSurface : tierSurface);
                Text rowText = CreateText("Port Investment Tier Label " + tier, row,
                    new Vector2(720f, 42f), Vector2.zero, 17, FontStyle.Bold,
                    TextAnchor.MiddleCenter, Ink);
                rowText.text = completed
                    ? "Lv." + tier + "　已完成建设"
                    : "Lv." + tier + "　投入 " + FormatInvestmentCost(cost);
                rowText.horizontalOverflow = HorizontalWrapMode.Wrap;
                rowText.verticalOverflow = VerticalWrapMode.Truncate;
                rowText.resizeTextForBestFit = false;
                rowText.resizeTextMinSize = 13;
                rowText.resizeTextMaxSize = 17;
                AddSceneLabelOutline(rowText);
            }

            int nextLevel = level + 1;
            PortInvestmentCost nextCost = nextLevel <= PortInvestmentCatalog.MaxLevel
                ? PortInvestmentCatalog.CostForLevel(session.CurrentPort, nextLevel)
                : default;
            bool canUpgrade = nextLevel <= PortInvestmentCatalog.MaxLevel && CanAffordPortInvestment(nextCost);
            Button upgrade = CreateButton("Confirm Port Investment", ledger,
                new Vector2(360f, 54f), new Vector2(-185f, -205f), canUpgrade ? Gold : PanelRaised,
                canUpgrade ? DeepSea : Ink, 18);
            Text upgradeLabel = upgrade.GetComponentInChildren<Text>();
            upgradeLabel.text = nextLevel <= PortInvestmentCatalog.MaxLevel
                ? "升级至 Lv." + nextLevel + " · " + FormatInvestmentCost(nextCost)
                : "港口已达到最高等级";
            upgradeLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            upgradeLabel.verticalOverflow = VerticalWrapMode.Truncate;
            upgradeLabel.resizeTextForBestFit = false;
            upgradeLabel.resizeTextMinSize = 14;
            upgradeLabel.resizeTextMaxSize = 18;
            AddSceneLabelOutline(upgradeLabel);
            upgrade.interactable = Application.isPlaying && !editorPreviewBuilding && canUpgrade;
            string quotedPort = session.CurrentPort;
            int quotedLevel = level;
            object quoteToken = investmentQuoteToken;
            CoreVoyageSession quotedSession = session;
            upgrade.onClick.AddListener(() =>
            {
                if (session == quotedSession && investmentQuoteToken == quoteToken)
                    InvestInCurrentPort(quotedPort, quotedLevel);
            });

            Button market = CreateButton("Open Port Market", ledger,
                new Vector2(168f, 54f), new Vector2(100f, -205f), PanelRaised, Ink, 16);
            Text marketLabel = market.GetComponentInChildren<Text>();
            marketLabel.text = "前往港口市场";
            marketLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            marketLabel.verticalOverflow = VerticalWrapMode.Truncate;
            AddSceneLabelOutline(marketLabel);
            market.onClick.AddListener(() =>
            {
                investmentSupplyPanelOpen = false;
                activePortFacility = PortFacilityArtCatalog.MarketFacilityIndex;
                portNotice = "已进入港口市场；本港投资等级会提升每月库存与购买额度。";
                Render();
            });

            Button warehouse = CreateButton("Open Investment Warehouse", ledger,
                new Vector2(168f, 54f), new Vector2(286f, -205f), PanelRaised, Ink, 16);
            warehouse.GetComponentInChildren<Text>().text = "本港仓储";
            warehouse.onClick.AddListener(() =>
            {
                activePortFacility = 0;
                warehouseViewedSession = session;
                warehouseViewedPort = CurrentWarehousePort;
                warehouseStorageOpen = true;
                portWarehouseNotice = string.Empty;
                Render();
            });

            Text note = CreateText("Port Investment Notice", ledger,
                new Vector2(740f, 40f), new Vector2(0f, -260f), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, darkInk);
            note.text = nextLevel > PortInvestmentCatalog.MaxLevel
                ? "本港建设已完成，部件商店稀有及以上概率达到 " + (currentChance * 100f).ToString("0.0") + "%。"
                : canUpgrade
                    ? "确认后一次扣除全部列出的投入；其他港口库存不参与本港建设。"
                    : "还缺少：" + InvestmentShortfall(nextCost);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.verticalOverflow = VerticalWrapMode.Truncate;
            note.resizeTextForBestFit = false;
            note.resizeTextMinSize = 11;
            note.resizeTextMaxSize = 14;
            AddSceneLabelOutline(note);
            if (nextLevel <= PortInvestmentCatalog.MaxLevel)
            {
                note.text = "本次投入：" + FormatInvestmentCost(nextCost) + "\n" + note.text;
                upgradeLabel.text = "升级至 Lv." + nextLevel;
            }
            ApplyPortInvestmentReadingLayout(panel, summary, ledger);
        }

        private static void ApplyPortInvestmentReadingLayout(RectTransform panel, RectTransform summary, RectTransform ledger)
        {
            panel.sizeDelta = new Vector2(1820, 730); panel.anchoredPosition = new Vector2(0, -45);
            summary.sizeDelta = new Vector2(720, 700); summary.anchoredPosition = new Vector2(-520, -5);
            ledger.sizeDelta = new Vector2(1000, 700); ledger.anchoredPosition = new Vector2(360, -5);
            foreach (RectTransform board in new[] { summary, ledger })
            {
                Text title = MaintenanceText(board, board == summary ? "Port Investment Summary Title" : "Port Investment Ledger Title");
                SetMaintenanceReadingText(title, 32, true);
                title.rectTransform.sizeDelta = new Vector2(board.rect.width - 40, 72);
                title.rectTransform.anchoredPosition = new Vector2(0, 300);
            }
            ScrollRect summaryScroll = MaintenanceReadingScroll("Investment Summary Reading Scroll", summary, new Vector2(680, 590), new Vector2(0, -41));
            float top = 0;
            foreach (string name in new[] { "Port Investment Port", "Port Investment Level", "Port Investment Quality Chance",
                "Port Investment Resources", "Port Investment Benefit", "Port Investment Sourcing" })
                top = FlowMaintenanceText(MaintenanceText(summary, name), summaryScroll.content, top,
                    name == "Port Investment Port" ? 18 : 22);
            FinishMaintenanceScroll(summaryScroll, top);

            ScrollRect plans = MaintenanceReadingScroll("Investment Plan Reading Scroll", ledger, new Vector2(960, 340), new Vector2(0, 84));
            top = FlowMaintenanceText(MaintenanceText(ledger, "Port Investment Ledger Hint"), plans.content, 0, 18);
            for (int tier = 1; tier <= PortInvestmentCatalog.MaxLevel; tier++)
            {
                RectTransform row = ledger.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Port Investment Tier " + tier);
                row.SetParent(plans.content, false); row.anchorMin = row.anchorMax = row.pivot = new Vector2(.5f, 1f);
                row.sizeDelta = new Vector2(plans.content.rect.width - 8, 1000);
                Text text = MaintenanceText(row, "Port Investment Tier Label " + tier);
                float height = Mathf.Max(64, FlowMaintenanceText(text, row, 8, 22));
                row.sizeDelta = new Vector2(row.sizeDelta.x, height); row.anchoredPosition = new Vector2(0, -top);
                top += height + 12;
            }
            FinishMaintenanceScroll(plans, top);
            // Current cost and shortfall remain separate from the plans. Large
            // inventories may scroll here without moving the transaction buttons.
            ScrollRect notice = MaintenanceReadingScroll("Investment Quote Reading Scroll", ledger, new Vector2(960, 132), new Vector2(0, -158));
            FinishMaintenanceScroll(notice, FlowMaintenanceText(MaintenanceText(ledger, "Port Investment Notice"), notice.content, 0, 18));
            string[] actions = { "Confirm Port Investment", "Open Port Market", "Open Investment Warehouse" };
            float[] widths = { 420, 240, 210 }, positions = { -250, 110, 355 };
            for (int i = 0; i < actions.Length; i++)
            {
                Button button = ledger.GetComponentsInChildren<Button>(true).Single(item => item.name == actions[i]);
                RectTransform rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(widths[i], 96); rect.anchoredPosition = new Vector2(positions[i], -280);
                Text text = button.GetComponentInChildren<Text>(); SetMaintenanceReadingText(text, 22);
                text.rectTransform.sizeDelta = new Vector2(widths[i] - 20, 88);
            }
        }

        private void InvestInCurrentPort(string quotedPort, int quotedLevel)
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null || session.Phase != CoreDemoPhase.Port ||
                activePortFacility != 7 || session.CurrentPort != quotedPort || PortInvestmentLevel(quotedPort) != quotedLevel)
                return;
            int current = PortInvestmentLevel(session.CurrentPort);
            int next = current + 1;
            if (next > PortInvestmentCatalog.MaxLevel)
            {
                portNotice = "本港建设已达到最高等级。";
                Render();
                return;
            }
            PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(session.CurrentPort, next);
            if (!CanAffordPortInvestment(cost))
            {
                portNotice = "建设失败：还缺少 " + InvestmentShortfall(cost) + "。";
                Render();
                return;
            }
            if (!session.TrySpendPortInvestment(cost.Gold, cost.Wood, cost.Iron,
                PortMarketCatalog.Key(cost.RequiredItem), cost.RequiredQuantity, cost.RareMaterials))
            {
                portNotice = "建设失败：材料或金币已变化，请重新核对。";
                Render();
                return;
            }
            PlayerPrefs.SetInt(PortInvestmentKey(session.CurrentPort), next);
            ResetComponentShopView();
            PlaySfx("investment_build", 0.58f);
            SaveProgress();
            portNotice = PortInvestmentCatalog.NameFor(session.CurrentPort) + "已升级至 Lv." + next +
                "；本港部件商店稀有及以上概率提升至 " + (PortInvestmentCatalog.HighQualityChance(next) * 100f).ToString("0.0") + "%。";
            Render();
        }
    }
}
