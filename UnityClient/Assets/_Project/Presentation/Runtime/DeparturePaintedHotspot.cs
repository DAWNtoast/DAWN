using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Retains a scene button's input while its painted artwork supplies the face.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class DeparturePaintedHotspot : MaskableGraphic
    {
        private const string FeedbackName = "Departure Painted Hotspot Feedback";

        [Serializable]
        private sealed class GraphicState
        {
            public Graphic graphic;
            public bool enabled, raycastTarget;
            public Color color, rendererColor;
            public Material material;
            public Sprite sprite, overrideSprite;
            public Image.Type imageType;
            public bool fillCenter, preserveAspect;
            public float pixelsPerUnitMultiplier;
        }

        [Serializable]
        private sealed class BehaviourState
        {
            public Behaviour behaviour;
            public bool enabled;
        }

        [SerializeField] private Selectable target;
        [SerializeField] private Image hit;
        [SerializeField] private bool applied, selected, round, addedHit;
        [SerializeField] private bool showOutline = true;
        [SerializeField] private Graphic originalTargetGraphic;
        [SerializeField] private Selectable.Transition originalTransition;
        [SerializeField] private ColorBlock originalColors;
        [SerializeField] private SpriteState originalSpriteState;
        [SerializeField] private List<GraphicState> graphics = new List<GraphicState>();
        [SerializeField] private List<BehaviourState> behaviours = new List<BehaviourState>();
        private bool lastInteractable;

        public static void Apply(Selectable target, bool selected = false, bool round = false, bool showOutline = true)
        {
            if (target == null) return;
            Transform child = target.transform.Find(FeedbackName);
            DeparturePaintedHotspot feedback = child == null ? null : child.GetComponent<DeparturePaintedHotspot>();
            if (feedback == null)
            {
                var holder = new GameObject(FeedbackName, typeof(RectTransform), typeof(CanvasRenderer));
                holder.transform.SetParent(target.transform, false);
                feedback = holder.AddComponent<DeparturePaintedHotspot>();
                feedback.target = target;
            }
            if (!feedback.applied) feedback.Capture();
            feedback.selected = selected;
            feedback.round = round;
            feedback.showOutline = showOutline;
            feedback.applied = true;
            feedback.HideCapturedArt();
            feedback.gameObject.SetActive(true);
            feedback.enabled = true;
            feedback.raycastTarget = false;
            feedback.color = Color.white;
            RectTransform rect = feedback.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            feedback.transform.SetAsLastSibling();
            feedback.lastInteractable = target.IsInteractable();
            feedback.SetVerticesDirty();
        }

        public static void RestoreAll(Transform root)
        {
            if (root == null) return;
            foreach (DeparturePaintedHotspot feedback in root.GetComponentsInChildren<DeparturePaintedHotspot>(true))
                feedback.Restore();
        }

        private void Capture()
        {
            graphics.Clear();
            behaviours.Clear();
            originalTargetGraphic = target.targetGraphic;
            originalTransition = target.transition;
            originalColors = target.colors;
            originalSpriteState = target.spriteState;
            hit = target.GetComponent<Image>();
            addedHit = hit == null;
            if (hit == null) hit = target.gameObject.AddComponent<Image>();
            foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                // Snapshot just the original artwork. Later dynamic labels and
                // this feedback subtree must remain outside the hide/restore set.
                if (graphic == null || graphic.transform.IsChildOf(transform)) continue;
                var state = new GraphicState { graphic = graphic, enabled = graphic.enabled,
                    raycastTarget = graphic.raycastTarget, color = graphic.color,
                    rendererColor = graphic.canvasRenderer.GetColor(), material = graphic.material };
                if (graphic is Image image)
                {
                    state.sprite = image.sprite;
                    state.overrideSprite = image.overrideSprite;
                    state.imageType = image.type;
                    state.fillCenter = image.fillCenter;
                    state.preserveAspect = image.preserveAspect;
                    state.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
                }
                graphics.Add(state);
            }
            foreach (Behaviour behaviour in target.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || behaviour.transform.IsChildOf(transform)) continue;
                if (behaviour is Outline || behaviour is NauticalButtonMotion)
                    behaviours.Add(new BehaviourState { behaviour = behaviour, enabled = behaviour.enabled });
            }
        }

        private void HideCapturedArt()
        {
            target.transition = Selectable.Transition.None;
            foreach (BehaviourState state in behaviours)
                if (state.behaviour != null) state.behaviour.enabled = false;
            foreach (GraphicState state in graphics)
            {
                if (state.graphic == null || state.graphic == hit) continue;
                state.graphic.enabled = false;
                state.graphic.raycastTarget = false;
            }
            hit.enabled = true;
            hit.raycastTarget = true;
            hit.sprite = null;
            hit.overrideSprite = null;
            hit.type = Image.Type.Simple;
            hit.color = Color.clear;
            hit.canvasRenderer.SetColor(Color.white);
            target.targetGraphic = hit;
        }

        private void Restore()
        {
            if (!applied) return;
            applied = false;
            foreach (GraphicState state in graphics)
            {
                Graphic graphic = state.graphic;
                if (graphic == null) continue;
                graphic.enabled = state.enabled;
                graphic.raycastTarget = state.raycastTarget;
                graphic.color = state.color;
                graphic.material = state.material;
                if (graphic is Image image)
                {
                    image.sprite = state.sprite;
                    image.overrideSprite = state.overrideSprite;
                    image.type = state.imageType;
                    image.fillCenter = state.fillCenter;
                    image.preserveAspect = state.preserveAspect;
                    image.pixelsPerUnitMultiplier = state.pixelsPerUnitMultiplier;
                }
                graphic.canvasRenderer.SetColor(state.rendererColor);
            }
            if (target != null)
            {
                target.targetGraphic = originalTargetGraphic;
                target.colors = originalColors;
                target.spriteState = originalSpriteState;
                target.transition = originalTransition;
                // Restore the current logical toggle value, not a stale alpha
                // from before the user opened the painted chart control.
                if (target is Toggle toggle && toggle.graphic != null)
                    toggle.graphic.canvasRenderer.SetAlpha(toggle.isOn ? 1f : 0f);
            }
            foreach (BehaviourState state in behaviours)
                if (state.behaviour != null) state.behaviour.enabled = state.enabled;
            if (addedHit && hit != null) { hit.enabled = false; hit.raycastTarget = false; }
            enabled = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        private void Update()
        {
            if (!applied || target == null) return;
            bool interactable = target.IsInteractable();
            if (interactable == lastInteractable) return;
            lastInteractable = interactable;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!applied || target == null) return;
            bool interactable = target.IsInteractable();
            // Pointer and focus states never add an outline to the painted face.
            Color tint = !interactable ? new Color(.025f, .04f, .04f, .18f) :
                selected ? new Color(.94f, .73f, .34f, .30f) : Color.clear;
            if (tint.a <= 0f) return;
            Rect rect = rectTransform.rect;
            float inset = 2f;
            rect = new Rect(rect.xMin + inset, rect.yMin + inset, Mathf.Max(0, rect.width - inset * 2), Mathf.Max(0, rect.height - inset * 2));
            if (rect.width <= 0f || rect.height <= 0f) return;
            Vector2[] points = round ? Circle(rect) : Chamfered(rect);
            if (!interactable)
            {
                // Dim the painted face itself so unavailable sailing, locked
                // waters and disabled arrows remain visually distinguishable.
                int first = mesh.currentVertCount;
                Color shade = new Color(.015f, .025f, .025f, .34f);
                foreach (Vector2 point in points) mesh.AddVert(point, shade, Vector2.zero);
                for (int index = 1; index < points.Length - 1; index++)
                    mesh.AddTriangle(first, first + index, first + index + 1);
            }
            // Painted chart cards and port plaques already supply their own borders.
            // Keep disabled shading without outlining their rectangular hit areas.
            if (!showOutline) return;
            Color glow = tint; glow.a *= .24f;
            for (int index = 0; index < points.Length; index++)
            {
                Vector2 a = points[index], b = points[(index + 1) % points.Length];
                Stroke(mesh, a, b, 5f, glow);
                Stroke(mesh, a, b, 1.3f, tint);
            }
        }

        private static Vector2[] Circle(Rect rect)
        {
            var points = new Vector2[48];
            for (int index = 0; index < points.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / points.Length;
                points[index] = rect.center + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), rect.size * .5f);
            }
            return points;
        }

        private static Vector2[] Chamfered(Rect rect)
        {
            float cut = Mathf.Clamp(Mathf.Min(rect.width, rect.height) * .22f, 3f, 14f);
            return new[] { new Vector2(rect.xMin + cut, rect.yMax), new Vector2(rect.xMax - cut, rect.yMax),
                new Vector2(rect.xMax, rect.yMax - cut), new Vector2(rect.xMax, rect.yMin + cut),
                new Vector2(rect.xMax - cut, rect.yMin), new Vector2(rect.xMin + cut, rect.yMin),
                new Vector2(rect.xMin, rect.yMin + cut), new Vector2(rect.xMin, rect.yMax - cut) };
        }

        private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 edge = b - a;
            if (edge.sqrMagnitude < .001f) return;
            Vector2 normal = new Vector2(-edge.y, edge.x).normalized * (width * .5f);
            int first = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
