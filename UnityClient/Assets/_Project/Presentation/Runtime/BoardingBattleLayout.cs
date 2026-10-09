using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Deck actors share a floor line above the retained hand and defense HUD.</summary>
    public static class BoardingBattleLayout
    {
        public static Vector2 PartySize => new Vector2(600f, 330f);
        public static Vector2 PartyPosition(bool enemy) => new Vector2(enemy ? 480f : -480f, 115f);
        public static Vector2 HealthPosition(bool enemy) => new Vector2(enemy ? 480f : -480f, -100f);

        public static Rect CoverUv(Texture texture, Vector2 target)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0 || target.x <= 0f || target.y <= 0f)
                return new Rect(0f, 0f, 1f, 1f);
            float sourceAspect = (float)texture.width / texture.height;
            float targetAspect = target.x / target.y;
            float width = Mathf.Min(1f, targetAspect / sourceAspect);
            float height = Mathf.Min(1f, sourceAspect / targetAspect);
            return new Rect((1f - width) * .5f, (1f - height) * .5f, width, height);
        }
    }
}
