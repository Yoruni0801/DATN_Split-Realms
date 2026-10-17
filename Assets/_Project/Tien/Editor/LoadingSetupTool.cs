using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Tien.Core;
using System.IO;

namespace Tien.EditorTools
{
    public class LoadingSetupTool
    {
        [MenuItem("Tools/Tien/Auto Setup Loading System")]
        public static void SetupLoadingSystem()
        {
            // 1. Create or Find Map Database
            string dataFolderPath = "Assets/_Project/Tien/Data";
            if (!AssetDatabase.IsValidFolder(dataFolderPath))
            {
                // Create folder if it doesn't exist
                string[] split = dataFolderPath.Split('/');
                string currentPath = split[0];
                for (int i = 1; i < split.Length; i++)
                {
                    if (!AssetDatabase.IsValidFolder(currentPath + "/" + split[i]))
                    {
                        AssetDatabase.CreateFolder(currentPath, split[i]);
                    }
                    currentPath += "/" + split[i];
                }
            }

            string dbPath = dataFolderPath + "/MapDatabase.asset";
            MapDatabase mapDatabase = AssetDatabase.LoadAssetAtPath<MapDatabase>(dbPath);
            if (mapDatabase == null)
            {
                mapDatabase = ScriptableObject.CreateInstance<MapDatabase>();
                AssetDatabase.CreateAsset(mapDatabase, dbPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created MapDatabase at " + dbPath);
            }

            // 2. Check if EventSystem exists, if not create one
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // 3. Create Canvas
            GameObject canvasObj = new GameObject("LoadingManager");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Ensure it renders on top

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
            CanvasGroup faderGroup = canvasObj.AddComponent<CanvasGroup>();
            faderGroup.alpha = 0f;
            faderGroup.blocksRaycasts = false;

            LoadingManager manager = canvasObj.AddComponent<LoadingManager>();

            // 4. Create Fader
            GameObject faderObj = new GameObject("Fader");
            faderObj.transform.SetParent(canvasObj.transform, false);
            Image faderImage = faderObj.AddComponent<Image>();
            faderImage.color = Color.black;
            faderImage.raycastTarget = true;
            RectTransform faderRect = faderObj.GetComponent<RectTransform>();
            faderRect.anchorMin = Vector2.zero;
            faderRect.anchorMax = Vector2.one;
            faderRect.sizeDelta = Vector2.zero;

            // 5. Create Loading Screen Panel
            GameObject loadingScreenObj = new GameObject("LoadingScreen");
            loadingScreenObj.transform.SetParent(canvasObj.transform, false);
            RectTransform loadingRect = loadingScreenObj.AddComponent<RectTransform>();
            loadingRect.anchorMin = Vector2.zero;
            loadingRect.anchorMax = Vector2.one;
            loadingRect.sizeDelta = Vector2.zero;

            // 6. Create Background Image
            GameObject bgObj = new GameObject("BackgroundImage");
            bgObj.transform.SetParent(loadingScreenObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark gray default
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // 7. Create Slider (using Unity's Default Controls)
            GameObject sliderObj = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObj.transform.SetParent(loadingScreenObj.transform, false);
            sliderObj.name = "Slider";
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.1f, 0.1f);
            sliderRect.anchorMax = new Vector2(0.9f, 0.15f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.interactable = false; // Player shouldn't interact with loading bar
            
            // Remove the handle
            Transform handleSlideArea = sliderObj.transform.Find("Handle Slide Area");
            if (handleSlideArea != null)
            {
                Object.DestroyImmediate(handleSlideArea.gameObject);
            }
            slider.targetGraphic = null;

            Image fillImage = sliderObj.transform.Find("Fill Area/Fill").GetComponent<Image>();

            // 8. Bind variables to LoadingManager using SerializedObject
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("mapDatabase").objectReferenceValue = mapDatabase;
            so.FindProperty("faderGroup").objectReferenceValue = faderGroup;
            so.FindProperty("loadingScreenPanel").objectReferenceValue = loadingScreenObj;
            so.FindProperty("loadingBackgroundImage").objectReferenceValue = bgImage;
            so.FindProperty("loadingBarSlider").objectReferenceValue = slider;
            so.FindProperty("loadingBarFill").objectReferenceValue = fillImage;
            so.ApplyModifiedProperties();

            // 9. Hide loading screen initially
            loadingScreenObj.SetActive(false);

            // Register created object for undo
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Loading System");
            Selection.activeGameObject = canvasObj;

            Debug.Log("Loading System auto-setup complete!");
        }
    }
}
