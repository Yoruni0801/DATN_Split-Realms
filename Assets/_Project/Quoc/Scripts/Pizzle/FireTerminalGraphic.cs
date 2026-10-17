using UnityEngine;
using UnityEngine.UI;

internal sealed class FireTerminalGraphic : MaskableGraphic
{
    [Header("Panel Background")]
    [SerializeField] private Color outerBorderColor = new Color(0.055f, 0.022f, 0.014f, 0.98f);
    [SerializeField] private Color innerBorderColor = new Color(0.37f, 0.14f, 0.035f, 0.94f);
    [SerializeField] private Color mainPanelColor = new Color(0.075f, 0.035f, 0.025f, 0.98f);

    [Header("Chamber Layout & Colors")]
    [SerializeField] private float chamberPaddingLeft = 85f;
    [SerializeField] private float chamberPaddingRight = 85f;
    [SerializeField] private float chamberPaddingBottom = 142f;
    [SerializeField] private float chamberPaddingTop = 62f;
    [SerializeField] private Color chamberColor = new Color(0.015f, 0.004f, 0.002f, 0.95f);
    [SerializeField] private Color chamberTopHighlight = new Color(0.65f, 0.28f, 0.06f, 0.9f);
    
    [Header("Lower Panel (Decor)")]
    [SerializeField] private Color lowerPanelColor = new Color(0.10f, 0.042f, 0.025f, 1f);

    [Header("Pipes & Slots")]
    [SerializeField] private Color slotPipeColor = new Color(0.38f, 0.16f, 0.05f, 0.9f);
    [SerializeField] private Color slotCircleColor = new Color(0.7f, 0.32f, 0.08f, 0.9f);
    [SerializeField] private Color pipeOuterColor = new Color(0.26f, 0.10f, 0.035f, 1f);
    [SerializeField] private Color pipeInnerColor = new Color(0.65f, 0.26f, 0.055f, 0.74f);

    [Header("Bolts")]
    [SerializeField] private Color boltColor = new Color(0.52f, 0.25f, 0.07f, 1f);
    [SerializeField] private Color boltSlotColor = new Color(0.08f, 0.025f, 0.01f, 1f);

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = rectTransform.rect;

        FireUiMesh.AddRect(helper, rect, outerBorderColor);
        FireUiMesh.AddRect(helper, Inset(rect, 9f), innerBorderColor);
        FireUiMesh.AddRect(helper, Inset(rect, 15f), mainPanelColor);

        Rect chamber = Rect.MinMaxRect(rect.xMin + chamberPaddingLeft, rect.yMin + chamberPaddingBottom, rect.xMax - chamberPaddingRight, rect.yMax - chamberPaddingTop);
        FireUiMesh.AddRect(helper, chamber, chamberColor);
        FireUiMesh.AddRect(helper, Rect.MinMaxRect(chamber.xMin, chamber.yMax - 5f, chamber.xMax, chamber.yMax), chamberTopHighlight);
        FireUiMesh.AddRect(helper, Rect.MinMaxRect(rect.xMin + 50f, rect.yMin + 30f, rect.xMax - 50f, rect.yMin + 126f), lowerPanelColor);

        float[] slots = { -305f, 0f, 305f };
        for (int i = 0; i < slots.Length; i++)
        {
            float x = slots[i];
            FireUiMesh.AddLine(helper, new Vector2(x, 186f), new Vector2(x, 232f), 10f, slotPipeColor);
            FireUiMesh.AddCircle(helper, new Vector2(x, 238f), 10f, 12, slotCircleColor);
        }

        Vector2[] bolts =
        {
            new Vector2(rect.xMin + 32f, rect.yMin + 32f),
            new Vector2(rect.xMax - 32f, rect.yMin + 32f),
            new Vector2(rect.xMin + 32f, rect.yMax - 32f),
            new Vector2(rect.xMax - 32f, rect.yMax - 32f)
        };
        foreach (Vector2 bolt in bolts)
        {
            FireUiMesh.AddCircle(helper, bolt, 11f, 12, boltColor);
            FireUiMesh.AddLine(helper, bolt + new Vector2(-5f, 0f), bolt + new Vector2(5f, 0f), 2f, boltSlotColor);
        }
    }

    private static Rect Inset(Rect rect, float amount)
    {
        return Rect.MinMaxRect(rect.xMin + amount, rect.yMin + amount, rect.xMax - amount, rect.yMax - amount);
    }
}
