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
                    int craftedIndex = -1;
                    if (session.CanCraftTreasureMap(rank.Id, out string reason))
                    {
                        if (session.TryCraftTreasureMapForRank(rank.Id, null, out reason))
                        {
                            treasureFolioOpen = true;
                            treasureFolioView = 1;
                            craftedIndex = session.TreasureMaps.Count - 1;
                            treasureFolioPage = craftedIndex / 2;
                            PlaySfx("treasure_craft", .58f);
                            SaveProgress();
                        }
                    }
                    Render();
                    if (craftedIndex >= 0) PlayTreasureCraftResult(craftedIndex % 2);
                });
            }
        }

        private void BuildTreasureWorkbench(RectTransform room)
        {
            RectTransform desk = CreateRect("Treasure Workbench", room, PhaseSurfaceLayout.BackgroundSize, Vector2.zero);
            bool canIdentify = session.CanIdentifyTreasureFragment(out _);

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
                int identifiedRank = -1;
                if (session.TryIdentifyTreasureFragment(out _))
                {
                    for (int i = 0; i < before.Length; i++)
                        if (session.GetIdentifiedTreasureFragments(CoreTreasureHuntCatalog.Ranks[i].Id) > before[i])
                            treasureBoardIndex = identifiedRank = i;
                    PlaySfx("treasure_craft", .42f);
                    SaveProgress();
                }
                Render();
                if (identifiedRank >= 0) PlayTreasureIdentificationResult(identifiedRank, before[identifiedRank]);
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

        private void PlayTreasureIdentificationResult(int rankIndex, int previousCount)
        {
            if (!Application.isPlaying || editorPreviewBuilding || !isActiveAndEnabled || contentRoot == null) return;
            RectTransform panel = CurrentTreasureView("Treasure Panel");
            if (panel == null) return;
            int piece = Mathf.Clamp(previousCount, 0, 3);
            RawImage target = panel.Find("Treasure Route " + rankIndex + "/Treasure Fragment " + rankIndex + " " + piece)?.GetComponent<RawImage>();
            if (target == null) return;
            TreasureResultMotion.PlayIdentification(panel, target, TreasureChartDisplayLayout.For(rankIndex), piece,
                TreasureChartDisplayLayout.PixelToPanel(new Vector2(335f, 727f)),
                CoreTreasureHuntCatalog.Ranks[rankIndex].Name, previousCount < 4, this);
        }

        private void PlayTreasureCraftResult(int slot)
        {
            if (!Application.isPlaying || editorPreviewBuilding || !isActiveAndEnabled || contentRoot == null) return;
            RectTransform overlay = CurrentTreasureView("Treasure Folio Overlay");
            if (overlay == null) return;
            RawImage target = overlay.Find("Treasure Folio/Treasure Folio Card " + slot +
                "/Treasure Map Preview " + slot)?.GetComponent<RawImage>();
            if (target != null) TreasureResultMotion.PlayCraft(target, this);
        }

        private RectTransform CurrentTreasureView(string name)
        {
            // Render deactivates old views immediately, but Destroy removes them only
            // at frame end. A plain Find can bind the result to that retired same-name tree.
            for (int index = contentRoot.childCount - 1; index >= 0; index--)
            {
                Transform child = contentRoot.GetChild(index);
                if (child.name == name && child.gameObject.activeInHierarchy) return child as RectTransform;
            }
            return null;
        }

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
            RectTransform folio = CreateFixedPanel("Treasure Folio", overlay, new Vector2(1620f, 860f),
                new Vector2(0f, 14f), Color.white);
            MarketShopSkin.ApplyPanel(folio.GetComponent<Image>());
            folio.GetComponent<Image>().raycastTarget = true;
            Color ink = MarketShopSkin.Ink;
            string heading = treasureFolioView == 1 ? "未使用的藏宝图" :
                treasureFolioView == 2 ? "已使用的藏宝图" : "藏图册";
            CreateImage("Treasure Folio Emblem Rim", folio, new Vector2(66f, 66f),
                new Vector2(-716f, 354f), Html("#9A7139"), circleSprite).raycastTarget = false;
            CreateImage("Treasure Folio Emblem Backing", folio, new Vector2(60f, 60f),
                new Vector2(-716f, 354f), Html("#493018"), circleSprite).raycastTarget = false;
            CreateItemIcon("Treasure Folio Emblem", folio, ItemIconCatalog.ForTreasureMap("gilded"),
                new Vector2(-716f, 354f), 54f);
            TreasureFolioText("Treasure Folio Title", folio, new Vector2(980f, 46f),
                new Vector2(-168f, 367f), 32, ink, heading, TextAnchor.MiddleLeft, true);
            TreasureFolioText("Treasure Folio Subtitle", folio, new Vector2(980f, 30f),
                new Vector2(-168f, 327f), 19, MarketShopSkin.MutedInk,
                "海图档案　·　持有 " + session.TreasureMaps.Count + " / " + CoreTreasureHuntCatalog.MaxHeldMaps +
                " 张　·　历史 " + session.UsedTreasureMaps.Count + " 张", TextAnchor.MiddleLeft);
            Button close = TreasureFolioButton("Treasure Close Folio", folio,
                new Vector2(80f, 44f), new Vector2(730f, 354f), "关闭", 20);
            close.onClick.AddListener(() => { treasureFolioOpen = false; Render(); });
            TreasureFolioRule("Treasure Folio Header Rule", folio, new Vector2(1460f, 1f), new Vector2(0f, 292f));

            if (treasureFolioView == 0)
                BuildTreasureFolioOverview(folio);
            else
            {
                bool used = treasureFolioView == 2;
                int count = used ? session.UsedTreasureMaps.Count : session.TreasureMaps.Count;
                int pages = Mathf.Max(1, (count + 1) / 2);
                treasureFolioPage = Mathf.Clamp(treasureFolioPage, 0, pages - 1);
                if (count == 0) BuildTreasureFolioEmpty(folio, used);
                int visible = Mathf.Min(2, count - treasureFolioPage * 2);
                for (int slot = 0; slot < visible; slot++)
                {
                    int index = treasureFolioPage * 2 + slot;
                    RectTransform card = CreateRect("Treasure Folio Card " + slot, folio,
                        new Vector2(660f, 550f), new Vector2(visible == 1 ? 0f : slot == 0 ? -350f : 350f, 19f));
                    Vector2 previewSize = new Vector2(636f, 357.75f);
                    Vector2 previewPosition = new Vector2(0f, 18f);
                    CreateImage("Treasure Map Brass Frame " + slot, card, previewSize + Vector2.one * 6f,
                        previewPosition, Html("#9A7C4D")).raycastTarget = false;
                    CreateImage("Treasure Map Inner Frame " + slot, card, previewSize + Vector2.one * 2f,
                        previewPosition, Html("#493727")).raycastTarget = false;
                    string rankName, seed, coverage, expedition = null;
                    if (used)
                    {
                        CoreUsedTreasureMapRecord record = session.UsedTreasureMaps[index];
                        rankName = record.Rank == null ? "未记录" : record.Rank.Name;
                        seed = record.Seed;
                        coverage = record.HasExplorationCoverage
                            ? record.ExplorationCoverage.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%"
                            : "未记录";
                        CoreVoyageMap map = session.PreviewUsedTreasureMap(record);
                        if (map != null) BuildTreasureUsedMapPreview(card, map, slot, previewSize, previewPosition);
                        else
                        {
                            CreateImage("Treasure Map Unrecorded Face " + slot, card, previewSize,
                                previewPosition, Html("#E5D8BD")).raycastTarget = false;
                            CreateItemIcon("Treasure Map Unrecorded Icon " + slot, card, ItemIconCatalog.ForTreasureMap("weathered"),
                                new Vector2(0f, 59f), 88f);
                            TreasureFolioText("Treasure Map Unrecorded " + slot, card, new Vector2(580f, 40f),
                                new Vector2(0f, -27f), 23, MarketShopSkin.MutedInk, "旧记录未保存海图", TextAnchor.MiddleCenter);
                        }
                    }
                    else
                    {
                        CoreTreasureMapRecord record = session.TreasureMaps[index];
                        rankName = record.Rank.Name;
                        seed = record.Seed;
                        coverage = "0%";
                        expedition = TreasureMapTheme(record) + "\n" + TreasureMapOverview(record);
                        RawImage preview = CreateRawImage("Treasure Map Preview " + slot, card,
                            previewSize, previewPosition,
                            TreasureChartArt(record.RankId), new Rect(0f, 0f, 1f, 1f));
                        preview.raycastTarget = false;
                    }
                    TreasureFolioText("Treasure Map Heading " + slot, card, new Vector2(360f, 38f),
                        new Vector2(-138f, 241f), 26, ink, rankName == "未记录" ? "旧航程存档" : rankName + "藏宝图",
                        TextAnchor.MiddleLeft, true);
                    TreasureFolioText("Treasure Map Archive Number " + slot, card, new Vector2(254f, 30f),
                        new Vector2(191f, 241f), 18, MarketShopSkin.MutedInk,
                        "图册 " + (index + 1).ToString("00") + (used ? "　·　已使用" : "　·　未使用"), TextAnchor.MiddleRight);
                    Text details = TreasureFolioText("Treasure Map Details " + slot, card, new Vector2(636f, 110f),
                        new Vector2(0f, -226f), used ? 20 : 17, ink,
                        used ? "分类：" + rankName + "\n种子：" + seed + "\n探索度：" + coverage
                            : expedition + "\n种子：" + seed + " · 探索度：" + coverage,
                        TextAnchor.UpperLeft);
                    details.lineSpacing = used ? 1.15f : 1f;
                }
                Button previous = TreasureFolioButton("Treasure Folio Previous Page", folio,
                    new Vector2(48f, 64f), new Vector2(-716f, 37f), "‹", 38);
                previous.interactable = treasureFolioPage > 0;
                previous.onClick.AddListener(() => { treasureFolioPage--; Render(); });
                Button next = TreasureFolioButton("Treasure Folio Next Page", folio,
                    new Vector2(48f, 64f), new Vector2(716f, 37f), "›", 38);
                next.interactable = treasureFolioPage + 1 < pages;
                next.onClick.AddListener(() => { treasureFolioPage++; Render(); });
                TreasureFolioText("Treasure Folio Page", folio, new Vector2(260f, 30f),
                    new Vector2(0f, -280f), 18, MarketShopSkin.MutedInk,
                    (treasureFolioPage + 1) + " / " + pages, TextAnchor.MiddleCenter);
                Button back = TreasureFolioButton("Treasure Folio Back", folio,
                    new Vector2(160f, 44f), new Vector2(602f, 354f), "返回藏图册", 20);
                back.onClick.AddListener(() => { treasureFolioView = 0; treasureFolioPage = 0; Render(); });
            }
            TreasureFolioRule("Treasure Folio Footer Rule", folio, new Vector2(1460f, 1f), new Vector2(0f, -310f));
            Button unused = TreasureFolioButton("Treasure Browse Unused", folio,
                new Vector2(610f, 54f), new Vector2(-326f, -357f), "查看未使用的藏宝图", 24);
            MarketShopSkin.SetMode(unused, treasureFolioView == 1);
            unused.onClick.AddListener(() => { treasureFolioView = 1; treasureFolioPage = 0; Render(); });
            Button history = TreasureFolioButton("Treasure Browse Used", folio,
                new Vector2(610f, 54f), new Vector2(326f, -357f), "查看已使用的藏宝图", 24);
            MarketShopSkin.SetMode(history, treasureFolioView == 2);
            history.onClick.AddListener(() => { treasureFolioView = 2; treasureFolioPage = 0; Render(); });
        }

        private void BuildTreasureFolioOverview(RectTransform folio)
        {
            bool empty = session.TreasureMaps.Count == 0;
            TreasureFolioText(empty ? "Treasure Empty Inventory" : "Treasure Inventory Summary", folio,
                new Vector2(1160f, 44f), new Vector2(0f, 231f), 28, MarketShopSkin.Ink,
                empty ? "尚未获得藏宝种子" : "未使用的藏宝图 · " + session.TreasureMaps.Count + " 张",
                TextAnchor.MiddleCenter, true);
            for (int index = 0; index < CoreTreasureHuntCatalog.Ranks.Count; index++)
            {
                CoreTreasureMapRank rank = CoreTreasureHuntCatalog.Ranks[index];
                float x = (index - 1) * 460f;
                int held = session.TreasureMaps.Count(map => map.RankId == rank.Id);
                int fragments = session.GetIdentifiedTreasureFragments(rank.Id);
                CreateItemIcon("Treasure Folio Rank Icon " + index, folio, ItemIconCatalog.ForTreasureMap(rank.Id),
                    new Vector2(x, 116f), 78f);
                TreasureFolioText("Treasure Folio Rank Name " + index, folio, new Vector2(400f, 38f),
                    new Vector2(x, 58f), 25, MarketShopSkin.Ink, rank.Name + "藏宝图", TextAnchor.MiddleCenter, true);
                TreasureFolioText("Treasure Folio Rank Theme " + index, folio, new Vector2(410f, 30f),
                    new Vector2(x, 22f), 19, MarketShopSkin.Ink,
                    CoreTreasureExpeditionCatalog.ThemeName(rank.Id), TextAnchor.MiddleCenter, true);
                TreasureFolioText("Treasure Folio Rank Held " + index, folio, new Vector2(400f, 32f),
                    new Vector2(x, -101f), 20, MarketShopSkin.Ink, "持有 " + held + " 张", TextAnchor.MiddleCenter);
                TreasureFolioText("Treasure Folio Rank Fragments " + index, folio, new Vector2(400f, 30f),
                    new Vector2(x, -136f), 18, MarketShopSkin.MutedInk,
                    "已鉴定碎片 " + fragments + " / " + CoreTreasureHuntCatalog.FragmentCost, TextAnchor.MiddleCenter);
                for (int piece = 0; piece < CoreTreasureHuntCatalog.FragmentCost; piece++)
                {
                    Vector2 point = new Vector2(x + (piece - 1.5f) * 44f, -163f);
                    CreateImage("Treasure Folio Fragment Slot " + index + " " + piece, folio, new Vector2(36f, 6f),
                        point, Html("#D0B98E")).raycastTarget = false;
                    if (fragments > piece)
                        CreateImage("Treasure Folio Fragment Progress " + index + " " + piece, folio, new Vector2(34f, 4f),
                            point, Html("#856335")).raycastTarget = false;
                }
                TreasureFolioText("Treasure Folio Rank Availability " + index, folio, new Vector2(400f, 28f),
                    new Vector2(x, -191f), 17, MarketShopSkin.MutedInk,
                    session.PirateLevel < rank.UnlockLevel ? "海盗等级 " + rank.UnlockLevel + " 解锁" :
                    fragments >= CoreTreasureHuntCatalog.FragmentCost ?
                        session.TreasureMaps.Count >= CoreTreasureHuntCatalog.MaxHeldMaps ? "碎片已集齐，图册已满" : "碎片已集齐，可在工作台合成" :
                        "集齐四片可合成", TextAnchor.MiddleCenter);
            }
            TreasureFolioRule("Treasure Folio Overview Left Rule", folio, new Vector2(1f, 358f), new Vector2(-230f, -23f));
            TreasureFolioRule("Treasure Folio Overview Right Rule", folio, new Vector2(1f, 358f), new Vector2(230f, -23f));
        }

        private void BuildTreasureFolioEmpty(RectTransform folio, bool used)
        {
            CreateItemIcon("Treasure Folio Empty Icon", folio,
                used ? ItemIconCatalog.ForTreasureMap("weathered") : ItemIconCatalog.ForSpecial("treasurefragment"),
                new Vector2(0f, 77f), 132f);
            TreasureFolioText("Treasure Empty Inventory", folio, new Vector2(1160f, 48f),
                new Vector2(0f, -35f), 28, MarketShopSkin.Ink,
                used ? "尚未使用藏宝图" : "尚未获得藏宝种子", TextAnchor.MiddleCenter, true);
        }

        private void TreasureFolioRule(string name, Transform parent, Vector2 size, Vector2 position) =>
            CreateImage(name, parent, size, position, Html("#AD956E")).raycastTarget = false;

        private Text TreasureFolioText(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, Color color, string value, TextAnchor alignment, bool heading = false)
        {
            Text label = TreasureLabel(name, parent, size, position, fontSize, color, value, alignment);
            if (heading) GameTypography.Apply(label, fontSize, FontStyle.Bold);
            label.color = color;
            return label;
        }

        private Button TreasureFolioButton(string name, Transform parent, Vector2 size, Vector2 position, string value, int fontSize)
        {
            Button button = CreateButton(name, parent, size, position, Color.clear, MarketShopSkin.Ink, fontSize);
            button.GetComponentInChildren<Text>().text = value;
            MarketShopSkin.ApplyLightButton(button);
            return button;
        }

        private void BuildTreasureUsedMapPreview(RectTransform card, CoreVoyageMap map, int slot, Vector2 size, Vector2 position)
        {
            RawImage water = CreateRawImage("Treasure Map Water " + slot, card, size,
                position, VoyageMapArt.ChartBase, new Rect(0f, 0f, 1f, 1f));
            water.raycastTarget = false;
            RectTransform preview = CreateRect("Treasure Map Preview " + slot, card, size, position);
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
