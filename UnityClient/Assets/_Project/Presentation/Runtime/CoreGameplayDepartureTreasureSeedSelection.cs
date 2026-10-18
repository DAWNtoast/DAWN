using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform departureTreasureSeedPicker;
        private object departureTreasureSeedPickerIdentity;

        private void BuildDepartureTreasureSeedControl(RectTransform routePanel, RectTransform view,
            CoreVoyageSession owner, int revision)
        {
            Button picker = CreateButton("Departure Treasure Seed Picker", routePanel,
                new Vector2(510f, 36f), new Vector2(290f, 438f), PanelRaised, Gold, 18);
            picker.GetComponentInChildren<Text>().text = tutorialActive ? "藏宝海图种子 · 教学结束后可选" :
                session.SelectedTreasureMap != null ? "更换藏宝海图 · " + session.SelectedTreasureMap.Name + " ▼" :
                "藏宝海图种子 · " + session.TreasureMaps.Count + " 张可用 ▼";
            picker.interactable = !tutorialActive;
            picker.onClick.AddListener(() =>
            {
                if (!DepartureChartViewCurrent(view, owner, revision) || !picker.IsInteractable() || tutorialActive) return;
                if (UsesDepartureDockScene)
                {
                    OpenDepartureDockTreasureSelection();
                    return;
                }
                OpenDepartureTreasureSeedPicker(view, owner, revision);
            });
            if (UsesDepartureDockScene) BuildDepartureDockTreasureSelection(routePanel, view, owner, revision);
        }

        private void OpenDepartureTreasureSeedPicker(RectTransform view, CoreVoyageSession owner, int revision)
        {
            CloseDepartureTreasureSeedPicker();
            CloseDepartureChartWarning();
            object identity = departureTreasureSeedPickerIdentity = new object();
            departureTreasureSeedPicker = CreateFixedPanel("Departure Treasure Seed Selection", contentRoot,
                PhaseSurfaceLayout.ContentSize, Vector2.zero, new Color(.025f, .018f, .012f, .68f));
            Image shade = departureTreasureSeedPicker.GetComponent<Image>();
            shade.sprite = null;
            shade.overrideSprite = null;
            shade.type = Image.Type.Simple;
            shade.color = new Color(.025f, .018f, .012f, .68f);
            shade.raycastTarget = true;
            departureTreasureSeedPicker.SetAsLastSibling();
            RectTransform card = CreateFixedPanel("Departure Treasure Seed Card", departureTreasureSeedPicker,
                new Vector2(1160f, 790f), Vector2.zero, Html("#F0E6CF"));
            MarketShopSkin.ApplyPanel(card.GetComponent<Image>());
            DepartureTreasureSeedText("Departure Treasure Seed Eyebrow", card, new Vector2(1000f, 20f),
                new Vector2(0f, 360f), 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                MarketShopSkin.MutedInk, "TREASURE CHARTS");
            DepartureTreasureSeedText("Departure Treasure Seed Title", card, new Vector2(1000f, 46f),
                new Vector2(0f, 326f), 34, FontStyle.Bold, TextAnchor.MiddleCenter,
                MarketShopSkin.Ink, "选择藏宝海图种子");
            Text notice = DepartureTreasureSeedText("Departure Treasure Seed Notice", card, new Vector2(1040f, 44f),
                new Vector2(0f, 275f), 19, FontStyle.Normal, TextAnchor.MiddleCenter, MarketShopSkin.MutedInk,
                string.Empty);
            notice.text = "可从任意港口出发，无需航海士；出航时消耗所选种子，探险结束返回出发港。";
            notice.resizeTextForBestFit = true;
            notice.resizeTextMinSize = 16;
            notice.resizeTextMaxSize = 19;
            DepartureTreasureSeedRule("Departure Treasure Seed Header Rule", card, 1040f, 244f);
            DepartureTreasureSeedText("Departure Treasure Seed Inventory", card, new Vector2(1040f, 22f),
                new Vector2(0f, 223f), 16, FontStyle.Normal, TextAnchor.MiddleLeft, MarketShopSkin.MutedInk,
                "藏图册  ·  " + session.TreasureMaps.Count + " 张可用");

            bool Current() => ReferenceEquals(identity, departureTreasureSeedPickerIdentity) &&
                departureTreasureSeedPicker != null && departureTreasureSeedPicker.gameObject.activeInHierarchy &&
                DepartureChartViewCurrent(view, owner, revision) && !tutorialActive;

            if (session.TreasureMaps.Count == 0)
            {
                CreateItemIcon("Departure Treasure Seed Empty Icon", card, ItemIconCatalog.ForSpecial("treasurefragment"),
                    new Vector2(0f, 76f), 132f);
                DepartureTreasureSeedText("Departure Treasure Seed Empty Title", card, new Vector2(1000f, 44f),
                    new Vector2(0f, -32f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, MarketShopSkin.Ink,
                    "暂无可用的藏宝海图种子");
                Text empty = DepartureTreasureSeedText("Departure Treasure Seed Empty", card, new Vector2(1000f, 64f),
                    new Vector2(0f, -93f), 20, FontStyle.Normal, TextAnchor.MiddleCenter, MarketShopSkin.MutedInk,
                    "在藏宝图桌鉴定碎片，拼齐四片后\n点击对应海图合成。");
                empty.lineSpacing = 1.25f;
                DepartureTreasureSeedRule("Departure Treasure Seed Empty Rule", card, 240f, -148f);
            }
            // The inventory is capped at six maps; keep every choice visible above the footer.
            for (int index = 0; index < session.TreasureMaps.Count; index++)
            {
                CoreTreasureMapRecord map = session.TreasureMaps[index];
                bool selected = session.SelectedTreasureMap?.Id == map.Id;
                Button option = CreateButton("Departure Treasure Seed Option " + index, card,
                    new Vector2(1060f, 72f), new Vector2(0f, 168f - index * 80f), PanelRaised, MarketShopSkin.Ink, 20);
                MarketShopSkin.ApplyLightButton(option, selected);
                Text caption = option.GetComponentInChildren<Text>();
                GameTypography.Apply(caption, 20, FontStyle.Normal);
                caption.alignment = TextAnchor.MiddleLeft;
                caption.rectTransform.sizeDelta = new Vector2(906f, 62f);
                caption.rectTransform.anchoredPosition = new Vector2(57f, 0f);
                caption.text = "<b>" + (selected ? "已选择 · " : string.Empty) + map.Name +
                    "</b>\n<size=16><color=#7A664F>种子：" + map.Seed + "</color></size>";
                caption.color = MarketShopSkin.Ink;
                caption.resizeTextForBestFit = true;
                caption.resizeTextMinSize = 14;
                caption.resizeTextMaxSize = 20;
                CreateItemIcon("Departure Treasure Seed Icon " + index, option.transform, ItemIconCatalog.ForTreasureMap(map.RankId),
                    new Vector2(-476f, 0f), 58f);
                if (selected)
                    CreateImage("Departure Treasure Seed Selected Marker", option.transform, new Vector2(3f, 48f),
                        new Vector2(-518f, 0f), new Color(.61f, .43f, .20f, 1f)).raycastTarget = false;
                option.onClick.AddListener(() =>
                {
                    if (!Current() || !option.IsInteractable()) return;
                    if (!session.TrySelectTreasureSeed(map.Seed, out string reason))
                    {
                        notice.text = reason;
                        notice.color = Html("#87543A");
                        return;
                    }
                    departureUseChartSeed = false;
                    session.ClearChartSeedSelection();
                    SaveProgress();
                    Render();
                });
            }

            DepartureTreasureSeedRule("Departure Treasure Seed Footer Rule", card, 1040f, -287f);
            Button ordinary = CreateButton("Departure Clear Treasure Seed", card, new Vector2(330f, 52f),
                new Vector2(-356f, -333f), PanelRaised, MarketShopSkin.Ink, 20);
            ordinary.GetComponentInChildren<Text>().text = "不使用种子 · 普通航线";
            MarketShopSkin.ApplyLightButton(ordinary);
            GameTypography.Apply(ordinary.GetComponentInChildren<Text>(), 20, FontStyle.Normal);
            ordinary.onClick.AddListener(() =>
            {
                if (!Current() || !ordinary.IsInteractable()) return;
                session.ClearSelectedTreasureMap();
                session.ClearChartSeedSelection();
                departureUseChartSeed = false;
                SaveProgress();
                Render();
            });
            Button table = CreateButton("Departure Treasure Seed Table", card, new Vector2(330f, 52f),
                new Vector2(0f, -333f), PanelRaised, Gold, 20);
            table.GetComponentInChildren<Text>().text = "前往藏宝图桌";
            WoodButtonSkin.SetRole(table, WoodButtonRole.Primary);
            GameTypography.Apply(table.GetComponentInChildren<Text>(), 20, FontStyle.Normal);
            table.onClick.AddListener(() =>
            {
                if (!Current() || !table.IsInteractable()) return;
                CommitDepartureSeed(view);
                activePortFacility = PortFacilityArtCatalog.TreasureFacilityIndex;
                Render();
            });
            Button close = CreateButton("Close Departure Treasure Seed Selection", card, new Vector2(330f, 52f),
                new Vector2(356f, -333f), PanelRaised, MarketShopSkin.Ink, 20);
            close.GetComponentInChildren<Text>().text = "返回出航配置";
            MarketShopSkin.ApplyTextButton(close);
            GameTypography.Apply(close.GetComponentInChildren<Text>(), 20, FontStyle.Normal);
            close.onClick.AddListener(() =>
            {
                if (Current()) CloseDepartureTreasureSeedPicker();
            });
        }

        private Text DepartureTreasureSeedText(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, FontStyle style, TextAnchor alignment, Color color, string value)
        {
            Text label = CreateText(name, parent, size, position, fontSize, style, alignment, color);
            label.color = color;
            label.text = value;
            label.raycastTarget = false;
            return label;
        }

        private void DepartureTreasureSeedRule(string name, Transform parent, float width, float y)
        {
            CreateImage(name, parent, new Vector2(width, 1f), new Vector2(0f, y),
                new Color(.60f, .47f, .30f, .40f)).raycastTarget = false;
        }

        private void CloseDepartureTreasureSeedPicker()
        {
            departureTreasureSeedPickerIdentity = null;
            if (departureTreasureSeedPicker == null) return;
            departureTreasureSeedPicker.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(departureTreasureSeedPicker.gameObject);
            else DestroyImmediate(departureTreasureSeedPicker.gameObject);
            departureTreasureSeedPicker = null;
        }
    }
}
