using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RotaGhostTrailController : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 0.065f;
    [SerializeField] private float minMoveSpeed = 2.0f;
    [SerializeField] private float fadeDuration = 0.32f;
    [SerializeField] private AnimationCurve alphaFadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Map Materials")]
    [SerializeField] private Material map1GhostMaterial; // Violet
    [SerializeField] private Material map2GhostMaterial; // Cyan
    [SerializeField] private Material map3GhostMaterial; // Amber

    [Header("Map Stage Transforms")]
    [SerializeField] private Transform stage1Root;
    [SerializeField] private Transform stage2Root;
    [SerializeField] private Transform stage3Root;

    private Player boundPlayer;
    private PlayerRotaSurfaceController rotaSurfaceController;
    private MeshRenderer playerMeshRenderer;
    private MeshFilter playerMeshFilter;
    private SpriteRenderer playerSpriteRenderer;

    private float timer;
    private Vector3 lastPlayerPos;

    private void OnEnable()
    {
        RotaSurfaceZone.PlayerEnteredAnyZone += HandlePlayerEntered;
        RotaSurfaceZone.PlayerExitedAllZones += HandlePlayerExited;
    }

    private void OnDisable()
    {
        RotaSurfaceZone.PlayerEnteredAnyZone -= HandlePlayerEntered;
        RotaSurfaceZone.PlayerExitedAllZones -= HandlePlayerExited;
        boundPlayer = null;
    }

    private void HandlePlayerEntered(Player player)
    {
        if (player == null) return;
        boundPlayer = player;
        rotaSurfaceController = player.GetComponent<PlayerRotaSurfaceController>();

        playerMeshRenderer = player.GetComponentInChildren<MeshRenderer>();
        playerMeshFilter = player.GetComponentInChildren<MeshFilter>();
        playerSpriteRenderer = player.GetComponentInChildren<SpriteRenderer>();

        lastPlayerPos = player.transform.position;
        timer = 0f;
    }

    private void HandlePlayerExited()
    {
        boundPlayer = null;
        rotaSurfaceController = null;
        playerMeshRenderer = null;
        playerMeshFilter = null;
        playerSpriteRenderer = null;
    }

    private void Update()
    {
        if (boundPlayer == null) return;

        // Nếu Player không còn ở trong trạng thái ROTA hoặc controller bị tắt thì ngắt ngay lập tức
        if (!boundPlayer.IsRotaModeActive || (rotaSurfaceController != null && !rotaSurfaceController.IsActive))
        {
            HandlePlayerExited();
            return;
        }

        Vector3 currentPos = boundPlayer.transform.position;
        float moveDistance = Vector3.Distance(currentPos, lastPlayerPos);
        float currentSpeed = moveDistance / Mathf.Max(0.0001f, Time.deltaTime);

        bool isJumpingOrMovingFast = currentSpeed >= minMoveSpeed;
        if (rotaSurfaceController != null && !rotaSurfaceController.IsGrounded)
        {
            isJumpingOrMovingFast = true; // Luôn tạo vệt mờ khi đang bay nhảy ngược trọng lực
        }

        timer += Time.deltaTime;
        if (timer >= spawnInterval && isJumpingOrMovingFast)
        {
            timer = 0f;
            SpawnGhostSnapshot();
        }

        lastPlayerPos = currentPos;
    }

    private int GetCurrentActiveStage()
    {
        if (stage3Root != null && stage3Root.gameObject.activeInHierarchy) return 3;
        if (stage2Root != null && stage2Root.gameObject.activeInHierarchy) return 2;
        return 1;
    }

    private Material GetCurrentGhostMaterial()
    {
        int stage = GetCurrentActiveStage();
        if (stage == 3 && map3GhostMaterial != null) return map3GhostMaterial;
        if (stage == 2 && map2GhostMaterial != null) return map2GhostMaterial;
        return map1GhostMaterial;
    }

    private void SpawnGhostSnapshot()
    {
        if (boundPlayer == null) return;

        Material ghostMat = GetCurrentGhostMaterial();
        if (ghostMat == null) return;

        GameObject ghostObj = new GameObject("RotaGhost_Snapshot");
        ghostObj.transform.position = boundPlayer.transform.position;
        ghostObj.transform.rotation = boundPlayer.transform.rotation;
        ghostObj.transform.localScale = boundPlayer.transform.lossyScale;

        // Trường hợp Player dùng Mesh (Spine SkeletonAnimation)
        if (playerMeshRenderer != null && playerMeshFilter != null && playerMeshFilter.sharedMesh != null)
        {
            MeshFilter mf = ghostObj.AddComponent<MeshFilter>();
            mf.sharedMesh = playerMeshFilter.sharedMesh;

            MeshRenderer mr = ghostObj.AddComponent<MeshRenderer>();
            mr.material = new Material(ghostMat);
            mr.sortingLayerID = playerMeshRenderer.sortingLayerID;
            mr.sortingOrder = playerMeshRenderer.sortingOrder - 1;

            GhostInstance inst = ghostObj.AddComponent<GhostInstance>();
            inst.Init(mr.material, fadeDuration, alphaFadeCurve);
        }
        // Trường hợp Player dùng SpriteRenderer
        else if (playerSpriteRenderer != null && playerSpriteRenderer.sprite != null)
        {
            SpriteRenderer sr = ghostObj.AddComponent<SpriteRenderer>();
            sr.sprite = playerSpriteRenderer.sprite;
            sr.material = new Material(ghostMat);
            sr.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            sr.sortingOrder = playerSpriteRenderer.sortingOrder - 1;
            sr.flipX = playerSpriteRenderer.flipX;
            sr.flipY = playerSpriteRenderer.flipY;

            GhostInstance inst = ghostObj.AddComponent<GhostInstance>();
            inst.Init(sr.material, fadeDuration, alphaFadeCurve);
        }
        else
        {
            Destroy(ghostObj);
        }
    }

    private class GhostInstance : MonoBehaviour
    {
        private Material mat;
        private float duration;
        private AnimationCurve curve;
        private float elapsed;
        private static readonly int AlphaProp = Shader.PropertyToID("_AlphaMultiplier");

        public void Init(Material material, float fadeDuration, AnimationCurve fadeCurve)
        {
            mat = material;
            duration = fadeDuration;
            curve = fadeCurve;
            elapsed = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = curve != null ? curve.Evaluate(t) : (1f - t);

            if (mat != null)
            {
                mat.SetFloat(AlphaProp, alpha);
            }

            if (elapsed >= duration)
            {
                if (mat != null) Destroy(mat);
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
