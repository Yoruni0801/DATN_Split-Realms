using UnityEngine;
using UnityEditor;

// Đặt script này trong thư mục có tên là "Editor"
public class AutoMaterialAssigner : EditorWindow
{
    private int targetLayer;
    private Material targetMaterial;

    // Tạo menu trên thanh công cụ của Unity
    [MenuItem("Tools/Tien/Auto Material Assigner")]
    public static void ShowWindow()
    {
        GetWindow<AutoMaterialAssigner>("Auto Material Assigner");
    }

    private void OnGUI()
    {
        GUILayout.Label("Đổi Material hàng loạt theo Layer", EditorStyles.boldLabel);

        // Ô chọn Layer
        targetLayer = EditorGUILayout.LayerField("Chọn Layer", targetLayer);
        
        // Ô chọn Material muốn đổi
        targetMaterial = (Material)EditorGUILayout.ObjectField("Material Mới", targetMaterial, typeof(Material), false);

        GUILayout.Space(10);

        // Nút bấm thực hiện
        if (GUILayout.Button("Áp dụng cho toàn bộ Scene", GUILayout.Height(30)))
        {
            if (targetMaterial == null)
            {
                Debug.LogWarning("⚠️ Bạn chưa chọn Material kìa!");
                return;
            }

            AssignMaterial();
        }
    }

    private void AssignMaterial()
    {
        int count = 0;
        // Tìm toàn bộ SpriteRenderer trong Scene (kể cả những object đang bị tắt)
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None); 

        foreach (var sr in renderers)
        {
            // Kiểm tra xem object có thuộc Layer được chọn không
            if (sr.gameObject.layer == targetLayer)
            {
                // Lưu lại lịch sử để có thể bấm Ctrl+Z (Undo) nếu đổi nhầm
                Undo.RecordObject(sr, "Đổi Material Hàng Loạt");
                
                // Gán Material mới
                sr.sharedMaterial = targetMaterial;
                count++;
                
                // Đánh dấu là object đã thay đổi để Unity lưu lại
                EditorUtility.SetDirty(sr);
            }
        }

        Debug.Log($"✅ Đã đổi thành công Material [{targetMaterial.name}] cho {count} object thuộc Layer [{LayerMask.LayerToName(targetLayer)}].");
    }
}
