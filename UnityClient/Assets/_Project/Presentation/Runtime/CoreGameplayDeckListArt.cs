using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private Button CreateCompactDeckListCard(Transform parent, int cardIndex, int quantity,
            string objectName, Vector2 size, Vector2 position, bool highlighted = false)
        {
            CoreBattleCard data = session.GetBattleCard(cardIndex);
            RectTransform root = CreateRect(objectName, parent, size, position);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            Color ink = new Color(.065f,.045f,.028f,1f);
            CreateImage(objectName + " Backing", root, new Vector2(size.x - 75f, size.y - 12f),
                new Vector2(12f, 0f), ink).raycastTarget = false;

            Vector2 pictureSize = new Vector2(101f, size.y - 12f);
            Texture2D texture = CardSchoolArtCatalog.Illustration(data, art);
            Rect uv = CardSchoolArtCatalog.IllustrationUv(data, art);
            if (texture != null)
            {
                float imageAspect = texture.width * uv.width / (texture.height * uv.height);
                float targetAspect = pictureSize.x / pictureSize.y;
                if (imageAspect > targetAspect)
                {
                    float width = uv.width * targetAspect / imageAspect;
                    uv.x += (uv.width - width) * .5f; uv.width = width;
                }
                else
                {
                    float height = uv.height * imageAspect / targetAspect;
                    uv.y += (uv.height - height) * .5f; uv.height = height;
                }
            }
            CreateRawImage(objectName + " Art", root, pictureSize,
                new Vector2(size.x * .5f - 42f - pictureSize.x * .5f, 0f), texture, uv).raycastTarget = false;
            RectTransform shade = CreateRect(objectName + " Name Shade", root, new Vector2(55f,size.y-12f),
                new Vector2(32f,0f));
            DeckListNameShadeGraphic gradient = shade.gameObject.AddComponent<DeckListNameShadeGraphic>();
            gradient.color = ink; gradient.raycastTarget = false;
            RectTransform rim = CreateRect(objectName + " Frame", root, size, Vector2.zero);
            DeckListRowGraphic frame = rim.gameObject.AddComponent<DeckListRowGraphic>();
            frame.Configure(data.SchoolId);
            if (highlighted)
            {
                Outline glow = rim.gameObject.AddComponent<Outline>();
                glow.effectColor = new Color(1f,.7f,.18f,.9f);
                glow.effectDistance = new Vector2(1f,-1f);
            }

            DeckListArtCatalog.Layout layout = DeckListArtCatalog.Bounds(CardSchoolArtCatalog.NormalizeSchool(data.SchoolId));
            Vector2 energyPosition = layout == null ? new Vector2(26f-size.x*.5f,0f) :
                new Vector2(DeckListArtCatalog.MapRowX(layout.energyX,layout,size.x)-size.x*.5f,
                    (layout.energyY-.5f)*size.y);
            Text energy = CreateText(objectName + " Energy",root,new Vector2(38f,44f),energyPosition,
                28,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white);
            energy.text = data.Energy.ToString();
            Text title = CreateText(objectName + " Name",root,new Vector2(129f,40f),
                new Vector2(54f + 64.5f-size.x*.5f,0f),22,FontStyle.Bold,TextAnchor.MiddleLeft,Html("#F9EFD1"));
            title.text = CardFaceLayout.DisplayName(data);
            title.resizeTextForBestFit = false;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            while (title.fontSize > 12 && title.preferredWidth > title.rectTransform.rect.width)
                title.fontSize--;
            if (title.preferredWidth > title.rectTransform.rect.width)
            {
                string fullName = title.text;
                int length = fullName.Length;
                do { title.text = fullName.Substring(0, --length) + "…"; }
                while (length > 1 && title.preferredWidth > title.rectTransform.rect.width);
            }
            Text copies = CreateText(objectName + " Quantity",root,new Vector2(37f,40f),
                new Vector2(size.x*.5f-21f,0f),26,FontStyle.Bold,TextAnchor.MiddleCenter,Html("#F0CE73"));
            copies.text = quantity.ToString();
            copies.resizeTextForBestFit = false;
            copies.horizontalOverflow = HorizontalWrapMode.Overflow;
            copies.verticalOverflow = VerticalWrapMode.Truncate;
            while (copies.fontSize > 12 && copies.preferredWidth > copies.rectTransform.rect.width)
                copies.fontSize--;
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = graphic == hit;
            return button;
        }

        private RectTransform CreateDeckListFrame(Transform parent, string name, Vector2 size, Vector2 position, string title)
        {
            RectTransform root = CreateRect(name,parent,size,position);
            RectTransform rim = CreateRect(name + " Frame",root,size,Vector2.zero);
            rim.gameObject.AddComponent<DeckListRowGraphic>().Configure("outer");
            Text count = CreateText(name + " Count",root,new Vector2(size.x-38f,42f),
                new Vector2(-5f,size.y*.5f-29f),26,FontStyle.Bold,TextAnchor.MiddleCenter,Html("#F0CE73"));
            count.text = title; count.raycastTarget = false;
            return root;
        }

        private Image CreateDeckListScrollThumb(Transform parent, string name, Vector2 size, Vector2 position)
        {
            Image thumb = CreateImage(name,parent,size,position,Color.white,DeckListArtCatalog.Thumb);
            thumb.preserveAspect = false;
            thumb.raycastTarget = false;
            return thumb;
        }
    }
}
