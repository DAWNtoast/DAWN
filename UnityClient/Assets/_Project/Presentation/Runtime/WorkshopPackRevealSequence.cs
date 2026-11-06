using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Reveals already committed pack results. Presentation never draws or grants cards.</summary>
    public sealed class WorkshopPackRevealSequence : MonoBehaviour
    {
        public const float CardInterval = .24f;
        public const float CardDuration = .82f;
        private Behaviour owner;
        private bool hadOwner;
        private RectTransform[] holders, faces, backs;
        private CanvasGroup[] holderGroups, faceGroups;
        private Vector2[] positions;
        private Vector3[] scales;
        private float[] faceAlphas;
        private bool[] raycasts, interactions;
        private Button openAgain;
        private bool againInteractable;
        private int preparedCount;
        private RewardSweepGraphic[] glints;
        public float Elapsed { get; private set; }
        public float Duration => holders == null ? 0f : .12f + CardDuration + (holders.Length - 1) * CardInterval;
        public bool IsComplete { get; private set; }

        public void Configure(RectTransform[] slots, RectTransform[] cardFaces, Button again, Behaviour owner, Color tint)
        {
            if (slots == null || cardFaces == null || slots.Length == 0 || slots.Length != cardFaces.Length)
                throw new System.ArgumentException("Card reveal requires matching slots and already created faces.");
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null || cardFaces[i] == null) throw new System.ArgumentException("Card reveal views must be alive.");
            this.owner = owner; hadOwner = owner != null; holders = slots; faces = cardFaces;
            openAgain = again; againInteractable = again != null && again.interactable;
            if (again != null) again.interactable = false;
            int count = holders.Length;
            backs = new RectTransform[count]; glints = new RewardSweepGraphic[count];
            holderGroups = new CanvasGroup[count]; faceGroups = new CanvasGroup[count];
            positions = new Vector2[count]; scales = new Vector3[count]; faceAlphas = new float[count];
            raycasts = new bool[count]; interactions = new bool[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = holders[i].anchoredPosition; scales[i] = holders[i].localScale;
                holderGroups[i] = holders[i].GetComponent<CanvasGroup>();
                if (holderGroups[i] == null) holderGroups[i] = holders[i].gameObject.AddComponent<CanvasGroup>();
                raycasts[i] = holderGroups[i].blocksRaycasts; interactions[i] = holderGroups[i].interactable;
                preparedCount = i + 1;
                holderGroups[i].blocksRaycasts = holderGroups[i].interactable = false;
                faceGroups[i] = faces[i].GetComponent<CanvasGroup>();
                if (faceGroups[i] == null) faceGroups[i] = faces[i].gameObject.AddComponent<CanvasGroup>();
                faceAlphas[i] = faceGroups[i].alpha;

                backs[i] = Child("Workshop Pack Card Back " + i, holders[i]);
                backs[i].gameObject.AddComponent<BattleCardFlightBackGraphic>().Configure(Color.white);
                RectTransform shine = Child("Workshop Pack Reveal Glint " + i, holders[i]);
                glints[i] = shine.gameObject.AddComponent<RewardSweepGraphic>(); glints[i].Configure(tint);
            }
            SetElapsed(0f);
        }

        private static RectTransform Child(string name, RectTransform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.sizeDelta = parent.rect.size; rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private void Update()
        {
            if (IsComplete || holders == null) return;
            if (hadOwner && (owner == null || !owner.isActiveAndEnabled)) { Cancel(); return; }
            SetElapsed(Elapsed + Time.unscaledDeltaTime);
        }

        public void SetElapsed(float seconds)
        {
            if (IsComplete || holders == null) return;
            Elapsed = Mathf.Clamp(float.IsNaN(seconds) ? Duration : seconds, 0f, Duration);
            if (Elapsed >= Duration) { Cancel(); return; }
            for (int i = 0; i < holders.Length; i++)
            {
                if (holders[i] == null || faces[i] == null) { Cancel(); return; }
                float local = Elapsed - .12f - i * CardInterval;
                float travel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(local / .40f));
                float flip = Mathf.Clamp01((local - .30f) / .38f);
                bool front = flip >= .5f;
                holders[i].anchoredPosition = Vector2.Lerp(new Vector2((i - 1) * 12f, 48f), positions[i], travel)
                    + Vector2.up * Mathf.Sin(travel * Mathf.PI) * 32f;
                float width = Mathf.Max(.025f, Mathf.Abs(Mathf.Cos(flip * Mathf.PI)));
                holders[i].localScale = Vector3.Scale(scales[i], new Vector3(width, 1f + .03f * Mathf.Sin(travel * Mathf.PI), 1f));
                faceGroups[i].alpha = front ? faceAlphas[i] : 0f;
                if (backs[i] != null) backs[i].gameObject.SetActive(!front);
                glints[i].gameObject.SetActive(front && local < CardDuration);
                if (front) glints[i].SetProgress(Mathf.Clamp01((local - .49f) / .33f));
                if (local >= CardDuration)
                {
                    holderGroups[i].blocksRaycasts = raycasts[i]; holderGroups[i].interactable = interactions[i];
                }
            }
        }

        public void Cancel()
        {
            if (IsComplete) return;
            IsComplete = true;
            if (holders != null)
                for (int i = 0; i < preparedCount; i++)
                {
                    if (holders[i] != null) { holders[i].anchoredPosition = positions[i]; holders[i].localScale = scales[i]; }
                    if (faceGroups != null && faceGroups[i] != null) faceGroups[i].alpha = faceAlphas[i];
                    if (holderGroups != null && holderGroups[i] != null)
                    { holderGroups[i].blocksRaycasts = raycasts[i]; holderGroups[i].interactable = interactions[i]; }
                    if (backs != null && backs[i] != null) backs[i].gameObject.SetActive(false);
                    if (glints != null && glints[i] != null) glints[i].gameObject.SetActive(false);
                }
            if (openAgain != null) openAgain.interactable = againInteractable;
        }
        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
    }
}
