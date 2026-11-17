using UnityEngine;
using UnityEngine.UIElements;

public class SymbolIcon : VisualElement
{
    public const int ShapeCount = 5;

    private readonly int shape;
    private readonly float size;

    public SymbolIcon(int shape, float size)
    {
        this.shape = Mathf.Abs(shape) % ShapeCount;
        this.size = size;

        pickingMode = PickingMode.Ignore;
        style.width = size;
        style.height = size;
        generateVisualContent += Draw;
    }

    public static SymbolIcon Overlay(VisualElement parent, int shape, float size)
    {
        var icon = new SymbolIcon(shape, size);
        icon.style.position = Position.Absolute;
        icon.style.left = Length.Percent(50);
        icon.style.top = Length.Percent(50);
        icon.style.marginLeft = -size / 2f;
        icon.style.marginTop = -size / 2f;
        parent.Add(icon);
        return icon;
    }

    private void Draw(MeshGenerationContext ctx)
    {
        var p = ctx.painter2D;
        float s = size;
        float c = s * 0.5f;

        p.fillColor = Color.white;
        p.strokeColor = new Color(0f, 0f, 0f, 0.85f);
        p.lineWidth = Mathf.Max(2f, s * 0.1f);
        p.lineJoin = LineJoin.Round;
        p.BeginPath();

        switch (shape)
        {
            case 0: // circle
                p.Arc(new Vector2(c, c), s * 0.42f, 0f, 360f);
                break;

            case 1: // triangle
                p.MoveTo(new Vector2(c, s * 0.08f));
                p.LineTo(new Vector2(s * 0.93f, s * 0.88f));
                p.LineTo(new Vector2(s * 0.07f, s * 0.88f));
                break;

            case 2: // square
                p.MoveTo(new Vector2(s * 0.14f, s * 0.14f));
                p.LineTo(new Vector2(s * 0.86f, s * 0.14f));
                p.LineTo(new Vector2(s * 0.86f, s * 0.86f));
                p.LineTo(new Vector2(s * 0.14f, s * 0.86f));
                break;

            case 3: // diamond
                p.MoveTo(new Vector2(c, s * 0.04f));
                p.LineTo(new Vector2(s * 0.96f, c));
                p.LineTo(new Vector2(c, s * 0.96f));
                p.LineTo(new Vector2(s * 0.04f, c));
                break;

            default: // star
                for (int i = 0; i < 10; i++)
                {
                    float r = (i % 2 == 0 ? 0.48f : 0.2f) * s;
                    float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                    var pt = new Vector2(c + Mathf.Cos(a) * r, c + Mathf.Sin(a) * r);
                    if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                }
                break;
        }

        p.ClosePath();
        p.Fill();
        p.Stroke();
    }
}