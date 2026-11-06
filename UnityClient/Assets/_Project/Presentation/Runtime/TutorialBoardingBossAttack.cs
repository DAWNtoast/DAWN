using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Approach, cutlass strike and retreat for the guided boarding boss. Presentation only.</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialBoardingBossAttack : MonoBehaviour
    {
        public const string ResourcePath = "BoardingBattle/Characters/tutorial_boss_attack_parts_v1";
        public const float ApproachStartTime = .12f;
        public const float ApproachEndTime = .82f;
        public const float SwingStartTime = 1.13f;
        public const float ContactTime = 1.30f;
        public const float RetreatStartTime = 1.90f;
        public const float RetreatEndTime = 2.65f;
        public const float Duration = 2.80f;
        const float StrikeDuration = 1.05f, StrikeContactTime = .48f;
        const float CaptainSize = 286f, ArmSize = 120f, CaptainX = 20f, FootV = 10f / 627f;
        static readonly Vector2 ShoulderUv = new Vector2(.384f, .758f);
        static readonly Vector2 ArmPivot = new Vector2(.845f, .455f);
        static readonly Vector2 ElbowUv = new Vector2(.61f, .42f);
        static readonly Vector2 TipUv = new Vector2(.081f, .89f);
        RawImage original, body, arm;
        TutorialBossBodyMesh bodyMesh;
        TutorialBossArmMesh armMesh;
        TutorialBossSwordTrail trail;
        TutorialBossContactShadow contactShadow;
        RectTransform layers, captainRoot;
        Vector2 shoulder;
        bool initialized, originalEnabled, focusLost, applicationPaused;
        public bool IsPlaying { get; private set; }
        public bool MotionSuspended { get; private set; }
        public bool ExternalClock { get; set; }
        public float Clock { get; private set; }
        public float Progress => IsPlaying ? Mathf.Clamp01(Clock / Duration) : 0f;
        public float ShoulderAngle { get; private set; }
        public float ForearmAngle { get; private set; }
        public Vector2 TravelOffset => captainRoot == null ? Vector2.zero : captainRoot.anchoredPosition;
        public Vector2 AttackDestination { get; private set; }
        public bool IsTravelling => IsPlaying && ((Clock > ApproachStartTime && Clock < ApproachEndTime) ||
            (Clock > RetreatStartTime && Clock < RetreatEndTime));
        public float StridePhase => GaitPhase(IsPlaying ? Clock : 0f);
        public Vector3 FootAnchor => body == null ? transform.position : body.rectTransform.TransformPoint(Vector3.zero);
        public Vector3 SwordTipPosition => arm == null ? transform.position : arm.rectTransform.TransformPoint(ArmOffset(TipUv, ForearmAngle));

        public static TutorialBoardingBossAttack Attach(RectTransform party, RawImage original)
        {
            if (party == null || original == null) return null;
            var existing = party.GetComponent<TutorialBoardingBossAttack>();
            if (existing != null) return existing;
            Texture2D atlas = Resources.Load<Texture2D>(ResourcePath);
            if (atlas == null) return null; // Keep the complete original art if the optional asset is unavailable.
            var actor = party.gameObject.AddComponent<TutorialBoardingBossAttack>();
            actor.Build(original, atlas);
            return actor;
        }

        void Build(RawImage source, Texture2D atlas)
        {
            original = source; originalEnabled = source.enabled;
            var host = new GameObject("Tutorial Boss Layered Formation", typeof(RectTransform));
            layers = host.GetComponent<RectTransform>(); layers.SetParent(transform, false);
            layers.anchorMin = layers.anchorMax = layers.pivot = new Vector2(.5f,.5f);
            layers.sizeDelta = BoardingBattleLayout.PartySize;
            layers.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            float floor = -BoardingBattleLayout.PartySize.y * .5f;
            CreatePart("Tutorial Boss Left Sailor", atlas, new Rect(0,.5f,.5f,.5f),
                new Vector2(-126f,floor), 266f, new Vector2(.5f,14f/627f));
            CreatePart("Tutorial Boss Right Sailor", atlas, new Rect(0,0,.5f,.5f),
                new Vector2(150f,floor), 262f, new Vector2(.5f,4f/627f));
            var captainObject = new GameObject("Tutorial Boss Moving Captain", typeof(RectTransform));
            captainRoot = captainObject.GetComponent<RectTransform>(); captainRoot.SetParent(layers,false);
            captainRoot.anchorMin = captainRoot.anchorMax = captainRoot.pivot = new Vector2(.5f,.5f);
            captainRoot.sizeDelta = BoardingBattleLayout.PartySize;
            var shadowObject = new GameObject("Tutorial Boss Moving Contact Shadow",typeof(RectTransform),typeof(CanvasRenderer));
            var shadowRect = shadowObject.GetComponent<RectTransform>(); shadowRect.SetParent(captainRoot,false);
            shadowRect.anchoredPosition = new Vector2(CaptainX,floor+1f); shadowRect.sizeDelta = new Vector2(88f,9f);
            contactShadow = shadowObject.AddComponent<TutorialBossContactShadow>(); contactShadow.raycastTarget = false;
            body = CreatePart("Tutorial Boss Captain Body", atlas, new Rect(.5f,.5f,.5f,.5f),
                new Vector2(CaptainX,floor), CaptainSize, new Vector2(.5f,FootV));
            body.rectTransform.SetParent(captainRoot,false);
            bodyMesh = body.gameObject.AddComponent<TutorialBossBodyMesh>(); bodyMesh.Owner = this;
            shoulder = new Vector2(CaptainX + (ShoulderUv.x-.5f)*CaptainSize, floor+(ShoulderUv.y-FootV)*CaptainSize);
            var trailObject = new GameObject("Tutorial Boss Cutlass Arc",typeof(RectTransform),typeof(CanvasRenderer));
            var trailRect = trailObject.GetComponent<RectTransform>(); trailRect.SetParent(captainRoot,false);
            trailRect.sizeDelta = BoardingBattleLayout.PartySize;
            trail = trailObject.AddComponent<TutorialBossSwordTrail>(); trail.Owner = this; trail.raycastTarget = false;
            arm = CreatePart("Tutorial Boss Sword Arm",atlas,new Rect(.5f,0,.5f,.5f),shoulder,ArmSize,ArmPivot);
            arm.rectTransform.SetParent(captainRoot,false);
            armMesh = arm.gameObject.AddComponent<TutorialBossArmMesh>(); armMesh.Owner = this;
            original.enabled = false;
            initialized = true;
            ApplyPose();
        }

        RawImage CreatePart(string name, Texture2D atlas, Rect uv, Vector2 position, float size, Vector2 pivot)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(RawImage));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(layers,false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = Vector2.one * size;
            var image = go.GetComponent<RawImage>(); image.texture = atlas; image.uvRect = uv;
            image.raycastTarget = false; image.color = original.color;
            return image;
        }

        public void BeginAttack(RectTransform target = null)
        {
            if (!initialized || !isActiveAndEnabled || MotionSuspended || focusLost || applicationPaused) return;
            if (target == null && transform.parent != null) target = transform.parent.Find("Player Ship Art") as RectTransform;
            AttackDestination = Vector2.zero;
            if (target != null && target != transform)
            {
                // Stand just outside the near defender, with the blade reaching their silhouette.
                // Convert through world space so the route follows the actual UI layout and scale.
                Vector3 targetFoot = target.TransformPoint(new Vector3(target.rect.width*.25f,-target.rect.height*.5f,0f));
                Vector2 localFoot = layers.InverseTransformPoint(targetFoot);
                Vector2 homeFoot = new Vector2(CaptainX,-BoardingBattleLayout.PartySize.y*.5f);
                AttackDestination = localFoot + Vector2.right*120f + Vector2.down*8f - homeFoot;
            }
            IsPlaying = true; Clock = 0f; ApplyPose();
        }
        public void SetPresentationTime(float seconds)
        {
            if (!initialized || !isActiveAndEnabled || MotionSuspended || !IsPlaying || !Finite(seconds)) return;
            Clock = Mathf.Clamp(seconds,0f,Duration);
            if (Clock >= Duration) { CancelAttack(); return; }
            ApplyPose();
        }
        public void AdvancePresentation(float seconds)
        {
            if (!isActiveAndEnabled || !IsPlaying || MotionSuspended || focusLost || applicationPaused || !Finite(seconds) || seconds <= 0f) return;
            SetPresentationTime(Clock + seconds);
        }
        public void SetMotionSuspended(bool suspended) { MotionSuspended = suspended; if (suspended) CancelAttack(); }
        public void CancelAttack() { IsPlaying = false; Clock = 0f; ApplyPose(); }
        void Update() { if (!ExternalClock) AdvancePresentation(Mathf.Min(Time.unscaledDeltaTime,.05f)); }

        void ApplyPose()
        {
            if (!initialized || arm == null || body == null || trail == null || captainRoot == null) return;
            float seconds = IsPlaying ? Clock : 0f;
            captainRoot.anchoredPosition = AttackDestination * TravelFraction(seconds);
            if(contactShadow != null) contactShadow.Visibility = Mathf.Clamp01(captainRoot.anchoredPosition.magnitude/60f);
            Angles(seconds,out float upper,out float lower);
            ShoulderAngle = upper; ForearmAngle = lower;
            arm.rectTransform.anchoredPosition = shoulder + BodyOffset(ShoulderUv, seconds);
            arm.rectTransform.localRotation = Quaternion.Euler(0f,0f,upper);
            body.SetVerticesDirty(); arm.SetVerticesDirty(); trail.SetVerticesDirty();
        }
        public Vector2 SampleBodyOffset(Vector2 normalized) => BodyOffset(normalized,IsPlaying ? Clock : 0f);
        internal static Vector2 BodyOffset(Vector2 normalized,float seconds)
        {
            float h = Mathf.Clamp01((normalized.y-FootV)/(1f-FootV));
            float upper = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.12f,.9f,h));
            float strike = seconds-ApproachEndTime;
            float wind = Window(strike,0f,.27f,.31f,StrikeContactTime);
            float cut = Window(strike,.31f,StrikeContactTime,.60f,StrikeDuration);
            Vector2 pose = new Vector2(3f*wind-5.5f*cut,-1.8f*cut)*upper;
            float gait = GaitWeight(seconds);
            if(gait<=0f)return pose;
            float phase=GaitPhase(seconds),direction=seconds<RetreatStartTime?-1f:1f;
            float leg = 1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.09f,.43f,normalized.y));
            float right = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.46f,.55f,normalized.x));
            float swing = Mathf.Cos(phase);
            float stepX = Mathf.Lerp(swing,-swing,right)*29f;
            float stepY = Mathf.Lerp(Mathf.Max(0f,Mathf.Sin(phase)),Mathf.Max(0f,-Mathf.Sin(phase)),right)*15f;
            // The legs alternate independently while the coat's middle and hips stay connected.
            pose += new Vector2(stepX,stepY)*leg*gait;
            pose += new Vector2(direction*9f,Mathf.Abs(Mathf.Sin(phase))*2.8f)*upper*gait;
            return pose;
        }

        static float TravelFraction(float seconds)
        {
            if(seconds<RetreatStartTime)return Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(ApproachStartTime,ApproachEndTime,seconds));
            return 1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(RetreatStartTime,RetreatEndTime,seconds));
        }
        static float GaitWeight(float seconds)
        {
            float t=seconds<RetreatStartTime?Mathf.InverseLerp(ApproachStartTime,ApproachEndTime,seconds):
                Mathf.InverseLerp(RetreatStartTime,RetreatEndTime,seconds);
            return Mathf.SmoothStep(0f,1f,Mathf.Min(t,1f-t)*8f);
        }
        static float GaitPhase(float seconds)
        {
            float t=seconds<RetreatStartTime?Mathf.InverseLerp(ApproachStartTime,ApproachEndTime,seconds):
                Mathf.InverseLerp(RetreatStartTime,RetreatEndTime,seconds);
            return Mathf.SmoothStep(0f,1f,t)*Mathf.PI*2f*2.5f;
        }
        internal static Vector2 ArmOffset(Vector2 uv,float elbowAngle)
        {
            Vector2 point = (uv-ArmPivot)*ArmSize;
            Vector2 elbow = (ElbowUv-ArmPivot)*ArmSize;
            // A rigid hand/blade and a small blended elbow band keep the steel straight.
            float weight = 1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.53f,.70f,uv.x));
            return Vector2.Lerp(point,elbow+Rotate(point-elbow,elbowAngle),weight);
        }
        internal Vector2 SampleTip(float seconds)
        {
            Angles(seconds,out float upper,out float lower);
            return shoulder+BodyOffset(ShoulderUv,seconds)+Rotate(ArmOffset(TipUv,lower),upper);
        }
        internal static void Angles(float seconds,out float upper,out float lower)
        {
            float strike=seconds-ApproachEndTime;
            float wind = Window(strike,0f,.27f,.31f,StrikeContactTime);
            float cut = Window(strike,.31f,StrikeContactTime,.60f,StrikeDuration);
            float gait=GaitWeight(seconds);
            upper = -15f-35f*wind+31f*cut+(-16f+4f*Mathf.Sin(GaitPhase(seconds)))*gait;
            lower = -8f*wind+17f*cut-8f*gait;
        }
        static Vector2 Rotate(Vector2 v,float degrees)
        {
            float a=degrees*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);
            return new Vector2(v.x*c-v.y*s,v.x*s+v.y*c);
        }
        static float Window(float t,float a,float b,float c,float d) =>
            Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(a,b,t))*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(c,d,t)));
        static bool Finite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);
        void OnEnable() { if(initialized && layers!=null && original!=null) { layers.gameObject.SetActive(true); original.enabled=false; CancelAttack(); } }
        void OnDisable() { CancelAttack(); if(layers!=null) layers.gameObject.SetActive(false); if(original!=null)original.enabled=originalEnabled; }
        void OnApplicationFocus(bool focused) { focusLost=!focused; if(!focused)CancelAttack(); }
        void OnApplicationPause(bool paused) { applicationPaused=paused; if(paused)CancelAttack(); }
        void OnDestroy() { if(original!=null)original.enabled=originalEnabled; if(layers!=null)Destroy(layers.gameObject); }
    }

    /// <summary>Only the isolated sword arm is articulated; blade vertices share one rigid forearm transform.</summary>
    public sealed class TutorialBossArmMesh : BaseMeshEffect
    {
        public TutorialBoardingBossAttack Owner;
        public override void ModifyMesh(VertexHelper vh)
        {
            if(!IsActive()||Owner==null)return;
            var image=(RawImage)graphic;Rect uv=image.uvRect;Color32 tint=image.color;
            const int columns=32,rows=20;vh.Clear();
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                Vector2 n=new Vector2(x/(float)columns,y/(float)rows);
                vh.AddVert(TutorialBoardingBossAttack.ArmOffset(n,Owner.ForearmAngle),tint,new Vector2(uv.x+n.x*uv.width,uv.y+n.y*uv.height));
            }
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {int i=y*(columns+1)+x;vh.AddTriangle(i,i+columns+1,i+1);vh.AddTriangle(i+1,i+columns+1,i+columns+2);}
        }
    }
    public sealed class TutorialBossBodyMesh : BaseMeshEffect
    {
        public TutorialBoardingBossAttack Owner;
        public override void ModifyMesh(VertexHelper vh)
        {
            if(!IsActive()||Owner==null)return;
            var image=(RawImage)graphic;Rect rect=image.GetPixelAdjustedRect(),uv=image.uvRect;Color32 tint=image.color;
            const int columns=28,rows=40;vh.Clear();
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                Vector2 n=new Vector2(x/(float)columns,y/(float)rows);
                Vector2 p=new Vector2(rect.xMin+n.x*rect.width,rect.yMin+n.y*rect.height)+Owner.SampleBodyOffset(n);
                vh.AddVert(p,tint,new Vector2(uv.x+n.x*uv.width,uv.y+n.y*uv.height));
            }
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {int i=y*(columns+1)+x;vh.AddTriangle(i,i+columns+1,i+1);vh.AddTriangle(i+1,i+columns+1,i+columns+2);}
        }
    }
    public sealed class TutorialBossSwordTrail : MaskableGraphic
    {
        public TutorialBoardingBossAttack Owner;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            const float trailStart=TutorialBoardingBossAttack.SwingStartTime+.02f;
            const float end=TutorialBoardingBossAttack.ApproachEndTime+.62f;
            vh.Clear();if(Owner==null||!Owner.IsPlaying||Owner.Clock<trailStart||Owner.Clock>end)return;
            const int segments=12;
            for(int i=0;i<segments;i++)
            {
                float a=i/(float)segments,b=(i+1f)/segments;
                Vector2 p=Owner.SampleTip(Mathf.Max(TutorialBoardingBossAttack.SwingStartTime,Owner.Clock-.07f+a*.07f));
                Vector2 q=Owner.SampleTip(Mathf.Max(TutorialBoardingBossAttack.SwingStartTime,Owner.Clock-.07f+b*.07f));
                Vector2 v=q-p;if(v.sqrMagnitude<.0001f)continue;
                Vector2 normal=new Vector2(-v.y,v.x).normalized*(.3f+1.1f*b);
                Color32 tint=new Color(1f,.86f,.55f,.55f*b*Mathf.Clamp01((end-Owner.Clock)/.10f));
                int start=vh.currentVertCount;
                vh.AddVert(p-normal,tint,Vector2.zero);vh.AddVert(p+normal,tint,Vector2.zero);
                vh.AddVert(q+normal,tint,Vector2.zero);vh.AddVert(q-normal,tint,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
        }
    }

    public sealed class TutorialBossContactShadow : MaskableGraphic
    {
        float visibility;
        public float Visibility { set { if(Mathf.Approximately(visibility,value))return;visibility=value;SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(visibility<=0f)return;
            Rect r=GetPixelAdjustedRect();const int segments=32;
            vh.AddVert(Vector2.zero,new Color(0f,0f,0f,.22f*visibility),Vector2.zero);
            for(int i=0;i<=segments;i++)
            {
                float a=i*Mathf.PI*2f/segments;
                vh.AddVert(new Vector2(Mathf.Cos(a)*r.width*.5f,Mathf.Sin(a)*r.height*.5f),Color.clear,Vector2.zero);
                if(i>0)vh.AddTriangle(0,i,i+1);
            }
        }
    }
}
