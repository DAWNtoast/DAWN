using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Moves only the teaching note, protecting the current action and its annotation.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GuidedTutorialBannerLayout : MonoBehaviour
    {
        private const float Clearance = 12f;
        private RectTransform surface, paper;
        private GuidedTutorialFocusGraphic[] focus;
        private RectTransform[] readingContext;
        private Text objective, instruction;
        private readonly Vector3[] corners = new Vector3[4];
        private Vector2 preferredPosition;
        private readonly List<Rect> obstacles = new List<Rect>();
        private readonly List<Rect> currentObstacles = new List<Rect>();
        private readonly List<float> xs = new List<float>();
        private readonly List<float> ys = new List<float>();
        private Rect cachedViewport;
        private Vector2 cachedSize, cachedPosition;
        private bool hasCachedPlacement;
        private bool applying;

        public void Configure(RectTransform surface, GuidedTutorialFocusGraphic[] focus, RectTransform[] readingContext = null)
        {
            this.surface = surface;
            this.focus = focus;
            this.readingContext = readingContext;
            paper = (RectTransform)transform;
            objective = paper.Find("Guided Tutorial Objective")?.GetComponent<Text>();
            instruction = paper.Find("Guided Tutorial Instruction")?.GetComponent<Text>();
            preferredPosition = paper.anchoredPosition;
            hasCachedPlacement = false;
            Apply();
        }

        private void LateUpdate() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();

        public void Apply()
        {
            if (applying || surface == null || paper == null || !isActiveAndEnabled) return;
            applying = true;
            try
            {
                FitReadingText();
                Vector2 size = paper.rect.size;
                Rect viewport = surface.rect;
                if (size.x <= 0f || size.y <= 0f || viewport.width <= 0f || viewport.height <= 0f) return;
                currentObstacles.Clear();
                if (focus != null)
                    foreach (GuidedTutorialFocusGraphic marker in focus)
                    {
                        if (marker == null || !marker.TryGetProtectedRect(surface, out Rect bounds)) continue;
                        currentObstacles.Add(Rect.MinMaxRect(bounds.xMin - Clearance, bounds.yMin - Clearance,
                            bounds.xMax + Clearance, bounds.yMax + Clearance));
                    }
                if (readingContext != null)
                    foreach (RectTransform readout in readingContext)
                    {
                        if (readout == null || !readout.gameObject.activeInHierarchy) continue;
                        readout.GetWorldCorners(corners);
                        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                        Vector2 max = new Vector2(float.MinValue, float.MinValue);
                        foreach (Vector3 corner in corners)
                        {
                            Vector2 point = surface.InverseTransformPoint(corner);
                            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                        }
                        currentObstacles.Add(Rect.MinMaxRect(min.x - Clearance, min.y - Clearance,
                            max.x + Clearance, max.y + Clearance));
                    }

                // The arrow animates inside fixed protected bounds. Follow real
                // target motion each frame, but do not solve all candidate-edge
                // intersections again while the teaching geometry is unchanged.
                if (hasCachedPlacement && cachedViewport.Equals(viewport) && cachedSize.Equals(size) &&
                    SameObstacles())
                {
                    if (!paper.anchoredPosition.Equals(cachedPosition)) paper.anchoredPosition = cachedPosition;
                    return;
                }

                float left = viewport.xMin + 20f, bottom = viewport.yMin + 8f;
                float right = Mathf.Max(left, viewport.xMax - 20f - size.x);
                float top = Mathf.Max(bottom, viewport.yMax - 8f - size.y);
                Vector2 preferred = new Vector2(
                    Mathf.Clamp(viewport.xMin + preferredPosition.x, left, right),
                    Mathf.Clamp(viewport.yMax + preferredPosition.y - size.y, bottom, top));
                obstacles.Clear();
                obstacles.AddRange(currentObstacles);
                xs.Clear(); ys.Clear();
                xs.Add(preferred.x); xs.Add(left); xs.Add(right);
                ys.Add(preferred.y); ys.Add(bottom); ys.Add(top);
                foreach (Rect bounds in obstacles)
                {
                    xs.Add(Mathf.Clamp(bounds.xMin - size.x, left, right));
                    xs.Add(Mathf.Clamp(bounds.xMax, left, right));
                    ys.Add(Mathf.Clamp(bounds.yMin - size.y, bottom, top));
                    ys.Add(Mathf.Clamp(bounds.yMax, bottom, top));
                }

                // A nearest unobstructed placement touches either a viewport or
                // obstacle edge on each axis. Include their intersections for
                // lessons that highlight several valid actions simultaneously.
                Vector2 best = preferred;
                float bestOverlap = float.MaxValue, bestDistance = float.MaxValue;
                foreach (float x in xs)
                    foreach (float y in ys)
                    {
                        var candidate = new Rect(new Vector2(x, y), size);
                        float overlap = 0f;
                        foreach (Rect obstacle in obstacles)
                            overlap += Mathf.Max(0f, Mathf.Min(candidate.xMax, obstacle.xMax) - Mathf.Max(candidate.xMin, obstacle.xMin)) *
                                Mathf.Max(0f, Mathf.Min(candidate.yMax, obstacle.yMax) - Mathf.Max(candidate.yMin, obstacle.yMin));
                        float distance = (candidate.position - preferred).sqrMagnitude;
                        if (overlap > bestOverlap || (Mathf.Approximately(overlap, bestOverlap) && distance >= bestDistance)) continue;
                        best = candidate.position;
                        bestOverlap = overlap;
                        bestDistance = distance;
                    }
                cachedPosition = new Vector2(best.x - viewport.xMin, best.y + size.y - viewport.yMax);
                cachedViewport = viewport;
                cachedSize = size;
                hasCachedPlacement = true;
                paper.anchoredPosition = cachedPosition;
            }
            finally { applying = false; }
        }

        private void FitReadingText()
        {
            if (objective == null || instruction == null) return;
            // CanvasScaler can change glyph rounding and line wrapping after a
            // viewport resize. Measure at the live scale without shrinking text.
            float titleHeight = Mathf.Max(32f, Mathf.Ceil(objective.preferredHeight));
            float instructionHeight = Mathf.Max(68f, Mathf.Ceil(instruction.preferredHeight));
            RectTransform title = objective.rectTransform, body = instruction.rectTransform;
            float top = -title.anchoredPosition.y;
            title.sizeDelta = new Vector2(title.sizeDelta.x, titleHeight);
            body.anchoredPosition = new Vector2(body.anchoredPosition.x, -top - titleHeight - 8f);
            body.sizeDelta = new Vector2(body.sizeDelta.x, instructionHeight);
            paper.sizeDelta = new Vector2(paper.sizeDelta.x, top * 2f + titleHeight + 8f + instructionHeight);
        }

        private bool SameObstacles()
        {
            if (obstacles.Count != currentObstacles.Count) return false;
            for (int index = 0; index < obstacles.Count; index++)
                if (!obstacles[index].Equals(currentObstacles[index])) return false;
            return true;
        }
    }
}
