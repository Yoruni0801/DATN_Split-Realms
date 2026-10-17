#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using Bao.Fluid2D;

namespace Bao.Fluid2D.Editor
{
    [CustomEditor(typeof(Fluid2DEnvironmentController))]
    public class Fluid2DEnvironmentControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty preset;
        private SerializedProperty fluidSystem;
        private SerializedProperty metaballRenderer;
        private SerializedProperty obstacleManager;
        private SerializedProperty phaseToggle;
        private SerializedProperty iceSpriteShapeController;
        private SerializedProperty iceSpriteShapeRenderer;
        private SerializedProperty emitter;
        private SerializedProperty drain;
        private SerializedProperty mainCamera;
        private SerializedProperty settingsOverride;
        private SerializedProperty waterMaterialOverride;
        private SerializedProperty splatMaterialOverride;
        private SerializedProperty iceProfileOverride;
        private SerializedProperty initialPhase;
        private SerializedProperty quality;
        private SerializedProperty terrainObstacleLayers;
        private SerializedProperty dynamicInteractionLayers;
        private SerializedProperty waterLayer;
        private SerializedProperty enableKeyboardToggle;
        private SerializedProperty toggleKey;
        private SerializedProperty onPhaseChanging;
        private SerializedProperty onPhaseChanged;
        private SerializedProperty onEnteredWater;
        private SerializedProperty onEnteredIce;
        private SerializedProperty lastValidationSummary;

        private bool showAdvancedRefs;
        private bool showEvents = true;
        private bool showDiagnostics = true;

        private void OnEnable()
        {
            preset = serializedObject.FindProperty("preset");
            fluidSystem = serializedObject.FindProperty("fluidSystem");
            metaballRenderer = serializedObject.FindProperty("metaballRenderer");
            obstacleManager = serializedObject.FindProperty("obstacleManager");
            phaseToggle = serializedObject.FindProperty("phaseToggle");
            iceSpriteShapeController = serializedObject.FindProperty("iceSpriteShapeController");
            iceSpriteShapeRenderer = serializedObject.FindProperty("iceSpriteShapeRenderer");
            emitter = serializedObject.FindProperty("emitter");
            drain = serializedObject.FindProperty("drain");
            mainCamera = serializedObject.FindProperty("mainCamera");
            settingsOverride = serializedObject.FindProperty("settingsOverride");
            waterMaterialOverride = serializedObject.FindProperty("waterMaterialOverride");
            splatMaterialOverride = serializedObject.FindProperty("splatMaterialOverride");
            iceProfileOverride = serializedObject.FindProperty("iceProfileOverride");
            initialPhase = serializedObject.FindProperty("initialPhase");
            quality = serializedObject.FindProperty("quality");
            terrainObstacleLayers = serializedObject.FindProperty("terrainObstacleLayers");
            dynamicInteractionLayers = serializedObject.FindProperty("dynamicInteractionLayers");
            waterLayer = serializedObject.FindProperty("waterLayer");
            enableKeyboardToggle = serializedObject.FindProperty("enableKeyboardToggle");
            toggleKey = serializedObject.FindProperty("toggleKey");
            onPhaseChanging = serializedObject.FindProperty("onPhaseChanging");
            onPhaseChanged = serializedObject.FindProperty("onPhaseChanged");
            onEnteredWater = serializedObject.FindProperty("onEnteredWater");
            onEnteredIce = serializedObject.FindProperty("onEnteredIce");
            lastValidationSummary = serializedObject.FindProperty("lastValidationSummary");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var controller = (Fluid2DEnvironmentController)target;

            EditorGUILayout.LabelField("Fluid2D Environment", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Production facade for one interactive ice/water region. " +
                "Use Auto Wire + Validate. Prefer Melt/Freeze API or UnityEvents — keyboard is off by default. " +
                "One Fluid2D GPU system per camera.",
                MessageType.Info);

            // ── Setup ──
            DrawSectionHeader("Setup");
            EditorGUILayout.PropertyField(preset, new GUIContent("Preset"));
            EditorGUILayout.PropertyField(initialPhase);
            EditorGUILayout.PropertyField(mainCamera, new GUIContent("Main Camera (auto if empty)"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto Wire", GUILayout.Height(26)))
                {
                    Undo.RecordObject(controller, "Auto Wire Fluid2D");
                    controller.AutoWire(true);
                    EditorUtility.SetDirty(controller);
                }
                if (GUILayout.Button("Bind Child Ice", GUILayout.Height(26)))
                {
                    Undo.RecordObject(controller, "Bind Child Ice Fluid2D");
                    controller.TryBindIceFromHierarchy(forceIfExternal: true);
                    Fluid2DEnvironmentAssetUtility.EnsureAndWireChildIceShape(controller, createIfMissing: true);
                    controller.TryBindIceFromHierarchy(forceIfExternal: true);
                    Fluid2DEnvironmentAssetUtility.FinalizeStarterIceGeometry(controller);
                    controller.AutoWire(false);
                    controller.ValidateSetup();
                    EditorUtility.SetDirty(controller);
                }
                if (GUILayout.Button("Validate Setup", GUILayout.Height(26)))
                {
                    controller.ValidateSetup();
                    EditorUtility.SetDirty(controller);
                    Debug.Log(controller.FormatValidationReport(), controller);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = preset.objectReferenceValue != null;
                if (GUILayout.Button("Apply Preset", GUILayout.Height(24)))
                {
                    Undo.RecordObject(controller, "Apply Fluid2D Preset");
                    if (controller.FluidSystem != null) Undo.RecordObject(controller.FluidSystem, "Apply Preset System");
                    if (controller.MetaballRenderer != null) Undo.RecordObject(controller.MetaballRenderer, "Apply Preset Renderer");
                    if (controller.PhaseToggle != null) Undo.RecordObject(controller.PhaseToggle, "Apply Preset Phase");
                    if (controller.IceSpriteShapeController != null) Undo.RecordObject(controller.IceSpriteShapeController, "Apply Preset Ice");
                    controller.ApplyPreset(preset.objectReferenceValue as Fluid2DEnvironmentPreset);
                    EditorUtility.SetDirty(controller);
                }
                GUI.enabled = true;

                if (GUILayout.Button("Refresh Obstacles", GUILayout.Height(24)))
                {
                    controller.RefreshObstacles();
                }
            }

            // ── Look ──
            DrawSectionHeader("Look");
            EditorGUILayout.PropertyField(iceProfileOverride, new GUIContent("Ice SpriteShape Profile"));
            EditorGUILayout.PropertyField(waterMaterialOverride, new GUIContent("Water Metaball Material"));
            EditorGUILayout.PropertyField(splatMaterialOverride, new GUIContent("Splat Material"));
            EditorGUILayout.PropertyField(settingsOverride, new GUIContent("Fluid2D Settings"));

            // ── Interaction ──
            DrawSectionHeader("Interaction");
            EditorGUILayout.PropertyField(terrainObstacleLayers, new GUIContent("Terrain / Ice Layers"));
            EditorGUILayout.PropertyField(dynamicInteractionLayers, new GUIContent("Dynamic Layers (doc)"));
            EditorGUILayout.PropertyField(waterLayer, new GUIContent("Water Particle Layer"));
            EditorGUILayout.PropertyField(enableKeyboardToggle, new GUIContent("Enable Keyboard Toggle"));
            using (new EditorGUI.DisabledScope(!enableKeyboardToggle.boolValue))
            {
                EditorGUILayout.PropertyField(toggleKey, new GUIContent("Toggle Key"));
            }

            if (Application.isPlaying)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Melt", GUILayout.Height(28)))
                        controller.Melt();
                    if (GUILayout.Button("Freeze", GUILayout.Height(28)))
                        controller.Freeze();
                    if (GUILayout.Button("Toggle", GUILayout.Height(28)))
                        controller.TogglePhase();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Melt / Freeze / Toggle buttons appear in Play Mode.", MessageType.None);
            }

            // ── Quality ──
            DrawSectionHeader("Quality");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(quality);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                controller.SetQuality((Fluid2DQualityLevel)quality.enumValueIndex);
            }
            else
            {
                var profile = Fluid2DQualityProfile.Get((Fluid2DQualityLevel)quality.enumValueIndex);
                EditorGUILayout.LabelField("RT Scale", profile.ResolutionScale.ToString("0.00"));
                EditorGUILayout.LabelField("Substeps (runtime override)", profile.Substeps.ToString());
            }

            // ── Events ──
            showEvents = EditorGUILayout.Foldout(showEvents, "Events", true);
            if (showEvents)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(onPhaseChanging);
                EditorGUILayout.PropertyField(onPhaseChanged);
                EditorGUILayout.PropertyField(onEnteredWater);
                EditorGUILayout.PropertyField(onEnteredIce);
                EditorGUI.indentLevel--;
            }

            // ── Diagnostics ──
            showDiagnostics = EditorGUILayout.Foldout(showDiagnostics, "Diagnostics", true);
            if (showDiagnostics)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Validation", lastValidationSummary.stringValue);
                EditorGUILayout.LabelField("Phase", controller.CurrentPhase.ToString());
                EditorGUILayout.LabelField("Active Particles", controller.ActiveParticleCount.ToString());
                EditorGUILayout.LabelField("Conserved Mass",
                    controller.HasConservedMass ? controller.ConservedMass.ToString() : "(not established)");
                EditorGUILayout.LabelField("Material Applies", controller.MaterialPropertyApplyCount.ToString());
                EditorGUILayout.LabelField("RT Recreations", controller.RenderTextureRecreationCount.ToString());
                EditorGUILayout.LabelField("RT Size", $"{controller.CurrentRtWidth}x{controller.CurrentRtHeight}");
                EditorGUILayout.LabelField("Drain Registry", controller.DrainRegistryCount.ToString());
                EditorGUILayout.LabelField("Drain Full Scans", controller.DrainFullScanCount.ToString());
                EditorGUILayout.LabelField("Obstacle Refresh #", controller.ObstacleRefreshCount.ToString());

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = controller.FluidSystem != null;
                    if (GUILayout.Button("Select Fluid System"))
                        Selection.activeObject = controller.FluidSystem;
                    GUI.enabled = controller.MetaballRenderer != null;
                    if (GUILayout.Button("Select Metaball"))
                        Selection.activeObject = controller.MetaballRenderer;
                    GUI.enabled = true;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = controller.PhaseToggle != null;
                    if (GUILayout.Button("Select Phase Toggle"))
                        Selection.activeObject = controller.PhaseToggle;
                    GUI.enabled = controller.IceSpriteShapeController != null;
                    if (GUILayout.Button("Select Ice Shape"))
                        Selection.activeObject = controller.IceSpriteShapeController;
                    GUI.enabled = true;
                }

                if (controller.MetaballRenderer != null && controller.MetaballRenderer.FluidCamera != null)
                {
                    if (GUILayout.Button("Select Runtime Fluid Camera"))
                        Selection.activeObject = controller.MetaballRenderer.FluidCamera.gameObject;
                }
                EditorGUI.indentLevel--;
            }

            // ── Advanced refs ──
            showAdvancedRefs = EditorGUILayout.Foldout(showAdvancedRefs, "Advanced References", true);
            if (showAdvancedRefs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(fluidSystem);
                EditorGUILayout.PropertyField(metaballRenderer);
                EditorGUILayout.PropertyField(obstacleManager);
                EditorGUILayout.PropertyField(phaseToggle);
                EditorGUILayout.PropertyField(iceSpriteShapeController);
                EditorGUILayout.PropertyField(iceSpriteShapeRenderer);
                EditorGUILayout.PropertyField(emitter);
                EditorGUILayout.PropertyField(drain);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
#endif
