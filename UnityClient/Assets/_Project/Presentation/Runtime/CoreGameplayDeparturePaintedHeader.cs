using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private DeparturePaintedHeaderState departurePaintedHeader;
        private bool departurePaintedHeaderReadoutsBound;

        private void RestoreDeparturePaintedHeader()
        {
            if (departurePaintedHeaderReadoutsBound)
            {
                Canvas.preWillRenderCanvases -= RefreshDeparturePaintedHeaderReadouts;
                departurePaintedHeaderReadoutsBound = false;
            }
            DeparturePaintedHeaderState previous = departurePaintedHeader;
            departurePaintedHeader = null;
            if (previous == null) return;
            if (previous.Root != null) DeparturePaintedHotspot.RestoreAll(previous.Root);
            previous.Restore();
            // Refresh the values from the session instead of leaving the cached
            // header with the date and wallet captured when it entered this scene.
            if (this != null) UpdatePortHeaderReadouts(true);
        }

        private void ApplyDeparturePaintedHeader()
        {
            RestoreDeparturePaintedHeader();
            if (!CanUseDeparturePaintedHeader()) return;
            Transform header = FindActiveGuidedContent("Port Facility Header");
            if (header == null) return;

            Text time = header.Find("Port Facility Time")?.GetComponent<Text>();
            Text gold = header.Find("Port Facility Gold")?.GetComponent<Text>();
            if (time == null || gold == null) return;
            var buttons = new List<Button>();
            foreach (string name in new[] { "Return To Port Hub", "Previous Port Facility", "Next Port Facility", "Port Facility Settings" })
            {
                Button button = header.Find(name)?.GetComponent<Button>();
                if (button == null || button.GetComponent<Image>() == null) return;
                buttons.Add(button);
            }

            var state = new DeparturePaintedHeaderState(header, time, gold, buttons);
            departurePaintedHeader = state;
            PlaceDeparturePaintedHeaderAboveScene(header);
            // Preserve the original Buttons, listeners and live readout references.
            // The painting supplies the frame, captions and button faces.
            foreach (Graphic graphic in header.GetComponentsInChildren<Graphic>(true))
            {
                graphic.enabled = false;
                graphic.raycastTarget = false;
            }
            foreach (Shadow effect in header.GetComponentsInChildren<Shadow>(true)) effect.enabled = false;

            Rect[] bounds = {
                new Rect(24, 7, 166, 44), new Rect(218, 7, 190, 44),
                new Rect(1380, 7, 151, 44), new Rect(1551, 7, 108, 44)
            };
            for (int index = 0; index < buttons.Count; index++)
            {
                Button button = buttons[index];
                Image face = button.GetComponent<Image>();
                face.enabled = true;
                face.color = Color.clear;
                face.raycastTarget = true;
                button.targetGraphic = face;
                button.transition = Selectable.Transition.None;
                PlaceDeparturePaintedHeaderRect(button.GetComponent<RectTransform>(), bounds[index]);
                DeparturePaintedHotspot.Apply(button);
            }

            ApplyDeparturePaintedReadout(time, new Rect(1058, 30, 134, 23), new Color(.96f, .92f, .82f, 1f));
            ApplyDeparturePaintedReadout(gold, new Rect(1247, 28, 108, 27), new Color(1f, .86f, .58f, 1f));
            Canvas.preWillRenderCanvases += RefreshDeparturePaintedHeaderReadouts;
            departurePaintedHeaderReadoutsBound = true;
            RefreshDeparturePaintedHeaderReadouts();
        }

        private bool CanUseDeparturePaintedHeader() => this != null && isActiveAndEnabled && contentRoot != null &&
            session != null && session.Phase == CoreDemoPhase.Port && session.CurrentPort == "断锚港" &&
            (activePortFacility == PortFacilityArtCatalog.VoyageFacilityIndex ||
                !Application.isPlaying && editorPreviewScreen == CoreGameplayPreviewScreen.VoyageDeparture);

        private static void PlaceDeparturePaintedHeaderAboveScene(Transform header)
        {
            Transform parent = header.parent;
            if (parent == null) return;
            int lastSceneIndex = -1;
            foreach (string name in new[] { "Route Selection", "Voyage Setup" })
            {
                Transform scene = parent.Find(name);
                if (scene != null) lastSceneIndex = Mathf.Max(lastSceneIndex, scene.GetSiblingIndex());
            }
            if (lastSceneIndex < 0) return;
            int insertionIndex = lastSceneIndex + 1;
            // Removing a header earlier in the list shifts both scene roots left.
            // Insert immediately after them so any later modal stays above it.
            if (header.GetSiblingIndex() < insertionIndex) insertionIndex--;
            header.SetSiblingIndex(insertionIndex);
        }

        private void ApplyDeparturePaintedReadout(Text label, Rect pixels, Color color)
        {
            PlaceDeparturePaintedHeaderRect(label.rectTransform, pixels);
            label.enabled = true;
            label.raycastTarget = false;
            label.color = color;
            label.fontSize = label.resizeTextMaxSize = 18;
            label.resizeTextMinSize = 14;
            label.resizeTextForBestFit = true;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = 1f;
        }

        private void PlaceDeparturePaintedHeaderRect(RectTransform rect, Rect pixels)
        {
            Vector2 sceneSize = PhaseSurfaceLayout.ContentSize;
            Vector2 center = new Vector2(pixels.center.x / 1672f * sceneSize.x - sceneSize.x * .5f,
                sceneSize.y * .5f - pixels.center.y / 941f * sceneSize.y);
            Vector3 local = rect.parent.InverseTransformPoint(contentRoot.TransformPoint(center));
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(pixels.width / 1672f * sceneSize.x, pixels.height / 941f * sceneSize.y);
            rect.anchoredPosition = new Vector2(local.x, local.y);
        }

        private void RefreshDeparturePaintedHeaderReadouts()
        {
            // The common updater continues to write its normal labelled text,
            // including after purchases. Remove only the painted heading just
            // before drawing; do not change its cached date/gold or session data.
            if (departurePaintedHeader == null) return;
            if (!CanUseDeparturePaintedHeader() || departurePaintedHeader.Root == null ||
                !departurePaintedHeader.Root.gameObject.activeInHierarchy)
            {
                RestoreDeparturePaintedHeader();
                return;
            }
            StripDeparturePaintedReadoutHeading(departurePaintedHeader.Time);
            StripDeparturePaintedReadoutHeading(departurePaintedHeader.Gold);
        }

        private static void StripDeparturePaintedReadoutHeading(Text label)
        {
            if (label == null || string.IsNullOrEmpty(label.text)) return;
            int separator = label.text.LastIndexOf('\n');
            if (separator >= 0) label.text = label.text.Substring(separator + 1);
        }

        private sealed class DeparturePaintedHeaderState
        {
            internal readonly Transform Root;
            internal readonly Text Time, Gold;
            private readonly int siblingIndex;
            private readonly List<DeparturePaintedGraphicState> graphics = new List<DeparturePaintedGraphicState>();
            private readonly List<DeparturePaintedButtonState> buttons = new List<DeparturePaintedButtonState>();
            private readonly List<DeparturePaintedRectState> rects = new List<DeparturePaintedRectState>();
            private readonly List<DeparturePaintedTextState> readouts = new List<DeparturePaintedTextState>();
            private readonly Dictionary<Shadow, bool> effects = new Dictionary<Shadow, bool>();

            internal DeparturePaintedHeaderState(Transform root, Text time, Text gold, List<Button> controls)
            {
                Root = root;
                Time = time;
                Gold = gold;
                siblingIndex = root.GetSiblingIndex();
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                    graphics.Add(new DeparturePaintedGraphicState(graphic));
                foreach (Shadow effect in root.GetComponentsInChildren<Shadow>(true)) effects.Add(effect, effect.enabled);
                foreach (Button button in controls)
                {
                    buttons.Add(new DeparturePaintedButtonState(button));
                    rects.Add(new DeparturePaintedRectState(button.GetComponent<RectTransform>()));
                }
                foreach (Text label in new[] { time, gold })
                {
                    rects.Add(new DeparturePaintedRectState(label.rectTransform));
                    readouts.Add(new DeparturePaintedTextState(label));
                }
            }

            internal void Restore()
            {
                if (Root != null) Root.SetSiblingIndex(siblingIndex);
                foreach (DeparturePaintedRectState state in rects) state.Restore();
                foreach (DeparturePaintedTextState state in readouts) state.Restore();
                foreach (DeparturePaintedGraphicState state in graphics) state.Restore();
                foreach (DeparturePaintedButtonState state in buttons) state.Restore();
                foreach (KeyValuePair<Shadow, bool> effect in effects)
                    if (effect.Key != null) effect.Key.enabled = effect.Value;
            }
        }

        private sealed class DeparturePaintedGraphicState
        {
            private readonly Graphic graphic;
            private readonly bool enabled, raycast;
            private readonly Color color;
            internal DeparturePaintedGraphicState(Graphic value)
            {
                graphic = value;
                enabled = value.enabled;
                raycast = value.raycastTarget;
                color = value.color;
            }
            internal void Restore()
            {
                if (graphic == null) return;
                graphic.enabled = enabled;
                graphic.raycastTarget = raycast;
                graphic.color = color;
            }
        }

        private sealed class DeparturePaintedButtonState
        {
            private readonly Button button;
            private readonly Graphic target;
            private readonly Selectable.Transition transition;
            internal DeparturePaintedButtonState(Button value)
            {
                button = value;
                target = value.targetGraphic;
                transition = value.transition;
            }
            internal void Restore()
            {
                if (button == null) return;
                button.targetGraphic = target;
                button.transition = transition;
            }
        }

        private sealed class DeparturePaintedRectState
        {
            private readonly RectTransform rect;
            private readonly Vector2 anchorMin, anchorMax, pivot, position, size;
            internal DeparturePaintedRectState(RectTransform value)
            {
                rect = value;
                anchorMin = value.anchorMin;
                anchorMax = value.anchorMax;
                pivot = value.pivot;
                position = value.anchoredPosition;
                size = value.sizeDelta;
            }
            internal void Restore()
            {
                if (rect == null) return;
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = pivot;
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
        }

        private sealed class DeparturePaintedTextState
        {
            private readonly Text label;
            private readonly string text;
            private readonly int font, minimum, maximum;
            private readonly bool bestFit;
            private readonly TextAnchor alignment;
            private readonly HorizontalWrapMode horizontal;
            private readonly VerticalWrapMode vertical;
            private readonly float spacing;
            internal DeparturePaintedTextState(Text value)
            {
                label = value;
                text = value.text;
                font = value.fontSize;
                minimum = value.resizeTextMinSize;
                maximum = value.resizeTextMaxSize;
                bestFit = value.resizeTextForBestFit;
                alignment = value.alignment;
                horizontal = value.horizontalOverflow;
                vertical = value.verticalOverflow;
                spacing = value.lineSpacing;
            }
            internal void Restore()
            {
                if (label == null) return;
                label.text = text;
                label.fontSize = font;
                label.resizeTextMinSize = minimum;
                label.resizeTextMaxSize = maximum;
                label.resizeTextForBestFit = bestFit;
                label.alignment = alignment;
                label.horizontalOverflow = horizontal;
                label.verticalOverflow = vertical;
                label.lineSpacing = spacing;
            }
        }
    }
}
