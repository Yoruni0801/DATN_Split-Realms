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
    /// Offline geometry tests + play-mode rapid Ice↔Water stress for starburst / self-intersecting ice.
    /// Menu: Tools/Bao/Fluid2D/Diagnostics/Ice Freeze Contour Safety…
    /// </summary>
    public static class IceFreezeContourSafetyTests
    {
        private const string ReportRelative = "Temp/Fluid2D_IceContourSafety.txt";

        [MenuItem("Tools/Bao/Fluid2D/Diagnostics/Ice Freeze Contour Safety (Edit Mode Geometry)")]
        public static void RunGeometryTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ice Freeze Contour Safety — Geometry " + DateTime.Now.ToString("o"));
            int fail = 0;
            int pass = 0;

            // 1) Classic self-intersecting bow-tie.
            var bowTie = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(2f, 2f),
                new Vector2(0f, 2f),
                new Vector2(2f, 0f)
            };
            if (IceWaterPhaseToggle2D.ContourHasSelfIntersection(bowTie))
            {
                pass++;
                sb.AppendLine("PASS: bow-tie detected as self-intersecting");
            }
            else
            {
                fail++;
                sb.AppendLine("FAIL: bow-tie not detected");
            }

            // 2) Simple square is clean.
            var square = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            if (!IceWaterPhaseToggle2D.ContourHasSelfIntersection(square))
            {
                pass++;
                sb.AppendLine("PASS: square has no self-intersection");
            }
            else
            {
                fail++;
                sb.AppendLine("FAIL: square falsely self-intersects");
            }

            // 3) True self-intersecting star (pentagram {5/2}): connect every 2nd vertex of a
            // regular pentagon. Angular outer/inner "starburst" outlines are simple concave
            // polygons and must NOT be used here — they have no proper edge crossings.
            // Convex hull of the five tips is the correct non-crossing repair.
            var pentagon = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f - Mathf.PI * 0.5f;
                pentagon[i] = new Vector2(Mathf.Cos(a) * 2f, Mathf.Sin(a) * 2f);
            }
            // Visit order 0→2→4→1→3 closes the pentagram with multiple proper crossings.
            var star = new List<Vector2>
            {
                pentagon[0], pentagon[2], pentagon[4], pentagon[1], pentagon[3]
            };
            bool starCross = IceWaterPhaseToggle2D.ContourHasSelfIntersection(star);
            var starHull = IceWaterPhaseToggle2D.BuildConvexHullForTest(star);
            bool hullOk = starHull != null && starHull.Count >= 3 && starHull.Count <= 5 &&
                          !IceWaterPhaseToggle2D.ContourHasSelfIntersection(starHull);
            if (starCross && hullOk)
            {
                pass++;
                sb.AppendLine($"PASS: star self-intersect={starCross}, hull repair verts={starHull.Count} clean");
            }
            else
            {
                fail++;
                sb.AppendLine($"FAIL: starCross={starCross} hullOk={hullOk} verts={starHull?.Count ?? 0}");
            }

            // 4) Convex hull of noisy cloud is simple and non-crossing.
            var cloud = new List<Vector2>();
            var rng = new System.Random(42);
            for (int i = 0; i < 40; i++)
            {
                cloud.Add(new Vector2(
                    (float)(rng.NextDouble() * 2.0 - 1.0),
                    (float)(rng.NextDouble() * 2.0 - 1.0)));
            }
            var hull = IceWaterPhaseToggle2D.BuildConvexHullForTest(cloud);
            if (hull != null && hull.Count >= 3 && !IceWaterPhaseToggle2D.ContourHasSelfIntersection(hull))
            {
                pass++;
                sb.AppendLine($"PASS: convex hull verts={hull.Count} non-self-intersecting");
            }
            else
            {
                fail++;
                sb.AppendLine($"FAIL: hull verts={hull?.Count ?? 0}");
            }

            // 5) Dense zig-zag fan (triangle starburst silhouette).
            var fan = new List<Vector2> { Vector2.zero };
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                fan.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (1.5f + (i % 3) * 0.8f));
                fan.Add(Vector2.zero * 0.01f + new Vector2(0.02f * i, 0f)); // near-center re-entries
            }
            // Clean via hull — always simple.
            var fanHull = IceWaterPhaseToggle2D.BuildConvexHullForTest(fan);
            if (fanHull != null && !IceWaterPhaseToggle2D.ContourHasSelfIntersection(fanHull))
            {
                pass++;
                sb.AppendLine($"PASS: fan→hull verts={fanHull.Count}");
            }
            else
            {
                fail++;
                sb.AppendLine("FAIL: fan hull");
            }

            // 6) Long shallow sparse pool: oriented ribbon must preserve length (not collapse to center).
            var longShallow = new List<Vector2>();
            for (int i = 0; i < 28; i++)
                longShallow.Add(new Vector2(i * 0.55f, (i % 3) * 0.04f));
            bool elongated = IceWaterPhaseToggle2D.IsElongatedPointSetForTest(longShallow);
            var ribbon = IceWaterPhaseToggle2D.BuildOrientedRibbonContourForTest(longShallow, halfWidth: 0.18f, endPad: 0.08f);
            float srcSpanX = longShallow[longShallow.Count - 1].x - longShallow[0].x;
            float ribbonSpanX = 0f;
            if (ribbon != null && ribbon.Count >= 3)
            {
                float minX = ribbon[0].x, maxX = ribbon[0].x;
                for (int i = 1; i < ribbon.Count; i++)
                {
                    minX = Mathf.Min(minX, ribbon[i].x);
                    maxX = Mathf.Max(maxX, ribbon[i].x);
                }
                ribbonSpanX = maxX - minX;
            }
            bool ribbonOk = elongated && ribbon != null && ribbon.Count >= 3 &&
                            !IceWaterPhaseToggle2D.ContourHasSelfIntersection(ribbon) &&
                            ribbonSpanX >= srcSpanX * 0.85f;
            if (ribbonOk)
            {
                pass++;
                sb.AppendLine($"PASS: long-shallow ribbon elongated={elongated} spanX={ribbonSpanX:F2} (src={srcSpanX:F2}) verts={ribbon.Count}");
            }
            else
            {
                fail++;
                sb.AppendLine($"FAIL: long-shallow ribbon elongated={elongated} spanX={ribbonSpanX:F2} src={srcSpanX:F2} verts={ribbon?.Count ?? 0}");
            }

            // 7) Compact cloud is NOT elongated (ribbon path must not steal starburst cases).
            var compactCloud = new List<Vector2>();
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    compactCloud.Add(new Vector2(x * 0.1f, y * 0.1f));
            if (!IceWaterPhaseToggle2D.IsElongatedPointSetForTest(compactCloud))
            {
                pass++;
                sb.AppendLine("PASS: compact 5x5 grid is not elongated");
            }
            else
            {
                fail++;
                sb.AppendLine("FAIL: compact grid falsely elongated");
            }

            sb.AppendLine($"RESULT pass={pass} fail={fail} total={pass + fail}");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportRelative));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            Debug.Log(sb.ToString() + "\nWrote " + path);

            if (fail > 0)
                EditorUtility.DisplayDialog("Ice Contour Safety", $"Geometry tests FAILED ({fail}).\nSee {ReportRelative}", "OK");
            else
                EditorUtility.DisplayDialog("Ice Contour Safety", $"Geometry tests PASS ({pass}).\nSee {ReportRelative}", "OK");
        }

        [MenuItem("Tools/Bao/Fluid2D/Diagnostics/Ice Freeze Rapid Toggle Stress (Play Mode)")]
        public static void RunRapidToggleStress()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Ice Rapid Toggle Stress",
                    "Enter Play Mode in TestScene (or any scene with Fluid2DEnvironmentController), then run this menu again.\n\n" +
                    "Alternatively use: Tools/Bao/Fluid2D/Diagnostics/Ice Freeze Contour Safety (Edit Mode Geometry) offline.",
                    "OK");
                return;
            }

            var controller = UnityEngine.Object.FindFirstObjectByType<Fluid2DEnvironmentController>();
            IceWaterPhaseToggle2D toggle = controller != null ? controller.PhaseToggle : null;
            if (toggle == null)
                toggle = UnityEngine.Object.FindFirstObjectByType<IceWaterPhaseToggle2D>();

            if (toggle == null)
            {
                EditorUtility.DisplayDialog("Ice Rapid Toggle Stress", "No IceWaterPhaseToggle2D in the open scene.", "OK");
                return;
            }

            var runner = UnityEngine.Object.FindFirstObjectByType<IceFreezeRapidToggleStressRunner>();
            if (runner == null)
            {
                var go = new GameObject("__IceFreezeRapidToggleStress");
                runner = go.AddComponent<IceFreezeRapidToggleStressRunner>();
            }
            runner.Begin(toggle, cycles: 12, intervalSeconds: 1f);
            Debug.Log("IceFreezeRapidToggleStress: started 12× Ice↔Water at ~1s interval on " + toggle.name);
        }

        [MenuItem("Tools/Bao/Fluid2D/Diagnostics/Ice Freeze BuildIce Pathological Clusters (Play Mode)")]
        public static void RunPathologicalClusterBuild()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Pathological Ice Build", "Enter Play Mode first.", "OK");
                return;
            }

            var toggle = UnityEngine.Object.FindFirstObjectByType<IceWaterPhaseToggle2D>();
            if (toggle == null)
            {
                EditorUtility.DisplayDialog("Pathological Ice Build", "No IceWaterPhaseToggle2D found.", "OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Pathological BuildIceFromWorldPositions " + DateTime.Now.ToString("o"));
            int fail = 0;

            // Cluster A: compact pool
            var compact = new List<Vector2>();
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 8; x++)
                    compact.Add(new Vector2(x * 0.12f, y * 0.12f));

            // Cluster B: mid-air spray (rapid-toggle style)
            var spray = new List<Vector2>();
            for (int i = 0; i < 30; i++)
            {
                float a = i * 0.7f;
                spray.Add(new Vector2(Mathf.Cos(a) * (0.3f + i * 0.04f), 1.2f + Mathf.Sin(a) * 0.5f));
            }

            // Cluster C: two distant blobs
            var dual = new List<Vector2>();
            for (int i = 0; i < 12; i++)
                dual.Add(new Vector2(i * 0.1f, 0f));
            for (int i = 0; i < 12; i++)
                dual.Add(new Vector2(4f + i * 0.1f, 0.2f));

            fail += RunOneBuild(toggle, compact, "compact", sb);
            fail += RunOneBuild(toggle, spray, "spray", sb);
            fail += RunOneBuild(toggle, dual, "dual", sb);

            // Starburst-like positions on a ring with center pile (worst-case stamp)
            var ring = new List<Vector2>();
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI * 2f / 20f;
                ring.Add(new Vector2(Mathf.Cos(a) * 1.8f, Mathf.Sin(a) * 1.8f));
            }
            for (int i = 0; i < 8; i++)
                ring.Add(Vector2.zero);
            fail += RunOneBuild(toggle, ring, "ring+center", sb);

            // Long shallow sparse strip (~full-screen horizontal pool stress).
            // Must preserve length (ice max extent ≥ ~80% of particle span) and not collapse to center blob.
            var longShallow = new List<Vector2>();
            for (int i = 0; i < 36; i++)
                longShallow.Add(new Vector2(i * 0.42f, Mathf.Sin(i * 0.35f) * 0.05f));
            fail += RunOneBuild(toggle, longShallow, "long-shallow", sb, requireLengthPreserve: true);

            // Two separated long blobs must remain multi-contour (not one central merge).
            var dualRibbon = new List<Vector2>();
            for (int i = 0; i < 14; i++)
                dualRibbon.Add(new Vector2(i * 0.35f, 0f));
            for (int i = 0; i < 14; i++)
                dualRibbon.Add(new Vector2(i * 0.35f, 3.5f));
            fail += RunOneBuild(toggle, dualRibbon, "dual-ribbon", sb, requireMinContours: 2);

            sb.AppendLine(fail == 0 ? "ALL PASS" : $"FAILURES={fail}");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportRelative));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path, sb.ToString());
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Pathological Ice Build", fail == 0 ? "PASS" : $"FAIL ({fail}) — see console / {ReportRelative}", "OK");
        }

        private static int RunOneBuild(
            IceWaterPhaseToggle2D toggle,
            List<Vector2> pts,
            string tag,
            StringBuilder sb,
            bool requireLengthPreserve = false,
            int requireMinContours = 1)
        {
            // Offset into a stable world region near the toggle.
            Vector2 origin = toggle.transform.position;
            var world = new List<Vector2>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
                world.Add(pts[i] + origin);

            bool built = toggle.BuildIceFromWorldPositionsForTest(world);
            toggle.GetLastFreezeDiagnostics(
                out int captured, out int contours, out int clusters,
                out float coverage, out float iceArea, out float iou,
                out Bounds pb, out Bounds ib, out bool fallback, out string reason);
            toggle.GetLastFreezeContourSafetyDiagnostics(
                out int selfX, out int clamps, out int hulls, out int simple,
                out bool lin, out string safety);

            float pbMax = Mathf.Max(pb.size.x, pb.size.y);
            float ibMax = Mathf.Max(ib.size.x, ib.size.y);
            float inflate = pbMax > 1e-4f ? ibMax / pbMax : 0f;
            bool boundsOk = inflate <= 2.5f || ibMax < pbMax + 0.5f;
            bool areaOk = iceArea < Mathf.Max(1e-4f, pb.size.x * pb.size.y) * 5f + 1f;

            // Length preservation: ice major extent must cover most of the particle silhouette.
            bool lengthOk = true;
            if (requireLengthPreserve)
            {
                float srcMajor = Mathf.Max(pb.size.x, pb.size.y);
                float iceMajor = Mathf.Max(ib.size.x, ib.size.y);
                float srcMinor = Mathf.Max(1e-4f, Mathf.Min(pb.size.x, pb.size.y));
                float iceMinor = Mathf.Max(1e-4f, Mathf.Min(ib.size.x, ib.size.y));
                // Reject central blob: ice major too short OR ice became nearly round while source was a strip.
                bool majorKept = srcMajor < 1e-3f || iceMajor >= srcMajor * 0.80f;
                bool notCollapsedBlob = iceMajor >= iceMinor * 1.6f || srcMajor < srcMinor * 2f;
                lengthOk = majorKept && notCollapsedBlob;
            }

            bool contourCountOk = contours >= Mathf.Max(1, requireMinContours);

            // Inspect generated blob local spline for pathological point counts / extents.
            bool splineOk = true;
            var blobs = toggle.GetComponentsInChildren<SpriteShapeController>(true);
            foreach (var ssc in blobs)
            {
                if (ssc == null || !ssc.gameObject.name.StartsWith("GeneratedIceBlob_")) continue;
                int n = ssc.spline != null ? ssc.spline.GetPointCount() : 0;
                if (n > 0 && ssc.spline != null)
                {
                    Bounds lb = new Bounds(ssc.spline.GetPosition(0), Vector3.zero);
                    for (int i = 1; i < n; i++)
                        lb.Encapsulate(ssc.spline.GetPosition(i));
                    if (lb.size.x > 40f || lb.size.y > 40f || n > 200)
                        splineOk = false;
                }
            }

            bool ok = built && boundsOk && areaOk && splineOk && lengthOk && contourCountOk;
            sb.AppendLine(
                $"[{tag}] built={built} captured={captured} contours={contours} clusters={clusters} " +
                $"coverage={coverage:F3} iceArea={iceArea:F3} iou={iou:F3} inflate={inflate:F2} " +
                $"pb={pb.size.x:F2}x{pb.size.y:F2} ib={ib.size.x:F2}x{ib.size.y:F2} " +
                $"ribbon={toggle.LastUsedRibbonFallback} densified={toggle.LastDensifiedSampleCount} oob={toggle.LastOutOfGridParticleCount} " +
                $"fallback={fallback}/{reason} selfX={selfX} clamps={clamps} hulls={hulls} simple={simple} " +
                $"linearForce={lin} safety={safety} boundsOk={boundsOk} lengthOk={lengthOk} contourCountOk={contourCountOk} " +
                $"splineOk={splineOk} => {(ok ? "PASS" : "FAIL")}");
            return ok ? 0 : 1;
        }
    }

    /// <summary>
    /// Play-mode component: Ice→Water→Ice at fixed interval; asserts bounds/coverage after each freeze.
    /// </summary>
    public class IceFreezeRapidToggleStressRunner : MonoBehaviour
    {
        private IceWaterPhaseToggle2D toggle;
        private int cycles;
        private int done;
        private float interval;
        private float nextTime;
        private bool waitingIce;
        private readonly StringBuilder report = new StringBuilder();
        private int failures;

        public void Begin(IceWaterPhaseToggle2D target, int cycles, float intervalSeconds)
        {
            toggle = target;
            this.cycles = Mathf.Max(1, cycles);
            interval = Mathf.Max(0.25f, intervalSeconds);
            done = 0;
            failures = 0;
            report.Clear();
            report.AppendLine("Rapid toggle stress " + DateTime.Now.ToString("o"));
            report.AppendLine($"target={toggle.name} cycles={this.cycles} interval={interval:F2}s");

            // Ensure starting ice.
            if (toggle.CurrentPhase != IceWaterPhaseToggle2D.Phase.Ice)
                toggle.Freeze();

            waitingIce = false;
            nextTime = Time.time + 0.35f;
            enabled = true;
        }

        private void Update()
        {
            if (toggle == null)
            {
                enabled = false;
                return;
            }

            if (Time.time < nextTime) return;

            if (!waitingIce)
            {
                // Melt, then schedule freeze ~1s later (user repro cadence).
                toggle.Melt();
                waitingIce = true;
                nextTime = Time.time + interval;
                return;
            }

            toggle.Freeze();
            waitingIce = false;
            done++;

            toggle.GetLastFreezeDiagnostics(
                out int captured, out int contours, out int clusters,
                out float coverage, out float iceArea, out float iou,
                out Bounds pb, out Bounds ib, out bool fallback, out string reason);
            toggle.GetLastFreezeContourSafetyDiagnostics(
                out int selfX, out int clamps, out int hulls, out int simple,
                out bool lin, out string safety);

            float pbMax = Mathf.Max(pb.size.x, pb.size.y, 1e-4f);
            float ibMax = Mathf.Max(ib.size.x, ib.size.y);
            float inflate = ibMax / pbMax;
            bool boundsOk = inflate <= 2.6f || ibMax <= pbMax + 0.75f;
            bool coverageOk = captured == 0 || coverage >= 0.85f;
            bool ok = boundsOk && (contours > 0 || captured == 0);

            if (!ok) failures++;
            report.AppendLine(
                $"cycle {done}: mass={toggle.ConservedPhaseMass} captured={captured} contours={contours} " +
                $"clusters={clusters} cov={coverage:F3} iceArea={iceArea:F2} iou={iou:F3} inflate={inflate:F2} " +
                $"selfX={selfX} clamps={clamps} hull={hulls} simple={simple} lin={lin} " +
                $"fb={fallback}/{reason} safety={safety} => {(ok ? "PASS" : "FAIL")}");

            if (done >= cycles)
            {
                report.AppendLine(failures == 0
                    ? $"DONE OK ({done} cycles)"
                    : $"DONE WITH {failures} FAILURES / {done}");
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp/Fluid2D_IceContourSafety.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, report.ToString());
                Debug.Log(report.ToString());
                enabled = false;
            }
            else
            {
                nextTime = Time.time + 0.15f; // brief beat before next melt
            }
        }
    }
}
#endif
