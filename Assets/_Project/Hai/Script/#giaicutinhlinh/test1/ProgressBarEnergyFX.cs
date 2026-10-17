using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lớp ánh sáng procedural phủ trên phần Fill của Slider. Không cần texture
/// mới: chỉ dùng các dải cyan/gold di chuyển và các hạt năng lượng nhỏ.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class ProgressBarEnergyFX : MaskableGraphic
{
    private float pressure;
    private float movement;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        color = Color.white;
    }

    public void SetEnergyState(float newPressure, float newMovement)
    {
        pressure = Mathf.Clamp01(newPressure);
        movement = Mathf.Clamp01(newMovement);
        SetVerticesDirty();
    }

    private void Update()
    {
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 1f || rect.height <= 1f)
        {
            return;
        }

        float time = Time.unscaledTime;
        Color32 aura = pressure > 0f
            ? new Color32(255, 173, 43, 45)
            : new Color32(78, 239, 255, 38);
        Color32 rim = pressure > 0f
            ? new Color32(255, 229, 150, 165)
            : new Color32(168, 252, 255, 135);

        // Viền lõi trên giúp thanh trông như chứa chất lỏng/năng lượng.
        AddRect(vh, new Rect(rect.xMin + 5f, rect.yMax - rect.height * 0.28f, Mathf.Max(0f, rect.width - 10f), rect.height * 0.16f), rim);

        // Dải quét ánh sáng chạy xuyên phần đã nạp của thanh.
        float scanWidth = Mathf.Clamp(rect.width * 0.1f, 18f, 64f);
        float scanX = Mathf.Lerp(rect.xMin - scanWidth, rect.xMax, Mathf.Repeat(time * (0.45f + movement * 0.8f), 1f));
        AddRect(vh, new Rect(scanX, rect.yMin + 3f, scanWidth, rect.height - 6f), aura);
        AddRect(vh, new Rect(scanX + scanWidth * 0.42f, rect.yMin + 5f, 2.5f, rect.height - 10f), rim);

        // Ba hạt nhỏ tạo chiều sâu, không che thông tin HP.
        for (int i = 0; i < 3; i++)
        {
            float x = Mathf.Lerp(rect.xMin + 12f, rect.xMax - 12f, Mathf.Repeat(time * (0.16f + i * 0.035f) + i * 0.31f, 1f));
            float y = rect.center.y + Mathf.Sin(time * 3.2f + i * 2.1f) * rect.height * 0.12f;
            AddDiamond(vh, new Vector2(x, y), 2.2f + pressure * 1.2f, rim);
        }
    }

    private static void AddRect(VertexHelper vh, Rect rect, Color32 tint)
    {
        if (rect.width <= 0f || rect.height <= 0f || tint.a == 0)
        {
            return;
        }

        int index = vh.currentVertCount;
        vh.AddVert(new Vector2(rect.xMin, rect.yMin), tint, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMin, rect.yMax), tint, Vector2.up);
        vh.AddVert(new Vector2(rect.xMax, rect.yMax), tint, Vector2.one);
        vh.AddVert(new Vector2(rect.xMax, rect.yMin), tint, Vector2.right);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private static void AddDiamond(VertexHelper vh, Vector2 center, float radius, Color32 tint)
    {
        int index = vh.currentVertCount;
        vh.AddVert(center + Vector2.up * radius, tint, Vector2.zero);
        vh.AddVert(center + Vector2.right * radius, tint, Vector2.right);
        vh.AddVert(center + Vector2.down * radius, tint, Vector2.one);
        vh.AddVert(center + Vector2.left * radius, tint, Vector2.up);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}
