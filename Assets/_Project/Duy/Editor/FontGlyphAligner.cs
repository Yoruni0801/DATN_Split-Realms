#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore;

namespace Duy.EditorTools
{
    public class FontGlyphAligner : EditorWindow
    {
        public TMP_FontAsset fontAsset;

        [MenuItem("Tools/Font Glyph Aligner (Can Bang Font)")]
        public static void ShowWindow()
        {
            GetWindow<FontGlyphAligner>("Font Glyph Aligner");
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("CÔNG CỤ TỰ ĐỘNG CĂN THẲNG HÀNG FONT CHỮ", EditorStyles.boldLabel);
            GUILayout.Label("Tự động san bằng đường chân chữ (Baseline) cho tất cả ký tự.", EditorStyles.miniLabel);
            GUILayout.Space(10);

            fontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("Font Asset Cần Sửa", fontAsset, typeof(TMP_FontAsset), false);

            GUILayout.Space(15);

            if (fontAsset != null)
            {
                if (GUILayout.Button("⚡ Tự Động San Phẳng Tất Cả Ký Tự (Align All Glyphs)", GUILayout.Height(35)))
                {
                    AlignAllGlyphs(fontAsset);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Hãy kéo file Magical Neverland SDF vào ô trên để bắt đầu!", MessageType.Info);
            }
        }

        private void AlignAllGlyphs(TMP_FontAsset font)
        {
            if (font == null) return;

            Undo.RecordObject(font, "Align Font Glyphs");

            int count = 0;
            if (font.characterTable != null)
            {
                foreach (var character in font.characterTable)
                {
                    if (character != null && character.glyph != null)
                    {
                        Glyph glyph = character.glyph;
                        GlyphMetrics metrics = glyph.metrics;

                        // Chuẩn hóa bearingY theo chiều cao nét chữ để đưa tất cả chân chữ về đường Baseline phẳng
                        float normalizedBearingY = metrics.height;

                        glyph.metrics = new GlyphMetrics(
                            metrics.width,
                            metrics.height,
                            metrics.horizontalBearingX,
                            normalizedBearingY,
                            metrics.horizontalAdvance
                        );
                        count++;
                    }
                }
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Thành công!", $"Đã tự động căn thẳng hàng chuẩn cho toàn bộ {count} ký tự trong Font asset '{font.name}'!", "OK");
        }
    }
}
#endif
