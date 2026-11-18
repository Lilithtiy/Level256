using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class WireCanvas : VisualElement
{
    public readonly List<(VisualElement a, VisualElement b, Color color)> Wires = new();

    public WireCanvas()
    {
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = 0;
        style.right = 0;
        style.top = 0;
        style.bottom = 0;
        generateVisualContent += Draw;
    }

    private void Draw(MeshGenerationContext ctx)
    {
        var p = ctx.painter2D;
        p.lineWidth = 8f;
        p.lineCap = LineCap.Round;

        foreach (var w in Wires)
        {
            Vector2 a = Center(w.a);
            Vector2 b = Center(w.b);
            if (float.IsNaN(a.x) || float.IsNaN(b.x)) continue;

            float dx = (b.x - a.x) * 0.5f;

            p.strokeColor = w.color;
            p.BeginPath();
            p.MoveTo(a);
            p.BezierCurveTo(new Vector2(a.x + dx, a.y), new Vector2(b.x - dx, b.y), b);
            p.Stroke();
        }
    }

    private Vector2 Center(VisualElement e) => this.WorldToLocal(e.worldBound.center);
}