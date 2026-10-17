using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LaserMirrorBeamVisual2D : MonoBehaviour
{
    [Header("█ Beam Prefabs (MagicArsenal) ——————————————")]

    // Prefab chứa LineRenderer tia beam (vd: Water Beam, Arcane Beam...).
    [SerializeField] private GameObject beamLinePrefab;

    // Prefab VFX tại điểm bắt đầu tia (vd: WaterBeamStart).
    [SerializeField] private GameObject beamStartPrefab;

    // Prefab VFX tại điểm kết thúc tia (vd: WaterBeamImpact).
    [SerializeField] private GameObject beamEndPrefab;

    [Header("█ Beam Settings ——————————————")]

    // Khoảng cách dịch lùi beamEnd so với điểm va chạm (giống MagicBeamScript).
    [SerializeField] private float beamEndOffset = 0.2f;

    // Tốc độ cuộn texture dọc theo tia beam.
    [SerializeField] private float textureScrollSpeed = 8f;

    // Tỷ lệ độ dài texture trên beam (nhỏ = texture lặp nhiều hơn).
    [SerializeField, Min(0.1f)] private float textureLengthScale = 3f;

    [Header("█ Sorting Orders ——————————————")]
    [SerializeField] private int baseSortingOrder = 72;

    // ═══════════════════════════════════════════════════════════════
    //  RUNTIME INSTANCES
    // ═══════════════════════════════════════════════════════════════

    private readonly List<GameObject> beamLineInstances = new();
    private readonly List<LineRenderer> lineSegments = new();
    private readonly List<Material> beamMaterials = new();
    private GameObject beamStartInstance;
    private GameObject beamEndInstance;
    private bool endpointsEnabled = true;

    public void Configure(LineRenderer oldBeamRenderer = null)
    {
        // No-op — prefab-based.
    }

    public void ApplyEnergyPalette(LaserMirrorCavePortal2D.PortalEnergyPalette palette)
    {
        // MagicArsenal prefab giữ nguyên màu gốc vật liệu.
    }

    private void OnDestroy()
    {
        DestroyBeamInstances();
    }

    private void LateUpdate()
    {
        UpdateTextureScroll();
    }

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════

    public void SetEndpointsActive(bool active)
    {
        endpointsEnabled = active;

        if (beamStartInstance != null)
        {
            beamStartInstance.SetActive(active);
        }

        if (beamEndInstance != null)
        {
            beamEndInstance.SetActive(active);
        }
    }

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            // Các segment sẽ được bật tương ứng trong SetBeamPoints
        }
        else
        {
            DestroyBeamInstances();
        }
    }

    public void SetBeamPoints(IList<Vector3> points)
    {
        if (points == null || points.Count < 2)
        {
            SetVisible(false);
            return;
        }

        ApplyBeamPositionsDirect(points);
    }

    public void SetHitEffects(IList<Vector3> points, IList<bool> reflectionPoints)
    {
        // Không dùng reflection joints thủ công - chỉ dùng prefab beamStart/beamEnd
    }

    public void EmitHitSpark(Vector3 hitPos, Vector2 incomingDir)
    {
        // Prefab beamEnd đã chứa hiệu ứng splash particles
    }

    // ═══════════════════════════════════════════════════════════════
    //  BEAM PREFAB LIFECYCLE & MULTI-SEGMENT RENDERING
    // ═══════════════════════════════════════════════════════════════

    private void EnsureEndpointInstances()
    {
        if (beamStartInstance == null && beamStartPrefab != null)
        {
            beamStartInstance = Instantiate(beamStartPrefab, Vector3.zero, Quaternion.identity, transform);
            beamStartInstance.SetActive(endpointsEnabled);
        }

        if (beamEndInstance == null && beamEndPrefab != null)
        {
            beamEndInstance = Instantiate(beamEndPrefab, Vector3.zero, Quaternion.identity, transform);
            beamEndInstance.SetActive(endpointsEnabled);
        }
    }

    private void DestroyBeamInstances()
    {
        for (int i = 0; i < beamMaterials.Count; i++)
        {
            if (beamMaterials[i] != null)
            {
                DestroyRuntimeObject(beamMaterials[i]);
            }
        }
        beamMaterials.Clear();

        for (int i = 0; i < beamLineInstances.Count; i++)
        {
            DestroyRuntimeObject(beamLineInstances[i]);
        }
        beamLineInstances.Clear();
        lineSegments.Clear();

        DestroyRuntimeObject(beamStartInstance);
        DestroyRuntimeObject(beamEndInstance);
        beamStartInstance = null;
        beamEndInstance = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  BEAM POSITIONS & TEXTURE
    // ═══════════════════════════════════════════════════════════════

    private void ApplyBeamPositionsDirect(IList<Vector3> points)
    {
        if (points == null || points.Count < 2 || beamLinePrefab == null)
        {
            DestroyBeamInstances();
            return;
        }

        EnsureEndpointInstances();

        int neededSegments = points.Count - 1;

        // Sinh thêm segment instance nếu chưa đủ
        while (beamLineInstances.Count < neededSegments)
        {
            GameObject segInstance = Instantiate(beamLinePrefab, Vector3.zero, Quaternion.identity, transform);
            LineRenderer lr = segInstance.GetComponent<LineRenderer>();
            if (lr == null) lr = segInstance.GetComponentInChildren<LineRenderer>();

            Material matInstance = null;
            if (lr != null)
            {
                lr.useWorldSpace = true;
                lr.sortingOrder = baseSortingOrder;
                lr.textureMode = LineTextureMode.Tile;
                // Mỗi segment là 1 đoạn 2 điểm thẳng tắp, không có góc nối miter nên triệt tiêu hoàn toàn răng cưa
                lr.numCornerVertices = 0;
                lr.numCapVertices = 0;
                lr.alignment = LineAlignment.View;

                if (lr.sharedMaterial != null)
                {
                    matInstance = new Material(lr.sharedMaterial) { hideFlags = HideFlags.DontSave };
                    lr.material = matInstance;
                }
            }

            beamLineInstances.Add(segInstance);
            lineSegments.Add(lr);
            beamMaterials.Add(matInstance);
        }

        // Cập nhật từng segment thẳng 2 điểm
        for (int i = 0; i < beamLineInstances.Count; i++)
        {
            if (i < neededSegments)
            {
                if (!beamLineInstances[i].activeSelf)
                {
                    beamLineInstances[i].SetActive(true);
                }

                LineRenderer lr = lineSegments[i];
                if (lr != null)
                {
                    lr.positionCount = 2;
                    lr.SetPosition(0, points[i]);
                    lr.SetPosition(1, points[i + 1]);

                    float segmentLength = Vector3.Distance(points[i], points[i + 1]);
                    float scale = textureLengthScale > 0.001f ? segmentLength / textureLengthScale : 1f;

                    if (beamMaterials[i] != null)
                    {
                        beamMaterials[i].mainTextureScale = new Vector2(scale, 1f);
                    }
                }
            }
            else
            {
                if (beamLineInstances[i].activeSelf)
                {
                    beamLineInstances[i].SetActive(false);
                }
            }
        }

        // Cập nhật vị trí beamStart tại điểm đầu
        if (beamStartInstance != null)
        {
            beamStartInstance.transform.position = points[0];
            if (points.Count >= 2)
            {
                beamStartInstance.transform.LookAt(points[1]);
            }
            beamStartInstance.SetActive(endpointsEnabled);
        }

        // Cập nhật vị trí beamEnd tại điểm cuối (hoặc điểm chạm đất)
        if (beamEndInstance != null)
        {
            Vector3 hitPoint = points[points.Count - 1];
            Vector3 prevPoint = points[points.Count - 2];
            Vector3 dir = (hitPoint - prevPoint).normalized;

            Vector3 endPos = hitPoint - dir * beamEndOffset;
            beamEndInstance.transform.position = endPos;
            beamEndInstance.transform.LookAt(prevPoint);
            beamEndInstance.SetActive(endpointsEnabled);
        }
    }

    private void UpdateTextureScroll()
    {
        if (beamMaterials.Count == 0) return;

        Vector2 scroll = new Vector2(Time.deltaTime * textureScrollSpeed, 0f);
        for (int i = 0; i < beamMaterials.Count; i++)
        {
            Material mat = beamMaterials[i];
            if (mat != null && i < beamLineInstances.Count && beamLineInstances[i].activeSelf)
            {
                mat.mainTextureOffset -= scroll;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  UTILITY
    // ═══════════════════════════════════════════════════════════════

    private static void DestroyRuntimeObject(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(obj))
            {
                return;
            }

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (obj != null)
                {
                    if (UnityEditor.EditorUtility.IsPersistent(obj))
                    {
                        return;
                    }
                    DestroyImmediate(obj, true);
                }
            };
#else
            DestroyImmediate(obj, true);
#endif
        }
    }
}
