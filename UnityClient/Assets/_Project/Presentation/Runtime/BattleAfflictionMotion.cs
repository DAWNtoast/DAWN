using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Small, noninteractive marks attached to the displayed combatant, never to its rules position.</summary>
    public sealed class BattleAfflictionMotion : MonoBehaviour
    {
        private Behaviour owner;
        private RectTransform target, stage;
        private BattleAfflictionGraphic graphic;
        private bool burning, cursed, boarding, suspended, paused, focusLost;
        private float clock, pulse;
        public bool HasBurn => burning;
        public bool HasCurse => cursed;
        public bool IsVisible => graphic != null && graphic.enabled;
        public int TriggerCount { get; private set; }
        public void Configure(Behaviour value, RectTransform combatant, bool isBoarding)
        { owner = value; target = combatant; stage = transform.parent as RectTransform; boarding = isBoarding; EnsureGraphic(); }
        public void Synchronize(IReadOnlyList<CoreBattleStatus> statuses)
        {
            burning = cursed = false;
            foreach (CoreBattleStatus status in statuses)
            { burning |= status.Id == "burn"; cursed |= status.Id == "curse_marks"; }
            suspended = false;
            Sample();
        }
        public void Trigger(CoreBattleSettlementKind kind)
        {
            EnsureGraphic(); pulse = .58f; TriggerCount++;
            graphic.PulseKind = kind; Sample();
        }
        private void EnsureGraphic()
        {
            if (graphic == null) graphic = GetComponent<BattleAfflictionGraphic>();
            if (graphic == null) graphic = gameObject.AddComponent<BattleAfflictionGraphic>();
            graphic.raycastTarget = false;
        }
        public void AdvancePresentation(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (owner == null || !owner.isActiveAndEnabled || target == null || !target.gameObject.activeInHierarchy)
            { Suspend(); return; }
            clock += Mathf.Max(0f, seconds); pulse = Mathf.Max(0f, pulse - Mathf.Max(0f, seconds)); Sample();
        }
        private void Sample()
        {
            if (graphic == null) return;
            graphic.enabled = !suspended && !paused && !focusLost && target != null && owner != null && owner.isActiveAndEnabled &&
                target.gameObject.activeInHierarchy && (burning || cursed || pulse > 0f);
            if (!graphic.enabled || stage == null) return;
            var rect = (RectTransform)transform;
            // Use actual rendered bounds. Naval art is scaled to its tactical cell.
            Vector3 a = stage.InverseTransformPoint(target.TransformPoint(target.rect.min));
            Vector3 b = stage.InverseTransformPoint(target.TransformPoint(target.rect.max));
            rect.anchoredPosition = stage.InverseTransformPoint(target.position);
            rect.sizeDelta = new Vector2(Mathf.Clamp(Mathf.Abs(b.x-a.x), 70f, boarding ? 350f : 240f),
                Mathf.Clamp(Mathf.Abs(b.y-a.y), 65f, boarding ? 290f : 200f));
            graphic.SetState(burning, cursed, clock, pulse / .58f);
        }
        public void Suspend() { suspended = true; pulse = 0f; if (graphic != null) graphic.enabled = false; }
        private void LateUpdate() { if (Application.isPlaying) AdvancePresentation(Time.unscaledDeltaTime); }
        private void OnDisable() => Suspend();
        private void OnApplicationPause(bool value) { paused = value; if (value) pulse = 0f; Sample(); }
        private void OnApplicationFocus(bool focused) { focusLost = !focused; if (!focused) pulse = 0f; Sample(); }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleAfflictionGraphic : MaskableGraphic
    {
        private bool burn, curse;
        private float clock, pulse;
        public CoreBattleSettlementKind PulseKind { get; set; }
        public void SetState(bool fire, bool marks, float time, float trigger)
        { burn = fire; curse = marks; clock = time; pulse = Mathf.Clamp01(trigger); raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect;
            if (burn || pulse > 0f && PulseKind == CoreBattleSettlementKind.Burn)
                for (int i = 0; i < 7; i++)
                {
                    float p = Mathf.Repeat(clock * .38f + i * .143f, 1f);
                    float x = Mathf.Sin(i * 3.17f) * r.width * .35f + Mathf.Sin(clock + i) * 3f;
                    Vector2 c = new Vector2(x, r.yMin * .48f + p * r.height * .53f);
                    float life = Mathf.Sin(p * Mathf.PI), size = 2f + pulse * 3f;
                    Diamond(mesh, c, new Vector2(size, size * 2.7f), new Color(1f, .40f + p * .3f, .12f, life * (.40f + pulse * .35f)));
                }
            if (curse || pulse > 0f && PulseKind == CoreBattleSettlementKind.CurseExecution)
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * .5f + clock * .23f;
                    Vector2 c = new Vector2(Mathf.Cos(angle) * r.width * .40f, Mathf.Sin(angle) * r.height * .25f);
                    Color tint = new Color(.70f, .43f, .96f, .23f + pulse * .45f);
                    Diamond(mesh, c, new Vector2(3f + pulse * 2f, 6f + pulse * 3f), tint);
                    Line(mesh, c + Vector2.down * 8f, c + Vector2.down * 15f, 1.1f, tint);
                }
            if (pulse > 0f)
            {
                Color c = PulseKind == CoreBattleSettlementKind.Recovery ? new Color(.34f, .9f, .65f, pulse * .6f) :
                    PulseKind == CoreBattleSettlementKind.BloodField ? new Color(.92f, .16f, .28f, pulse * .6f) :
                    PulseKind == CoreBattleSettlementKind.CurseExecution ? new Color(.7f, .36f, 1f, pulse * .5f) : new Color(1f, .5f, .12f, pulse * .4f);
                float radius = .25f + (1f-pulse) * .23f;
                for (int i = 0; i < 28; i++)
                {
                    float a = i * Mathf.PI * 2f / 28f, b = (i + 1) * Mathf.PI * 2f / 28f;
                    Line(mesh, new Vector2(Mathf.Cos(a) * r.width * radius, Mathf.Sin(a) * r.height * radius * .45f),
                        new Vector2(Mathf.Cos(b) * r.width * radius, Mathf.Sin(b) * r.height * radius * .45f), 1.8f, c);
                }
            }
        }
        private static void Diamond(VertexHelper mesh, Vector2 c, Vector2 size, Color color)
        {
            int n = mesh.currentVertCount;
            mesh.AddVert(c + Vector2.left*size.x, color, Vector2.zero); mesh.AddVert(c + Vector2.up*size.y, color, Vector2.zero);
            mesh.AddVert(c + Vector2.right*size.x, color, Vector2.zero); mesh.AddVert(c + Vector2.down*size.y, color, Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 normal = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f; int n = mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero); mesh.AddVert(a+normal,color,Vector2.zero);
            mesh.AddVert(b+normal,color,Vector2.zero); mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
