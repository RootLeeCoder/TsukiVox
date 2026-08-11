using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class QuestUiSurface : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float cornerRadius = 8f;
        [SerializeField, Range(2, 12)] private int cornerSegments = 5;

        private readonly List<Vector2> perimeter = new List<Vector2>(52);

        public void SetCornerRadius(float radius)
        {
            cornerRadius = Mathf.Max(0f, radius);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(cornerRadius, Mathf.Min(rect.width, rect.height) * 0.5f);

            perimeter.Clear();
            if (radius <= 0.01f)
            {
                perimeter.Add(new Vector2(rect.xMin, rect.yMin));
                perimeter.Add(new Vector2(rect.xMin, rect.yMax));
                perimeter.Add(new Vector2(rect.xMax, rect.yMax));
                perimeter.Add(new Vector2(rect.xMax, rect.yMin));
            }
            else
            {
                AddCorner(rect.xMin + radius, rect.yMin + radius, radius, 180f, 90f);
                AddCorner(rect.xMax - radius, rect.yMin + radius, radius, 270f, 90f);
                AddCorner(rect.xMax - radius, rect.yMax - radius, radius, 0f, 90f);
                AddCorner(rect.xMin + radius, rect.yMax - radius, radius, 90f, 90f);
            }

            var centerIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(rect.center, color, new Vector2(0.5f, 0.5f));
            for (var index = 0; index < perimeter.Count; index += 1)
            {
                var point = perimeter[index];
                var uv = new Vector2(
                    Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                    Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
                vertexHelper.AddVert(point, color, uv);
            }

            for (var index = 0; index < perimeter.Count; index += 1)
            {
                var next = (index + 1) % perimeter.Count;
                vertexHelper.AddTriangle(centerIndex, centerIndex + index + 1, centerIndex + next + 1);
            }
        }

        private void AddCorner(float centerX, float centerY, float radius, float startDegrees, float sweepDegrees)
        {
            for (var index = 0; index <= cornerSegments; index += 1)
            {
                var angle = (startDegrees + sweepDegrees * index / cornerSegments) * Mathf.Deg2Rad;
                perimeter.Add(new Vector2(centerX + Mathf.Cos(angle) * radius, centerY + Mathf.Sin(angle) * radius));
            }
        }
    }
}
