#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Tien.AdvancedLoading.Core;
using Tien.AdvancedLoading.UI;
using Tien.AdvancedLoading.Data;

namespace Tien.AdvancedLoading.Editor
{
    public class AdvancedLoadingSetupTool
    {
        [MenuItem("Tools/Tien/Loading System/Auto Setup AAA")]
        public static void Setup()
        {
            string rootPath = "Assets/_Project/Tien/code/AdvancedLoading";
            
            // 1. Tạo Data Folder và Database
            string dataPath = rootPath + "/DataAssets";
            if (!System.IO.Directory.Exists(dataPath))
            {
                System.IO.Directory.CreateDirectory(dataPath);
            }
            
            string dbPath = dataPath + "/LoadingDatabase.asset";
            LoadingDatabase db = AssetDatabase.LoadAssetAtPath<LoadingDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LoadingDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }

            // 2. Tạo Scene Mới
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Tạo Root Canvas
            GameObject canvasObj = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 4. Tạo Tấm Fader Đen (Chuyển cảnh Fade In / Fade Out mượt mà)
            GameObject faderObj = new GameObject("Fader", typeof(Image));
            faderObj.transform.SetParent(canvasObj.transform, false);
            Stretch(faderObj);
            Image faderImg = faderObj.GetComponent<Image>();
            faderImg.color = new Color(0, 0, 0, 0);
            faderImg.raycastTarget = false;

            // 5. Gắn Component
            FadeController fadeCtrl = canvasObj.AddComponent<FadeController>();
            SerializedObject soFade = new SerializedObject(fadeCtrl);
            soFade.FindProperty("faderImage").objectReferenceValue = faderImg;
            soFade.ApplyModifiedProperties();

            LoadingController controller = canvasObj.AddComponent<LoadingController>();
            SerializedObject soCtrl = new SerializedObject(controller);
            soCtrl.FindProperty("database").objectReferenceValue = db;
            soCtrl.FindProperty("fadeController").objectReferenceValue = fadeCtrl;
            soCtrl.FindProperty("fadeDuration").floatValue = 0.35f;
            soCtrl.FindProperty("minimumLoadingTime").floatValue = 0f;
            soCtrl.ApplyModifiedProperties();

            // 6. Lưu Scene
            string scenePath = rootPath + "/LoadingScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            // 7. Thêm vào Build Settings
            EditorBuildSettingsScene[] original = EditorBuildSettings.scenes;
            bool found = false;
            foreach (var s in original) if (s.path == scenePath) found = true;
            if (!found)
            {
                var newScenes = new EditorBuildSettingsScene[original.Length + 1];
                System.Array.Copy(original, newScenes, original.Length);
                newScenes[original.Length] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = newScenes;
            }

            Debug.Log("<color=green><b>[Pure Fade System]</b> Setup Complete! Đã xóa sạch UI/Hình nền loading, chỉ giữ lại hiệu ứng Fade In / Fade Out nhanh mượt.</color>");
        }

        private static void Stretch(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
#endif
