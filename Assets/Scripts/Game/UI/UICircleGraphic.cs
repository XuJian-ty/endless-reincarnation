using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [AddComponentMenu("UI/UICircleGraphic")]
    public sealed class UICircleGraphic : MaskableGraphic
    {
        [SerializeField, Range(6, 96)] private int _segments = 40;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            int segments = Mathf.Clamp(_segments, 6, 96);

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vh.AddVert(vertex);

            float angleStep = Mathf.PI * 2f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * radius + center.x;
                float y = Mathf.Sin(angle) * radius + center.y;
                vertex.position = new Vector2(x, y);
                vh.AddVert(vertex);
            }

            for (int i = 1; i <= segments; i++)
                vh.AddTriangle(0, i, i + 1);
        }
    }
}
