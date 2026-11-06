using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class BattleSchoolAttackGraphic
    {
        public Vector2 SourceDeck { get; private set; }
        public Vector2 TargetDeck { get; private set; }
        public float SkyY { get; private set; }
        private RawImage[] boardingSprites;
        private RawImage guardSprite;

        public static void ReleaseLoadedTextures() => BoardingRemasterFxArt.ReleaseLoadedTextures();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBoardingArtCache() => ReleaseLoadedTextures();
        private float Facing => end.x >= start.x ? 1f : -1f;
        private Vector2 SpellTarget => outcome == BattleSchoolAttackOutcome.Missed ? end + Vector2.right * Facing * 180f : end;
        private Vector2 SpellFloor => new Vector2(SpellTarget.x, TargetDeck.y);

        public void ConfigureBoardingEnvironment(Vector2 sourceDeck, Vector2 targetDeck, float skyY)
        {
            SourceDeck = Safe(sourceDeck); TargetDeck = Safe(targetDeck); SkyY = Finite(skyY);
            UpdateBoardingSprites(); SetVerticesDirty();
        }

        private void ConfigureBoardingSprites()
        {
            if (boardingSprites != null)
                foreach (RawImage element in boardingSprites) RetireElement(element);
            RetireElement(guardSprite);
            boardingSprites = null;
            guardSprite = null;
            if (!boardingStage || BoardingRemasterFxArt.Load() == null) return;
            int count = kind == BattleSchoolAttackKind.Fire ? 3 : 1;
            boardingSprites = new RawImage[count];
            bool anchoredToDeck = kind == BattleSchoolAttackKind.Fire || kind == BattleSchoolAttackKind.Tide ||
                kind == BattleSchoolAttackKind.Curse || kind == BattleSchoolAttackKind.Thunder;
            for (int i = 0; i < count; i++)
                boardingSprites[i] = BoardingRemasterFxArt.CreateLayer(rectTransform,
                    "Boarding " + kind + " Element " + i, anchoredToDeck ? 0f : .5f);
            if (outcome == BattleSchoolAttackOutcome.Guarded)
                guardSprite = BoardingRemasterFxArt.CreateLayer(rectTransform, "Boarding Parry");
            UpdateBoardingSprites();
        }

        private static void RetireElement(RawImage element)
        {
            if (element == null) return;
            element.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(element.gameObject);
            else DestroyImmediate(element.gameObject);
        }

        private void UpdateBoardingSprites()
        {
            if (boardingSprites == null) return;
            float travel = Mathf.Clamp01(progress / ContactAt);
            float after = Mathf.InverseLerp(ContactAt, 1f, progress);
            float fade = Mathf.Clamp01(progress * 15f) * Mathf.Clamp01((1f - progress) * 5f);
            if (outcome == BattleSchoolAttackOutcome.Guarded) fade *= .69f;
            int row = BoardingRemasterFxArt.SchoolRow(kind);
            for (int i = 0; i < boardingSprites.Length; i++)
            {
                float time = progress;
                Vector2 position = SpellTarget, extent;
                bool mirror = Facing < 0f;
                switch (kind)
                {
                    case BattleSchoolAttackKind.Fire:
                        time = Mathf.InverseLerp(ContactAt - .10f + i * .018f, 1f, progress);
                        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time * 6f));
                        float height = (i == 1 ? 300f : 205f) * rise;
                        extent = new Vector2(i == 1 ? 204f : 145f, height);
                        position = SpellFloor + new Vector2((i - 1) * 68f, -height * .035f);
                        mirror = i == 1;
                        break;
                    case BattleSchoolAttackKind.Tide:
                        float head = Mathf.Lerp(SourceDeck.x, SpellFloor.x, travel) + Facing * after * 128f;
                        float crest = 145f + 68f * travel - after * 79f;
                        extent = new Vector2(333f, crest);
                        position = new Vector2(head - Facing * 104f, TargetDeck.y - crest * .10f);
                        break;
                    case BattleSchoolAttackKind.Curse:
                        float reach = Mathf.SmoothStep(0f, 1f, travel);
                        extent = new Vector2(128f + 62f * reach, 54f + 214f * reach);
                        position = SpellFloor - Vector2.up * 5f;
                        break;
                    case BattleSchoolAttackKind.Thunder:
                        float fall = Mathf.InverseLerp(.14f, ContactAt, progress);
                        float boltHeight = Mathf.Lerp(48f, Mathf.Max(50f, SkyY - SpellFloor.y), fall);
                        extent = new Vector2(138f + 45f * fall, boltHeight);
                        position = new Vector2(SpellTarget.x, SkyY - boltHeight);
                        time = Mathf.Lerp(0f, .48f, travel) + after * .52f;
                        break;
                    case BattleSchoolAttackKind.Arcane:
                        position = Vector2.Lerp(start + Vector2.right * Facing * 60f, SpellTarget,
                            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.40f, 1f, travel)));
                        extent = Vector2.one * (progress < ContactAt ? 92f + travel * 16f : 155f + after * 57f);
                        break;
                    default:
                        position = progress < ContactAt ? SpellTarget : BloodPath(after, 1);
                        extent = new Vector2(145f, 157f) * (1f - after * .39f);
                        break;
                }
                BoardingRemasterFxArt.Pose(boardingSprites[i], row, time, position, extent, fade, mirror);
            }
            if (guardSprite != null)
                BoardingRemasterFxArt.Pose(guardSprite, BoardingRemasterFxArt.Guard, after, end,
                    new Vector2(159f, 196f), progress < ContactAt ? 0f : Mathf.Clamp01((1f - after) * 5f),
                    Facing > 0f);
        }

        private void DrawBoardingScene(VertexHelper mesh)
        {
            switch (kind)
            {
                case BattleSchoolAttackKind.Fire: DrawDeckEruption(mesh); break;
                case BattleSchoolAttackKind.Thunder: DrawSkyStrike(mesh); break;
                case BattleSchoolAttackKind.Tide: DrawDeckBreaker(mesh); break;
                case BattleSchoolAttackKind.Arcane: DrawArcaneConvergence(mesh); break;
                case BattleSchoolAttackKind.Curse: DrawCreepingCurse(mesh); break;
                case BattleSchoolAttackKind.Blood: DrawBloodReturn(mesh); break;
            }
            if (progress >= ContactAt && outcome == BattleSchoolAttackOutcome.Guarded)
            {
                float life = Mathf.Sin(Mathf.InverseLerp(ContactAt, 1f, progress) * Mathf.PI);
                Color ice = new Color(.7f, .95f, 1f, life * .8f);
                // Only a guard has the shield shape; the school's attack remains intact outside it.
                Vector2 c = end;
                Line(mesh,c+new Vector2(-48,48),c+new Vector2(48,48),4f,ice);
                Line(mesh,c+new Vector2(48,48),c+new Vector2(38,-15),4f,ice);
                Line(mesh,c+new Vector2(38,-15),c+new Vector2(0,-54),4f,ice);
                Line(mesh,c+new Vector2(0,-54),c+new Vector2(-38,-15),4f,ice);
                Line(mesh,c+new Vector2(-38,-15),c+new Vector2(-48,48),4f,ice);
            }
        }

        private void DrawDeckEruption(VertexHelper mesh)
        {
            Vector2 floor = SpellFloor;
            float heat = Mathf.Clamp01(progress / ContactAt);
            float fade = Mathf.Clamp01((1f-progress)*3f);
            SoftEllipse(mesh, floor, new Vector2(160f, 17f), new Color(1f,.25f,.025f, (.18f + .3f*heat)*fade));
            // Heat crawls along the deck seams before the fire rises vertically.
            for(int seam=0;seam<5;seam++)
            {
                Vector2 a=floor+new Vector2(-125f+seam*49f,(seam%2)*6f);
                Line(mesh,a,a+new Vector2(35f*heat,3f),2.5f,new Color(1f,.49f,.08f,fade*heat));
            }
            float t = Mathf.InverseLerp(ContactAt-.06f,1f,progress);
            if(t<=0f) return;
            float life=Mathf.Clamp01(t*8f)*Mathf.Clamp01((1f-t)*2f);
            if(boardingSprites==null)
                for(int i=0;i<7;i++)
                {
                    Vector2 basePoint=floor+new Vector2((i-3)*38f,0);
                    float h=(170f+70f*Mathf.Sin(i*1.7f))*Mathf.Clamp01(t*5f);
                    Flame(mesh,basePoint,25f,h,progress*18f+i,new Color(1f,.29f,.035f,life*.85f));
                    Flame(mesh,basePoint,13f,h*.67f,progress*18f+i,new Color(1f,.85f,.32f,life));
                }
            for(int i=0;i<20;i++)
            {
                float age=Mathf.Repeat(t*1.5f+i*.137f,1f);
                Vector2 at=floor+new Vector2(Mathf.Sin(i*2.3f)*140f+Mathf.Sin(age*7f+i)*17f,25f+age*255f);
                Line(mesh,at,at+new Vector2(2f,6f+age*9f),2f,new Color(1f,.64f,.15f,life*(1f-age)));
            }
            for(int i=0;i<5;i++)
                SoftEllipse(mesh,floor+new Vector2((i-2)*45f,130f+t*150f+i%2*32f),new Vector2(34f+t*30f,30f+t*28f),new Color(.17f,.105f,.085f,life*.20f));
        }

        private void DrawSkyStrike(VertexHelper mesh)
        {
            float charge=Mathf.Clamp01(progress/(ContactAt-.10f));
            float cloudAlpha=Mathf.Clamp01(progress*12f)*Mathf.Clamp01((.85f-progress)*3f);
            Vector2 top=new Vector2(SpellTarget.x+Facing*34f,SkyY-13f);
            for(int i=0;i<5;i++)
                SoftEllipse(mesh,top+new Vector2((i-2)*35f,-8f-(i%2)*9f),new Vector2(58f,22f),new Color(.09f,.13f,.22f,cloudAlpha*.30f));
            SoftEllipse(mesh,top+Vector2.down*18f,new Vector2(45f*charge,12f),new Color(.59f,.76f,1f,cloudAlpha*.6f));
            if(progress<ContactAt-.11f)
            {
                for(int i=0;i<4;i++)
                    Lightning(mesh,top+new Vector2(-70+i*25f,-14f),top+new Vector2(-45+i*25f,-26f),3,.4f*charge, i);
                return;
            }
            float descend=Mathf.Clamp01((progress-(ContactAt-.11f))/.11f);
            Vector2 tip=Vector2.Lerp(top,SpellFloor,descend);
            float flash=progress<ContactAt?1f:Mathf.Clamp01(1f-(progress-ContactAt)/.22f);
            float echo=Mathf.Clamp01(1f-Mathf.Abs(progress-.65f)/.06f)*.58f;
            float strength=Mathf.Max(flash,echo)*Mathf.Clamp01((1f-progress)*5f);
            Lightning(mesh,top,tip,11,strength,0);
            if(descend>.55f)
            {
                Lightning(mesh,Vector2.Lerp(top,tip,.37f),SpellTarget+new Vector2(-95f,45f),5,strength*.7f,3);
                Lightning(mesh,Vector2.Lerp(top,tip,.57f),SpellTarget+new Vector2(96f,18f),5,strength*.65f,7);
            }
            if(progress>=ContactAt)
            {
                SoftEllipse(mesh,SpellFloor,new Vector2(100f,16f),new Color(.65f,.82f,1f,strength*.7f));
                for(int i=0;i<10;i++)
                {
                    float a=i*Mathf.PI/9f;
                    Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    Line(mesh,SpellFloor+d*15f,SpellFloor+d*(55f+(progress-ContactAt)*80f),2f,new Color(.9f,.94f,1f,strength*.8f));
                }
            }
        }

        private void DrawDeckBreaker(VertexHelper mesh)
        {
            float travel=Mathf.Clamp01(progress/ContactAt), after=Mathf.InverseLerp(ContactAt,1f,progress);
            float head=Mathf.Lerp(SourceDeck.x,SpellFloor.x,travel)+Facing*after*130f;
            float life=Mathf.Clamp01(progress*14f)*Mathf.Clamp01((1f-progress)*2.3f);
            Color water=new Color(.08f,.61f,.85f,life*.6f), foam=new Color(.78f,.98f,1f,life*.9f);
            // Long, flat water sheet stays on the floor, trailing a moving crest.
            for(int row=0;row<3;row++)
            {
                Vector2 previous=new Vector2(head-Facing*350f,TargetDeck.y+row*7f);
                for(int i=1;i<=22;i++)
                {
                    float u=i/22f;
                    Vector2 next=new Vector2(head-Facing*350f*(1f-u),TargetDeck.y+row*7f+Mathf.Sin(u*17f-progress*15f+row)*4f);
                    Line(mesh,previous,next,9f-row*2f,WithAlpha(water,water.a*u)); previous=next;
                }
            }
            if(boardingSprites==null)
            {
                Vector2 crest=new Vector2(head-Facing*20f,TargetDeck.y+90f);
                Vector2 previous=new Vector2(head-Facing*250f,TargetDeck.y+5f);
                for(int i=1;i<=24;i++)
                {
                    float u=i/24f;
                    Vector2 next=new Vector2(head-Facing*250f*(1f-u),TargetDeck.y+Mathf.Pow(u,2.7f)*155f);
                    Triangle(mesh,previous,next,new Vector2(next.x,TargetDeck.y),water);
                    Line(mesh,previous,next,5f,foam); previous=next;
                }
                for(int i=0;i<18;i++)
                {
                    float a=i/18f*Mathf.PI*1.3f;
                    Vector2 p=crest+new Vector2(Mathf.Sin(a)*Facing*58f,Mathf.Cos(a)*60f);
                    Vector2 q=crest+new Vector2(Mathf.Sin(a+.18f)*Facing*58f,Mathf.Cos(a+.18f)*60f);
                    Line(mesh,p,q,11f-i*.4f,foam);
                }
            }
            for(int i=0;i<16;i++)
            {
                float age=Mathf.Repeat(progress*1.6f+i*.173f,1f);
                Vector2 p=new Vector2(head+Facing*(age*85f-30f),TargetDeck.y+35f+Mathf.Sin(age*Mathf.PI)*135f+i%3*14f);
                Line(mesh,p,p+new Vector2(Facing*4f,7f),2.5f,WithAlpha(foam,foam.a*(1f-age)));
            }
        }

        private void DrawArcaneConvergence(VertexHelper mesh)
        {
            float travel=Mathf.Clamp01(progress/ContactAt), t=Mathf.InverseLerp(ContactAt,1f,progress);
            Color violet=new Color(.59f,.40f,1f,Mathf.Clamp01((1f-progress)*3f));
            Vector2 focus=start+Vector2.right*Facing*68f;
            for(int lane=0;lane<3;lane++)
            {
                Vector2 source=focus+new Vector2(-Facing*25f, (lane-1)*66f);
                float release=Mathf.Clamp01((travel-.48f)/.52f);
                Vector2 projectile=Vector2.Lerp(source,SpellTarget,release);
                if(progress<ContactAt)
                {
                    CrystalShard(mesh,projectile,13f+travel*6f,progress*3f+lane,violet);
                    SoftEllipse(mesh,source,new Vector2(22f,30f),WithAlpha(violet,(1f-release)*.4f));
                    if(release>0f) LayeredLine(mesh,Vector2.Lerp(source,SpellTarget,Mathf.Max(0f,release-.22f)),projectile,4f,violet);
                }
                else
                {
                    float flash=Mathf.Clamp01(1f-t*5f);
                    LayeredLine(mesh,source,SpellTarget,3f,WithAlpha(violet,flash));
                }
            }
            if(progress<ContactAt) return;
            // Shattered, outward-moving crystals replace a circular hit glyph.
            for(int i=0;i<12;i++)
            {
                float a=i*2.399f;
                Vector2 at=SpellTarget+Radial(a)*(18f+t*150f);
                CrystalShard(mesh,at,(8f+i%3*4f)*(1f-t),a+t*2f,WithAlpha(violet,(1f-t)*.9f));
            }
            SoftEllipse(mesh,SpellTarget,new Vector2(35f,65f),WithAlpha(violet,Mathf.Clamp01(1f-t*4f)*.5f));
        }

        private void DrawCreepingCurse(VertexHelper mesh)
        {
            float growth=Mathf.Clamp01(progress/ContactAt), t=Mathf.InverseLerp(ContactAt,1f,progress);
            float life=Mathf.Clamp01(progress*12f)*Mathf.Clamp01((1f-progress)*3f);
            SoftEllipse(mesh,SpellFloor,new Vector2(145f,20f),new Color(.045f,.015f,.07f,life*.76f));
            Color shadow=new Color(.12f,.035f,.19f,life*.85f), venom=new Color(.46f,.78f,.24f,life*.72f);
            // Roots climb from the deck, curl around the silhouette, and tighten at contact.
            for(int stem=0;stem<6;stem++)
            {
                Vector2 previous=SpellFloor+new Vector2((stem-2.5f)*34f,0);
                for(int i=1;i<=22;i++)
                {
                    float u=i/22f*growth;
                    float x=Mathf.Sin(u*8f+stem*1.5f)*(75f-22f*t)+(stem-2.5f)*15f*(1f-u);
                    Vector2 next=SpellFloor+new Vector2(x,u*(220f+stem%2*45f));
                    Line(mesh,previous,next,9f*(1f-u*.6f),shadow);
                    Line(mesh,previous+Vector2.right*2f,next+Vector2.right*2f,2f,venom);
                    if(i%6==0) Triangle(mesh,next,next+new Vector2((stem%2==0?1:-1)*15f,12f),previous,shadow);
                    previous=next;
                }
            }
            for(int i=0;i<5;i++)
                SoftEllipse(mesh,SpellFloor+new Vector2(Mathf.Sin(i*2f+t)*80f,50f+growth*170f+i*7f),new Vector2(36f,22f),new Color(.19f,.06f,.24f,life*.18f));
        }

        private void DrawBloodReturn(VertexHelper mesh)
        {
            Color blood=new Color(.76f,.035f,.12f,Mathf.Clamp01((1f-progress)*3f));
            float cut=Mathf.Clamp01((progress-.13f)/(ContactAt-.13f));
            if(progress<.56f)
            {
                for(int i=0;i<2;i++)
                {
                    Vector2 a=SpellTarget+new Vector2(-65f+i*25f,90f),b=SpellTarget+new Vector2(62f+i*25f,-65f);
                    Vector2 tip=Vector2.Lerp(a,b,cut);
                    LayeredLine(mesh,a,tip,7f-i*2f,WithAlpha(blood,Mathf.Clamp01((.56f-progress)*7f)));
                }
            }
            if(progress<ContactAt) return;
            float recall=Mathf.Clamp01((progress-ContactAt)/(1f-ContactAt));
            for(int stream=0;stream<3;stream++)
            {
                float head=Mathf.Clamp01(recall*1.25f-stream*.07f);
                float tail=Mathf.Max(0f,head-.20f);
                Vector2 previous=BloodPath(tail,stream);
                for(int i=1;i<=18;i++)
                {
                    float u=Mathf.Lerp(tail,head,i/18f);
                    Vector2 next=BloodPath(u,stream);
                    Line(mesh,previous,next,(3f+stream)*i/18f,WithAlpha(blood,blood.a*i/18f)); previous=next;
                }
                SoftEllipse(mesh,BloodPath(head,stream),new Vector2(8f,5f),WithAlpha(blood,.9f*(1f-recall)));
            }
            SoftEllipse(mesh,start,new Vector2(30f,50f),WithAlpha(blood,Mathf.Sin(recall*Mathf.PI)*.17f));
        }
        private Vector2 BloodPath(float u,int lane) => Vector2.Lerp(SpellTarget,start,u)+Vector2.up*Mathf.Sin(u*Mathf.PI)*(85f+lane*35f);

        private void Lightning(VertexHelper mesh,Vector2 a,Vector2 b,int steps,float alpha,int seed)
        {
            if(alpha<=.001f) return;
            Vector2 previous=a;
            for(int i=1;i<=steps;i++)
            {
                float t=i/(float)steps;
                float jitter=i==steps?0f:Mathf.Sin(i*18.31f+seed*7.1f)*25f;
                Vector2 next=Vector2.Lerp(a,b,t)+Vector2.right*jitter;
                Line(mesh,previous,next,13f,new Color(.36f,.62f,1f,alpha*.17f));
                Line(mesh,previous,next,5f,new Color(.55f,.8f,1f,alpha*.9f));
                Line(mesh,previous,next,2.2f,new Color(.97f,.99f,1f,alpha));previous=next;
            }
        }
        private void SoftEllipse(VertexHelper mesh,Vector2 center,Vector2 radius,Color tint)
        {
            if(tint.a<=.001f) return;
            for(int i=0;i<24;i++)
            {
                int n=mesh.currentVertCount; Vertex(mesh,center,tint);
                Vertex(mesh,center+Vector2.Scale(Radial(i*Mathf.PI/12f),radius),WithAlpha(tint,0f));
                Vertex(mesh,center+Vector2.Scale(Radial((i+1)*Mathf.PI/12f),radius),WithAlpha(tint,0f));
                mesh.AddTriangle(n,n+1,n+2);
            }
        }
        private void CrystalShard(VertexHelper mesh,Vector2 center,float radius,float angle,Color tint)
        {
            Vector2 d=Radial(angle)*radius,n=new Vector2(-d.y,d.x)*.42f;
            Triangle(mesh,center+d,center+n,center-d,tint);
            Triangle(mesh,center+d,center-d,center-n,Color.Lerp(tint,new Color(1f,1f,1f,tint.a),.6f));
        }
        private void LayeredLine(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color tint)
        {
            Line(mesh,a,b,width*3f,WithAlpha(tint,tint.a*.14f));Line(mesh,a,b,width,tint);
            Line(mesh,a,b,width*.28f,Color.Lerp(tint,new Color(1f,1f,1f,tint.a),.65f));
        }
        private void Flame(VertexHelper mesh,Vector2 at,float width,float height,float sway,Color tint)
        {
            Vector2 previousLeft=at-Vector2.right*width,previousRight=at+Vector2.right*width;
            for(int i=1;i<=12;i++)
            {
                float u=i/12f; float offset=Mathf.Sin(u*8f+sway)*width*u;
                Vector2 c=at+new Vector2(offset,u*height);float w=width*Mathf.Pow(1f-u,.7f);
                Vector2 l=c-Vector2.right*w,r=c+Vector2.right*w;
                Triangle(mesh,previousLeft,previousRight,r,tint);Triangle(mesh,previousLeft,r,l,tint);
                previousLeft=l;previousRight=r;
            }
        }
    }
}
