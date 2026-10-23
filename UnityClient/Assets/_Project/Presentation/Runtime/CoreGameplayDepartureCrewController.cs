using System;
using System.Collections.Generic;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string CrewLocationKeyPrefix = "ShatteredSail.CrewLocation.v1.";
        public const string CrewAtSeaKey = "ShatteredSail.CrewAtSea.v1";
        private const string CrewAtSeaLocation = "__AT_SEA__";
        private bool departureCrewConfigurationOpen;
        private int selectedDepartureOrdinaryCrew = -1;
        private int departureCrewShipIndex = -1;
        private string departureCrewPort = string.Empty;
        private readonly List<string> selectedDepartureSpecialCrew = new List<string>();
        private readonly HashSet<string> departureStandbyCrew = new HashSet<string>(StringComparer.Ordinal);
        private int departureCrewViewRevision;
        private int departureCrewRosterRevision;

        public static string CrewLocationKeyForReset(string crewId)
        {
            return CrewLocationKey(crewId);
        }
        private static string CrewLocationKey(string crewId)
        {
            return CrewLocationKeyPrefix + (string.IsNullOrWhiteSpace(crewId) ? "unknown" : crewId.Trim());
        }

        private string CrewLocation(CrewVariantDefinition crew)
        {
            if (crew == null) return string.Empty;
            string location = PlayerPrefs.GetString(CrewLocationKey(crew.Id), string.Empty);
            return string.IsNullOrWhiteSpace(location) ? crew.PortName : location.Trim();
        }

        private bool HasRecruitedCrewAwayFromCurrentPort(int profession)
        {
            if (!Application.isPlaying || editorPreviewBuilding) return false;
            foreach (CrewVariantDefinition candidate in CrewNarrativeCatalog.AllCrew)
            {
                if (candidate.ProfessionIndex != profession || !IsCrewRecruited(candidate)) continue;
                if (!IsCrewAvailableAtCurrentPort(candidate)) return true;
            }
            return false;
        }
        private bool IsCrewAvailableAtCurrentPort(CrewVariantDefinition crew)
        {
            if (crew == null || !IsCrewRecruited(crew)) return false;
            if (!Application.isPlaying || editorPreviewBuilding) return true;
            // Older saves (and saves written before the regional roster gained
            // location records) do not have a location key. A recruited active
            // identity is the crew member represented by the captain's current
            // roster, so keep it available only while its location is unknown.
            // An explicit port or at-sea marker must be respected.
            if (!PlayerPrefs.HasKey(CrewLocationKey(crew.Id))) return true;
            string location = CrewLocation(crew);
            if (string.Equals(location, CrewAtSeaLocation, StringComparison.Ordinal)) return false;
            return string.Equals(location, session.CurrentPort, StringComparison.Ordinal);
        }

        private List<CrewVariantDefinition> AvailableDepartureCrew()
        {
            var result = new List<CrewVariantDefinition>();
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                if (IsCrewAvailableAtCurrentPort(crew)) result.Add(crew);
            return result;
        }
        private void EnsureDepartureCrewSelection()
        {
            if (session == null) return;
            bool changed = departureCrewShipIndex != session.SelectedShipIndex ||
                !string.Equals(departureCrewPort, session.CurrentPort, StringComparison.Ordinal) ||
                selectedDepartureOrdinaryCrew < 0;
            if (changed)
            {
                departureCrewShipIndex = session.SelectedShipIndex;
                departureCrewPort = session.CurrentPort;
                selectedDepartureOrdinaryCrew = -1;
                selectedDepartureSpecialCrew.Clear();
                departureStandbyCrew.Clear();
            }

            var roster = AvailableDepartureCrew();
            var availableCrew = new HashSet<string>(StringComparer.Ordinal);
            foreach (CrewVariantDefinition crew in roster) availableCrew.Add(crew.Id);
            selectedDepartureSpecialCrew.RemoveAll(id => !availableCrew.Contains(id));
            while (selectedDepartureSpecialCrew.Count > session.CrewCountValue)
                selectedDepartureSpecialCrew.RemoveAt(selectedDepartureSpecialCrew.Count - 1);
            foreach (CrewVariantDefinition crew in roster)
            {
                if (departureStandbyCrew.Contains(crew.Id) || selectedDepartureSpecialCrew.Contains(crew.Id)) continue;
                if (selectedDepartureSpecialCrew.Count < session.CrewCountValue) selectedDepartureSpecialCrew.Add(crew.Id);
                else departureStandbyCrew.Add(crew.Id);
            }
            int maximumOrdinary = Math.Max(0, session.CrewCountValue - selectedDepartureSpecialCrew.Count);
            if (selectedDepartureOrdinaryCrew < 0) selectedDepartureOrdinaryCrew = maximumOrdinary;
            selectedDepartureOrdinaryCrew = Mathf.Clamp(selectedDepartureOrdinaryCrew, 0, maximumOrdinary);
        }

        private int DepartureSpecialCrewCount()
        {
            EnsureDepartureCrewSelection();
            return selectedDepartureSpecialCrew.Count;
        }

        private void ApplyDepartureCrewConfiguration()
        {
            EnsureDepartureCrewSelection();
            session.ConfigureDepartureCrew(selectedDepartureOrdinaryCrew, selectedDepartureSpecialCrew.Count);
            session.ConfigureCrewCapabilities(
                SelectedDepartureProfessionCount(0),
                HasSelectedDepartureProfession(1),
                HasSelectedDepartureProfession(2),
                HasSelectedDepartureProfession(3),
                HasSelectedDepartureProfession(4),
                HasSelectedDepartureProfession(5));
            RefreshSpecialCardSources();
        }

        private int SelectedDepartureProfessionCount(int profession)
        {
            int count = 0;
            foreach (string id in selectedDepartureSpecialCrew)
                if (CrewNarrativeCatalog.FindById(id)?.ProfessionIndex == profession) count++;
            return count;
        }

        private bool HasSelectedDepartureProfession(int profession) => SelectedDepartureProfessionCount(profession) > 0;

        private void PersistCrewAtSea()
        {
            if (!Application.isPlaying || editorPreviewBuilding) return;
            List<string> atSea = new List<string>();
            foreach (string id in selectedDepartureSpecialCrew)
            {
                CrewVariantDefinition crew = CrewNarrativeCatalog.FindById(id);
                if (crew == null || !IsCrewRecruited(crew)) continue;
                PlayerPrefs.SetString(CrewLocationKey(id), CrewAtSeaLocation);
                atSea.Add(id);
            }
            PlayerPrefs.SetString(CrewAtSeaKey, string.Join(",", atSea));
            PlayerPrefs.Save();
        }

        private void RecoverCrewLocationsAfterReload()
        {
            if (!Application.isPlaying || editorPreviewBuilding || !PlayerPrefs.HasKey(CrewAtSeaKey)) return;
            // Restore the baseline port profile first. Explicitly suspended
            // voyages reapply their selected crew's at-sea locations after the
            // checkpoint is accepted; other reloads cannot strand a crew member.
            string csv = PlayerPrefs.GetString(CrewAtSeaKey, string.Empty);
            foreach (string id in csv.Split(','))
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                PlayerPrefs.SetString(CrewLocationKey(id.Trim()), session.CurrentPort);
            }
            PlayerPrefs.DeleteKey(CrewAtSeaKey);
            PlayerPrefs.Save();
        }
        private void CommitCrewLocationsAfterReturn() => CommitCrewLocationsAfterReturnInternal(true);

        private void CommitCrewLocationsAfterReturnInternal(bool flush)
        {
            if (!Application.isPlaying || editorPreviewBuilding) return;
            string csv = PlayerPrefs.GetString(CrewAtSeaKey, string.Empty);
            foreach (string id in csv.Split(','))
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                PlayerPrefs.SetString(CrewLocationKey(id.Trim()), session.CurrentPort);
            }
            PlayerPrefs.DeleteKey(CrewAtSeaKey);
            if (flush) PlayerPrefs.Save();
            selectedDepartureOrdinaryCrew = -1;
            departureCrewShipIndex = -1;
            departureCrewPort = string.Empty;
            selectedDepartureSpecialCrew.Clear();
        }

        private string DepartureOrdinaryCrewLabel() =>
            "普通船员 " + selectedDepartureOrdinaryCrew + "/" + session.CrewCountValue +
            "　特殊 " + selectedDepartureSpecialCrew.Count + "（普通上限 " +
            Math.Max(0, session.CrewCountValue - selectedDepartureSpecialCrew.Count) + "）";

        private string DepartureCrewSpeedSummary()
        {
            float penalty = session.GetCrewSpeedPenalty(selectedDepartureOrdinaryCrew);
            float bonus = session.GetCrewSpeedBonus(selectedDepartureOrdinaryCrew);
            string effect = penalty > 0f ? "航速 -" + penalty.ToString("0.0") + " 节 · 战斗 -" +
                ((session.MinimumOrdinaryCrew - selectedDepartureOrdinaryCrew) * 2) :
                bonus > 0f ? "航速 +" + bonus.ToString("0.0") + " 节" : "无船员航速增减";
            return "航速 " + session.GetEffectiveSpeedKnots(selectedDepartureOrdinaryCrew).ToString("0.0") +
                " 节 · " + effect;
        }

        private void BuildDepartureOrdinaryCrewControls(RectTransform panel)
        {
            Text count = CreateText("DepartureOrdinaryCrewCount", panel, new Vector2(436f, 32f),
                new Vector2(0f, 148f), 17, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            count.raycastTarget = false;
            Text speed = CreateText("DepartureCrewSpeedPreview", panel, new Vector2(552f, 28f),
                new Vector2(0f, 114f), 15, FontStyle.Normal, TextAnchor.MiddleCenter, MutedInk);
            speed.raycastTarget = false;
            for (int direction = -1; direction <= 1; direction += 2)
            {
                Button adjust = CreateButton(direction < 0 ? "DepartureOrdinaryCrewDecreaseButton" : "DepartureOrdinaryCrewIncreaseButton",
                    panel, new Vector2(40f, 32f), new Vector2(direction * 252f, 148f), PanelRaised, Gold, 21);
                adjust.GetComponentInChildren<Text>().text = direction < 0 ? "−" : "+";
            }
            RefreshDepartureOrdinaryCrewControls(panel);
        }

        private void RefreshDepartureOrdinaryCrewControls(RectTransform panel)
        {
            EnsureDepartureCrewSelection();
            int revision = ++departureCrewViewRevision;
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            int ship = session.SelectedShipIndex;
            int chartRevision = departureChartRevision;
            Text count = panel.Find("DepartureOrdinaryCrewCount")?.GetComponent<Text>();
            if (count != null) count.text = DepartureOrdinaryCrewLabel();
            Text speed = panel.Find("DepartureCrewSpeedPreview")?.GetComponent<Text>();
            if (speed != null)
            {
                speed.text = DepartureCrewSpeedSummary();
                speed.color = session.GetCrewSpeedPenalty(selectedDepartureOrdinaryCrew) > 0f ? Coral : MutedInk;
            }
            for (int direction = -1; direction <= 1; direction += 2)
            {
                int change = direction;
                Button adjust = panel.Find(direction < 0 ? "DepartureOrdinaryCrewDecreaseButton" : "DepartureOrdinaryCrewIncreaseButton")?.GetComponent<Button>();
                if (adjust == null) continue;
                int maximum = Math.Max(0, session.CrewCountValue - selectedDepartureSpecialCrew.Count);
                adjust.interactable = Application.isPlaying && !editorPreviewBuilding &&
                    (direction < 0 ? selectedDepartureOrdinaryCrew > 0 : selectedDepartureOrdinaryCrew < maximum);
                // A retained button must capture the newly selected vessel. A saved
                // callback from the previous binding still fails its revision guard.
                adjust.onClick = new Button.ButtonClickedEvent();
                adjust.onClick.AddListener(() =>
                {
                    if (!IsCurrentDepartureOrdinaryCrewControl(adjust, panel, owner, port, ship, revision, chartRevision)) return;
                    EnsureDepartureCrewSelection();
                    int currentMaximum = Math.Max(0, session.CrewCountValue - selectedDepartureSpecialCrew.Count);
                    int next = Mathf.Clamp(selectedDepartureOrdinaryCrew + change, 0, currentMaximum);
                    if (next == selectedDepartureOrdinaryCrew) return;
                    CommitDepartureSeed(panel);
                    selectedDepartureOrdinaryCrew = next;
                    ApplyDepartureCrewConfiguration();
                    Render();
                });
            }
        }
        private bool IsCurrentDepartureOrdinaryCrewControl(Button control, RectTransform panel,
            CoreVoyageSession owner, string port, int ship, int revision, int chartRevision) =>
            Application.isPlaying && !editorPreviewBuilding && control != null && control.gameObject.activeInHierarchy &&
            control.interactable && IsDepartureShipView() && !departureCrewConfigurationOpen &&
            ReferenceEquals(session, owner) && session.CurrentPort == port && session.SelectedShipIndex == ship &&
            departureCrewViewRevision == revision && DepartureChartViewCurrent(panel, owner, chartRevision);

        private void BuildDepartureCrewConfiguration(RectTransform setupPanel)
        {
            EnsureDepartureCrewSelection();
            CloseDepartureBasicSupplies();
            int revision = ++departureCrewViewRevision;
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            int ship = session.SelectedShipIndex;
            RectTransform overlay = CreateFixedPanel("Departure Crew Overlay", contentRoot,
                PhaseSurfaceLayout.ContentSize, Vector2.zero, new Color(.025f, .018f, .012f, .68f));
            Image shade = overlay.GetComponent<Image>();
            shade.sprite = shade.overrideSprite = null;
            shade.type = Image.Type.Simple;
            shade.color = new Color(.025f, .018f, .012f, .68f);
            shade.raycastTarget = true;
            overlay.SetAsLastSibling();
            // Keep the dock visible while excluding its controls from both
            // pointer input and automatic keyboard/controller navigation.
            overlay.gameObject.AddComponent<DepartureCrewInputBarrier>().Bind(contentRoot);
            RectTransform card = CreateFixedPanel("Departure Crew Configuration", overlay,
                new Vector2(840f, 500f), new Vector2(24f, -40f), PanelRaised);
            MarketShopSkin.ApplyPanel(card.GetComponent<Image>());
            Color ink = MarketShopSkin.Ink;
            CreateText("Departure Crew Configuration Title", card, new Vector2(740f, 42f),
                new Vector2(0f, 202f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, ink).text = "船员配置 · " + port;
            WarehouseDialogRule("Departure Crew Header Rule", card, 748f, 174f);
            WarehouseDialogRule("Departure Crew Footer Rule", card, 748f, -138f);
            Text total = CreateText("Departure Crew Total", card, new Vector2(748f, 28f),
                new Vector2(0f, -158f), 17, FontStyle.Bold, TextAnchor.MiddleCenter, ink);

            RectTransform scrollRoot = CreateRect("Departure Special Crew Scroll", card,
                new Vector2(748f, 300f), new Vector2(0f, 20f));
            scrollRoot.gameObject.AddComponent<Image>().color = Color.clear;
            ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            RectTransform viewport = CreateRect("Viewport", scrollRoot, Vector2.zero, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-12f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform roster = CreateRect("Content", viewport, new Vector2(0f, 300f), Vector2.zero);
            roster.anchorMin = new Vector2(0f, 1f);
            roster.anchorMax = Vector2.one;
            roster.pivot = new Vector2(.5f, 1f);
            scroll.viewport = viewport;
            scroll.content = roster;
            RectTransform bar = CreateRect("Scrollbar", scrollRoot, new Vector2(8f, 0f), new Vector2(-4f, 0f));
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = Vector2.one;
            bar.gameObject.AddComponent<Image>().color = new Color(.25f, .16f, .08f, .15f);
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            RectTransform handle = CreateRect("Handle", bar, Vector2.zero, Vector2.zero);
            Image thumb = handle.gameObject.AddComponent<Image>();
            thumb.color = new Color(.35f, .25f, .13f, .75f);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = thumb;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            Action refreshLabels = () =>
            {
                ApplyDepartureCrewConfiguration();
                RefreshDepartureStartReadiness();
                total.text = "普通 " + selectedDepartureOrdinaryCrew + "  ·  特殊 " +
                    selectedDepartureSpecialCrew.Count + "  /  承载 " + session.CrewCountValue;
                if (!session.HasEnoughOrdinaryCrewForDeparture)
                    total.text += "　出航至少需 " + session.MinimumDepartureOrdinaryCrew + " 名普通船员（40%）";
                total.color = session.HasEnoughOrdinaryCrewForDeparture ? ink : new Color(.57f, .15f, .08f);
                if (headerStatus != null) headerStatus.text = BuildHeaderStatus();
            };
            Action rebuildRoster = null;
            rebuildRoster = () =>
            {
                int rosterRevision = ++departureCrewRosterRevision;
                float position = scroll.verticalNormalizedPosition;
                for (int childIndex = roster.childCount - 1; childIndex >= 0; childIndex--)
                {
                    Transform child = roster.GetChild(childIndex);
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
                }
                List<CrewVariantDefinition> available = AvailableDepartureCrew();
                roster.sizeDelta = new Vector2(0f, Mathf.Max(300f, ((available.Count + 2) / 3) * 150f));
                if (available.Count == 0)
                {
                    Text empty = CreateText("Departure Special Crew Empty", roster, new Vector2(690f, 80f),
                        new Vector2(0f, -150f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
                    empty.rectTransform.anchorMin = empty.rectTransform.anchorMax = new Vector2(.5f, 1f);
                    empty.text = "当前港口暂无已招募的特殊船员\n可前往酒馆招募，或等待其他船员随航返回";
                }
                for (int index = 0; index < available.Count; index++)
                {
                    CrewVariantDefinition crew = available[index];
                    bool selected = selectedDepartureSpecialCrew.Contains(crew.Id);
                    Button toggle = CreateButton("Departure Special Crew Toggle " + index, roster,
                        new Vector2(234f, 142f), new Vector2(-246f + index % 3 * 246f, -75f - index / 3 * 150f),
                        PanelRaised, ink, 16);
                    MarketShopSkin.ApplyLightButton(toggle, selected);
                    RectTransform row = (RectTransform)toggle.transform;
                    row.anchorMin = row.anchorMax = new Vector2(.5f, 1f);
                    toggle.GetComponentInChildren<Text>().text = string.Empty;
                    Image portraitFrame = CreateImage("Departure Special Crew Portrait Frame " + index, row,
                        new Vector2(90f, 118f), new Vector2(-65f, 0f), Color.white, WoodButtonSkin.Face);
                    portraitFrame.type = Image.Type.Sliced;
                    portraitFrame.pixelsPerUnitMultiplier = 28f;
                    portraitFrame.raycastTarget = false;
                    RawImage portrait = CreateRawImage("Departure Special Crew Portrait " + index, row,
                    new Vector2(82f, 108f), new Vector2(-65f, 0f), portArt.DepartureCrewPortrait(crew), portArt.DepartureCrewPortraitUv(crew));
                    portrait.raycastTarget = false;
                    FitCrewPortrait(portrait, new Vector2(82f, 108f));
                    portraitFrame.rectTransform.sizeDelta = portrait.rectTransform.sizeDelta + new Vector2(8f, 8f);
                    Text name = CreateText("Departure Special Crew Name " + index, row, new Vector2(120f, 48f),
                        new Vector2(48f, 40f), 18, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
                    name.text = crew.DisplayName.Replace(" ", "\n");
                    name.resizeTextForBestFit = true;
                    name.resizeTextMinSize = 16;
                    name.resizeTextMaxSize = 18;
                    Text profession = CreateText("Departure Special Crew Profession " + index, row, new Vector2(120f, 24f),
                        new Vector2(48f, -2f), 16, FontStyle.Normal, TextAnchor.MiddleLeft, MarketShopSkin.MutedInk);
                    profession.text = new[] { "航海士", "木匠", "船医", "炮术官", "厨师", "潜水员" }[crew.ProfessionIndex];
                    Color stateColor = selected ? new Color(.14f, .31f, .25f) : MarketShopSkin.MutedInk;
                    Text state = CreateText("Departure Special Crew State " + index, row, new Vector2(120f, 26f),
                        new Vector2(48f, -44f), 17, FontStyle.Bold, TextAnchor.MiddleLeft, stateColor);
                    state.text = selected ? "✓ 已登船" : selectedDepartureSpecialCrew.Count >= session.CrewCountValue
                        ? "满载" : "待命";
                    Text identity = CreateText("Departure Special Crew Identity", row, Vector2.one, Vector2.zero,
                        1, FontStyle.Normal, TextAnchor.MiddleCenter, Color.clear);
                    identity.text = crew.Id;
                    identity.gameObject.SetActive(false);
                    toggle.onClick.AddListener(() =>
                    {
                        if (!IsCurrentDepartureCrewControl(toggle, owner, port, ship, revision) ||
                            rosterRevision != departureCrewRosterRevision || !IsCrewAvailableAtCurrentPort(crew)) return;
                        EnsureDepartureCrewSelection();
                        if (selectedDepartureSpecialCrew.Remove(crew.Id)) departureStandbyCrew.Add(crew.Id);
                        else if (selectedDepartureSpecialCrew.Count < session.CrewCountValue)
                        {
                            departureStandbyCrew.Remove(crew.Id);
                            selectedDepartureSpecialCrew.Add(crew.Id);
                        }
                        refreshLabels();
                        rebuildRoster();
                        Button selectedCrew = DepartureCrewChoiceForIdentity(roster, crew.Id);
                        if (selectedCrew != null && EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(selectedCrew.gameObject);
                    });
                }
                bar.gameObject.SetActive(roster.sizeDelta.y > 300.5f);
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = position;
            };
            Button back = CreateButton("Back To Voyage Setup", card, new Vector2(260f, 44f),
                new Vector2(-150f, -198f), PanelRaised, Ink, 17);
            MarketShopSkin.ApplyTextButton(back);
            back.GetComponentInChildren<Text>().text = HasWorkshopSpecialCardReturn ? "返回特殊卡牌" : "返回出航";
            Button confirm = CreateButton("Confirm Departure Crew", card, new Vector2(260f, 44f),
                new Vector2(150f, -198f), Gold, DeepSea, 17);
            confirm.GetComponentInChildren<Text>().text = "确认船员配置";
            MarketShopSkin.SetMode(confirm, true);
            Action close = () =>
            {
                departureCrewConfigurationOpen = false;
                departureCrewViewRevision++;
                overlay.gameObject.SetActive(false);
                ApplyDepartureCrewConfiguration();
                if (TryReturnToWorkshopSpecialCards()) return;
                Render();
                FocusDepartureCrewSetup();
            };
            back.onClick.AddListener(() => { if (IsCurrentDepartureCrewControl(back, owner, port, ship, revision)) close(); });
            confirm.onClick.AddListener(() =>
            {
                if (!IsCurrentDepartureCrewControl(confirm, owner, port, ship, revision)) return;
                portNotice = "船员配置已保存，特殊船员将随船抵达目的港。";
                close();
            });
            refreshLabels();
            rebuildRoster();
            scroll.verticalNormalizedPosition = 1f;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(back.gameObject);
        }

        private static Button DepartureCrewChoiceForIdentity(Transform roster, string identity)
        {
            foreach (Button button in roster.GetComponentsInChildren<Button>())
                foreach (Text label in button.GetComponentsInChildren<Text>(true))
                    if (label.name == "Departure Special Crew Identity" && label.text == identity) return button;
            return null;
        }

        private void FocusDepartureCrewSetup()
        {
            if (contentRoot == null || EventSystem.current == null) return;
            foreach (Button button in contentRoot.GetComponentsInChildren<Button>())
                if (button.name == "Configure Voyage Crew" && button.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    return;
                }
        }

        private bool IsCurrentDepartureCrewControl(Component control, CoreVoyageSession owner, string port, int ship, int revision) =>
            Application.isPlaying && !editorPreviewBuilding && control != null && control.gameObject.activeInHierarchy &&
            ReferenceEquals(session, owner) && session != null && session.Phase == CoreDemoPhase.Port &&
            activePortFacility == PortFacilityArtCatalog.VoyageFacilityIndex && departureCrewConfigurationOpen && session.CurrentPort == port &&
            session.SelectedShipIndex == ship && departureCrewViewRevision == revision;

        private void MarkCrewAtCurrentPortOnRecruitment(CrewVariantDefinition crew)
        {
            if (crew == null || !Application.isPlaying || editorPreviewBuilding) return;
            PlayerPrefs.SetString(CrewLocationKey(crew.Id), session.CurrentPort);
        }
    }

    // The overlay shares its parent with the dock to preserve authored geometry.
    // A sibling barrier restores itself even when a render or scene teardown
    // disables the overlay without using either of its close buttons.
    internal sealed class DepartureCrewInputBarrier : MonoBehaviour
    {
        private Transform surface;
        private readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        private readonly List<bool> interactable = new List<bool>();
        private readonly List<bool> raycasts = new List<bool>();

        internal void Bind(Transform surface)
        {
            Restore();
            this.surface = surface;
            for (int index = 0; index < surface.childCount; index++)
            {
                Transform sibling = surface.GetChild(index);
                if (sibling == transform || !sibling.gameObject.activeSelf) continue;
                CanvasGroup group = sibling.GetComponent<CanvasGroup>();
                if (group == null) group = sibling.gameObject.AddComponent<CanvasGroup>();
                groups.Add(group);
                interactable.Add(group.interactable);
                raycasts.Add(group.blocksRaycasts);
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private void Restore()
        {
            for (int index = 0; index < groups.Count; index++)
                if (groups[index] != null)
                {
                    groups[index].interactable = interactable[index];
                    groups[index].blocksRaycasts = raycasts[index];
                }
            groups.Clear();
            interactable.Clear();
            raycasts.Clear();
        }

        private void OnEnable()
        {
            // Adding this component precedes Bind. A later hierarchy re-enable
            // reuses the same window and must reacquire its input barrier.
            if (surface == null) return;
            Bind(surface);
            if (EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform)) return;
            foreach (Button button in GetComponentsInChildren<Button>())
                if (button.name == "Back To Voyage Setup" && button.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    return;
                }
        }

        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
    }
}
