using System;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared clock layout for authored, procedural and retained voyage headers.</summary>
    public static class VoyageTimeRibbonSkin
    {
        private static readonly Color Ink = new Color32(245, 231, 200, 255);
        private static readonly Color MutedInk = new Color32(162, 182, 181, 255);

        public static void Apply(RectTransform ribbon)
        {
            if (ribbon == null) return;
            RectTransform track = ribbon.Find("Lookout Time Track") as RectTransform;
            Layout(track, new Vector2(VoyageTimeRibbonGraphic.DesignWidth, 68f), Vector2.zero);
            if (track != null) track.SetAsFirstSibling();
            VoyageTimeRibbonGraphic timeline = track == null ? null : track.GetComponent<VoyageTimeRibbonGraphic>();
            if (timeline != null) timeline.raycastTarget = false;
            Color accent = NavigationTimeOfDayUtility.AccentColor(timeline == null ? NavigationTimeOfDay.Day : timeline.TimeOfDay);
            Label(ribbon, "Lookout Time Readout", new Vector2(128f, 40f), new Vector2(-280f, -9f), 32, Ink);
            Label(ribbon, "Lookout Time Legend", new Vector2(212f, 18f), new Vector2(-238f, 22f), 14, MutedInk);
            Label(ribbon, "Lookout Time Phase", new Vector2(68f, 26f), new Vector2(-167f, -10f), 16, accent,
                TextAnchor.MiddleCenter);
            Label(ribbon, "Lookout Time Caption", new Vector2(100f, 18f), new Vector2(-58f, 22f), 13, MutedInk).text = "昼夜时序";
            Label(ribbon, "Lookout Time Transition", new Vector2(328f, 18f), new Vector2(168f, 22f), 14, Ink,
                TextAnchor.MiddleRight);
            for (int hour = 0; hour <= 24; hour += 6)
            {
                float x = Mathf.Lerp(VoyageTimeRibbonGraphic.RailLeft, VoyageTimeRibbonGraphic.RailRight, hour / 24f) - 360f;
                Label(ribbon, "Lookout Time Hour " + hour, new Vector2(28f, 18f), new Vector2(x, -24f), 13, MutedInk,
                    TextAnchor.MiddleCenter).text = hour.ToString("00", CultureInfo.InvariantCulture);
            }
        }

        public static void Update(RectTransform ribbon, DateTime gameDate, double elapsedGameHours)
        {
            if (ribbon == null) return;
            VoyageTimeRibbonGraphic timeline = ribbon.GetComponentInChildren<VoyageTimeRibbonGraphic>(true);
            if (timeline != null) timeline.SetVoyageHours(elapsedGameHours);
            Apply(ribbon);
            ribbon.Find("Lookout Time Readout").GetComponent<Text>().text = gameDate.ToString("HH:mm", CultureInfo.InvariantCulture);
            ribbon.Find("Lookout Time Legend").GetComponent<Text>().text = "游戏日期　" + CoreVoyageSession.FormatGameDate(gameDate, false);
            NavigationTimeOfDay phase = NavigationTimeOfDayUtility.FromVoyageHours(elapsedGameHours);
            ribbon.Find("Lookout Time Phase").GetComponent<Text>().text = NavigationTimeOfDayUtility.Label(phase);
            ribbon.Find("Lookout Time Transition").GetComponent<Text>().text = TransitionLabel(elapsedGameHours);
        }

        private static string TransitionLabel(double elapsedGameHours)
        {
            double hour = NavigationTimeOfDayUtility.LocalHour(elapsedGameHours);
            NavigationTimeOfDay phase = NavigationTimeOfDayUtility.FromVoyageHours(elapsedGameHours);
            double remaining;
            string next;
            switch (phase)
            {
                case NavigationTimeOfDay.Day:
                    remaining = NavigationTimeOfDayUtility.DuskStartHour - hour;
                    next = "黄昏";
                    break;
                case NavigationTimeOfDay.Dusk:
                    remaining = NavigationTimeOfDayUtility.NightStartHour - hour;
                    next = "夜晚";
                    break;
                default:
                    remaining = (NavigationTimeOfDayUtility.DayStartHour - hour + 24d) % 24d;
                    next = "白天";
                    break;
            }
            int minutes = Math.Max(1, (int)Math.Ceiling(remaining * 60d - 1e-8d));
            string duration = minutes >= 60 ? (minutes / 60).ToString(CultureInfo.InvariantCulture) + "小时" : string.Empty;
            if (minutes % 60 != 0) duration += (minutes % 60).ToString(CultureInfo.InvariantCulture) + "分钟";
            return "距" + next + " " + duration;
        }

        private static Text Label(RectTransform parent, string name, Vector2 size, Vector2 position, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                created.layer = parent.gameObject.layer;
                child = created.transform;
                child.SetParent(parent, false);
            }
            Layout((RectTransform)child, size, position);
            Text text = child.GetComponent<Text>();
            GameTypography.Apply(text, fontSize, FontStyle.Normal);
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void Layout(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.localScale = Vector3.one;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
