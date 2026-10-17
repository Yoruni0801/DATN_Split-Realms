using UnityEngine;
using UnityEngine.UI;

internal sealed class FireCaveBackdropGraphic : MaskableGraphic
{
    private void Update()
    {
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect area = rectTransform.rect;
        float time = Time.unscaledTime;

        for (int band = 0; band < 7; band++)
        {
            float y0 = Mathf.Lerp(area.yMin, area.yMax, band / 7f);
            float y1 = Mathf.Lerp(area.yMin, area.yMax, (band + 1f) / 7f);
            Color shade = Color.Lerp(
                new Color(0.13f, 0.035f, 0.012f, 0.56f),
                new Color(0.01f, 0.002f, 0.001f, 0.72f),
                band / 6f);
            FireUiMesh.AddRect(helper, Rect.MinMaxRect(area.xMin, y0, area.xMax, y1), shade);
        }

        for (int i = 0; i < 18; i++)
        {
            float x = Mathf.Lerp(area.xMin, area.xMax, i / 17f);
            float height = 70f + Mathf.Repeat(i * 83f, 180f);
            FireUiMesh.AddTriangle(
                helper,
                new Vector2(x - 85f, area.yMax),
                new Vector2(x + 80f, area.yMax),
                new Vector2(x + Mathf.Sin(i * 1.7f) * 34f, area.yMax - height),
                new Color(0.018f, 0.004f, 0.002f, 0.82f));
        }

        const int lavaSegments = 32;
        float lavaBase = area.yMin;
        for (int i = 0; i < lavaSegments; i++)
        {
            float x0 = Mathf.Lerp(area.xMin, area.xMax, i / (float)lavaSegments);
            float x1 = Mathf.Lerp(area.xMin, area.xMax, (i + 1f) / lavaSegments);
            float y0 = lavaBase + 72f + Mathf.Sin(i * 0.82f + time * 0.7f) * 18f;
            float y1 = lavaBase + 72f + Mathf.Sin((i + 1f) * 0.82f + time * 0.7f) * 18f;
            FireUiMesh.AddQuad(
                helper,
                new Vector2(x0, lavaBase),
                new Vector2(x0, y0),
                new Vector2(x1, y1),
                new Vector2(x1, lavaBase),
                new Color(1f, 0.16f, 0.01f, 0.46f));
            FireUiMesh.AddLine(
                helper,
                new Vector2(x0, y0),
                new Vector2(x1, y1),
                4f,
                new Color(1f, 0.52f, 0.12f, 0.66f));
        }

        for (int i = 0; i < 42; i++)
        {
            float seedX = Mathf.Repeat(i * 0.6180339f, 1f);
            float speed = 18f + (i % 7) * 7f;
            float y = Mathf.Repeat(i * 73f + time * speed, area.height + 120f) - 60f + area.yMin;
            float x = Mathf.Lerp(area.xMin, area.xMax, seedX) + Mathf.Sin(time * 0.7f + i) * 16f;
            float radius = 1.8f + i % 4;
            float alpha = 0.2f + (i % 5) * 0.08f;
            FireUiMesh.AddCircle(helper, new Vector2(x, y), radius, 6, new Color(1f, 0.32f, 0.05f, alpha));
        }
    }
}
