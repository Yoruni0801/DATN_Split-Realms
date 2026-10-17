using System.Collections;
using UnityEngine;

public class DemoPurifyController : MonoBehaviour
{
    [Header("Nguồn phát ánh sáng")]
    [SerializeField] private Transform lightHolder;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Overlay lan sáng toàn màn hình")]
    [SerializeField] private GameObject purifyOverlay;
    [SerializeField] private Material purifyWaveMaterial;

    [Header("Các material hắc khí cần thanh tẩy")]
    [SerializeField] private Material[] darkAuraMaterials;

    [Header("Cài đặt")]
    [SerializeField] private KeyCode activateKey = KeyCode.Space;
    [SerializeField] private float duration = 1.8f;
    [SerializeField] private bool keepPurifiedAfterFinish = true;

    private Coroutine purifyRoutine;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Start()
    {
        if (purifyOverlay != null)
            purifyOverlay.SetActive(false);

        SetPurifyProgress(0f);
    }

    private void Update()
    {
        if (Input.GetKeyDown(activateKey))
        {
            PlayPurify();
        }
    }

    public void PlayPurify()
    {
        if (purifyRoutine != null)
            StopCoroutine(purifyRoutine);

        purifyRoutine = StartCoroutine(PurifyRoutine());
    }

    private IEnumerator PurifyRoutine()
    {
        if (purifyOverlay != null)
            purifyOverlay.SetActive(true);

        SetCenterFromLightHolder();
        SetPurifyProgress(0f);

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float progress = Mathf.SmoothStep(0f, 1f, t);

            SetPurifyProgress(progress);

            yield return null;
        }

        SetPurifyProgress(1f);

        yield return new WaitForSeconds(0.15f);

        if (purifyOverlay != null)
            purifyOverlay.SetActive(false);

        if (!keepPurifiedAfterFinish)
        {
            SetPurifyProgress(0f);
        }

        purifyRoutine = null;
    }

    private void SetCenterFromLightHolder()
    {
        if (purifyWaveMaterial == null || lightHolder == null)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        if (cam == null)
            return;

        Vector3 viewportPos = cam.WorldToViewportPoint(lightHolder.position);

        purifyWaveMaterial.SetVector(
            "_Center",
            new Vector4(viewportPos.x, viewportPos.y, 0f, 0f)
        );
    }

    private void SetPurifyProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (purifyWaveMaterial != null)
            purifyWaveMaterial.SetFloat("_Progress", progress);

        if (darkAuraMaterials == null)
            return;

        foreach (Material mat in darkAuraMaterials)
        {
            if (mat != null)
                mat.SetFloat("_PurifyProgress", progress);
        }
    }
}