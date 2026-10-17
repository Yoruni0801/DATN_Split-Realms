using System.Collections;
using UnityEngine;

/// <summary>Moves the player to a music pillar with the six note keys.</summary>
[DisallowMultipleComponent]
public sealed class MusicNoteJumpController : MonoBehaviour
{
    private static readonly KeyCode[] NoteKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6
    };

    [Header("Jump controls")]
    [SerializeField] private float minimumJumpDuration = 0.22f;
    [SerializeField] private float maximumJumpDuration = 0.52f;
    [SerializeField] private float secondsPerWorldUnit = 0.035f;
    [SerializeField] private float jumpArcHeight = 2.1f;

    [Header("Music VFX")]
    [SerializeField] private Color jumpTrailColor = new Color(0.35f, 0.95f, 1f, 0.9f);
    [SerializeField] private float jumpTrailWidth = 0.11f;
    [SerializeField, Range(6, 24)] private int jumpTrailSegments = 16;
    [SerializeField, Range(3, 12)] private int landingNoteCount = 6;

    private readonly SoundPillar[] pillars = new SoundPillar[6];
    private MelodyManager melodyManager;
    private Player player;
    private Rigidbody2D playerBody;
    private Coroutine jumpRoutine;
    private bool wasPlayerControlLocked;
    private bool wasBodySimulated;
    private static Material trailMaterial;

    public void Configure(MelodyManager manager)
    {
        melodyManager = manager;
        CachePillars();
    }

    private void Start()
    {
        if (melodyManager == null) melodyManager = GetComponent<MelodyManager>();
        CachePillars();
    }

    private void Update()
    {
        if (jumpRoutine != null || melodyManager == null || !melodyManager.IsPlayerTurn) return;

        for (int i = 0; i < NoteKeys.Length; i++)
        {
            if (Input.GetKeyDown(NoteKeys[i]))
            {
                TryJumpToPillar(i);
                break;
            }
        }
    }

    private void CachePillars()
    {
        for (int i = 0; i < pillars.Length; i++) pillars[i] = null;

        Transform miniGameRoot = transform.parent != null ? transform.parent : transform;
        SoundPillar[] found = miniGameRoot.GetComponentsInChildren<SoundPillar>(true);
        if (found.Length == 0)
            found = FindObjectsByType<SoundPillar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (SoundPillar pillar in found)
        {
            if (pillar != null && pillar.pillarID >= 0 && pillar.pillarID < pillars.Length)
                pillars[pillar.pillarID] = pillar;
        }
    }

    public void JumpToPillarDirectly(SoundPillar target)
    {
        if (target == null || !ResolvePlayer())
        {
            return;
        }

        if (jumpRoutine != null)
        {
            StopCoroutine(jumpRoutine);
            RestorePlayerPhysics();
        }

        jumpRoutine = StartCoroutine(JumpToPillarRoutine(target));
    }

    private void TryJumpToPillar(int noteIndex)
    {
        SoundPillar target = pillars[noteIndex];
        if (target == null)
        {
            CachePillars();
            target = pillars[noteIndex];
        }

        if (target == null || !ResolvePlayer())
        {
            Debug.LogWarning("[MusicNoteJump] Missing Player or sound pillar for this key.", this);
            return;
        }

        if (jumpRoutine != null)
        {
            StopCoroutine(jumpRoutine);
            RestorePlayerPhysics();
        }

        jumpRoutine = StartCoroutine(JumpToPillarRoutine(target));
    }

    private bool ResolvePlayer()
    {
        if (player == null) player = PlayerRegistry.Current ?? FindFirstObjectByType<Player>();
        if (player != null && playerBody == null) playerBody = player.GetComponent<Rigidbody2D>();
        return player != null;
    }

    private IEnumerator JumpToPillarRoutine(SoundPillar target)
    {
        wasPlayerControlLocked = player.IsControlLocked;
        player.SetControlLocked(true);

        if (playerBody != null)
        {
            wasBodySimulated = playerBody.simulated;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.simulated = false;
        }

        Vector3 start = player.transform.position;
        Vector3 end = target.GetNpcLandingPosition();
        end.z = start.z;
        player.FacePosition(end);

        float distance = Vector2.Distance(start, end);
        float duration = Mathf.Clamp(distance * secondsPerWorldUnit, minimumJumpDuration, maximumJumpDuration);
        float arcHeight = Mathf.Max(jumpArcHeight, distance * 0.16f);
        CreateJumpTrail(start, end, arcHeight, duration, target.glowColor);

        Vector3 baseScale = player.transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);
            Vector3 position = Vector3.Lerp(start, end, smoothT);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            player.transform.position = position;

            // A short squash at take-off, a stretch in the air, then a soft landing.
            float stretch = Mathf.Sin(t * Mathf.PI);
            float squash = Mathf.Clamp01(1f - t * 7f) + Mathf.Clamp01((t - 0.78f) * 6f);
            player.transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + squash * 0.10f - stretch * 0.05f, 1f - squash * 0.12f + stretch * 0.14f, 1f));
            yield return null;
        }

        player.transform.position = end;
        player.transform.localScale = baseScale;
        target.IgnorePlayerContactsFor(0.25f);
        RestorePlayerPhysics();
        target.InteractPillar();
        SpawnLandingBurst(target, end);
        jumpRoutine = null;
    }

    private void CreateJumpTrail(Vector3 start, Vector3 end, float arcHeight, float duration, Color pillarColor)
    {
        GameObject trailObject = new GameObject("Music Jump Arc");
        LineRenderer trail = trailObject.AddComponent<LineRenderer>();
        trail.useWorldSpace = true;
        trail.positionCount = jumpTrailSegments + 1;
        trail.widthCurve = AnimationCurve.EaseInOut(0f, 0.18f, 1f, 0.02f);
        trail.widthMultiplier = jumpTrailWidth;
        trail.numCapVertices = 4;
        trail.numCornerVertices = 3;
        trail.material = GetTrailMaterial();
        trail.colorGradient = CreateTrailGradient(Color.Lerp(jumpTrailColor, pillarColor, 0.5f));
        trail.sortingOrder = 50;

        for (int i = 0; i <= jumpTrailSegments; i++)
        {
            float t = i / (float)jumpTrailSegments;
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            position.z = start.z + 0.05f;
            trail.SetPosition(i, position);
        }

        StartCoroutine(FadeTrail(trail, duration + 0.18f));
    }

    private IEnumerator FadeTrail(LineRenderer trail, float duration)
    {
        float elapsed = 0f;
        while (trail != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(1f - elapsed / duration);
            trail.widthMultiplier = jumpTrailWidth * Mathf.SmoothStep(1f, 0f, 1f - alpha);
            yield return null;
        }

        if (trail != null) Destroy(trail.gameObject);
    }

    private void SpawnLandingBurst(SoundPillar target, Vector3 position)
    {
        if (target.noteSprite == null) return;

        for (int i = 0; i < landingNoteCount; i++)
        {
            GameObject note = new GameObject("Music Landing Spark");
            note.transform.position = position + Vector3.up * 0.1f;
            SpriteRenderer renderer = note.AddComponent<SpriteRenderer>();
            renderer.sprite = target.noteSprite;
            renderer.color = target.glowColor;
            renderer.sortingOrder = 55;
            StartCoroutine(AnimateLandingSpark(note.transform, renderer, i, landingNoteCount));
        }
    }

    private IEnumerator AnimateLandingSpark(Transform spark, SpriteRenderer renderer, int index, int count)
    {
        float angle = (360f / count) * index + Random.Range(-12f, 12f);
        Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
        float distance = Random.Range(0.65f, 1.25f);
        float duration = Random.Range(0.32f, 0.48f);
        Vector3 start = spark.position;
        float elapsed = 0f;

        while (spark != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            spark.position = start + direction * (distance * t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.35f);
            spark.localScale = Vector3.one * Mathf.Lerp(0.55f, 0.08f, t);
            Color color = renderer.color;
            color.a = 1f - t;
            renderer.color = color;
            spark.Rotate(0f, 0f, 220f * Time.unscaledDeltaTime);
            yield return null;
        }

        if (spark != null) Destroy(spark.gameObject);
    }

    private static Gradient CreateTrailGradient(Color color)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 0.55f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.16f), new GradientAlphaKey(0.48f, 0.72f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    private static Material GetTrailMaterial()
    {
        if (trailMaterial != null) return trailMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        trailMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        return trailMaterial;
    }

    private void OnDisable()
    {
        if (jumpRoutine != null) StopCoroutine(jumpRoutine);
        jumpRoutine = null;
        RestorePlayerPhysics();
    }

    private void RestorePlayerPhysics()
    {
        if (playerBody != null)
        {
            playerBody.simulated = wasBodySimulated;
            playerBody.linearVelocity = Vector2.zero;
        }

        if (player != null) player.SetControlLocked(wasPlayerControlLocked);
    }
}
