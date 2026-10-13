using System;
using System.Collections.Generic;
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
            // Clip the base hull at the gallery waterline. The dossier is uncut.
            RectTransform waterClip = CreateRect("Shipyard Waterline Clip", vessel, new Vector2(920f, 575f), new Vector2(0f, 134.5f));
            waterClip.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 8);
            Graphic shipArt = BuildShipyardVessel(waterClip, index, waterline - waterClip.anchoredPosition, 1f, "Shipyard");
            Button inspect = vessel.gameObject.AddComponent<Button>();
            inspect.targetGraphic = shipArt;
            inspect.gameObject.name = "Shipyard Ship " + index;
            CreateShipyardWater("Shipyard Water Contact", panel, waterline, true);

            // The ship can be inspected regardless of price or ownership. The
            // gallery never invokes payment or silently switches the flagship.
            RectTransform plaque = CreateFixedPanel("Shipyard Gallery Plaque", panel, new Vector2(780f, 114f), new Vector2(0f, -337f), ScenePlaqueColor(0.91f));
            plaque.GetComponent<Image>().raycastTarget = false;
            Text title = ShipyardText("Shipyard Ship Name " + index, plaque, new Vector2(750f, 42f), new Vector2(0f, 30f), 30, Gold);
            title.text = CoreVoyageSession.Ships[index].Name;
            ShipyardText("Shipyard Gallery Role", plaque, new Vector2(750f, 28f), Vector2.zero, 20, Ink).text =
                CoreShipMarketCatalog.Role(index) + "　·　" + port + "售 " + CoreShipMarketCatalog.ShipsForPort(ShipyardPortIndex()).Count + " 型";
            ShipyardText("Shipyard Gallery Ownership", plaque, new Vector2(750f, 28f), new Vector2(0f, -32f), 19, Ink).text =
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
                    new Vector2(780f, 520f) * scale, waterline + new Vector2(0f, 210f * scale));
                RawImage hull = modular.Find(prefix + " Modular Ship Base").GetComponent<RawImage>();
                hull.raycastTarget = prefix == "Shipyard";
                if (prefix == "Shipyard") AlignShipHullToWaterline(modular, hull, waterline.y - 4f * scale);
                return hull;
            }

            // All redrawn cells use the same landscape painting proportions.
            // Preserve those proportions in both the gallery and dossier.
            Texture2D texture = portArt.ShipTexture(index);
            Rect uv = portArt.ShipUv(index);
            float aspect = (texture.width * uv.width) / (texture.height * uv.height);
            Vector2 baseSize = new Vector2(Mathf.Min(650f, 560f * aspect), Mathf.Min(560f, 650f / aspect));
            RectTransform composite = CreateRect(prefix + " Composite Ship " + index, parent,
                baseSize * scale, waterline + new Vector2(0f, baseSize.y * (250f / 585f) * scale));
            float ix = 10f / texture.width;
            float iy = 10f / texture.height;
            uv = new Rect(uv.x + ix, uv.y + iy, uv.width - ix * 2f, uv.height - iy * 2f);
            RawImage ship = CreateRawImage(prefix + " Ship Art " + index, composite,
                baseSize * scale, Vector2.zero, texture, uv);
            ship.color = portArt.ShipTint(index);
            BattleShipCutout.Attach(ship);
            ship.raycastTarget = prefix == "Shipyard";
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
            Image paper = CreateImage("Shipyard Dossier Parchment", detail, new Vector2(1620f, 820f), Vector2.zero, Color.white);
            NauticalSurfaceSkin.ApplyReadingPanel(paper);
            paper.raycastTarget = false;
            Color ink = Html("#302116");
            Color muted = Html("#76502B");
            CoreShipDefinition definition = CoreVoyageSession.Ships[index];
            ShipyardText("Shipyard Dossier Name", detail, new Vector2(1440f, 55f), new Vector2(0f, 287f), 36, ink, TextAnchor.MiddleLeft).text = definition.Name;
            ShipyardText("Shipyard Dossier Role", detail, new Vector2(1440f, 34f), new Vector2(0f, 242f), 23, muted, TextAnchor.MiddleLeft).text =
                CoreShipMarketCatalog.Role(index) + "　·　所属港：" + ShipyardVesselHomePort(index);
            string[] tabLabels = { "船只性能", "船只背景", "配件装配" };
            for (int tabIndex = 0; tabIndex < tabLabels.Length; tabIndex++)
            {
                ShipyardDossierTab tab = (ShipyardDossierTab)tabIndex;
                Button tabButton = CreateButton("Shipyard Dossier Tab " + tab, detail, new Vector2(440f, 46f),
                    new Vector2(-486f + 486f * tabIndex, 192f), Gold, Ink, 25);
                tabButton.GetComponentInChildren<Text>().text = tabLabels[tabIndex];
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
                BuildShipyardVessel(detail, index, new Vector2(-468f, -151f), 0.60f, "Shipyard Dossier");
                if (shipyardDossierTab == ShipyardDossierTab.Background)
                {
                    RectTransform storyPage = CreateRect("Shipyard Background Page", detail, new Vector2(750f, 350f), new Vector2(326f, -20f));
                    ShipyardText("Shipyard Dossier Story Title", storyPage, new Vector2(750f, 40f), new Vector2(0f, 130f), 30, muted, TextAnchor.MiddleLeft).text = "船坞旧闻";
                    ShipyardText("Shipyard Dossier Story", storyPage, new Vector2(750f, 240f), new Vector2(0f, -28f), 29, ink, TextAnchor.UpperLeft).text =
                        string.IsNullOrWhiteSpace(definition.Lore) ? ShipyardHistories[CoreShipMarketCatalog.ArtworkIndex(index)] : definition.Lore;
                }
                else BuildShipyardPerformancePage(detail, index, ink, muted);
            }
            bool owned = OwnsShipyardVessel(index);
            bool selected = index == session.SelectedShipIndex;
            bool local = CoreShipMarketCatalog.IsSoldAtPort(ShipyardPortIndex(), index);
            int purchaseCost = CoreShipMarketCatalog.PurchaseCost(index);
            bool affordable = session.ProfileGold >= purchaseCost;
            bool cargoFits = session.CanSelectShip(index);
            Button back = CreateButton(HasWorkshopSpecialCardReturn ? "Back To Workshop Special Cards" :
                shipyardReturnToDeparture ? "Back To Voyage Departure" : "Back To Shipyard Gallery", detail,
                new Vector2(236f, 42f), new Vector2(-594f, 338f), Html("#6D4B2C"), Ink, 23);
            back.GetComponentInChildren<Text>().text = HasWorkshopSpecialCardReturn ? "返回特殊卡牌" :
                shipyardReturnToDeparture ? "返回出航码头" : "返回船坞";
            back.onClick.AddListener(() =>
            {
                if (!IsCurrentShipyardControl(back, owner, port, revision)) return;
                shipyardDetailOpen = false;
                ResetShipyardEquipmentSelection();
                if (TryReturnToWorkshopSpecialCards()) return;
                if (shipyardReturnToDeparture) ReturnFromDepartureDossier();
                else RefreshShipyardDossierView();
            });
            // Refitting has its own action beneath the comparison cards. Ship
            // purchasing and flagship selection remain on the other dossier tabs.
            if (shipyardDossierTab == ShipyardDossierTab.Equipment) return;
            ShipyardText("Shipyard Dossier Price", detail, new Vector2(1440f, 32f), new Vector2(0f, -230f), 22, ink, TextAnchor.MiddleRight).text =
                !cargoFits ? "货舱不足 · 请先将货物存入本港仓库或出售" :
                (owned ? local ? "已拥有" : "已拥有外港船 · 可在此设为旗舰或改装" :
                    local ? "购船价 " + purchaseCost + " 金币" : "本港不出售此船型 · 所属港：" + ShipyardVesselHomePort(index));
            Button confirm = CreateButton("Confirm Shipyard Vessel", detail, new Vector2(414f, 54f), new Vector2(471f, -284f), Html("#28594E"), Ink, 23);
            confirm.GetComponentInChildren<Text>().text = selected ? "当前旗舰" : !cargoFits ? "货物超过该船舱容" : owned ? "设为旗舰" :
                !local ? "本港不出售此船型" : affordable ? "购买并启用 · " + purchaseCost + " 金币" :
                "金币不足 · 还需 " + (purchaseCost - session.ProfileGold);
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
                    if (installed == null) continue;
                    ComponentArtEntry baseline = componentArt.Get(slot * ComponentArtCatalog.EntriesPerSlot);
                    values[0] += installed.HullBonus - baseline.HullBonus;
                    values[1] += installed.ArmorBonus - baseline.ArmorBonus;
                    values[2] += installed.FirepowerBonus - baseline.FirepowerBonus;
                    values[3] += installed.SpeedBonus - baseline.SpeedBonus;
                    deltas[0] += installed.HullBonus - baseline.HullBonus;
                    deltas[1] += installed.ArmorBonus - baseline.ArmorBonus;
                    deltas[2] += installed.FirepowerBonus - baseline.FirepowerBonus;
                    deltas[3] += installed.SpeedBonus - baseline.SpeedBonus;
                }

            RectTransform page = CreateRect("Shipyard Performance Page", detail, new Vector2(750f, 350f), new Vector2(326f, -20f));
            RectTransform stats = CreateRect("Shipyard Performance Stats", page, new Vector2(900f, 330f), new Vector2(-145f, -2f));
            stats.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            string[] captions = { "船体上限", "装甲", "火力", "航速", "货舱容量" };
            float barWidth = 500f;
            float barX = 98f;
            for (int stat = 0; stat < captions.Length; stat++)
            {
                float y = 132f - stat * 58f;
                float value = stat == 0 ? Mathf.Max(1f, values[stat]) : Mathf.Max(0f, values[stat]);
                float maximum = ShipyardUnmodifiedStatMaximum(stat);
                bool reachesMaximum = value >= maximum - 0.001f;
                float ratio = Mathf.Clamp01(value / maximum);
                Color trackColor = new Color(0.12f, 0.09f, 0.06f, 0.72f);
                Color fillColor = reachesMaximum ? Html("#4C985D") : Html("#C6922E");
                Image track = CreateImage("Shipyard Stat Bar " + stat, stats, new Vector2(barWidth, 24f), new Vector2(barX, y), trackColor);
                track.raycastTarget = false;
                float innerWidth = barWidth - 4f;
                float fillWidth = Mathf.Max(4f, innerWidth * ratio);
                Image fill = CreateImage("Shipyard Stat Bar Fill " + stat, stats, new Vector2(fillWidth, 20f),
                    new Vector2(barX - (innerWidth - fillWidth) * 0.5f, y), fillColor);
                fill.raycastTarget = false;
                ShipyardText("Shipyard Dossier Stat " + stat, stats, new Vector2(192f, 32f), new Vector2(-243f, y), 23, ink, TextAnchor.MiddleLeft).text =
                    captions[stat] + "  " + FormatShipyardStatValue(stat, value);
                Text delta = ShipyardText("Shipyard Stat Delta " + stat, stats, new Vector2(78f, 30f), new Vector2(408f, y), 20,
                    fillColor, TextAnchor.MiddleLeft);
                delta.text = FormatShipyardStatDelta(stat, deltas[stat]);
                // Put the cap on a separate baseline below the delta.  The old
                // -17 offset overlapped the 30px delta label by several pixels,
                // which made values such as "+2 / 8" look doubled.
                Text maximumLabel = ShipyardText("Shipyard Stat Maximum " + stat, stats, new Vector2(86f, 22f), new Vector2(403f, y - 27f), 12,
                    muted, TextAnchor.MiddleLeft);
                maximumLabel.text = "/ " + FormatShipyardStatValue(stat, maximum).Replace(" 节", string.Empty);
            }
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

        private Text ShipyardText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Text label = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, anchor, color);
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            if (anchor == TextAnchor.UpperLeft) label.lineSpacing = 1.22f;
            return label;
        }
    }
}
