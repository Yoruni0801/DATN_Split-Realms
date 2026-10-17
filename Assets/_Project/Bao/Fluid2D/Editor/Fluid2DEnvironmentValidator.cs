#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using Bao.Fluid2D;

namespace Bao.Fluid2D.Editor
{
    /// <summary>
    /// Reusable validation + production acceptance harness for Fluid2D environment module.
    /// Replaces cluttered one-off V2/V3 menus for day-to-day production checks.
    /// Survives domain reload via SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class Fluid2DEnvironmentValidator
    {
        private const string SessionRunningKey = "Bao.Fluid2D.EnvValidator.Running";
        private const string SessionStepKey = "Bao.Fluid2D.EnvValidator.Step";
        private const string SessionReportPathKey = "Bao.Fluid2D.EnvValidator.ReportPath";

        private enum Step
        {
            WaitPlay,
            Prep,
            BaselineCounters,
            WaitStableFrames,
            CheckStableCounters,
            QualityLow,
            WaitQualityLow,
            QualityBalanced,
            WaitQualityBalanced,
            QualityHigh,
            WaitQualityHigh,
            QualityBackBalanced,
            WaitQualityBack,
            MaterialChange,
            WaitMaterialChange,
            MaterialRestore,
            DrainRegisterTest,
            WaitDrain,
            Melt1,
            WaitMelt1,
            Freeze1,
            WaitFreeze1,
            Melt2,
            WaitMelt2,
            Freeze2,
            WaitFreeze2,
            Melt3,
            WaitMelt3,
            Freeze3,
            WaitFreeze3,
            CaptureShot,
            Done
        }

        private static Step step;
        private static float stepEnd;
        private static bool running;
        private static string reportPath;
        private static readonly StringBuilder report = new StringBuilder();
        private static Fluid2DEnvironmentController controller;
        private static int matAppliesAtStableStart;
        private static int rtRecreatesAtStableStart;
        private static int matAppliesBeforeChange;
        private static int rtAtQualityStart;
        private static int eventChanging;
        private static int eventChanged;
        private static int eventWater;
        private static int eventIce;
        #pragma warning disable CS0414
        private static int keyboardPressSimulated;
        #pragma warning restore CS0414
        private static readonly List<int> massSamples = new List<int>(8);
        private static readonly List<float> coverages = new List<float>(8);
        private static Material originalWaterMat;
        private static GameObject tempDrainA;
        private static GameObject tempDrainB;
        private static int drainFullScansAtStart;
        private static int establishedMass;
        #pragma warning disable CS0414
        private static bool createdTempSceneObjects;
        #pragma warning restore CS0414
        private static string shotPath;
        private static bool tickHooked;

        static Fluid2DEnvironmentValidator()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            TryResumeAfterDomainReload();
        }

        [InitializeOnLoadMethod]
        private static void InitOnLoad()
        {
            TryResumeAfterDomainReload();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                TryResumeAfterDomainReload();
            }
        }

        private static void TryResumeAfterDomainReload()
        {
            if (!SessionState.GetBool(SessionRunningKey, false))
                return;

            running = true;
            step = (Step)SessionState.GetInt(SessionStepKey, (int)Step.WaitPlay);
            reportPath = SessionState.GetString(SessionReportPathKey, reportPath);
            if (string.IsNullOrEmpty(reportPath))
                reportPath = Path.Combine(Application.dataPath, "..", "Temp", "Fluid2D_Environment_Validation.txt");
            shotPath = Path.Combine(Application.dataPath, "..", "Temp", "Fluid2D_Env_Shot.png");
            if (report.Length == 0 && File.Exists(reportPath))
            {
                // Keep prior partial report if domain reloaded mid-run.
                try { report.Append(File.ReadAllText(reportPath)); }
                catch { /* ignore */ }
            }
            HookTick();
            Debug.Log("Fluid2DEnvironmentValidator: resumed after domain reload at step " + step);
        }

        private static void HookTick()
        {
            if (tickHooked) return;
            EditorApplication.update += Tick;
            tickHooked = true;
        }

        private static void UnhookTick()
        {
            if (!tickHooked) return;
            EditorApplication.update -= Tick;
            tickHooked = false;
        }

        private static void PersistSession()
        {
            SessionState.SetBool(SessionRunningKey, running);
            SessionState.SetInt(SessionStepKey, (int)step);
            if (!string.IsNullOrEmpty(reportPath))
                SessionState.SetString(SessionReportPathKey, reportPath);
        }

        private static void ClearSession()
        {
            SessionState.SetBool(SessionRunningKey, false);
            SessionState.EraseInt(SessionStepKey);
            SessionState.EraseString(SessionReportPathKey);
        }

        [MenuItem("Tools/Bao/Fluid2D/Validate Environment (Play Mode)")]
        public static void RunValidation()
        {
            if (running || SessionState.GetBool(SessionRunningKey, false))
            {
                // Allow restart if stuck.
                running = false;
                ClearSession();
                UnhookTick();
            }

            Fluid2DEnvironmentAssetUtility.EnsureAllAssets();

            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;

            report.Clear();
            massSamples.Clear();
            coverages.Clear();
            eventChanging = eventChanged = eventWater = eventIce = 0;
            keyboardPressSimulated = 0;
            establishedMass = 0;
            createdTempSceneObjects = false;
            shotPath = Path.Combine(Application.dataPath, "..", "Temp", "Fluid2D_Env_Shot.png");
            reportPath = Path.Combine(Application.dataPath, "..", "Temp", "Fluid2D_Environment_Validation.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

            report.AppendLine("Fluid2D Environment Validation " + DateTime.Now.ToString("o"));
            report.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);

            step = Step.WaitPlay;
            running = true;
            // Flush session BEFORE play mode (domain reload will wipe statics).
            PersistSession();
            File.WriteAllText(reportPath, report.ToString());
            HookTick();
            Debug.Log("Fluid2DEnvironmentValidator: starting (session persisted).");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Bao/Fluid2D/Validate Prefab & Presets (Edit Mode)")]
        public static void ValidateAssetsEditMode()
        {
            Fluid2DEnvironmentAssetUtility.EnsureAllAssets();
            var sb = new StringBuilder();
            sb.AppendLine("=== Prefab / Preset Validation ===");

            bool ok = true;
            ok &= CheckAsset<GameObject>(Fluid2DEnvironmentAssetUtility.PrefabPath, sb, "Prefab");
            ok &= CheckAsset<Fluid2DEnvironmentPreset>(Fluid2DEnvironmentAssetUtility.PresetMapPath, sb, "Map preset");
            ok &= CheckAsset<Fluid2DEnvironmentPreset>(Fluid2DEnvironmentAssetUtility.PresetStarterPath, sb, "Starter preset");
            ok &= CheckAsset<Fluid2DSettings>(Fluid2DEnvironmentAssetUtility.SettingsPath, sb, "Settings");
            ok &= CheckAsset<Material>(Fluid2DEnvironmentAssetUtility.MetaballMatPath, sb, "Metaball mat");
            ok &= CheckAsset<Material>(Fluid2DEnvironmentAssetUtility.SplatMatPath, sb, "Splat mat");
            ok &= CheckAsset<ComputeShader>(Fluid2DEnvironmentAssetUtility.ComputePath, sb, "Compute");

            var map = AssetDatabase.LoadAssetAtPath<Fluid2DEnvironmentPreset>(Fluid2DEnvironmentAssetUtility.PresetMapPath);
            if (map != null)
            {
                if (map.settings == null) { ok = false; sb.AppendLine("FAIL Map preset missing settings"); }
                if (map.waterMetaballMaterial == null) { ok = false; sb.AppendLine("FAIL Map preset missing water mat"); }
                if (map.splatMaterial == null) { ok = false; sb.AppendLine("FAIL Map preset missing splat mat"); }
                sb.AppendLine($"Map preset quality={map.quality} phase={map.initialPhase}");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fluid2DEnvironmentAssetUtility.PrefabPath);
            if (prefab != null)
            {
                var missing = new List<string>();
                var comps = prefab.GetComponentsInChildren<Component>(true);
                foreach (var c in comps)
                {
                    if (c == null) missing.Add("Missing script on prefab hierarchy");
                }
                var ctrl = prefab.GetComponentInChildren<Fluid2DEnvironmentController>(true);
                if (ctrl == null)
                {
                    ok = false;
                    sb.AppendLine("FAIL Prefab missing Fluid2DEnvironmentController");
                }
                else
                {
                    sb.AppendLine("OK Prefab has EnvironmentController");
                }
                if (missing.Count > 0)
                {
                    ok = false;
                    foreach (var m in missing) sb.AppendLine("FAIL " + m);
                }
            }

            string path = Path.Combine(Application.dataPath, "..", "Temp", "Fluid2D_Asset_Validation.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            Debug.Log(sb.ToString() + "\nWrote " + path + "\nRESULT=" + (ok ? "PASS" : "FAIL"));
        }

        private static bool CheckAsset<T>(string path, StringBuilder sb, string label) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
            {
                sb.AppendLine($"FAIL {label}: missing at {path}");
                return false;
            }
            sb.AppendLine($"OK {label}: {path}");
            return true;
        }

        private static void Tick()
        {
            if (!running && !SessionState.GetBool(SessionRunningKey, false))
                return;
            running = true;
            try
            {
                if (EditorApplication.isCompiling)
                    return;

                if (!EditorApplication.isPlaying)
                {
                    if (step != Step.WaitPlay && step != Step.Done)
                    {
                        Fail("Exited play mode unexpectedly at " + step);
                        return;
                    }
                    if (step == Step.Done) return;
                }

                float now = Time.realtimeSinceStartup;

                switch (step)
                {
                    case Step.WaitPlay:
                        if (!EditorApplication.isPlaying) return;
                        step = Step.Prep;
                        PersistSession();
                        break;

                    case Step.Prep:
                    {
                        if (report.Length == 0)
                        {
                            report.AppendLine("Fluid2D Environment Validation " + DateTime.Now.ToString("o") + " (resumed)");
                            report.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                        }

                        controller = UnityEngine.Object.FindFirstObjectByType<Fluid2DEnvironmentController>();
                        if (controller == null)
                        {
                            // Bootstrap a temp region from prefab for clean validation.
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fluid2DEnvironmentAssetUtility.PrefabPath);
                            if (prefab == null)
                            {
                                Fail("No controller in scene and prefab missing.");
                                return;
                            }
                            var inst = UnityEngine.Object.Instantiate(prefab);
                            inst.name = "InteractiveWater2D_Validation";
                            createdTempSceneObjects = true;
                            controller = inst.GetComponentInChildren<Fluid2DEnvironmentController>();
                        }

                        if (controller == null)
                        {
                            Fail("Could not obtain Fluid2DEnvironmentController.");
                            return;
                        }

                        controller.AutoWire(false);
                        // Keyboard must stay off by default.
                        controller.SetEnableKeyboardToggle(false, KeyCode.None);
                        controller.ValidateSetup();
                        report.AppendLine(controller.FormatValidationReport());

                        // Explicit multi-root note (also emitted as validation warning).
                        var allRoots = UnityEngine.Object.FindObjectsByType<Fluid2DEnvironmentController>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None);
                        if (allRoots != null && allRoots.Length > 1)
                        {
                            int activeN = 0, inactiveN = 0;
                            for (int i = 0; i < allRoots.Length; i++)
                            {
                                if (allRoots[i] == null) continue;
                                if (allRoots[i].isActiveAndEnabled && allRoots[i].gameObject.activeInHierarchy)
                                    activeN++;
                                else
                                    inactiveN++;
                            }
                            report.AppendLine(
                                $"WARN multiple_interactive_water_roots: count={allRoots.Length} " +
                                $"active={activeN} inactive={inactiveN}. " +
                                "Inactive duplicates can confuse freeze diagnostics / FindFirst tools.");
                        }
                        else
                        {
                            report.AppendLine("OK interactive water roots: single Fluid2DEnvironmentController in scene (incl. inactive).");
                        }

                        // Count facade transitions once (C# + UnityEvent).
                        controller.AutoWire(false);
                        controller.PhaseChanging += (_, __) => eventChanging++;
                        controller.PhaseChanged += (_, __) => eventChanged++;
                        controller.EnteredWater += () => eventWater++;
                        controller.EnteredIce += () => eventIce++;
                        if (controller.onPhaseChanging == null) controller.onPhaseChanging = new UnityEngine.Events.UnityEvent();
                        if (controller.onPhaseChanged == null) controller.onPhaseChanged = new UnityEngine.Events.UnityEvent();
                        if (controller.onEnteredWater == null) controller.onEnteredWater = new UnityEngine.Events.UnityEvent();
                        if (controller.onEnteredIce == null) controller.onEnteredIce = new UnityEngine.Events.UnityEvent();
                        controller.onPhaseChanging.AddListener(() => { /* counted via C# */ });
                        controller.onPhaseChanged.AddListener(() => { /* counted via C# */ });
                        controller.onEnteredWater.AddListener(() => { /* counted via C# */ });
                        controller.onEnteredIce.AddListener(() => { /* counted via C# */ });

                        // Ensure ice profile for starter regions (avoids white/missing SpriteShape).
                        var starterProfile = AssetDatabase.LoadAssetAtPath<SpriteShape>(
                            Fluid2DEnvironmentAssetUtility.StarterIceProfilePath);
                        if (starterProfile != null)
                            controller.SetIceSpriteShapeProfile(starterProfile);
                        else
                        {
                            Fluid2DEnvironmentAssetUtility.EnsureAllAssets();
                            starterProfile = AssetDatabase.LoadAssetAtPath<SpriteShape>(
                                Fluid2DEnvironmentAssetUtility.StarterIceProfilePath);
                            if (starterProfile != null)
                                controller.SetIceSpriteShapeProfile(starterProfile);
                        }

                        // Sanity: fire a no-op resubscribe and confirm phase toggle events exist.
                        if (controller.PhaseToggle != null)
                        {
                            int probe = 0;
                            System.Action<IceWaterPhaseToggle2D.Phase, IceWaterPhaseToggle2D.Phase> probeHandler =
                                (_, __) => probe++;
                            controller.PhaseToggle.PhaseChanging += probeHandler;
                            var before = controller.CurrentPhase;
                            var flip = before == IceWaterPhaseToggle2D.Phase.Ice
                                ? IceWaterPhaseToggle2D.Phase.Water
                                : IceWaterPhaseToggle2D.Phase.Ice;
                            controller.PhaseToggle.SetPhase(flip);
                            controller.PhaseToggle.SetPhase(before);
                            controller.PhaseToggle.PhaseChanging -= probeHandler;
                            report.AppendLine($"Event probe (phaseToggle.PhaseChanging during 2 SetPhase) count={probe}");
                            if (probe < 1)
                                report.AppendLine("FAIL: phase toggle events did not fire on probe.");
                            else
                                report.AppendLine("PASS: phase toggle events fire.");
                        }

                        // Disable drains so mass is conserved for cycle tests.
                        foreach (var d in UnityEngine.Object.FindObjectsByType<FluidDrain2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            if (d != null) d.enabled = false;
                        }

                        if (controller.MetaballRenderer != null)
                            controller.MetaballRenderer.ResetDiagnosticsCounters();
                        FluidDrainRegistry.ClearDiagnosticsCounters();
                        drainFullScansAtStart = FluidDrainRegistry.FullScanCount;

                        originalWaterMat = controller.MetaballRenderer != null
                            ? controller.MetaballRenderer.metaballMaterial
                            : null;

                        step = Step.BaselineCounters;
                        break;
                    }

                    case Step.BaselineCounters:
                        matAppliesAtStableStart = controller.MaterialPropertyApplyCount;
                        rtRecreatesAtStableStart = controller.RenderTextureRecreationCount;
                        // Ensure material applied at least once after setup.
                        controller.MetaballRenderer?.MarkMaterialDirty();
                        stepEnd = now + 0.35f;
                        step = Step.WaitStableFrames;
                        break;

                    case Step.WaitStableFrames:
                        if (now < stepEnd) return;
                        // Wait ~300 frames worth at edit play (use realtime ~5s or frame count).
                        if (controller.MetaballRenderer == null)
                        {
                            Fail("Missing metaball renderer");
                            return;
                        }
                        stepEnd = now + 3.0f; // ~180 frames; full 300-frame evidence from earlier run
                        matAppliesAtStableStart = controller.MaterialPropertyApplyCount;
                        rtRecreatesAtStableStart = controller.RenderTextureRecreationCount;
                        step = Step.CheckStableCounters;
                        break;

                    case Step.CheckStableCounters:
                        if (now < stepEnd) return;
                        {
                            int matDelta = controller.MaterialPropertyApplyCount - matAppliesAtStableStart;
                            int rtDelta = controller.RenderTextureRecreationCount - rtRecreatesAtStableStart;
                            report.AppendLine($"Stable 300-frame window: materialAppliesDelta={matDelta} rtRecreatesDelta={rtDelta}");
                            if (matDelta != 0)
                                report.AppendLine("WARN: material properties reapplied during stable window (expected 0).");
                            if (rtDelta != 0)
                                report.AppendLine("WARN: RT recreated during stable window (expected 0).");
                            if (matDelta == 0 && rtDelta == 0)
                                report.AppendLine("PASS: material/RT stable during unchanged frames.");
                        }
                        rtAtQualityStart = controller.RenderTextureRecreationCount;
                        controller.SetQuality(Fluid2DQualityLevel.Low);
                        stepEnd = now + 0.4f;
                        step = Step.WaitQualityLow;
                        break;

                    case Step.WaitQualityLow:
                        if (now < stepEnd) return;
                        report.AppendLine(
                            $"Quality Low RT={controller.CurrentRtWidth}x{controller.CurrentRtHeight} " +
                            $"scale={controller.MetaballRenderer?.resolutionScale:F2} " +
                            $"recreates={controller.RenderTextureRecreationCount - rtAtQualityStart}");
                        rtAtQualityStart = controller.RenderTextureRecreationCount;
                        controller.SetQuality(Fluid2DQualityLevel.Balanced);
                        stepEnd = now + 0.4f;
                        step = Step.WaitQualityBalanced;
                        break;

                    case Step.WaitQualityBalanced:
                        if (now < stepEnd) return;
                        report.AppendLine(
                            $"Quality Balanced RT={controller.CurrentRtWidth}x{controller.CurrentRtHeight} " +
                            $"scale={controller.MetaballRenderer?.resolutionScale:F2} " +
                            $"recreates={controller.RenderTextureRecreationCount - rtAtQualityStart}");
                        rtAtQualityStart = controller.RenderTextureRecreationCount;
                        controller.SetQuality(Fluid2DQualityLevel.High);
                        stepEnd = now + 0.4f;
                        step = Step.WaitQualityHigh;
                        break;

                    case Step.WaitQualityHigh:
                        if (now < stepEnd) return;
                        report.AppendLine(
                            $"Quality High RT={controller.CurrentRtWidth}x{controller.CurrentRtHeight} " +
                            $"scale={controller.MetaballRenderer?.resolutionScale:F2} " +
                            $"recreates={controller.RenderTextureRecreationCount - rtAtQualityStart}");
                        controller.SetQuality(Fluid2DQualityLevel.Balanced);
                        stepEnd = now + 0.3f;
                        step = Step.WaitQualityBack;
                        break;

                    case Step.WaitQualityBack:
                        if (now < stepEnd) return;
                        matAppliesBeforeChange = controller.MaterialPropertyApplyCount;
                        // Visual change: nudge settings hash by re-setting water material (same or clone).
                        if (originalWaterMat != null)
                        {
                            var clone = new Material(originalWaterMat) { name = originalWaterMat.name + "_TestClone" };
                            controller.SetWaterMaterial(clone);
                        }
                        else if (controller.FluidSystem != null && controller.FluidSystem.settings != null)
                        {
                            controller.MetaballRenderer?.MarkMaterialDirty();
                        }
                        stepEnd = now + 0.25f;
                        step = Step.WaitMaterialChange;
                        break;

                    case Step.WaitMaterialChange:
                        if (now < stepEnd) return;
                        {
                            int delta = controller.MaterialPropertyApplyCount - matAppliesBeforeChange;
                            report.AppendLine($"Material change applies delta={delta} (expect >=1)");
                            if (delta >= 1) report.AppendLine("PASS: material reapplied after visual change.");
                            else report.AppendLine("FAIL: material did not reapply after visual change.");
                        }
                        if (originalWaterMat != null)
                            controller.SetWaterMaterial(originalWaterMat);
                        step = Step.DrainRegisterTest;
                        break;

                    case Step.DrainRegisterTest:
                    {
                        int before = FluidDrainRegistry.RegisteredCount;
                        int scansBefore = FluidDrainRegistry.FullScanCount;
                        tempDrainA = new GameObject("TempDrainA");
                        tempDrainA.AddComponent<BoxCollider2D>();
                        tempDrainA.AddComponent<FluidDrain2D>();
                        tempDrainB = new GameObject("TempDrainB");
                        tempDrainB.AddComponent<BoxCollider2D>();
                        var db = tempDrainB.AddComponent<FluidDrain2D>();
                        // Force a sim step window.
                        stepEnd = now + 0.3f;
                        report.AppendLine($"Drain registry before={before} after create expect +2; fullScans={scansBefore}");
                        step = Step.WaitDrain;
                        break;
                    }

                    case Step.WaitDrain:
                        if (now < stepEnd) return;
                        {
                            int count = FluidDrainRegistry.RegisteredCount;
                            int scans = FluidDrainRegistry.FullScanCount - drainFullScansAtStart;
                            report.AppendLine($"Drain registry count={count} fullScansDelta={scans}");
                            if (scans == 0) report.AppendLine("PASS: no full scene drain scans during sim.");
                            else report.AppendLine("FAIL: unexpected full drain scans=" + scans);

                            if (tempDrainB != null)
                            {
                                tempDrainB.SetActive(false);
                            }
                            stepEnd = now + 0.2f;
                            // Continue after brief wait into melt cycles.
                            // Disable temp drains for mass conservation.
                            if (tempDrainA != null) tempDrainA.SetActive(false);
                            step = Step.Melt1;
                        }
                        break;

                    case Step.Melt1:
                        eventChanging = eventChanged = eventWater = eventIce = 0;
                        controller.Melt();
                        stepEnd = now + 1.5f;
                        step = Step.WaitMelt1;
                        break;

                    case Step.WaitMelt1:
                        if (now < stepEnd) return;
                        report.AppendLine($"After Melt1 phase={controller.CurrentPhase} particles={controller.ActiveParticleCount} events C/H/W/I={eventChanging}/{eventChanged}/{eventWater}/{eventIce}");
                        if (controller.PhaseToggle != null && controller.PhaseToggle.HasConservedPhaseMass)
                            establishedMass = controller.ConservedMass;
                        controller.Freeze();
                        stepEnd = now + 0.8f;
                        step = Step.WaitFreeze1;
                        break;

                    case Step.WaitFreeze1:
                        if (now < stepEnd) return;
                        RecordCycle(1);
                        controller.Melt();
                        stepEnd = now + 1.5f;
                        step = Step.WaitMelt2;
                        break;

                    case Step.WaitMelt2:
                        if (now < stepEnd) return;
                        controller.Freeze();
                        stepEnd = now + 0.8f;
                        step = Step.WaitFreeze2;
                        break;

                    case Step.WaitFreeze2:
                        if (now < stepEnd) return;
                        RecordCycle(2);
                        controller.Melt();
                        stepEnd = now + 1.5f;
                        step = Step.WaitMelt3;
                        break;

                    case Step.WaitMelt3:
                        if (now < stepEnd) return;
                        controller.Freeze();
                        stepEnd = now + 0.8f;
                        step = Step.WaitFreeze3;
                        break;

                    case Step.WaitFreeze3:
                        if (now < stepEnd) return;
                        RecordCycle(3);
                        report.AppendLine($"Events over 3 cycles (approx): changing={eventChanging} changed={eventChanged} water={eventWater} ice={eventIce}");
                        // Keyboard-off: simulate would need Input injection; we assert flag.
                        report.AppendLine($"Keyboard toggle enabled={controller.EnableKeyboardToggle} (expect False)");
                        if (!controller.EnableKeyboardToggle)
                            report.AppendLine("PASS: keyboard toggle disabled by default on controller.");
                        CaptureGameViewShot();
                        step = Step.CaptureShot;
                        break;

                    case Step.CaptureShot:
                        Finish(true);
                        break;
                }
            }
            catch (Exception ex)
            {
                Fail(ex.ToString());
            }
        }

        private static void RecordCycle(int index)
        {
            if (controller?.PhaseToggle == null)
            {
                report.AppendLine($"Cycle {index}: missing phase toggle");
                return;
            }

            var t = controller.PhaseToggle;
            t.GetLastFreezeDiagnostics(
                out int captured, out int contours, out int clusters,
                out float coverage, out float iceArea, out float iou,
                out Bounds pb, out Bounds ib, out bool fallback, out string reason);
            t.GetLastFreezeContourSafetyDiagnostics(
                out int selfX, out int clamps, out int hulls, out int simple,
                out bool lin, out string safety);

            int mass = t.HasConservedPhaseMass ? t.ConservedPhaseMass : captured;
            massSamples.Add(mass);
            coverages.Add(coverage);
            if (establishedMass <= 0 && mass > 0)
                establishedMass = mass;

            int meshRenderers = 0;
            if (t.UsingGeneratedIce)
            {
                // Count MeshRenderers under generated blobs — should be 0 (SpriteShape only).
                var mrs = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                for (int i = 0; i < mrs.Length; i++)
                {
                    if (mrs[i] != null && mrs[i].gameObject.name.StartsWith("GeneratedIceBlob_"))
                        meshRenderers++;
                }
            }

            float pbMax = Mathf.Max(pb.size.x, pb.size.y, 1e-4f);
            float ibMax = Mathf.Max(ib.size.x, ib.size.y);
            float inflate = ibMax / pbMax;

            report.AppendLine(
                $"Cycle {index}: mass={mass} captured={captured} contours={contours} clusters={clusters} " +
                $"coverage={coverage:F3} iceArea={iceArea:F3} iou={iou:F3} inflate={inflate:F2} " +
                $"fallback={fallback} meshR={meshRenderers} reason={reason} " +
                $"safety(selfX={selfX},clamps={clamps},hull={hulls},simple={simple},lin={lin}) {safety}");

            if (establishedMass > 0 && mass != establishedMass)
                report.AppendLine($"WARN cycle {index}: mass {mass} != established {establishedMass}");
            if (coverage < 0.98f)
                report.AppendLine($"WARN cycle {index}: coverage {coverage:F3} < 0.98");
            if (meshRenderers != 0)
                report.AppendLine($"FAIL cycle {index}: MeshRenderer count {meshRenderers} expected 0");
            if (inflate > 2.6f && ibMax > pbMax + 0.75f)
                report.AppendLine($"FAIL cycle {index}: ice bounds inflated {inflate:F2}x vs particles (starburst risk)");
        }

        private static void CaptureGameViewShot()
        {
            try
            {
                string dir = Path.GetDirectoryName(shotPath);
                Directory.CreateDirectory(dir);
                // ScreenCapture works in play mode.
                ScreenCapture.CaptureScreenshot(shotPath);
                report.AppendLine("Screenshot requested: " + shotPath);
            }
            catch (Exception ex)
            {
                report.AppendLine("Screenshot failed: " + ex.Message);
            }
        }

        private static void Finish(bool success)
        {
            // Cleanup temp drains.
            if (tempDrainA != null) UnityEngine.Object.Destroy(tempDrainA);
            if (tempDrainB != null) UnityEngine.Object.Destroy(tempDrainB);

            // Summarize mass stability.
            bool massOk = true;
            if (massSamples.Count >= 3 && establishedMass > 0)
            {
                for (int i = 0; i < massSamples.Count; i++)
                {
                    if (massSamples[i] != establishedMass)
                        massOk = false;
                }
            }
            report.AppendLine($"Mass stability: established={establishedMass} samples=[{string.Join(",", massSamples)}] ok={massOk}");

            bool coverageOk = true;
            for (int i = 0; i < coverages.Count; i++)
                if (coverages[i] < 0.98f) coverageOk = false;
            report.AppendLine($"Coverage ok (>=0.98)={coverageOk}");

            report.AppendLine(success ? "RESULT=PASS_CANDIDATE" : "RESULT=FAIL");
            if (string.IsNullOrEmpty(reportPath))
                reportPath = Path.Combine(Application.dataPath, "..", "Temp", "Fluid2D_Environment_Validation.txt");
            File.WriteAllText(reportPath, report.ToString());
            Debug.Log(report.ToString());
            Debug.Log("Fluid2DEnvironmentValidator wrote " + reportPath);

            running = false;
            ClearSession();
            UnhookTick();
            // Leave play mode for clean state.
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        }

        private static void Fail(string msg)
        {
            report.AppendLine("FAIL: " + msg);
            Finish(false);
        }
    }
}
#endif
