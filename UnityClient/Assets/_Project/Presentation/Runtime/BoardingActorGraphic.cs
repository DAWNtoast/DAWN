using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BoardingActorAction { Idle, Attack, Cast, Hit, Guard, Heal, Prepare, Death }

    /// <summary>Foot-pinned card performances. The resting artwork stays completely still.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RawImage))]
    public sealed class BoardingActorGraphic : BaseMeshEffect
    {
        private RawImage image;
        private Texture2D bossActionAtlas;
        private Texture2D defeatAtlas;
        private Texture formationTexture;
        private Rect formationUv;
        private Texture restingTexture;
        private Rect restingUv;
        private string restingIdentity, restingCardId, restingName;
        public Text NameLabel { private get; set; }
        [SerializeField] private float facing = 1f;
        [SerializeField] private float phase;
        private float actionElapsed, actionDuration = .45f;
        private bool focusLost, applicationPaused, externallyTimedDeath;
        [field: SerializeField] public string Identity { get; private set; }
        [field: SerializeField] public string CardId { get; private set; }
        [field: SerializeField] public bool UsesActionAtlas { get; private set; }
        public bool UsesBossActionAtlas => bossActionAtlas != null;
        public bool UsesDefeatAtlas => defeatAtlas != null && Action == BoardingActorAction.Death;
        private bool PaintedBossFormation => Formation && BoardingBattleArtCatalog.IsNamedEnemy(Identity);
        [field: SerializeField] public bool Formation { get; private set; }
        [field: SerializeField] public float FootV { get; private set; }
        public float Clock { get; private set; }
        public BoardingActorAction Action { get; private set; }
        public float ActionProgress => Action == BoardingActorAction.Idle ? 1f : Mathf.Clamp01(actionElapsed / actionDuration);
        public bool MotionSuspended { get; private set; }

        public void Configure(bool formation, float facing, float phase, float normalizedFoot,
            string identity = null, string cardId = null, bool usesActionAtlas = false)
        {
            image = GetComponent<RawImage>();
            Identity = identity;
            CardId = cardId;
            UsesActionAtlas = usesActionAtlas;
            Formation = formation;
            this.facing = facing < 0f ? -1f : 1f;
            this.phase = Finite(phase) ? phase : 0f;
            FootV = Finite(normalizedFoot) ? Mathf.Clamp(normalizedFoot, 0f, .9f) : 0f;
            Clock = 0f;
            ClearAction();
            Dirty();
        }

        public void SetPresentationTime(float seconds)
        {
            if (!Finite(seconds) || Action == BoardingActorAction.Idle) return;
            externallyTimedDeath = Action == BoardingActorAction.Death;
            actionElapsed = Clock = Mathf.Max(0f, seconds);
            if (actionElapsed >= actionDuration && Action != BoardingActorAction.Death) ClearAction();
            Dirty();
        }

        public void AdvancePresentation(float seconds)
        {
            if (!isActiveAndEnabled || MotionSuspended || focusLost || applicationPaused || Action == BoardingActorAction.Idle ||
                !Finite(seconds) || seconds <= 0f) return;
            if (Action == BoardingActorAction.Death && actionElapsed >= actionDuration) return;
            actionElapsed = Mathf.Min(actionDuration, actionElapsed + seconds);
            Clock = actionElapsed;
            if (actionElapsed >= actionDuration && Action != BoardingActorAction.Death) ClearAction();
            Dirty();
        }

        public void PlayAction(BoardingActorAction action, float duration = .45f) => StartAction(action, duration, false);

        public void React(BoardingActorAction reaction, float duration) => StartAction(reaction, duration, true);

        private void StartAction(BoardingActorAction action, float duration, bool incoming)
        {
            if (action == BoardingActorAction.Idle) { ClearAction(); Dirty(); return; }
            if (!isActiveAndEnabled || MotionSuspended || focusLost || applicationPaused) return;
            // The resolution owns a fallen pose until the retained view is restored.
            if (Action == BoardingActorAction.Death || (!incoming && Priority(action) < Priority(Action))) return;
            if (action == BoardingActorAction.Death)
            {
                ClearAction();
                if (Formation && !UsesActionAtlas) defeatAtlas = BoardingBattleArtCatalog.ForEnemyDefeat(Identity);
                if (defeatAtlas != null)
                {
                    formationTexture = image.texture;
                    formationUv = image.uvRect;
                    image.texture = defeatAtlas;
                }
            }
            else if (PaintedBossFormation && bossActionAtlas == null)
            {
                bossActionAtlas = BoardingBattleArtCatalog.ForBossActions(Identity);
                if (bossActionAtlas != null)
                {
                    // Keep the original formation through interrupts such as Attack -> Hit -> Guard.
                    formationTexture = image.texture;
                    formationUv = image.uvRect;
                    image.texture = bossActionAtlas;
                }
            }
            Action = action;
            actionElapsed = Clock = 0f;
            externallyTimedDeath = false;
            actionDuration = Finite(duration) ? Mathf.Max(.05f, duration) : .45f;
            Dirty();
        }

        public void SetMotionSuspended(bool suspended)
        {
            MotionSuspended = suspended;
            if (suspended) ClearAction();
            Dirty();
        }

        public void PlayGuestAction(string identity, string cardId, string displayName, Texture2D sheet,
            BoardingActorAction action, float duration)
        {
            if (!UsesActionAtlas || sheet == null || !isActiveAndEnabled || MotionSuspended || focusLost || applicationPaused ||
                Action == BoardingActorAction.Death) return;
            ClearAction();
            restingTexture = image.texture;
            restingUv = image.uvRect;
            restingIdentity = Identity;
            restingCardId = CardId;
            restingName = NameLabel != null ? NameLabel.text : null;
            if (NameLabel != null) NameLabel.text = displayName;
            Identity = identity;
            CardId = cardId;
            image.texture = sheet;
            PlayAction(action, duration);
        }

        public Vector2 SampleOffset(Vector2 normalized)
        {
            if (!isActiveAndEnabled || UsesActionAtlas || UsesDefeatAtlas || MotionSuspended || Action == BoardingActorAction.Idle ||
                (PaintedBossFormation && Action != BoardingActorAction.Death) ||
                !Finite(normalized.x) || !Finite(normalized.y)) return Vector2.zero;
            float h = Mathf.Clamp01((normalized.y - FootV) / Mathf.Max(.1f, 1f - FootV));
            if (Action == BoardingActorAction.Death)
            {
                // Older saved figure art has a gentle stagger during the resolution fade.
                float stagger = Mathf.SmoothStep(0f, 1f, ActionProgress) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .53f, h));
                return new Vector2(-facing * 12f, -15f) * (stagger * image.rectTransform.rect.height / 280f);
            }
            // Soles and the ankle band remain still; bending grows smoothly above them.
            float upper = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .92f, h));
            if (upper <= 0f) return Vector2.zero;
            float u = Mathf.Clamp01(normalized.x);
            Vector2 offset = Vector2.zero;
            float weightSum = 0f;
            int count = Formation ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                float center = Formation ? .20f + i * .31f : .5f;
                float local = (u - center) / (Formation ? .20f : .5f);
                // Soft influences keep long weapons continuous across neighbouring bodies.
                float weight = Mathf.Exp(-local * local * 2.1f);
                float arms = Mathf.Exp(-Mathf.Pow((h - .47f) / .18f, 2f));
                float t = ActionProgress;
                float strength = i == 1 || !Formation ? 1f : .68f;
                Vector2 body = Vector2.zero, hand = Vector2.zero;
                float angle = 0f;
                switch (Action)
                {
                    case BoardingActorAction.Attack:
                        // Pull the weapon back, cut forward/down quickly, then recover.
                        float windup = Window(t, 0f, .22f, .22f, .48f);
                        float cut = Window(t, .22f, .48f, .57f, 1f);
                        body = new Vector2(-4f * windup + 12f * cut, -2f * cut);
                        hand = new Vector2(-10f * windup + 20f * cut, 9f * windup - 14f * cut);
                        angle = 7f * windup - 11f * cut;
                        break;
                    case BoardingActorAction.Cast:
                        // Raise and hold the hands before a separate forward release.
                        float gather = Window(t, 0f, .28f, .58f, 1f);
                        float release = Window(t, .48f, .67f, .73f, 1f);
                        body = new Vector2(2f * gather + 4f * release, 1.5f * gather);
                        hand = new Vector2(3f * gather + 13f * release, 17f * gather - 5f * release);
                        angle = -4f * gather + 5f * release;
                        break;
                    case BoardingActorAction.Hit:
                        float hit = Window(t, 0f, .12f, .18f, 1f);
                        body = new Vector2(-13f * hit, -3f * hit);
                        hand = new Vector2(-5f * hit, 3f * hit);
                        break;
                    case BoardingActorAction.Guard:
                        float brace = Window(t, 0f, .20f, .62f, 1f);
                        body = new Vector2(-4.5f * brace, -4f * brace);
                        hand = new Vector2(-6f * brace, 10f * brace);
                        angle = 6f * brace;
                        break;
                    case BoardingActorAction.Heal:
                        float mend = Window(t, 0f, .38f, .60f, 1f);
                        body = new Vector2(0f, 2f * mend);
                        hand = new Vector2(Mathf.Clamp(local, -1f, 1f) * 5f * mend, 10f * mend);
                        angle = -3f * mend;
                        break;
                    case BoardingActorAction.Prepare:
                        float ready = Window(t, 0f, .32f, .50f, 1f);
                        body = new Vector2(-1.5f * ready, -1.5f * ready);
                        hand = new Vector2(-Mathf.Clamp(local, -1f, 1f) * 7f * ready, -5f * ready);
                        angle = 3f * ready;
                        break;
                }
                // Soft shoulder/forearm rotation carries weapons without splitting shared image boundaries.
                float radians = angle * Mathf.Deg2Rad;
                Vector2 lever = new Vector2(Mathf.Clamp(local, -1.4f, 1.4f) * 42f, (h - .61f) * 145f);
                Vector2 rotation = new Vector2(lever.x * (Mathf.Cos(radians) - 1f) - lever.y * Mathf.Sin(radians),
                    lever.x * Mathf.Sin(radians) + lever.y * (Mathf.Cos(radians) - 1f));
                // Arms need their full stroke; the torso's gradual ankle-to-head
                // weight would otherwise shrink a weapon swing to a tiny wobble.
                float armWeight = arms * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .38f, h));
                Vector2 part = (body * upper + (hand + rotation) * armWeight) * strength;
                part.x *= facing;
                offset += part * weight;
                weightSum += weight;
            }
            // Scale design-space motion with the figure, not screen resolution.
            float height = image != null ? image.rectTransform.rect.height : 280f;
            return offset / Mathf.Max(.00001f, weightSum) * Mathf.Clamp(height / 280f, .1f, 4f);
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || UsesActionAtlas || UsesDefeatAtlas || (PaintedBossFormation && Action != BoardingActorAction.Death)) return;
            if (image == null) image = GetComponent<RawImage>();
            Rect rect = image.GetPixelAdjustedRect();
            Rect uv = image.uvRect;
            int columns = Formation ? 48 : 20;
            const int rows = 28;
            Color32 tint = image.color;
            vh.Clear();
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    Vector2 n = new Vector2(x / (float)columns, y / (float)rows);
                    Vector2 position = new Vector2(rect.xMin + n.x * rect.width, rect.yMin + n.y * rect.height) + SampleOffset(n);
                    vh.AddVert(position, tint, new Vector2(uv.x + n.x * uv.width, uv.y + n.y * uv.height));
                }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int a = y * (columns + 1) + x;
                    vh.AddTriangle(a, a + columns + 1, a + 1);
                    vh.AddTriangle(a + 1, a + columns + 1, a + columns + 2);
                }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static int Priority(BoardingActorAction action) => action == BoardingActorAction.Death ? 4 :
            action == BoardingActorAction.Hit ? 3 : action == BoardingActorAction.Attack || action == BoardingActorAction.Cast ? 2 :
            action == BoardingActorAction.Idle ? 0 : 1;
        private static float Window(float t, float riseStart, float riseEnd, float fallStart, float fallEnd) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(riseStart, riseEnd, t)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fallStart, fallEnd, t)));
        private void ClearAction()
        {
            Action = BoardingActorAction.Idle;
            actionElapsed = Clock = 0f;
            externallyTimedDeath = false;
            if (defeatAtlas != null || bossActionAtlas != null)
            {
                Texture2D activeSheet = defeatAtlas != null ? defeatAtlas : bossActionAtlas;
                if (image.texture == activeSheet)
                {
                    image.texture = formationTexture;
                    image.uvRect = formationUv;
                }
                formationTexture = null;
                defeatAtlas = null;
                bossActionAtlas = null;
            }
            if (restingTexture == null) return;
            image.texture = restingTexture;
            image.uvRect = restingUv;
            Identity = restingIdentity;
            CardId = restingCardId;
            if (NameLabel != null) NameLabel.text = restingName;
            restingTexture = null;
        }
        private void Dirty()
        {
            if (UsesDefeatAtlas && image != null)
                image.uvRect = DefeatFrameUv(ActionProgress);
            else if (UsesBossActionAtlas && image != null)
                image.uvRect = BossActionFrameUv(Action, ActionProgress);
            else if (UsesActionAtlas && image != null)
                image.uvRect = FrameUv(Action, ActionProgress);
            else if (graphic != null) graphic.SetVerticesDirty();
        }

        public static Rect DefeatFrameUv(float progress)
        {
            const int frames = 6;
            int frame = Finite(progress) ? Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * frames), 0, frames - 1) : 0;
            const float halfX = .5f / 384f, halfY = .5f / 256f;
            return new Rect((frame + halfX) / frames, halfY, (1f - 2f * halfX) / frames, 1f - 2f * halfY);
        }

        public static Rect BossActionFrameUv(BoardingActorAction action, float progress)
        {
            const int columns = 8, rows = 4;
            int row;
            switch (action)
            {
                case BoardingActorAction.Hit: row = 1; break;
                case BoardingActorAction.Cast:
                case BoardingActorAction.Heal:
                case BoardingActorAction.Prepare: row = 2; break;
                case BoardingActorAction.Guard: row = 3; break;
                default: row = 0; break;
            }
            int frame = Finite(progress) ? Mathf.Min(columns - 1, Mathf.FloorToInt(Mathf.Clamp01(progress) * columns)) : 0;
            const float halfX = .5f / 384f, halfY = .5f / 256f;
            return new Rect((frame + halfX) / columns, (rows - 1 - row + halfY) / rows,
                (1f - 2f * halfX) / columns, (1f - 2f * halfY) / rows);
        }

        public static Rect FrameUv(BoardingActorAction action, float progress)
        {
            const int columns = 6, rows = 5;
            int row;
            switch (action)
            {
                case BoardingActorAction.Cast:
                case BoardingActorAction.Heal:
                case BoardingActorAction.Prepare: row = 1; break;
                case BoardingActorAction.Hit: row = 2; break;
                case BoardingActorAction.Guard: row = 3; break;
                case BoardingActorAction.Death: row = 4; break;
                default: row = 0; break;
            }
            int frame = action == BoardingActorAction.Idle || !Finite(progress) ? 0 :
                Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * columns), 0, columns - 1);
            // Cells have an alpha gutter; inset to texel centers to prevent bilinear bleed.
            const float inset = .5f / 256f;
            return new Rect((frame + inset) / columns, (rows - 1 - row + inset) / rows,
                (1f - 2f * inset) / columns, (1f - 2f * inset) / rows);
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            image = GetComponent<RawImage>();
            GetComponentInParent<BoardingPartyPresentation>()?.Register(this);
        }
        private void Update()
        {
            // BattleResolutionMotion samples defeat itself so bodies and shadows use one clock.
            if (!externallyTimedDeath) AdvancePresentation(Time.unscaledDeltaTime);
        }
        protected override void OnDisable() { ClearAction(); Dirty(); base.OnDisable(); }
        private void OnApplicationFocus(bool focused)
        {
            focusLost = !focused;
            // The resolution pauses on focus loss and resumes the same defeat frame.
            if (focusLost && !externallyTimedDeath) ClearAction();
            Dirty();
        }
        private void OnApplicationPause(bool paused) { applicationPaused = paused; if (paused) ClearAction(); Dirty(); }
    }
}
