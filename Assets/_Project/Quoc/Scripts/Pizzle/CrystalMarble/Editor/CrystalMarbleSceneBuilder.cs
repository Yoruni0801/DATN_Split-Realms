using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Builds the Crystal Marble scene as authored scene content. Run from
/// Quoc/MiniGame/Build Crystal Marble (Fire Cave) or from batch mode.
/// </summary>
public static class CrystalMarbleSceneBuilder
{
    private const string ScenePath = "Assets/_Project/Quoc/Scene/MiniGame/CrystalMarble.unity";
    private const string GeneratedRoot = "Assets/_Project/Quoc/Scene/MiniGame/Generated";
    private const string SceneName = "CrystalMarble";

    private static readonly Color Charcoal = new Color(0.018f, 0.009f, 0.006f, 1f);
    private static readonly Color CharredRock = new Color(0.075f, 0.039f, 0.026f, 1f);
    private static readonly Color Basalt = new Color(0.13f, 0.07f, 0.045f, 1f);
    private static readonly Color Bronze = new Color(0.37f, 0.17f, 0.055f, 1f);
    private static readonly Color BronzeLight = new Color(0.85f, 0.32f, 0.07f, 1f);
    private static readonly Color Ember = new Color(1f, 0.17f, 0.025f, 1f);
    private static readonly Color Crystal = new Color(1f, 0.20f, 0.045f, 1f);
    private static readonly Color Rune = new Color(1f, 0.36f, 0.09f, 1f);
    private static readonly Color UIInk = new Color(0.055f, 0.018f, 0.009f, 0.96f);
    private static readonly Color UIPaper = new Color(0.92f, 0.54f, 0.24f, 1f);

    [MenuItem("Tools/Quoc/MiniGame/Build Crystal Marble (Fire Cave)")]
    public static void BuildFromMenu()
    {
        Build();
    }

    [MenuItem("Tools/Quoc/MiniGame/Build Crystal Marble and Render Preview")]
    public static void BuildAndRenderPreview()
    {
        Build();
        RenderPreview();
    }

    public static void Build()
    {
        EnsureDirectory(GeneratedRoot);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = SceneName;
        ConfigureRenderSettings();

        MaterialLibrary materials = CreateMaterials();
        MeshLibrary meshes = CreateMeshes();

        Transform root = new GameObject("CRYSTAL_MARBLE_MINIGAME").transform;
        root.position = Vector3.zero;

        Camera camera = CreateCamera(root);
        CreateVolume(root);
        CreateFireCaveEnvironment(root, materials);
        CreateLighting(root, materials);

        PuzzleReferences puzzle = CreatePuzzle(root, materials, meshes);
        UIReferences ui = CreateUserInterface(root);
        CreateEventSystem();
        WirePuzzleController(puzzle, ui, materials);

        AddToBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = root.gameObject;

        Debug.Log("Crystal Marble Fire Cave scene built and saved to " + ScenePath + ". Camera: " + camera.name);
    }

    public static void RenderPreview()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            Debug.LogWarning("Crystal Marble preview skipped: no camera found.");
            return;
        }

        RenderTexture renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32)
        {
            name = "CrystalMarblePreviewRT",
            antiAliasing = 2
        };
        Texture2D preview = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;
        preview.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        preview.Apply();
        string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "CrystalMarblePreview.png");
        EnsureDirectory("Temp");
        File.WriteAllBytes(outputPath, preview.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(renderTexture);
        UnityEngine.Object.DestroyImmediate(preview);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Crystal Marble preview rendered to " + outputPath);
    }

    private static void ConfigureRenderSettings()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.028f, 0.013f, 0.009f, 1f);
        RenderSettings.ambientIntensity = 0.65f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.035f, 0.011f, 0.006f, 1f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.013f;
    }

    private static Camera CreateCamera(Transform root)
    {
        GameObject go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        go.transform.SetParent(root, false);
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0.18f, -15.5f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        Camera camera = go.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.008f, 0.003f, 0.002f, 1f);
        camera.orthographic = false;
        camera.fieldOfView = 38f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.allowHDR = true;
        camera.allowMSAA = true;

        UniversalAdditionalCameraData additional = go.AddComponent<UniversalAdditionalCameraData>();
        additional.renderPostProcessing = true;
        additional.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        additional.SetRenderer(0);
        return camera;
    }

    private static void CreateVolume(Transform root)
    {
        GameObject go = new GameObject("Post FX - Ember Bloom");
        go.transform.SetParent(root, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;

        string path = GeneratedRoot + "/CM_FireCaveVolumeProfile.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        volume.sharedProfile = profile;

        Bloom bloom = GetOrAddOverride<Bloom>(profile);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 1.15f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.75f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.72f;

        Vignette vignette = GetOrAddOverride<Vignette>(profile);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.36f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.72f;
        vignette.rounded.overrideState = true;
        vignette.rounded.value = true;

        ColorAdjustments color = GetOrAddOverride<ColorAdjustments>(profile);
        color.postExposure.overrideState = true;
        color.postExposure.value = -0.25f;
        color.contrast.overrideState = true;
        color.contrast.value = 16f;
        color.colorFilter.overrideState = true;
        color.colorFilter.value = new Color(1f, 0.72f, 0.57f, 1f);
    }

    private static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component;
        if (!profile.TryGet(out component))
        {
            component = profile.Add<T>(true);
        }
        return component;
    }

    private static void CreateFireCaveEnvironment(Transform root, MaterialLibrary materials)
    {
        Transform environment = NewGroup(root, "Environment_FireCave");

        GameObject backdrop = CreatePrimitive("Cave_Backdrop", PrimitiveType.Quad, environment, new Vector3(0f, 0.8f, 5f), new Vector3(28f, 18f, 1f), Quaternion.Euler(0f, 180f, 0f), materials.charcoal, false);
        backdrop.isStatic = true;

        GameObject floor = CreatePrimitive("LavaFloor_Platform", PrimitiveType.Cube, environment, new Vector3(0f, -3.55f, 1.3f), new Vector3(24f, 0.35f, 10f), Quaternion.identity, materials.basalt, true);
        floor.isStatic = true;
        GameObject lavaGlow = CreatePrimitive("LavaGlow_Fissure", PrimitiveType.Cube, environment, new Vector3(0f, -3.32f, -0.15f), new Vector3(15f, 0.045f, 0.8f), Quaternion.Euler(0f, 0f, 1.2f), materials.lava, false);
        lavaGlow.isStatic = true;

        for (int i = 0; i < 22; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * (5.2f + (i % 4) * 0.75f);
            float y = -2.65f + (i % 5) * 1.08f;
            float z = 2.2f + (i % 3) * 0.8f;
            float scale = 0.85f + (i % 4) * 0.3f;
            GameObject rock = CreatePrimitive("Basalt_Chunk_" + i.ToString("00"), PrimitiveType.Sphere, environment, new Vector3(x, y, z), new Vector3(scale * 1.35f, scale * 0.9f, scale), Quaternion.Euler(i * 9f, i * 21f, i * 7f), i % 3 == 0 ? materials.charredRock : materials.basalt, true);
            rock.isStatic = true;
        }

        for (int i = 0; i < 9; i++)
        {
            float x = -5.4f + i * 1.35f;
            float height = 0.8f + (i % 4) * 0.38f;
            GameObject stalactite = CreatePrimitive("Stalactite_" + i.ToString("00"), PrimitiveType.Cylinder, environment, new Vector3(x, 4.75f - height * 0.5f, 3.15f + (i % 2) * 0.3f), new Vector3(0.55f + (i % 3) * 0.15f, height, 0.55f + (i % 3) * 0.15f), Quaternion.Euler(180f, 0f, 0f), materials.charredRock, true);
            stalactite.isStatic = true;
        }

        CreateFireBowl(environment, materials, new Vector3(-5.8f, -2.7f, 0.2f), 0);
        CreateFireBowl(environment, materials, new Vector3(5.8f, -2.7f, 1.1f), 1);

        ParticleSystem embers = CreateEmberParticles(environment, materials, new Vector3(0f, -0.9f, 1.4f), "Floating_Embers");
        ParticleSystem.MainModule main = embers.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.095f);
    }

    private static void CreateFireBowl(Transform parent, MaterialLibrary materials, Vector3 position, int index)
    {
        Transform bowl = NewGroup(parent, "FireBrazier_" + index);
        bowl.position = position;
        CreatePrimitive("Bowl", PrimitiveType.Cylinder, bowl, new Vector3(0f, -0.12f, 0f), new Vector3(0.72f, 0.16f, 0.72f), Quaternion.identity, materials.bronze, true);
        CreatePrimitive("BowlInner", PrimitiveType.Cylinder, bowl, new Vector3(0f, 0.02f, 0f), new Vector3(0.52f, 0.06f, 0.52f), Quaternion.identity, materials.lava, false);
        Light light = NewLight(bowl, "BrazierLight", LightType.Point, Ember, 3.8f, 5.4f, new Vector3(0f, 0.35f, -0.15f));
        light.shadows = LightShadows.Soft;
        CreateFireParticles(bowl, materials, new Vector3(0f, 0.3f, 0f), "Fire", 0.85f + index * 0.1f);
    }

    private static void CreateLighting(Transform root, MaterialLibrary materials)
    {
        Transform lights = NewGroup(root, "Lighting_Ember");
        Light key = NewLight(lights, "Key_Ember", LightType.Point, new Color(1f, 0.20f, 0.045f), 9f, 13f, new Vector3(0f, 2.4f, -2.4f));
        key.shadows = LightShadows.Soft;
        Light rim = NewLight(lights, "Rim_Orange", LightType.Point, new Color(1f, 0.055f, 0.01f), 11f, 16f, new Vector3(0f, -0.6f, 4.3f));
        rim.shadows = LightShadows.None;
        Light fill = NewLight(lights, "Fill_Bronze", LightType.Point, new Color(1f, 0.47f, 0.18f), 4f, 8f, new Vector3(-3.8f, 1.2f, -1.2f));
        fill.shadows = LightShadows.None;
    }

    private static PuzzleReferences CreatePuzzle(Transform root, MaterialLibrary materials, MeshLibrary meshes)
    {
        Transform editable = NewGroup(root, "Puzzle_Editable");
        editable.position = new Vector3(0f, 0.3f, 0f);

        Transform solution = NewGroup(editable, "SolutionPoseMarker");
        solution.localPosition = Vector3.zero;
        solution.localRotation = Quaternion.identity;

        Transform start = NewGroup(editable, "START_POSE__edit_this_to_change_difficulty");
        start.localPosition = Vector3.zero;
        start.localRotation = Quaternion.Euler(38f, -54f, 86f);
        start.gameObject.SetActive(false);

        Transform housing = NewGroup(editable, "CrystalHousing");
        CreatePrimitive("Housing_Backplate", PrimitiveType.Cylinder, housing, new Vector3(0f, 0f, 1.45f), new Vector3(6.5f, 0.22f, 6.5f), Quaternion.Euler(90f, 0f, 0f), materials.charredRock, true);
        CreatePrimitive("Housing_InnerBasin", PrimitiveType.Cylinder, housing, new Vector3(0f, 0f, 1.2f), new Vector3(5.75f, 0.16f, 5.75f), Quaternion.Euler(90f, 0f, 0f), materials.basalt, true);
        CreateTorus("Bronze_Outer_Ring", housing, Vector3.zero + new Vector3(0f, 0f, 0.55f), Quaternion.Euler(90f, 0f, 0f), 3.15f, 0.22f, materials.bronze, meshes.torus);
        CreateTorus("Bronze_Inner_Ring", housing, new Vector3(0f, 0f, 0.35f), Quaternion.Euler(90f, 0f, 0f), 2.78f, 0.075f, materials.bronzeLight, meshes.torus);
        CreateTorus("Bronze_Outer_Groove", housing, new Vector3(0f, 0f, 0.72f), Quaternion.Euler(90f, 0f, 0f), 3.46f, 0.055f, materials.rune, meshes.torus);

        Transform orb = NewGroup(editable, "CrystalOrb");
        orb.localPosition = new Vector3(0f, 0f, 0.05f);
        GameObject shell = CreatePrimitive("CrystalShell__transparent_3D", PrimitiveType.Sphere, orb, Vector3.zero, new Vector3(5.25f, 5.25f, 5.25f), Quaternion.identity, materials.crystalGlass, false);
        Renderer shellRenderer = shell.GetComponent<Renderer>();
        shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;

        Transform rig = NewGroup(orb, "RotatablePelletRig");
        rig.localRotation = start.localRotation;
        List<Vector3> pelletPoints = BuildFireRunePoints();
        Transform pelletGroup = NewGroup(rig, "Baked_Crystal_Pellets");
        for (int i = 0; i < pelletPoints.Count; i++)
        {
            Vector3 point = pelletPoints[i];
            GameObject pellet = new GameObject("Pellet_" + i.ToString("00"), typeof(MeshFilter), typeof(MeshRenderer));
            pellet.transform.SetParent(pelletGroup, false);
            pellet.transform.localPosition = point;
            pellet.transform.localRotation = Quaternion.Euler(i * 19f, i * 31f, i * 47f);
            float scale = 0.105f + (i % 4) * 0.018f;
            pellet.transform.localScale = new Vector3(scale * 0.78f, scale * 1.25f, scale * 0.78f);
            MeshFilter filter = pellet.GetComponent<MeshFilter>();
            filter.sharedMesh = meshes.diamond;
            MeshRenderer renderer = pellet.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = i % 5 == 0 ? materials.crystalLight : materials.crystal;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        Transform guide = NewGroup(solution, "Guide_Ember_Sigil__H_toggles");
        guide.localPosition = new Vector3(0f, 0f, 1.85f);
        CreateRuneTubes(guide, materials.rune, 0.035f);
        guide.gameObject.SetActive(true);

        Transform solved = NewGroup(editable, "SolvedFX__appears_on_success");
        CreateTorus("Solved_Ring", solved, new Vector3(0f, 0f, -0.25f), Quaternion.Euler(90f, 0f, 0f), 3.75f, 0.065f, materials.rune, meshes.torus);
        CreateTorus("Solved_Core_Ring", solved, new Vector3(0f, 0f, -0.4f), Quaternion.Euler(90f, 0f, 0f), 2.15f, 0.04f, materials.crystalLight, meshes.torus);
        ParticleSystem solvedParticles = CreateEmberParticles(solved, materials, new Vector3(0f, 0f, -0.3f), "Solved_Sparks");
        ParticleSystem.EmissionModule solvedEmission = solvedParticles.emission;
        solvedEmission.rateOverTime = 0f;
        solvedEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
        solved.gameObject.SetActive(false);

        Light crystalLight = NewLight(editable, "CrystalCoreLight", LightType.Point, new Color(1f, 0.13f, 0.025f), 2.2f, 8f, new Vector3(0f, 0.1f, -2.4f));
        crystalLight.shadows = LightShadows.None;

        return new PuzzleReferences
        {
            root = editable,
            rig = rig,
            solution = solution,
            start = start,
            hintGuide = guide.gameObject,
            solvedFx = solved.gameObject,
            crystalLight = crystalLight,
            shellRenderer = shellRenderer
        };
    }

    private static UIReferences CreateUserInterface(Transform root)
    {
        GameObject canvasObject = new GameObject("UI_FireCave", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(root, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform full = canvasObject.GetComponent<RectTransform>();
        Image topShade = CreateImage(full, "TopShade", new Vector2(0f, 0.74f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0.015f, 0.004f, 0.002f, 0.38f));
        topShade.raycastTarget = false;
        Image bottomShade = CreateImage(full, "BottomShade", new Vector2(0f, 0f), new Vector2(1f, 0.24f), Vector2.zero, Vector2.zero, new Color(0.015f, 0.004f, 0.002f, 0.48f));
        bottomShade.raycastTarget = false;

        RectTransform tag = CreatePanel(full, "Header_Tag", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(88f, -92f), new Vector2(420f, 46f), UIInk, UIPaper);
        Text tagText = CreateText(tag, "TagText", "THỬ THÁCH  /  HANG LỬA  •  05", 18, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.63f, 1f), new Vector2(20f, 0f), new Vector2(380f, 42f), font);
        tagText.fontStyle = FontStyle.Bold;

        RectTransform title = CreatePanel(full, "Header_Title", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(88f, -188f), new Vector2(760f, 76f), new Color(0.035f, 0.009f, 0.004f, 0.9f), Bronze);
        Text titleText = CreateText(title, "TitleText", "HỎA ẤN TINH CẦU", 35, TextAnchor.MiddleLeft, new Color(1f, 0.76f, 0.48f, 1f), new Vector2(22f, 0f), new Vector2(710f, 70f), font);
        titleText.fontStyle = FontStyle.Bold;
        CreateText(title, "TitleSub", "CRYSTAL MARBLE  /  FIRE CAVE", 12, TextAnchor.LowerLeft, new Color(1f, 0.3f, 0.08f, 0.94f), new Vector2(24f, 7f), new Vector2(500f, 23f), font);

        RectTransform anglePanel = CreatePanel(full, "AnglePanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-88f, -110f), new Vector2(280f, 76f), new Color(0.035f, 0.009f, 0.004f, 0.88f), Bronze);
        Text angleLabel = CreateText(anglePanel, "AngleLabel", "GÓC LỆCH", 12, TextAnchor.UpperLeft, new Color(0.9f, 0.55f, 0.32f, 1f), new Vector2(18f, -10f), new Vector2(220f, 25f), font);
        Text angleText = CreateText(anglePanel, "AngleValue", "LỆCH  000°", 23, TextAnchor.LowerLeft, new Color(1f, 0.84f, 0.62f, 1f), new Vector2(18f, 3f), new Vector2(240f, 45f), font);
        angleText.fontStyle = FontStyle.Bold;

        RectTransform objective = CreatePanel(full, "ObjectiveCard", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(88f, 74f), new Vector2(330f, 232f), new Color(0.035f, 0.012f, 0.007f, 0.82f), new Color(0.34f, 0.12f, 0.045f, 0.95f));
        CreateText(objective, "ObjectiveTitle", "MỤC TIÊU", 15, TextAnchor.UpperLeft, new Color(1f, 0.48f, 0.16f, 1f), new Vector2(22f, -12f), new Vector2(270f, 29f), font).fontStyle = FontStyle.Bold;
        CreateText(objective, "ObjectiveCopy", "Xoay các mảnh tinh thể\nđể tạo thành hình Hỏa Ấn.\n\nKhớp hình chiếu rồi giữ yên\ncho đến khi lõi cộng hưởng.", 16, TextAnchor.UpperLeft, new Color(0.93f, 0.76f, 0.59f, 1f), new Vector2(22f, -52f), new Vector2(286f, 155f), font);

        RectTransform meter = CreatePanel(full, "AlignmentMeter", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 206f), new Vector2(640f, 48f), new Color(0.02f, 0.006f, 0.003f, 0.8f), new Color(0.38f, 0.15f, 0.055f, 1f));
        CreateText(meter, "MeterTitle", "MỨC CỘNG HƯỞNG", 12, TextAnchor.UpperLeft, new Color(0.91f, 0.58f, 0.33f, 1f), new Vector2(18f, -6f), new Vector2(250f, 24f), font).fontStyle = FontStyle.Bold;
        Image meterBack = CreateImage(meter, "MeterBack", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(600f, 11f), new Color(0.11f, 0.028f, 0.012f, 1f));
        Image meterFill = CreateImage(meterBack.rectTransform, "MeterFill", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Color(1f, 0.23f, 0.035f, 1f));
        meterFill.type = Image.Type.Filled;
        meterFill.fillMethod = Image.FillMethod.Horizontal;
        meterFill.fillOrigin = 0;
        meterFill.fillAmount = 0f;
        RectTransform fillRect = meterFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        RectTransform controls = CreatePanel(full, "ControlDeck", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(860f, 110f), new Color(0.03f, 0.008f, 0.004f, 0.94f), new Color(0.37f, 0.15f, 0.055f, 1f));
        CreateText(controls, "ControlTitle", "ĐIỀU KHIỂN", 13, TextAnchor.UpperLeft, new Color(1f, 0.42f, 0.13f, 1f), new Vector2(24f, -10f), new Vector2(170f, 22f), font).fontStyle = FontStyle.Bold;
        CreateText(controls, "ControlCopy", "KÉO CHUỘT  •  WASD / MŨI TÊN  •  Q / E  •  R  ĐẶT LẠI", 15, TextAnchor.MiddleLeft, new Color(0.93f, 0.78f, 0.63f, 1f), new Vector2(24f, -2f), new Vector2(790f, 34f), font);
        CreateKeyCap(controls, "LMB", new Vector2(310f, -43f), font, UIPaper);
        CreateKeyCap(controls, "Q / E", new Vector2(393f, -43f), font, UIPaper);
        CreateKeyCap(controls, "R", new Vector2(500f, -43f), font, UIPaper);
        CreateKeyCap(controls, "H", new Vector2(548f, -43f), font, UIPaper);

        Text instruction = CreateText(full, "Instruction", "", 14, TextAnchor.MiddleCenter, new Color(0.75f, 0.55f, 0.42f, 1f), new Vector2(0f, 125f), new Vector2(1100f, 34f), font);
        Text status = CreateText(full, "Status", "ĐANG TÌM HÌNH CHIẾU", 18, TextAnchor.MiddleCenter, new Color(0.66f, 0.50f, 0.40f, 1f), new Vector2(0f, 165f), new Vector2(900f, 36f), font);
        status.fontStyle = FontStyle.Bold;
        Text hint = CreateText(full, "HintToggle", "H  •  ẨN HỎA ẤN", 14, TextAnchor.MiddleRight, new Color(0.9f, 0.66f, 0.42f, 1f), new Vector2(-88f, 232f), new Vector2(260f, 34f), font);

        RectTransform success = CreateRect(full, "SuccessOverlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image successShade = CreateImage(success, "SuccessShade", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.003f, 0.001f, 0.86f));
        successShade.raycastTarget = true;
        RectTransform successCard = CreatePanel(success, "SuccessCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(720f, 390f), new Color(0.055f, 0.012f, 0.006f, 0.98f), new Color(0.85f, 0.31f, 0.075f, 1f));
        Text successTitle = CreateText(successCard, "SuccessTitle", "HỎA ẤN THỨC TỈNH", 32, TextAnchor.MiddleCenter, new Color(1f, 0.76f, 0.41f, 1f), new Vector2(0f, 92f), new Vector2(650f, 55f), font);
        successTitle.fontStyle = FontStyle.Bold;
        CreateText(successCard, "SuccessSub", "Tinh thể đã cộng hưởng với mạch dung nham.", 17, TextAnchor.MiddleCenter, new Color(0.96f, 0.78f, 0.60f, 1f), new Vector2(0f, 42f), new Vector2(640f, 34f), font);
        CreateText(successCard, "SuccessReward", "LỐI VÀO HANG LỬA ĐÃ ĐƯỢC MỞ KHÓA", 14, TextAnchor.MiddleCenter, new Color(1f, 0.36f, 0.10f, 1f), new Vector2(0f, 4f), new Vector2(620f, 30f), font).fontStyle = FontStyle.Bold;
        Button continueButton = CreateButton(successCard, "ContinueButton", "TIẾP TỤC", new Vector2(0f, -88f), new Vector2(280f, 60f), font);
        success.gameObject.AddComponent<CanvasGroup>();
        CanvasGroup successGroup = success.GetComponent<CanvasGroup>();
        successGroup.alpha = 0f;
        successGroup.interactable = false;
        successGroup.blocksRaycasts = false;
        success.gameObject.SetActive(true);

        return new UIReferences
        {
            canvas = canvas,
            angleText = angleText,
            statusText = status,
            instructionText = instruction,
            hintText = hint,
            alignmentFill = meterFill,
            successOverlay = successGroup,
            successTitleText = successTitle,
            continueButton = continueButton
        };
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color fill, Color border)
    {
        RectTransform outer = CreateRect(parent, name, anchorMin, anchorMax, anchoredPosition, size);
        Image borderImage = outer.gameObject.AddComponent<Image>();
        borderImage.color = border;
        borderImage.raycastTarget = false;
        RectTransform inner = CreateRect(outer, "PanelFill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        inner.offsetMin = new Vector2(2f, 2f);
        inner.offsetMax = new Vector2(-2f, -2f);
        Image fillImage = inner.gameObject.AddComponent<Image>();
        fillImage.color = fill;
        fillImage.raycastTarget = false;
        CreateText(outer, "PanelCorner_L", "◆", 9, TextAnchor.MiddleCenter, border, new Vector2(-size.x * 0.5f + 8f, size.y * 0.5f - 8f), new Vector2(18f, 18f), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        CreateText(outer, "PanelCorner_R", "◆", 9, TextAnchor.MiddleCenter, border, new Vector2(size.x * 0.5f - 8f, size.y * 0.5f - 8f), new Vector2(18f, 18f), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        return outer;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Font font)
    {
        RectTransform rect = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.74f, 0.20f, 0.045f, 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.74f, 0.20f, 0.045f, 1f);
        colors.highlightedColor = new Color(1f, 0.39f, 0.09f, 1f);
        colors.pressedColor = new Color(0.42f, 0.08f, 0.018f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.25f, 0.09f, 0.03f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        Text text = CreateText(rect, "Label", label, 18, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.71f, 1f), Vector2.zero, size - new Vector2(8f, 8f), font);
        text.fontStyle = FontStyle.Bold;
        return button;
    }

    private static void CreateKeyCap(Transform parent, string label, Vector2 position, Font font, Color color)
    {
        RectTransform rect = CreateRect(parent, "Key_" + label.Replace("/", "_"), new Vector2(0f, 0f), new Vector2(0f, 0f), position, new Vector2(label.Length > 2 ? 76f : 38f, 30f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.33f, 0.10f, 0.032f, 1f);
        image.raycastTarget = false;
        Text text = CreateText(rect, "KeyLabel", label, 12, TextAnchor.MiddleCenter, color, Vector2.zero, new Vector2(70f, 26f), font);
        text.fontStyle = FontStyle.Bold;
    }

    private static Text CreateText(Transform parent, string name, string content, int size, TextAnchor alignment, Color color, Vector2 position, Vector2 dimensions, Font font)
    {
        RectTransform rect = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, dimensions);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule module = eventSystem.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }

    private static void WirePuzzleController(PuzzleReferences puzzle, UIReferences ui, MaterialLibrary materials)
    {
        GameObject controllerObject = puzzle.root.gameObject;
        CrystalMarblePuzzle controller = controllerObject.AddComponent<CrystalMarblePuzzle>();
        AudioSource source = controllerObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        AudioSource ambience = controllerObject.AddComponent<AudioSource>();
        ambience.playOnAwake = false;
        ambience.loop = true;
        ambience.spatialBlend = 0f;
        ambience.volume = 0.18f;

        SerializedObject serialized = new SerializedObject(controller);
        SetReference(serialized, "marbleRig", puzzle.rig);
        SetReference(serialized, "solutionReference", puzzle.solution);
        SetReference(serialized, "startReference", puzzle.start);
        SetReference(serialized, "hintGuide", puzzle.hintGuide);
        SetReference(serialized, "solvedFx", puzzle.solvedFx);
        SetReference(serialized, "crystalLight", puzzle.crystalLight);
        SetReference(serialized, "crystalShellRenderer", puzzle.shellRenderer);
        SetReference(serialized, "angleText", ui.angleText);
        SetReference(serialized, "statusText", ui.statusText);
        SetReference(serialized, "instructionText", ui.instructionText);
        SetReference(serialized, "hintText", ui.hintText);
        SetReference(serialized, "alignmentFill", ui.alignmentFill);
        SetReference(serialized, "successOverlay", ui.successOverlay);
        SetReference(serialized, "successTitleText", ui.successTitleText);
        SetReference(serialized, "continueButton", ui.continueButton);
        SetReference(serialized, "feedbackSource", source);
        SetReference(serialized, "ambienceSource", ambience);
        SetReference(serialized, "ambienceClip", materials.ambienceClip);
        SetReference(serialized, "rotateClip", materials.rotateClip);
        SetReference(serialized, "nearClip", materials.nearClip);
        SetReference(serialized, "solvedClip", materials.solvedClip);
        SetReference(serialized, "resetClip", materials.resetClip);
        SetReference(serialized, "hintClip", materials.hintClip);
        serialized.FindProperty("returnSceneName").stringValue = "Map5";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(ui.continueButton.onClick, controller.ContinueAfterSolved);
    }

    private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static MaterialLibrary CreateMaterials()
    {
        MaterialLibrary materials = new MaterialLibrary();
        materials.charcoal = CreateMaterial("CM_Charcoal", Charcoal, 0.12f, 0.62f, Color.black, 0f, false);
        materials.charredRock = CreateMaterial("CM_CharredRock", CharredRock, 0.18f, 0.5f, Color.black, 0f, false);
        materials.basalt = CreateMaterial("CM_Basalt", Basalt, 0.34f, 0.68f, Color.black, 0f, false);
        materials.bronze = CreateMaterial("CM_AgedBronze", Bronze, 0.72f, 0.76f, new Color(0.3f, 0.035f, 0.006f), 0.7f, false);
        materials.bronzeLight = CreateMaterial("CM_HotBronze", BronzeLight, 0.58f, 0.8f, new Color(0.75f, 0.08f, 0.008f), 1.8f, false);
        materials.lava = CreateMaterial("CM_LavaGlow", new Color(0.12f, 0.006f, 0.002f), 0f, 0.42f, Ember, 7.5f, false);
        materials.crystal = CreateMaterial("CM_EmberCrystal", Crystal, 0.2f, 0.86f, new Color(1f, 0.03f, 0.004f), 5.1f, false);
        materials.crystalLight = CreateMaterial("CM_EmberCrystal_Highlight", new Color(1f, 0.73f, 0.28f), 0.16f, 0.92f, new Color(1f, 0.12f, 0.008f), 8.5f, false);
        materials.crystalGlass = CreateMaterial("CM_CrystalShell_Glass", new Color(0.63f, 0.095f, 0.02f, 0.18f), 0.08f, 0.96f, new Color(1f, 0.035f, 0.002f), 2.8f, true);
        materials.rune = CreateMaterial("CM_RuneGuide", Rune, 0f, 0.35f, Rune, 5.6f, true);
        materials.rune.SetFloat("_Cull", (float)CullMode.Off);
        materials.fireParticle = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Quoc/Asset/Piloto Studio/Materials/Shared/Fire_Add_Soft.mat");
        materials.ambienceClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Tien/MagicArsenal/Effects/Sound/Loops/fireloop.wav");
        materials.rotateClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Tien/MIgame/freesound_community-stoneblockdragwoodgrind-82327.mp3");
        materials.nearClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Quoc/Asset/Game UI & Puzzle Sound Effects Pack/Rewards/GUIP_Reward_Star_Rating_Glassy_01.wav");
        materials.solvedClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Quoc/Asset/Game UI & Puzzle Sound Effects Pack/Rewards/GUIP_Reward_RevealHit_Shimmer_Chime_05.wav");
        materials.resetClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Quoc/Asset/Game UI & Puzzle Sound Effects Pack/UI/GUIP_UI_Select_Metal_Light_01.wav");
        materials.hintClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Quoc/Asset/Game UI & Puzzle Sound Effects Pack/UI/GUIP_UI_Select_Sci-Fi_Glow_01.wav");
        return materials;
    }

    private static Material CreateMaterial(string name, Color color, float metallic, float smoothness, Color emission, float emissionIntensity, bool transparent)
    {
        string path = GeneratedRoot + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.SetColor("_EmissionColor", emission * emissionIntensity);
        if (emissionIntensity > 0f)
        {
            material.EnableKeyword("_EMISSION");
        }

        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            material.SetFloat("_Surface", 0f);
            material.renderQueue = -1;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static MeshLibrary CreateMeshes()
    {
        MeshLibrary meshes = new MeshLibrary();
        meshes.diamond = LoadOrCreateMesh("CM_DiamondShard", CreateDiamondMesh);
        meshes.torus = LoadOrCreateMesh("CM_OrnateTorus", () => CreateTorusMesh(64, 14));
        return meshes;
    }

    private static Mesh LoadOrCreateMesh(string name, Func<Mesh> factory)
    {
        string path = GeneratedRoot + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = factory();
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
        }
        return mesh;
    }

    private static Mesh CreateDiamondMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, -0.5f), new Vector3(0f, -0.5f, 0f)
        };
        mesh.triangles = new[]
        {
            0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1,
            5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateTorusMesh(int radialSegments, int tubularSegments)
    {
        float major = 1f;
        float minor = 0.07f;
        Vector3[] vertices = new Vector3[radialSegments * tubularSegments];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[radialSegments * tubularSegments * 6];
        for (int i = 0; i < radialSegments; i++)
        {
            float u = i / (float)radialSegments * Mathf.PI * 2f;
            for (int j = 0; j < tubularSegments; j++)
            {
                float v = j / (float)tubularSegments * Mathf.PI * 2f;
                int index = i * tubularSegments + j;
                float radius = major + minor * Mathf.Cos(v);
                vertices[index] = new Vector3(radius * Mathf.Cos(u), minor * Mathf.Sin(v), radius * Mathf.Sin(u));
                uvs[index] = new Vector2(i / (float)radialSegments, j / (float)tubularSegments);
                int nextI = (i + 1) % radialSegments;
                int nextJ = (j + 1) % tubularSegments;
                int tri = (i * tubularSegments + j) * 6;
                triangles[tri] = index;
                triangles[tri + 1] = nextI * tubularSegments + j;
                triangles[tri + 2] = i * tubularSegments + nextJ;
                triangles[tri + 3] = i * tubularSegments + nextJ;
                triangles[tri + 4] = nextI * tubularSegments + j;
                triangles[tri + 5] = nextI * tubularSegments + nextJ;
            }
        }
        Mesh mesh = new Mesh { vertices = vertices, uv = uvs, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void CreateTorus(string name, Transform parent, Vector3 position, Quaternion rotation, float majorRadius, float minorRadius, Material material, Mesh torusMesh)
    {
        GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.transform.localScale = new Vector3(majorRadius, minorRadius / 0.07f, majorRadius);
        go.GetComponent<MeshFilter>().sharedMesh = torusMesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.transform.localScale = scale;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
        if (!collider)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                UnityEngine.Object.DestroyImmediate(col);
            }
        }
        return go;
    }

    private static Transform NewGroup(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Light NewLight(Transform parent, string name, LightType type, Color color, float intensity, float range, Vector3 localPosition)
    {
        GameObject go = new GameObject(name, typeof(Light));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        Light light = go.GetComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        return light;
    }

    private static ParticleSystem CreateFireParticles(Transform parent, MaterialLibrary materials, Vector3 localPosition, string name, float scale)
    {
        ParticleSystem particles = CreateEmberParticles(parent, materials, localPosition, name);
        particles.transform.localScale = Vector3.one * scale;
        ParticleSystem.MainModule main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.52f);
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 26f;
        return particles;
    }

    private static ParticleSystem CreateEmberParticles(Transform parent, MaterialLibrary materials, Vector3 localPosition, string name)
    {
        GameObject go = new GameObject(name, typeof(ParticleSystem), typeof(ParticleSystemRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        ParticleSystem particles = go.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.19f, 0.025f, 0.95f), new Color(1f, 0.74f, 0.25f, 1f));
        main.maxParticles = 180;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 18f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 5.6f;
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.42f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.11f, 0.11f);
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = materials.fireParticle;
        return particles;
    }

    private static List<Vector3> BuildFireRunePoints()
    {
        List<Vector3> points = new List<Vector3>();
        AddSegment(points, new Vector3(-0.04f, -1.65f, 0f), new Vector3(-0.04f, 0.92f, 0f), 12, 0.19f);
        AddSegment(points, new Vector3(-0.04f, 0.18f, 0f), new Vector3(-1.18f, 1.08f, 0f), 8, 0.32f);
        AddSegment(points, new Vector3(-0.04f, 0.18f, 0f), new Vector3(1.18f, 1.08f, 0f), 8, 0.54f);
        AddSegment(points, new Vector3(-0.04f, 0.88f, 0f), new Vector3(-0.52f, 1.55f, 0f), 5, 0.78f);
        AddSegment(points, new Vector3(-0.52f, 1.55f, 0f), new Vector3(0.03f, 2.04f, 0f), 5, 1.03f);
        AddSegment(points, new Vector3(0.03f, 2.04f, 0f), new Vector3(0.58f, 1.47f, 0f), 5, 1.24f);
        AddSegment(points, new Vector3(0.58f, 1.47f, 0f), new Vector3(0.36f, 0.92f, 0f), 4, 1.48f);
        AddSegment(points, new Vector3(-0.04f, -1.03f, 0f), new Vector3(-0.72f, -1.52f, 0f), 5, 1.71f);
        AddSegment(points, new Vector3(-0.04f, -1.03f, 0f), new Vector3(0.62f, -1.5f, 0f), 5, 1.94f);
        return points;
    }

    private static void AddSegment(List<Vector3> points, Vector3 start, Vector3 end, int count, float phase)
    {
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            point.z = Mathf.Sin((t + phase) * Mathf.PI * 2f) * 0.11f;
            points.Add(point);
        }
    }

    private static void CreateRuneTubes(Transform parent, Material material, float radius)
    {
        Vector3[] points =
        {
            new Vector3(-0.04f, -1.65f, 0f), new Vector3(-0.04f, 0.92f, 0f),
            new Vector3(-0.04f, 0.18f, 0f), new Vector3(-1.18f, 1.08f, 0f),
            new Vector3(-0.04f, 0.18f, 0f), new Vector3(1.18f, 1.08f, 0f),
            new Vector3(-0.04f, 0.88f, 0f), new Vector3(-0.52f, 1.55f, 0f),
            new Vector3(-0.52f, 1.55f, 0f), new Vector3(0.03f, 2.04f, 0f),
            new Vector3(0.03f, 2.04f, 0f), new Vector3(0.58f, 1.47f, 0f),
            new Vector3(0.58f, 1.47f, 0f), new Vector3(0.36f, 0.92f, 0f),
            new Vector3(-0.04f, -1.03f, 0f), new Vector3(-0.72f, -1.52f, 0f),
            new Vector3(-0.04f, -1.03f, 0f), new Vector3(0.62f, -1.5f, 0f)
        };
        for (int i = 0; i < points.Length; i += 2)
        {
            CreateCylinderBetween(parent, "GuideSegment_" + (i / 2).ToString("00"), points[i], points[i + 1], radius, material);
        }
    }

    private static GameObject CreateCylinderBetween(Transform parent, string name, Vector3 start, Vector3 end, float radius, Material material)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;
        GameObject go = CreatePrimitive(name, PrimitiveType.Cylinder, parent, (start + end) * 0.5f, new Vector3(radius, length * 0.5f, radius), Quaternion.FromToRotation(Vector3.up, direction.normalized), material, false);
        return go;
    }

    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(scene => scene.path != ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static void EnsureDirectory(string assetPath)
    {
        string absolute = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absolute))
        {
            Directory.CreateDirectory(absolute);
        }
    }

    private sealed class MaterialLibrary
    {
        public Material charcoal;
        public Material charredRock;
        public Material basalt;
        public Material bronze;
        public Material bronzeLight;
        public Material lava;
        public Material crystal;
        public Material crystalLight;
        public Material crystalGlass;
        public Material rune;
        public Material fireParticle;
        public AudioClip ambienceClip;
        public AudioClip rotateClip;
        public AudioClip nearClip;
        public AudioClip solvedClip;
        public AudioClip resetClip;
        public AudioClip hintClip;
    }

    private sealed class MeshLibrary
    {
        public Mesh diamond;
        public Mesh torus;
    }

    private sealed class PuzzleReferences
    {
        public Transform root;
        public Transform rig;
        public Transform solution;
        public Transform start;
        public GameObject hintGuide;
        public GameObject solvedFx;
        public Light crystalLight;
        public Renderer shellRenderer;
    }

    private sealed class UIReferences
    {
        public Canvas canvas;
        public Text angleText;
        public Text statusText;
        public Text instructionText;
        public Text hintText;
        public Image alignmentFill;
        public CanvasGroup successOverlay;
        public Text successTitleText;
        public Button continueButton;
    }
}
