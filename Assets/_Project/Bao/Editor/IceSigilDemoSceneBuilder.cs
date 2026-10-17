using System.IO;
using Bao.IceSigil;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bao.EditorTools
{
    public static class IceSigilDemoSceneBuilder
    {
        private const string RootFolder = "Assets/_Project/Bao/Minigames/IceSigil";
        private const string ArtFolder = RootFolder + "/Art";
        private const string ScenePath = "Assets/_Project/Bao/Scene/MiniGameTachAnBang.unity";
        private const string FontPath = "Assets/_Project/Bao/Font/GrenzeGotisch-Regular-Vietnamese SDF.asset";
        private const string FontMaterialPath = RootFolder + "/Art/IceSigilText.mat";
        private const string IcePulseMaterialPath = RootFolder + "/Art/IceSigilPulse.mat";
        private const int TracePointCount = 72;

        private static readonly Color Cyan = new(0.38f, 0.95f, 1f, 1f);
        private static readonly Color PaleCyan = new(0.80f, 1f, 1f, 1f);
        private static readonly Color DeepTeal = new(0.015f, 0.16f, 0.21f, 0.88f);
        private static TMP_FontAsset demoFont;
        private static Material demoFontMaterial;
        private static Material icePulseMaterial;

        [MenuItem("Tools/Bao/Ice Sigil Demo/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            string backdropPath = ArtFolder + "/IceCaveBackdrop.png";
            string hudPath = ArtFolder + "/IceSigilHUD.png";
            string sigilsPath = ArtFolder + "/IceSigilTiles.png";
            string releasedSigilsPath = ArtFolder + "/IceSigilReleasedTiles.png";
            string toolsPath = ArtFolder + "/IceTools.png";

            ConfigureTexture(backdropPath, false);
            ConfigureTexture(hudPath, true);
            ConfigureTexture(sigilsPath, true);
            ConfigureTexture(releasedSigilsPath, true);
            ConfigureTexture(toolsPath, true);

            Texture2D backdrop = LoadTexture(backdropPath);
            Texture2D hud = LoadTexture(hudPath);
            Texture2D sigils = LoadTexture(sigilsPath);
            Texture2D releasedSigils = LoadTexture(releasedSigilsPath);
            Texture2D tools = LoadTexture(toolsPath);
            demoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            ConfigureFontMaterial();
            ConfigureIcePulseMaterial();
            if (backdrop == null || hud == null || sigils == null || releasedSigils == null || tools == null)
            {
                Debug.LogError("Ice Sigil Demo: one or more generated textures could not be loaded.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateCanvas();
            BuildBackdrop(canvas.transform, backdrop);
            BuildPlayArea(canvas.transform, sigils, releasedSigils);
            BuildMeters(canvas.transform);
            BuildProgress(canvas.transform);
            BuildTexts(canvas.transform);
            BuildToolTray(canvas.transform, tools);
            BuildResultPanel(canvas.transform);
            BuildCursor(canvas.transform, tools);

            canvas.gameObject.AddComponent<IceSigilMinigameController>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/_Project/Bao/Scene");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
            Debug.Log($"Ice Sigil Demo built successfully: {ScenePath}");
        }

        private static void ConfigureTexture(string path, bool hasAlpha)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = hasAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = hasAlpha;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static Texture2D LoadTexture(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ConfigureFontMaterial()
        {
            if (demoFont == null || demoFont.material == null)
            {
                demoFontMaterial = null;
                return;
            }

            demoFontMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontMaterialPath);
            if (demoFontMaterial == null)
            {
                demoFontMaterial = new Material(demoFont.material)
                {
                    name = "IceSigilText"
                };
                AssetDatabase.CreateAsset(demoFontMaterial, FontMaterialPath);
            }
            else
            {
                demoFontMaterial.CopyPropertiesFromMaterial(demoFont.material);
            }

            demoFontMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            demoFontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.005f, 0.14f, 0.19f, 1f));
            demoFontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.10f);
            demoFontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.005f, 0.08f, 0.11f, 0.62f));
            EditorUtility.SetDirty(demoFontMaterial);
        }

        private static void ConfigureIcePulseMaterial()
        {
            Shader shader = Shader.Find("Bao/IceSigilPulseUI");
            if (shader == null)
            {
                icePulseMaterial = null;
                return;
            }

            icePulseMaterial = AssetDatabase.LoadAssetAtPath<Material>(IcePulseMaterialPath);
            if (icePulseMaterial == null)
            {
                icePulseMaterial = new Material(shader)
                {
                    name = "IceSigilPulse"
                };
                AssetDatabase.CreateAsset(icePulseMaterial, IcePulseMaterialPath);
            }
            else
            {
                icePulseMaterial.shader = shader;
            }

            icePulseMaterial.SetColor("_Tint", Color.white);
            icePulseMaterial.SetColor("_GlowColor", new Color(0.36f, 1f, 1f, 1f));
            icePulseMaterial.SetFloat("_GlowStrength", 0.75f);
            icePulseMaterial.SetFloat("_Pulse", 0f);
            EditorUtility.SetDirty(icePulseMaterial);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.06f, 0.09f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new("IceSigilCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        private static void BuildBackdrop(Transform parent, Texture2D backdrop)
        {
            RawImage background = CreateRawImage("Backdrop", parent, backdrop, new Rect(0f, 0f, 1f, 1f));
            Stretch(background.rectTransform);
            background.color = new Color(0.64f, 0.82f, 0.86f, 1f);
            background.raycastTarget = false;

            Image dimmer = CreateImage("BackdropDimmer", parent, new Color(0.005f, 0.055f, 0.085f, 0.62f));
            Stretch(dimmer.rectTransform);
            dimmer.raycastTarget = false;

        }

        private static void BuildPlayArea(Transform parent, Texture2D sigils, Texture2D releasedSigils)
        {
            RectTransform playArea = CreateRect("PlayArea", parent);
            Stretch(playArea);

            RawImage aura = CreateRawImage("SigilAura", playArea, sigils, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
            SetRect(aura.rectTransform, new Vector2(0f, 18f), new Vector2(650f, 650f));
            aura.color = new Color(0.45f, 1f, 1f, 0.22f);
            aura.material = icePulseMaterial;
            aura.raycastTarget = false;

            RawImage shadow = CreateRawImage("SigilShadow", playArea, sigils, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
            SetRect(shadow.rectTransform, new Vector2(16f, 2f), new Vector2(594f, 594f));
            shadow.color = new Color(0f, 0.08f, 0.12f, 0.62f);
            shadow.raycastTarget = false;

            RawImage sigil = CreateRawImage("Sigil", playArea, sigils, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
            SetRect(sigil.rectTransform, new Vector2(0f, 18f), new Vector2(580f, 580f));
            sigil.material = icePulseMaterial;
            sigil.raycastTarget = false;

            RawImage releasedSigil = CreateRawImage("ReleasedSigil", playArea, releasedSigils, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
            SetRect(releasedSigil.rectTransform, new Vector2(0f, 18f), new Vector2(620f, 620f));
            releasedSigil.material = icePulseMaterial;
            releasedSigil.raycastTarget = false;
            releasedSigil.gameObject.SetActive(false);

            RectTransform traceGuide = CreateRect("TraceGuide", playArea);
            SetRect(traceGuide, new Vector2(0f, 18f), new Vector2(480f, 480f));
            RectTransform segments = CreateRect("Segments", traceGuide);
            RectTransform points = CreateRect("Points", traceGuide);
            Stretch(segments);
            Stretch(points);

            for (int i = 0; i < TracePointCount; i++)
            {
                Image segment = CreateImage($"Segment_{i:00}", segments, new Color(0.78f, 1f, 1f, 0.74f));
                segment.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                segment.raycastTarget = false;
            }

            for (int i = 0; i < TracePointCount; i++)
            {
                Image point = CreateImage($"Point_{i:00}", points, new Color(0.78f, 1f, 1f, 0.72f));
                point.rectTransform.sizeDelta = new Vector2(14f, 14f);
                point.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                point.raycastTarget = false;
            }

            RectTransform anchors = CreateRect("PryAnchors", playArea);
            Stretch(anchors);
            Vector2[] positions =
            {
                new(0f, 322f),
                new(316f, 18f),
                new(0f, -286f),
                new(-316f, 18f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                Image anchor = CreateImage($"Anchor_{i}", anchors, PaleCyan);
                SetRect(anchor.rectTransform, positions[i], new Vector2(52f, 52f));
                anchor.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                anchor.raycastTarget = false;
                Image core = CreateImage("Core", anchor.transform, new Color(0.01f, 0.23f, 0.31f, 0.88f));
                SetRect(core.rectTransform, Vector2.zero, new Vector2(28f, 28f));
                core.raycastTarget = false;
                anchor.gameObject.SetActive(false);
            }

            RectTransform shards = CreateRect("Shards", playArea);
            Stretch(shards);

            BuildCrackLines(playArea);
            BuildAmbientIce(playArea);
            BuildTracePathControls(playArea);

            Image reticle = CreateImage("PointerReticle", playArea, new Color(0.42f, 1f, 0.88f, 0.96f));
            SetRect(reticle.rectTransform, Vector2.zero, new Vector2(18f, 18f));
            reticle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            reticle.raycastTarget = false;
            Image reticleCore = CreateImage("Core", reticle.transform, Color.white);
            SetRect(reticleCore.rectTransform, Vector2.zero, new Vector2(8f, 8f));
            reticleCore.raycastTarget = false;
        }

        private static void BuildTracePathControls(Transform parent)
        {
            RectTransform root = CreateRect("TracePathControls", parent);
            SetRect(root, Vector2.zero, new Vector2(580f, 580f));

            for (int variant = 0; variant < 3; variant++)
            {
                RectTransform group = CreateRect($"Variant_{variant}", root);
                SetRect(group, Vector2.zero, new Vector2(580f, 580f));
                Vector2[] points = GetDefaultTracePath(variant);
                for (int i = 0; i < points.Length; i++)
                {
                    Image control = CreateImage($"P_{i:00}", group, variant == 2
                        ? new Color(1f, 0.82f, 0.24f, 0.95f)
                        : new Color(0.42f, 1f, 0.84f, 0.90f));
                    SetRect(control.rectTransform, points[i], new Vector2(22f, 22f));
                    control.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    control.raycastTarget = false;
                }
            }

            root.gameObject.SetActive(false);
        }

        private static Vector2[] GetDefaultTracePath(int sigilType)
        {
            return sigilType switch
            {
                1 => new[]
                {
                    new Vector2(0f, 168f),
                    new Vector2(44f, 112f),
                    new Vector2(20f, 58f),
                    new Vector2(18f, -150f),
                    new Vector2(-18f, -176f),
                    new Vector2(-58f, -142f),
                    new Vector2(-58f, -106f),
                    new Vector2(-12f, -106f),
                    new Vector2(-58f, -76f),
                    new Vector2(-58f, -38f),
                    new Vector2(-18f, -38f),
                    new Vector2(-18f, 58f),
                    new Vector2(-44f, 112f),
                    new Vector2(0f, 168f)
                },
                2 => new[]
                {
                    new Vector2(0f, 176f),
                    new Vector2(122f, -50f),
                    new Vector2(58f, -50f),
                    new Vector2(24f, 10f),
                    new Vector2(0f, -30f),
                    new Vector2(-40f, -74f),
                    new Vector2(-122f, -74f),
                    new Vector2(0f, 176f)
                },
                _ => new[]
                {
                    new Vector2(0f, 178f),
                    new Vector2(58f, 104f),
                    new Vector2(94f, 28f),
                    new Vector2(100f, -48f),
                    new Vector2(66f, -116f),
                    new Vector2(0f, -150f),
                    new Vector2(-66f, -116f),
                    new Vector2(-100f, -48f),
                    new Vector2(-94f, 28f),
                    new Vector2(-58f, 104f),
                    new Vector2(0f, 178f)
                }
            };
        }

        private static void BuildCrackLines(Transform parent)
        {
            RectTransform root = CreateRect("CrackLines", parent);
            SetRect(root, new Vector2(0f, 18f), new Vector2(580f, 580f));
            Vector2[] positions =
            {
                new(-96f, 146f),
                new(112f, 128f),
                new(160f, 8f),
                new(78f, -126f),
                new(-114f, -118f),
                new(-172f, -4f),
                new(-34f, 64f),
                new(32f, -48f)
            };
            float[] rotations = { -32f, 38f, -8f, 62f, -54f, 14f, 72f, -74f };
            Vector2[] sizes =
            {
                new(112f, 7f),
                new(126f, 7f),
                new(92f, 6f),
                new(118f, 7f),
                new(128f, 7f),
                new(96f, 6f),
                new(82f, 6f),
                new(86f, 6f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                Image line = CreateImage($"Crack_{i:00}", root, new Color(0.55f, 1f, 1f, 0.32f));
                SetRect(line.rectTransform, positions[i], sizes[i]);
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);
                line.raycastTarget = false;
                line.gameObject.SetActive(false);
            }
        }

        private static void BuildAmbientIce(Transform parent)
        {
            RectTransform root = CreateRect("AmbientIce", parent);
            Stretch(root);
            for (int i = 0; i < 26; i++)
            {
                Image particle = CreateImage($"IceMote_{i:00}", root, new Color(0.58f, 1f, 1f, 0.18f));
                float x = Mathf.Lerp(-760f, 760f, (i * 37 % 100) / 100f);
                float y = Mathf.Lerp(-260f, 320f, (i * 53 % 100) / 100f);
                float size = 5f + (i * 7 % 10);
                SetRect(particle.rectTransform, new Vector2(x, y), new Vector2(size, size));
                particle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                particle.raycastTarget = false;
            }
        }

        private static void BuildHud(Transform parent, Texture2D hud)
        {
            RawImage hudImage = CreateRawImage("GeneratedHUD", parent, hud, new Rect(0f, 0f, 1f, 1f));
            Stretch(hudImage.rectTransform);
            hudImage.raycastTarget = false;
        }

        private static void BuildMeters(Transform parent)
        {
            RectTransform meters = CreateRect("Meters", parent);
            Stretch(meters);

            BuildVerticalMeter(meters, "CrackMeter", new Vector2(-770f, 12f), "ĐỘ NỨT", false);
            BuildVerticalMeter(meters, "ForceMeter", new Vector2(770f, 12f), "LỰC CẠY", true);
        }

        private static void BuildVerticalMeter(Transform parent, string name, Vector2 position, string label, bool safeBand)
        {
            RectTransform root = CreateRect(name, parent);
            SetRect(root, position, new Vector2(54f, 326f));

            Image background = CreateImage("Background", root, new Color(0.005f, 0.08f, 0.12f, 0.72f));
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            Image fill = CreateImage("Fill", root, Cyan);
            Stretch(fill.rectTransform);
            fill.raycastTarget = false;

            if (safeBand)
            {
                Image band = CreateImage("SafeBand", root, new Color(0.42f, 1f, 0.70f, 0.46f));
                band.rectTransform.anchorMin = new Vector2(0f, 0.42f);
                band.rectTransform.anchorMax = new Vector2(1f, 0.68f);
                band.rectTransform.offsetMin = Vector2.zero;
                band.rectTransform.offsetMax = Vector2.zero;
                band.raycastTarget = false;
            }

            TextMeshProUGUI meterLabel = CreateText("Label", root, label, 20f, PaleCyan, FontStyles.Bold);
            SetRect(meterLabel.rectTransform, new Vector2(0f, 190f), new Vector2(180f, 34f));
            TextMeshProUGUI value = CreateText("Value", root, "0%", 21f, Color.white, FontStyles.Bold);
            SetRect(value.rectTransform, new Vector2(0f, -190f), new Vector2(130f, 34f));
        }

        private static void BuildProgress(Transform parent)
        {
            RectTransform progress = CreateRect("Progress", parent);
            SetRect(progress, new Vector2(0f, -330f), new Vector2(620f, 24f));
            Image background = CreateImage("Background", progress, new Color(0.005f, 0.12f, 0.17f, 0.78f));
            Stretch(background.rectTransform);
            Image fill = CreateImage("Fill", progress, new Color(0.42f, 1f, 0.90f, 0.92f));
            Stretch(fill.rectTransform);
            TextMeshProUGUI value = CreateText("Value", progress, "0%", 18f, Color.white, FontStyles.Bold);
            SetRect(value.rectTransform, new Vector2(0f, 27f), new Vector2(220f, 32f));
        }

        private static void BuildTexts(Transform parent)
        {
            RectTransform texts = CreateRect("Texts", parent);
            Stretch(texts);

            TextMeshProUGUI title = CreateText("Title", texts, "TÁCH ẤN BĂNG", 42f, Color.white, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0f, 468f), new Vector2(720f, 70f));
            title.characterSpacing = 4f;

            TextMeshProUGUI phase = CreateText("PhaseText", texts, "KHẮC ĐƯỜNG ẤN", 30f, PaleCyan, FontStyles.Bold);
            SetRect(phase.rectTransform, new Vector2(0f, 375f), new Vector2(720f, 50f));
            TextMeshProUGUI instruction = CreateText("InstructionText", texts, "Giữ chuột trái và đi theo đường sáng", 22f, Color.white, FontStyles.Normal);
            SetRect(instruction.rectTransform, new Vector2(0f, 335f), new Vector2(900f, 44f));
            TextMeshProUGUI status = CreateText("StatusText", texts, "Bắt đầu từ điểm sáng trên đỉnh cổ ấn", 20f, new Color(0.72f, 0.94f, 0.97f, 1f), FontStyles.Italic);
            SetRect(status.rectTransform, new Vector2(0f, -258f), new Vector2(900f, 42f));
        }

        private static void BuildToolTray(Transform parent, Texture2D tools)
        {
            RectTransform tray = CreateRect("ToolTray", parent);
            Stretch(tray);
            Vector2[] positions =
            {
                new(-122f, -438f),
                new(122f, -438f)
            };
            Rect[] uvs =
            {
                new(0f, 0.5f, 0.5f, 0.5f),
                new(0f, 0f, 0.5f, 0.5f)
            };
            string[] labels = { "1 · KHẮC", "2 · CẠY" };

            Image trayBacking = CreateImage("Backing", tray, new Color(0.005f, 0.09f, 0.13f, 0.58f));
            SetRect(trayBacking.rectTransform, new Vector2(0f, -438f), new Vector2(560f, 178f));
            trayBacking.raycastTarget = false;

            for (int i = 0; i < positions.Length; i++)
            {
                Image slot = CreateImage($"ToolSlot_{i}", tray, new Color(0.01f, 0.15f, 0.20f, 0.22f));
                SetRect(slot.rectTransform, positions[i], new Vector2(154f, 154f));
                RawImage icon = CreateRawImage("Icon", slot.transform, tools, uvs[i]);
                SetRect(icon.rectTransform, new Vector2(0f, 7f), new Vector2(118f, 118f));
                icon.raycastTarget = false;
                TextMeshProUGUI label = CreateText("Label", slot.transform, labels[i], 16f, Color.white, FontStyles.Bold);
                SetRect(label.rectTransform, new Vector2(0f, -73f), new Vector2(150f, 28f));
            }
        }

        private static void BuildResultPanel(Transform parent)
        {
            Image result = CreateImage("ResultPanel", parent, new Color(0.008f, 0.10f, 0.14f, 0.96f));
            SetRect(result.rectTransform, new Vector2(0f, -228f), new Vector2(650f, 118f));

            Image inner = CreateImage("InnerGlow", result.transform, new Color(0.20f, 0.82f, 0.90f, 0.13f));
            SetRect(inner.rectTransform, Vector2.zero, new Vector2(620f, 88f));
            TextMeshProUGUI title = CreateText("ResultTitle", result.transform, "ẤN BĂNG ĐÃ ĐƯỢC GIẢI PHÓNG", 24f, PaleCyan, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0f, 18f), new Vector2(610f, 42f));
            TextMeshProUGUI subtitle = CreateText("ResultSubtitle", result.transform, "Nhấn R để thử lại", 16f, Color.white, FontStyles.Normal);
            SetRect(subtitle.rectTransform, new Vector2(0f, -26f), new Vector2(560f, 38f));
            result.gameObject.SetActive(false);
        }

        private static void BuildCursor(Transform parent, Texture2D tools)
        {
            RectTransform playArea = parent.Find("PlayArea") as RectTransform;
            RawImage cursor = CreateRawImage("ToolCursor", playArea, tools, new Rect(0f, 0.5f, 0.5f, 0.5f));
            SetRect(cursor.rectTransform, Vector2.zero, new Vector2(104f, 104f));
            cursor.raycastTarget = false;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(string name, Transform parent, Texture texture, Rect uv)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            RawImage image = gameObject.GetComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize, Color color, FontStyles style)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = demoFont != null ? demoFont : TMP_Settings.defaultFontAsset;
            if (demoFontMaterial != null)
            {
                text.fontSharedMaterial = demoFontMaterial;
            }
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
