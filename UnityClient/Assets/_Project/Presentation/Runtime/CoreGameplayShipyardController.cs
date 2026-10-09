using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum ShipyardDossierTab { Performance, Background, Equipment }

    public sealed partial class CoreGameplayDemoController
    {
        private bool shipyardDetailOpen;
        private ShipyardDossierTab shipyardDossierTab;

        private static readonly string[] ShipyardHistories =
        {
            SeventhPortLoreCatalog.ShipLoreFor("sloop").Description,
            SeventhPortLoreCatalog.ShipLoreFor("merchant").Description,
            SeventhPortLoreCatalog.ShipLoreFor("patrol").Description,
            SeventhPortLoreCatalog.ShipLoreFor("relic").Description,
        };

        private int shipyardViewRevision;
        private ShipyardVesselQuote shipyardVesselQuote;

        private sealed class ShipyardVesselQuote
        {
            public CoreVoyageSession Session;
            public string Port;
            public int ShipIndex, Price, Revision;
            public RectTransform View;
            public bool Consumed;
        }

        private bool OwnsShipyardVessel(int index) => index >= 0 && index < CoreVoyageSession.Ships.Count &&
            (index == 0 || (!(editorPreviewBuilding || !Application.isPlaying) &&
            (index == session.SelectedShipIndex || PlayerPrefs.GetInt(ShipOwnedKey(index), 0) == 1)));

        private int ShipyardPortIndex()
        {
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
                if (CoreVoyageSession.DestinationPorts[index] == session.CurrentPort) return index;
            return -1;
        }

        private List<int> VisibleShipyardVessels()
        {
            var visible = new List<int>();
            foreach (int index in CoreShipMarketCatalog.ShipsForPort(ShipyardPortIndex())) visible.Add(index);
            for (int index = 0; index < CoreVoyageSession.Ships.Count; index++)
                if (OwnsShipyardVessel(index) && !visible.Contains(index)) visible.Add(index);
            // Authored/editor inspections can name any catalog vessel without
            // borrowing a player's ownership flags or granting purchasing rights.
            if (!Application.isPlaying && editorPreviewShipIndex >= 0 && editorPreviewShipIndex < CoreVoyageSession.Ships.Count &&
                !visible.Contains(editorPreviewShipIndex)) visible.Add(editorPreviewShipIndex);
            return visible;
        }

        private static string ShipyardVesselHomePort(int index)
        {
            int port = CoreShipMarketCatalog.HomePortIndex(index);
            return port >= 0 && port < CoreVoyageSession.DestinationPorts.Count ? CoreVoyageSession.DestinationPorts[port] : "未知港口";
        }

        private string ShipyardOwnershipLabel(int index)
        {
            bool local = CoreShipMarketCatalog.IsSoldAtPort(ShipyardPortIndex(), index);
            if (index == session.SelectedShipIndex) return local ? "当前旗舰" : "当前旗舰 · 已拥有外港船";
            if (OwnsShipyardVessel(index)) return local ? "已拥有" : "已拥有外港船";
            return local ? "售价 " + CoreShipMarketCatalog.PurchaseCost(index) + " 金币" : "本港不售 · 所属港 " + ShipyardVesselHomePort(index);
        }

        private bool IsCurrentShipyardControl(Component control, CoreVoyageSession owner, string port, int revision)
        {
            return control != null && control.gameObject.activeInHierarchy && ReferenceEquals(session, owner) &&
                session != null && session.Phase == CoreDemoPhase.Port && session.CurrentPort == port &&
                revision == shipyardViewRevision && activePortFacility == 1;
        }

        private void BuildShipyardFacility()
        {
            shipyardViewRevision++;
            shipyardVesselQuote = null;
            selectedShipPreview = Mathf.Clamp(selectedShipPreview, 0, CoreVoyageSession.Ships.Count - 1);
            List<int> visibleShips = VisibleShipyardVessels();
            if (!visibleShips.Contains(selectedShipPreview)) selectedShipPreview = visibleShips[0];
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            int revision = shipyardViewRevision;
            RectTransform panel = CreateFixedPanel("Shipyard Panel", contentRoot, new Vector2(1820f, 780f), new Vector2(0f, -55f), Color.clear);
            panel.GetComponent<Image>().raycastTarget = false;
            if (shipyardDetailOpen)
            {
                BuildShipyardDossier(panel, selectedShipPreview);
                return;
            }

            int index = selectedShipPreview;
            Vector2 waterline = new Vector2(0f, -145f);
            CreateShipyardWater("Shipyard Water Shadow", panel, waterline, false);
            Image glow = CreateImage("Shipyard Berth Glow " + index, panel, new Vector2(620f, 40f), waterline, Color.clear, circleSprite);
            glow.raycastTarget = false;
            RectTransform vessel = CreateRect("Shipyard Floating Vessel", panel, new Vector2(780f, 590f), Vector2.zero);
            // One water plane clips every equipment layer, so plating or the
            // bow cannot hang below the submerged hull. The dossier is uncut.
            RectTransform waterClip = CreateRect("Shipyard Waterline Clip", vessel, new Vector2(920f, 575f), new Vector2(0f, 134.5f));
            waterClip.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 8);
            Graphic shipArt = BuildShipyardVessel(waterClip, index, waterline - waterClip.anchoredPosition, 1f, "Shipyard");
            Button inspect = vessel.gameObject.AddComponent<Button>();
            inspect.targetGraphic = shipArt;
            inspect.gameObject.name = "Shipyard Ship " + index;
            CreateShipyardWater("Shipyard Water Contact", panel, waterline, true);

            // The ship can be inspected regardless of price or ownership. The
            // gallery never invokes payment or silently switches the flagship.
            float plaqueHeight = 120f * GameReadability.Scale;
            RectTransform plaque = CreateFixedPanel("Shipyard Gallery Plaque", panel, new Vector2(900f, plaqueHeight), new Vector2(0f, -394f + plaqueHeight * .5f), ScenePlaqueColor(0.91f));
            plaque.GetComponent<Image>().raycastTarget = false;
            Text title = ShipyardText("Shipyard Ship Name " + index, plaque, new Vector2(870f, 48f * GameReadability.Scale), new Vector2(0f, 32f * GameReadability.Scale), 32, Gold);
            title.text = CoreVoyageSession.Ships[index].Name;
            ShipyardText("Shipyard Gallery Role", plaque, new Vector2(870f, 30f * GameReadability.Scale), new Vector2(0f, -8f * GameReadability.Scale), 20, Ink).text =
                CoreShipMarketCatalog.Role(index) + "　·　" + port + "售 " + CoreShipMarketCatalog.ShipsForPort(ShipyardPortIndex()).Count + " 型";
            ShipyardText("Shipyard Gallery Ownership", plaque, new Vector2(870f, 30f * GameReadability.Scale), new Vector2(0f, -40f * GameReadability.Scale), 19, Ink).text =
                ShipyardOwnershipLabel(index) + "　·　" + (visibleShips.IndexOf(index) + 1) + " / " + visibleShips.Count;
            ConfigureGroundedObjectHover(inspect, glow, shipArt, title, Gold);
            ConfigureShipyardSilhouetteHover(inspect, waterClip, glow);
            inspect.onClick.AddListener(() =>
            {
                if (!IsCurrentShipyardControl(inspect, owner, port, revision)) return;
                shipyardDetailOpen = true;
                shipyardDossierTab = ShipyardDossierTab.Performance;
                ResetShipyardEquipmentSelection();
                RefreshShipyardDossierView();
            });

            Button previous = CreateShipyardArrow(panel, false);
            previous.interactable = visibleShips.Count > 1;
            previous.onClick.AddListener(() =>
            {
                if (IsCurrentShipyardControl(previous, owner, port, revision)) BrowseShipyard(-1);
            });
            Button next = CreateShipyardArrow(panel, true);
            next.interactable = visibleShips.Count > 1;
            next.onClick.AddListener(() =>
            {
                if (IsCurrentShipyardControl(next, owner, port, revision)) BrowseShipyard(1);
            });
            ShipyardText("Shipyard Previous Caption", panel, new Vector2(220f, 36f), new Vector2(-665f, -91f), 21, Ink).text = "上一艘";
            ShipyardText("Shipyard Next Caption", panel, new Vector2(220f, 36f), new Vector2(665f, -91f), 21, Ink).text = "下一艘";
        }

        private Button CreateShipyardArrow(Transform parent, bool forward)
        {
            string name = forward ? "Shipyard Next Vessel" : "Shipyard Previous Vessel";
            Button button = CreateButton(name, parent, new Vector2(148f, 78f), new Vector2(forward ? 665f : -665f, -30f), Color.white, Gold, 30);
            button.GetComponentInChildren<Text>().text = string.Empty;
            float direction = forward ? 1f : -1f;
            CreateLine(button.transform, new Vector2(-12f * direction, 19f), new Vector2(12f * direction, 0f), Gold, 4f);
            CreateLine(button.transform, new Vector2(12f * direction, 0f), new Vector2(-12f * direction, -19f), Gold, 4f);
            return button;
        }

        private static void ConfigureShipyardSilhouetteHover(Button button, Transform visuals, Image waterGlow)
        {
            List<Outline> edges = new List<Outline>();
            foreach (RawImage layer in visuals.GetComponentsInChildren<RawImage>())
            {
                Outline outline = layer.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.80f, 0.30f, 0.9f);
                outline.effectDistance = new Vector2(3f, -3f);
                outline.useGraphicAlpha = true;
                outline.enabled = false;
                edges.Add(outline);
            }
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            trigger.triggers.Find(entry => entry.eventID == EventTriggerType.PointerEnter).callback.AddListener(_ =>
            {
                if (!button.interactable) return;
                // Keep pointer interaction, but never paint an outline or a
                // reflected water glow over the vessel.
                foreach (Outline edge in edges) edge.enabled = false;
                waterGlow.color = Color.clear;
            });
            trigger.triggers.Find(entry => entry.eventID == EventTriggerType.PointerExit).callback.AddListener(_ =>
            {
                foreach (Outline edge in edges) edge.enabled = false;
                waterGlow.color = Color.clear;
            });
        }

        private void BrowseShipyard(int direction)
        {
            List<int> visible = VisibleShipyardVessels();
            int current = visible.IndexOf(selectedShipPreview);
            selectedShipPreview = visible[(Math.Max(0, current) + (direction < 0 ? -1 : 1) + visible.Count) % visible.Count];
            shipyardDetailOpen = false;
            shipyardDossierTab = ShipyardDossierTab.Performance;
            ResetShipyardEquipmentSelection();
            RefreshShipyardDossierView();
        }

        private void CreateShipyardWater(string name, Transform parent, Vector2 waterline, bool foam)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(650f, 62f), waterline);
            rect.gameObject.AddComponent<BattleShipWaterGraphic>().Configure(foam, 0.5f);
        }

        private Graphic BuildShipyardVessel(Transform parent, int index, Vector2 waterline, float scale, string prefix)
        {
            if (index == 0)
            {
                RectTransform modular = BuildModularShipPreview(prefix + " Modular Ship", parent,
                    new Vector2(780f, 520f) * scale, waterline + new Vector2(0f, 210f * scale), index);
                RawImage hull = modular.Find(prefix + " Modular Ship Base").GetComponent<RawImage>();
                hull.raycastTarget = prefix == "Shipyard";
                if (prefix == "Shipyard") AlignShipHullToWaterline(modular, hull, waterline.y - 4f * scale);
                return hull;
            }

            // Keep the hull in its original aspect ratio. Narrow shader keying
            // removes the atlas navy without destroying dark sails or rigging.
            Texture2D texture = portArt.ShipTexture(index);
            Rect uv = portArt.ShipUv(index);
            float aspect = (texture.width * uv.width) / (texture.height * uv.height);
            Vector2 baseSize = index >= 4 ? new Vector2(Mathf.Min(650f, 560f * aspect), Mathf.Min(560f, 650f / aspect)) :
                new Vector2(650f, 585f);
            RectTransform composite = CreateRect(prefix + " Composite Ship " + index, parent,
                baseSize * scale, waterline + new Vector2(0f, baseSize.y * (250f / 585f) * scale));
            float ix = 10f / texture.width;
            float iy = 10f / texture.height;
            uv = new Rect(uv.x + ix, uv.y + iy, uv.width - ix * 2f, uv.height - iy * 2f);
            if (index < 4) uv = new Rect(uv.x, uv.y + uv.height * 0.085f, uv.width, uv.height * 0.90f);
            RawImage ship = CreateRawImage(prefix + " Ship Art " + index, composite,
                baseSize * scale, Vector2.zero, texture, uv);
            ship.color = portArt.ShipTint(index);
            BattleShipCutout.Attach(ship);
            ship.raycastTarget = prefix == "Shipyard";
            AddShipEquipmentLayers(prefix, composite, baseSize * scale, index);
            if (prefix == "Shipyard") AlignShipHullToWaterline(composite, ship, waterline.y - 4f * scale);
            return ship;
        }

        private void BuildShipyardDossier(RectTransform panel, int index)
        {
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            int revision = shipyardViewRevision;
            RectTransform detail = CreateFixedPanel("Shipyard Detail", panel, new Vector2(1630f, 744f), new Vector2(0f, -10f), Color.clear);
            detail.GetComponent<Image>().raycastTarget = false;
            RawImage paper = CreateRawImage("Shipyard Dossier Parchment", detail, new Vector2(1620f, 740f), Vector2.zero,
                cardWorkshopArt.CollectionParchment, new Rect(0f, 0.2f, 1f, 0.6f));
            paper.raycastTarget = false;
            Color ink = Html("#302116");
            Color muted = Html("#76502B");
            CoreShipDefinition definition = CoreVoyageSession.Ships[index];
            ShipyardText("Shipyard Dossier Name", detail, new Vector2(1440f, 55f * GameReadability.Scale), new Vector2(0f, 302f), 36, ink, TextAnchor.MiddleLeft).text = definition.Name;
            ShipyardText("Shipyard Dossier Role", detail, new Vector2(1440f, 40f * GameReadability.Scale), new Vector2(0f, 233f), 22, muted, TextAnchor.MiddleLeft).text =
                CoreShipMarketCatalog.Role(index) + "　·　所属港：" + ShipyardVesselHomePort(index) + "　·　" + ShipyardOwnershipLabel(index);
            string[] tabLabels = { "船只性能", "船只背景", "配件装配" };
            for (int tabIndex = 0; tabIndex < tabLabels.Length; tabIndex++)
            {
                ShipyardDossierTab tab = (ShipyardDossierTab)tabIndex;
                Button tabButton = CreateButton("Shipyard Dossier Tab " + tab, detail, new Vector2(440f, 48f * GameReadability.Scale),
                    new Vector2(-486f + 486f * tabIndex, 154f), Gold, Ink, GameReadability.FontSize(24));
                tabButton.GetComponentInChildren<Text>().text = tabLabels[tabIndex];
                GameReadability.Body(tabButton.GetComponentInChildren<Text>(), 24);
                WoodButtonSkin.SetSelected(tabButton, shipyardDossierTab == tab);
                tabButton.onClick.AddListener(() =>
                {
                    if (!IsCurrentShipyardControl(tabButton, owner, port, revision) ||
                        !shipyardDetailOpen || selectedShipPreview != index) return;
                    shipyardDossierTab = tab;
                    ResetShipyardEquipmentSelection();
                    RefreshShipyardDossierView();
                });
            }
            if (shipyardDossierTab == ShipyardDossierTab.Equipment)
                BuildShipyardEquipmentPage(detail, index, ink, muted);
            else
            {
                // The complete hull rectangle stays above the caption viewport,
                // including the tallest composite vessel and its transparent rim.
                BuildShipyardVessel(detail, index, new Vector2(-468f, -112f), 0.40f, "Shipyard Dossier");
                Text caption = ShipyardText("Shipyard Dossier Illustration Caption", detail, new Vector2(490f, 76f), new Vector2(-468f, -180f), 22, muted);
                caption.text =
                    "标准船员 " + definition.CrewCount + " 人 · 基础每日水粮各 " + definition.CrewCount + "\n各船配件独立保存 · 战术牌组共用";
                ReadableParagraph(caption, detail, new Vector2(490f, 76f), new Vector2(-468f, -176f), 22, true);
                if (shipyardDossierTab == ShipyardDossierTab.Background)
                {
                    RectTransform storyPage = CreateRect("Shipyard Background Page", detail, new Vector2(750f, 310f), new Vector2(326f, -50f));
                    ShipyardText("Shipyard Dossier Story Title", storyPage, new Vector2(750f, 50f * GameReadability.Scale), new Vector2(0f, 130f), 32, muted, TextAnchor.MiddleLeft).text = "船坞旧闻";
                    ShipyardText("Shipyard Dossier Story", storyPage, new Vector2(750f, 240f), new Vector2(0f, -28f), 29, ink, TextAnchor.UpperLeft).text =
                        string.IsNullOrWhiteSpace(definition.Lore) ? ShipyardHistories[CoreShipMarketCatalog.ArtworkIndex(index)] : definition.Lore;
                    WrapShipyardDossierText(storyPage, "Shipyard Dossier Story");
                }
                else BuildShipyardPerformancePage(detail, index, ink, muted);
            }
            bool owned = OwnsShipyardVessel(index);
            bool selected = index == session.SelectedShipIndex;
            bool local = CoreShipMarketCatalog.IsSoldAtPort(ShipyardPortIndex(), index);
            int purchaseCost = CoreShipMarketCatalog.PurchaseCost(index);
            bool affordable = session.ProfileGold >= purchaseCost;
            bool cargoFits = session.CanSelectShip(index);
            ShipyardText("Shipyard Dossier Price", detail, new Vector2(1440f, 40f * GameReadability.Scale), new Vector2(0f, -244f), 22, ink, TextAnchor.MiddleRight).text =
                !cargoFits ? "货舱不足 · 请先将货物存入本港仓库或出售" :
                (owned ? local ? "已拥有" : "已拥有外港船 · 可在此设为旗舰或改装" :
                    local ? "购船价 " + purchaseCost + " 金币" : "本港不出售此船型 · 所属港：" + ShipyardVesselHomePort(index)) +
                "　·　持有 " + session.ProfileGold + " 金币";
            Button back = CreateButton(shipyardReturnToDeparture ? "Back To Voyage Departure" : "Back To Shipyard Gallery", detail, new Vector2(330f, 54f * GameReadability.Scale), new Vector2(15f, -319f), Html("#6D4B2C"), Ink, GameReadability.FontSize(23));
            back.GetComponentInChildren<Text>().text = shipyardReturnToDeparture ? "返回出航码头" : "返回船坞";
            GameReadability.Body(back.GetComponentInChildren<Text>(), 23);
            back.onClick.AddListener(() =>
            {
                if (!IsCurrentShipyardControl(back, owner, port, revision)) return;
                shipyardDetailOpen = false;
                ResetShipyardEquipmentSelection();
                if (shipyardReturnToDeparture) ReturnFromDepartureDossier();
                else RefreshShipyardDossierView();
            });
            Button confirm = CreateButton("Confirm Shipyard Vessel", detail, new Vector2(470f, 54f * GameReadability.Scale), new Vector2(443f, -319f), Html("#28594E"), Ink, GameReadability.FontSize(23));
            confirm.GetComponentInChildren<Text>().text = selected ? "当前旗舰" : !cargoFits ? "货物超过该船舱容" : owned ? "设为旗舰" :
                !local ? "本港不出售此船型" : affordable ? "购买并启用 · " + purchaseCost + " 金币" :
                "金币不足 · 还需 " + (purchaseCost - session.ProfileGold);
            GameReadability.Body(confirm.GetComponentInChildren<Text>(), 23);
            confirm.interactable = !selected && cargoFits && (owned || local && affordable);
            var quote = new ShipyardVesselQuote
            {
                Session = owner, Port = port, ShipIndex = index, Price = purchaseCost,
                Revision = revision, View = detail,
            };
            shipyardVesselQuote = quote;
            confirm.onClick.AddListener(() =>
            {
                if (ReferenceEquals(shipyardVesselQuote, quote) && IsCurrentShipyardControl(confirm, owner, port, revision))
                    ConfirmShipyardVessel(index);
            });
        }

        private void BuildShipyardPerformancePage(Transform detail, int index, Color ink, Color muted)
        {
            CoreShipDefinition definition = CoreVoyageSession.Ships[index];
            float[] values = { definition.MaxHull, definition.Armor, definition.Firepower, definition.SpeedKnots, definition.CargoCapacity };
            float[] deltas = { 0f, 0f, 0f, 0f, 0f };
            if (OwnsShipyardVessel(index))
                for (int slot = 0; slot < ComponentArtCatalog.SlotCount; slot++)
                {
                    ComponentArtEntry installed = ShipComponent(index, (ComponentSlot)slot);
                    ComponentArtEntry baseline = componentArt.Get(slot * ComponentArtCatalog.EntriesPerSlot);
                    values[0] += (installed?.HullBonus ?? 0) - baseline.HullBonus;
                    values[1] += (installed?.ArmorBonus ?? 0) - baseline.ArmorBonus;
                    values[2] += (installed?.FirepowerBonus ?? 0) - baseline.FirepowerBonus;
                    values[3] += (installed?.SpeedBonus ?? 0f) - baseline.SpeedBonus;
                    deltas[0] += (installed?.HullBonus ?? 0) - baseline.HullBonus;
                    deltas[1] += (installed?.ArmorBonus ?? 0) - baseline.ArmorBonus;
                    deltas[2] += (installed?.FirepowerBonus ?? 0) - baseline.FirepowerBonus;
                    deltas[3] += (installed?.SpeedBonus ?? 0f) - baseline.SpeedBonus;
                }

            RectTransform page = CreateRect("Shipyard Performance Page", detail, new Vector2(750f, 310f), new Vector2(326f, -50f));
            RectTransform stats = CreateRect("Shipyard Performance Stats", page, new Vector2(500f, 330f), new Vector2(-145f, -2f));
            stats.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            string[] captions = { "船体上限", "装甲", "火力", "航速", "货舱容量" };
            float barWidth = 292f;
            float barX = -6f;
            for (int stat = 0; stat < captions.Length; stat++)
            {
                float y = 132f - stat * 58f;
                float value = stat == 0 ? Mathf.Max(1f, values[stat]) : Mathf.Max(0f, values[stat]);
                float maximum = ShipyardUnmodifiedStatMaximum(stat);
                bool reachesMaximum = value >= maximum - 0.001f;
                float ratio = Mathf.Clamp01(value / maximum);
                Color trackColor = new Color(0.12f, 0.09f, 0.06f, 0.72f);
                Color fillColor = reachesMaximum ? Html("#4C985D") : Html("#C6922E");
                Image track = CreateImage("Shipyard Stat Bar " + stat, stats, new Vector2(barWidth, 24f), new Vector2(barX, y), trackColor, circleSprite);
                track.raycastTarget = false;
                float fillWidth = Mathf.Max(4f, barWidth * ratio);
                Image fill = CreateImage("Shipyard Stat Bar Fill " + stat, stats, new Vector2(fillWidth, 16f),
                    new Vector2(barX - (barWidth - fillWidth) * 0.5f, y), fillColor, circleSprite);
                fill.raycastTarget = false;
                ShipyardText("Shipyard Dossier Stat " + stat, stats, new Vector2(192f, 32f), new Vector2(-243f, y), 23, ink, TextAnchor.MiddleLeft).text =
                    captions[stat] + "  " + FormatShipyardStatValue(stat, value);
                Text delta = ShipyardText("Shipyard Stat Delta " + stat, stats, new Vector2(78f, 30f), new Vector2(200f, y), 20,
                    fillColor, TextAnchor.MiddleLeft);
                delta.text = FormatShipyardStatDelta(stat, deltas[stat]);
                // Put the cap on a separate baseline below the delta.  The old
                // -17 offset overlapped the 30px delta label by several pixels,
                // which made values such as "+2 / 8" look doubled.
                Text maximumLabel = ShipyardText("Shipyard Stat Maximum " + stat, stats, new Vector2(86f, 22f), new Vector2(195f, y - 27f), 12,
                    muted, TextAnchor.MiddleLeft);
                maximumLabel.text = "/ " + FormatShipyardStatValue(stat, maximum).Replace(" 节", string.Empty);
            }
            ShipyardText("Shipyard Performance Source", stats, new Vector2(500f, 28f), new Vector2(-2f, -151f), 16, muted, TextAnchor.MiddleLeft).text =
                OwnsShipyardVessel(index) ? "当前装配 · 右侧为部件加成" : "原装性能 · 购入后可改装";

            RectTransform description = CreateFixedPanel("Shipyard Description Panel", page, new Vector2(GameReadability.Level == 0 ? 220f : 316f, GameReadability.Level == 0 ? 306f : 354f), new Vector2(260f, -3f),
                new Color(0.84f, 0.67f, 0.28f, 0.16f));
            description.GetComponent<Image>().raycastTarget = false;
            ShipyardText("Shipyard Description Heading", description, new Vector2(190f, 34f), new Vector2(0f, 126f), 22, muted).text = "船只描述";
            ShipyardText("Shipyard Dossier Specialization", description, new Vector2(GameReadability.Level == 0 ? 184f : 280f, GameReadability.Level == 0 ? 205f : 250f), new Vector2(0f, 2f), 24, ink, TextAnchor.UpperLeft).text = CoreShipMarketCatalog.Specialty(index);
            ShipyardText("Shipyard Description Footer", description, new Vector2(190f, 28f), new Vector2(0f, -126f), 15, muted).text = "船型档案 · " + (index + 1) + " / " + CoreVoyageSession.Ships.Count;
            description.sizeDelta = new Vector2(290f, 310f);
            description.anchoredPosition = new Vector2(240f, 0f);
            WrapShipyardDossierText(description, "Shipyard Dossier Specialization");
            ReflowShipyardStats(stats);
        }

        private static float ShipyardUnmodifiedStatMaximum(int stat)
        {
            float maximum = 0f;
            foreach (CoreShipDefinition ship in CoreVoyageSession.Ships)
            {
                float value = stat == 0 ? ship.MaxHull : stat == 1 ? ship.Armor : stat == 2 ? ship.Firepower :
                    stat == 3 ? ship.SpeedKnots : ship.CargoCapacity;
                maximum = Mathf.Max(maximum, value);
            }
            return maximum;
        }
        private static string FormatShipyardStatValue(int stat, float value)
        {
            if (stat == 3) return Mathf.Max(2.5f, value).ToString("0.#") + " 节";
            return Mathf.Max(0f, value).ToString("0.#");
        }

        private static string FormatShipyardStatDelta(int stat, float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return string.Empty;
            string sign = delta > 0f ? "+" : string.Empty;
            return sign + (stat == 3 ? delta.ToString("0.#") : Mathf.RoundToInt(delta).ToString());
        }
        private void RefreshShipyardDossierView()
        {
            if (Application.isPlaying) Render();
            else
            {
                editorPreviewShipIndex = selectedShipPreview;
                editorPreviewShipPage = (int)shipyardDossierTab;
                editorPreviewScreen = shipyardDetailOpen ? CoreGameplayPreviewScreen.ShipyardDetail : CoreGameplayPreviewScreen.Shipyard;
                RefreshEditorPreview();
            }
        }

        private void ConfirmShipyardVessel(int index)
        {
            // Recheck state as well as button interactivity: stale or repeated
            // callbacks must not buy another vessel or charge a second time.
            ShipyardVesselQuote quote = shipyardVesselQuote;
            if (!Application.isPlaying || editorPreviewBuilding || session == null ||
                quote == null || quote.Consumed || !ReferenceEquals(quote.Session, session) || quote.Port != session.CurrentPort ||
                quote.ShipIndex != index || quote.Revision != shipyardViewRevision ||
                quote.View == null || !quote.View.gameObject.activeInHierarchy ||
                session.Phase != CoreDemoPhase.Port || activePortFacility != 1 ||
                !shipyardDetailOpen || index != selectedShipPreview || index < 0 || index >= CoreVoyageSession.Ships.Count ||
                index == session.SelectedShipIndex) return;
            if (!session.CanSelectShip(index))
            {
                portNotice = "货物超过舱容，请先存仓或出售。金币未扣除。";
                Render();
                return;
            }
            bool owned = OwnsShipyardVessel(index);
            if (!owned && (!CoreShipMarketCatalog.IsSoldAtPort(ShipyardPortIndex(), index) ||
                quote.Price != CoreShipMarketCatalog.PurchaseCost(index))) return;
            quote.Consumed = true;
            if (!owned)
            {
                if (!session.TrySpendProfileGold(quote.Price, "船坞购船")) { Render(); return; }
                PlayerPrefs.SetInt(ShipOwnedKey(index), 1);
                PlaySfx("shipyard_equip", 0.56f);
            }
            EnsureShipComponentLoadout(index);
            session.SelectShip(index);
            RefreshComponentBonuses();
            SaveProgress();
            portNotice = (owned ? "已切换旗舰为「" : "已购入并启用「") + CoreVoyageSession.Ships[index].Name + "」。";
            Render();
        }

        private static void WrapShipyardDossierText(RectTransform page, string name)
        {
            Text[] labels = page.GetComponentsInChildren<Text>(true).OrderByDescending(text => text.rectTransform.anchoredPosition.y).ToArray();
            if (labels.Length == 0) return;
            Image hit = page.GetComponent<Image>() ?? page.gameObject.AddComponent<Image>();
            hit.color = Color.clear; hit.raycastTarget = true;
            page.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = page.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = page; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            RectTransform content = new GameObject(name + " Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(page, false); content.anchorMin = content.anchorMax = new Vector2(.5f, 1f); content.pivot = new Vector2(.5f, 1f);
            float top = 12f;
            foreach (Text label in labels)
            {
                label.transform.SetParent(content, false);
                label.alignment = TextAnchor.UpperLeft;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.rectTransform.sizeDelta = new Vector2(page.rect.width - 24f, page.rect.height);
                float height = MeasureCardTextHeight(label);
                PlaceCardEffectParagraph(label, page.rect.width - 24f, height, top);
                top += height + 12f;
            }
            content.sizeDelta = new Vector2(page.rect.width, Mathf.Max(page.rect.height, top)); content.anchoredPosition = Vector2.zero;
            scroll.content = content; scroll.verticalNormalizedPosition = 1f;
        }

        private static void ReflowShipyardStats(RectTransform stats)
        {
            stats.sizeDelta = new Vector2(450f, 310f);
            stats.anchoredPosition = new Vector2(-135f, 0f);
            stats.GetComponent<CanvasGroup>().blocksRaycasts = true;
            RectTransform content = new GameObject("Shipyard Performance Stats Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(stats, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            float step = 100f * GameReadability.Scale;
            for (int index = 0; index < 5; index++)
            {
                string[] names = { "Shipyard Dossier Stat ", "Shipyard Stat Delta ", "Shipyard Stat Maximum ", "Shipyard Stat Bar ", "Shipyard Stat Bar Fill " };
                foreach (string prefix in names)
                {
                    RectTransform row = stats.Find(prefix + index) as RectTransform;
                    if (row == null) continue;
                    row.SetParent(content, false);
                    row.anchorMin = row.anchorMax = new Vector2(.5f, 1f);
                    float y = -12f - index * step;
                    if (prefix.Contains("Dossier Stat")) { row.sizeDelta = new Vector2(416f, 40f * GameReadability.Scale); row.anchoredPosition = new Vector2(0f, y - 20f * GameReadability.Scale); }
                    else if (prefix.Contains("Delta")) { row.sizeDelta = new Vector2(100f, 32f * GameReadability.Scale); row.anchoredPosition = new Vector2(148f, y - 59f * GameReadability.Scale); }
                    else if (prefix.Contains("Maximum")) { row.sizeDelta = new Vector2(170f, 30f * GameReadability.Scale); row.anchoredPosition = new Vector2(-120f, y - 59f * GameReadability.Scale); }
                    else row.anchoredPosition = new Vector2(row.anchoredPosition.x, y - 87f * GameReadability.Scale);
                }
            }
            Text source = stats.Find("Shipyard Performance Source").GetComponent<Text>();
            source.transform.SetParent(content, false);
            source.rectTransform.sizeDelta = new Vector2(416f, 100f);
            float sourceHeight = MeasureCardTextHeight(source);
            PlaceCardEffectParagraph(source, 416f, sourceHeight, 5f * step + 12f);
            content.sizeDelta = new Vector2(450f, 5f * step + sourceHeight + 24f);
            content.anchoredPosition = Vector2.zero;
            Image hit = stats.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            stats.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = stats.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = stats; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 48f;
        }

        private Text ShipyardText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Text label = CreateText(name, parent, size, position, GameReadability.FontSize(Mathf.Max(fontSize, 18)), FontStyle.Normal, anchor, color);
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            if (anchor == TextAnchor.UpperLeft) label.lineSpacing = 1.22f;
            return label;
        }
    }
}
