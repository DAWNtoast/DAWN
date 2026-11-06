using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Measured boot contacts, tight occlusion and a soft cast onto the deck.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BoardingContactShadowGraphic : MaskableGraphic
    {
        [SerializeField] private bool formation;
        [SerializeField] private Vector3[] contacts;
        public int ContactCount => contacts == null ? 0 : contacts.Length;
        public Vector2[] ContactPoints
        {
            get
            {
                var points = new Vector2[ContactCount];
                Rect r = rectTransform.rect;
                for(int i=0;i<points.Length;i++)points[i]=new Vector2(r.xMin+contacts[i].x*r.width,r.yMin+contacts[i].y*r.height);
                return points;
            }
        }

        public void Configure(RawImage source, bool isFormation)
        {
            formation = isFormation;
            contacts = source == null ? null : BoardingFootShadowProfiles.For(source.texture, source.uvRect);
            if(contacts == null || contacts.Length == 0)
            {
                // Legacy/unknown art still gets two separate feet per person.
                // Derive the grounded sole row from the figure placement instead of treating
                // the transparent image border as the floor.
                float floor = source == null ? 0f : (-BoardingBattleLayout.PartySize.y*.5f-
                    source.rectTransform.anchoredPosition.y-source.rectTransform.rect.yMin)/source.rectTransform.rect.height;
                contacts = formation ? new[] { new Vector3(.12f,floor,.055f),new Vector3(.25f,floor,.055f),
                    new Vector3(.43f,floor,.055f),new Vector3(.56f,floor,.055f),new Vector3(.75f,floor,.055f),new Vector3(.88f,floor,.055f) }
                    : new[] { new Vector3(.38f,floor,.1f),new Vector3(.62f,floor,.1f) };
            }
            raycastTarget = false;
            color = new Color(.055f, .035f, .018f, 1f);
            SetVerticesDirty();
        }

        public void Configure(bool isFormation) => Configure(null,isFormation);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if(contacts==null)return;
            for(int i=0;i+1<contacts.Length;i+=2)
            {
                Vector3 a=contacts[i],b=contacts[i+1];
                Vector2 center=new Vector2(rect.xMin+(a.x+b.x)*.5f*rect.width,
                    rect.yMin+(a.y+b.y)*.5f*rect.height);
                float halfSpan=Mathf.Abs(a.x-b.x)*rect.width*.5f;
                AddContact(vh,center+new Vector2(10f,-3f),new Vector2(halfSpan+16f,12f),.19f,.08f);
            }
            // Draw broad penumbrae first, then the dark narrow sole contacts.
            // The light comes from upper-left, so cast shadows fall slightly right/down.
            for(int layer=0;layer<3;layer++)for (int i = 0; i < contacts.Length; i++)
            {
                Vector3 foot=contacts[i];
                Vector2 point=new Vector2(rect.xMin+foot.x*rect.width,rect.yMin+foot.y*rect.height);
                float width=Mathf.Clamp(foot.z*rect.width,10f,48f);
                if(layer==0)AddContact(vh,point+new Vector2(width*.55f,-5f),new Vector2(width*1.35f,12f),.32f,.10f);
                else if(layer==1)AddContact(vh,point+Vector2.down*.8f,new Vector2(width*.85f,6.5f),.46f,.26f);
                else AddContact(vh,point+Vector2.up*.25f,new Vector2(width*.55f,2.6f),.72f,.56f);
            }
        }

        private void AddContact(VertexHelper vh, Vector2 center, Vector2 radius, float opacity, float coreRadius)
        {
            const int segments = 32;
            int start = vh.currentVertCount;
            Color32 inner = new Color(color.r,color.g,color.b,color.a*opacity);
            Color32 outer = new Color(color.r, color.g, color.b, 0f);
            vh.AddVert(center, inner, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 point = new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                vh.AddVert(center + point * coreRadius, inner, Vector2.zero);
                vh.AddVert(center + point, outer, Vector2.zero);
                if (i == 0) continue;
                int current = start + 1 + i * 2;
                vh.AddTriangle(start, current - 2, current);
                vh.AddTriangle(current - 2, current - 1, current + 1);
                vh.AddTriangle(current - 2, current + 1, current);
            }
        }
    }
}
