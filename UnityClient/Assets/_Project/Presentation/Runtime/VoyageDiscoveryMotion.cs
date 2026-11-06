using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Only covers cells revealed by one committed voyage action. Never changes map knowledge.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageDiscoveryMotion : MaskableGraphic
    {
        public const float Duration = 1.05f;
        private Rect[] cells = Array.Empty<Rect>();
        private float[] delays = Array.Empty<float>();
        private Vector2[] discoveries = Array.Empty<Vector2>();
        private Behaviour owner;
        private bool paused;
        public float Elapsed { get; private set; }
        public int RevealedCellCount => cells.Length;
        public int DiscoveredLocationCount => discoveries.Length;
        public bool IsPlaying { get; private set; }

        public void Configure(Rect[] revealed, float[] startTimes, Vector2[] locations, Color fog, Behaviour controller)
        {
            cells = revealed ?? Array.Empty<Rect>();
            delays = startTimes ?? Array.Empty<float>();
            discoveries = locations ?? Array.Empty<Vector2>();
            owner = controller; color = fog; raycastTarget = false;
            Elapsed = 0f; IsPlaying = true; SetVerticesDirty();
        }

        private void Update()
        {
            if (!IsPlaying || paused) return;
            if (owner == null || !owner.isActiveAndEnabled) { Finish(); return; }
            SetElapsed(Elapsed + Mathf.Min(.05f, Time.unscaledDeltaTime));
        }

        public void SetElapsed(float elapsed)
        {
            Elapsed = Mathf.Clamp(elapsed, 0f, Duration);
            if (Elapsed >= Duration) Finish();
            else SetVerticesDirty();
        }

        private void Finish() { IsPlaying = false; cells = Array.Empty<Rect>(); discoveries = Array.Empty<Vector2>(); SetVerticesDirty(); enabled = false; }
        protected override void OnDisable() { IsPlaying = false; cells = Array.Empty<Rect>(); discoveries = Array.Empty<Vector2>(); base.OnDisable(); }
        private void OnApplicationPause(bool value) => paused = value;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!IsPlaying) return;
            for (int i = 0; i < cells.Length; i++)
            {
                float delay = i < delays.Length ? delays[i] : 0f;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Elapsed - delay) / .62f));
                Color tint = color; tint.a = 1f - t;
                if (tint.a > .001f) Quad(mesh, cells[i], tint);
            }
            for (int i = 0; i < discoveries.Length; i++)
            {
                float t = Mathf.Clamp01((Elapsed - .20f - Mathf.Min(i, 5) * .025f) / .70f);
                if (t <= 0f || t >= 1f) continue;
                Color ink = new Color(.95f, .79f, .36f, Mathf.Sin(t * Mathf.PI) * .7f);
                Ring(mesh, discoveries[i], 15f + 37f * t, 1.4f, ink);
                ink.a *= .45f;
                Ring(mesh, discoveries[i], 12f + 24f * t, .8f, ink);
            }
        }

        private static void Quad(VertexHelper mesh, Rect r, Color tint)
        {
            int n = mesh.currentVertCount;
            mesh.AddVert(new Vector3(r.xMin, r.yMin), tint, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMin, r.yMax), tint, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax, r.yMax), tint, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax, r.yMin), tint, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float radius, float width, Color tint)
        {
            const int segments = 40;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int n = mesh.currentVertCount;
                mesh.AddVert(center + p * (radius - width), tint, Vector2.zero);
                mesh.AddVert(center + p * radius, tint, Vector2.zero);
                mesh.AddVert(center + q * radius, tint, Vector2.zero);
                mesh.AddVert(center + q * (radius - width), tint, Vector2.zero);
                mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }
}
