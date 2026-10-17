using UnityEngine;
using UnityEngine.UI;

internal sealed class MagmaVesselGraphic : MaskableGraphic
{
    private int vesselStyle;
    private int capacity = 1;
    private int modelAmount;
    private int targetAmount;
    private bool targetVessel;
    private bool solvedGlow;
    private float displayedAmount;

    public float DisplayedAmount => displayedAmount;

    public void SetVesselStyle(int style)
    {
        vesselStyle = Mathf.Clamp(style, 0, 3);
        SetVerticesDirty();
    }

    public void SetModelState(int newCapacity, int amount, bool isTarget, int target, bool snap)
    {
        capacity = Mathf.Max(1, newCapacity);
        modelAmount = Mathf.Clamp(amount, 0, capacity);
        targetVessel = isTarget;
        targetAmount = Mathf.Clamp(target, 0, capacity);
        if (snap)
        {
            displayedAmount = modelAmount;
        }

        SetVerticesDirty();
    }

    public void SetDisplayedAmount(float value)
    {
        displayedAmount = Mathf.Clamp(value, 0f, capacity);
        SetVerticesDirty();
    }

    public void SetSolvedGlow(bool active)
    {
        solvedGlow = active;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (displayedAmount > 0.01f || solvedGlow)
        {
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = rectTransform.rect;
        float time = Time.unscaledTime;

        Rect body = Rect.MinMaxRect(rect.xMin + 18f, rect.yMin + 24f, rect.xMax - 18f, rect.yMax - 32f);
        Rect rim = Rect.MinMaxRect(body.xMin - 10f, body.yMin - 11f, body.xMax + 10f, body.yMax + 11f);

        if (solvedGlow)
        {
            float pulse = 0.5f + Mathf.Sin(time * 4f) * 0.18f;
            FireUiMesh.AddRect(helper, Rect.MinMaxRect(rim.xMin - 13f, rim.yMin - 13f, rim.xMax + 13f, rim.yMax + 13f), new Color(0.35f, 1f, 0.82f, pulse * 0.22f));
        }

        FireUiMesh.AddRect(helper, rim, new Color(0.36f, 0.14f, 0.045f, 1f));
        FireUiMesh.AddRect(helper, body, new Color(0.025f, 0.009f, 0.006f, 0.96f));
        FireUiMesh.AddRect(helper, Rect.MinMaxRect(body.xMin + 9f, body.yMin + 9f, body.xMax - 9f, body.yMax - 9f), new Color(0.22f, 0.16f, 0.13f, 0.34f));

        float innerBottom = body.yMin + 12f;
        float innerTop = body.yMax - 12f;
        float fillTop = Mathf.Lerp(innerBottom, innerTop, displayedAmount / capacity);
        if (displayedAmount > 0.001f)
        {
            Rect lavaRect = Rect.MinMaxRect(body.xMin + 11f, innerBottom, body.xMax - 11f, fillTop);
            FireUiMesh.AddRect(helper, lavaRect, new Color(0.78f, 0.08f, 0.008f, 0.94f));
            FireUiMesh.AddRect(helper, Rect.MinMaxRect(lavaRect.xMin + 5f, lavaRect.yMin, lavaRect.xMax - 5f, lavaRect.yMax), new Color(1f, 0.18f, 0.012f, 0.88f));
            FireUiMesh.AddRect(helper, Rect.MinMaxRect(lavaRect.xMin, fillTop - 5f, lavaRect.xMax, fillTop + 3f), new Color(1f, 0.65f, 0.16f, 1f));

            for (int bubble = 0; bubble < 5; bubble++)
            {
                float span = Mathf.Max(6f, fillTop - innerBottom);
                float y = innerBottom + Mathf.Repeat(bubble * 47f + time * (12f + bubble * 3f), span);
                float x = Mathf.Lerp(body.xMin + 24f, body.xMax - 24f, Mathf.Repeat(bubble * 0.37f + vesselStyle * 0.19f, 1f));
                float radius = 2.5f + bubble % 3;
                FireUiMesh.AddCircle(helper, new Vector2(x, y), radius, 8, new Color(1f, 0.72f, 0.2f, 0.62f));
            }
        }

        for (int tick = 1; tick < capacity; tick++)
        {
            float y = Mathf.Lerp(innerBottom, innerTop, tick / (float)capacity);
            float width = tick % 2 == 0 ? 13f : 8f;
            FireUiMesh.AddLine(helper, new Vector2(body.xMax - 10f - width, y), new Vector2(body.xMax - 10f, y), 2f, new Color(0.85f, 0.63f, 0.42f, 0.64f));
        }

        if (targetVessel)
        {
            float targetY = Mathf.Lerp(innerBottom, innerTop, targetAmount / (float)capacity);
            Color cyan = new Color(0.56f, 0.91f, 0.84f, 1f);
            FireUiMesh.AddLine(helper, new Vector2(body.xMin - 16f, targetY), new Vector2(body.xMax + 18f, targetY), 4f, cyan);
            FireUiMesh.AddTriangle(
                helper,
                new Vector2(body.xMax + 19f, targetY),
                new Vector2(body.xMax + 32f, targetY + 8f),
                new Vector2(body.xMax + 32f, targetY - 8f),
                cyan);
        }

        FireUiMesh.AddRect(helper, Rect.MinMaxRect(rim.xMin - 4f, rim.yMax - 4f, rim.xMax + 4f, rim.yMax + 13f), new Color(0.46f, 0.20f, 0.055f, 1f));
        FireUiMesh.AddRect(helper, Rect.MinMaxRect(rim.xMin - 7f, rim.yMin - 12f, rim.xMax + 7f, rim.yMin + 3f), new Color(0.30f, 0.11f, 0.035f, 1f));

        Vector2 runeCenter = new Vector2(0f, rect.yMax - 15f);
        Color rune = targetVessel ? new Color(0.56f, 0.91f, 0.84f, 0.92f) : new Color(0.82f, 0.39f, 0.09f, 0.9f);
        FireUiMesh.AddCircle(helper, runeCenter, 14f, 16, new Color(rune.r, rune.g, rune.b, 0.16f));
        FireUiMesh.AddLine(helper, runeCenter + new Vector2(-7f, -7f), runeCenter + new Vector2(0f, 8f), 3f, rune);
        FireUiMesh.AddLine(helper, runeCenter + new Vector2(0f, 8f), runeCenter + new Vector2(7f, -7f), 3f, rune);
        if (vesselStyle == 1)
        {
            FireUiMesh.AddLine(helper, runeCenter + new Vector2(-6f, 0f), runeCenter + new Vector2(6f, 0f), 2f, rune);
        }
        else if (vesselStyle == 2)
        {
            FireUiMesh.AddCircle(helper, runeCenter + new Vector2(0f, -3f), 3f, 8, rune);
        }
    }
}
