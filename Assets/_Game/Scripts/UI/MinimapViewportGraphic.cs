using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class MinimapViewportGraphic : MaskableGraphic
{
    [SerializeField] private Color32 areaColor = new Color32(216, 234, 216, 37);
    [SerializeField] private Color32 outlineColor = new Color32(244, 238, 190, 240);
    [SerializeField] private Color32 frameColor = new Color32(142, 109, 57, 255);
    [SerializeField] private Color32 cornerColor = new Color32(225, 183, 104, 255);
    private readonly List<Vector2> points = new List<Vector2>(12);
    private bool drawFrame;

    internal void SetOutline(List<Vector2> outline, bool showFrame)
    {
        bool dirty = drawFrame != showFrame || points.Count != outline.Count;
        for (int i = 0; !dirty && i < points.Count; i++)
            dirty = (points[i] - outline[i]).sqrMagnitude > .00000001f;
        if (!dirty) return;
        drawFrame = showFrame;
        points.Clear();
        points.AddRange(outline);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        if (points.Count >= 3)
        {
            foreach (var point in points) mesh.AddVert(ToLocal(rect, point), areaColor, Vector2.zero);
            for (int i = 1; i < points.Count - 1; i++) mesh.AddTriangle(0, i, i + 1);
            for (int i = 0; i < points.Count; i++)
                Line(mesh, ToLocal(rect, points[i]), ToLocal(rect, points[(i + 1) % points.Count]), 1.5f, outlineColor);
        }
        if (drawFrame) Frame(mesh, rect);
    }

    private static Vector2 ToLocal(Rect rect, Vector2 point)
        => rect.min + Vector2.Scale(rect.size, point);

    private void Frame(VertexHelper mesh, Rect rect)
    {
        Vector2 a = rect.min + Vector2.one;
        Vector2 b = new Vector2(rect.xMax - 1f, rect.yMin + 1f);
        Vector2 c = rect.max - Vector2.one;
        Vector2 d = new Vector2(rect.xMin + 1f, rect.yMax - 1f);
        Line(mesh, a, b, 2f, frameColor);
        Line(mesh, b, c, 2f, frameColor);
        Line(mesh, c, d, 2f, frameColor);
        Line(mesh, d, a, 2f, frameColor);
        Corner(mesh, a, Vector2.right, Vector2.up);
        Corner(mesh, b, Vector2.left, Vector2.up);
        Corner(mesh, c, Vector2.left, Vector2.down);
        Corner(mesh, d, Vector2.right, Vector2.down);
    }

    private void Corner(VertexHelper mesh, Vector2 at, Vector2 across, Vector2 up)
    {
        Line(mesh, at, at + across * 13f, 2f, cornerColor);
        Line(mesh, at, at + up * 13f, 2f, cornerColor);
        Line(mesh, at + across * 7f, at + up * 7f, 1f, cornerColor);
    }

    private static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color32 color)
    {
        var direction = to - from;
        if (direction.sqrMagnitude < .000001f) return;
        Vector2 side = new Vector2(-direction.y, direction.x).normalized * (width * .5f);
        int index = mesh.currentVertCount;
        mesh.AddVert(from + side, color, Vector2.zero);
        mesh.AddVert(to + side, color, Vector2.zero);
        mesh.AddVert(to - side, color, Vector2.zero);
        mesh.AddVert(from - side, color, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
