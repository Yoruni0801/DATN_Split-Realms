using UnityEngine;
using UnityEngine.UI;

internal sealed class RuneButtonGraphic : MaskableGraphic
{
    public enum RuneKind
    {
        Left,
        Valve,
        Right
    }

    private RuneKind rune;

    public void SetRune(RuneKind value)
    {
        rune = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = rectTransform.rect;
        Color tint = color;
        Color frame = Multiply(new Color(0.48f, 0.19f, 0.045f, 1f), tint);
        Color fill = Multiply(new Color(0.13f, 0.045f, 0.018f, 1f), tint);
        Color glow = Multiply(new Color(1f, 0.29f, 0.025f, 1f), tint);

        FireUiMesh.AddRect(helper, rect, frame);
        FireUiMesh.AddRect(helper, Rect.MinMaxRect(rect.xMin + 5f, rect.yMin + 5f, rect.xMax - 5f, rect.yMax - 5f), fill);
        FireUiMesh.AddRect(helper, Rect.MinMaxRect(rect.xMin + 6f, rect.yMin + 6f, rect.xMin + 56f, rect.yMax - 6f), new Color(glow.r, glow.g, glow.b, 0.92f));
        FireUiMesh.AddRect(helper, Rect.MinMaxRect(rect.xMin + 6f, rect.yMax - 7f, rect.xMax - 6f, rect.yMax - 4f), new Color(glow.r, glow.g, glow.b, 0.74f));

        Vector2 center = new Vector2(rect.xMin + 31f, 0f);
        Color icon = new Color(0.08f, 0.018f, 0.006f, 1f);
        if (rune == RuneKind.Valve)
        {
            FireUiMesh.AddCircle(helper, center, 13f, 16, icon);
            FireUiMesh.AddCircle(helper, center, 6f, 12, new Color(glow.r, glow.g, glow.b, 1f));
        }
        else
        {
            float direction = rune == RuneKind.Left ? -1f : 1f;
            FireUiMesh.AddLine(helper, center + new Vector2(-10f * direction, 0f), center + new Vector2(10f * direction, 0f), 5f, icon);
            FireUiMesh.AddTriangle(
                helper,
                center + new Vector2(13f * direction, 0f),
                center + new Vector2(2f * direction, 9f),
                center + new Vector2(2f * direction, -9f),
                icon);
        }
    }

    private static Color Multiply(Color value, Color tint)
    {
        return new Color(value.r * tint.r, value.g * tint.g, value.b * tint.b, value.a * tint.a);
    }
}
