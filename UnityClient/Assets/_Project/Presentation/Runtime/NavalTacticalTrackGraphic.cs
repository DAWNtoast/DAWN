using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalTacticalTrackGraphic : MaskableGraphic
    {
        private readonly List<Vector2> playerPath = new List<Vector2>();
        private readonly List<Vector2> enemyPath = new List<Vector2>();
        private Vector2 player, enemy;

        public void SetTrack(IReadOnlyList<Vector2> playerPoints, IReadOnlyList<Vector2> enemyPoints,
            Vector2 playerPosition, Vector2 enemyPosition)
        {
            playerPath.Clear();
            enemyPath.Clear();
            if (playerPoints != null) for (int i = 0; i < playerPoints.Count; i++) playerPath.Add(playerPoints[i]);
            if (enemyPoints != null) for (int i = 0; i < enemyPoints.Count; i++) enemyPath.Add(enemyPoints[i]);
            player = playerPosition;
            enemy = enemyPosition;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Color ally = new Color(.28f, .91f, 1f, .8f);
            Color hostile = new Color(1f, .36f, .26f, .8f);
            DrawPath(helper, playerPath, ally);
            DrawPath(helper, enemyPath, hostile);
            DrawRing(helper, player, 52f, ally);
            DrawRing(helper, enemy, 52f, hostile);
        }

        private static void DrawPath(VertexHelper helper, List<Vector2> path, Color tint)
        {
            for (int index = 1; index < path.Count; index++)
            {
                Vector2 delta = path[index] - path[index - 1];
                float length = delta.magnitude;
                if (length < .25f) continue;
                Vector2 direction = delta / length;
                for (float cursor = 0f; cursor < length; cursor += 16f)
                    AddLine(helper, path[index - 1] + direction * cursor,
                        path[index - 1] + direction * Mathf.Min(length, cursor + 10f), 2.5f, tint);
            }
        }

        private static void DrawRing(VertexHelper helper, Vector2 center, float radius, Color tint)
        {
            const int segments = 48;
            for (int index = 0; index < segments; index++)
            {
                float first = index * Mathf.PI * 2f / segments;
                float last = (index + 1) * Mathf.PI * 2f / segments;
                AddLine(helper, center + new Vector2(Mathf.Cos(first), Mathf.Sin(first)) * radius,
                    center + new Vector2(Mathf.Cos(last), Mathf.Sin(last)) * radius, 2.5f, tint);
            }
        }

        internal static void AddLine(VertexHelper helper, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
            int first = helper.currentVertCount;
            helper.AddVert(from - normal, tint, Vector2.zero);
            helper.AddVert(from + normal, tint, Vector2.zero);
            helper.AddVert(to + normal, tint, Vector2.zero);
            helper.AddVert(to - normal, tint, Vector2.zero);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }
    }
}
