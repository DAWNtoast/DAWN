using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BattleCommandIconKind
    {
        Cannon, Melee, Ram, Guard, Repair, Empower, Curse, Move, Wait, Fire,
        Energy, ActionPoint, Mana, TidePower, ThunderChain, CurseMark, Blood,
        Powder, Cannonball, Planks, Medicine, Gold, Food, DrawPile, DiscardPile,
        Load, Morale, Supplies
    }

    /// <summary>Outlined nautical command, resource and deck symbols without font dependencies.</summary>
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class BattleCommandIconGraphic : MaskableGraphic
    {
        [SerializeField] private BattleCommandIconKind kind;
        private Vector2 center;
        private float scale;
        public BattleCommandIconKind Kind => kind;
        private static Color Ink => Rgb(18, 27, 35);
        private static Color Ivory => Rgb(255, 242, 204);
        private static Color Steel => Rgb(169, 214, 231);
        private static Color Gold => Rgb(255, 198, 65);
        private static Color Aqua => Rgb(59, 219, 208);
        private static Color Rose => Rgb(249, 83, 106);
        private static Color Violet => Rgb(190, 132, 255);
        private static Color Timber => Rgb(181, 114, 61);

        public void Configure(BattleCommandIconKind kind)
        {
            this.kind = kind;
            color = Color.white;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            center = rect.center;
            scale = Mathf.Min(rect.width, rect.height) * .46f;
            switch (kind)
            {
                case BattleCommandIconKind.Cannon:
                    Polygon(mesh, Steel, V(-.77f,.18f), V(.6f,.68f), V(.78f,.2f), V(-.6f,-.27f));
                    Stroke(mesh, Gold, .13f, V(.58f,.7f), V(.8f,.17f));
                    Stroke(mesh, Timber, .23f, V(-.69f,-.31f), V(.5f,-.31f));
                    Circle(mesh, V(-.46f,-.51f), .28f, Timber);
                    Circle(mesh, V(.38f,-.51f), .28f, Timber);
                    Circle(mesh, V(-.46f,-.51f), .08f, Ivory, false);
                    Circle(mesh, V(.38f,-.51f), .08f, Ivory, false);
                    break;
                case BattleCommandIconKind.Melee:
                    Sword(mesh, true, Steel); Sword(mesh, false, Ivory); break;
                case BattleCommandIconKind.Ram:
                    Polygon(mesh, Timber, V(-.63f,.58f), V(.52f,.36f), V(.9f,0f), V(.43f,-.61f), V(-.66f,-.53f));
                    Polygon(mesh, Steel, V(.48f,.34f), V(.93f,0f), V(.43f,-.55f), V(.23f,-.19f));
                    Stroke(mesh, Aqua, .12f, V(-.86f,.78f), V(-.2f,.78f));
                    Stroke(mesh, Aqua, .12f, V(-.92f,-.78f), V(.04f,-.78f));
                    break;
                case BattleCommandIconKind.Guard: Shield(mesh, Aqua); break;
                case BattleCommandIconKind.Repair:
                    Plank(mesh, V(0f,-.36f), .15f);
                    Stroke(mesh, Timber, .23f, V(-.49f,-.66f), V(.31f,.55f));
                    Polygon(mesh, Steel, V(-.06f,.72f), V(.29f,.88f), V(.8f,.42f), V(.56f,.13f));
                    break;
                case BattleCommandIconKind.Empower:
                    Sword(mesh, false, Steel);
                    Arrow(mesh, V(-.55f,.06f), V(-.55f,.69f), Gold, .2f); break;
                case BattleCommandIconKind.Curse: Skull(mesh); break;
                case BattleCommandIconKind.Move:
                    Arrow(mesh, V(-.75f,.28f), V(.7f,.28f), Aqua, .23f);
                    Stroke(mesh, Steel, .14f, V(-.7f,-.23f), V(-.15f,-.23f));
                    Stroke(mesh, Steel, .14f, V(-.43f,-.61f), V(.15f,-.61f)); break;
                case BattleCommandIconKind.Wait: Hourglass(mesh); break;
                case BattleCommandIconKind.Fire: Flame(mesh); break;
                case BattleCommandIconKind.Energy:
                    Circle(mesh, Vector2.zero, .72f, Rgb(54,155,220));
                    Bolt(mesh, Ivory, .8f);
                    break;
                case BattleCommandIconKind.ActionPoint:
                    Circle(mesh, Vector2.zero, .55f, Gold);
                    Circle(mesh, Vector2.zero, .28f, Ink, false);
                    for (int i=0; i<8; i++)
                    {
                        float angle = i*Mathf.PI*.25f;
                        Vector2 unit = V(Mathf.Cos(angle),Mathf.Sin(angle));
                        Stroke(mesh, Gold,.13f,unit*.45f,unit*.85f);
                    }
                    break;
                case BattleCommandIconKind.Mana:
                    Polygon(mesh, Rgb(103,164,255), V(0f,.9f), V(.64f,.32f), V(.44f,-.58f), V(0f,-.89f), V(-.44f,-.58f), V(-.64f,.32f));
                    Stroke(mesh, Ivory,.07f,V(0f,.75f),V(-.23f,.1f),V(0f,-.72f));
                    Stroke(mesh, Rgb(51,88,168),.07f,V(-.55f,.27f),V(.55f,.27f)); break;
                case BattleCommandIconKind.TidePower: Waves(mesh); break;
                case BattleCommandIconKind.ThunderChain:
                    Ring(mesh,V(-.42f,-.35f),.4f,Steel);
                    Ring(mesh,V(.42f,.35f),.4f,Steel);
                    Bolt(mesh,Gold,.78f); break;
                case BattleCommandIconKind.CurseMark:
                    Polygon(mesh,Violet,V(-.9f,0f),V(-.4f,.44f),V(.4f,.44f),V(.9f,0f),V(.4f,-.44f),V(-.4f,-.44f));
                    Circle(mesh,Vector2.zero,.28f,Ink,false);
                    Polygon(mesh,Rose,V(0f,.23f),V(.12f,0f),V(0f,-.23f),V(-.12f,0f));
                    Stroke(mesh,Violet,.11f,V(0f,.57f),V(0f,.84f));
                    Stroke(mesh,Violet,.11f,V(0f,-.57f),V(0f,-.84f)); break;
                case BattleCommandIconKind.Blood: Drop(mesh,Rose); break;
                case BattleCommandIconKind.Powder:
                    Barrel(mesh);
                    Circle(mesh,V(0f,-.02f),.21f,Ink,false);
                    Stroke(mesh,Gold,.11f,V(.02f,.2f),V(.21f,.4f)); break;
                case BattleCommandIconKind.Cannonball:
                    Circle(mesh,V(-.07f,-.08f),.7f,Steel);
                    Circle(mesh,V(-.28f,.18f),.2f,Ivory,false);
                    Stroke(mesh,Gold,.12f,V(.39f,.48f),V(.67f,.76f)); break;
                case BattleCommandIconKind.Planks:
                    Plank(mesh,V(0f,.42f),-.12f); Plank(mesh,V(0f,-.18f),.14f); break;
                case BattleCommandIconKind.Medicine:
                    Polygon(mesh,Rgb(109,206,163),V(-.26f,.76f),V(.26f,.76f),V(.26f,.3f),V(.59f,.03f),V(.59f,-.72f),V(-.59f,-.72f),V(-.59f,.03f),V(-.26f,.3f));
                    Stroke(mesh,Gold,.15f,V(-.31f,.78f),V(.31f,.78f));
                    Cross(mesh,V(0f,-.27f),.34f,Ivory); break;
                case BattleCommandIconKind.Gold:
                    Coin(mesh,V(-.35f,-.4f),.43f); Coin(mesh,V(.39f,-.19f),.43f); Coin(mesh,V(-.18f,.38f),.43f); break;
                case BattleCommandIconKind.Food:
                    Polygon(mesh,Gold,V(-.77f,-.5f),V(-.75f,.02f),V(-.46f,.52f),V(.05f,.72f),V(.56f,.51f),V(.8f,.09f),V(.7f,-.48f));
                    Stroke(mesh,Ivory,.13f,V(-.39f,.18f),V(-.2f,.42f));
                    Stroke(mesh,Ivory,.13f,V(.09f,.15f),V(.26f,.38f)); break;
                case BattleCommandIconKind.DrawPile: Cards(mesh,false); break;
                case BattleCommandIconKind.DiscardPile: Cards(mesh,true); break;
                case BattleCommandIconKind.Load:
                    Circle(mesh,V(-.43f,-.5f),.32f,Steel); Circle(mesh,V(.27f,-.5f),.32f,Steel);
                    Circle(mesh,V(-.08f,.04f),.32f,Steel);
                    Arrow(mesh,V(.55f,.09f),V(.55f,.75f),Gold,.17f); break;
                case BattleCommandIconKind.Morale:
                    Stroke(mesh,Ivory,.14f,V(-.55f,-.85f),V(-.55f,.85f));
                    Polygon(mesh,Gold,V(-.47f,.77f),V(.76f,.66f),V(.43f,.21f),V(.77f,-.18f),V(-.47f,-.05f));
                    break;
                case BattleCommandIconKind.Supplies:
                    Polygon(mesh,Timber,V(-.74f,.57f),V(.75f,.57f),V(.75f,-.7f),V(-.74f,-.7f));
                    Stroke(mesh,Gold,.15f,V(-.76f,.59f),V(.76f,.59f));
                    Stroke(mesh,Gold,.15f,V(-.76f,-.7f),V(.76f,-.7f));
                    Stroke(mesh,Ivory,.13f,V(-.57f,.42f),V(.57f,-.54f));
                    Stroke(mesh,Ivory,.13f,V(-.57f,-.54f),V(.57f,.42f)); break;
            }
        }

        private void Sword(VertexHelper mesh,bool mirror,Color blade)
        {
            float x=mirror?-1f:1f;
            Polygon(mesh,blade,V(-.34f*x,-.23f),V(.48f*x,.72f),V(.78f*x,.86f),V(.7f*x,.5f),V(-.17f*x,-.4f));
            Stroke(mesh,Gold,.13f,V(-.66f*x,-.16f),V(-.13f*x,-.67f));
            Stroke(mesh,Timber,.15f,V(-.44f*x,-.44f),V(-.73f*x,-.77f));
        }
        private void Shield(VertexHelper mesh,Color tint) => Polygon(mesh,tint,
            V(-.69f,.65f),V(0f,.84f),V(.69f,.65f),V(.57f,-.3f),V(0f,-.86f),V(-.57f,-.3f));
        private void Skull(VertexHelper mesh)
        {
            Circle(mesh,V(0f,.17f),.65f,Violet);
            Polygon(mesh,Violet,V(-.43f,-.13f),V(.43f,-.13f),V(.31f,-.73f),V(-.31f,-.73f));
            Circle(mesh,V(-.25f,.14f),.17f,Ink,false); Circle(mesh,V(.25f,.14f),.17f,Ink,false);
            Polygon(mesh,Ink,V(0f,-.1f),V(-.12f,-.32f),V(.12f,-.32f));
            Line(mesh,V(-.12f,-.47f),V(-.12f,-.71f),.07f,Ink);
            Line(mesh,V(.12f,-.47f),V(.12f,-.71f),.07f,Ink);
        }
        private void Hourglass(VertexHelper mesh)
        {
            Polygon(mesh,Steel,V(-.48f,.61f),V(.48f,.61f),V(.1f,0f),V(.48f,-.61f),V(-.48f,-.61f),V(-.1f,0f));
            Polygon(mesh,Gold,V(-.29f,.4f),V(.29f,.4f),V(0f,.03f));
            Polygon(mesh,Gold,V(0f,-.2f),V(.3f,-.52f),V(-.3f,-.52f));
            Stroke(mesh,Gold,.16f,V(-.65f,.73f),V(.65f,.73f));
            Stroke(mesh,Gold,.16f,V(-.65f,-.73f),V(.65f,-.73f));
        }
        private void Flame(VertexHelper mesh)
        {
            Polygon(mesh,Rgb(255,131,45),V(-.7f,-.21f),V(-.55f,.41f),V(-.25f,.1f),V(0f,.9f),V(.35f,.41f),V(.38f,.05f),V(.65f,.36f),V(.7f,-.32f),V(.36f,-.73f),V(-.25f,-.79f));
            Polygon(mesh,Gold,V(-.3f,-.43f),V(0f,.2f),V(.3f,-.4f),V(.15f,-.65f),V(-.15f,-.65f));
        }
        private void Bolt(VertexHelper mesh,Color tint,float size) => Polygon(mesh,tint,
            V(.1f,.91f)*size,V(-.64f,-.05f)*size,V(-.08f,-.05f)*size,V(-.27f,-.92f)*size,V(.7f,.25f)*size,V(.12f,.25f)*size);
        private void Waves(VertexHelper mesh)
        {
            for(int row=0;row<3;row++)
            {
                float y=.5f-row*.49f;
                Stroke(mesh,row==1?Steel:Aqua,.14f,V(-.8f,y-.06f),V(-.5f,y+.1f),V(-.25f,y+.1f),V(.09f,y-.1f),V(.4f,y-.1f),V(.8f,y+.12f));
            }
        }
        private void Drop(VertexHelper mesh,Color tint)
        {
            Polygon(mesh,tint,V(0f,.88f),V(.58f,-.02f),V(.64f,-.38f),V(.39f,-.72f),V(0f,-.85f),V(-.39f,-.72f),V(-.64f,-.38f),V(-.58f,-.02f));
            Stroke(mesh,Ivory,.08f,V(-.3f,-.13f),V(-.35f,-.37f),V(-.2f,-.53f));
        }
        private void Barrel(VertexHelper mesh)
        {
            Polygon(mesh,Timber,V(-.48f,.76f),V(.48f,.76f),V(.67f,.25f),V(.67f,-.25f),V(.48f,-.76f),V(-.48f,-.76f),V(-.67f,-.25f),V(-.67f,.25f));
            Stroke(mesh,Steel,.14f,V(-.6f,.43f),V(.6f,.43f));
            Stroke(mesh,Steel,.14f,V(-.6f,-.43f),V(.6f,-.43f));
        }
        private void Plank(VertexHelper mesh,Vector2 at,float slope)
        {
            Polygon(mesh,Timber,at+V(-.8f,.2f-slope),at+V(.8f,.2f+slope),at+V(.8f,-.2f+slope),at+V(-.8f,-.2f-slope));
            Line(mesh,at+V(-.65f,-slope),at+V(.62f,slope),.065f,Gold);
            Circle(mesh,at+V(-.61f,-slope),.045f,Ink,false); Circle(mesh,at+V(.61f,slope),.045f,Ink,false);
        }
        private void Cross(VertexHelper mesh,Vector2 at,float size,Color tint)
        {
            Line(mesh,at+Vector2.left*size,at+Vector2.right*size,size*.6f,tint);
            Line(mesh,at+Vector2.down*size,at+Vector2.up*size,size*.6f,tint);
        }
        private void Coin(VertexHelper mesh,Vector2 at,float radius)
        {
            Circle(mesh,at,radius,Gold); Circle(mesh,at,radius*.62f,Rgb(211,139,38),false);
            Line(mesh,at+Vector2.down*radius*.4f,at+Vector2.up*radius*.4f,.1f,Ivory);
        }
        private void Cards(VertexHelper mesh,bool discard)
        {
            Polygon(mesh,discard?Steel:Aqua,V(-.75f,.74f),V(.35f,.74f),V(.35f,-.57f),V(-.75f,-.57f));
            Polygon(mesh,Ivory,V(-.42f,.53f),V(.68f,.53f),V(.68f,-.79f),V(-.42f,-.79f));
            Arrow(mesh,discard?V(.12f,.23f):V(.12f,-.54f),discard?V(.12f,-.54f):V(.12f,.23f),discard?Rose:Aqua,.19f);
        }
        private void Arrow(VertexHelper mesh,Vector2 from,Vector2 to,Color tint,float width)
        {
            Vector2 direction=(to-from).normalized, side=V(-direction.y,direction.x);
            Stroke(mesh,tint,width,from,to-direction*.22f);
            Polygon(mesh,tint,to+direction*.16f,to-direction*.3f+side*.31f,to-direction*.3f-side*.31f);
        }
        private void Ring(VertexHelper mesh,Vector2 at,float radius,Color tint)
        { Circle(mesh,at,radius,tint); Circle(mesh,at,radius*.58f,Ink,false); }
        private void Circle(VertexHelper mesh,Vector2 at,float radius,Color tint,bool outlined=true)
        {
            var points=new Vector2[16];
            for(int i=0;i<points.Length;i++) { float angle=i*Mathf.PI*2f/points.Length; points[i]=at+V(Mathf.Cos(angle),Mathf.Sin(angle))*radius; }
            if(outlined) Polygon(mesh,tint,points); else Fill(mesh,tint,points);
        }
        private void Stroke(VertexHelper mesh,Color tint,float width,params Vector2[] points)
        {
            for(int i=1;i<points.Length;i++) Line(mesh,points[i-1],points[i],width+.1f,Ink);
            for(int i=1;i<points.Length;i++) Line(mesh,points[i-1],points[i],width,tint);
        }
        private void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color tint)
        {
            Vector2 delta=b-a;
            if(delta.sqrMagnitude<.000001f) return;
            Vector2 side=V(-delta.y,delta.x).normalized*width*.5f;
            Fill(mesh,tint,a-side,a+side,b+side,b-side);
        }
        private void Polygon(VertexHelper mesh,Color tint,params Vector2[] points)
        {
            Fill(mesh,tint,points);
            for(int i=0;i<points.Length;i++) Line(mesh,points[i],points[(i+1)%points.Length],.08f,Ink);
        }
        private void Fill(VertexHelper mesh,Color tint,params Vector2[] points)
        {
            if(points.Length<3) return;
            int start=mesh.currentVertCount;
            for(int i=0;i<points.Length;i++) mesh.AddVert((Vector3)(center+points[i]*scale),tint*color,Vector2.zero);
            int[] remaining=new int[points.Length];
            float area=0f;
            for(int i=0;i<points.Length;i++) { remaining[i]=i; area+=Cross(points[i],points[(i+1)%points.Length]); }
            float sign=area>=0f?1f:-1f;
            int count=points.Length;
            while(count>2)
            {
                bool clipped=false;
                for(int i=0;i<count;i++)
                {
                    int previous=remaining[(i+count-1)%count],current=remaining[i],next=remaining[(i+1)%count];
                    Vector2 a=points[previous],b=points[current],c=points[next];
                    if(Cross(b-a,c-b)*sign<=.000001f) continue;
                    bool occupied=false;
                    for(int j=0;j<count;j++)
                    {
                        int other=remaining[j];
                        if(other==previous||other==current||other==next) continue;
                        Vector2 p=points[other];
                        if(Cross(b-a,p-a)*sign>=-.000001f&&Cross(c-b,p-b)*sign>=-.000001f&&Cross(a-c,p-c)*sign>=-.000001f)
                        { occupied=true; break; }
                    }
                    if(occupied) continue;
                    mesh.AddTriangle(start+previous,start+current,start+next);
                    for(int j=i;j<count-1;j++) remaining[j]=remaining[j+1];
                    count--; clipped=true; break;
                }
                if(!clipped) break;
            }
        }
        private static Vector2 V(float x,float y) => new Vector2(x,y);
        private static float Cross(Vector2 a,Vector2 b) => a.x*b.y-a.y*b.x;
        private static Color Rgb(byte red,byte green,byte blue) => new Color32(red,green,blue,255);
    }
}
