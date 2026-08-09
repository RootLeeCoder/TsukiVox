using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    public enum QuestUiIconKind
    {
        Moon,
        Search,
        Plus,
        Queue,
        Settings,
        Replay,
        Previous,
        Play,
        Pause,
        Next,
        Microphone,
        Back,
        ChevronRight,
        Close,
        Copy,
        Volume,
        BrandRing,
        BrandSmile,
        Sparkles,
        Check,
        Trash,
        Stop,
        Power,
    }

    /// <summary>
    /// Small runtime vector icon set following Lucide's 24-unit line geometry.
    /// This avoids platform-dependent symbol glyphs on Quest.
    /// </summary>
    public sealed class QuestUiIcon : MaskableGraphic
    {
        private const float CoordinateSize = 24f;

        [SerializeField] private QuestUiIconKind iconKind;
        [SerializeField, Range(1f, 3.5f)] private float strokeWidth = 2.2f;

        private readonly List<Vector2> points = new List<Vector2>(40);

        public QuestUiIconKind IconKind => iconKind;

        public float StrokeWidth
        {
            get => strokeWidth;
            set
            {
                strokeWidth = Mathf.Clamp(value, 1f, 3.5f);
                SetVerticesDirty();
            }
        }

        public void SetIcon(QuestUiIconKind nextIcon)
        {
            if (iconKind == nextIcon)
            {
                return;
            }

            iconKind = nextIcon;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var scale = Mathf.Min(rect.width, rect.height) / CoordinateSize;
            var thickness = Mathf.Max(1.25f, strokeWidth * scale);

            switch (iconKind)
            {
                case QuestUiIconKind.Moon:
                    AddArc(vertexHelper, rect, 12f, 12f, 8.5f, 42f, 320f, thickness);
                    AddArc(vertexHelper, rect, 14.6f, 10.5f, 6.6f, 103f, 246f, thickness);
                    break;
                case QuestUiIconKind.Search:
                    AddCircle(vertexHelper, rect, 10.5f, 10.5f, 6.5f, thickness);
                    AddLine(vertexHelper, rect, 15.1f, 15.1f, 20f, 20f, thickness);
                    break;
                case QuestUiIconKind.Sparkles:
                    AddPolyline(vertexHelper, rect, thickness, 12f, 3f, 13.5f, 9.2f, 19f, 12f, 13.5f, 14.8f, 12f, 21f, 10.5f, 14.8f, 5f, 12f, 10.5f, 9.2f, 12f, 3f);
                    AddLine(vertexHelper, rect, 4f, 3f, 4f, 7f, thickness);
                    AddLine(vertexHelper, rect, 2f, 5f, 6f, 5f, thickness);
                    AddLine(vertexHelper, rect, 19f, 3f, 19f, 6f, thickness);
                    AddLine(vertexHelper, rect, 17.5f, 4.5f, 20.5f, 4.5f, thickness);
                    break;
                case QuestUiIconKind.Plus:
                    AddLine(vertexHelper, rect, 12f, 5f, 12f, 19f, thickness);
                    AddLine(vertexHelper, rect, 5f, 12f, 19f, 12f, thickness);
                    break;
                case QuestUiIconKind.Check:
                    AddPolyline(vertexHelper, rect, thickness, 4f, 12.5f, 9f, 17f, 20f, 6f);
                    break;
                case QuestUiIconKind.Trash:
                    AddLine(vertexHelper, rect, 3f, 6f, 21f, 6f, thickness);
                    AddLine(vertexHelper, rect, 9f, 3f, 15f, 3f, thickness);
                    AddRoundedRect(vertexHelper, rect, 6f, 6f, 12f, 16f, 2f, thickness);
                    AddLine(vertexHelper, rect, 10f, 10f, 10f, 18f, thickness);
                    AddLine(vertexHelper, rect, 14f, 10f, 14f, 18f, thickness);
                    break;
                case QuestUiIconKind.Stop:
                    AddRoundedRect(vertexHelper, rect, 5f, 5f, 14f, 14f, 2f, thickness);
                    break;
                case QuestUiIconKind.Power:
                    AddLine(vertexHelper, rect, 12f, 3f, 12f, 12f, thickness);
                    AddArc(vertexHelper, rect, 12f, 12f, 8.5f, -48f, 276f, thickness);
                    break;
                case QuestUiIconKind.Queue:
                    AddLine(vertexHelper, rect, 3f, 5f, 13f, 5f, thickness);
                    AddLine(vertexHelper, rect, 3f, 9f, 13f, 9f, thickness);
                    AddLine(vertexHelper, rect, 3f, 13f, 10f, 13f, thickness);
                    AddLine(vertexHelper, rect, 3f, 17f, 8f, 17f, thickness);
                    AddLine(vertexHelper, rect, 18f, 5f, 18f, 15.5f, thickness);
                    AddCircle(vertexHelper, rect, 15.7f, 17.2f, 2.25f, thickness);
                    break;
                case QuestUiIconKind.Settings:
                    AddCircle(vertexHelper, rect, 12f, 12f, 7f, thickness);
                    AddCircle(vertexHelper, rect, 12f, 12f, 2.5f, thickness);
                    for (var index = 0; index < 8; index += 1)
                    {
                        var angle = index * Mathf.PI * 0.25f;
                        AddLine(
                            vertexHelper,
                            rect,
                            12f + Mathf.Cos(angle) * 7.4f,
                            12f + Mathf.Sin(angle) * 7.4f,
                            12f + Mathf.Cos(angle) * 9.2f,
                            12f + Mathf.Sin(angle) * 9.2f,
                            thickness);
                    }
                    break;
                case QuestUiIconKind.Replay:
                    // Lucide RotateCcw, matching the WebXR control icon geometry.
                    AddArc(vertexHelper, rect, 12f, 12f, 9f, 180f, -270f, thickness);
                    AddPolyline(vertexHelper, rect, thickness, 12f, 3f, 9.2f, 3.5f, 6.8f, 4.7f, 5.25f, 5.75f, 3f, 8f);
                    AddPolyline(vertexHelper, rect, thickness, 3f, 3f, 3f, 8f, 8f, 8f);
                    break;
                case QuestUiIconKind.Previous:
                    AddLine(vertexHelper, rect, 5f, 5f, 5f, 19f, thickness);
                    AddPolyline(vertexHelper, rect, thickness, 19f, 4f, 9f, 12f, 19f, 20f, 19f, 4f);
                    break;
                case QuestUiIconKind.Play:
                    AddTriangle(vertexHelper, rect, 8f, 4.5f, 19f, 12f, 8f, 19.5f);
                    break;
                case QuestUiIconKind.Pause:
                    AddLine(vertexHelper, rect, 8.5f, 5f, 8.5f, 19f, thickness * 1.5f);
                    AddLine(vertexHelper, rect, 15.5f, 5f, 15.5f, 19f, thickness * 1.5f);
                    break;
                case QuestUiIconKind.Next:
                    AddPolyline(vertexHelper, rect, thickness, 5f, 4f, 15f, 12f, 5f, 20f, 5f, 4f);
                    AddLine(vertexHelper, rect, 19f, 5f, 19f, 19f, thickness);
                    break;
                case QuestUiIconKind.Microphone:
                    AddLine(vertexHelper, rect, 9f, 5f, 9f, 12f, thickness);
                    AddLine(vertexHelper, rect, 15f, 5f, 15f, 12f, thickness);
                    AddArc(vertexHelper, rect, 12f, 5f, 3f, 180f, 180f, thickness);
                    AddArc(vertexHelper, rect, 12f, 12f, 3f, 0f, 180f, thickness);
                    AddArc(vertexHelper, rect, 12f, 12f, 7f, 0f, 180f, thickness);
                    AddLine(vertexHelper, rect, 12f, 19f, 12f, 22f, thickness);
                    AddLine(vertexHelper, rect, 8f, 22f, 16f, 22f, thickness);
                    break;
                case QuestUiIconKind.Back:
                    AddPolyline(vertexHelper, rect, thickness, 15f, 5f, 8f, 12f, 15f, 19f);
                    break;
                case QuestUiIconKind.ChevronRight:
                    AddPolyline(vertexHelper, rect, thickness, 9f, 5f, 16f, 12f, 9f, 19f);
                    break;
                case QuestUiIconKind.Close:
                    AddLine(vertexHelper, rect, 5f, 5f, 19f, 19f, thickness);
                    AddLine(vertexHelper, rect, 19f, 5f, 5f, 19f, thickness);
                    break;
                case QuestUiIconKind.Copy:
                    AddRoundedRect(vertexHelper, rect, 8f, 8f, 13f, 13f, 2f, thickness);
                    AddPolyline(vertexHelper, rect, thickness, 16f, 6f, 16f, 4f, 14f, 2f, 4f, 2f, 2f, 4f, 2f, 14f, 4f, 16f, 6f, 16f);
                    break;
                case QuestUiIconKind.Volume:
                    AddPolyline(vertexHelper, rect, thickness, 3f, 9f, 7f, 9f, 12f, 5f, 12f, 19f, 7f, 15f, 3f, 15f, 3f, 9f);
                    AddArc(vertexHelper, rect, 12f, 12f, 5f, -55f, 110f, thickness);
                    AddArc(vertexHelper, rect, 12f, 12f, 9f, -45f, 90f, thickness);
                    break;
                case QuestUiIconKind.BrandRing:
                    AddCircle(vertexHelper, rect, 12f, 12f, 7.5f, thickness);
                    break;
                case QuestUiIconKind.BrandSmile:
                    AddPolyline(vertexHelper, rect, thickness, 7.5f, 15.4f, 9f, 16.3f, 10.5f, 16.8f, 12f, 17f, 13.8f, 16.9f, 15.5f, 16.5f, 16.9f, 15.8f);
                    break;
            }
        }

        private void AddPolyline(VertexHelper vertexHelper, Rect rect, float thickness, params float[] coordinates)
        {
            points.Clear();
            for (var index = 0; index + 1 < coordinates.Length; index += 2)
            {
                points.Add(ToLocal(rect, coordinates[index], coordinates[index + 1]));
            }

            for (var index = 1; index < points.Count; index += 1)
            {
                AddLine(vertexHelper, points[index - 1], points[index], thickness);
            }
        }

        private void AddRoundedRect(VertexHelper vertexHelper, Rect rect, float x, float y, float width, float height, float radius, float thickness)
        {
            points.Clear();
            AddArcPoints(x + radius, y + radius, radius, 180f, 90f, 4);
            AddArcPoints(x + width - radius, y + radius, radius, 270f, 90f, 4);
            AddArcPoints(x + width - radius, y + height - radius, radius, 0f, 90f, 4);
            AddArcPoints(x + radius, y + height - radius, radius, 90f, 90f, 4);
            points.Add(points[0]);
            AddPointLines(vertexHelper, rect, thickness);
        }

        private void AddCircle(VertexHelper vertexHelper, Rect rect, float centerX, float centerY, float radius, float thickness)
        {
            AddArc(vertexHelper, rect, centerX, centerY, radius, 0f, 360f, thickness);
        }

        private void AddArc(VertexHelper vertexHelper, Rect rect, float centerX, float centerY, float radius, float startDegrees, float sweepDegrees, float thickness)
        {
            points.Clear();
            var segments = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(sweepDegrees) / 15f));
            AddArcPoints(centerX, centerY, radius, startDegrees, sweepDegrees, segments);
            AddPointLines(vertexHelper, rect, thickness);
        }

        private void AddArcPoints(float centerX, float centerY, float radius, float startDegrees, float sweepDegrees, int segments)
        {
            for (var index = 0; index <= segments; index += 1)
            {
                var angle = (startDegrees + sweepDegrees * index / segments) * Mathf.Deg2Rad;
                points.Add(new Vector2(centerX + Mathf.Cos(angle) * radius, centerY + Mathf.Sin(angle) * radius));
            }
        }

        private void AddPointLines(VertexHelper vertexHelper, Rect rect, float thickness)
        {
            for (var index = 1; index < points.Count; index += 1)
            {
                AddLine(vertexHelper, ToLocal(rect, points[index - 1]), ToLocal(rect, points[index]), thickness);
            }
        }

        private void AddLine(VertexHelper vertexHelper, Rect rect, float x1, float y1, float x2, float y2, float thickness)
        {
            AddLine(vertexHelper, ToLocal(rect, x1, y1), ToLocal(rect, x2, y2), thickness);
        }

        private void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end, float thickness)
        {
            var direction = end - start;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            var normal = new Vector2(-direction.y, direction.x).normalized * thickness * 0.5f;
            AddQuad(vertexHelper, start - normal, start + normal, end + normal, end - normal);
        }

        private void AddTriangle(VertexHelper vertexHelper, Rect rect, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            var vertexIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(ToLocal(rect, x1, y1), color, Vector2.zero);
            vertexHelper.AddVert(ToLocal(rect, x2, y2), color, Vector2.zero);
            vertexHelper.AddVert(ToLocal(rect, x3, y3), color, Vector2.zero);
            vertexHelper.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
        }

        private void AddQuad(VertexHelper vertexHelper, Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight)
        {
            var vertexIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(bottomLeft, color, Vector2.zero);
            vertexHelper.AddVert(topLeft, color, Vector2.up);
            vertexHelper.AddVert(topRight, color, Vector2.one);
            vertexHelper.AddVert(bottomRight, color, Vector2.right);
            vertexHelper.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
            vertexHelper.AddTriangle(vertexIndex, vertexIndex + 2, vertexIndex + 3);
        }

        private static Vector2 ToLocal(Rect rect, float x, float y)
        {
            var scale = Mathf.Min(rect.width, rect.height) / CoordinateSize;
            return rect.center + new Vector2((x - CoordinateSize * 0.5f) * scale, (CoordinateSize * 0.5f - y) * scale);
        }

        private static Vector2 ToLocal(Rect rect, Vector2 point)
        {
            return ToLocal(rect, point.x, point.y);
        }
    }
}
