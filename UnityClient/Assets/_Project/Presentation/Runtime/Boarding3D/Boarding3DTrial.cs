// Fixed-camera boarding prototype. Isolated from the production battle/session and saves.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace ShatteredSail.Presentation
{
    public sealed class Boarding3DTrial : MonoBehaviour
    {
        public Boarding3DActor Player { get; private set; }
        public Boarding3DActor Enemy { get; private set; }
        public Camera ViewCamera { get; private set; }
        public bool Paused { get; private set; }
        public bool AutoDemo { get; private set; } = true;
        public float Speed { get; private set; } = 1f;
        public string CurrentBeat { get; private set; } = "idle";
        public int CompletedAttacks { get; private set; }
        public int ReceivedHits { get; private set; }
        public float SimulationTime { get; private set; }
        public bool ExternalClock { get; set; }
        public event Action<string> BeatStarted;

        readonly List<Material> materials = new List<Material>();
        readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        readonly List<GameObject> transient = new List<GameObject>();
        readonly Vector3 playerHome = new Vector3(-2.15f, .08f, -.25f);
        readonly Vector3 enemyHome = new Vector3(2.15f, .08f, .25f);
        Transform world, effects, playerRing, enemyRing;
        LineRenderer swordTrail;
        readonly Vector3[] trailPoints = new Vector3[10];
        Text status, autoLabel, pauseLabel, speedLabel, hint;
        Font font;
        float beatTime, beatDuration, demoWait = 1.4f, impactFlash;
        int demoIndex;
        bool impactDone, initialized, applicationPaused, focusLost;
        Vector3 playerFrom, enemyFrom;
        Quaternion playerRotationFrom, enemyRotationFrom;
        readonly string[] demonstration = { "walk", "turn", "attack", "counter", "cast", "guard", "heal", "death", "reset" };
        static readonly Color Gold = new Color(.91f, .74f, .43f);
        static readonly Color Ink = new Color(.035f, .075f, .095f);

        void Start() => Initialize();
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Application.targetFrameRate = 60;
            world = new GameObject("3D Boarding Stage").transform;
            world.SetParent(transform, false);
            effects = new GameObject("Transient Effects").transform;
            effects.SetParent(world, false);
            BuildEnvironment();
            Player = Boarding3DActor.Create(world, false);
            Enemy = Boarding3DActor.Create(world, true);
            Player.transform.localPosition = playerHome;
            Enemy.transform.localPosition = enemyHome;
            Player.transform.localRotation = Quaternion.Euler(0, 90, 0);
            Enemy.transform.localRotation = Quaternion.Euler(0, -90, 0);
            BuildCamera();
            BuildInterface();
            var trailObject = new GameObject("Blade arc");
            trailObject.transform.SetParent(effects, false);
            swordTrail = trailObject.AddComponent<LineRenderer>();
            swordTrail.sharedMaterial = MakeMaterial(new Color(1f, .82f, .42f), true);
            swordTrail.startWidth = .065f; swordTrail.endWidth = .005f;
            swordTrail.positionCount = trailPoints.Length; swordTrail.useWorldSpace = true;
            swordTrail.enabled = false;
            if (GetComponent<Boarding3DTrialCapture>() == null) gameObject.AddComponent<Boarding3DTrialCapture>();
        }

        void Update()
        {
            if (!initialized) return;
            ReadKeyboard();
            if (!ExternalClock) Step(Mathf.Min(Time.unscaledDeltaTime, .05f));
        }

        public void Step(float seconds)
        {
            if (!initialized || !isActiveAndEnabled || Paused || applicationPaused || (focusLost && !ExternalClock) ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
            float dt = seconds * Speed;
            SimulationTime += dt;
            Player.Tick(dt); Enemy.Tick(dt);
            if (CurrentBeat != "idle")
            {
                beatTime += dt;
                float t = Mathf.Clamp01(beatTime / beatDuration);
                AnimateBeat(t);
                if (t >= 1f) FinishBeat();
            }
            else if (AutoDemo)
            {
                demoWait -= dt;
                if (demoWait <= 0f)
                {
                    BeginBeat(demonstration[demoIndex++ % demonstration.Length]);
                    demoWait = 1f;
                }
            }
            AnimateEffects(dt);
        }

        public void BeginBeat(string beat)
        {
            Initialize();
            if (beat == "reset") { ResetTrial(false); BeatStarted?.Invoke(beat); return; }
            // Preserve the current pose so interrupted actions can blend rather than snap.
            if (Player.Action == Boarding3DAction.Death || Enemy.Action == Boarding3DAction.Death) ResetActors();
            playerFrom = Player.transform.localPosition; enemyFrom = Enemy.transform.localPosition;
            playerRotationFrom = Player.transform.localRotation; enemyRotationFrom = Enemy.transform.localRotation;
            Player.Play(Boarding3DAction.Idle); Enemy.Play(Boarding3DAction.Idle);
            ClearEffects(); swordTrail.enabled = false;
            CurrentBeat = beat; beatTime = 0f; impactDone = false;
            beatDuration = beat == "walk" ? 2.8f : beat == "turn" ? 2.4f : beat == "death" ? 2f : 1.65f;
            switch (beat)
            {
                case "walk": Player.Play(Boarding3DAction.Walk, beatDuration); SetStatus("移动", "迈步前进，再回到起点 · 双脚交替落地"); break;
                case "turn": Player.Play(Boarding3DAction.Turn, beatDuration); SetStatus("转向", "原地转身，观察模型侧面与背面"); break;
                case "attack": Player.Play(Boarding3DAction.Attack, beatDuration); SetStatus("挥砍", "蓄力 → 跨步挥刀 → 命中 → 收势"); break;
                case "counter": Enemy.Play(Boarding3DAction.Attack, beatDuration); SetStatus("受击", "敌方进攻，观察后仰和重心恢复"); break;
                case "cast": Player.Play(Boarding3DAction.Cast, beatDuration); SetStatus("施法", "抬手聚能，释放后恢复站姿"); break;
                case "guard": Player.Play(Boarding3DAction.Guard, beatDuration); Enemy.Play(Boarding3DAction.Attack, beatDuration); SetStatus("格挡", "敌方挥砍与我方防守配合"); break;
                case "heal": Player.Play(Boarding3DAction.Heal, beatDuration); SetStatus("恢复", "支援动作与柔和环形特效"); break;
                case "death": Enemy.Play(Boarding3DAction.Death, 1.25f); SetStatus("倒下", "倒地姿势保持，可用“复位”重新开始"); break;
                default: FinishBeat(); break;
            }
            BeatStarted?.Invoke(beat);
        }

        void AnimateBeat(float t)
        {
            Player.transform.localPosition = playerHome; Enemy.transform.localPosition = enemyHome;
            Player.transform.localRotation = Quaternion.Euler(0,90,0); Enemy.transform.localRotation = Quaternion.Euler(0,-90,0);
            if (CurrentBeat == "walk")
            {
                float distance = Mathf.Sin(t * Mathf.PI) * 1.65f;
                Player.transform.localPosition = playerHome + Vector3.right * distance;
                Player.transform.localRotation = Quaternion.Euler(0, t < .48f ? 90f : Mathf.Lerp(90, -90, Mathf.SmoothStep(0, 1, (t - .48f) / .12f)), 0);
            }
            else if (CurrentBeat == "turn")
                Player.transform.localRotation = Quaternion.Euler(0, 90f + 360f * Mathf.SmoothStep(0, 1, t), 0);
            else if (CurrentBeat == "attack" || CurrentBeat == "counter" || CurrentBeat == "guard")
            {
                bool playerAttacks = CurrentBeat == "attack";
                Boarding3DActor actor = playerAttacks ? Player : Enemy;
                Vector3 home = playerAttacks ? playerHome : enemyHome;
                float advance = Window(t, .12f, .40f, .57f, .94f) * 2.75f;
                actor.transform.localPosition = home + Vector3.right * advance * (playerAttacks ? 1f : -1f);
                actor.transform.localPosition += Vector3.forward * ((playerAttacks ? enemyHome.z : playerHome.z) - home.z) * (advance / 2.75f);
                if (t >= .47f && !impactDone)
                {
                    impactDone = true; CompletedAttacks++;
                    if (CurrentBeat != "guard")
                    {
                        (playerAttacks ? Enemy : Player).Play(Boarding3DAction.Hit, .65f);
                        ReceivedHits++;
                    }
                    SpawnImpact((playerAttacks ? Enemy : Player).transform.position + Vector3.up * 1.25f,
                        CurrentBeat == "guard" ? new Color(.3f, .82f, 1f) : Gold);
                }
                if (actor.WeaponTip != null)
                {
                    for (int i = trailPoints.Length - 1; i > 0; i--) trailPoints[i] = trailPoints[i - 1];
                    trailPoints[0] = actor.WeaponTip.position;
                    if (t < .15f) for (int i = 1; i < trailPoints.Length; i++) trailPoints[i] = trailPoints[0];
                    swordTrail.SetPositions(trailPoints); swordTrail.enabled = t > .32f && t < .61f;
                }
            }
            else if (CurrentBeat == "cast" && t >= .55f && !impactDone)
            {
                impactDone = true; Enemy.Play(Boarding3DAction.Hit, .65f); ReceivedHits++;
                SpawnImpact(Enemy.transform.position + Vector3.up * 1.2f, new Color(.35f, .95f, 1f));
                var beam = new GameObject("Spell streak"); beam.transform.SetParent(effects, false);
                var line = beam.AddComponent<LineRenderer>(); line.sharedMaterial = MakeMaterial(new Color(.3f, .85f, 1f), true);
                line.startWidth = .11f; line.endWidth = .06f; line.positionCount = 2;
                line.SetPosition(0, Player.transform.position + Vector3.up * 1.5f);
                line.SetPosition(1, Enemy.transform.position + Vector3.up * 1.2f);
                transient.Add(beam);
            }
            if (CurrentBeat == "heal") playerRing.localScale = Vector3.one * (1f + .25f * Mathf.Sin(t * Mathf.PI));
            float entrance = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(beatTime / .16f));
            Player.transform.localPosition = Vector3.Lerp(playerFrom, Player.transform.localPosition, entrance);
            Enemy.transform.localPosition = Vector3.Lerp(enemyFrom, Enemy.transform.localPosition, entrance);
            Player.transform.localRotation = Quaternion.Slerp(playerRotationFrom, Player.transform.localRotation, entrance);
            Enemy.transform.localRotation = Quaternion.Slerp(enemyRotationFrom, Enemy.transform.localRotation, entrance);
        }

        void FinishBeat()
        {
            bool dead = CurrentBeat == "death";
            CurrentBeat = "idle"; beatTime = 0f;
            Player.transform.localPosition = playerHome; Enemy.transform.localPosition = enemyHome;
            Enemy.transform.localRotation = Quaternion.Euler(0, -90, 0);
            swordTrail.enabled = false; playerRing.localScale = Vector3.one;
            ClearEffects();
            SetStatus(dead ? "倒地保持" : "待机", dead ? "敌方已倒下 · 点击复位或选择任意动作" : "呼吸与重心微动 · 选择下方动作进行比较");
        }

        public void ResetTrial(bool resetSequence = true)
        {
            Initialize(); ResetActors(); FinishBeat();
            if (resetSequence) { demoIndex = 0; CompletedAttacks = ReceivedHits = 0; SimulationTime = 0f; }
            demoWait = 1.4f;
        }

        void ResetActors()
        {
            Player.ResetPose(); Enemy.ResetPose();
            Player.transform.localPosition = playerHome; Enemy.transform.localPosition = enemyHome;
            Player.transform.localRotation = Quaternion.Euler(0, 90, 0); Enemy.transform.localRotation = Quaternion.Euler(0, -90, 0);
            ClearEffects(); if (swordTrail != null) swordTrail.enabled = false;
        }

        public void SetAutoDemo(bool enabled) { AutoDemo = enabled; demoWait = .6f; if (autoLabel != null) autoLabel.text = enabled ? "自动演示  开" : "自动演示  关"; }
        public void SetPaused(bool paused) { Paused = paused; if (pauseLabel != null) pauseLabel.text = paused ? "继续" : "暂停"; }
        public void SetSpeed(float speed) { Speed = Mathf.Clamp(speed, .25f, 2f); if (speedLabel != null) speedLabel.text = Speed < 1f ? "慢放  0.5×" : "速度  1×"; }
        void SetStatus(string action, string description) { if (status != null) status.text = action; if (hint != null) hint.text = description; }
        static float Window(float t, float a, float b, float c, float d) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a,b,t)) * (1f - Mathf.SmoothStep(0,1,Mathf.InverseLerp(c,d,t)));

        void BuildCamera()
        {
            var cameraObject = new GameObject("Fixed orthographic camera"); cameraObject.transform.SetParent(world, false);
            ViewCamera = cameraObject.AddComponent<Camera>();
            ViewCamera.orthographic = true; ViewCamera.orthographicSize = 2.65f;
            ViewCamera.transform.position = new Vector3(4.5f, 5f, -12f);
            ViewCamera.transform.LookAt(new Vector3(0, 1f, .15f));
            ViewCamera.clearFlags = CameraClearFlags.SolidColor; ViewCamera.backgroundColor = new Color(.12f, .25f, .29f);
            ViewCamera.nearClipPlane = .1f; ViewCamera.farClipPlane = 120f;
            ViewCamera.allowHDR = false; ViewCamera.allowMSAA = true;
            ViewCamera.rect = new Rect(0, .22f, 1, .68f);
            var sun = new GameObject("Warm sunset key"); sun.transform.SetParent(world, false);
            var light = sun.AddComponent<Light>(); light.type = LightType.Directional;
            light.color = new Color(1f, .85f, .65f); light.intensity = 1.2f; light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40, -25, 0);
            var fill = new GameObject("Ocean fill"); fill.transform.SetParent(world, false);
            var fillLight = fill.AddComponent<Light>(); fillLight.type = LightType.Directional;
            fillLight.color = new Color(.4f, .7f, 1f); fillLight.intensity = .45f;
            fill.transform.rotation = Quaternion.Euler(25, 155, 0);
        }

        void BuildEnvironment()
        {
            Material sea = MakeMaterial(new Color(.055f, .29f, .34f));
            Cube("Sea", new Vector3(0,-.65f,4), new Vector3(100,.15f,100), sea);
            Material hull = MakeMaterial(new Color(.16f,.09f,.065f));
            Cube("Deck foundation", new Vector3(0,-.24f,0), new Vector3(14,.45f,8), hull);
            for (int i = 0; i < 35; i++)
            {
                float shade = .86f + (i * 17 % 7) * .027f;
                Material wood = MakeMaterial(new Color(.46f,.29f,.16f) * shade);
                Cube("Timber plank " + i, new Vector3(-6.8f + i * .4f,0,0), new Vector3(.386f,.12f,7.8f),wood);
                for (int j = 0; j < 3; j++) Cube("Plank joint", new Vector3(-6.8f + i*.4f,.065f,-2.9f+j*2.7f+(i%2)*.9f),new Vector3(.38f,.008f,.014f),hull);
            }
            Material railing = MakeMaterial(new Color(.23f,.14f,.085f));
            Material brass = MakeMaterial(new Color(.62f,.44f,.2f));
            for (int i = -6; i <= 6; i += 2)
            {
                Cube("Aft rail post", new Vector3(i,.55f,3.5f), new Vector3(.19f,1.1f,.19f),railing);
                Cube("Post cap", new Vector3(i,1.12f,3.5f),new Vector3(.28f,.12f,.28f),brass);
            }
            Cube("Aft rail top",new Vector3(0,1f,3.5f),new Vector3(13,.16f,.20f),railing);
            Cube("Aft rail lower",new Vector3(0,.42f,3.5f),new Vector3(13,.10f,.12f),railing);
            Cylinder("Mast",new Vector3(-5.3f,3,2.7f),new Vector3(.32f,3,.32f),railing);
            Cylinder("Mast foot",new Vector3(-5.3f,.24f,2.7f),new Vector3(.56f,.24f,.56f),brass);
            Cube("Sail boom",new Vector3(-3.6f,4.7f,2.7f),new Vector3(5,.12f,.12f),railing);
            Cube("Furled canvas",new Vector3(-3.6f,4.5f,2.7f),new Vector3(4.7f,.32f,.28f),MakeMaterial(new Color(.78f,.72f,.56f)));
            Rope(new Vector3(-5.3f,5.5f,2.7f),new Vector3(-6.4f,.2f,-2.6f),.035f,brass);
            Rope(new Vector3(-5.3f,5.5f,2.7f),new Vector3(-1.8f,1,3.5f),.035f,brass);
            Barrel(new Vector3(5.8f,.06f,2.65f),railing,brass);
            Barrel(new Vector3(4.85f,.06f,2.9f),railing,brass);
            playerRing = Ring("Player footing",playerHome + Vector3.up*.002f,.53f,new Color(.16f,.65f,.64f));
            enemyRing = Ring("Enemy footing",enemyHome + Vector3.up*.002f,.53f,new Color(.7f,.28f,.22f));
        }

        void Barrel(Vector3 at,Material wood,Material band)
        {
            Cylinder("Barrel",at+Vector3.up*.45f,new Vector3(.64f,.45f,.64f),wood);
            Cylinder("Barrel band",at+Vector3.up*.19f,new Vector3(.67f,.04f,.67f),band);
            Cylinder("Barrel band",at+Vector3.up*.70f,new Vector3(.67f,.04f,.67f),band);
        }
        Transform Ring(string name,Vector3 position,float radius,Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(world,false); go.transform.localPosition=position;
            var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=MakeMaterial(color,true);
            line.useWorldSpace=false; line.loop=true; line.startWidth=line.endWidth=.028f; line.positionCount=48;
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            return go.transform;
        }
        void Rope(Vector3 a,Vector3 b,float width,Material material)
        {
            var go=new GameObject("Rigging rope");go.transform.SetParent(world,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.positionCount=2;
            line.startWidth=line.endWidth=width;line.SetPosition(0,a);line.SetPosition(1,b);
        }
        GameObject Cube(string name,Vector3 position,Vector3 scale,Material material) => Primitive(name,PrimitiveType.Cube,position,scale,material);
        GameObject Cylinder(string name,Vector3 position,Vector3 scale,Material material) => Primitive(name,PrimitiveType.Cylinder,position,scale,material);
        GameObject Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(world,false);
            go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;Destroy(go.GetComponent<Collider>());return go;
        }
        Material MakeMaterial(Color color,bool unlit=false)
        {
            string key = color.ToString("F4") + (unlit ? "/unlit" : "/lit");
            if (materialCache.TryGetValue(key, out Material existing)) return existing;
            Shader shader=Shader.Find(unlit?"Unlit/Color":"Standard");
            if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
            var material=new Material(shader){color=color};if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.12f);
            materials.Add(material);materialCache.Add(key,material);return material;
        }

        void SpawnImpact(Vector3 position,Color color)
        {
            ClearEffects(); impactFlash=.28f;
            Material material=MakeMaterial(color,true);
            for(int i=0;i<9;i++)
            {
                var spark=GameObject.CreatePrimitive(PrimitiveType.Cube);spark.name="Contact spark";
                spark.transform.SetParent(effects,false);spark.transform.position=position;
                float a=i*2.399f;spark.transform.rotation=Quaternion.Euler(i*19,i*41,a*Mathf.Rad2Deg);
                spark.transform.localScale=new Vector3(.035f,.035f,.28f);spark.GetComponent<Renderer>().sharedMaterial=material;
                Destroy(spark.GetComponent<Collider>());transient.Add(spark);
            }
        }
        void AnimateEffects(float dt)
        {
            if(impactFlash<=0)return;impactFlash-=dt;
            for(int i=0;i<transient.Count;i++)if(transient[i]!=null&&transient[i].name=="Contact spark")
            {float a=i*2.399f;transient[i].transform.position+=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.6f,Mathf.Sin(a))*.8f*dt;}
            if(impactFlash<=0)ClearEffects();
        }
        void ClearEffects(){foreach(var item in transient)if(item!=null)Destroy(item);transient.Clear();impactFlash=0;}

        void BuildInterface()
        {
            font=Resources.Load<Font>("Fonts/HarborBody-Regular");
            if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
            var canvasObject=new GameObject("Trial interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            Transform ui=canvasObject.transform;
            Panel(ui,"Top bar",new Vector2(0,800),new Vector2(1600,100),Ink);
            Label(ui,"Chapter","BOARDING  /  MOTION STUDY  01",new Vector2(48,857),new Vector2(650,25),16,Gold);
            Label(ui,"Title","甲板交锋",new Vector2(48,809),new Vector2(400,45),34,Color.white);
            Label(ui,"Subtitle","固定视角 · 3D 战斗角色",new Vector2(270,817),new Vector2(500,30),20,new Color(.65f,.77f,.78f));
            Label(ui,"Prototype","动作原型  /  不影响正式存档",new Vector2(1160,828),new Vector2(390,30),17,new Color(.65f,.77f,.78f),TextAnchor.MiddleRight);
            Panel(ui,"Control dock",Vector2.zero,new Vector2(1600,198),Ink);
            Panel(ui,"Gold rule",new Vector2(0,197),new Vector2(1600,2),Gold*.75f);
            Label(ui,"Actor left","我方船长",new Vector2(55,742),new Vector2(200,28),20,new Color(.65f,.94f,.87f));
            Label(ui,"Actor right","敌方掠夺者",new Vector2(1300,742),new Vector2(245,28),20,new Color(.98f,.7f,.58f),TextAnchor.MiddleRight);
            status=Label(ui,"Current action","待机",new Vector2(48,145),new Vector2(170,35),26,Gold);
            hint=Label(ui,"Motion detail","呼吸与重心微动 · 选择下方动作进行比较",new Vector2(220,150),new Vector2(850,28),18,new Color(.70f,.79f,.80f));
            string[] labels={"移动","转向","挥砍","施法","格挡","受击","恢复","倒下"};
            string[] beats={"walk","turn","attack","cast","guard","counter","heal","death"};
            for(int i=0;i<labels.Length;i++)
            {
                string chosen=beats[i];
                Button button=Button(ui,labels[i],new Vector2(48+i*133,69),new Vector2(120,56),()=>{SetAutoDemo(false);SetPaused(false);BeginBeat(chosen);},out _);
                var number=Label(button.transform,"Shortcut",(i+1).ToString(),new Vector2(7,36),new Vector2(15,16),11,new Color(.55f,.66f,.67f));
            }
            Button(ui,"复位",new Vector2(1132,69),new Vector2(120,56),()=>ResetTrial(),out _);
            Button(ui,"暂停",new Vector2(1265,69),new Vector2(120,56),()=>SetPaused(!Paused),out pauseLabel);
            Button(ui,"速度  1×",new Vector2(1398,69),new Vector2(155,56),()=>SetSpeed(Speed<1f?1f:.5f),out speedLabel);
            Button(ui,"自动演示  开",new Vector2(1320,144),new Vector2(233,35),()=>SetAutoDemo(!AutoDemo),out autoLabel);
            Label(ui,"Footer","1–8 选择动作     空格 暂停     R 复位     Esc 退出",new Vector2(48,24),new Vector2(1000,24),15,new Color(.45f,.58f,.61f));
            Label(ui,"Camera label","固定正交镜头  /  角色动作可慢放",new Vector2(1090,24),new Vector2(462,24),15,new Color(.45f,.58f,.61f),TextAnchor.MiddleRight);
            if(EventSystem.current==null)
            {
                var events=new GameObject("Trial input",typeof(EventSystem));events.transform.SetParent(transform,false);
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
        }
        RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        void Panel(Transform parent,string name,Vector2 position,Vector2 size,Color color){var image=Rect(parent,name,position,size).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;}
        Text Label(Transform parent,string name,string value,Vector2 position,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var text=Rect(parent,name,position,size).gameObject.AddComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;
            text.color=color;text.alignment=alignment;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Overflow;return text;
        }
        Button Button(Transform parent,string title,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action,out Text label)
        {
            var rect=Rect(parent,title,position,size);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.085f,.16f,.18f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            var colors=button.colors;colors.highlightedColor=new Color(.5f,.8f,.77f);colors.pressedColor=Gold;button.colors=colors;
            label=Label(rect,"Label",title,Vector2.zero,size,19,new Color(.9f,.92f,.86f),TextAnchor.MiddleCenter);return button;
        }
        void ReadKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.spaceKey.wasPressedThisFrame)SetPaused(!Paused);
            if(keyboard.rKey.wasPressedThisFrame)ResetTrial();
            if(keyboard.escapeKey.wasPressedThisFrame)Application.Quit();
            var keys=new[]{Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8};
            for(int i=0;i<keys.Length;i++)if(keyboard[keys[i]].wasPressedThisFrame)ManualBeat(i);
#else
            if(Input.GetKeyDown(KeyCode.Space))SetPaused(!Paused);
            if(Input.GetKeyDown(KeyCode.R))ResetTrial();
            if(Input.GetKeyDown(KeyCode.Escape))Application.Quit();
            for(int i=0;i<8;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))ManualBeat(i);
#endif
        }
        void ManualBeat(int index){SetAutoDemo(false);SetPaused(false);BeginBeat(new[]{"walk","turn","attack","cast","guard","counter","heal","death"}[index]);}
        void OnApplicationFocus(bool focus){focusLost=!focus;}
        void OnApplicationPause(bool paused){applicationPaused=paused;}
        void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);}
    }
}
