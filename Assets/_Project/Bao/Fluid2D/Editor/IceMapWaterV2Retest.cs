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
    /// Automated V2 water retest (screenshots + 5-cycle mass conservation).
    /// Does not save Map_IceandSnow.unity. Menu: Fluid2D/Ice Map Water V2 Retest
    /// </summary>
    public static class IceMapWaterV2Retest
    {
        private enum Step
        {
            WaitPlay,
            Prep,
            MeltBaseline,
            WaitCalm,
            CaptureCalm,
            WaitShot1,
            FreezeForSplashPrep,
            WaitFreezePrep,
            MeltForSplash,
            WaitSplash,
            CaptureSplash,
            WaitShot2,
            WaitSettleForProxy,
            SetupProxy,
            CaptureProxy,
            WaitShot3,
            TeardownProxy,
            FreezeVerify,
            WaitFreezeVerify,
            CaptureIce,
            WaitShot4,
            CycleStart,
            CycleMelt,
            CycleWaitWater,
            CycleRecordWater,
            CycleFreeze,
            CycleWaitIce,
            CycleRecordIce,
            Done
        }

        private static Step step;
        private static float stepEndRealtime;
        private static int cycleIndex;
        private static bool running;
        private static string reportPath;
        private static string tempDir;
        private static readonly StringBuilder report = new StringBuilder();
        private static readonly List<string> cycleLines = new List<string>();
        private static int establishedMass;
        private static GameObject proxyRoot;
        private static Camera mainCam;
        private static Color savedBg;
        private static CameraClearFlags savedFlags;
        private static bool savedOrthographic;
        private static float savedOrthoSize;
        private static Vector3 savedCamPos;
        private static readonly List<FluidDrain2D> disabledDrains = new List<FluidDrain2D>();
        private static readonly List<Vector3> drainSavedPositions = new List<Vector3>();
        private static string pendingShot;

        [MenuItem("Tools/Bao/Fluid2D/Diagnostics/Legacy Ice Map Water V2 Retest")]
        public static void Run()
        {
            if (running)
            {
                Debug.LogWarning("IceMapWaterV2Retest already running.");
                return;
            }

            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;

            tempDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
            Directory.CreateDirectory(tempDir);
            reportPath = Path.Combine(tempDir, "IceMapWater_V2_Report.txt");
            report.Clear();
            cycleLines.Clear();
            disabledDrains.Clear();
            drainSavedPositions.Clear();
            pendingShot = null;
            report.AppendLine("IceMapWater V2 Retest " + DateTime.Now.ToString("o"));
            report.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);

            step = Step.WaitPlay;
            cycleIndex = 0;
            establishedMass = 0;
            running = true;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
            Debug.Log("IceMapWaterV2Retest: started.");
        }

        private static void Tick()
        {
            if (!running) return;

            try
            {
                if (EditorApplication.isCompiling)
                    return;

                if (!EditorApplication.isPlaying)
                {
                    if (step != Step.WaitPlay && step != Step.Done)
                    {
                        Fail("Exited play mode unexpectedly at step " + step);
                        return;
                    }
                    if (step == Step.Done)
                        return;
                }

                float now = Time.realtimeSinceStartup;
                var toggle = UnityEngine.Object.FindFirstObjectByType<IceWaterPhaseToggle2D>();
                var fluid = UnityEngine.Object.FindFirstObjectByType<Fluid2DSystem>();
                var renderer = UnityEngine.Object.FindFirstObjectByType<Fluid2DMetaballRenderer>();

                switch (step)
                {
                    case Step.WaitPlay:
                        if (!EditorApplication.isPlaying) return;
                        if (toggle == null || fluid == null)
                        {
                            Fail("Missing IceWaterPhaseToggle2D or Fluid2DSystem.");
                            return;
                        }
                        step = Step.Prep;
                        break;

                    case Step.Prep:
                    {
                        // Disable drains so mass conservation is measurable (particles leaving
                        // bounds by design is allowed; drain is intentional sink — turn off for test).
                        // Fluid2DSystem now skips !isActiveAndEnabled drains, but we still disable here.
                        foreach (var d in UnityEngine.Object.FindObjectsByType<FluidDrain2D>(FindObjectsSortMode.None))
                        {
                            if (d == null) continue;
                            drainSavedPositions.Add(d.transform.position);
                            disabledDrains.Add(d);
                            if (d.enabled)
                                d.enabled = false;
                            // Park far away so even a missed enabled check cannot sink mass.
                            d.transform.position = new Vector3(0f, -500f, 0f);
                        }
                        report.AppendLine("disabledDrains=" + disabledDrains.Count);

                        if (toggle.CurrentPhase != IceWaterPhaseToggle2D.Phase.Ice)
                            toggle.Freeze();
                        LogSettings(fluid);

                        mainCam = Camera.main;
                        if (mainCam != null)
                        {
                            savedBg = mainCam.backgroundColor;
                            savedFlags = mainCam.clearFlags;
                            savedOrthographic = mainCam.orthographic;
                            savedOrthoSize = mainCam.orthographicSize;
                            savedCamPos = mainCam.transform.position;
                            // Frame container + ice fall path for readable Game View.
                            mainCam.orthographic = true;
                            mainCam.orthographicSize = 7.5f;
                            mainCam.transform.position = new Vector3(0f, 1.5f, -10f);
                        }
                        step = Step.MeltBaseline;
                        break;
                    }

                    case Step.MeltBaseline:
                        toggle.Melt();
                        // Long enough for pool to settle so calm is not mid-air splash.
                        stepEndRealtime = now + 2.8f;
                        step = Step.WaitCalm;
                        break;

                    case Step.WaitCalm:
                        if (now < stepEndRealtime) return;
                        // Frame settled pool for calm shot.
                        if (mainCam != null)
                        {
                            mainCam.orthographicSize = 5.5f;
                            mainCam.transform.position = new Vector3(0f, -1.2f, -10f);
                        }
                        stepEndRealtime = now + 0.15f;
                        step = Step.CaptureCalm;
                        break;

                    case Step.CaptureCalm:
                        if (now < stepEndRealtime) return;
                        {
                            int active = fluid.ActiveParticleCount;
                            int tracked = toggle.TrackedParticleCount;
                            establishedMass = tracked > 0 ? tracked : active;
                            report.AppendLine(
                                $"Calm2p8s active={active} tracked={tracked} conserved={toggle.ConservedPhaseMass} hasMass={toggle.HasConservedPhaseMass}");
                            QueueShot("IceMapWater_V2_Calm_1p5s.png");
                            // Keep legacy name requested as before recapture sibling.
                            QueueShot("IceMapWater_V2_Before_1p5s.png");
                            stepEndRealtime = now + 0.35f;
                            step = Step.WaitShot1;
                        }
                        break;

                    case Step.WaitShot1:
                        if (now < stepEndRealtime) return;
                        FlushShots();
                        // Widen for splash path.
                        if (mainCam != null)
                        {
                            mainCam.orthographicSize = 7.5f;
                            mainCam.transform.position = new Vector3(0f, 1.5f, -10f);
                        }
                        step = Step.FreezeForSplashPrep;
                        break;

                    case Step.FreezeForSplashPrep:
                        toggle.Freeze();
                        stepEndRealtime = now + 0.35f;
                        step = Step.WaitFreezePrep;
                        break;

                    case Step.WaitFreezePrep:
                        if (now < stepEndRealtime) return;
                        step = Step.MeltForSplash;
                        break;

                    case Step.MeltForSplash:
                        toggle.Melt();
                        // Real impact window ~0.35-0.9s after melt.
                        stepEndRealtime = now + 0.55f;
                        step = Step.WaitSplash;
                        break;

                    case Step.WaitSplash:
                        if (now < stepEndRealtime) return;
                        step = Step.CaptureSplash;
                        break;

                    case Step.CaptureSplash:
                    {
                        int active = fluid.ActiveParticleCount;
                        int tracked = toggle.TrackedParticleCount;
                        report.AppendLine($"Splash0p55s active={active} tracked={tracked}");
                        QueueShot("IceMapWater_V2_Splash.png");
                        stepEndRealtime = now + 0.3f;
                        step = Step.WaitShot2;
                        break;
                    }

                    case Step.WaitShot2:
                        if (now < stepEndRealtime) return;
                        FlushShots();
                        // Let water settle in container before proxy + freeze verify.
                        stepEndRealtime = now + 2.0f;
                        step = Step.WaitSettleForProxy;
                        break;

                    case Step.WaitSettleForProxy:
                        if (now < stepEndRealtime) return;
                        if (mainCam != null)
                        {
                            mainCam.orthographicSize = 5.5f;
                            mainCam.transform.position = new Vector3(0f, -1.2f, -10f);
                        }
                        step = Step.SetupProxy;
                        break;

                    case Step.SetupProxy:
                        SetupMapLikeProxy(mainCam);
                        stepEndRealtime = now + 0.25f;
                        step = Step.CaptureProxy;
                        break;

                    case Step.CaptureProxy:
                        if (now < stepEndRealtime) return;
                        QueueShot("IceMapWater_V2_ProxyBG.png");
                        stepEndRealtime = now + 0.3f;
                        step = Step.WaitShot3;
                        break;

                    case Step.WaitShot3:
                        if (now < stepEndRealtime) return;
                        FlushShots();
                        step = Step.TeardownProxy;
                        break;

                    case Step.TeardownProxy:
                        TeardownMapLikeProxy(mainCam);
                        step = Step.FreezeVerify;
                        break;

                    case Step.FreezeVerify:
                        toggle.Freeze();
                        stepEndRealtime = now + 0.45f;
                        step = Step.WaitFreezeVerify;
                        break;

                    case Step.WaitFreezeVerify:
                        if (now < stepEndRealtime) return;
                        step = Step.CaptureIce;
                        break;

                    case Step.CaptureIce:
                        LogIce(toggle, "FreezeVerify");
                        LogIceObjects();
                        QueueShot("IceMapWater_V2_Freeze.png");
                        stepEndRealtime = now + 0.3f;
                        step = Step.WaitShot4;
                        break;

                    case Step.WaitShot4:
                        if (now < stepEndRealtime) return;
                        FlushShots();
                        cycleIndex = 0;
                        step = Step.CycleStart;
                        break;

                    case Step.CycleStart:
                        if (cycleIndex >= 5)
                        {
                            step = Step.Done;
                            break;
                        }
                        step = Step.CycleMelt;
                        break;

                    case Step.CycleMelt:
                        toggle.Melt();
                        // Settle so freeze captures nearly all mass still in bounds.
                        stepEndRealtime = now + 1.6f;
                        step = Step.CycleWaitWater;
                        break;

                    case Step.CycleWaitWater:
                        if (now < stepEndRealtime) return;
                        step = Step.CycleRecordWater;
                        break;

                    case Step.CycleRecordWater:
                    {
                        int active = fluid.ActiveParticleCount;
                        int tracked = toggle.TrackedParticleCount;
                        var frag = toggle.CaptureWaterFragmentationDiagnostics(true);
                        string line =
                            $"Cycle{cycleIndex + 1}_Water active={active} tracked={tracked} conserved={toggle.ConservedPhaseMass} " +
                            $"clusters={frag.clusterCount} isolated={frag.isolatedCount} largestFrac={frag.largestClusterFraction:F3} " +
                            $"maxSpd={frag.maxSpeed:F2}";
                        cycleLines.Add(line);
                        report.AppendLine(line);
                        step = Step.CycleFreeze;
                        break;
                    }

                    case Step.CycleFreeze:
                        toggle.Freeze();
                        stepEndRealtime = now + 0.45f;
                        step = Step.CycleWaitIce;
                        break;

                    case Step.CycleWaitIce:
                        if (now < stepEndRealtime) return;
                        step = Step.CycleRecordIce;
                        break;

                    case Step.CycleRecordIce:
                    {
                        LogIce(toggle, $"Cycle{cycleIndex + 1}_Ice");
                        int active = fluid != null ? fluid.ActiveParticleCount : -1;
                        int tracked = toggle.TrackedParticleCount;
                        report.AppendLine(
                            $"Cycle{cycleIndex + 1}_Ice activeParticles={active} tracked={tracked} rtPresent={(renderer != null)}");
                        cycleIndex++;
                        step = Step.CycleStart;
                        break;
                    }

                    case Step.Done:
                        Finish(success: true);
                        break;
                }
            }
            catch (Exception ex)
            {
                Fail(ex.ToString());
            }
        }

        private static void QueueShot(string fileName)
        {
            // Reliable camera RT capture (ScreenCapture often misses when Game View is not focused).
            string path = Path.Combine(tempDir, fileName);
            Camera cam = mainCam != null ? mainCam : Camera.main;
            if (cam == null)
            {
                ScreenCapture.CaptureScreenshot(path);
                report.AppendLine("CAPTURE_FALLBACK " + fileName);
                return;
            }

            int w = Mathf.Max(960, cam.pixelWidth > 16 ? cam.pixelWidth : 1280);
            int h = Mathf.Max(540, cam.pixelHeight > 16 ? cam.pixelHeight : 720);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            report.AppendLine("CAPTURE " + fileName + " -> " + path + " bytes=" + new FileInfo(path).Length);
            pendingShot = path;
            Debug.Log("IceMapWaterV2Retest capture: " + path);
        }

        private static void FlushShots()
        {
            // no-op marker; waits handle frame completion for ScreenCapture
            pendingShot = null;
        }

        private static void LogSettings(Fluid2DSystem fluid)
        {
            var s = fluid.settings;
            if (s == null)
            {
                report.AppendLine("SETTINGS: null");
                return;
            }
            report.AppendLine("SETTINGS V2:");
            report.AppendLine($"  surfaceColor={s.surfaceColor}");
            report.AppendLine($"  deepColor={s.deepColor}");
            report.AppendLine($"  foamColor={s.foamColor}");
            report.AppendLine($"  outlineColor={s.outlineColor}");
            report.AppendLine($"  densityThreshold={s.densityThreshold}");
            report.AppendLine($"  outlineWidth={s.outlineWidth}");
            report.AppendLine($"  edgeSoftness={s.edgeSoftness}");
            report.AppendLine($"  depthDarkening={s.depthDarkening}");
            report.AppendLine($"  highlightStrength={s.highlightStrength}");
            report.AppendLine($"  rippleStrength={s.rippleStrength}");
            report.AppendLine($"  verticalGradient={s.verticalGradient}");
            report.AppendLine($"  surfaceBandStrength={s.surfaceBandStrength}");
            report.AppendLine($"  surfaceBandWidth={s.surfaceBandWidth}");
            report.AppendLine($"  noiseStrength={s.noiseStrength}");
            report.AppendLine($"  noiseScale={s.noiseScale}");
            report.AppendLine($"  noiseScaleB={s.noiseScaleB}");
            report.AppendLine($"  noiseSpeed={s.noiseSpeed}");
            report.AppendLine($"  fresnelStrength={s.fresnelStrength}");
            report.AppendLine($"  fresnelPower={s.fresnelPower}");
            report.AppendLine($"  foamCurvature={s.foamCurvature}");
            report.AppendLine($"  sparkleStrength={s.sparkleStrength}");
            report.AppendLine($"  bodyAlpha={s.bodyAlpha}");
            report.AppendLine($"  bandContrast={s.bandContrast}");
            report.AppendLine($"  bottomRimScale={s.bottomRimScale}");
        }

        private static void LogIce(IceWaterPhaseToggle2D toggle, string tag)
        {
            toggle.GetLastFreezeDiagnostics(
                out int captured, out int contours, out int clusters,
                out float coverage, out float iceArea, out float iou,
                out Bounds pb, out Bounds ib, out bool fallback, out string reason);
            toggle.GetLastFreezeTopologyDiagnostics(
                out int inputClusters, out int outputBlobs, out int holes,
                out int bridges, out int sats, out float cov2, out float iou2);
            string line =
                $"{tag} captured={captured} contours={contours} clusters={clusters} coverage={coverage:F4} " +
                $"iceArea={iceArea:F3} iou={iou:F4} blobs={toggle.GeneratedBlobCount} " +
                $"inputClusters={inputClusters} holes={holes} bridges={bridges} sats={sats} " +
                $"fallback={fallback} reason={reason}";
            report.AppendLine(line);
            cycleLines.Add(line);
        }

        private static void LogIceObjects()
        {
            var blobs = UnityEngine.Object.FindObjectsByType<SpriteShapeController>(FindObjectsSortMode.None);
            int meshRenderersOnBlobs = 0;
            int spriteShapeOnBlobs = 0;
            foreach (var ssc in blobs)
            {
                if (ssc == null || !ssc.gameObject.name.StartsWith("GeneratedIceBlob_"))
                    continue;
                spriteShapeOnBlobs++;
                var mr = ssc.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled)
                    meshRenderersOnBlobs++;
                var ssr = ssc.GetComponent<SpriteShapeRenderer>();
                report.AppendLine(
                    $"  blob={ssc.gameObject.name} spriteShapeRenderer={(ssr != null && ssr.enabled)} " +
                    $"meshRenderer={(mr != null && mr.enabled)} colliders={ssc.GetComponents<Collider2D>().Length}");
            }
            report.AppendLine($"IceObjects spriteShapeBlobs={spriteShapeOnBlobs} enabledMeshRenderers={meshRenderersOnBlobs}");
        }

        private static void SetupMapLikeProxy(Camera cam)
        {
            TeardownMapLikeProxy(cam);
            proxyRoot = new GameObject("__IceMapWater_V2_ProxyBG");
            proxyRoot.hideFlags = HideFlags.DontSave;

            CreateProxyQuad(proxyRoot.transform, "Snow", new Vector3(0f, 5.5f, 5f), new Vector2(40f, 14f),
                new Color(0.78f, 0.93f, 0.97f, 1f));
            CreateProxyQuad(proxyRoot.transform, "Cave", new Vector3(0f, -3.5f, 5f), new Vector2(40f, 12f),
                new Color(0.04f, 0.18f, 0.24f, 1f));
            CreateProxyQuad(proxyRoot.transform, "MidBand", new Vector3(0f, 1.2f, 5f), new Vector2(40f, 3.5f),
                new Color(0.12f, 0.38f, 0.44f, 1f));

            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.28f, 0.34f, 1f);
            }
        }

        private static void CreateProxyQuad(Transform parent, string name, Vector3 pos, Vector2 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var col = go.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.Destroy(col);
            var r = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.hideFlags = HideFlags.DontSave;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static void TeardownMapLikeProxy(Camera cam)
        {
            if (proxyRoot != null)
            {
                UnityEngine.Object.Destroy(proxyRoot);
                proxyRoot = null;
            }
            if (cam != null)
            {
                cam.clearFlags = savedFlags;
                cam.backgroundColor = savedBg;
            }
        }

        private static void Fail(string message)
        {
            report.AppendLine("FAIL: " + message);
            Debug.LogError("IceMapWaterV2Retest FAIL: " + message);
            Finish(success: false);
        }

        private static void Finish(bool success)
        {
            report.AppendLine("--- MASS CONSERVATION ---");
            report.AppendLine("establishedMass(first calm tracked)=" + establishedMass);
            int maxAllowedDelta = Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(1, establishedMass) * 0.01f));
            report.AppendLine("allowedDelta=max(1%,2)=" + maxAllowedDelta);

            var trackedCounts = new List<int>();
            foreach (var line in cycleLines)
            {
                if (!line.Contains("_Water ")) continue;
                int idx = line.IndexOf("tracked=", StringComparison.Ordinal);
                if (idx < 0) continue;
                int start = idx + "tracked=".Length;
                int end = start;
                while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-')) end++;
                if (int.TryParse(line.Substring(start, end - start), out int t))
                    trackedCounts.Add(t);
            }

            bool massOk = trackedCounts.Count == 5;
            bool noGrowth = true;
            if (trackedCounts.Count > 0)
            {
                int baseline = trackedCounts[0];
                report.AppendLine("cycleWaterTracked=[" + string.Join(",", trackedCounts) + "]");
                for (int i = 0; i < trackedCounts.Count; i++)
                {
                    int d = trackedCounts[i] - baseline;
                    int ad = Mathf.Abs(d);
                    // Growth beyond tolerance is always a fail (morphological mass invention).
                    // Loss can be bounds-exit; with drains disabled we still require hold within tol.
                    bool ok = ad <= maxAllowedDelta;
                    if (d > maxAllowedDelta) noGrowth = false;
                    report.AppendLine($"  cycle{i + 1} tracked={trackedCounts[i]} delta={d} abs={ad} ok={ok}");
                    if (!ok) massOk = false;
                }
            }
            else
            {
                massOk = false;
                report.AppendLine("No cycle water tracked counts parsed.");
            }

            report.AppendLine("NO_MASS_GROWTH=" + noGrowth);
            report.AppendLine("MASS_OK=" + massOk);
            report.AppendLine("SUCCESS=" + (success && massOk));
            File.WriteAllText(reportPath, report.ToString());
            Debug.Log("IceMapWaterV2Retest wrote " + reportPath + "\n" + report);

            TeardownMapLikeProxy(mainCam);
            for (int i = 0; i < disabledDrains.Count; i++)
            {
                var d = disabledDrains[i];
                if (d == null) continue;
                d.enabled = true;
                if (i < drainSavedPositions.Count)
                    d.transform.position = drainSavedPositions[i];
            }
            disabledDrains.Clear();
            drainSavedPositions.Clear();

            if (mainCam != null)
            {
                mainCam.orthographic = savedOrthographic;
                mainCam.orthographicSize = savedOrthoSize;
                mainCam.transform.position = savedCamPos;
                mainCam.clearFlags = savedFlags;
                mainCam.backgroundColor = savedBg;
            }

            running = false;
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            Debug.Log("IceMapWaterV2Retest finished success=" + success + " massOk=" + massOk);
        }
    }
}
#endif
