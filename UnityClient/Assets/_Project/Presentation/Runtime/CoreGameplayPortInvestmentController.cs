using System;
using System.Collections.Generic;
using System.Globalization;
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
        private int investmentReturnFacility = -1;
        private CoreVoyageSession investmentReturnSession;
        private string investmentReturnPort;

        private bool HasInvestmentReturn => investmentReturnFacility == activePortFacility &&
            session != null && session.Phase == CoreDemoPhase.Port &&
            ReferenceEquals(investmentReturnSession, session) && investmentReturnPort == session.CurrentPort;

        private void OpenFacilityFromInvestment(int facility)
        {
            EnterPortFacility(facility);
            investmentReturnFacility = facility;
            investmentReturnSession = session;
            investmentReturnPort = session.CurrentPort;
        }

        private void ClearInvestmentReturn()
        {
            investmentReturnFacility = -1;
            investmentReturnSession = null;
            investmentReturnPort = null;
        }

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
            Color darkInk = Html("#332116");
            Color mutedInk = Html("#705039");
            Color warmInk = Html("#F7E3B4");
            Color shortfallInk = Html("#A13224");
            Rect sceneBounds = PortInvestmentSceneLayout.Scene;
            Rect ledgerBounds = PortInvestmentSceneLayout.Ledger;
            Rect benefitBounds = PortInvestmentSceneLayout.Benefits;
            Rect plaqueBounds = PortInvestmentSceneLayout.ProjectPlaque;

            // The painting already contains the paper and timber. These clear
            // roots register live controls without hiding any of the harbour.
            RectTransform panel = CreateInvestmentRegion("Port Investment Panel", contentRoot, sceneBounds, sceneBounds);
            RectTransform summary = CreateInvestmentRegion("Port Investment Summary", panel, benefitBounds, sceneBounds);
            RectTransform ledger = CreateInvestmentRegion("Port Investment Ledger", panel, ledgerBounds, sceneBounds);
            RectTransform plaque = CreateInvestmentRegion("Port Investment Project Plaque", panel, plaqueBounds, sceneBounds);

            int level = PortInvestmentLevel(session.CurrentPort);
            int nextLevel = level + 1;
            bool hasNextLevel = nextLevel <= PortInvestmentCatalog.MaxLevel;
            int comparisonLevel = Mathf.Min(nextLevel, PortInvestmentCatalog.MaxLevel);
            CreateInvestmentText("Project Name", plaque, new Rect(138f, 126f, 278f, 27f), plaqueBounds,
                session.CurrentPort + " · " + PortInvestmentCatalog.NameFor(session.CurrentPort), 25, warmInk);
            CreateInvestmentText("Project Caption", plaque, new Rect(138f, 160f, 87f, 25f), plaqueBounds,
                "本港建设", 19, warmInk);
            CreateInvestmentText("Current Level", plaque, new Rect(228f, 160f, 65f, 25f), plaqueBounds,
                "Lv." + level, 22, warmInk);
            CreateInvestmentText("Next Level", plaque, new Rect(326f, 160f, 65f, 25f), plaqueBounds,
                "Lv." + comparisonLevel, 22, warmInk);
            RectTransform levelArrow = CreateInvestmentRegion("Port Investment Comparison Arrow", plaque,
                new Rect(299f, 164f, 21f, 16f), plaqueBounds);
            BuildInvestmentArrowStroke(levelArrow, "Shaft", new Vector2(21f, 2.5f), new Vector2(-1f, 0f), 0f, warmInk);
            BuildInvestmentArrowStroke(levelArrow, "Upper", new Vector2(10f, 2.5f), new Vector2(6f, 3f), -40f, warmInk);
            BuildInvestmentArrowStroke(levelArrow, "Lower", new Vector2(10f, 2.5f), new Vector2(6f, -3f), 40f, warmInk);
            BuildInvestmentBenefits(summary, benefitBounds, level, comparisonLevel, warmInk);

            PortInvestmentCost nextCost = hasNextLevel
                ? PortInvestmentCatalog.CostForLevel(session.CurrentPort, nextLevel)
                : default;
            CreateInvestmentText("Ledger Title", ledger, new Rect(1170f, 212f, 376f, 42f), ledgerBounds,
                hasNextLevel ? "升级至 Lv." + nextLevel + " 所需材料" : "港口建设已完成", 29, darkInk);
            if (hasNextLevel)
                BuildInvestmentRequirements(ledger, nextCost, darkInk, shortfallInk);
            else
            {
                CreateInvestmentText("Completed", ledger, new Rect(1170f, 368f, 376f, 55f), ledgerBounds,
                    "已达到最高投资等级", 28, darkInk);
                CreateInvestmentText("Completed Detail", ledger, new Rect(1175f, 426f, 366f, 75f), ledgerBounds,
                    "本港已享有全部建设收益\n感谢您为港口发展作出的贡献", 21, mutedInk, wrap: true);
            }
            bool canUpgrade = hasNextLevel && CanAffordPortInvestment(nextCost);

            Button upgrade = CreateInvestmentButton("Confirm Port Investment", ledger,
                new Rect(1167f, 655f, 379f, 46f), ledgerBounds, 24,
                !hasNextLevel ? "港口已达到最高等级" : canUpgrade ? "升级至 Lv." + nextLevel : "材料不足");
            if (hasNextLevel && !canUpgrade)
                upgrade.GetComponentInChildren<Text>().color = Html("#F06B58");
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

            Button market = CreateInvestmentButton("Open Port Market", ledger,
                new Rect(1167f, 711f, 184f, 35f), ledgerBounds, 19, "前往港口市场");
            market.onClick.AddListener(() =>
            {
                OpenFacilityFromInvestment(PortFacilityArtCatalog.MarketFacilityIndex);
                portNotice = "已进入港口市场；本港投资等级会提升每月库存与购买额度。";
                Render();
            });
            Button warehouse = CreateInvestmentButton("Open Investment Warehouse", ledger,
                new Rect(1361f, 711f, 185f, 35f), ledgerBounds, 19, "本港仓储");
            warehouse.onClick.AddListener(() =>
            {
                OpenFacilityFromInvestment(PortFacilityArtCatalog.WarehouseFacilityIndex);
                warehouseViewedSession = session;
                warehouseViewedPort = CurrentWarehousePort;
                warehouseStorageOpen = true;
                portWarehouseNotice = string.Empty;
                Render();
            });
        }

        private RectTransform CreateInvestmentRegion(string name, Transform parent, Rect bounds, Rect parentBounds)
        {
            RectTransform region = CreateFixedPanel(name, parent, PortInvestmentSceneLayout.Size(bounds.size),
                PortInvestmentSceneLayout.RelativePosition(bounds, parentBounds), Color.clear);
            region.GetComponent<Image>().raycastTarget = false;
            return region;
        }

        private Text CreateInvestmentText(string name, Transform parent, Rect bounds, Rect parentBounds,
            string value, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter,
            bool wrap = false, int minimumFontSize = 17)
        {
            Text label = CreateText("Port Investment " + name, parent, PortInvestmentSceneLayout.Size(bounds.size),
                PortInvestmentSceneLayout.RelativePosition(bounds, parentBounds), fontSize, FontStyle.Bold, alignment, color);
            label.text = value;
            label.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Min(fontSize, minimumFontSize);
            label.resizeTextMaxSize = fontSize;
            // With one-line fit, preserve exact balances rather than abbreviating
            // old saves containing four decimal places or combined long totals.
            if (!wrap) FitInvestmentSingleLine(label, fontSize, minimumFontSize);
            AddSceneLabelOutline(label);
            return label;
        }

        private static void FitInvestmentSingleLine(Text label, int preferredSize, int minimumSize)
        {
            label.resizeTextForBestFit = false;
            label.fontSize = preferredSize;
            while (label.fontSize > minimumSize && label.preferredWidth > label.rectTransform.rect.width)
                label.fontSize--;
        }

        private Button CreateInvestmentButton(string name, Transform parent, Rect bounds, Rect parentBounds,
            int fontSize, string caption)
        {
            Button button = CreateButton(name, parent, PortInvestmentSceneLayout.Size(bounds.size),
                PortInvestmentSceneLayout.RelativePosition(bounds, parentBounds), PanelRaised, Ink, fontSize);
            Text label = button.GetComponentInChildren<Text>();
            label.text = caption;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            FitInvestmentSingleLine(label, fontSize, 17);
            AddSceneLabelOutline(label);
            return button;
        }

        private void BuildInvestmentRequirements(RectTransform ledger, PortInvestmentCost cost, Color ink, Color shortfallInk)
        {
            Rect ledgerBounds = PortInvestmentSceneLayout.Ledger;
            var requirements = new List<(string Key, string Name, int Quantity)>();
            if (cost.Gold > 0) requirements.Add(("gold", "金币", cost.Gold));
            if (cost.Wood > 0) requirements.Add(("wood", "木材", cost.Wood));
            if (cost.Iron > 0) requirements.Add(("iron", "铁矿", cost.Iron));
            if (cost.RequiredQuantity > 0)
                requirements.Add((PortMarketCatalog.Key(cost.RequiredItem), PortMarketCatalog.Name(cost.RequiredItem), cost.RequiredQuantity));
            if (cost.RareMaterials != null)
                foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                    requirements.Add((rare.Key, CoreWarehouseItemCatalog.Find(rare.Key).Name, rare.Quantity));

            CreateInvestmentText("Material Header", ledger, new Rect(1210f, 274f, 109f, 25f), ledgerBounds,
                "材料", 19, ink, TextAnchor.MiddleLeft);
            CreateInvestmentText("Quantity Header", ledger, new Rect(1320f, 274f, 226f, 25f), ledgerBounds,
                "拥有 / 需求", 19, ink, TextAnchor.MiddleRight);
            float spacing = requirements.Count <= 4 ? 66f : 49f;
            float firstY = requirements.Count <= 4 ? 331f : 306f;
            for (int index = 0; index < requirements.Count; index++)
            {
                var requirement = requirements[index];
                float top = firstY + index * spacing;
                Rect rowBounds = new Rect(1167f, top, 379f, 47f);
                RectTransform row = CreateInvestmentRegion("Port Investment Requirement " + requirement.Key,
                    ledger, rowBounds, ledgerBounds);
                decimal owned = requirement.Key == "gold" ? session.ProfileGold :
                    session.GetPortInvestmentMaterialAmount(requirement.Key);
                bool enough = owned >= requirement.Quantity;
                Color quantityInk = enough ? ink : shortfallInk;
                ItemIconArt art = requirement.Key == "gold" ? ItemIconCatalog.ForSpecial(requirement.Key) :
                    ItemIconCatalog.ForCargo(requirement.Key);
                Vector2 iconPosition = PortInvestmentSceneLayout.Position(new Vector2(1185f, top + 21f)) -
                    PortInvestmentSceneLayout.Position(rowBounds.center);
                CreateItemIcon("Port Investment Requirement Icon " + requirement.Key, row,
                    art, iconPosition, PortInvestmentSceneLayout.Size(new Vector2(36f, 36f)).x);
                CreateInvestmentText("Requirement Name " + requirement.Key, row,
                    new Rect(1210f, top + 1f, 109f, 36f), rowBounds, requirement.Name, 23, ink, TextAnchor.MiddleLeft);
                CreateInvestmentText("Requirement Quantity " + requirement.Key, row,
                    new Rect(1320f, top + 1f, 226f, 29f), rowBounds,
                    owned.ToString("0.####", CultureInfo.InvariantCulture) + "/" + requirement.Quantity.ToString(CultureInfo.InvariantCulture),
                    25, quantityInk, TextAnchor.MiddleRight, minimumFontSize: 18);
                if (!enough)
                    CreateInvestmentText("Requirement Shortfall " + requirement.Key, row,
                        new Rect(1320f, top + 30f, 226f, 17f), rowBounds,
                        "缺 " + (requirement.Quantity - owned).ToString("0.####", CultureInfo.InvariantCulture),
                        16, shortfallInk, TextAnchor.MiddleRight, minimumFontSize: 16);
            }
        }

        private void BuildInvestmentArrowStroke(RectTransform parent, string name, Vector2 size,
            Vector2 position, float rotation, Color ink)
        {
            RectTransform stroke = CreateFixedPanel("Port Investment Arrow " + name, parent, size, position, Color.clear);
            stroke.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = stroke.GetComponent<Image>();
            image.sprite = null;
            image.color = ink;
            image.raycastTarget = false;
        }

        private void BuildInvestmentBenefits(RectTransform summary, Rect bounds, int currentLevel, int nextLevel, Color ink)
        {
            bool complete = currentLevel == PortInvestmentCatalog.MaxLevel;
            CreateInvestmentText("Benefit Heading", summary, new Rect(494f, 682f, 194f, 29f), bounds,
                complete ? "本港建设收益" : "本次升级收益", 21, ink);
            float chance = PortInvestmentCatalog.HighQualityChance(currentLevel) * 100f;
            float nextChance = PortInvestmentCatalog.HighQualityChance(nextLevel) * 100f;
            int components = PortComponentShopCatalog.StockCount(currentLevel);
            int nextComponents = PortComponentShopCatalog.StockCount(nextLevel);
            int quota = PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Wood, currentLevel);
            int nextQuota = PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Wood, nextLevel);
            BuildInvestmentBenefit(summary, bounds, "Quality", "新部件稀有及以上", 196f,
                chance.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                nextChance.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                "+" + (nextChance - chance).ToString("0.0", CultureInfo.InvariantCulture) + " 个百分点", ink);
            BuildInvestmentBenefit(summary, bounds, "Component", "每月部件数量", 465f,
                components + " 件", nextComponents + " 件", "+" + (nextComponents - components) + " 件", ink);
            BuildInvestmentBenefit(summary, bounds, "Quota", "普通货物月额度", 735f,
                quota.ToString(CultureInfo.InvariantCulture), nextQuota.ToString(CultureInfo.InvariantCulture),
                "+" + (nextQuota - quota), ink);
        }

        private void BuildInvestmentBenefit(RectTransform summary, Rect bounds, string key, string title,
            float left, string current, string next, string increase, Color ink)
        {
            CreateInvestmentText("Current " + key + " Label", summary, new Rect(left, 723f, 232f, 24f), bounds,
                title, 20, ink);
            CreateInvestmentText("Current " + key + " Value", summary, new Rect(left + 6f, 751f, 89f, 32f), bounds,
                current, 28, ink);
            CreateInvestmentText(key + " Arrow", summary, new Rect(left + 101f, 754f, 30f, 27f), bounds,
                "→", 24, ink);
            CreateInvestmentText("Next " + key + " Value", summary, new Rect(left + 137f, 751f, 89f, 32f), bounds,
                next, 28, Html("#FFD37D"));
            CreateInvestmentText(key + " Increase", summary, new Rect(left, 788f, 232f, 25f), bounds,
                increase, 22, Html("#FFD37D"));
        }

        private void InvestInCurrentPort(string quotedPort, int quotedLevel)
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null || session.Phase != CoreDemoPhase.Port ||
                activePortFacility != PortFacilityArtCatalog.MaintenanceFacilityIndex || session.CurrentPort != quotedPort || PortInvestmentLevel(quotedPort) != quotedLevel)
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
