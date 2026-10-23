// Export-only compatibility layer. Icon drawing sources are compiled unmodified.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class RequireComponent : Attribute { public RequireComponent(params Type[] types) {} }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute {}
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute {}
    public class CanvasRenderer {}
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x=x; this.y=y; }
        public static Vector2 zero => new Vector2(0,0);
        public static Vector2 one => new Vector2(1,1);
        public static Vector2 up => new Vector2(0,1);
        public static Vector2 down => new Vector2(0,-1);
        public static Vector2 left => new Vector2(-1,0);
        public static Vector2 right => new Vector2(1,0);
        public float sqrMagnitude => x*x+y*y;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector2 normalized => magnitude > 1e-5f ? this/magnitude : zero;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x+b.x,a.y+b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x-b.x,a.y-b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x,-a.y);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x*b,a.y*b);
        public static Vector2 operator *(float b, Vector2 a) => a*b;
        public static Vector2 operator /(Vector2 a, float b) => new Vector2(a.x/b,a.y/b);
        public static implicit operator Vector3(Vector2 a) => new Vector3(a.x,a.y,0);
    }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z=0) {this.x=x;this.y=y;this.z=z;} }
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float x,float y,float width,float height) {this.x=x;this.y=y;this.width=width;this.height=height;}
        public float xMin=>x; public float xMax=>x+width; public float yMin=>y; public float yMax=>y+height;
        public Vector2 center=>new Vector2(x+width*.5f,y+height*.5f);
        public static Rect MinMaxRect(float x1,float y1,float x2,float y2)=>new Rect(x1,y1,x2-x1,y2-y1);
    }
    public class RectTransform { public Rect rect = new Rect(-64,-64,128,128); }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1) {this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white => new Color(1,1,1,1);
        public static Color operator *(Color a,Color b)=>new Color(a.r*b.r,a.g*b.g,a.b*b.b,a.a*b.a);
        public static Color Lerp(Color a,Color b,float t) { t=Mathf.Clamp01(t); return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t); }
    }
    public struct Color32
    {
        public byte r,g,b,a;
        public Color32(byte r,byte g,byte b,byte a) {this.r=r;this.g=g;this.b=b;this.a=a;}
        public static implicit operator Color(Color32 c)=>new Color(c.r/255f,c.g/255f,c.b/255f,c.a/255f);
    }
    public static class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Sin(float a)=>(float)Math.Sin(a);
        public static float Cos(float a)=>(float)Math.Cos(a);
        public static float Pow(float a,float b)=>(float)Math.Pow(a,b);
        public static float Clamp01(float a)=>Math.Max(0,Math.Min(1,a));
        public static float Repeat(float a,float b)=>Math.Max(0,Math.Min(b,a-(float)Math.Floor(a/b)*b));
        public static float InverseLerp(float a,float b,float v)=>a!=b?Clamp01((v-a)/(b-a)):0;
    }
}
namespace UnityEngine.UI
{
    using UnityEngine;
    public class MaskableGraphic
    {
        public RectTransform rectTransform=new RectTransform();
        public Color color=Color.white;
        public bool raycastTarget;
        protected virtual void OnEnable() {}
        protected virtual void OnPopulateMesh(VertexHelper helper) {}
        protected void SetVerticesDirty() {}
        protected Rect GetPixelAdjustedRect()=>rectTransform.rect;
    }
    public struct ExportVertex { public Vector3 position; public Color color; }
    public class VertexHelper
    {
        public readonly List<ExportVertex> vertices=new List<ExportVertex>();
        public readonly List<int[]> triangles=new List<int[]>();
        public int currentVertCount=>vertices.Count;
        public void Clear() {vertices.Clear();triangles.Clear();}
        public void AddVert(Vector3 position, Color color, Vector2 uv) {vertices.Add(new ExportVertex{position=position,color=color});}
        public void AddTriangle(int a,int b,int c) {triangles.Add(new[]{a,b,c});}
    }
}
namespace ShatteredSail.Core.Gameplay
{
    public enum CoreBattleStatusKind { Guard,Armor,Attack,Fire,Retaliation,Healing,Curse,Weakness,Tide,Arcane,Thunder,Blood,Delayed,Anchor,Slow,Immunity,Seal,Draw }
    public enum CoreNavalFieldKind {Fire,Arcane,Curse,Tide,Thunder,Blood}
    public enum CoreNavalFieldChangeKind {Placed,Overwritten,Removed,Expired,Triggered}
    public struct VoyagePoint {public float X,Y;public VoyagePoint(float x,float y){X=x;Y=y;}}
    public class CoreVoyageSession {public const float NavalGridCellMeters=100f;}
    public class CoreNavalFieldPresentationEvent {public int X,Y; public CoreNavalFieldKind Kind;public CoreNavalFieldChangeKind Change;}
}
namespace ShatteredSail.Presentation
{
    using UnityEngine;
    using ShatteredSail.Core.Gameplay;
    public class NavalFieldMotionCue {public CoreNavalFieldPresentationEvent Event; public float Progress;}
    // WorldToBoard at a centered tile, zero camera rotation, the actual standard .75 px/m scale.
    public class NavalTacticalBoardView {public Vector2 WorldToBoard(VoyagePoint p)=>new Vector2(p.X,p.Y)*.75f;}
}
