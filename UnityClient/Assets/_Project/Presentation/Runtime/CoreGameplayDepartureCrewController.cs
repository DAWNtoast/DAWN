using System;
using System.Collections.Generic;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
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

        private CrewVariantDefinition DepartureCrewForProfession(int profession)
        {
            CrewVariantDefinition active = ActiveCrewForProfession(profession);
            if (active != null && IsCrewAvailableAtCurrentPort(active)) return active;
            foreach (CrewVariantDefinition candidate in CrewNarrativeCatalog.AllCrew)
                if (candidate.ProfessionIndex == profession && IsCrewAvailableAtCurrentPort(candidate)) return candidate;
            return CurrentTavernCrew(profession);
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

            var availableCrew = new HashSet<string>(StringComparer.Ordinal);
            for (int profession = 0; profession < TavernCrewSlotCount; profession += 1)
            {
                CrewVariantDefinition crew = DepartureCrewForProfession(profession);
                if (crew != null && IsCrewAvailableAtCurrentPort(crew)) availableCrew.Add(crew.Id);
            }
            selectedDepartureSpecialCrew.RemoveAll(id => !availableCrew.Contains(id));
            foreach (string id in availableCrew)
                if (!departureStandbyCrew.Contains(id) && !selectedDepartureSpecialCrew.Contains(id) &&
                    selectedDepartureSpecialCrew.Count < Math.Min(TavernCrewSlotCount, session.CrewCountValue))
                    selectedDepartureSpecialCrew.Add(id);
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
                HasSelectedDepartureProfession(0),
                HasSelectedDepartureProfession(1),
                HasSelectedDepartureProfession(2),
                HasSelectedDepartureProfession(3),
                HasSelectedDepartureProfession(4),
                HasSelectedDepartureProfession(5));
            RefreshSpecialCardSources();
        }

        private bool HasSelectedDepartureProfession(int profession)
        {
            CrewVariantDefinition crew = DepartureCrewForProfession(profession);
            return crew != null && selectedDepartureSpecialCrew.Contains(crew.Id);
        }

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

        private string DepartureCrewSpeedHint() =>
            (session.GetCrewSpeedPenalty(selectedDepartureOrdinaryCrew) > 0f ? "普通船员不足半载 · " : "已达半载 · ") +
            DepartureCrewSpeedSummary() +
            "\n普通不足半编每缺 1 人 -0.8 节；严格超 80% 才加速，满编 +10%。";

        private void BuildDepartureOrdinaryCrewControls(RectTransform panel)
        {
            EnsureDepartureCrewSelection();
            int revision = ++departureCrewViewRevision;
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            int ship = session.SelectedShipIndex;
            int chartRevision = departureChartRevision;
            Text count = CreateText("DepartureOrdinaryCrewCount", panel, new Vector2(436f, 32f),
                new Vector2(0f, 148f), 17, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            count.text = DepartureOrdinaryCrewLabel();
            count.raycastTarget = false;
            Text speed = CreateText("DepartureCrewSpeedPreview", panel, new Vector2(552f, 28f),
                new Vector2(0f, 114f), 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                session.GetCrewSpeedPenalty(selectedDepartureOrdinaryCrew) > 0f ? Coral : MutedInk);
            speed.text = DepartureCrewSpeedSummary();
            speed.raycastTarget = false;

            for (int direction = -1; direction <= 1; direction += 2)
            {
                int change = direction;
                Button adjust = CreateButton(direction < 0 ? "DepartureOrdinaryCrewDecreaseButton" : "DepartureOrdinaryCrewIncreaseButton",
                    panel, new Vector2(40f, 32f), new Vector2(direction * 252f, 148f), PanelRaised, Gold, 21);
                adjust.GetComponentInChildren<Text>().text = direction < 0 ? "−" : "+";
                int maximum = Math.Max(0, session.CrewCountValue - selectedDepartureSpecialCrew.Count);
                adjust.interactable = Application.isPlaying && !editorPreviewBuilding &&
                    (direction < 0 ? selectedDepartureOrdinaryCrew > 0 : selectedDepartureOrdinaryCrew < maximum);
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
            int revision = ++departureCrewViewRevision;
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            int ship = session.SelectedShipIndex;
            Text title = CreateText("Departure Crew Configuration Title", setupPanel, new Vector2(540f, 44f),
                new Vector2(0f, 254f), 23, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            title.text = "船员配置";

            Text ordinary = CreateText("Departure Ordinary Crew Label", setupPanel, new Vector2(540f, 40f),
                new Vector2(0f, 205f), 17, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            ordinary.text = DepartureOrdinaryCrewLabel();
            Slider ordinarySlider = CreateSlider("Departure Ordinary Crew Slider", setupPanel,
                new Vector2(470f, 30f), new Vector2(-12f, 160f), 0f, Math.Max(0, session.CrewCountValue - selectedDepartureSpecialCrew.Count), selectedDepartureOrdinaryCrew);
            ordinarySlider.wholeNumbers = true;

            bool shortStaffed = session.GetCrewSpeedPenalty(selectedDepartureOrdinaryCrew) > 0f;
            Text ordinaryHint = CreateText("Departure Ordinary Crew Hint", setupPanel, new Vector2(540f, 44f),
                new Vector2(0f, 116f), 13, FontStyle.Bold, TextAnchor.MiddleLeft, shortStaffed ? Coral : MutedInk);
            ordinaryHint.text = DepartureCrewSpeedHint();

            Text special = CreateText("Departure Special Crew Heading", setupPanel, new Vector2(540f, 34f),
                new Vector2(0f, 78f), 16, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            special.text = "特殊船员　" + selectedDepartureSpecialCrew.Count + "/" + TavernCrewSlotCount +
                "　（登船后随航抵达新港）";

            for (int index = 0; index < TavernCrewSlotCount; index += 1)
            {
                int captured = index;
                CrewVariantDefinition crew = DepartureCrewForProfession(index);
                bool available = !Application.isPlaying || editorPreviewBuilding || IsCrewAvailableAtCurrentPort(crew);
                bool selected = crew != null && selectedDepartureSpecialCrew.Contains(crew.Id);
                float x = -205f + (index % 3) * 205f;
                float y = 23f - (index / 3) * 72f;
                Color color = selected ? new Color(0.20f, 0.42f, 0.38f, 0.92f) :
                    available ? ScenePlaqueColor(0.78f) : new Color(0.16f, 0.18f, 0.18f, 0.35f);
                Button toggle = CreateButton("Departure Special Crew Toggle " + index, setupPanel,
                    new Vector2(190f, 58f), new Vector2(x, y), color, selected ? Color.white : Ink, 12);
                toggle.GetComponentInChildren<Text>().text = string.Empty;
                toggle.interactable = available && crew != null;
                if (crew != null)
                {
                    RawImage portrait = CreateRawImage("Departure Special Crew Portrait " + index, toggle.transform,
                        new Vector2(42f, 48f), new Vector2(-67f, 0f), portArt.CrewPortrait(crew), portArt.CrewPortraitUv(crew, true));
                    portrait.raycastTarget = false;
                    FitCrewPortrait(portrait, new Vector2(42f, 48f));
                    Text name = CreateText("Departure Special Crew Name " + index, toggle.transform,
                        new Vector2(120f, 24f), new Vector2(22f, 10f), 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                        available ? (selected ? Color.white : Ink) : MutedInk);
                    name.text = crew.DisplayName;
                    Text state = CreateText("Departure Special Crew State " + index, toggle.transform,
                        new Vector2(120f, 20f), new Vector2(22f, -13f), 10, FontStyle.Normal, TextAnchor.MiddleLeft,
                        available ? Gold : MutedInk);
                    state.text = !IsCrewRecruited(crew) ? "未招募" : available ? (selected ? "已登船" : "待命") : "人在其他港口";
                }
                toggle.onClick.AddListener(() =>
                {
                    if (!IsCurrentDepartureCrewControl(toggle, owner, port, ship, revision) || !toggle.interactable) return;
                    CrewVariantDefinition selectedCrew = DepartureCrewForProfession(captured);
                    if (crew == null || selectedCrew == null || crew.Id != selectedCrew.Id || !IsCrewAvailableAtCurrentPort(selectedCrew)) return;
                    EnsureDepartureCrewSelection();
                    if (selectedDepartureSpecialCrew.Contains(selectedCrew.Id))
                    {
                        selectedDepartureSpecialCrew.Remove(selectedCrew.Id);
                        departureStandbyCrew.Add(selectedCrew.Id);
                    }
                    else if (selectedDepartureSpecialCrew.Count < Math.Min(TavernCrewSlotCount, session.CrewCountValue))
                    {
                        departureStandbyCrew.Remove(selectedCrew.Id);
                        selectedDepartureSpecialCrew.Add(selectedCrew.Id);
                    }
                    Render();
                });
            }

            Text total = CreateText("Departure Crew Total", setupPanel, new Vector2(540f, 34f),
                new Vector2(0f, -128f), 14, FontStyle.Bold, TextAnchor.MiddleCenter, MutedInk);
            total.text = "本次出航编组　普通 " + selectedDepartureOrdinaryCrew + "　+　特殊 " +
                selectedDepartureSpecialCrew.Count + "　/　船只最大承载 " + session.CrewCountValue;
            ordinarySlider.onValueChanged.AddListener(value =>
            {
                if (!IsCurrentDepartureCrewControl(ordinarySlider, owner, port, ship, revision) ||
                    float.IsNaN(value) || float.IsInfinity(value)) return;
                EnsureDepartureCrewSelection();
                selectedDepartureOrdinaryCrew = Mathf.Clamp(Mathf.RoundToInt(value), 0,
                    Math.Max(0, session.CrewCountValue - selectedDepartureSpecialCrew.Count));
                ordinarySlider.SetValueWithoutNotify(selectedDepartureOrdinaryCrew);
                ApplyDepartureCrewConfiguration();
                ordinary.text = DepartureOrdinaryCrewLabel();
                bool understaffed = session.GetCrewSpeedPenalty(selectedDepartureOrdinaryCrew) > 0f;
                ordinaryHint.color = NauticalSurfaceSkin.ReadableTextColor(ordinaryHint.transform.parent,
                    understaffed ? Coral : MutedInk);
                ordinaryHint.text = DepartureCrewSpeedHint();
                total.text = "本次出航编组　普通 " + selectedDepartureOrdinaryCrew + "　+　特殊 " +
                    selectedDepartureSpecialCrew.Count + "　/　船只最大承载 " + session.CrewCountValue;
                if (headerStatus != null) headerStatus.text = BuildHeaderStatus();
            });

            Button back = CreateButton("Back To Voyage Setup", setupPanel, new Vector2(240f, 48f),
                new Vector2(-135f, -220f), PanelRaised, Ink, 15);
            back.GetComponentInChildren<Text>().text = HasWorkshopSpecialCardReturn ? "返回特殊卡牌" : "返回出航配置";
            back.onClick.AddListener(() =>
            {
                if (!IsCurrentDepartureCrewControl(back, owner, port, ship, revision)) return;
                departureCrewConfigurationOpen = false;
                if (TryReturnToWorkshopSpecialCards()) return;
                Render();
            });
            Button confirm = CreateButton("Confirm Departure Crew", setupPanel, new Vector2(240f, 48f),
                new Vector2(135f, -220f), Gold, DeepSea, 15);
            confirm.GetComponentInChildren<Text>().text = "确认船员配置";
            confirm.onClick.AddListener(() =>
            {
                if (!IsCurrentDepartureCrewControl(confirm, owner, port, ship, revision)) return;
                departureCrewConfigurationOpen = false;
                ApplyDepartureCrewConfiguration();
                portNotice = session.IsShortStaffed
                    ? "编组已保存；普通船员不足半载，航速与战斗减益。"
                    : "船员配置已保存，特殊船员将随船抵达目的港。";
                if (TryReturnToWorkshopSpecialCards()) return;
                Render();
            });
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
}
