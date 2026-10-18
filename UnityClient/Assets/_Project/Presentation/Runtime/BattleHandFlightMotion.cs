using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Presentation only: the caller owns the hand and all deck mutations.</summary>
    public sealed class BattleHandFlightMotion : MonoBehaviour
    {
        public const float FlightDuration = .4f;
        public const float DealInterval = .06f;
        public const float DiscardInterval = .015f;
        private const float SettleDuration = .1f;

        private sealed class Flight
        {
            public RectTransform Card, Back;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public Vector2 PilePosition;
            public CanvasGroup Group;
            public float Alpha;
            public bool BlocksRaycasts, Interactable, HoverEnabled, Finished;
            public BattleCardHoverFeedback Hover;
            public Canvas Canvas;
            public bool OverrideSorting;
            public int SortingOrder;
            public float Delay, PileScale;
        }

        private readonly List<Flight> flights = new List<Flight>(8);
        private RectTransform pile;
        private Vector3 pileScale;
        private Quaternion pileRotation;
        private float elapsed, duration;
        private bool discarding;
        public bool IsPlaying { get; private set; }

        public void Deal(IReadOnlyList<RectTransform> cards, RectTransform source)
        {
            Begin(cards, source, false);
        }

        public void Discard(IReadOnlyList<RectTransform> cards, RectTransform destination)
        {
            Begin(cards, destination, true);
        }

        private void Begin(IReadOnlyList<RectTransform> cards, RectTransform deck, bool discard)
        {
            Complete();
            if (!isActiveAndEnabled || cards == null || cards.Count == 0 || deck == null) return;
            discarding = discard;
            pile = deck;
            pileScale = deck.localScale;
            pileRotation = deck.localRotation;
            elapsed = 0f;
            for (int index = 0; index < cards.Count; index++)
            {
                RectTransform card = cards[index];
                if (card == null || !card.gameObject.activeInHierarchy || card.parent == null) continue;
                var flight = new Flight
                {
                    Card = card, Position = card.localPosition, Scale = card.localScale, Rotation = card.localRotation,
                    Hover = card.GetComponent<BattleCardHoverFeedback>(),
                    Group = card.GetComponent<CanvasGroup>(), Canvas = card.GetComponent<Canvas>(),
                    Delay = flights.Count * (discard ? DiscardInterval : DealInterval),
                };
                if (flight.Hover != null)
                {
                    flight.HoverEnabled = flight.Hover.enabled;
                    flight.Hover.enabled = false;
                    // OnDisable resets hover. Retain the observed starting pose.
                    card.localPosition = flight.Position;
                    card.localScale = flight.Scale;
                    card.localRotation = flight.Rotation;
                }
                if (flight.Group == null) flight.Group = card.gameObject.AddComponent<CanvasGroup>();
                flight.Alpha = flight.Group.alpha;
                flight.BlocksRaycasts = flight.Group.blocksRaycasts;
                flight.Interactable = flight.Group.interactable;
                flight.Group.blocksRaycasts = false;
                flight.Group.interactable = false;
                if (flight.Canvas == null) flight.Canvas = card.gameObject.AddComponent<Canvas>();
                flight.OverrideSorting = flight.Canvas.overrideSorting;
                flight.SortingOrder = flight.Canvas.sortingOrder;
                flight.Canvas.overrideSorting = true;
                flight.Canvas.sortingOrder = 180 + index;

                Transform icon = deck.Find("Icon");
                RectTransform pileBack = icon != null ? icon.Find("Card Back") as RectTransform : null;
                if (pileBack != null) icon = pileBack;
                flight.PilePosition = card.parent.InverseTransformPoint(icon != null ? icon.position : deck.position);
                Vector3 deckWidth = card.parent.InverseTransformVector(pileBack != null
                    ? pileBack.TransformVector(Vector3.right * pileBack.rect.width)
                    : deck.TransformVector(Vector3.right * 54f));
                flight.PileScale = Mathf.Max(.08f, deckWidth.magnitude / CardFaceLayout.FrameSize.x);
                GameObject back = new GameObject("Battle Flight Back", typeof(RectTransform), typeof(Canvas), typeof(BattleCardFlightBackGraphic));
                flight.Back = (RectTransform)back.transform;
                flight.Back.SetParent(card.parent, false);
                flight.Back.sizeDelta = CardFaceLayout.FrameSize;
                Canvas backCanvas = back.GetComponent<Canvas>();
                backCanvas.overrideSorting = true;
                backCanvas.sortingOrder = flight.Canvas.sortingOrder;
                back.GetComponent<BattleCardFlightBackGraphic>().raycastTarget = false;
                flights.Add(flight);
            }
            if (flights.Count == 0) { Complete(); return; }
            duration = flights[flights.Count - 1].Delay + FlightDuration + (discard ? 0f : SettleDuration);
            IsPlaying = true;
            Sample(); // Hide undealt faces before the first rendered frame.
        }

        private void Update()
        {
            if (!IsPlaying) return;
            elapsed += Time.unscaledDeltaTime;
            Sample();
            if (elapsed < duration) return;
            IsPlaying = false;
            RestorePile();
            // Discarded faces stay hidden until the caller removes their views.
            // Complete/OnDisable can still restore them if the action is cancelled.
            if (!discarding) Complete();
        }

        private void Sample()
        {
            float pulse = 0f;
            foreach (Flight flight in flights)
            {
                if (flight.Card == null) continue;
                float localTime = elapsed - flight.Delay;
                if (localTime < 0f)
                {
                    flight.Group.alpha = discarding ? flight.Alpha : 0f;
                    flight.Back.gameObject.SetActive(false);
                    continue;
                }
                float t = Mathf.Clamp01(localTime / FlightDuration);
                float pulseTime = discarding ? localTime - FlightDuration * .75f : localTime;
                if (pulseTime >= 0f && pulseTime < .16f)
                    pulse = Mathf.Max(pulse, Mathf.Sin(pulseTime / .16f * Mathf.PI));
                if (localTime >= FlightDuration + (discarding ? 0f : SettleDuration))
                {
                    if (!flight.Finished)
                    {
                        Restore(flight);
                        if (discarding)
                        {
                            if (flight.Hover != null) flight.Hover.enabled = false;
                            flight.Group.alpha = 0f;
                            flight.Group.blocksRaycasts = false;
                            flight.Group.interactable = false;
                        }
                        flight.Back.gameObject.SetActive(false);
                        flight.Finished = true;
                    }
                    continue;
                }
                float travel = discarding ? EaseInOutQuart(t) : EaseInOutQuad(t);
                Vector2 start = discarding ? (Vector2)flight.Position : flight.PilePosition;
                Vector2 end = discarding ? flight.PilePosition : (Vector2)flight.Position;
                // A shallow lift keeps the motion a quick shot from the deck.
                Vector2 position = Vector2.LerpUnclamped(start, end, travel);
                position.y += Mathf.Sin(travel * Mathf.PI) * (discarding ? 44f : 38f);
                float rotationT = 1f - (1f - t) * (1f - t);
                Quaternion deckRotation = Quaternion.Euler(0f, 0f, discarding ? 23f : -28f);
                Quaternion rotation = discarding
                    ? Quaternion.Slerp(flight.Rotation, deckRotation, rotationT)
                    : Quaternion.Slerp(deckRotation, flight.Rotation, rotationT);
                float flip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .88f, t));
                float facing = Mathf.Cos(flip * Mathf.PI);
                bool front = discarding ? facing > 0f : facing <= 0f;
                Vector3 small = Vector3.one * flight.PileScale;
                Vector3 scale = discarding ? Vector3.Lerp(flight.Scale, small, travel) : Vector3.Lerp(small, flight.Scale, rotationT);
                float settle = discarding ? 0f : Mathf.Clamp01((localTime - FlightDuration) / SettleDuration);
                float lift = discarding ? 0f : .034f * Mathf.Sin(t * Mathf.PI) + .02f * (1f - settle) * t;
                scale *= 1f + lift;
                scale.x *= Mathf.Max(.025f, Mathf.Abs(facing));
                scale.y *= Mathf.Lerp(.92f, 1f, discarding ? 1f - t : t);
                flight.Card.localPosition = new Vector3(position.x, position.y, flight.Position.z);
                flight.Card.localRotation = rotation;
                flight.Card.localScale = scale;
                flight.Group.alpha = front ? flight.Alpha : 0f;
                flight.Back.gameObject.SetActive(!front);
                flight.Back.localPosition = flight.Card.localPosition;
                flight.Back.localRotation = rotation;
                flight.Back.localScale = scale;
            }
            if (pile != null)
            {
                pile.localScale = Vector3.Scale(pileScale, new Vector3(1f + pulse * .045f, 1f - pulse * .035f, 1f));
                pile.localRotation = pileRotation * Quaternion.Euler(0f, 0f, pulse * (discarding ? -2f : 2f));
            }
        }

        private static float EaseInOutQuad(float t) => t < .5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * .5f;
        private static float EaseInOutQuart(float t) => t < .5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) * .5f;

        private static void Restore(Flight flight)
        {
            if (flight.Card == null) return;
            flight.Card.localPosition = flight.Position;
            flight.Card.localScale = flight.Scale;
            flight.Card.localRotation = flight.Rotation;
            if (flight.Canvas != null)
            {
                flight.Canvas.overrideSorting = flight.OverrideSorting;
                flight.Canvas.sortingOrder = flight.SortingOrder;
            }
            if (flight.Group != null)
            {
                flight.Group.alpha = flight.Alpha;
                flight.Group.blocksRaycasts = flight.BlocksRaycasts;
                flight.Group.interactable = flight.Interactable;
            }
            if (flight.Hover != null) flight.Hover.enabled = flight.HoverEnabled;
        }

        public void Complete()
        {
            IsPlaying = false;
            foreach (Flight flight in flights)
            {
                Restore(flight);
                if (flight.Back == null) continue;
                flight.Back.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(flight.Back.gameObject); else DestroyImmediate(flight.Back.gameObject);
            }
            flights.Clear();
            RestorePile();
            pile = null;
        }

        private void RestorePile()
        {
            if (pile == null) return;
            pile.localScale = pileScale;
            pile.localRotation = pileRotation;
        }

        private void OnDisable() => Complete();
        private void OnDestroy() => Complete();
    }
}
