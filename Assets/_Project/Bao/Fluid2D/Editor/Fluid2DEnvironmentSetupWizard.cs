#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;
using Bao.Fluid2D;

namespace Bao.Fluid2D.Editor
{
    /// <summary>
    /// Designer wizard: create or repair an interactive water region.
    /// Does not auto-modify Map_IceandSnow — only runs on explicit Create/Repair click.
    /// </summary>
    public class Fluid2DEnvironmentSetupWizard : EditorWindow
    {
        private const string PrefabPath = "Assets/_Project/Bao/Fluid2D/Prefabs/InteractiveWater2D.prefab";
        private const string PresetMapPath = "Assets/_Project/Bao/Fluid2D/Presets/Fluid2D_Map_IceandSnow.asset";
        private const string PresetStarterPath = "Assets/_Project/Bao/Fluid2D/Presets/Fluid2D_Starter.asset";
        private const string SettingsPath = "Assets/_Project/Bao/Fluid2D/Settings/Fluid2DSettings_Default.asset";
        private const string MetaballMatPath = "Assets/_Project/Bao/Fluid2D/Materials/FluidMetaball_Mat.mat";
        private const string SplatMatPath = "Assets/_Project/Bao/Fluid2D/Materials/FluidSplat_Mat.mat";
        private const string ComputePath = "Assets/_Project/Bao/Fluid2D/Shaders/FluidSimulation.compute";

        // Only set via the ObjectField or explicit "Use Selection" — never overwritten by passive scene selection.
        private SpriteShapeController selectedIce;
        private Fluid2DEnvironmentPreset preset;
        private Fluid2DEnvironmentController existingController;
        private bool createOptionalEmitter;
        private bool createOptionalDrain;
        private bool frameCameraOnCreate = true;
        private Vector2 scroll;
        private string status =
            "Leave Ice SpriteShape empty for a starter IceShape region. " +
            "Assign an ice shape only when you intentionally want an external silhouette.";

        [MenuItem("Tools/Bao/Fluid2D/Environment Water Setup")]
        public static void Open()
        {
            var window = GetWindow<Fluid2DEnvironmentSetupWizard>("Fluid2D Water Setup");
            window.minSize = new Vector2(440, 400);
            window.Show();
        }

        private void OnEnable()
        {
            // Only sync controller from selection; never auto-fill ice (avoids stale Snow/map art wiring).
            TrySyncControllerSelection();
            if (preset == null)
                preset = AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(PresetStarterPath)
                         ?? AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(PresetMapPath);
        }

        private void OnSelectionChange()
        {
            TrySyncControllerSelection();
            Repaint();
        }

        private void TrySyncControllerSelection()
        {
            if (Selection.activeGameObject == null)
                return;

            var ctrl = Selection.activeGameObject.GetComponent<Fluid2DEnvironmentController>()
                       ?? Selection.activeGameObject.GetComponentInParent<Fluid2DEnvironmentController>();
            if (ctrl != null)
                existingController = ctrl;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Environment Water Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates or repairs ONE interactive ice/water region under the current scene.\n" +
                "Does NOT auto-save or rewrite Map_IceandSnow — only acts when you click Create/Repair.\n" +
                "Empty Ice SpriteShape = use the prefab/child IceShape starter (recommended for TestScene).\n" +
                "Constraint: one Fluid2D GPU system per main camera.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            selectedIce = (SpriteShapeController)EditorGUILayout.ObjectField(
                "Ice SpriteShape (optional)", selectedIce, typeof(SpriteShapeController), true);
            if (EditorGUI.EndChangeCheck() && selectedIce == null)
            {
                // User cleared the field — keep it empty (do not re-sync from selection).
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Ice", GUILayout.Width(140)))
                {
                    var ice = Selection.activeGameObject != null
                        ? Selection.activeGameObject.GetComponent<SpriteShapeController>()
                          ?? Selection.activeGameObject.GetComponentInParent<SpriteShapeController>()
                        : null;
                    selectedIce = ice;
                    status = ice != null
                        ? $"Using external ice '{ice.name}'."
                        : "No SpriteShapeController on current selection.";
                }
                if (GUILayout.Button("Clear Ice Field", GUILayout.Width(120)))
                {
                    selectedIce = null;
                    status = "Ice field cleared — Create will use starter child IceShape.";
                }
            }

            existingController = (Fluid2DEnvironmentController)EditorGUILayout.ObjectField(
                "Existing Controller (repair)", existingController, typeof(Fluid2DEnvironmentController), true);
            preset = (Fluid2DEnvironmentPreset)EditorGUILayout.ObjectField(
                "Preset", preset, typeof(Fluid2DEnvironmentPreset), false);

            createOptionalEmitter = EditorGUILayout.ToggleLeft("Include optional Emitter child", createOptionalEmitter);
            createOptionalDrain = EditorGUILayout.ToggleLeft("Include optional Drain child", createOptionalDrain);
            frameCameraOnCreate = EditorGUILayout.ToggleLeft(
                "Frame orthographic camera on create (2D)", frameCameraOnCreate);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create New Region", GUILayout.Height(34)))
                    CreateNewRegion();
                if (GUILayout.Button("Repair Selected Region", GUILayout.Height(34)))
                    RepairRegion();
            }

            if (GUILayout.Button("Ensure Presets + Prefab Assets Exist", GUILayout.Height(26)))
            {
                Fluid2DEnvironmentAssetUtility.EnsureAllAssets();
                if (preset == null)
                    preset = AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(PresetStarterPath);
                status = "Presets and prefab assets ensured (starter geometry + fill refreshed).";
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void CreateNewRegion()
        {
            Fluid2DEnvironmentAssetUtility.EnsureAllAssets();

            // Empty ice field → starter region. Do not reuse scene selection.
            SpriteShapeController externalIce = selectedIce;
            bool useExternalIce = externalIce != null;

            if (preset == null)
            {
                preset = useExternalIce
                    ? AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(PresetMapPath)
                    : AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(PresetStarterPath)
                      ?? AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(PresetMapPath);
            }

            GameObject root;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(root, "Create InteractiveWater2D");
            }
            else
            {
                root = Fluid2DEnvironmentAssetUtility.BuildRegionHierarchy(
                    "InteractiveWater2D",
                    useExternalIce ? externalIce : null,
                    createOptionalEmitter,
                    createOptionalDrain);
                Undo.RegisterCreatedObjectUndo(root, "Create InteractiveWater2D");
            }

            var controller = root.GetComponent<Fluid2DEnvironmentController>()
                             ?? root.GetComponentInChildren<Fluid2DEnvironmentController>(true);
            if (controller == null)
                controller = Undo.AddComponent<Fluid2DEnvironmentController>(root);

            // Optional emitter / drain on prefab path.
            if (prefab != null)
                EnsureOptionalChildren(root, controller, createOptionalEmitter, createOptionalDrain);

            if (useExternalIce && !externalIce.transform.IsChildOf(root.transform))
            {
                // Explicit external ice only — never from stale selection.
                // Do not rebind to prefab child IceShape after this.
                WireExternalIce(controller, externalIce);
            }
            else
            {
                // Prefer / create child IceShape and wire phase + renderer to it.
                useExternalIce = false;
                Fluid2DEnvironmentAssetUtility.EnsureAndWireChildIceShape(controller, createIfMissing: true);
            }

            if (preset != null)
            {
                Undo.RecordObject(controller, "Apply Preset");
                controller.ApplyPreset(preset);
            }

            if (!useExternalIce)
            {
                // ApplyPreset can leave ice profile only; keep hierarchy ownership on child IceShape.
                Fluid2DEnvironmentAssetUtility.EnsureAndWireChildIceShape(controller, createIfMissing: true);
                controller.TryBindIceFromHierarchy(forceIfExternal: true);
                Fluid2DEnvironmentAssetUtility.FinalizeStarterIceGeometry(controller);
            }

            controller.AutoWire(true);
            Fluid2DEnvironmentAssetUtility.PlaceRegionInCameraView(root);
            if (frameCameraOnCreate)
                Fluid2DEnvironmentAssetUtility.EnsureMainCamera2DFraming(controller, root.transform.position);

            EnsureComputeShader(controller);
            controller.ValidateSetup();
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);

            string iceName = controller.IceSpriteShapeController != null
                ? controller.IceSpriteShapeController.name
                : "(none)";
            status = useExternalIce
                ? $"Created region '{root.name}' wired to external ice '{externalIce.name}'. Validation: {controller.LastValidationSummary}"
                : $"Created starter region '{root.name}' with child ice '{iceName}'. Validation: {controller.LastValidationSummary}";
            Debug.Log(controller.FormatValidationReport(), controller);
        }

        private void RepairRegion()
        {
            Fluid2DEnvironmentAssetUtility.EnsureAllAssets();
            var controller = existingController;
            if (controller == null && selectedIce != null)
            {
                controller = selectedIce.GetComponent<Fluid2DEnvironmentController>()
                             ?? selectedIce.GetComponentInParent<Fluid2DEnvironmentController>();
            }

            if (controller == null)
            {
                status = "No controller found. Select a region root or create a new region.";
                return;
            }

            Undo.RecordObject(controller, "Repair Fluid2D Region");

            bool useExternalIce = selectedIce != null
                                  && !selectedIce.transform.IsChildOf(controller.transform);

            if (useExternalIce)
            {
                WireExternalIce(controller, selectedIce);
            }
            else
            {
                // Recover child IceShape even when field is empty or points at the child already.
                controller.TryBindIceFromHierarchy(forceIfExternal: true);
                Fluid2DEnvironmentAssetUtility.EnsureAndWireChildIceShape(controller, createIfMissing: true);
            }

            if (preset != null)
                controller.ApplyPreset(preset);

            controller.AutoWire(true);
            if (!useExternalIce)
                controller.TryBindIceFromHierarchy(forceIfExternal: true);

            Fluid2DEnvironmentAssetUtility.FinalizeStarterIceGeometry(controller);
            EnsureComputeShader(controller);
            controller.ValidateSetup();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            status = $"Repaired '{controller.name}'. Ice={controller.IceSpriteShapeController?.name ?? "null"}. " +
                     $"Validation: {controller.LastValidationSummary}";
            Debug.Log(controller.FormatValidationReport(), controller);
            Selection.activeGameObject = controller.gameObject;
        }

        private static void EnsureOptionalChildren(
            GameObject root,
            Fluid2DEnvironmentController controller,
            bool withEmitter,
            bool withDrain)
        {
            if (root == null || controller == null)
                return;

            var system = controller.FluidSystem
                         ?? root.GetComponentInChildren<Fluid2DSystem>(true);
            if (system == null)
                return;

            if (withEmitter && root.GetComponentInChildren<FluidEmitter2D>(true) == null)
            {
                var eGo = new GameObject("Emitter");
                Undo.RegisterCreatedObjectUndo(eGo, "Add Emitter");
                eGo.transform.SetParent(root.transform, false);
                eGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                var em = eGo.AddComponent<FluidEmitter2D>();
                em.fluidSystem = system;
                em.emitOnStart = false;
            }

            if (withDrain && root.GetComponentInChildren<FluidDrain2D>(true) == null)
            {
                var dGo = new GameObject("Drain");
                Undo.RegisterCreatedObjectUndo(dGo, "Add Drain");
                dGo.transform.SetParent(root.transform, false);
                dGo.transform.localPosition = new Vector3(0f, -3f, 0f);
                var dr = dGo.AddComponent<FluidDrain2D>();
                dr.fluidSystem = system;
                var box = dGo.GetComponent<BoxCollider2D>();
                if (box != null) box.size = new Vector2(4f, 0.5f);
            }
        }

        private static void WireExternalIce(Fluid2DEnvironmentController controller, SpriteShapeController ice)
        {
            if (controller == null || ice == null)
                return;

            var so = new SerializedObject(controller);
            so.FindProperty("iceSpriteShapeController").objectReferenceValue = ice;
            so.FindProperty("iceSpriteShapeRenderer").objectReferenceValue = ice.GetComponent<SpriteShapeRenderer>();
            var phase = ice.GetComponent<IceWaterPhaseToggle2D>();
            if (phase == null)
                phase = Undo.AddComponent<IceWaterPhaseToggle2D>(ice.gameObject);
            so.FindProperty("phaseToggle").objectReferenceValue = phase;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Cross-wire phase to this region's fluid system when available.
            var system = controller.FluidSystem
                         ?? controller.GetComponentInChildren<Fluid2DSystem>(true);
            if (system != null)
                phase.fluidSystem = system;
            phase.spriteShapeController = ice;
            phase.spriteShapeRenderer = ice.GetComponent<SpriteShapeRenderer>();
            phase.iceCollider = ice.GetComponent<Collider2D>();
        }

        private static void EnsureComputeShader(Fluid2DEnvironmentController controller)
        {
            if (controller.FluidSystem == null)
                return;
            if (controller.FluidSystem.simulationShader != null)
                return;
            var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            if (cs != null)
            {
                Undo.RecordObject(controller.FluidSystem, "Assign Compute Shader");
                controller.FluidSystem.simulationShader = cs;
                EditorUtility.SetDirty(controller.FluidSystem);
            }
        }
    }

    /// <summary>
    /// Shared asset/prefab creation used by the wizard and menu bootstrap.
    /// </summary>
    public static class Fluid2DEnvironmentAssetUtility
    {
        public const string PrefabPath = "Assets/_Project/Bao/Fluid2D/Prefabs/InteractiveWater2D.prefab";
        public const string PresetMapPath = "Assets/_Project/Bao/Fluid2D/Presets/Fluid2D_Map_IceandSnow.asset";
        public const string PresetStarterPath = "Assets/_Project/Bao/Fluid2D/Presets/Fluid2D_Starter.asset";
        public const string SettingsPath = "Assets/_Project/Bao/Fluid2D/Settings/Fluid2DSettings_Default.asset";
        public const string MetaballMatPath = "Assets/_Project/Bao/Fluid2D/Materials/FluidMetaball_Mat.mat";
        public const string SplatMatPath = "Assets/_Project/Bao/Fluid2D/Materials/FluidSplat_Mat.mat";
        public const string ComputePath = "Assets/_Project/Bao/Fluid2D/Shaders/FluidSimulation.compute";
        public const string StarterIceProfilePath = "Assets/_Project/Bao/Fluid2D/Presets/Fluid2D_IceProfile_Starter.asset";
        public const string StarterIceFillTexPath = "Assets/_Project/Bao/Fluid2D/Presets/Fluid2D_IceFill_White.png";

        private static readonly Vector3[] StarterSpline =
        {
            new Vector3(-2f, -0.5f, 0f),
            new Vector3(2f, -0.5f, 0f),
            new Vector3(2f, 0.5f, 0f),
            new Vector3(-2f, 0.5f, 0f)
        };

        private static readonly Color StarterIceTint = new Color(0.78f, 0.93f, 1f, 1f);

        [MenuItem("Tools/Bao/Fluid2D/Ensure Environment Assets")]
        public static void EnsureAllAssetsMenu()
        {
            EnsureAllAssets();
            EditorUtility.DisplayDialog("Fluid2D", "Presets and InteractiveWater2D prefab are ready.", "OK");
        }

        [MenuItem("Tools/Bao/Fluid2D/Repair Scene Environment Regions")]
        public static void RepairAllSceneRegionsMenu()
        {
            EnsureAllAssets();
            var controllers = Object.FindObjectsByType<Fluid2DEnvironmentController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (controllers == null || controllers.Length == 0)
            {
                EditorUtility.DisplayDialog("Fluid2D", "No Fluid2DEnvironmentController in the open scene.", "OK");
                return;
            }

            int fixedCount = 0;
            foreach (var c in controllers)
            {
                if (c == null) continue;
                Undo.RecordObject(c, "Repair Scene Fluid2D Regions");
                // Recover child IceShape when no intentional external ice is assigned.
                c.TryBindIceFromHierarchy(forceIfExternal: true);
                EnsureAndWireChildIceShape(c, createIfMissing: true);
                c.AutoWire(false);
                c.TryBindIceFromHierarchy(forceIfExternal: true);
                FinalizeStarterIceGeometry(c);
                EnsureComputeOnController(c);
                c.ValidateSetup();
                EditorUtility.SetDirty(c);
                fixedCount++;
                Debug.Log(c.FormatValidationReport(), c);
            }

            if (controllers.Length > 0)
                EditorSceneManager.MarkSceneDirty(controllers[0].gameObject.scene);

            EditorUtility.DisplayDialog("Fluid2D",
                $"Repaired {fixedCount} region(s). Child IceShape refs recovered; geometry AABB fixed.", "OK");
        }

        [MenuItem("Tools/Bao/Fluid2D/Ensure Main Camera Orthographic (2D)")]
        public static void EnsureMainCamera2DMenu()
        {
            var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Fluid2D", "No camera found in the scene.", "OK");
                return;
            }

            var region = Object.FindFirstObjectByType<Fluid2DEnvironmentController>();
            Vector3 focus = region != null ? region.transform.position : Vector3.zero;
            EnsureMainCamera2DFraming(region, focus);
            EditorUtility.DisplayDialog("Fluid2D",
                $"Camera '{cam.name}' set orthographic (size={cam.orthographicSize:0.##}) for 2D framing.", "OK");
        }

        public static void EnsureAllAssets()
        {
            EnsureFolder("Assets/_Project/Bao/Fluid2D/Presets");
            EnsureFolder("Assets/_Project/Bao/Fluid2D/Prefabs");

            var settings = AssetDatabase.LoadAssetAtPath<Fluid2DSettings>(SettingsPath);
            var metaball = AssetDatabase.LoadAssetAtPath<Material>(MetaballMatPath);
            var splat = AssetDatabase.LoadAssetAtPath<Material>(SplatMatPath);
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);

            var iceProfile = EnsureStarterIceProfile();

            EnsurePreset(PresetMapPath, "Map_IceandSnow V3 glacial water", settings, metaball, splat,
                Fluid2DQualityLevel.Balanced, IceWaterPhaseToggle2D.Phase.Ice, iceProfile);
            EnsurePreset(PresetStarterPath, "Neutral starter preset for custom regions", settings, metaball, splat,
                Fluid2DQualityLevel.Balanced, IceWaterPhaseToggle2D.Phase.Ice, iceProfile);

            EnsurePrefab(settings, metaball, splat, compute);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static GameObject BuildRegionHierarchy(
            string name,
            SpriteShapeController existingIce,
            bool withEmitter,
            bool withDrain)
        {
            var root = new GameObject(name);
            var controller = root.AddComponent<Fluid2DEnvironmentController>();

            var settings = AssetDatabase.LoadAssetAtPath<Fluid2DSettings>(SettingsPath);
            var metaball = AssetDatabase.LoadAssetAtPath<Material>(MetaballMatPath);
            var splat = AssetDatabase.LoadAssetAtPath<Material>(SplatMatPath);
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);

            // Fluid system child — keep inactive until refs are assigned to avoid OnEnable warnings.
            var fluidGo = new GameObject("FluidSystem");
            fluidGo.transform.SetParent(root.transform, false);
            fluidGo.SetActive(false);
            var system = fluidGo.AddComponent<Fluid2DSystem>();
            var renderer = fluidGo.AddComponent<Fluid2DMetaballRenderer>();
            var obstacles = fluidGo.AddComponent<Fluid2DObstacleManager>();
            system.obstacleManager = obstacles;
            obstacles.fluidSystem = system;
            system.settings = settings;
            system.particleMaterial = splat;
            system.simulationShader = compute;
            renderer.metaballMaterial = metaball;
            // Particles draw on the dedicated Fluid2D layer; the fluid camera sees only that layer.
            fluidGo.layer = 16;
            renderer.waterLayer = 16;
            fluidGo.SetActive(true);

            GameObject iceGo;
            SpriteShapeController iceCtrl;
            if (existingIce != null)
            {
                iceGo = existingIce.gameObject;
                iceCtrl = existingIce;
            }
            else
            {
                iceGo = new GameObject("IceShape");
                iceGo.transform.SetParent(root.transform, false);
                iceCtrl = iceGo.AddComponent<SpriteShapeController>();
                if (iceGo.GetComponent<SpriteShapeRenderer>() == null)
                    iceGo.AddComponent<SpriteShapeRenderer>();
                ApplyStarterSpline(iceCtrl, iceGo);
            }

            var phase = iceGo.GetComponent<IceWaterPhaseToggle2D>();
            if (phase == null)
                phase = iceGo.AddComponent<IceWaterPhaseToggle2D>();
            phase.fluidSystem = system;
            phase.spriteShapeController = iceCtrl;
            phase.spriteShapeRenderer = iceGo.GetComponent<SpriteShapeRenderer>();
            phase.iceCollider = iceGo.GetComponent<Collider2D>();
            phase.enableKeyboardToggle = false;
            phase.startAsIce = true;

            var iceProfile = AssetDatabase.LoadAssetAtPath<SpriteShape>(StarterIceProfilePath)
                             ?? EnsureStarterIceProfile();
            if (iceCtrl.spriteShape == null && iceProfile != null)
                iceCtrl.spriteShape = iceProfile;

            ConfigureIceRenderer(iceGo.GetComponent<SpriteShapeRenderer>(), iceCtrl);

            if (withEmitter)
            {
                var eGo = new GameObject("Emitter");
                eGo.transform.SetParent(root.transform, false);
                eGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                var em = eGo.AddComponent<FluidEmitter2D>();
                em.fluidSystem = system;
                em.emitOnStart = false;
            }

            if (withDrain)
            {
                var dGo = new GameObject("Drain");
                dGo.transform.SetParent(root.transform, false);
                dGo.transform.localPosition = new Vector3(0f, -3f, 0f);
                var dr = dGo.AddComponent<FluidDrain2D>();
                dr.fluidSystem = system;
                var box = dGo.GetComponent<BoxCollider2D>();
                if (box != null) box.size = new Vector2(4f, 0.5f);
            }

            var so = new SerializedObject(controller);
            so.FindProperty("fluidSystem").objectReferenceValue = system;
            so.FindProperty("metaballRenderer").objectReferenceValue = renderer;
            so.FindProperty("obstacleManager").objectReferenceValue = obstacles;
            so.FindProperty("phaseToggle").objectReferenceValue = phase;
            so.FindProperty("iceSpriteShapeController").objectReferenceValue = iceCtrl;
            so.FindProperty("iceSpriteShapeRenderer").objectReferenceValue = iceGo.GetComponent<SpriteShapeRenderer>();
            so.FindProperty("enableKeyboardToggle").boolValue = false;
            so.FindProperty("quality").enumValueIndex = (int)Fluid2DQualityLevel.Balanced;
            so.ApplyModifiedPropertiesWithoutUndo();

            controller.AutoWire(false);
            return root;
        }

        /// <summary>
        /// Ensure a child IceShape exists under the controller and serialized refs point at it
        /// (and its phase toggle), not an external map prop.
        /// </summary>
        public static SpriteShapeController EnsureAndWireChildIceShape(
            Fluid2DEnvironmentController controller,
            bool createIfMissing)
        {
            if (controller == null)
                return null;

            var iceCtrl = FindPreferredChildIce(controller.transform);
            if (iceCtrl == null && createIfMissing)
            {
                var iceGo = new GameObject("IceShape");
                Undo.RegisterCreatedObjectUndo(iceGo, "Create IceShape");
                iceGo.transform.SetParent(controller.transform, false);
                iceCtrl = iceGo.AddComponent<SpriteShapeController>();
                if (iceGo.GetComponent<SpriteShapeRenderer>() == null)
                    iceGo.AddComponent<SpriteShapeRenderer>();
                ApplyStarterSpline(iceCtrl, iceGo);

                var phaseNew = iceGo.AddComponent<IceWaterPhaseToggle2D>();
                phaseNew.enableKeyboardToggle = false;
                phaseNew.startAsIce = true;
                phaseNew.spriteShapeController = iceCtrl;
                phaseNew.spriteShapeRenderer = iceGo.GetComponent<SpriteShapeRenderer>();
                phaseNew.iceCollider = iceGo.GetComponent<Collider2D>();
                phaseNew.fluidSystem = controller.FluidSystem
                                       ?? controller.GetComponentInChildren<Fluid2DSystem>(true);

                var profile = AssetDatabase.LoadAssetAtPath<SpriteShape>(StarterIceProfilePath)
                              ?? EnsureStarterIceProfile();
                if (profile != null)
                    iceCtrl.spriteShape = profile;
            }

            if (iceCtrl == null)
                return null;

            var iceGoFinal = iceCtrl.gameObject;
            var ssr = iceGoFinal.GetComponent<SpriteShapeRenderer>();
            var phase = iceGoFinal.GetComponent<IceWaterPhaseToggle2D>();
            if (phase == null)
                phase = Undo.AddComponent<IceWaterPhaseToggle2D>(iceGoFinal);

            phase.spriteShapeController = iceCtrl;
            phase.spriteShapeRenderer = ssr;
            phase.iceCollider = iceGoFinal.GetComponent<Collider2D>();
            if (phase.fluidSystem == null)
                phase.fluidSystem = controller.FluidSystem
                                    ?? controller.GetComponentInChildren<Fluid2DSystem>(true);
            phase.enableKeyboardToggle = false;

            if (iceCtrl.spriteShape == null)
            {
                var profile = AssetDatabase.LoadAssetAtPath<SpriteShape>(StarterIceProfilePath)
                              ?? EnsureStarterIceProfile();
                if (profile != null)
                    iceCtrl.spriteShape = profile;
            }

            // Prefer child spline if empty / invalid default.
            if (iceCtrl.spline == null || iceCtrl.spline.GetPointCount() < 3)
                ApplyStarterSpline(iceCtrl, iceGoFinal);

            ConfigureIceRenderer(ssr, iceCtrl);

            var so = new SerializedObject(controller);
            so.FindProperty("iceSpriteShapeController").objectReferenceValue = iceCtrl;
            so.FindProperty("iceSpriteShapeRenderer").objectReferenceValue = ssr;
            so.FindProperty("phaseToggle").objectReferenceValue = phase;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(iceCtrl);
            if (ssr != null) EditorUtility.SetDirty(ssr);
            EditorUtility.SetDirty(phase);
            return iceCtrl;
        }

        public static void FinalizeStarterIceGeometry(Fluid2DEnvironmentController controller)
        {
            if (controller == null)
                return;

            var iceCtrl = controller.IceSpriteShapeController
                          ?? FindPreferredChildIce(controller.transform);
            if (iceCtrl == null)
                return;

            // Only rewrite spline for our starter IceShape (not authored map ice).
            bool isStarterChild = iceCtrl.transform.IsChildOf(controller.transform)
                                  && (iceCtrl.name == "IceShape" || iceCtrl.spline.GetPointCount() < 3);
            if (isStarterChild && iceCtrl.spline.GetPointCount() < 3)
                ApplyStarterSpline(iceCtrl, iceCtrl.gameObject);

            ConfigureIceRenderer(iceCtrl.GetComponent<SpriteShapeRenderer>(), iceCtrl);
        }

        public static void PlaceRegionInCameraView(GameObject root)
        {
            if (root == null)
                return;

            // Keep local hierarchy at origin; place root where a 2D camera is looking when far away.
            var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                root.transform.position = Vector3.zero;
                return;
            }

            Vector3 p = root.transform.position;
            // If already near camera XY, leave it; otherwise center under the camera.
            Vector3 camFocus = cam.transform.position;
            float dist = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(camFocus.x, camFocus.y));
            float viewRadius = cam.orthographic ? cam.orthographicSize * 2.5f : 12f;
            if (dist > viewRadius || Mathf.Abs(p.z - 0f) > 5f)
            {
                root.transform.position = new Vector3(camFocus.x, camFocus.y, 0f);
            }
            else if (Mathf.Abs(p.z) > 0.01f)
            {
                root.transform.position = new Vector3(p.x, p.y, 0f);
            }
        }

        public static void EnsureMainCamera2DFraming(Fluid2DEnvironmentController controller, Vector3 focusWorld)
        {
            Camera cam = null;
            if (controller != null && controller.MainCamera != null)
                cam = controller.MainCamera;
            if (cam == null)
                cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (cam == null)
                return;

            Undo.RecordObject(cam, "Fluid2D 2D Camera Framing");
            Undo.RecordObject(cam.transform, "Fluid2D 2D Camera Framing");

            if (!cam.orthographic)
            {
                cam.orthographic = true;
                Debug.Log($"Fluid2D: switched camera '{cam.name}' from perspective to orthographic for 2D fluid framing.", cam);
            }

            if (cam.orthographicSize < 1f || cam.orthographicSize > 40f)
                cam.orthographicSize = 6f;
            else if (cam.orthographicSize < 3f)
                cam.orthographicSize = 5f;

            // Face -Z (standard 2D), keep Z distance comfortable for overlays.
            var t = cam.transform;
            t.rotation = Quaternion.identity;
            t.position = new Vector3(focusWorld.x, focusWorld.y, -10f);

            if (controller != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("mainCamera").objectReferenceValue = cam;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (controller.MetaballRenderer != null)
                {
                    Undo.RecordObject(controller.MetaballRenderer, "Assign Main Camera");
                    controller.MetaballRenderer.mainCamera = cam;
                    EditorUtility.SetDirty(controller.MetaballRenderer);
                }
            }

            EditorUtility.SetDirty(cam);
        }

        public static SpriteShapeController FindPreferredChildIce(Transform root)
        {
            if (root == null)
                return null;

            // Prefer explicit IceShape name.
            var all = root.GetComponentsInChildren<SpriteShapeController>(true);
            SpriteShapeController named = null;
            SpriteShapeController withPhase = null;
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                if (s.name == "IceShape")
                    named = s;
                if (s.GetComponent<IceWaterPhaseToggle2D>() != null && withPhase == null)
                    withPhase = s;
            }
            return named ?? withPhase ?? (all.Length > 0 ? all[0] : null);
        }

        private static void ApplyStarterSpline(SpriteShapeController iceCtrl, GameObject iceGo)
        {
            if (iceCtrl == null || iceGo == null)
                return;

            var spline = iceCtrl.spline;
            spline.Clear();
            for (int i = 0; i < StarterSpline.Length; i++)
                spline.InsertPointAt(i, StarterSpline[i]);
            iceCtrl.spline.isOpenEnded = false;
            iceCtrl.boundsScale = 1f;

            var poly = iceGo.GetComponent<PolygonCollider2D>();
            if (poly == null)
                poly = iceGo.AddComponent<PolygonCollider2D>();
            poly.points = new[]
            {
                new Vector2(-2f, -0.5f), new Vector2(2f, -0.5f),
                new Vector2(2f, 0.5f), new Vector2(-2f, 0.5f)
            };
            EditorUtility.SetDirty(iceCtrl);
            EditorUtility.SetDirty(poly);
        }

        private static void ConfigureIceRenderer(SpriteShapeRenderer ssr, SpriteShapeController iceCtrl)
        {
            if (ssr == null)
                return;

            // Visible ice tint for starter regions (profile may be fill-only).
            if (ssr.color.a < 0.5f || ssr.color.maxColorComponent < 0.05f)
                ssr.color = StarterIceTint;
            else if (ssr.color.a < 0.99f)
            {
                var c = ssr.color;
                c.a = 1f;
                ssr.color = c;
            }

            // Finite local AABB from actual spline (fallback to starter box) — avoid default ~2^20 extents.
            Bounds aabb;
            if (iceCtrl != null && iceCtrl.spline != null && iceCtrl.spline.GetPointCount() > 0)
            {
                aabb = new Bounds(iceCtrl.spline.GetPosition(0), Vector3.zero);
                for (int i = 1; i < iceCtrl.spline.GetPointCount(); i++)
                    aabb.Encapsulate(iceCtrl.spline.GetPosition(i));
            }
            else
            {
                aabb = new Bounds(StarterSpline[0], Vector3.zero);
                for (int i = 1; i < StarterSpline.Length; i++)
                    aabb.Encapsulate(StarterSpline[i]);
            }
            aabb.Expand(0.15f);
            var size = aabb.size;
            size.z = 0f;
            aabb.size = size;
            ssr.SetLocalAABB(aabb);

            if (iceCtrl != null)
            {
                iceCtrl.boundsScale = 1f;
                if (iceCtrl.spriteShape == null)
                {
                    var profile = AssetDatabase.LoadAssetAtPath<SpriteShape>(StarterIceProfilePath)
                                  ?? EnsureStarterIceProfile();
                    if (profile != null)
                        iceCtrl.spriteShape = profile;
                }
                iceCtrl.RefreshSpriteShape();
                EditorUtility.SetDirty(iceCtrl);
            }

            EditorUtility.SetDirty(ssr);
        }

        private static void EnsureComputeOnController(Fluid2DEnvironmentController controller)
        {
            if (controller == null || controller.FluidSystem == null)
                return;
            if (controller.FluidSystem.simulationShader != null)
                return;
            var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            if (cs == null) return;
            Undo.RecordObject(controller.FluidSystem, "Assign Compute Shader");
            controller.FluidSystem.simulationShader = cs;
            EditorUtility.SetDirty(controller.FluidSystem);
        }

        private static SpriteShape EnsureStarterIceProfile()
        {
            EnsureFolder("Assets/_Project/Bao/Fluid2D/Presets");
            var fill = EnsureStarterFillTexture();
            var profile = AssetDatabase.LoadAssetAtPath<SpriteShape>(StarterIceProfilePath);
            if (profile == null)
            {
                try
                {
                    profile = ScriptableObject.CreateInstance<SpriteShape>();
                    profile.name = "Fluid2D_IceProfile_Starter";
                    profile.fillOffset = 0f;
                    profile.useSpriteBorders = false;
                    if (fill != null)
                        profile.fillTexture = fill;
                    AssetDatabase.CreateAsset(profile, StarterIceProfilePath);
                    AssetDatabase.SaveAssets();
                    Debug.Log("Created starter ice SpriteShape profile at " + StarterIceProfilePath);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Failed to create starter SpriteShape profile: " + ex.Message);
                    return null;
                }
            }
            else
            {
                profile.fillOffset = 0f;
                profile.useSpriteBorders = false;
                if (profile.fillTexture == null && fill != null)
                    profile.fillTexture = fill;
                EditorUtility.SetDirty(profile);
            }
            return profile;
        }

        private static Texture2D EnsureStarterFillTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(StarterIceFillTexPath);
            if (existing != null)
                return existing;

            EnsureFolder("Assets/_Project/Bao/Fluid2D/Presets");
            const int size = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var ice = new Color(0.82f, 0.94f, 1f, 1f);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = ice;
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            File.WriteAllBytes(StarterIceFillTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(StarterIceFillTexPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(StarterIceFillTexPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(StarterIceFillTexPath);
        }

        private static void EnsurePreset(
            string path,
            string notes,
            Fluid2DSettings settings,
            Material metaball,
            Material splat,
            Fluid2DQualityLevel quality,
            IceWaterPhaseToggle2D.Phase phase,
            SpriteShape iceProfile)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<Fluid2DEnvironmentPreset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.settings = settings;
            asset.waterMetaballMaterial = metaball;
            asset.splatMaterial = splat;
            asset.iceSpriteShapeProfile = iceProfile;
            asset.quality = quality;
            asset.initialPhase = phase;
            asset.waterLayer = 16;
            asset.particleSpacing = 0.16f;
            asset.maxParticlesPerToggle = 2000;
            asset.disableAdaptiveMeltSpacing = false;
            asset.particlesPerUnitArea = 0f;
            asset.logMeltDiagnostics = true;
            asset.designerNotes = notes;
            asset.terrainObstacleLayers = ~0;
            EditorUtility.SetDirty(asset);
        }

        private static void EnsurePrefab(
            Fluid2DSettings settings,
            Material metaball,
            Material splat,
            ComputeShader compute)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                using (var edit = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
                {
                    var root = edit.prefabContentsRoot;
                    var system = root.GetComponentInChildren<Fluid2DSystem>(true);
                    var renderer = root.GetComponentInChildren<Fluid2DMetaballRenderer>(true);
                    if (system != null)
                    {
                        system.settings = settings;
                        system.particleMaterial = splat;
                        system.simulationShader = compute;
                    }
                    if (renderer != null)
                        renderer.metaballMaterial = metaball;

                    var controller = root.GetComponent<Fluid2DEnvironmentController>()
                                     ?? root.GetComponentInChildren<Fluid2DEnvironmentController>(true);
                    if (controller != null)
                    {
                        EnsureAndWireChildIceShape(controller, createIfMissing: true);
                        FinalizeStarterIceGeometry(controller);
                        controller.AutoWire(false);
                        var starterPreset = AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(PresetStarterPath);
                        if (starterPreset != null)
                            controller.ApplyPreset(starterPreset);
                    }
                }
                return;
            }

            var temp = BuildRegionHierarchy("InteractiveWater2D", null, false, false);
            var ctrl = temp.GetComponent<Fluid2DEnvironmentController>();
            var starter = AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(PresetStarterPath);
            if (ctrl != null && starter != null)
                ctrl.ApplyPreset(starter);
            FinalizeStarterIceGeometry(ctrl);
            PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
