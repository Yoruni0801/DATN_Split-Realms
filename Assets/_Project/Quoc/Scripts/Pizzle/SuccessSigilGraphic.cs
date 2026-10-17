using UnityEngine;
using UnityEngine.UI;

internal sealed class SuccessSigilGraphic : MaskableGraphic
{
    private void Update()
    {
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        float time = Time.unscaledTime;
        float pulse = 0.78f + Mathf.Sin(time * 3.6f) * 0.16f;
        Color cyan = new Color(0.56f, 0.91f, 0.84f, pulse);
        Color ember = new Color(1f, 0.27f, 0.03f, 0.86f);
        Vector2 center = Vector2.zero;

        FireUiMesh.AddCircle(helper, center, 50f, 40, new Color(cyan.r, cyan.g, cyan.b, 0.12f));
        for (int i = 0; i < 8; i++)
        {
            float angle0 = i * Mathf.PI * 0.25f + time * 0.15f;
            float angle1 = angle0 + Mathf.PI * 0.19f;
            FireUiMesh.AddLine(
                helper,
                center + new Vector2(Mathf.Cos(angle0), Mathf.Sin(angle0)) * 38f,
                center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * 38f,
                4f,
                cyan);
        }

        FireUiMesh.AddTriangle(helper, new Vector2(0f, 34f), new Vector2(-22f, -25f), new Vector2(22f, -25f), ember);
        FireUiMesh.AddTriangle(helper, new Vector2(0f, 19f), new Vector2(-11f, -17f), new Vector2(11f, -17f), new Color(1f, 0.72f, 0.2f, 1f));
    }
}
