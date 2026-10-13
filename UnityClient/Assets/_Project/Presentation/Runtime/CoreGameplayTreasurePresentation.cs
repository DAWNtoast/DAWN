using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool treasureFolioOpen;
        private int treasureFolioView;
        private int treasureFolioPage;
        private int treasureBoardIndex;
        private string treasureFragmentNotice;

        private static Texture2D TreasureChartArt(string rankId) =>
            Resources.Load<Texture2D>("TreasureHunt/treasure_chart_" + rankId);

        private void BuildTreasureFragmentBoards(RectTransform panel)
        {
            for (int index = 0; index < CoreTreasureHuntCatalog.Ranks.Count; index++)
            {
                int captured = index;
                CoreTreasureMapRank rank = CoreTreasureHuntCatalog.Ranks[index];
                int fragments = session.GetIdentifiedTreasureFragments(rank.Id);
                bool ready = session.CanCraftTreasureMap(rank.Id, out _);
                bool selected = treasureBoardIndex == index;
                TreasureChartDisplayLayout display = TreasureChartDisplayLayout.For(index);
                Button board = CreateButton("Treasure Route " + index, panel, display.Size, display.Position, Color.clear, Ink, 18);
                board.GetComponentInChildren<Text>().text = string.Empty;
                board.GetComponent<Image>().raycastTarget = false;
                Image contact = CreateImage("Treasure Chart Paper Contact " + index, board.transform, display.Size,
                    new Vector2(0f, -2f), new Color(.025f, .015f, .005f, .55f));
                contact.raycastTarget = false;
                display.Project(contact);
                Image edge = CreateImage("Treasure Chart Paper Edge " + index, board.transform, display.Size,
                    Vector2.zero, ready ? Gold : selected ? Html("#D9B969") : Html("#A88C59"));
                edge.raycastTarget = false;
                display.Project(edge);
                Image backing = CreateImage("Treasure Chart Backing " + index, board.transform, display.Size - Vector2.one * 5f,
                    Vector2.zero, Html("#241D16"));
                backing.raycastTarget = false;
                display.Project(backing);
                // A subdued complete chart is the guide; only identified quadrants are opaque.
                RawImage map = CreateRawImage("Treasure Route Art " + index, board.transform, display.Size - Vector2.one * 5f,
                    Vector2.zero, TreasureChartArt(rank.Id), new Rect(0f, 0f, 1f, 1f));
                map.color = new Color(1f, 1f, 1f, .22f);
                map.raycastTarget = true;
                display.Project(map);
                for (int piece = 0; piece < 4; piece++)
                {
                    float x = piece % 2 * .5f;
                    float y = piece < 2 ? .5f : 0f;
                    RawImage fragment = CreateRawImage("Treasure Fragment " + index + " " + piece, board.transform,
                        display.Size - Vector2.one * 5f, Vector2.zero, TreasureChartArt(rank.Id), new Rect(x, y, .5f, .5f));
                    fragment.color = new Color(1f, 1f, 1f, fragments > piece ? 1f : 0f);
                    fragment.raycastTarget = false;
                    display.ProjectFragment(fragment, piece);
                }
                Image glow = CreateImage("Treasure Route Hover " + index, board.transform, display.Size, Vector2.zero, Color.clear);
                glow.raycastTarget = false;
                display.Project(glow);
                Text title = CreateText("Treasure Route Name " + index, board.transform, display.NameSize,
                    display.NamePosition - display.Position, 24, FontStyle.Bold, TextAnchor.MiddleCenter, Html("#291B0F"));
                title.text = rank.Name + "藏宝图";
                title.color = Html("#291B0F");
                title.resizeTextForBestFit = true;
                title.resizeTextMinSize = 20;
                title.resizeTextMaxSize = 24;
                title.raycastTarget = true;
                Text progress = CreateText("Treasure Route Progress " + index, board.transform, display.LabelSize,
                    display.LabelPosition - display.Position, 20, FontStyle.Normal, TextAnchor.MiddleCenter, ready ? Gold : Ink);
                progress.text = Mathf.Min(fragments, 4) + "/4" +
                    (ready ? " · 点击合成" : fragments >= 4 ? " · 图册已满" : session.PirateLevel < rank.UnlockLevel ? " · " + rank.UnlockLevel + "级解锁" : string.Empty);
                progress.resizeTextForBestFit = true;
                progress.resizeTextMinSize = 17;
                progress.resizeTextMaxSize = 20;
                progress.raycastTarget = true;
                AddSceneLabelOutline(progress);
                ConfigureGroundedObjectHover(board, glow, map, progress, Gold);
                board.GetComponent<NauticalButtonMotion>().enabled = false;
                CoreVoyageSession owner = session;
                bool used = false;
                board.onClick.AddListener(() =>
                {
                    if (used || !ReferenceEquals(session, owner) || board == null || !board.gameObject.activeInHierarchy) return;
                    used = true;
                    treasureBoardIndex = captured;
                    if (session.CanCraftTreasureMap(rank.Id, out string reason))
                    {
                        if (session.TryCraftTreasureMapForRank(rank.Id, null, out reason))
                        {
                            treasureFolioOpen = true;
                            treasureFolioView = 1;
                            treasureFolioPage = 0;
                            PlaySfx("treasure_craft", .58f);
                            SaveProgress();
                        }
                    }
                    treasureFragmentNotice = reason;
                    Render();
                });
            }
        }

        private void BuildTreasureWorkbench(RectTransform room)
        {
            RectTransform desk = CreateRect("Treasure Workbench", room, PhaseSurfaceLayout.BackgroundSize, Vector2.zero);
            RectTransform status = CreateRect("Treasure Workbench Status Rail", desk,
                TreasureSourceSize(new Vector2(864f, 72f)), TreasureChartDisplayLayout.PixelToPanel(new Vector2(836f, 113f)));
            Image statusFace = status.gameObject.AddComponent<Image>();
            statusFace.color = new Color(.055f, .031f, .018f, .87f);
            statusFace.raycastTarget = false;
            TreasureLabel("Treasure Workbench Status", status, new Vector2(944f, 30f),
                new Vector2(0f, 18f), 22, Ink,
                "海盗等级 " + session.PirateLevel + "　·　未鉴定碎片 " + session.TreasureFragments +
                "　·　藏宝种子 " + session.TreasureMaps.Count + "/" + CoreTreasureHuntCatalog.MaxHeldMaps,
                TextAnchor.MiddleCenter);
            bool canIdentify = session.CanIdentifyTreasureFragment(out string reason);
            string notice = !string.IsNullOrEmpty(treasureFragmentNotice) ? treasureFragmentNotice :
                canIdentify ? "鉴定后自动拼入海图 · 集齐四片，点击海图合成" :
                session.PirateLevel < 2 ? reason : "航行事件与测绘可获碎片；击败首领有 50% 概率掉落 1 个。";
            Text noticeLabel = TreasureLabel("Treasure Workbench Notice", status, new Vector2(944f, 28f),
                new Vector2(0f, -17f), 18, Ink, notice, TextAnchor.MiddleCenter);
            noticeLabel.resizeTextForBestFit = true;
            noticeLabel.resizeTextMinSize = 16;
            noticeLabel.resizeTextMaxSize = 18;

            Vector2 identifyCenter = new Vector2(335f, 727f);
            Button identify = CreateButton("Treasure Identify Fragment", desk, TreasureSourceSize(new Vector2(416f, 176f)),
                TreasureChartDisplayLayout.PixelToPanel(identifyCenter), Color.clear, Ink, 24);
            identify.interactable = canIdentify;
            ConfigureTreasureStation(identify, identifyCenter, new Vector2(345f, 789f), "鉴定碎片");
            RawImage fragmentIcon = CreateItemIcon("Treasure Craft Fragment Icon", identify.transform,
                ItemIconCatalog.ForSpecial("treasurefragment"),
                TreasureChartDisplayLayout.PixelToPanel(new Vector2(270f, 706f)) - TreasureChartDisplayLayout.PixelToPanel(identifyCenter), 32f);
            fragmentIcon.raycastTarget = false;
            TreasureStationStatus(desk, "Treasure Identify Status", new Vector2(345f, 832f),
                session.PirateLevel < 2 ? "等级 2 解锁" : session.TreasureFragments + " 片待鉴定");
            CoreVoyageSession owner = session;
            bool used = false;
            identify.onClick.AddListener(() =>
            {
                if (used || !ReferenceEquals(owner, session) || identify == null || !identify.gameObject.activeInHierarchy || !identify.IsInteractable()) return;
                used = true;
                int[] before = CoreTreasureHuntCatalog.Ranks.Select(rank => session.GetIdentifiedTreasureFragments(rank.Id)).ToArray();
                if (session.TryIdentifyTreasureFragment(out string result))
                {
                    for (int i = 0; i < before.Length; i++)
                        if (session.GetIdentifiedTreasureFragments(CoreTreasureHuntCatalog.Ranks[i].Id) > before[i]) treasureBoardIndex = i;
                    PlaySfx("treasure_craft", .42f);
                    SaveProgress();
                }
                treasureFragmentNotice = result;
                Render();
            });
            Vector2 folioCenter = new Vector2(851f, 727f);
            Button folio = CreateButton("Treasure Open Folio", desk, TreasureSourceSize(new Vector2(350f, 184f)),
                TreasureChartDisplayLayout.PixelToPanel(folioCenter), Color.clear, Ink, 24);
            ConfigureTreasureStation(folio, folioCenter, new Vector2(850f, 789f), "查看藏图册");
            TreasureStationStatus(desk, "Treasure Folio Status", new Vector2(850f, 832f),
                "藏宝种子 " + session.TreasureMaps.Count + "/" + CoreTreasureHuntCatalog.MaxHeldMaps);
            folio.onClick.AddListener(() => { treasureFolioOpen = true; treasureFolioView = 0; treasureFolioPage = 0; Render(); });
            Vector2 purifyCenter = new Vector2(1348f, 724f);
            Button purify = BuildCursePurificationButton(desk, TreasureSourceSize(new Vector2(256f, 184f)),
                TreasureChartDisplayLayout.PixelToPanel(purifyCenter));
            ConfigureTreasureStation(purify, purifyCenter, new Vector2(1348f, 789f), "诅咒净化");
            TreasureStationStatus(desk, "Treasure Purify Status", new Vector2(1348f, 832f), session.CurseCount == 0 ? "当前无诅咒" :
                session.CurseCount + " 层 · 净化每层 " + CoreVoyageSession.CursePurificationCost.ToString("0") + " 金");
            if (treasureFolioOpen) BuildTreasureFolio();
        }

        private static Vector2 TreasureSourceSize(Vector2 pixels) =>
            PhaseSurfaceLayout.SourcePixelSizeToContent(pixels, new Vector2(1672f, 941f));

        private static void ConfigureTreasureStation(Button button, Vector2 sourceCenter, Vector2 plaqueCenter, string caption)
        {
            Image face = button.GetComponent<Image>();
            face.sprite = null;
            face.overrideSprite = null;
            face.type = Image.Type.Simple;
            face.color = Color.clear;
            face.raycastTarget = true;
            NauticalButtonMotion motion = button.GetComponent<NauticalButtonMotion>();
            if (motion != null) motion.enabled = false;
            Text label = button.GetComponentInChildren<Text>();
            label.rectTransform.sizeDelta = TreasureSourceSize(new Vector2(206f, 34f));
            label.rectTransform.anchoredPosition = TreasureChartDisplayLayout.PixelToPanel(plaqueCenter) -
                TreasureChartDisplayLayout.PixelToPanel(sourceCenter);
            label.text = caption;
            label.color = Color.white;
            GameTypography.Apply(label, 26, FontStyle.Bold);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 22;
            label.resizeTextMaxSize = 26;
            label.raycastTarget = false;
            foreach (Shadow effect in label.GetComponents<Shadow>()) effect.enabled = false;
            button.targetGraphic = label;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Html("#291B0F");
            colors.highlightedColor = Html("#6A3A12");
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Html("#120C06");
            colors.disabledColor = Html("#67543C");
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .12f;
            button.colors = colors;
        }

        private void TreasureStationStatus(RectTransform desk, string name, Vector2 sourceCenter, string value)
        {
            Text label = TreasureLabel(name, desk, TreasureSourceSize(new Vector2(322f, 26f)),
                TreasureChartDisplayLayout.PixelToPanel(sourceCenter), 20, Ink, value, TextAnchor.MiddleCenter);
            AddSceneLabelOutline(label);
        }

        private void BuildTreasureFolio()
        {
            RectTransform overlay = CreateFixedPanel("Treasure Folio Overlay", contentRoot,
                PhaseSurfaceLayout.BackgroundSize, PhaseSurfaceLayout.BackgroundPosition, new Color(.012f, .018f, .024f, .78f));
            RectTransform folio = CreateFixedPanel("Treasure Folio", overlay, new Vector2(1540f, 760f),
                new Vector2(0f, 14f), Color.white);
            NauticalSurfaceSkin.ApplyReadingPanel(folio.GetComponent<Image>());
            Color ink = Html("#302116");
            string heading = treasureFolioView == 1 ? "未使用的藏宝图" :
                treasureFolioView == 2 ? "已使用的藏宝图" : "藏图册";
            TreasureLabel("Treasure Folio Title", folio, new Vector2(1080f, 44f),
                new Vector2(0f, 302f), 30, ink, heading, TextAnchor.MiddleCenter);
            Button close = TreasureFolioButton("Treasure Close Folio", folio,
                new Vector2(94f, 44f), new Vector2(661f, 302f), "关闭", 20);
            close.onClick.AddListener(() => { treasureFolioOpen = false; Render(); });

            if (treasureFolioView == 0)
            {
                bool empty = session.TreasureMaps.Count == 0;
                TreasureLabel(empty ? "Treasure Empty Inventory" : "Treasure Inventory Summary", folio,
                    new Vector2(1160f, 72f), Vector2.zero, 30, ink,
                    empty ? "尚未获得藏宝种子" : "未使用的藏宝图 · " + session.TreasureMaps.Count + " 张",
                    TextAnchor.MiddleCenter);
            }
            else
            {
                bool used = treasureFolioView == 2;
                int count = used ? session.UsedTreasureMaps.Count : session.TreasureMaps.Count;
                int pages = Mathf.Max(1, (count + 1) / 2);
                treasureFolioPage = Mathf.Clamp(treasureFolioPage, 0, pages - 1);
                if (count == 0)
                    TreasureLabel("Treasure Empty Inventory", folio, new Vector2(1160f, 72f),
                        Vector2.zero, 30, ink, used ? "尚未使用藏宝图" : "尚未获得藏宝种子", TextAnchor.MiddleCenter);
                for (int slot = 0; slot < 2; slot++)
                {
                    int index = treasureFolioPage * 2 + slot;
                    if (index >= count) break;
                    RectTransform card = CreateRect("Treasure Folio Card " + slot, folio,
                        new Vector2(580f, 488f), new Vector2(slot == 0 ? -326f : 326f, 18f));
                    string rankName, seed, coverage;
                    if (used)
                    {
                        CoreUsedTreasureMapRecord record = session.UsedTreasureMaps[index];
                        rankName = record.Rank == null ? "未记录" : record.Rank.Name;
                        seed = record.Seed;
                        coverage = record.HasExplorationCoverage
                            ? record.ExplorationCoverage.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%"
                            : "未记录";
                        CoreVoyageMap map = session.PreviewUsedTreasureMap(record);
                        if (map != null) BuildTreasureUsedMapPreview(card, map, slot);
                        else TreasureLabel("Treasure Map Unrecorded " + slot, card, new Vector2(560f, 320f),
                            new Vector2(0f, 48f), 23, ink, "旧记录未保存海图", TextAnchor.MiddleCenter);
                    }
                    else
                    {
                        CoreTreasureMapRecord record = session.TreasureMaps[index];
                        rankName = record.Rank.Name;
                        seed = record.Seed;
                        coverage = "0%";
                        RawImage preview = CreateRawImage("Treasure Map Preview " + slot, card,
                            new Vector2(560f, 315f), new Vector2(0f, 48f),
                            TreasureChartArt(record.RankId), new Rect(0f, 0f, 1f, 1f));
                        preview.raycastTarget = false;
                    }
                    TreasureLabel("Treasure Map Details " + slot, card, new Vector2(560f, 120f),
                        new Vector2(0f, -178f), 20, ink,
                        "分类：" + rankName + "\n种子：" + seed + "\n探索度：" + coverage,
                        TextAnchor.UpperLeft);
                }
                Button previous = TreasureFolioButton("Treasure Folio Previous Page", folio,
                    new Vector2(60f, 64f), new Vector2(-676f, 38f), "‹", 42);
                previous.interactable = treasureFolioPage > 0;
                previous.onClick.AddListener(() => { treasureFolioPage--; Render(); });
                Button next = TreasureFolioButton("Treasure Folio Next Page", folio,
                    new Vector2(60f, 64f), new Vector2(676f, 38f), "›", 42);
                next.interactable = treasureFolioPage + 1 < pages;
                next.onClick.AddListener(() => { treasureFolioPage++; Render(); });
                TreasureLabel("Treasure Folio Page", folio, new Vector2(260f, 32f),
                    new Vector2(0f, -239f), 20, ink, (treasureFolioPage + 1) + " / " + pages, TextAnchor.MiddleCenter);
                Button back = TreasureFolioButton("Treasure Folio Back", folio,
                    new Vector2(148f, 44f), new Vector2(-635f, 302f), "返回藏图册", 20);
                back.onClick.AddListener(() => { treasureFolioView = 0; treasureFolioPage = 0; Render(); });
            }
            Button unused = TreasureFolioButton("Treasure Browse Unused", folio,
                new Vector2(510f, 52f), new Vector2(-330f, -292f), "查看未使用的藏宝图", 24);
            unused.onClick.AddListener(() => { treasureFolioView = 1; treasureFolioPage = 0; Render(); });
            Button history = TreasureFolioButton("Treasure Browse Used", folio,
                new Vector2(510f, 52f), new Vector2(330f, -292f), "查看已使用的藏宝图", 24);
            history.onClick.AddListener(() => { treasureFolioView = 2; treasureFolioPage = 0; Render(); });
        }

        private Button TreasureFolioButton(string name, Transform parent, Vector2 size, Vector2 position, string value, int fontSize)
        {
            Button button = CreateButton(name, parent, size, position, Color.clear, Html("#302116"), fontSize);
            Image face = button.GetComponent<Image>();
            face.sprite = null;
            face.overrideSprite = null;
            face.color = new Color(.29f, .18f, .07f, .10f);
            Text label = button.GetComponentInChildren<Text>();
            label.text = value;
            label.color = Html("#302116");
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1.7f);
            colors.pressedColor = new Color(.7f, .7f, .7f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, .3f);
            button.colors = colors;
            return button;
        }

        private void BuildTreasureUsedMapPreview(RectTransform card, CoreVoyageMap map, int slot)
        {
            Vector2 size = new Vector2(560f, 315f);
            RawImage water = CreateRawImage("Treasure Map Water " + slot, card, size,
                new Vector2(0f, 48f), VoyageMapArt.ChartBase, new Rect(0f, 0f, 1f, 1f));
            water.raycastTarget = false;
            RectTransform preview = CreateRect("Treasure Map Preview " + slot, card, size, new Vector2(0f, 48f));
            VoyageChartSurfaceGraphic surface = preview.gameObject.AddComponent<VoyageChartSurfaceGraphic>();
            surface.Initialize(map, VoyageMapArt, 0);
            foreach (CoreMapNode node in map.Nodes)
            {
                Vector2 point = new Vector2((node.Position.X / map.WorldWidth - .5f) * size.x,
                    (node.Position.Y / map.WorldHeight - .5f) * size.y);
                bool core = node.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId;
                TreasureLabel("Treasure Map Landmark " + slot + " " + node.Id, surface.transform,
                    new Vector2(28f, 28f), point, core ? 18 : 13,
                    core ? Html("#682519") : Html("#513F20"), core ? "×" : "·", TextAnchor.MiddleCenter);
            }
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
